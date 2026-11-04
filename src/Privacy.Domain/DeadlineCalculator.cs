namespace Privacy.Domain;

/// <summary>
/// Calcolo dei termini per le richieste dei diritti (regola it-1182-v1).
/// Specchio in C# della funzione SQL ps.compute_due_date: le due implementazioni devono dare lo stesso risultato.
/// Regola PROPOSTA, da confermare dal consulente sul Reg. 1182/71.
/// </summary>
public sealed class DeadlineCalculator
{
    public const string RuleVersion = "it-1182-v1";

    private static readonly (int Month, int Day)[] FixedHolidays =
        { (1, 1), (1, 6), (4, 25), (5, 1), (6, 2), (8, 15), (11, 1), (12, 8), (12, 25), (12, 26) };

    private readonly HashSet<DateOnly> _localHolidays;

    public DeadlineCalculator(IEnumerable<DateOnly>? localHolidays = null)
        => _localHolidays = new HashSet<DateOnly>(localHolidays ?? Array.Empty<DateOnly>());

    public static DateOnly EasterMonday(int year)
    {
        int a = year % 19, b = year / 100, c = year % 100;
        int d = b / 4, e = b % 4, f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day).AddDays(1);
    }

    public bool IsNonWorking(DateOnly day)
        => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
           || Array.IndexOf(FixedHolidays, (day.Month, day.Day)) >= 0
           || day == EasterMonday(day.Year)
           || _localHolidays.Contains(day);

    /// <summary>Giorno corrispondente dopo <paramref name="months"/> mesi (fine mese ricondotta), poi primo giorno lavorativo.</summary>
    public DateOnly ComputeDueDate(DateOnly received, int months = 1)
    {
        var d = received.AddMonths(months);
        while (IsNonWorking(d)) d = d.AddDays(1);
        return d;
    }

    /// <summary>Data civile di ricezione nel fuso Europe/Rome.</summary>
    public static DateOnly ReceivedDate(DateTimeOffset receivedAt)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(receivedAt, tz).DateTime);
    }
}
