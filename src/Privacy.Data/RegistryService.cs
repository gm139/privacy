using Npgsql;
using static Privacy.Data.SqlHelpers;

namespace Privacy.Data;

public sealed record GroundExport(string Article6Code, string? Article9Code, string? NationalReference, string Scope);
public sealed record PurposeExport(Guid PurposeKey, string Name, string? Description, string? LegalAssessment,
    string ConsentAssessmentCode, IReadOnlyList<GroundExport> Grounds);
public sealed record ActivityExport(string Code, string Name, int VersionNumber, string? Description, DateOnly? EffectiveFrom,
    DateOnly? ReviewDueOn, IReadOnlyList<PurposeExport> Purposes, IReadOnlyList<string> DataCategories,
    IReadOnlyList<string> SubjectCategories, IReadOnlyList<string> RecipientCategories);

/// <summary>Un'altra operazione ha modificato la stessa riga: ricaricare e riprovare (T15).</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }
}

/// <summary>
/// Flusso F01: registro dei trattamenti versionato. Bozza → in verifica → pubblicata (immutabile) → superata.
/// La completezza alla pubblicazione e l'immutabilità sono imposte dal database.
/// </summary>
public sealed class RegistryService
{
    private readonly Db _db;
    public RegistryService(Db db) => _db = db;

    /// <summary>Crea l'attività e la sua prima versione in bozza; restituisce l'id della versione.</summary>
    public async Task<Guid> CreateActivityAsync(Guid commandId, Guid practiceId, Guid actorId, string code, string name, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "create_activity", ct) is { } existing) return existing;
        var activity = (Guid)(await Scalar(conn, tx,
            "INSERT INTO ps.processing_activities (practice_id, code, name, created_by) VALUES ($1,$2,$3,$4) RETURNING id", ct, practiceId, code, name, actorId))!;
        var version = (Guid)(await Scalar(conn, tx,
            "INSERT INTO ps.activity_versions (activity_id, version_number, created_by) VALUES ($1,1,$2) RETURNING id", ct, activity, actorId))!;
        await Audit(conn, tx, actorId, "registry.create_activity", "processing_activity", activity, ct);
        await SetResult(conn, tx, commandId, version, ct);
        await tx.CommitAsync(ct);
        return version;
    }

    public async Task UpdateDraftAsync(Guid versionId, Guid actorId, string? description, DateOnly? reviewDueOn, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await Exec(conn, tx, "UPDATE ps.activity_versions SET description = $2, review_due_on = $3 WHERE id = $1", ct,
            versionId, (object?)description ?? DBNull.Value, (object?)reviewDueOn ?? DBNull.Value);
        await Audit(conn, tx, actorId, "registry.update_draft", "activity_version", versionId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<Guid> AddPurposeAsync(Guid versionId, Guid actorId, string name, string? description, string? legalAssessment,
        string consentAssessmentCode, IReadOnlyList<GroundExport> grounds, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var pid = (Guid)(await Scalar(conn, tx, """
            INSERT INTO ps.purpose_versions (activity_version_id, name, description, legal_assessment, consent_assessment_code, created_by)
            VALUES ($1,$2,$3,$4,$5,$6) RETURNING id
            """, ct, versionId, name, (object?)description ?? DBNull.Value, (object?)legalAssessment ?? DBNull.Value, consentAssessmentCode, actorId))!;
        foreach (var g in grounds)
            await Exec(conn, tx, """
                INSERT INTO ps.purpose_legal_grounds (purpose_version_id, article6_code, article9_code, national_reference, scope, assessment_note, created_by)
                VALUES ($1,$2,$3,$4,$5,$6,$7)
                """, ct, pid, g.Article6Code, (object?)g.Article9Code ?? DBNull.Value, (object?)g.NationalReference ?? DBNull.Value,
                g.Scope, legalAssessment ?? g.Scope, actorId);
        await Audit(conn, tx, actorId, "registry.add_purpose", "activity_version", versionId, ct);
        await tx.CommitAsync(ct);
        return pid;
    }

    public async Task SetCategoriesAsync(Guid versionId, Guid actorId, IReadOnlyList<string> data, IReadOnlyList<string> subjects,
        IReadOnlyList<string> recipients, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        foreach (var (table, codes) in new[] { ("activity_data_categories", data), ("activity_subject_categories", subjects), ("activity_recipient_categories", recipients) })
        {
            await Exec(conn, tx, $"DELETE FROM ps.{table} WHERE activity_version_id = $1", ct, versionId);
            foreach (var code in codes.Distinct())
                await Exec(conn, tx, $"INSERT INTO ps.{table} (activity_version_id, category_code) VALUES ($1,$2)", ct, versionId, code);
        }
        await Audit(conn, tx, actorId, "registry.set_categories", "activity_version", versionId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task SubmitForReviewAsync(Guid versionId, Guid actorId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await Exec(conn, tx, "UPDATE ps.activity_versions SET status_code = 'in_review' WHERE id = $1 AND status_code = 'draft'", ct, versionId);
        await Audit(conn, tx, actorId, "registry.submit_review", "activity_version", versionId, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Approva e pubblica. Rifiutata dal database se la versione è incompleta. La versione pubblicata precedente
    /// diventa «superata» nella stessa transazione. <paramref name="expectedRowVersion"/> evita pubblicazioni concorrenti.
    /// </summary>
    public async Task PublishAsync(Guid commandId, Guid versionId, Guid approverId, DateOnly effectiveFrom, int expectedRowVersion, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "publish_activity", ct) is not null) return;
        // blocco della riga e controllo della versione di riga
        var current = await Scalar(conn, tx, "SELECT row_version FROM ps.activity_versions WHERE id = $1 FOR UPDATE", ct, versionId);
        if (current is null) throw new InvalidOperationException("versione inesistente");
        if ((int)current != expectedRowVersion)
            throw new ConcurrencyConflictException($"la versione è stata modificata (attesa {expectedRowVersion}, attuale {current})");
        await Exec(conn, tx, """
            UPDATE ps.activity_versions SET status_code = 'superseded'
            WHERE status_code = 'published' AND activity_id = (SELECT activity_id FROM ps.activity_versions WHERE id = $1)
            """, ct, versionId);
        await Exec(conn, tx, """
            UPDATE ps.activity_versions SET status_code = 'published', approved_by = $2, approved_at = now(), effective_from = $3
            WHERE id = $1
            """, ct, versionId, approverId, effectiveFrom);
        await Audit(conn, tx, approverId, "registry.publish", "activity_version", versionId, ct);
        await SetResult(conn, tx, commandId, versionId, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Nuova revisione in bozza: copia finalità, presupposti e associazioni della versione di partenza.</summary>
    public async Task<Guid> NewRevisionAsync(Guid fromVersionId, Guid actorId, string changeReason, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var nv = (Guid)(await Scalar(conn, tx, """
            INSERT INTO ps.activity_versions (activity_id, version_number, description, review_due_on, change_reason, created_by)
            SELECT activity_id, (SELECT max(version_number) + 1 FROM ps.activity_versions x WHERE x.activity_id = o.activity_id),
                   description, review_due_on, $2, $3
            FROM ps.activity_versions o WHERE o.id = $1 RETURNING id
            """, ct, fromVersionId, changeReason, actorId))!;
        await Exec(conn, tx, """
            INSERT INTO ps.purpose_versions (activity_version_id, purpose_key, name, description, legal_assessment, consent_assessment_code, created_by)
            SELECT $2, purpose_key, name, description, legal_assessment, consent_assessment_code, $3 FROM ps.purpose_versions WHERE activity_version_id = $1
            """, ct, fromVersionId, nv, actorId);
        await Exec(conn, tx, """
            INSERT INTO ps.purpose_legal_grounds (purpose_version_id, article6_code, article9_code, national_reference, scope, assessment_note, created_by)
            SELECT np.id, g.article6_code, g.article9_code, g.national_reference, g.scope, g.assessment_note, $3
            FROM ps.purpose_legal_grounds g
            JOIN ps.purpose_versions op ON op.id = g.purpose_version_id AND op.activity_version_id = $1
            JOIN ps.purpose_versions np ON np.activity_version_id = $2 AND np.purpose_key = op.purpose_key
            """, ct, fromVersionId, nv, actorId);
        foreach (var t in new[] { ("activity_data_categories", "category_code"), ("activity_subject_categories", "category_code"),
                                  ("activity_recipient_categories", "category_code"), ("activity_systems", "system_id") })
            await Exec(conn, tx, $"INSERT INTO ps.{t.Item1} (activity_version_id, {t.Item2}) SELECT $2, {t.Item2} FROM ps.{t.Item1} WHERE activity_version_id = $1", ct, fromVersionId, nv);
        await Exec(conn, tx, "INSERT INTO ps.activity_measures (activity_version_id, measure_id, measure_description_snapshot) SELECT $2, measure_id, measure_description_snapshot FROM ps.activity_measures WHERE activity_version_id = $1", ct, fromVersionId, nv);
        await Exec(conn, tx, "INSERT INTO ps.activity_engagements (activity_version_id, engagement_id, role_snapshot) SELECT $2, engagement_id, role_snapshot FROM ps.activity_engagements WHERE activity_version_id = $1", ct, fromVersionId, nv);
        await Audit(conn, tx, actorId, "registry.new_revision", "activity_version", nv, ct);
        await tx.CommitAsync(ct);
        return nv;
    }

    /// <summary>
    /// Registro alla data T: per ogni attività la versione pubblicata (o poi superata) in vigore a quella data.
    /// Legge solo righe di versioni immutabili: modifiche successive a fornitori o finalità non cambiano il risultato.
    /// Limite attuale: sistemi, misure e incarichi (snapshot) non sono ancora inclusi nell'esportazione.
    /// </summary>
    public async Task<IReadOnlyList<ActivityExport>> ExportAsOfAsync(Guid practiceId, DateOnly asOf, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        var versions = new List<(Guid Id, string Code, string Name, int N, string? Desc, DateOnly? From, DateOnly? Review)>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT DISTINCT ON (a.id) v.id, a.code, a.name, v.version_number, v.description, v.effective_from, v.review_due_on
            FROM ps.processing_activities a JOIN ps.activity_versions v ON v.activity_id = a.id
            WHERE a.practice_id = $1 AND v.status_code IN ('published','superseded') AND v.effective_from <= $2
            ORDER BY a.id, v.effective_from DESC, v.version_number DESC
            """, conn))
        {
            cmd.Parameters.AddWithValue(practiceId);
            cmd.Parameters.AddWithValue(asOf);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                versions.Add((r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.IsDBNull(4) ? null : r.GetString(4),
                    r.IsDBNull(5) ? null : DateOnly.FromDateTime(r.GetDateTime(5)), r.IsDBNull(6) ? null : DateOnly.FromDateTime(r.GetDateTime(6))));
        }

        var result = new List<ActivityExport>();
        foreach (var v in versions.OrderBy(x => x.Code))
        {
            var purposes = new List<PurposeExport>();
            await using (var cmd = new NpgsqlCommand("SELECT id, purpose_key, name, description, legal_assessment, consent_assessment_code FROM ps.purpose_versions WHERE activity_version_id = $1 ORDER BY name", conn))
            {
                cmd.Parameters.AddWithValue(v.Id);
                var rows = new List<(Guid Id, Guid Key, string Name, string? D, string? L, string C)>();
                await using (var r = await cmd.ExecuteReaderAsync(ct))
                    while (await r.ReadAsync(ct))
                        rows.Add((r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5)));
                foreach (var p in rows)
                {
                    var grounds = new List<GroundExport>();
                    await using var gc = new NpgsqlCommand("SELECT article6_code, article9_code, national_reference, scope FROM ps.purpose_legal_grounds WHERE purpose_version_id = $1 ORDER BY article6_code, article9_code NULLS FIRST, scope", conn);
                    gc.Parameters.AddWithValue(p.Id);
                    await using var gr = await gc.ExecuteReaderAsync(ct);
                    while (await gr.ReadAsync(ct))
                        grounds.Add(new GroundExport(gr.GetString(0), gr.IsDBNull(1) ? null : gr.GetString(1), gr.IsDBNull(2) ? null : gr.GetString(2), gr.GetString(3)));
                    purposes.Add(new PurposeExport(p.Key, p.Name, p.D, p.L, p.C, grounds));
                }
            }
            result.Add(new ActivityExport(v.Code, v.Name, v.N, v.Desc, v.From, v.Review, purposes,
                await Codes(conn, "activity_data_categories", v.Id, ct), await Codes(conn, "activity_subject_categories", v.Id, ct),
                await Codes(conn, "activity_recipient_categories", v.Id, ct)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<string>> Codes(NpgsqlConnection conn, string table, Guid versionId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand($"SELECT category_code FROM ps.{table} WHERE activity_version_id = $1 ORDER BY category_code", conn);
        cmd.Parameters.AddWithValue(versionId);
        var list = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(r.GetString(0));
        return list;
    }
}
