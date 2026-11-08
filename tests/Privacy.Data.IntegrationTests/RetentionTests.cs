using Npgsql;
using Privacy.Data;
using Xunit;

namespace Privacy.Data.IntegrationTests;

/// <summary>Richiede PRIVACY_TEST_CONN (db/start_test_db.sh).</summary>
public class RetentionTests
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

    [Fact]
    public async Task Indefinite_WithoutCriterion_IsRejected_NullDurationIsNeverUnlimited()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var svc = new RetentionService(db);
        await Assert.ThrowsAsync<PostgresException>(() => svc.CreateDraftAsync(Guid.NewGuid(), practice, user,
            new PolicyDraft("requests", "request_closed", "indefinite", null, null, null, null, new DateOnly(2027, 1, 1))));
        await Assert.ThrowsAsync<PostgresException>(() => svc.CreateDraftAsync(Guid.NewGuid(), practice, user,
            new PolicyDraft("requests", "request_closed", "fixed", null, null, null, null, new DateOnly(2027, 1, 1))));
    }

    [Fact]
    public async Task ExpiredList_OnlyApprovedFixedPolicies_WithReason()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var requests = new RightsRequestService(db);
        var svc = new RetentionService(db);

        // richiesta ricevuta e chiusa (chiusura = oggi, regola da 1 mese): non ancora scaduta oggi, scaduta fra 2 mesi
        var id = await requests.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, user, null, "Mario (collab.)", "other", "email",
            DateTimeOffset.UtcNow, "Copia dati", new[] { "access" }));
        await requests.DecideAsync(Guid.NewGuid(), id, user, "access", "granted", null, DateTimeOffset.UtcNow);
        await requests.CloseAsync(Guid.NewGuid(), id, user);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var draft = await svc.CreateDraftAsync(Guid.NewGuid(), practice, user,
            new PolicyDraft("requests", "request_closed", "fixed", 1, null, null, "Scelta del titolare", today.AddYears(1)));
        Assert.Empty(await svc.ListExpiredAsync(practice, today.AddMonths(2)));   // bozza non approvata: nessun effetto
        await svc.ApproveAsync(Guid.NewGuid(), draft, user);
        Assert.Empty(await svc.ListExpiredAsync(practice, today));
        var expired = (await svc.ListExpiredAsync(practice, today.AddMonths(2))).Single();
        Assert.Equal(("requests", id), (expired.RecordClassCode, expired.ItemId));
        Assert.Contains("1 mesi", expired.Reason);
        Assert.Equal(draft, expired.PolicyVersionId);
    }

    [Fact]
    public async Task Indefinite_WithCriterion_ShowsUpForManualReview_NotAsExpired()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var svc = new RetentionService(db);
        var p = await svc.CreateDraftAsync(Guid.NewGuid(), practice, user,
            new PolicyDraft("clinical", "last_visit", "indefinite", null, null, "Da valutare con il consulente", null, new DateOnly(2026, 1, 1)));
        await svc.ApproveAsync(Guid.NewGuid(), p, user);
        Assert.Empty(await svc.ListExpiredAsync(practice, new DateOnly(2100, 1, 1)));
        Assert.Single(await svc.ListManualReviewAsync(practice, new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public async Task RecordDisposal_IsIdempotent_AndAppendOnly()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var svc = new RetentionService(db);
        var cmd = Guid.NewGuid();
        var a = await svc.RecordDisposalAsync(cmd, user, "clinical", "Eliminazione cartelle scadute", 12, null, "Autorizzata dal titolare");
        Assert.Equal(a, await svc.RecordDisposalAsync(cmd, user, "clinical", "Eliminazione cartelle scadute", 12, null, "Autorizzata dal titolare"));
        await using var c = await db.OpenAsync();
        await using var q = new NpgsqlCommand("UPDATE ps.disposal_records SET item_count = 0 WHERE id = $1", c);
        q.Parameters.AddWithValue(a);
        await Assert.ThrowsAsync<PostgresException>(() => q.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task NewVersion_SupersedesPrevious_AndExpiredListDoesNotDuplicate()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var requests = new RightsRequestService(db);
        var svc = new RetentionService(db);
        var id = await requests.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, user, null, "Mario (collab.)", "other", "email",
            DateTimeOffset.UtcNow, "Copia dati", new[] { "access" }));
        await requests.DecideAsync(Guid.NewGuid(), id, user, "access", "granted", null, DateTimeOffset.UtcNow);
        await requests.CloseAsync(Guid.NewGuid(), id, user);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var v1 = await svc.CreateDraftAsync(Guid.NewGuid(), practice, user,
            new PolicyDraft("requests", "request_closed", "fixed", 1, null, null, null, today.AddYears(1)));
        await svc.ApproveAsync(Guid.NewGuid(), v1, user);
        var key = (await svc.ListPoliciesAsync(practice)).Single().PolicyKey;
        var v2 = await svc.CreateDraftAsync(Guid.NewGuid(), practice, user,
            new PolicyDraft("requests", "request_closed", "fixed", 2, null, null, null, today.AddYears(1), key));
        await svc.ApproveAsync(Guid.NewGuid(), v2, user);

        // la versione precedente è superata e la lista scaduti contiene UNA riga sola per la richiesta
        await using var c = await db.OpenAsync();
        await using var q = new NpgsqlCommand("SELECT status_code FROM ps.retention_policy_versions WHERE id = $1", c);
        q.Parameters.AddWithValue(v1);
        Assert.Equal("superseded", (string)(await q.ExecuteScalarAsync())!);
        var expired = await svc.ListExpiredAsync(practice, today.AddMonths(4));
        Assert.Single(expired);
        Assert.Equal(v2, expired[0].PolicyVersionId);
    }

    [Fact]
    public async Task NewVersion_OfUnknownPolicyKey_IsRejected()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var svc = new RetentionService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateDraftAsync(Guid.NewGuid(), practice, user,
            new PolicyDraft("requests", "request_closed", "fixed", 1, null, null, null, new DateOnly(2027, 1, 1), Guid.NewGuid())));
    }

    [Fact]
    public async Task ListPolicies_ShowsDraftsAndApproved_NotSuperseded()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var svc = new RetentionService(db);
        var p = await svc.CreateDraftAsync(Guid.NewGuid(), practice, user, new PolicyDraft("requests", "request_closed", "fixed", 24, null, null, null, new DateOnly(2027, 1, 1)));
        Assert.Equal("draft", (await svc.ListPoliciesAsync(practice)).Single().StatusCode);
        await svc.ApproveAsync(Guid.NewGuid(), p, user);
        var row = (await svc.ListPoliciesAsync(practice)).Single();
        Assert.Equal(("approved", 24), (row.StatusCode, row.Months));
    }
}
