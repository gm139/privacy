using Npgsql;
using Privacy.Data;
using Xunit;

namespace Privacy.Data.IntegrationTests;

/// <summary>Richiede PRIVACY_TEST_CONN (db/start_test_db.sh).</summary>
public class DisposalTests
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("PRIVACY_TEST_CONN");

    private static async Task<(Db db, Guid practice, Guid user)> SetupAsync()
    {
        var db = new Db(Conn!);
        await using var c = await db.OpenAsync();
        await using var p = new NpgsqlCommand("INSERT INTO ps.practices (name, controller_name, address, privacy_contact) VALUES ('Studio','Dott.','Via 1','p@example.test') RETURNING id", c);
        var practice = (Guid)(await p.ExecuteScalarAsync())!;
        await using var u = new NpgsqlCommand("INSERT INTO ps.users (practice_id, username, display_name, password_verifier) VALUES ($1,'medico','Medico','argon2id$1$x$y$z') RETURNING id", c);
        u.Parameters.AddWithValue(practice);
        return (db, practice, (Guid)(await u.ExecuteScalarAsync())!);
    }

    private static async Task<long> Count(Db db, string sql, params object[] p)
    {
        await using var c = await db.OpenAsync();
        await using var q = new NpgsqlCommand(sql, c);
        foreach (var v in p) q.Parameters.AddWithValue(v);
        return (long)(await q.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> ClosedRequest(Db db, Guid practice, Guid user)
    {
        var svc = new RightsRequestService(db);
        var id = await svc.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, user, null, "Mario (collab.)", "other", "email",
            DateTimeOffset.UtcNow, "Copia dati", new[] { "access" }));
        await svc.DecideAsync(Guid.NewGuid(), id, user, "access", "granted", null, DateTimeOffset.UtcNow);
        await svc.CloseAsync(Guid.NewGuid(), id, user);
        return id;
    }

    private static string TempLedger() => Path.Combine(Path.GetTempPath(), "privacy-ledger-" + Guid.NewGuid().ToString("N") + ".jsonl");

    [Fact]
    public async Task Dispose_ClosedRequest_DeletesAllRows_RecordsLedger_AndWritesExternalFile()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var path = TempLedger();
        var svc = new DisposalService(db, new DisposalLedger(path));
        var id = await ClosedRequest(db, practice, user);

        var cmd = Guid.NewGuid();
        Assert.True(await svc.DisposeAsync(cmd, "rights_request", id, user, "Durata approvata trascorsa"));
        Assert.True(await svc.DisposeAsync(cmd, "rights_request", id, user, "Durata approvata trascorsa"));   // ritentato: nessun errore
        Assert.Equal(0, await Count(db, "SELECT count(*) FROM ps.rights_requests WHERE id = $1", id));
        Assert.Equal(0, await Count(db, "SELECT count(*) FROM ps.request_events WHERE request_id = $1", id));
        Assert.Equal(1, await Count(db, "SELECT count(*) FROM ps.disposal_items WHERE item_type = 'rights_request' AND item_id = $1", id));
        Assert.Contains(new DisposalLedger(path).Read(), e => e.ItemId == id && e.ItemType == "rights_request");
        File.Delete(path);
    }

    [Fact]
    public async Task Dispose_OpenRequest_IsRefused_AndNothingChanges()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var path = TempLedger();
        var svc = new DisposalService(db, new DisposalLedger(path));
        var id = await new RightsRequestService(db).RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, user, null, "Mario", "other", "email",
            DateTimeOffset.UtcNow, "Copia", new[] { "access" }));
        await Assert.ThrowsAsync<PostgresException>(() => svc.DisposeAsync(Guid.NewGuid(), "rights_request", id, user, "prova"));
        Assert.Equal(1, await Count(db, "SELECT count(*) FROM ps.rights_requests WHERE id = $1", id));
        Assert.Empty(new DisposalLedger(path).Read());
    }

    [Fact]
    public async Task Dispose_ClosedIncident_Works_AndAppCannotDeleteDirectly()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var inc = new IncidentService(db);
        var id = await inc.ReportAsync(new ReportIncidentCommand(Guid.NewGuid(), practice, user, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Smarrimento dispositivo"));
        await inc.AddActionAsync(Guid.NewGuid(), id, user, "containment", DateTimeOffset.UtcNow, "Dispositivo bloccato", null);
        // la chiusura richiede la valutazione (art. 33)
        await inc.AssessAsync(new AssessIncidentCommand(Guid.NewGuid(), id, user, "risk", "Dispositivo smarrito",
            "Accesso non autorizzato possibile", "notify", "not_communicate", 1, null, null, new[] { "health_relation" }));
        await inc.CloseAsync(Guid.NewGuid(), id, user);

        await using (var c = await db.OpenAsync())
        {
            await using var q = new NpgsqlCommand("DELETE FROM ps.incident_actions WHERE incident_id = $1", c);
            q.Parameters.AddWithValue(id);
            await Assert.ThrowsAsync<PostgresException>(() => q.ExecuteNonQueryAsync());   // il ruolo operativo non cancella direttamente
        }
        var path = TempLedger();
        Assert.True(await new DisposalService(db, new DisposalLedger(path)).DisposeAsync(Guid.NewGuid(), "incident", id, user, "Durata approvata trascorsa"));
        Assert.Equal(0, await Count(db, "SELECT count(*) FROM ps.incidents WHERE id = $1", id));
        Assert.Equal(0, await Count(db, "SELECT count(*) FROM ps.incident_actions WHERE incident_id = $1", id));
        File.Delete(path);
    }

    [Fact]
    public async Task Sync_FillsLedgerFileFromDatabase_AfterCrashBetweenCommitAndAppend()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var id = await ClosedRequest(db, practice, user);
        // eliminazione eseguita direttamente nel database (come se l'app si fosse fermata prima di scrivere il file)
        await using (var c = await db.OpenAsync())
        {
            await using var q = new NpgsqlCommand("SELECT ps.dispose_item('rights_request', $1, $2, 'prova', NULL)", c);
            q.Parameters.AddWithValue(id); q.Parameters.AddWithValue(user);
            await q.ExecuteScalarAsync();
        }
        var path = TempLedger();
        var ledger = new DisposalLedger(path);
        Assert.Empty(ledger.Read());
        await new DisposalService(db, ledger).SyncLedgerAsync();
        Assert.Contains(ledger.Read(), e => e.ItemId == id);
        File.Delete(path);
    }

    [Fact]
    public async Task Ledger_IgnoresDuplicates()
    {
        var path = TempLedger();
        var l = new DisposalLedger(path);
        var e = new LedgerEntry("incident", Guid.NewGuid(), DateTimeOffset.UtcNow);
        l.Append(new[] { e }); l.Append(new[] { e });
        Assert.Single(l.Read());
        File.Delete(path);
    }
}
