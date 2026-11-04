using Npgsql;
using Privacy.Data;
using Privacy.Domain;
using Xunit;

namespace Privacy.Data.IntegrationTests;

/// <summary>Richiede PRIVACY_TEST_CONN (db/start_test_db.sh). Ogni test crea il proprio studio sintetico.</summary>
public class RightsRequestTests
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("PRIVACY_TEST_CONN");
    private static readonly DateTimeOffset Received = new(2026, 1, 31, 10, 0, 0, TimeSpan.FromHours(1));

    private static async Task<(RightsRequestService svc, Guid practice, Guid user, Db db)> SetupAsync()
    {
        var db = new Db(Conn!);
        await using var c = await db.OpenAsync();
        await using var p = new NpgsqlCommand("INSERT INTO ps.practices (name, controller_name, address, privacy_contact) VALUES ('Studio test','Dott. Test','Via Prova 1','privacy@example.test') RETURNING id", c);
        var practice = (Guid)(await p.ExecuteScalarAsync())!;
        await using var u = new NpgsqlCommand("INSERT INTO ps.users (practice_id, username, display_name, password_verifier) VALUES ($1,'medico','Medico','argon2id$1$x$y$z') RETURNING id", c);
        u.Parameters.AddWithValue(practice);
        var user = (Guid)(await u.ExecuteScalarAsync())!;
        return (new RightsRequestService(db), practice, user, db);
    }

    private static RegisterRequestCommand Cmd(Guid practice, Guid user, Guid? commandId = null, string[]? rights = null) =>
        new(commandId ?? Guid.NewGuid(), practice, user, null, "Mario Rossi (collaboratore)", "other", "email",
            Received, "Chiedo copia dei miei dati", rights ?? new[] { "access" });

    [Fact]
    public async Task Register_NonPatient_ComputesDeadlineLikeDomain()
    {
        if (Conn is null) return;
        var (svc, practice, user, _) = await SetupAsync();
        await svc.RegisterAsync(Cmd(practice, user));
        var due = (await svc.ListDueAsync(practice, new DateOnly(2026, 12, 31), 0)).Single();
        var expected = new DeadlineCalculator().ComputeDueDate(DeadlineCalculator.ReceivedDate(Received));
        Assert.Equal(expected, due.InitialDueOn);
        Assert.Equal(expected, due.EffectiveDueOn);
        Assert.StartsWith("R-2026-", due.ReferenceCode);
    }

    [Fact]
    public async Task Register_Retry_SameCommand_NoDuplicate()
    {
        if (Conn is null) return;
        var (svc, practice, user, _) = await SetupAsync();
        var cmd = Cmd(practice, user);
        var a = await svc.RegisterAsync(cmd);
        var b = await svc.RegisterAsync(cmd);
        Assert.Equal(a, b);
        Assert.Single(await svc.ListDueAsync(practice, new DateOnly(2026, 12, 31), 0));
    }

    [Fact]
    public async Task Close_WithoutDecisionAndResponse_IsRejected_ThenSucceeds()
    {
        if (Conn is null) return;
        var (svc, practice, user, _) = await SetupAsync();
        var id = await svc.RegisterAsync(Cmd(practice, user));
        await Assert.ThrowsAsync<PostgresException>(() => svc.CloseAsync(Guid.NewGuid(), id, user));
        await svc.DecideAsync(Guid.NewGuid(), id, user, "access", "granted", null, DateTimeOffset.UtcNow);
        await svc.CloseAsync(Guid.NewGuid(), id, user);
        Assert.Empty(await svc.ListDueAsync(practice, new DateOnly(2030, 1, 1), 0));
    }

    [Fact]
    public async Task Failed_Command_LeavesNothingBehind()
    {
        if (Conn is null) return;
        var (svc, practice, user, db) = await SetupAsync();
        var id = await svc.RegisterAsync(Cmd(practice, user));
        var failing = Guid.NewGuid();
        await Assert.ThrowsAsync<PostgresException>(() => svc.CloseAsync(failing, id, user));
        await using var c = await db.OpenAsync();
        await using var q = new NpgsqlCommand("SELECT count(*) FROM ps.processed_commands WHERE command_id = $1", c);
        q.Parameters.AddWithValue(failing);
        Assert.Equal(0L, (long)(await q.ExecuteScalarAsync())!);   // rollback completo: il comando può essere ritentato
    }

    [Fact]
    public async Task Extension_MovesEffectiveDueOnly_AndRejectsBeyondLimit()
    {
        if (Conn is null) return;
        var (svc, practice, user, _) = await SetupAsync();
        var id = await svc.RegisterAsync(Cmd(practice, user));
        var initial = (await svc.ListDueAsync(practice, new DateOnly(2030, 1, 1), 0)).Single().InitialDueOn;
        var limit = new DeadlineCalculator().ComputeDueDate(DeadlineCalculator.ReceivedDate(Received), 3);
        await svc.ExtendAsync(Guid.NewGuid(), id, user, limit, "Complessità della richiesta", null);
        var after = (await svc.ListDueAsync(practice, new DateOnly(2030, 1, 1), 0)).Single();
        Assert.Equal(initial, after.InitialDueOn);
        Assert.Equal(limit, after.EffectiveDueOn);
        await Assert.ThrowsAsync<PostgresException>(() =>
            svc.ExtendAsync(Guid.NewGuid(), id, user, limit.AddDays(1), "Oltre il limite", null));
    }

    [Fact]
    public async Task Detail_ShowsRightsDecisionsAndEvents()
    {
        if (Conn is null) return;
        var (svc, practice, user, _) = await SetupAsync();
        var id = await svc.RegisterAsync(Cmd(practice, user, rights: new[] { "access", "erasure" }));
        await svc.DecideAsync(Guid.NewGuid(), id, user, "access", "granted", null, DateTimeOffset.UtcNow);
        var d = (await svc.GetDetailAsync(id))!;
        Assert.Equal(2, d.Rights.Count);
        Assert.True(d.Rights.Single(r => r.RightCode == "access").ResponseSent);
        Assert.False(d.Rights.Single(r => r.RightCode == "erasure").Decided);
        Assert.Equal(new[] { "received", "decision" }, d.Events.Select(e => e.TypeCode).ToArray());
        Assert.False(d.Closed);
        Assert.Null(await svc.GetDetailAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Catalogs_AreReadable_AndOnlyWhitelistedTables()
    {
        if (Conn is null) return;
        var cat = new CatalogService(new Db(Conn));
        Assert.Contains((await cat.GetAsync("cat_right")), i => i.Code == "access");
        await Assert.ThrowsAsync<ArgumentException>(() => cat.GetAsync("users"));
    }
}
