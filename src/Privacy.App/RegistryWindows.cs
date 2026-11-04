using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Privacy.Data;

namespace Privacy.App;

/// <summary>Nuova attività di trattamento: crea l'attività e la sua prima revisione in bozza.</summary>
public sealed class NewActivityWindow : DialogForm
{
    public TextBox Code { get; } public TextBox ActivityName { get; }
    public Guid? CreatedVersionId { get; private set; }
    public NewActivityWindow(AppServices s, Guid practiceId, Guid userId) : base("Nuova attività di trattamento")
    {
        var cmd = Guid.NewGuid();
        Code = AddText("_Codice (es. A1)");
        ActivityName = AddText("_Nome dell'attività (es. Cura dei pazienti)");
        AddNote("L'attività nasce in bozza: finalità, basi giuridiche e categorie si completano nella schermata successiva. Nel registro non vanno le singole visite dei pazienti.");
        Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(Code.Text) || string.IsNullOrWhiteSpace(ActivityName.Text)) return "Codice e nome sono obbligatori.";
            CreatedVersionId = await s.Registry.CreateActivityAsync(cmd, practiceId, userId, Code.Text.Trim(), ActivityName.Text.Trim());
            return null;
        };
    }
}

/// <summary>Finalità con il suo primo presupposto di liceità, oppure solo un presupposto aggiuntivo per una finalità esistente.</summary>
public sealed class PurposeWindow : DialogForm
{
    public TextBox PurposeName { get; } public TextBox Description { get; } public TextBox Assessment { get; }
    public ComboBox Consent { get; } public ComboBox Article6 { get; } public ComboBox Article9 { get; }
    public TextBox NationalReference { get; } public TextBox Scope { get; }

    public PurposeWindow(AppServices s, Guid userId, Guid versionId, Guid? existingPurposeId,
        IReadOnlyList<CatalogItem> consent, IReadOnlyList<CatalogItem> art6, IReadOnlyList<CatalogItem> art9)
        : base(existingPurposeId is null ? "Nuova finalità" : "Nuovo presupposto di liceità", 580)
    {
        PurposeName = new TextBox(); Description = new TextBox(); Assessment = new TextBox(); Consent = new ComboBox();
        if (existingPurposeId is null)
        {
            PurposeName = AddText("_Nome della finalità (es. Cura e assistenza)");
            Description = AddText("_Descrizione", multiline: true);
            Assessment = AddText("_Valutazione giuridica del titolare", multiline: true);
            Consent = AddCombo("Valutazione sul _consenso", consent);
        }
        Article6 = AddCombo("Base giuridica (art. _6)", art6);
        Article9 = AddCombo("Condizione per i dati particolari (art. _9), se trattati", new[] { new CatalogItem("", "— non applicabile —") }.Concat(art9));
        NationalReference = AddText("Riferimento normativo nazionale (facoltativo)");
        Scope = AddText("A quale parte della finalità si applica");
        var cmd = Guid.NewGuid();
        Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(Scope.Text)) return "Indicare a quale parte della finalità si applica la base giuridica.";
            var a9 = Article9.SelectedValue as string;
            var ground = new GroundExport(Sel(Article6), string.IsNullOrEmpty(a9) ? null : a9,
                string.IsNullOrWhiteSpace(NationalReference.Text) ? null : NationalReference.Text.Trim(), Scope.Text.Trim());
            if (existingPurposeId is { } pid)
            {
                await s.Registry.AddGroundAsync(pid, userId, ground, Scope.Text.Trim());
                return null;
            }
            if (string.IsNullOrWhiteSpace(PurposeName.Text)) return "Il nome della finalità è obbligatorio.";
            if (string.IsNullOrWhiteSpace(Assessment.Text)) return "La valutazione giuridica è obbligatoria.";
            await s.Registry.AddPurposeAsync(versionId, userId, PurposeName.Text.Trim(), string.IsNullOrWhiteSpace(Description.Text) ? null : Description.Text.Trim(),
                Assessment.Text.Trim(), Sel(Consent), new[] { ground });
            return null;
        };
    }
}

/// <summary>Editor di una revisione del registro: dati, categorie, finalità; invio in verifica e pubblicazione.</summary>
public sealed class ActivityEditorWindow : Window
{
    private readonly AppServices _s; private readonly Guid _practice, _user, _versionId;
    private readonly TextBlock _header = new() { FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _description = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70 };
    private readonly DatePicker _review = new();
    private readonly StackPanel _categories = new();
    private readonly ListBox _purposes = new() { MinHeight = 110 };
    private readonly TextBlock _status = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button _save = new() { Content = "_Salva dati e categorie" }, _addPurpose = new() { Content = "Aggiungi _finalità…" },
        _addGround = new() { Content = "Aggiungi pre_supposto…" }, _removePurpose = new() { Content = "_Rimuovi finalità" },
        _submit = new() { Content = "_Invia in verifica" }, _publish = new() { Content = "_Pubblica…" };
    private readonly Dictionary<string, List<(CheckBox Box, string Code)>> _checks = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);   // Loaded e chiamate manuali non devono caricare in parallelo
    private VersionDetail? _detail;
    private IReadOnlyDictionary<string, string> _labels = new Dictionary<string, string>();

    public string HeaderText => _header.Text;
    public string StatusText => _status.Text;
    internal TextBox DescriptionBox => _description;
    internal DatePicker ReviewPicker => _review;
    internal void Check(string table, string code) { foreach (var (box, c) in _checks[table]) if (c == code) box.IsChecked = true; }
    public ActivityEditorWindow(AppServices s, Guid practiceId, Guid userId, Guid versionId)
    {
        _s = s; _practice = practiceId; _user = userId; _versionId = versionId;
        Title = "Attività del registro"; Width = 820; Height = 760; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(_header);
        panel.Children.Add(new Label { Content = "_Descrizione dell'attività", Target = _description, Padding = new Thickness(0, 10, 0, 2) }); panel.Children.Add(_description);
        panel.Children.Add(new Label { Content = "_Riesame previsto entro", Target = _review, Padding = new Thickness(0, 10, 0, 2) }); panel.Children.Add(_review);
        panel.Children.Add(_categories);
        panel.Children.Add(new TextBlock { Text = "Finalità", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 2) }); panel.Children.Add(_purposes);
        var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var b in new[] { _addPurpose, _addGround, _removePurpose }) { b.Margin = new Thickness(0, 0, 8, 0); b.Padding = new Thickness(12, 6, 12, 6); row1.Children.Add(b); }
        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        foreach (var b in new[] { _save, _submit, _publish }) { b.Margin = new Thickness(0, 0, 8, 0); b.Padding = new Thickness(12, 6, 12, 6); row2.Children.Add(b); }
        panel.Children.Add(row1); panel.Children.Add(row2); panel.Children.Add(_status);
        Content = new ScrollViewer { Content = panel };
        System.Windows.Automation.AutomationProperties.SetName(_description, "Descrizione dell'attività");
        System.Windows.Automation.AutomationProperties.SetName(_purposes, "Finalità");
        _save.Click += async (_, _) => await Guard(SaveAsync);
        _addPurpose.Click += async (_, _) => await Guard(OnAddPurpose);
        _addGround.Click += async (_, _) => await Guard(OnAddGround);
        _removePurpose.Click += async (_, _) => await Guard(OnRemovePurpose);
        _submit.Click += async (_, _) => await Guard(OnSubmitReview);
        _publish.Click += async (_, _) => await Guard(OnPublish);
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task Guard(Func<Task> action)
    {
        try { _status.Text = ""; await action(); }
        catch (Npgsql.PostgresException e) { _status.Text = DialogForm.FriendlyDb(e); }
        catch (FormatException e) { _status.Text = e.Message; }
        catch (Exception) { _status.Text = "Operazione non riuscita. Verificare il database e riprovare."; }
    }

    public async Task LoadAsync()
    {
        await _loadLock.WaitAsync();
        try { await LoadCoreAsync(); } finally { _loadLock.Release(); }
    }

    private async Task LoadCoreAsync()
    {
        await Guard(async () =>
        {
            _detail = await _s.Registry.GetVersionAsync(_versionId);
            if (_detail is null) { _status.Text = "Revisione non trovata."; return; }
            var d = _detail;
            if (_checks.Count == 0)
            {
                foreach (var (table, title) in new[] { ("cat_data_category", "Categorie di dati"), ("cat_subject_category", "Categorie di interessati"), ("cat_recipient_category", "Categorie di destinatari") })
                {
                    var items = await _s.Catalog.GetAsync(table);
                    _categories.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2) });
                    var list = new List<(CheckBox, string)>();
                    foreach (var i in items) { var cb = new CheckBox { Content = i.Label, Margin = new Thickness(0, 1, 0, 1) }; _categories.Children.Add(cb); list.Add((cb, i.Code)); }
                    _checks[table] = list;
                }
                var all = new Dictionary<string, string>();
                foreach (var t in new[] { "cat_consent_assessment", "cat_article6", "cat_article9", "cat_data_category", "cat_subject_category", "cat_recipient_category" })
                    foreach (var i in await _s.Catalog.GetAsync(t)) all[i.Code] = i.Label;
                _labels = all;
            }
            var editable = d.StatusCode is "draft" or "in_review";
            _header.Text = $"{d.Code} - {d.Name} - revisione {d.VersionNumber} ({StatusLabel(d.StatusCode)})";
            _description.Text = d.Description ?? "";
            _review.SelectedDate = d.ReviewDueOn?.ToDateTime(TimeOnly.MinValue);
            Mark("cat_data_category", d.DataCategories); Mark("cat_subject_category", d.SubjectCategories); Mark("cat_recipient_category", d.RecipientCategories);
            _purposes.ItemsSource = d.Purposes.Select(p => $"{p.Name} - consenso: {Label(p.ConsentAssessmentCode)} - {p.Grounds.Count} presuppost{(p.Grounds.Count == 1 ? "o" : "i")}: " +
                string.Join("; ", p.Grounds.Select(g => Label(g.Article6Code) + (g.Article9Code is null ? "" : " + " + Label(g.Article9Code))))).ToList();
            foreach (var b in new[] { _save, _addPurpose, _addGround, _removePurpose }) b.IsEnabled = editable;
            _submit.IsEnabled = d.StatusCode == "draft"; _publish.IsEnabled = editable;
            _description.IsReadOnly = !editable; _review.IsEnabled = editable;
            foreach (var l in _checks.Values) foreach (var (box, _) in l) box.IsEnabled = editable;
            if (!editable) _status.Text = "Revisione pubblicata o superata: non è modificabile. Per cambiare qualcosa, creare una nuova revisione.";
        });
    }

    private string Label(string code) => _labels.TryGetValue(code, out var l) ? l : code;
    private void Mark(string table, IReadOnlyList<string> codes) { foreach (var (box, code) in _checks[table]) box.IsChecked = codes.Contains(code); }
    private static string StatusLabel(string c) => c switch { "draft" => "bozza", "in_review" => "in verifica", "published" => "pubblicata", "superseded" => "superata", _ => c };
    private IReadOnlyList<string> Checked(string table) => _checks[table].Where(x => x.Box.IsChecked == true).Select(x => x.Code).ToList();

    public async Task SaveAsync()
    {
        await _s.Registry.UpdateDraftAsync(_versionId, _user, string.IsNullOrWhiteSpace(_description.Text) ? null : _description.Text.Trim(),
            _review.SelectedDate is { } d ? DateOnly.FromDateTime(d) : null);
        await _s.Registry.SetCategoriesAsync(_versionId, _user, Checked("cat_data_category"), Checked("cat_subject_category"), Checked("cat_recipient_category"));
        await LoadAsync();
        _status.Foreground = System.Windows.Media.Brushes.DarkGreen; _status.Text = "Salvato.";
    }

    private async Task<DialogForm> BuildPurposeDialog(Guid? existing) => new PurposeWindow(_s, _user, _versionId, existing,
        await _s.Catalog.GetAsync("cat_consent_assessment"), await _s.Catalog.GetAsync("cat_article6"), await _s.Catalog.GetAsync("cat_article9")) { Owner = this };

    private async Task OnAddPurpose() { if ((await BuildPurposeDialog(null)).ShowDialog() == true) await LoadAsync(); }

    private async Task OnAddGround()
    {
        if (_purposes.SelectedIndex < 0 || _detail is null) { _status.Text = "Selezionare prima una finalità."; return; }
        if ((await BuildPurposeDialog(_detail.Purposes[_purposes.SelectedIndex].Id)).ShowDialog() == true) await LoadAsync();
    }

    private async Task OnRemovePurpose()
    {
        if (_purposes.SelectedIndex < 0 || _detail is null) { _status.Text = "Selezionare prima una finalità."; return; }
        if (MessageBox.Show(this, "Rimuovere la finalità selezionata e i suoi presupposti?", "Rimozione", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await _s.Registry.RemovePurposeAsync(_detail.Purposes[_purposes.SelectedIndex].Id, _user);
        await LoadAsync();
    }

    private async Task OnSubmitReview() { await SaveAsync(); await _s.Registry.SubmitForReviewAsync(_versionId, _user); await LoadAsync(); }

    private async Task OnPublish()
    {
        await SaveAsync();
        var f = new DialogForm("Pubblica la revisione") { Owner = this };
        var from = f.AddDate("_In vigore dal", DateOnly.FromDateTime(DateTime.Today));
        f.AddNote("Con la pubblicazione la revisione diventa immutabile e la precedente risulta «superata». Il database rifiuta la pubblicazione se mancano elementi obbligatori e ne mostra l'elenco.");
        var cmd = Guid.NewGuid();
        f.Submit = async () =>
        {
            if (from.SelectedDate is not { } d) return "Indicare la data di entrata in vigore.";
            var current = await _s.Registry.GetVersionAsync(_versionId) ?? throw new InvalidOperationException();
            if (current.StatusCode == "draft") await _s.Registry.SubmitForReviewAsync(_versionId, _user);
            current = await _s.Registry.GetVersionAsync(_versionId) ?? throw new InvalidOperationException();
            await _s.Registry.PublishAsync(cmd, _versionId, _user, DateOnly.FromDateTime(d), current.RowVersion);
            return null;
        };
        if (f.ShowDialog() == true) await LoadAsync();
    }
}

/// <summary>Esportazione del registro alla data T: testo leggibile, copiabile e salvabile.</summary>
public sealed class RegistryExportWindow : Window
{
    private readonly TextBox _text = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, AcceptsReturn = true, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
    public string Text => _text.Text;
    public RegistryExportWindow(string text, DateOnly asOf)
    {
        Title = $"Registro alla data {asOf:dd/MM/yyyy}"; Width = 860; Height = 680; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _text.Text = text;
        var copy = new Button { Content = "_Copia", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        var save = new Button { Content = "_Salva come file…", Padding = new Thickness(12, 6, 12, 6) };
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Children = { copy, save } };
        var dock = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(bar, Dock.Bottom); dock.Children.Add(bar); dock.Children.Add(_text);
        Content = dock;
        System.Windows.Automation.AutomationProperties.SetName(_text, "Testo del registro");
        copy.Click += (_, _) => Clipboard.SetText(_text.Text);
        save.Click += (_, _) =>
        {
            var dlg = new SaveFileDialog { Filter = "File di testo (*.txt)|*.txt", FileName = $"registro-trattamenti-{asOf:yyyy-MM-dd}.txt" };
            if (dlg.ShowDialog(this) == true) System.IO.File.WriteAllText(dlg.FileName, _text.Text, System.Text.Encoding.UTF8);
        };
    }
}
