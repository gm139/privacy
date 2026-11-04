using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using Privacy.Data;
using Privacy.Domain;

namespace Privacy.App;

public sealed record RequestRow(Guid Id, string Reference, string Status, string Received, string Due, string Situation);
public sealed record PolicyGridRow(Guid Id, string Class, string Trigger, string Duration, string Status, string Review);
public sealed record ExpiredGridRow(ExpiringItem Item, string Type, string Reference, string BaseDate, string ExpiredOn, string Reason);
public sealed record ReviewGridRow(string Class, string Kind, string Criterion, string Review);
public sealed record BackupGridRow(string Started, string Status, string Verified, string File);
public sealed record ActivityGridRow(Guid VersionId, string Code, string Name, string Version, string Status, string From, string Review);
public sealed record SubjectRow(Guid Id, string Code, string First, string Last, string Birth, string Status);
public sealed record NoticeRow(string Title, string Version, string From);
public sealed record IncidentRow(Guid Id, string Reference, string Status, string Classification, string Deadline, string Remaining);

public partial class MainWindow : Window
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
    private static readonly CultureInfo It = new("it-IT");
    private readonly AppServices _services;
    private readonly Guid _practiceId, _userId;

    public int RequestCount => RequestsGrid.Items.Count;
    public int IncidentCount => IncidentsGrid.Items.Count;
    public int PolicyCount => PoliciesGrid.Items.Count;
    public int ExpiredCount => ExpiredGrid.Items.Count;
    public int BackupRunCount => BackupGrid.Items.Count;
    public string BackupStatusText => BackupStatus.Text;
    private IReadOnlyList<PolicyRow> _policies = Array.Empty<PolicyRow>();
    private Dictionary<string, string> _labels = new();
    public int ActivityCount => ActivitiesGrid.Items.Count;
    private IReadOnlyList<ActivityRow> _activities = Array.Empty<ActivityRow>();
    public int SubjectCount => SubjectsGrid.Items.Count;
    public int NoticeCount => NoticesGrid.Items.Count;
    private IReadOnlyList<SubjectRecord> _subjects = Array.Empty<SubjectRecord>();

    public MainWindow(AppServices services, Guid practiceId, Guid userId, string displayName)
    {
        InitializeComponent();
        _services = services; _practiceId = practiceId; _userId = userId;
        HeaderText.Text = $"Scadenze - {displayName}";
        Loaded += async (_, _) => await LoadAsync();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void OnNewRequest(object sender, RoutedEventArgs e)
    {
        try
        {
            var cat = new CatalogService(_services.Db);
            var w = new NewRequestWindow(_services, _practiceId, _userId, await cat.GetAsync("cat_requester_capacity"),
                await cat.GetAsync("cat_request_channel"), await cat.GetAsync("cat_right")) { Owner = this };
            if (w.ShowDialog() == true) await LoadAsync();
        }
        catch (Exception) { StatusText.Text = "Impossibile aprire il modulo. Verificare il database e riprovare."; }
    }

    private async void OnNewIncident(object sender, RoutedEventArgs e)
    {
        try
        {
            var w = new NewIncidentWindow(_services, _practiceId, _userId, await new CatalogService(_services.Db).GetAsync("cat_incident_classification")) { Owner = this };
            if (w.ShowDialog() == true) await LoadAsync();
        }
        catch (Exception) { StatusText.Text = "Impossibile aprire il modulo. Verificare il database e riprovare."; }
    }

    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        if (Tabs.SelectedIndex == 0 && RequestsGrid.SelectedItem is RequestRow r)
        { new RequestDetailWindow(_services, _practiceId, _userId, r.Id) { Owner = this }.ShowDialog(); await LoadAsync(); }
        else if (Tabs.SelectedIndex == 1 && IncidentsGrid.SelectedItem is IncidentRow i)
        { new IncidentDetailWindow(_services, _userId, i.Id) { Owner = this }.ShowDialog(); await LoadAsync(); }
        else StatusText.Text = "Selezionare prima una riga dell'elenco.";
    }

    public async Task LoadAsync()
    {
        RefreshButton.IsEnabled = false;
        StatusText.Text = "";
        try
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Rome).DateTime);
            var requests = await _services.Requests.ListDueAsync(_practiceId, today, 30);
            RequestsGrid.ItemsSource = requests.Select(r => new RequestRow(r.Id, r.ReferenceCode, StatusLabel(r.StatusCode),
                r.ReceivedAt.ToOffset(Rome.GetUtcOffset(r.ReceivedAt)).ToString("dd/MM/yyyy", It), r.EffectiveDueOn.ToString("dd/MM/yyyy", It),
                Situation(r.EffectiveDueOn, today, r.EffectiveDueOn != r.InitialDueOn))).ToList();

            var now = DateTimeOffset.UtcNow;
            var incidents = await _services.Incidents.ListOpenAsync(_practiceId);
            await LoadPatientsAsync();
            await LoadRegistryAsync(today);
            await LoadRetentionAsync(today);
            await LoadBackupAsync();
            IncidentsGrid.ItemsSource = incidents.Select(i => new IncidentRow(i.Id, i.ReferenceCode, i.StatusCode, ClassLabel(i.ClassificationCode),
                i.AwarenessAt is null ? "Consapevolezza non ancora registrata" : TimeZoneInfo.ConvertTime(IncidentClock.Deadline(i.AwarenessAt.Value), Rome).ToString("dd/MM/yyyy HH:mm", It),
                i.AwarenessAt is null ? "-" : RemainingText(IncidentClock.Remaining(i.AwarenessAt.Value, now)))).ToList();
        }
        catch (Exception)
        {
            StatusText.Text = "Impossibile leggere i dati. Verificare che il database sia attivo e riprovare.";
        }
        finally { RefreshButton.IsEnabled = true; }
    }

    private string L(string code) => _labels.TryGetValue(code, out var l) ? l : code;

    public async Task LoadRetentionAsync(DateOnly today)
    {
        foreach (var t in new[] { "cat_record_class", "cat_retention_trigger", "cat_duration_kind" })
            foreach (var i in await _services.Catalog.GetAsync(t)) _labels[i.Code] = i.Label;
        _policies = await _services.Retention.ListPoliciesAsync(_practiceId);
        PoliciesGrid.ItemsSource = _policies.Select(p => new PolicyGridRow(p.Id, L(p.RecordClassCode), L(p.TriggerCode),
            p.DurationKindCode == "fixed" ? $"{(p.Months is > 0 ? p.Months + " mesi" : "")}{(p.Months is > 0 && p.Days is > 0 ? " + " : "")}{(p.Days is > 0 ? p.Days + " giorni" : "")}".Trim() : L(p.DurationKindCode),
            p.StatusCode == "draft" ? "Bozza" : "Approvata", p.ReviewDueOn.ToString("dd/MM/yyyy", It))).ToList();
        var expired = await _services.Retention.ListExpiredAsync(_practiceId, today);
        ExpiredGrid.ItemsSource = expired.Select(x => new ExpiredGridRow(x, x.ItemType switch { "rights_request" => "Richiesta dei diritti", "incident" => "Incidente", "notice_delivery" => "Consegna di informativa", _ => x.ItemType },
            x.Reference, x.BaseDate.ToString("dd/MM/yyyy", It), x.ExpiredOn.ToString("dd/MM/yyyy", It), x.Reason)).ToList();
        ReviewGrid.ItemsSource = (await _services.Retention.ListManualReviewAsync(_practiceId, today))
            .Select(m => new ReviewGridRow(L(m.RecordClassCode), L(m.DurationKindCode), m.Criterion, m.ReviewDueOn.ToString("dd/MM/yyyy", It))).ToList();
    }

    public async Task LoadBackupAsync()
    {
        var runs = await _services.Backup.ListRunsAsync();
        BackupGrid.ItemsSource = runs.Select(r => new BackupGridRow(r.StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", It),
            r.StatusCode switch { "ok" => "Riuscito", "failed" => "Fallito", _ => "In corso" }, r.VerifiedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", It) ?? "", r.FileName ?? "")).ToList();
        BackupStatus.Text = BackupAdvice.Status(runs, DateTimeOffset.UtcNow, _services.CanBackup);
        BackupNowButton.IsEnabled = _services.CanBackup;
    }

    private async void OnNewPolicy(object sender, RoutedEventArgs e)
    {
        try
        {
            var cat = _services.Catalog;
            var w = new NewPolicyWindow(_services, _practiceId, _userId, await cat.GetAsync("cat_record_class"), await cat.GetAsync("cat_retention_trigger"), await cat.GetAsync("cat_duration_kind")) { Owner = this };
            if (w.ShowDialog() == true) await LoadAsync();
        }
        catch (Exception) { StatusText.Text = "Impossibile aprire il modulo."; }
    }

    private async void OnApprovePolicy(object sender, RoutedEventArgs e)
    {
        if (PoliciesGrid.SelectedItem is not PolicyGridRow row) { StatusText.Text = "Selezionare una regola."; return; }
        if (_policies.First(p => p.Id == row.Id).StatusCode != "draft") { StatusText.Text = "La regola è già approvata."; return; }
        if (MessageBox.Show(this, "Approvare la regola? Da questo momento determina quali elementi risultano scaduti.", "Approvazione", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { await _services.Retention.ApproveAsync(Guid.NewGuid(), row.Id, _userId); await LoadAsync(); }
        catch (Npgsql.PostgresException ex) { StatusText.Text = DialogForm.FriendlyDb(ex); }
        catch (Exception) { StatusText.Text = "Approvazione non riuscita."; }
    }

    private async void OnDisposeSelected(object sender, RoutedEventArgs e)
    {
        var items = ExpiredGrid.SelectedItems.OfType<ExpiredGridRow>().Select(r => r.Item).ToList();
        if (items.Count == 0) { StatusText.Text = "Selezionare uno o più elementi scaduti."; return; }
        var w = new DisposeItemsWindow(_services, _userId, items) { Owner = this };
        if (w.ShowDialog() == true) { MessageBox.Show(this, $"Eliminati {w.Disposed} elementi.", "Eliminazione", MessageBoxButton.OK, MessageBoxImage.Information); await LoadAsync(); }
    }

    private async void OnBackupNow(object sender, RoutedEventArgs e)
    {
        BackupNowButton.IsEnabled = false; StatusText.Text = "Backup in corso…";
        try
        {
            var r = await _services.Backup.BackupAsync(KeyStore.GetOrCreateBackupKey(), KeyStore.BackupKeyVersion);
            StatusText.Text = "";
            MessageBox.Show(this, $"Backup completato e verificato.\n{r.FileName}\n{(r.ExternalPath is null ? "ATTENZIONE: nessuna copia esterna configurata (externalBackupDir)." : "Copia esterna salvata.")}", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception) { StatusText.Text = "Backup non riuscito. Controllare la configurazione degli strumenti PostgreSQL e lo spazio disponibile."; }
        finally { try { await LoadBackupAsync(); } catch (Exception) { } }
    }

    private void OnVerifyBackup(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Backup Privacy Studio (*.psbk)|*.psbk" };
        if (dlg.ShowDialog(this) != true) return;
        try { BackupService.VerifyFile(dlg.FileName, KeyStore.GetOrCreateBackupKey(), KeyStore.BackupKeyVersion); StatusText.Foreground = System.Windows.Media.Brushes.DarkGreen; StatusText.Text = "Il file si decifra ed è una copia valida."; }
        catch (BackupFormatException ex) { StatusText.Foreground = System.Windows.Media.Brushes.Firebrick; StatusText.Text = "File non valido: " + ex.Message + "."; }
        catch (Exception) { StatusText.Foreground = System.Windows.Media.Brushes.Firebrick; StatusText.Text = "Impossibile leggere il file."; }
    }

    private void OnExportKeys(object sender, RoutedEventArgs e)
    {
        var w = new ExportKeysWindow { Owner = this };
        if (w.ShowDialog() != true || w.Result is null) return;
        var dlg = new SaveFileDialog { Filter = "Chiavi di recupero (*.pskr)|*.pskr", FileName = $"chiavi-recupero-{DateTime.Today:yyyy-MM-dd}.pskr" };
        if (dlg.ShowDialog(this) == true) { System.IO.File.WriteAllBytes(dlg.FileName, w.Result); MessageBox.Show(this, "File salvato. Conservarlo fuori dal PC, separato dalla frase segreta.", "Chiavi di recupero"); }
    }

    private void OnImportKeys(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Chiavi di recupero (*.pskr)|*.pskr" };
        if (dlg.ShowDialog(this) != true) return;
        if (new ImportKeysWindow(System.IO.File.ReadAllBytes(dlg.FileName)) { Owner = this }.ShowDialog() == true)
            MessageBox.Show(this, "Chiavi reinstallate.", "Chiavi di recupero");
    }

    public async Task LoadRegistryAsync(DateOnly today)
    {
        _activities = await _services.Registry.ListActivitiesAsync(_practiceId);
        ActivitiesGrid.ItemsSource = _activities.Select(a => new ActivityGridRow(a.VersionId, a.Code, a.Name, a.VersionNumber.ToString(), StatusOf(a, today),
            a.EffectiveFrom?.ToString("dd/MM/yyyy", It) ?? "", a.ReviewDueOn?.ToString("dd/MM/yyyy", It) ?? "")).ToList();
    }

    private static string StatusOf(ActivityRow a, DateOnly today) => a.StatusCode switch
    {
        "draft" => "Bozza", "in_review" => "In verifica", "superseded" => "Superata",
        "published" => a.ReviewDueOn is { } r && r < today ? "Pubblicata - riesame scaduto" : "Pubblicata", _ => a.StatusCode
    };

    private ActivityRow? SelectedActivity() => ActivitiesGrid.SelectedItem is ActivityGridRow r ? _activities.FirstOrDefault(a => a.VersionId == r.VersionId) : null;

    private async void OnNewActivity(object sender, RoutedEventArgs e)
    {
        var w = new NewActivityWindow(_services, _practiceId, _userId) { Owner = this };
        if (w.ShowDialog() == true && w.CreatedVersionId is { } v)
        { new ActivityEditorWindow(_services, _practiceId, _userId, v) { Owner = this }.ShowDialog(); await LoadAsync(); }
    }

    private async void OnOpenActivity(object sender, RoutedEventArgs e)
    {
        if (SelectedActivity() is not { } a) { StatusText.Text = "Selezionare un'attività."; return; }
        new ActivityEditorWindow(_services, _practiceId, _userId, a.VersionId) { Owner = this }.ShowDialog();
        await LoadAsync();
    }

    private async void OnNewRevision(object sender, RoutedEventArgs e)
    {
        if (SelectedActivity() is not { } a) { StatusText.Text = "Selezionare un'attività."; return; }
        if (a.StatusCode != "published") { StatusText.Text = "Una nuova revisione si crea a partire da una revisione pubblicata."; return; }
        var f = new DialogForm("Nuova revisione") { Owner = this };
        var reason = f.AddText("_Motivo della modifica", multiline: true);
        f.AddNote("La nuova revisione parte da una copia di quella pubblicata, che resta in vigore finché non pubblichi la nuova.");
        Guid created = default;
        f.Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(reason.Text)) return "Indicare il motivo della modifica.";
            created = await _services.Registry.NewRevisionAsync(a.VersionId, _userId, reason.Text.Trim());
            return null;
        };
        if (f.ShowDialog() == true)
        { new ActivityEditorWindow(_services, _practiceId, _userId, created) { Owner = this }.ShowDialog(); await LoadAsync(); }
    }

    private async void OnExportRegistry(object sender, RoutedEventArgs e)
    {
        var f = new DialogForm("Esporta il registro") { Owner = this };
        var date = f.AddDate("Alla _data", DateOnly.FromDateTime(DateTime.Today));
        string? text = null; DateOnly asOf = default;
        f.Submit = async () =>
        {
            if (date.SelectedDate is not { } d) return "Indicare la data.";
            asOf = DateOnly.FromDateTime(d);
            var export = await _services.Registry.ExportAsOfAsync(_practiceId, asOf);
            var labels = new Dictionary<string, string>();
            foreach (var t in new[] { "cat_consent_assessment", "cat_article6", "cat_article9", "cat_data_category", "cat_subject_category", "cat_recipient_category" })
                foreach (var i in await _services.Catalog.GetAsync(t)) labels[i.Code] = i.Label;
            await using var c = await _services.Db.OpenAsync();
            await using var q = new Npgsql.NpgsqlCommand("SELECT name FROM ps.practices WHERE id = $1", c);
            q.Parameters.AddWithValue(_practiceId);
            text = RegistryExportFormatter.Format(export, asOf, (string?)await q.ExecuteScalarAsync() ?? "", labels);
            return null;
        };
        if (f.ShowDialog() == true && text is not null) new RegistryExportWindow(text, asOf) { Owner = this }.ShowDialog();
    }

    public async Task LoadPatientsAsync()
    {
        _subjects = await _services.Subjects.SearchAsync(_practiceId, SearchBox.Text);
        SubjectsGrid.ItemsSource = _subjects.Select(x => new SubjectRow(x.Id, x.InternalCode, x.FirstName, x.LastName,
            x.BirthDate?.ToString("dd/MM/yyyy", It) ?? "", x.StatusCode == "removed" ? "Rimosso" : "")).ToList();
        var notices = await _services.Notices.ListPublishedAsync(_practiceId);
        NoticesGrid.ItemsSource = notices.Select(n => new NoticeRow(n.Title, n.VersionNumber.ToString(), n.EffectiveFrom?.ToString("dd/MM/yyyy", It) ?? "")).ToList();
    }

    private async void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        try { await LoadPatientsAsync(); } catch (Exception) { StatusText.Text = "Ricerca non riuscita."; }
    }

    private SubjectRecord? Selected() => SubjectsGrid.SelectedItem is SubjectRow r ? _subjects.FirstOrDefault(x => x.Id == r.Id) : null;

    private async void OnNewSubject(object sender, RoutedEventArgs e)
    {
        if (new SubjectFormWindow(_services, _practiceId, _userId, null) { Owner = this }.ShowDialog() == true) await LoadAsync();
    }

    private async void OnRectifySubject(object sender, RoutedEventArgs e)
    {
        if (Selected() is not { } s) { StatusText.Text = "Selezionare un interessato."; return; }
        if (s.StatusCode == "removed") { StatusText.Text = "L'interessato è stato rimosso: non è modificabile."; return; }
        if (new SubjectFormWindow(_services, _practiceId, _userId, s) { Owner = this }.ShowDialog() == true) await LoadAsync();
    }

    private async void OnRemoveSubject(object sender, RoutedEventArgs e)
    {
        if (Selected() is not { } s) { StatusText.Text = "Selezionare un interessato."; return; }
        if (s.StatusCode == "removed") { StatusText.Text = "L'interessato è già stato rimosso."; return; }
        if (new RemoveSubjectWindow(_services, _userId, s) { Owner = this }.ShowDialog() == true) await LoadAsync();
    }

    private async void OnDeliver(object sender, RoutedEventArgs e)
    {
        if (Selected() is not { } s) { StatusText.Text = "Selezionare un interessato."; return; }
        if (s.StatusCode == "removed") { StatusText.Text = "L'interessato è stato rimosso."; return; }
        try
        {
            var notices = await _services.Notices.ListPublishedAsync(_practiceId);
            if (notices.Count == 0) { StatusText.Text = "Non ci sono informative pubblicate: crearne una dalla scheda «Informative»."; return; }
            var w = new DeliveryWindow(_services, _userId, s, notices, await _services.Catalog.GetAsync("cat_delivery_channel"), await _services.Catalog.GetAsync("cat_receipt_status")) { Owner = this };
            if (w.ShowDialog() == true) MessageBox.Show(this, $"Consegna registrata.\nCodice: {w.CreatedCode}", "Consegna", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception) { StatusText.Text = "Impossibile aprire il modulo di consegna."; }
    }

    private void OnHistory(object sender, RoutedEventArgs e)
    {
        if (Selected() is not { } s) { StatusText.Text = "Selezionare un interessato."; return; }
        new DeliveriesHistoryWindow(_services, s) { Owner = this }.ShowDialog();
    }

    private async void OnNewNotice(object sender, RoutedEventArgs e)
    {
        try
        {
            var activities = await _services.Registry.ListPublishedAsync(_practiceId);
            if (activities.Count == 0) { StatusText.Text = "Il registro non ha attività pubblicate: un'informativa va collegata ad almeno una."; return; }
            if (new NewNoticeWindow(_services, _practiceId, _userId, activities) { Owner = this }.ShowDialog() == true) await LoadAsync();
        }
        catch (Exception) { StatusText.Text = "Impossibile aprire il modulo."; }
    }

    private static string Situation(DateOnly due, DateOnly today, bool extended)
    {
        var days = due.DayNumber - today.DayNumber;
        var text = days < 0 ? $"Scaduta da {-days} giorni" : days == 0 ? "Scade oggi" : $"Mancano {days} giorni";
        return extended ? text + " (prorogata)" : text;
    }

    private static string RemainingText(TimeSpan t) =>
        t < TimeSpan.Zero ? $"SCADUTA da {(int)(-t).TotalHours} ore" : $"{(int)t.TotalHours} ore {t.Minutes} minuti";

    private static string StatusLabel(string code) => code switch
    {
        "received" => "Ricevuta", "verification" => "In verifica", "inquiry" => "In istruttoria", "response" => "Riscontro in preparazione", _ => code
    };

    private static string ClassLabel(string code) => code switch
    {
        "to_assess" => "Da valutare", "technical" => "Incidente tecnico", "breach" => "Violazione di dati personali", _ => code
    };
}
