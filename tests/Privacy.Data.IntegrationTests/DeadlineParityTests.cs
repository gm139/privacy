using Privacy.Data;
using Privacy.Domain;
using Xunit;

namespace Privacy.Data.IntegrationTests;

/// <summary>Richiede PRIVACY_TEST_CONN (vedi db/start_test_db.sh); senza, il test esce senza verificare nulla.</summary>
public class DeadlineParityTests
{
    [Fact]
    public async Task CSharp_and_Sql_agree_for_every_day_2026_2030()
    {
        var cs = Environment.GetEnvironmentVariable("PRIVACY_TEST_CONN");
        if (string.IsNullOrEmpty(cs)) return;
        var db = new Db(cs);
        var calc = new DeadlineCalculator();
        var mismatches = new List<string>();
        for (var d = new DateOnly(2026, 1, 1); d <= new DateOnly(2030, 12, 31); d = d.AddDays(1))
            foreach (var months in new[] { 1, 3 })
            {
                var sql = await db.ComputeDueDateAsync(d, months);
                var cSharp = calc.ComputeDueDate(d, months);
                if (sql != cSharp) mismatches.Add($"{d} +{months}m: SQL {sql} C# {cSharp}");
            }
        Assert.Empty(mismatches);
    }
}
