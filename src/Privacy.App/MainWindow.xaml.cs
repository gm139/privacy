using System.Globalization;
using System.Windows;
using Privacy.Data;
using Privacy.Domain;

namespace Privacy.App;

public sealed record RequestRow(Guid Id, string Reference, string Status, string Received, string Due, string Situation);
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
