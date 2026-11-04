using Npgsql;
using Privacy.Data;
using Privacy.Domain;
using Xunit;

namespace Privacy.Data.IntegrationTests;

/// <summary>Richiede PRIVACY_TEST_CONN (db/start_test_db.sh).</summary>
public class IncidentTests
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("PRIVACY_TEST_CONN");
    private static readonly DateTimeOffset Awareness = new(2026, 3, 28, 12, 0, 0, TimeSpan.FromHours(1));

    private static async Task<(IncidentService svc, Guid practice, Guid user)> SetupAsync()
    {
        var db = new Db(Conn!);
        await using var c = await db.OpenAsync();
        await using var p = new NpgsqlCommand("INSERT INTO ps.practices (name, controller_name, address, privacy_contact) VALUES ('Studio test','Dott. Test','Via Prova 1','privacy@example.test') RETURNING id", c);
        var practice = (Guid)(await p.ExecuteScalarAsync())!;
        await using var u = new NpgsqlCommand("INSERT INTO ps.users (practice_id, username, display_name, password_verifier) VALUES ($1,'medico','Medico','argon2id$1$x$y$z') RETURNING id", c);
        u.Parameters.AddWithValue(practice);
        return (new IncidentService(db), practice, (Guid)(await u.ExecuteScalarAsync())!);
    }

    private static ReportIncidentCommand Report(Guid practice, Guid user, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), practice, user, Awareness.AddHours(-2), Awareness, "Server dello studio non raggiungibile", "breach");

    [Fact]
    public async Task Report_DeadlineIs72Hours_SameAsDomainClock()
    {
        if (Conn is null) return;
        var (svc, practice, user) = await SetupAsync();
        await svc.ReportAsync(Report(practice, user));
        var inc = (await svc.ListOpenAsync(practice)).Single();
        Assert.Equal(IncidentClock.Deadline(Awareness), inc.NotificationDeadlineAt);
        Assert.StartsWith("I-2026-", inc.ReferenceCode);
    }

    [Fact]
    public async Task Report_Retry_NoDuplicate()
    {
        if (Conn is null) return;
        var (svc, practice, user) = await SetupAsync();
        var cmd = Report(practice, user);
        Assert.Equal(await svc.ReportAsync(cmd), await svc.ReportAsync(cmd));
        Assert.Single(await svc.ListOpenAsync(practice));
    }

    [Fact]
    public async Task Notification_RequiresProtocol()
    {
        if (Conn is null) return;
        var (svc, practice, user) = await SetupAsync();
        var id = await svc.ReportAsync(Report(practice, user));
        await Assert.ThrowsAsync<PostgresException>(() =>
            svc.AddActionAsync(Guid.NewGuid(), id, user, "notification", DateTimeOffset.UtcNow, "Notifica inviata", null));
        await svc.AddActionAsync(Guid.NewGuid(), id, user, "notification", DateTimeOffset.UtcNow, "Notifica inviata", "PROT-123");
    }

    [Fact]
    public async Task Assess_RecordsDecision_AndIncidentCanBeClosed()
    {
        if (Conn is null) return;
        var (svc, practice, user) = await SetupAsync();
        var id = await svc.ReportAsync(Report(practice, user));
        await svc.AssessAsync(new AssessIncidentCommand(Guid.NewGuid(), id, user, "risk", "Dati sanitari non disponibili",
            "Ritardo nelle cure", "notify", "not_communicate", 120, null, null, new[] { "health_relation" }));
        await svc.CloseAsync(Guid.NewGuid(), id, user);
        Assert.Empty(await svc.ListOpenAsync(practice));
    }

    [Fact]
    public async Task SetAwareness_RecomputesDeadline()
    {
        if (Conn is null) return;
        var (svc, practice, user) = await SetupAsync();
        var id = await svc.ReportAsync(new ReportIncidentCommand(Guid.NewGuid(), practice, user, Awareness, null, "Possibile accesso non autorizzato"));
        Assert.Null((await svc.ListOpenAsync(practice)).Single().NotificationDeadlineAt);
        await svc.SetAwarenessAsync(Guid.NewGuid(), id, user, Awareness);
        Assert.Equal(IncidentClock.Deadline(Awareness), (await svc.ListOpenAsync(practice)).Single().NotificationDeadlineAt);
    }
}
