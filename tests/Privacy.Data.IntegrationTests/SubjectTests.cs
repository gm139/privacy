using Npgsql;
using Privacy.Data;
using Xunit;

namespace Privacy.Data.IntegrationTests;

/// <summary>Richiede PRIVACY_TEST_CONN (db/start_test_db.sh).</summary>
public class SubjectTests
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("PRIVACY_TEST_CONN");
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly SubjectInput Anna = new("Anna", "Verdi", new DateOnly(1980, 3, 5), "VRDNNA80C45H501X", "anna@example.test", "3330000000");

    private static async Task<(Db db, Guid practice, Guid user)> SetupAsync()
    {
        var db = new Db(Conn!);
        await using var c = await db.OpenAsync();
        await using var p = new NpgsqlCommand("INSERT INTO ps.practices (name, controller_name, address, privacy_contact) VALUES ('Studio Rossi','Dott. Rossi','Via Roma 1','privacy@example.test') RETURNING id", c);
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

    [Fact]
    public async Task Create_Retry_SameCommand_NoDuplicate_AndRectifyChangesOnlyCurrentRecord()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var svc = new SubjectService(db);
        var cmd = Guid.NewGuid();
        var id = await svc.CreateAsync(cmd, practice, user, Anna);
        Assert.Equal(id, await svc.CreateAsync(cmd, practice, user, Anna));
        Assert.Equal(1, await Count(db, "SELECT count(*) FROM ps.data_subjects WHERE practice_id = $1", practice));
        await svc.RectifyAsync(Guid.NewGuid(), id, user, Anna with { Phone = "3339999999" });
        Assert.Equal("3339999999", (await svc.GetAsync(id))!.Phone);
        Assert.Equal(1, await Count(db, "SELECT count(*) FROM ps.audit_events WHERE target_id = $1 AND action_code = 'subject.rectify'", id));
    }

    [Fact]
    public async Task Remove_ErasesPersonalData_KeepsAnonymisedProof_DisposesFiles()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var subjects = new SubjectService(db);
        var id = await subjects.CreateAsync(Guid.NewGuid(), practice, user, Anna);
        var code = (await subjects.GetAsync(id))!.InternalCode;

        // informativa pubblicata + consegna con ricevuta firmata (file di prova)
        var reg = new RegistryService(db);
        var v = await reg.CreateActivityAsync(Guid.NewGuid(), practice, user, "A1", "Cura");
        await reg.UpdateDraftAsync(v, user, "Cartella", new DateOnly(2027, 11, 1));
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
        var notices = new NoticeService(db);
        var nv = await notices.CreateDraftAsync(Guid.NewGuid(), practice, user, "Informativa", "Informativa di {{practice_name}}");
        await notices.PublishAsync(Guid.NewGuid(), nv, user, new DateOnly(2026, 6, 1), new[] { v }, Key);
        var (deliveryId, _) = await notices.RecordDeliveryAsync(new RecordDeliveryCommand(Guid.NewGuid(), nv, id, null, user, "in_person",
            "signed_receipt", new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.FromHours(2)), null, null, "nota con dati"), Key);
        Guid file;
        await using (var c = await db.OpenAsync())
        {
            await using var f = new NpgsqlCommand("""
                INSERT INTO ps.files (media_type, size_bytes, ciphertext, nonce, auth_tag, wrapped_key, key_version, plaintext_hmac)
                VALUES ('application/pdf', 3, '\x010203', $1, $2, '\x0a', 'k1', $3) RETURNING id
                """, c);
            f.Parameters.AddWithValue(new byte[12]); f.Parameters.AddWithValue(new byte[16]); f.Parameters.AddWithValue(new byte[32]);
            file = (Guid)(await f.ExecuteScalarAsync())!;
            await using var s = new NpgsqlCommand("INSERT INTO ps.signature_evidence (delivery_id, signed_file_id, signature_type_code) VALUES ($1,$2,'wet_scan')", c);
            s.Parameters.AddWithValue(deliveryId); s.Parameters.AddWithValue(file);
            await s.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<PostgresException>(() => subjects.RemoveAsync(Guid.NewGuid(), id, user, "  "));   // motivazione obbligatoria
        var removeCmd = Guid.NewGuid();
        await subjects.RemoveAsync(removeCmd, id, user, "Richiesta art. 17 accolta dopo valutazione del titolare");
        await subjects.RemoveAsync(removeCmd, id, user, "Richiesta art. 17 accolta dopo valutazione del titolare");   // ritentato: nessun errore

        var after = (await subjects.GetAsync(id))!;
        Assert.Equal("removed", after.StatusCode);
        Assert.Equal(code, after.InternalCode);                       // il codice interno resta
        Assert.Equal(("Interessato", "rimosso"), (after.FirstName, after.LastName));
        Assert.Null(after.TaxCode); Assert.Null(after.Email); Assert.Null(after.Phone); Assert.Null(after.BirthDate);
        Assert.Equal(1, await Count(db, "SELECT count(*) FROM ps.notice_deliveries WHERE id = $1 AND subject_name_snapshot = 'Interessato rimosso' AND note IS NULL", deliveryId));
        Assert.Equal(1, await Count(db, "SELECT count(*) FROM ps.files WHERE id = $1 AND status_code = 'disposed' AND ciphertext IS NULL", file));
        Assert.Equal(1, await Count(db, "SELECT count(*) FROM ps.audit_events WHERE target_id = $1 AND action_code = 'subject.remove'", id));
        // il modulo non si ricostruisce più identico: i dati sono stati cancellati
        Assert.False((await notices.ReconstructAsync(deliveryId, Key)).Matches);
        // e l'interessato rimosso non è più modificabile
        await Assert.ThrowsAsync<PostgresException>(() => subjects.RectifyAsync(Guid.NewGuid(), id, user, Anna));
    }
}
