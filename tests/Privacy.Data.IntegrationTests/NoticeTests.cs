using Npgsql;
using Privacy.Data;
using Privacy.Domain;
using Xunit;

namespace Privacy.Data.IntegrationTests;

/// <summary>Richiede PRIVACY_TEST_CONN (db/start_test_db.sh).</summary>
public class NoticeTests
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("PRIVACY_TEST_CONN");
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private const string Template = "Informativa di {{practice_name}} - {{address}}. Contatti privacy: {{privacy_contact}}. Versione {{document_version}} del {{effective_date}}.";

    private sealed record Ctx(Db Db, NoticeService Notices, Guid Practice, Guid User, Guid ActivityVersion);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new Db(Conn!);
        Guid practice, user;
        await using (var c = await db.OpenAsync())
        {
            await using var p = new NpgsqlCommand("INSERT INTO ps.practices (name, controller_name, address, privacy_contact) VALUES ('Studio Rossi','Dott. Rossi','Via Roma 1','privacy@example.test') RETURNING id", c);
            practice = (Guid)(await p.ExecuteScalarAsync())!;
            await using var u = new NpgsqlCommand("INSERT INTO ps.users (practice_id, username, display_name, password_verifier) VALUES ($1,'medico','Medico','argon2id$1$x$y$z') RETURNING id", c);
            u.Parameters.AddWithValue(practice);
            user = (Guid)(await u.ExecuteScalarAsync())!;
        }
        var reg = new RegistryService(db);
        var v = await reg.CreateActivityAsync(Guid.NewGuid(), practice, user, "A1", "Cura");
        await reg.UpdateDraftAsync(v, user, "Cartella clinica", new DateOnly(2027, 11, 1));
        await reg.AddPurposeAsync(v, user, "Cura", "Terapia", "Art. 9(2)(h)", "not_required", new[] { new GroundExport("contract", "health_care", null, "Cura") });
        await reg.SetCategoriesAsync(v, user, new[] { "identification", "health_relation" }, new[] { "patients" }, new[] { "none_internal" });
        await reg.SubmitForReviewAsync(v, user);
        int rv;
        await using (var c = await db.OpenAsync())
        {
            await using var q = new NpgsqlCommand("SELECT row_version FROM ps.activity_versions WHERE id = $1", c);
            q.Parameters.AddWithValue(v);
            rv = (int)(await q.ExecuteScalarAsync())!;
        }
        await reg.PublishAsync(Guid.NewGuid(), v, user, new DateOnly(2026, 6, 1), rv);
        return new Ctx(db, new NoticeService(db), practice, user, v);
    }

    private static async Task<Guid> PublishedNotice(Ctx x, string template = Template)
    {
        var nv = await x.Notices.CreateDraftAsync(Guid.NewGuid(), x.Practice, x.User, "Informativa pazienti", template);
        await x.Notices.PublishAsync(Guid.NewGuid(), nv, x.User, new DateOnly(2026, 6, 1), new[] { x.ActivityVersion }, Key);
        return nv;
    }

    private static async Task<Guid> NewSubject(Ctx x, string first, string last)
    {
        await using var c = await x.Db.OpenAsync();
        await using var q = new NpgsqlCommand("INSERT INTO ps.data_subjects (practice_id, internal_code, first_name, last_name) VALUES ($1,$2,$3,$4) RETURNING id", c);
        q.Parameters.AddWithValue(x.Practice); q.Parameters.AddWithValue(Guid.NewGuid().ToString("N")[..8]); q.Parameters.AddWithValue(first); q.Parameters.AddWithValue(last);
        return (Guid)(await q.ExecuteScalarAsync())!;
    }

    private static RecordDeliveryCommand Delivery(Ctx x, Guid version, Guid subject, string status = "delivered_unsigned", Guid? rep = null, string? sig = null, Guid? cmd = null) =>
        new(cmd ?? Guid.NewGuid(), version, subject, rep, x.User, "in_person", status,
            status == "failed_attempt" ? null : new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.FromHours(2)), sig, sig is null ? null : "genitore", null);

    [Fact]
    public async Task Publish_ResolvesPlaceholders_RejectsUnknownVariable()
    {
        if (Conn is null) return;
        var x = await SetupAsync();
        var nv = await PublishedNotice(x);
        await using (var c = await x.Db.OpenAsync())
        {
            await using var q = new NpgsqlCommand("SELECT body FROM ps.document_versions WHERE id = $1", c);
            q.Parameters.AddWithValue(nv);
            var body = (string)(await q.ExecuteScalarAsync())!;
            Assert.DoesNotContain("{{", body);
            Assert.Contains("Via Roma 1", body);
        }
        var bad = await x.Notices.CreateDraftAsync(Guid.NewGuid(), x.Practice, x.User, "Errata", "Segreto: {{password}}");
        await Assert.ThrowsAsync<TemplateException>(() => x.Notices.PublishAsync(Guid.NewGuid(), bad, x.User, new DateOnly(2026, 6, 1), new[] { x.ActivityVersion }, Key));
    }

    [Fact]
    public async Task Publish_WithoutRegistryLink_IsRejected()
    {
        if (Conn is null) return;
        var x = await SetupAsync();
        var nv = await x.Notices.CreateDraftAsync(Guid.NewGuid(), x.Practice, x.User, "Senza registro", Template);
        await Assert.ThrowsAsync<PostgresException>(() => x.Notices.PublishAsync(Guid.NewGuid(), nv, x.User, new DateOnly(2026, 6, 1), Array.Empty<Guid>(), Key));
    }

    [Fact]
    public async Task Delivery_Unsigned_IsRecordedAsSuch_AndRetryDoesNotDuplicate()
    {
        if (Conn is null) return;
        var x = await SetupAsync();
        var nv = await PublishedNotice(x);
        var s = await NewSubject(x, "Anna", "Verdi");
        var cmd = Delivery(x, nv, s);
        var (id, code) = await x.Notices.RecordDeliveryAsync(cmd, Key);
        var again = await x.Notices.RecordDeliveryAsync(cmd, Key);
        Assert.Equal((id, code), again);
        await using var c = await x.Db.OpenAsync();
        await using var q = new NpgsqlCommand("SELECT count(*), min(receipt_status_code) FROM ps.notice_deliveries WHERE subject_id = $1", c);
        q.Parameters.AddWithValue(s);
        await using var r = await q.ExecuteReaderAsync();
        await r.ReadAsync();
        Assert.Equal(1L, r.GetInt64(0));
        Assert.Equal("delivered_unsigned", r.GetString(1));
    }

    [Fact]
    public async Task Reconstruct_AfterPracticeDataChange_GivesSameHmac()
    {
        if (Conn is null) return;
        var x = await SetupAsync();
        var nv = await PublishedNotice(x);
        var s = await NewSubject(x, "Anna", "Verdi");
        var (id, _) = await x.Notices.RecordDeliveryAsync(Delivery(x, nv, s), Key);
        await using (var c = await x.Db.OpenAsync())
        {
            await using var q = new NpgsqlCommand("UPDATE ps.practices SET address = 'Nuova sede 99', privacy_contact = 'nuovo@example.test' WHERE id = $1", c);
            q.Parameters.AddWithValue(x.Practice);
            await q.ExecuteNonQueryAsync();
        }
        var rendering = await x.Notices.ReconstructAsync(id, Key);
        Assert.True(rendering.Matches);
        Assert.Contains("Via Roma 1", rendering.Text);
        Assert.DoesNotContain("Nuova sede", rendering.Text);
        Assert.False((await x.Notices.ReconstructAsync(id, new byte[32])).Matches);   // chiave diversa: non coincide
    }

    [Fact]
    public async Task Delivery_WithExpiredRepresentation_IsRejected()
    {
        if (Conn is null) return;
        var x = await SetupAsync();
        var nv = await PublishedNotice(x);
        var minor = await NewSubject(x, "Luca", "Bianchi");
        var parent = await NewSubject(x, "Marta", "Bianchi");
        Guid rep;
        await using (var c = await x.Db.OpenAsync())
        {
            await using var q = new NpgsqlCommand("INSERT INTO ps.representations (subject_id, representative_id, capacity_code, scope, valid_from, valid_until, verified_at, verified_by) VALUES ($1,$2,'parental_responsibility','Tutte le informative','2020-01-01','2025-12-31', now(), $3) RETURNING id", c);
            q.Parameters.AddWithValue(minor); q.Parameters.AddWithValue(parent); q.Parameters.AddWithValue(x.User);
            rep = (Guid)(await q.ExecuteScalarAsync())!;
        }
        await Assert.ThrowsAsync<PostgresException>(() => x.Notices.RecordDeliveryAsync(Delivery(x, nv, minor, rep: rep, sig: "Marta Bianchi"), Key));
    }

    [Fact]
    public async Task Lists_PublishedNotices_ActivitiesAndDeliveries_AndSubjectSearch()
    {
        if (Conn is null) return;
        var x = await SetupAsync();
        var nv = await PublishedNotice(x);
        var s = await NewSubject(x, "Anna", "Verdi");
        await x.Notices.RecordDeliveryAsync(Delivery(x, nv, s), Key);
        var notice = (await x.Notices.ListPublishedAsync(x.Practice)).Single();
        Assert.Equal(("Informativa pazienti", 1), (notice.Title, notice.VersionNumber));
        Assert.Single(await new RegistryService(x.Db).ListPublishedAsync(x.Practice));
        var delivery = (await x.Notices.ListDeliveriesAsync(s)).Single();
        Assert.Equal("delivered_unsigned", delivery.ReceiptStatusCode);
        var subjects = new SubjectService(x.Db);
        Assert.Single(await subjects.SearchAsync(x.Practice, "verd"));
        Assert.Empty(await subjects.SearchAsync(x.Practice, "nessuno-con-questo-nome"));
        Assert.Single(await subjects.SearchAsync(x.Practice, null));
    }
}
