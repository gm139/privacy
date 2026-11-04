using Npgsql;
using Privacy.Data;
using Xunit;

namespace Privacy.Data.IntegrationTests;

/// <summary>Richiede PRIVACY_TEST_CONN (db/start_test_db.sh).</summary>
public class RegistryTests
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("PRIVACY_TEST_CONN");

    private static async Task<(RegistryService svc, Db db, Guid practice, Guid user)> SetupAsync()
    {
        var db = new Db(Conn!);
        await using var c = await db.OpenAsync();
        await using var p = new NpgsqlCommand("INSERT INTO ps.practices (name, controller_name, address, privacy_contact) VALUES ('Studio test','Dott. Test','Via Prova 1','privacy@example.test') RETURNING id", c);
        var practice = (Guid)(await p.ExecuteScalarAsync())!;
        await using var u = new NpgsqlCommand("INSERT INTO ps.users (practice_id, username, display_name, password_verifier) VALUES ($1,'medico','Medico','argon2id$1$x$y$z') RETURNING id", c);
        u.Parameters.AddWithValue(practice);
        return (new RegistryService(db), db, practice, (Guid)(await u.ExecuteScalarAsync())!);
    }

    private static async Task<int> RowVersion(Db db, Guid versionId)
    {
        await using var c = await db.OpenAsync();
        await using var q = new NpgsqlCommand("SELECT row_version FROM ps.activity_versions WHERE id = $1", c);
        q.Parameters.AddWithValue(versionId);
        return (int)(await q.ExecuteScalarAsync())!;
    }

    private static readonly GroundExport HealthGround = new("contract", "health_care", null, "Cura del paziente");

    private static async Task<Guid> CompleteDraft(RegistryService svc, Guid practice, Guid user, string code = "A1")
    {
        var v = await svc.CreateActivityAsync(Guid.NewGuid(), practice, user, code, "Cura dei pazienti");
        await svc.UpdateDraftAsync(v, user, "Gestione della cartella clinica", new DateOnly(2027, 11, 1));
        await svc.AddPurposeAsync(v, user, "Cura", "Diagnosi e terapia", "Art. 9(2)(h): cura prestata da professionista", "not_required", new[] { HealthGround });
        await svc.SetCategoriesAsync(v, user, new[] { "identification", "health_relation" }, new[] { "patients" }, new[] { "none_internal" });
        await svc.SubmitForReviewAsync(v, user);
        return v;
    }

    [Fact]
    public async Task Publish_Incomplete_IsRejected()
    {
        if (Conn is null) return;
        var (svc, db, practice, user) = await SetupAsync();
        var v = await svc.CreateActivityAsync(Guid.NewGuid(), practice, user, "A1", "Vuota");
        await svc.SubmitForReviewAsync(v, user);
        var rv = await RowVersion(db, v);
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            svc.PublishAsync(Guid.NewGuid(), v, user, new DateOnly(2026, 6, 1), rv));
        Assert.Contains("pubblicazione rifiutata", ex.MessageText);
    }

    [Fact]
    public async Task Publish_Complete_ThenPublishedVersionIsImmutable()
    {
        if (Conn is null) return;
        var (svc, db, practice, user) = await SetupAsync();
        var v = await CompleteDraft(svc, practice, user);
        await svc.PublishAsync(Guid.NewGuid(), v, user, new DateOnly(2026, 6, 1), await RowVersion(db, v));
        await Assert.ThrowsAsync<PostgresException>(() => svc.UpdateDraftAsync(v, user, "modifica abusiva", null));
        await Assert.ThrowsAsync<PostgresException>(() => svc.SetCategoriesAsync(v, user, new[] { "contact" }, new[] { "patients" }, new[] { "none_internal" }));
    }

    [Fact]
    public async Task Export_AsOf_IsStable_AfterNewRevision()
    {
        if (Conn is null) return;
        var (svc, db, practice, user) = await SetupAsync();
        var v1 = await CompleteDraft(svc, practice, user);
        await svc.PublishAsync(Guid.NewGuid(), v1, user, new DateOnly(2026, 6, 1), await RowVersion(db, v1));
        var before = await svc.ExportAsOfAsync(practice, new DateOnly(2026, 12, 31));

        // seconda revisione: cambia la descrizione e si aggiunge una finalità
        var v2 = await svc.NewRevisionAsync(v1, user, "Aggiunta finalità amministrativa");
        await svc.UpdateDraftAsync(v2, user, "Descrizione modificata", new DateOnly(2027, 11, 1));
        await svc.AddPurposeAsync(v2, user, "Amministrazione", "Fatturazione", "Obbligo fiscale", "not_required", new[] { new GroundExport("legal_obligation", null, "DPR 633/72", "Fatture") });
        await svc.SubmitForReviewAsync(v2, user);
        await svc.PublishAsync(Guid.NewGuid(), v2, user, new DateOnly(2027, 1, 1), await RowVersion(db, v2));

        var atT = await svc.ExportAsOfAsync(practice, new DateOnly(2026, 12, 31));
        Assert.Equal(before, atT, new ExportComparer());           // il passato non cambia
        Assert.Equal(1, atT.Single().VersionNumber);
        var later = (await svc.ExportAsOfAsync(practice, new DateOnly(2027, 2, 1))).Single();
        Assert.Equal(2, later.VersionNumber);
        Assert.Equal(2, later.Purposes.Count);
        Assert.Contains("health_relation", later.DataCategories);   // le associazioni sono state copiate
    }

    [Fact]
    public async Task Publish_SameVersionTwice_SecondGetsExplicitConflict()
    {
        if (Conn is null) return;
        var (svc, db, practice, user) = await SetupAsync();
        var v = await CompleteDraft(svc, practice, user);
        var rv = await RowVersion(db, v);
        await svc.PublishAsync(Guid.NewGuid(), v, user, new DateOnly(2026, 6, 1), rv);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            svc.PublishAsync(Guid.NewGuid(), v, user, new DateOnly(2026, 6, 1), rv));
    }

    [Fact]
    public async Task Publish_Retry_SameCommand_IsIdempotent()
    {
        if (Conn is null) return;
        var (svc, db, practice, user) = await SetupAsync();
        var v = await CompleteDraft(svc, practice, user);
        var rv = await RowVersion(db, v);
        var cmd = Guid.NewGuid();
        await svc.PublishAsync(cmd, v, user, new DateOnly(2026, 6, 1), rv);
        await svc.PublishAsync(cmd, v, user, new DateOnly(2026, 6, 1), rv);   // nessun errore, nessun doppio effetto
        Assert.Single(await svc.ExportAsOfAsync(practice, new DateOnly(2026, 12, 31)));
    }

    private sealed class ExportComparer : IEqualityComparer<IReadOnlyList<ActivityExport>>
    {
        public bool Equals(IReadOnlyList<ActivityExport>? x, IReadOnlyList<ActivityExport>? y) =>
            x is not null && y is not null && System.Text.Json.JsonSerializer.Serialize(x) == System.Text.Json.JsonSerializer.Serialize(y);
        public int GetHashCode(IReadOnlyList<ActivityExport> o) => System.Text.Json.JsonSerializer.Serialize(o).GetHashCode();
    }
}
