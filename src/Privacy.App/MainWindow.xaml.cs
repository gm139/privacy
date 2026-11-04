using System.Globalization;
using System.Windows;
using Privacy.Data;
using Privacy.Domain;

namespace Privacy.App;

public sealed record RequestRow(string Reference, string Status, string Received, string Due, string Situation);
public sealed record IncidentRow(string Reference, string Status, string Classification, string Deadline, string Remaining);

public partial class MainWindow : Window
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
    private static readonly CultureInfo It = new("it-IT");
    private readonly AppServices _services;
    private readonly Guid _practiceId;

    public int RequestCount => RequestsGrid.Items.Count;
    public int IncidentCount => IncidentsGrid.Items.Count;

    public MainWindow(AppServices services, Guid practiceId, string displayName)
    {
        InitializeComponent();
        _services = services; _practiceId = practiceId;
        HeaderText.Text = $"Scadenze - {displayName}";
        Loaded += async (_, _) => await LoadAsync();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadAsync();

    public async Task LoadAsync()
    {
        RefreshButton.IsEnabled = false;
        StatusText.Text = "";
        try
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Rome).DateTime);
            var requests = await _services.Requests.ListDueAsync(_practiceId, today, 30);
            RequestsGrid.ItemsSource = requests.Select(r => new RequestRow(r.ReferenceCode, StatusLabel(r.StatusCode),
                r.ReceivedAt.ToOffset(Rome.GetUtcOffset(r.ReceivedAt)).ToString("dd/MM/yyyy", It), r.EffectiveDueOn.ToString("dd/MM/yyyy", It),
                Situation(r.EffectiveDueOn, today, r.EffectiveDueOn != r.InitialDueOn))).ToList();

            var now = DateTimeOffset.UtcNow;
            var incidents = await _services.Incidents.ListOpenAsync(_practiceId);
            IncidentsGrid.ItemsSource = incidents.Select(i => new IncidentRow(i.ReferenceCode, i.StatusCode, ClassLabel(i.ClassificationCode),
                i.AwarenessAt is null ? "Consapevolezza non ancora registrata" : TimeZoneInfo.ConvertTime(IncidentClock.Deadline(i.AwarenessAt.Value), Rome).ToString("dd/MM/yyyy HH:mm", It),
                i.AwarenessAt is null ? "-" : RemainingText(IncidentClock.Remaining(i.AwarenessAt.Value, now)))).ToList();
        }
        catch (Exception)
        {
            StatusText.Text = "Impossibile leggere i dati. Verificare che il database sia attivo e riprovare.";
        }
        finally { RefreshButton.IsEnabled = true; }
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
