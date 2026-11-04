using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Privacy.Data;
using Privacy.Domain;

namespace Privacy.App;

/// <summary>Nuova segnalazione di incidente (F04).</summary>
public sealed class NewIncidentWindow : DialogForm
{
    public DateTimeField Discovered { get; }
    public DateTimeField Awareness { get; }
    public TextBox Description { get; }
    public ComboBox Classification { get; }
    public Guid? CreatedId { get; private set; }

    public NewIncidentWindow(AppServices s, Guid practiceId, Guid userId, IReadOnlyList<CatalogItem> classifications) : base("Nuovo incidente")
    {
        var commandId = Guid.NewGuid();
        Discovered = AddDateTime("_Rilevato il", DateTimeOffset.UtcNow);
        Awareness = AddDateTime("Momento in cui il titolare ne è venuto a conoscenza (lasciare vuoto se non ancora accertato)", null);
        Description = AddText("_Descrizione dei fatti", multiline: true);
        Classification = AddCombo("_Classificazione provvisoria", classifications);
        AddNote("Le 72 ore per la notifica al Garante decorrono dalla consapevolezza. Il programma non decide al posto del titolare.");
        Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(Description.Text)) return "Descrivere i fatti.";
            var found = Discovered.Value ?? throw new FormatException("Indicare quando è stato rilevato.");
            CreatedId = await s.Incidents.ReportAsync(new ReportIncidentCommand(commandId, practiceId, userId, found, Awareness.Value,
                Description.Text.Trim(), Sel(Classification)));
            return null;
        };
    }
}

/// <summary>Dettaglio di un incidente: consapevolezza, valutazione, azioni, chiusura.</summary>
public sealed class IncidentDetailWindow : Window
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
    private readonly AppServices _s; private readonly Guid _user, _id;
    private readonly TextBlock _header = new() { FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _info = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) };
    private readonly ListBox _actions = new() { MinHeight = 140 };
    private readonly TextBlock _status = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
    private readonly Button _awareness = new() { Content = "_Consapevolezza…" }, _assess = new() { Content = "_Valutazione…" },
                            _action = new() { Content = "_Registra azione…" }, _close = new() { Content = "C_hiudi incidente" };
    private IncidentDetail? _detail;

    public string HeaderText => _header.Text;
    public IncidentDetailWindow(AppServices s, Guid userId, Guid incidentId)
    {
        _s = s; _user = userId; _id = incidentId;
        Title = "Incidente"; Width = 760; Height = 640; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var b in new[] { _awareness, _assess, _action, _close }) { b.Margin = new Thickness(0, 0, 8, 0); b.Padding = new Thickness(12, 6, 12, 6); buttons.Children.Add(b); }
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(_header); panel.Children.Add(_info);
        panel.Children.Add(new TextBlock { Text = "Azioni registrate", FontWeight = FontWeights.SemiBold }); panel.Children.Add(_actions);
        panel.Children.Add(buttons); panel.Children.Add(_status);
        Content = new ScrollViewer { Content = panel };
        System.Windows.Automation.AutomationProperties.SetName(_actions, "Azioni registrate");
        _awareness.Click += async (_, _) => await OnAwareness();
        _assess.Click += async (_, _) => await OnAssess();
        _action.Click += async (_, _) => await OnAction();
        _close.Click += async (_, _) => await OnClose();
        Loaded += async (_, _) => await LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            _detail = await _s.Incidents.GetDetailAsync(_id);
            if (_detail is null) { _status.Text = "Incidente non trovato."; return; }
            var d = _detail;
            _header.Text = $"{d.ReferenceCode} - {(d.Closed ? "chiuso" : "aperto")}";
            string deadline = d.AwarenessAt is null ? "Consapevolezza non ancora registrata: le 72 ore non sono ancora avviate."
                : $"Notifica al Garante, se dovuta, entro {TimeZoneInfo.ConvertTime(IncidentClock.Deadline(d.AwarenessAt.Value), Rome):dd/MM/yyyy HH:mm} "
                  + $"({Remaining(IncidentClock.Remaining(d.AwarenessAt.Value, DateTimeOffset.UtcNow))}).";
            _info.Text = $"{deadline}\nValutazioni registrate: {d.AssessmentCount}\n\n{d.Description}";
            _actions.ItemsSource = d.Actions.Select(a => $"{a.OccurredAt.ToLocalTime():dd/MM/yyyy HH:mm}  [{a.TypeCode}] {a.Description}{(a.ProtocolReference is null ? "" : $"  (protocollo {a.ProtocolReference})")}").ToList();
            foreach (var b in new[] { _awareness, _assess, _action, _close }) b.IsEnabled = !d.Closed;
            _status.Text = "";
        }
        catch (Exception) { _status.Text = "Impossibile leggere l'incidente. Verificare il database e riprovare."; }
    }

    private static string Remaining(TimeSpan t) => t < TimeSpan.Zero ? $"scaduta da {(int)(-t).TotalHours} ore" : $"restano {(int)t.TotalHours} ore e {t.Minutes} minuti";

    private async Task OnAwareness()
    {
        var f = new DialogForm("Consapevolezza") { Owner = this };
        var when = f.AddDateTime("_Momento in cui il titolare ne è venuto a conoscenza", _detail?.AwarenessAt ?? DateTimeOffset.UtcNow);
        f.AddNote("Da questo istante decorrono le 72 ore. La modifica resta tracciata nell'audit.");
        var cmd = Guid.NewGuid();
        f.Submit = async () =>
        {
            var v = when.Value ?? throw new FormatException("Indicare data e ora.");
            await _s.Incidents.SetAwarenessAsync(cmd, _id, _user, v);
            return null;
        };
        if (f.ShowDialog() == true) await LoadAsync();
    }

    private async Task OnAssess()
    {
        var cat = new CatalogService(_s.Db);
        var f = new DialogForm("Valutazione dell'incidente", 560) { Owner = this };
        var risk = f.AddCombo("_Livello di rischio", await cat.GetAsync("cat_risk_level"));
        var reason = f.AddText("_Motivazione della valutazione", multiline: true);
        var conseq = f.AddText("_Conseguenze probabili", multiline: true);
        var notify = f.AddCombo("Decisione sulla _notifica al Garante", await cat.GetAsync("cat_notification_decision"));
        var comm = f.AddCombo("Decisione sulla comunicazione agli _interessati", await cat.GetAsync("cat_subject_comm_decision"));
        var subjects = f.AddText("Numero approssimativo di interessati (facoltativo)");
        var delay = f.AddText("Motivo dell'eventuale ritardo (facoltativo)");
        var cats = f.AddChecks("Categorie di dati coinvolte", await cat.GetAsync("cat_data_category"));
        var cmd = Guid.NewGuid();
        f.Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(reason.Text) || string.IsNullOrWhiteSpace(conseq.Text)) return "Motivazione e conseguenze sono obbligatorie.";
            long? n = null;
            if (!string.IsNullOrWhiteSpace(subjects.Text)) { if (!long.TryParse(subjects.Text.Trim(), out var x) || x < 0) return "Numero di interessati non valido."; n = x; }
            await _s.Incidents.AssessAsync(new AssessIncidentCommand(cmd, _id, _user, DialogForm.Sel(risk), reason.Text.Trim(), conseq.Text.Trim(),
                DialogForm.Sel(notify), DialogForm.Sel(comm), n, null, string.IsNullOrWhiteSpace(delay.Text) ? null : delay.Text.Trim(),
                cats.Where(c => c.Box.IsChecked == true).Select(c => c.Code).ToList()));
            return null;
        };
        if (f.ShowDialog() == true) await LoadAsync();
    }

    private async Task OnAction()
    {
        var types = await new CatalogService(_s.Db).GetAsync("cat_incident_action_type");
        var f = new DialogForm("Registra azione") { Owner = this };
        var type = f.AddCombo("_Tipo di azione", types);
        var when = f.AddDateTime("_Quando è avvenuta", DateTimeOffset.UtcNow);
        var desc = f.AddText("_Descrizione", multiline: true);
        var proto = f.AddText("_Protocollo o ricevuta (obbligatorio per la notifica al Garante, da inserire dopo l'invio)");
        var cmd = Guid.NewGuid();
        f.Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(desc.Text)) return "Descrivere l'azione.";
            var v = when.Value ?? throw new FormatException("Indicare data e ora.");
            await _s.Incidents.AddActionAsync(cmd, _id, _user, DialogForm.Sel(type), v, desc.Text.Trim(), string.IsNullOrWhiteSpace(proto.Text) ? null : proto.Text.Trim());
            return null;
        };
        if (f.ShowDialog() == true) await LoadAsync();
    }

    private async Task OnClose()
    {
        if (MessageBox.Show(this, "Chiudere l'incidente?", "Chiusura", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { await _s.Incidents.CloseAsync(Guid.NewGuid(), _id, _user); await LoadAsync(); }
        catch (Npgsql.PostgresException e) { _status.Text = DialogForm.FriendlyDb(e); }
        catch (Exception) { _status.Text = "Chiusura non riuscita. Riprovare."; }
    }
}
