using Privacy.Domain;
using Xunit;

namespace Privacy.Domain.Tests;

// Valori attesi calcolati a mano sul calendario (stessi casi di db/tests/test_v1.sql, piano di test §2).
public class DeadlineCalculatorTests
{
    private readonly DeadlineCalculator _c = new();
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact] public void Ordinary() => Assert.Equal(D(2026, 4, 16), _c.ComputeDueDate(D(2026, 3, 16)));
    [Fact] public void EndOfMonth_SlipsFromSaturday() => Assert.Equal(D(2026, 3, 2), _c.ComputeDueDate(D(2026, 1, 31)));
    [Fact] public void EndOfMonth_LeapYear() => Assert.Equal(D(2028, 2, 29), _c.ComputeDueDate(D(2028, 1, 31)));
    [Fact] public void ShortMonth() => Assert.Equal(D(2026, 12, 30), _c.ComputeDueDate(D(2026, 11, 30)));
    [Fact] public void YearChange_SlipsFromSunday() => Assert.Equal(D(2027, 2, 1), _c.ComputeDueDate(D(2026, 12, 31)));
    [Fact] public void Weekend_SlipsToMonday() => Assert.Equal(D(2026, 11, 16), _c.ComputeDueDate(D(2026, 10, 15)));
    [Fact] public void Holiday_25April_ThenWeekend() => Assert.Equal(D(2025, 4, 28), _c.ComputeDueDate(D(2025, 3, 25)));
    [Fact] public void Extension_ThreeMonths() => Assert.Equal(D(2026, 4, 30), _c.ComputeDueDate(D(2026, 1, 31), 3));
    [Fact] public void EasterMonday_Known() { Assert.Equal(D(2026, 4, 6), DeadlineCalculator.EasterMonday(2026)); Assert.Equal(D(2025, 4, 21), DeadlineCalculator.EasterMonday(2025)); }
    [Fact] public void LocalHoliday_Slips() => Assert.Equal(D(2026, 4, 17), new DeadlineCalculator(new[] { D(2026, 4, 16) }).ComputeDueDate(D(2026, 3, 16)));
    [Fact] public void ReceivedDate_UsesRomeZone() => Assert.Equal(D(2026, 3, 16), DeadlineCalculator.ReceivedDate(new DateTimeOffset(2026, 3, 15, 23, 30, 0, TimeSpan.Zero)));
}

public class IncidentClockTests
{
    [Fact]
    public void Deadline_Is_72_Instants_Across_DstChange()
    {
        // 28/03/2026 12:00 Roma (+01:00); il 29/03 alle 02:00 le lancette vanno avanti: 72 ore reali = 31/03 13:00 Roma (+02:00)
        var awareness = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.FromHours(1));
        var deadline = IncidentClock.Deadline(awareness);
        Assert.Equal(new DateTimeOffset(2026, 3, 31, 13, 0, 0, TimeSpan.FromHours(2)), deadline);
        Assert.Equal(TimeSpan.FromHours(72), deadline - awareness);
    }

    [Fact] public void Remaining_IsNegative_WhenExpired()
        => Assert.True(IncidentClock.Remaining(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero)) < TimeSpan.Zero);
}
