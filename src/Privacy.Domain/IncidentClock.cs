namespace Privacy.Domain;

/// <summary>Termine di notifica al Garante (art. 33): 72 ore su ISTANTI dalla consapevolezza, mai in giorni lavorativi.</summary>
public static class IncidentClock
{
    public static readonly TimeSpan NotificationWindow = TimeSpan.FromHours(72);

    public static DateTimeOffset Deadline(DateTimeOffset awareness) => awareness.ToUniversalTime() + NotificationWindow;

    /// <summary>Tempo residuo (negativo se scaduto).</summary>
    public static TimeSpan Remaining(DateTimeOffset awareness, DateTimeOffset now) => Deadline(awareness) - now.ToUniversalTime();
}
