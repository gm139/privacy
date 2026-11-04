using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Privacy.Data;
using Privacy.Domain;

namespace Privacy.App;

/// <summary>Nuova richiesta di esercizio dei diritti (F03). Nessuna scelta preselezionata.</summary>
public sealed class NewRequestWindow : DialogForm
{
    public TextBox Requester { get; }
    public ComboBox Capacity { get; }
    public ComboBox Channel { get; }
    public DateTimeField Received { get; }
    public TextBox Text { get; }
    public IReadOnlyList<(CheckBox Box, string Code)> Rights { get; }
    public Guid? CreatedId { get; private set; }

    public NewRequestWindow(AppServices s, Guid practiceId, Guid userId, IReadOnlyList<CatalogItem> capacities,
        IReadOnlyList<CatalogItem> channels, IReadOnlyList<CatalogItem> rights) : base("Nuova richiesta dei diritti")
    {
        var commandId = Guid.NewGuid();   // lo stesso comando ritentato non crea duplicati
        Requester = AddText("_Nome del richiedente");
        Capacity = AddCombo("_Qualità del richiedente", capacities);
        Channel = AddCombo("_Canale di ricezione", channels);
        Received = AddDateTime("_Ricevuta il", DateTimeOffset.UtcNow);
        Text = AddText("_Testo della richiesta", multiline: true);
        Rights = AddChecks("Diritti richiesti (almeno uno)", rights);
        AddNote("Il termine di risposta viene calcolato automaticamente dalla data di ricezione.");
        Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(Requester.Text)) return "Indicare il nome del richiedente.";
            if (string.IsNullOrWhiteSpace(Text.Text)) return "Riportare il testo della richiesta.";
            var chosen = Rights.Where(r => r.Box.IsChecked == true).Select(r => r.Code).ToList();
            if (chosen.Count == 0) return "Selezionare almeno un diritto.";
            var received = Received.Value ?? throw new FormatException("Indicare la data di ricezione.");
            CreatedId = await s.Requests.RegisterAsync(new RegisterRequestCommand(commandId, practiceId, userId, null, Requester.Text.Trim(),
                Sel(Capacity), Sel(Channel), received, Text.Text.Trim(), chosen));
            return null;
        };
    }
}

/// <summary>Dettaglio di una richiesta: diritti, eventi e azioni (proroga, decisione, chiusura).</summary>
public sealed class RequestDetailWindow : Window
{
    private static readonly CultureInfo It = new("it-IT");
    private readonly AppServices _s; private readonly Guid _practice, _user, _id;
    private readonly TextBlock _header = new() { FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _info = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) };
    private readonly ListBox _rights = new() { Height = 90 };
    private readonly ListBox _events = new() { MinHeight = 140 };
    private readonly TextBlock _status = new() { Foreground = ThemeManager.Error, TextWrapping = TextWrapping.Wrap };
    private readonly Button _extend = new() { Content = "_Proroga…" }, _decide = new() { Content = "_Decisione…" }, _close = new() { Content = "_Chiudi richiesta" };
    private RequestDetail? _detail;
    private Dictionary<string, string> _rightLabels = new();
    private string RightLabel(string code) => _rightLabels.TryGetValue(code, out var l) ? l : code;

    public string HeaderText => _header.Text;
    public RequestDetailWindow(AppServices s, Guid practiceId, Guid userId, Guid requestId)
    {
        _s = s; _practice = practiceId; _user = userId; _id = requestId;
        FontSize = 14; Title = "Richiesta dei diritti"; Width = 720; SizeToContent = SizeToContent.Height; MaxHeight = 760; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var b in new[] { _extend, _decide, _close }) { b.Margin = new Thickness(0, 0, 8, 0); b.Padding = new Thickness(12, 6, 12, 6); buttons.Children.Add(b); }
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(_header); panel.Children.Add(_info);
        panel.Children.Add(new TextBlock { Text = "Diritti richiesti", FontWeight = FontWeights.SemiBold }); panel.Children.Add(_rights);
        panel.Children.Add(new TextBlock { Text = "Cronologia", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) }); panel.Children.Add(_events);
        panel.Children.Add(buttons); panel.Children.Add(_status);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _rights.ItemTemplate = DialogForm.WrapTemplate(); _events.ItemTemplate = DialogForm.WrapTemplate();
        ScrollViewer.SetHorizontalScrollBarVisibility(_rights, ScrollBarVisibility.Disabled); ScrollViewer.SetHorizontalScrollBarVisibility(_events, ScrollBarVisibility.Disabled);
        System.Windows.Automation.AutomationProperties.SetName(_rights, "Diritti richiesti");
        System.Windows.Automation.AutomationProperties.SetName(_events, "Cronologia della richiesta");
        _extend.Click += async (_, _) => await OnExtend();
        _decide.Click += async (_, _) => await OnDecide();
        _close.Click += async (_, _) => await OnClose();
        Loaded += async (_, _) => await LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            if (_rightLabels.Count == 0) _rightLabels = (await _s.Catalog.GetAsync("cat_right")).ToDictionary(i => i.Code, i => i.Label);
            _detail = await _s.Requests.GetDetailAsync(_id);
            if (_detail is null) { _status.Text = "Richiesta non trovata."; return; }
            var d = _detail;
            _header.Text = $"{d.ReferenceCode} - {(d.Closed ? "chiusa" : "aperta")}";
            _info.Text = $"Richiedente: {d.RequesterName}\nRicevuta il {d.ReceivedAt.ToLocalTime():dd/MM/yyyy}. Scadenza {d.EffectiveDueOn:dd/MM/yyyy}"
                         + (d.EffectiveDueOn != d.InitialDueOn ? $" (prorogata; iniziale {d.InitialDueOn:dd/MM/yyyy})" : "") + $"\n\n{d.RequestText}";
            _rights.ItemsSource = d.Rights.Select(r => $"{RightLabel(r.RightCode)}: {(r.ResponseSent ? "riscontro inviato" : r.Decided ? "deciso, riscontro non ancora inviato" : "da decidere")}").ToList();
            _events.ItemsSource = d.Events.Select(e => $"{e.OccurredAt.ToLocalTime():dd/MM/yyyy HH:mm}  {e.Description}").ToList();
            foreach (var b in new[] { _extend, _decide, _close }) b.IsEnabled = !d.Closed;
            _status.Text = "";
        }
        catch (Exception) { _status.Text = "Impossibile leggere la richiesta. Verificare il database e riprovare."; }
    }

    private async Task OnExtend()
    {
        if (_detail is null) return;
        var limit = new DeadlineCalculator().ComputeDueDate(DeadlineCalculator.ReceivedDate(_detail.ReceivedAt), 3);
        var f = new DialogForm("Proroga del termine") { Owner = this };
        var date = f.AddDate("_Nuova scadenza", null);
        var reason = f.AddText("_Motivazione (complessità o numero delle richieste)", multiline: true);
        var notified = f.AddDateTime("Comunicata all'interessato il (se già avvenuto)", null);
        f.AddNote($"Limite complessivo: {limit:dd/MM/yyyy} (tre mesi dalla ricezione; regola da confermare con il consulente).");
        var cmd = Guid.NewGuid();
        f.Submit = async () =>
        {
            if (date.SelectedDate is not { } d) return "Indicare la nuova scadenza.";
            if (string.IsNullOrWhiteSpace(reason.Text)) return "La proroga richiede una motivazione.";
            await _s.Requests.ExtendAsync(cmd, _id, _user, DateOnly.FromDateTime(d), reason.Text.Trim(), notified.Value);
            return null;
        };
        if (f.ShowDialog() == true) await LoadAsync();
    }

    private async Task OnDecide()
    {
        if (_detail is null) return;
        var open = _detail.Rights.Where(r => !r.Decided).Select(r => new CatalogItem(r.RightCode, RightLabel(r.RightCode))).ToList();
        if (open.Count == 0) { _status.Text = "Tutti i diritti hanno già una decisione."; return; }
        var outcomes = await new CatalogService(_s.Db).GetAsync("cat_request_outcome");
        var f = new DialogForm("Decisione su un diritto") { Owner = this };
        var right = f.AddCombo("_Diritto", open);
        var outcome = f.AddCombo("_Esito", outcomes);
        var reason = f.AddText("_Motivazione (obbligatoria se non accolta del tutto)", multiline: true);
        var sent = f.AddDateTime("Riscontro inviato il (lasciare vuoto se non ancora inviato)", null);
        var cmd = Guid.NewGuid();
        f.Submit = async () =>
        {
            await _s.Requests.DecideAsync(cmd, _id, _user, DialogForm.Sel(right), DialogForm.Sel(outcome),
                string.IsNullOrWhiteSpace(reason.Text) ? null : reason.Text.Trim(), sent.Value);
            return null;
        };
        if (f.ShowDialog() == true) await LoadAsync();
    }

    private async Task OnClose()
    {
        if (MessageBox.Show(this, "Chiudere la richiesta? Serve una decisione con riscontro inviato per ogni diritto.", "Chiusura", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { await _s.Requests.CloseAsync(Guid.NewGuid(), _id, _user); await LoadAsync(); }
        catch (Npgsql.PostgresException e) { _status.Text = DialogForm.FriendlyDb(e); }
        catch (Exception) { _status.Text = "Chiusura non riuscita. Riprovare."; }
    }
}
