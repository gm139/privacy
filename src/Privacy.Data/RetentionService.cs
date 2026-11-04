using Npgsql;
using static Privacy.Data.SqlHelpers;

namespace Privacy.Data;

public sealed record PolicyDraft(string RecordClassCode, string TriggerCode, string DurationKindCode, int? Months, int? Days,
    string? Criterion, string? LegalReference, DateOnly ReviewDueOn);

public sealed record ExpiringItem(string RecordClassCode, string ItemType, Guid ItemId, string Reference, DateOnly BaseDate,
    DateOnly ExpiredOn, Guid PolicyVersionId, string Reason);

public sealed record PolicyRow(Guid Id, string RecordClassCode, string TriggerCode, string DurationKindCode, int? Months, int? Days,
    string? Criterion, DateOnly ReviewDueOn, string StatusCode);

public sealed record ManualReviewItem(Guid PolicyVersionId, string RecordClassCode, string DurationKindCode, string Criterion, DateOnly ReviewDueOn);

/// <summary>
/// Flusso F05 (v1 manuale): regole di conservazione approvate dal titolare, elenco degli elementi scaduti con il motivo,
/// registrazione delle eliminazioni. L'app NON elimina da sola: il titolare autorizza ed esegue, l'app registra.
/// </summary>
public sealed class RetentionService
{
    private readonly Db _db;
    public RetentionService(Db db) => _db = db;

    public async Task<Guid> CreateDraftAsync(Guid commandId, Guid practiceId, Guid actorId, PolicyDraft p, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "create_policy", ct) is { } existing) return existing;
        var id = (Guid)(await Scalar(conn, tx, """
            INSERT INTO ps.retention_policy_versions (practice_id, record_class_code, trigger_code, duration_kind_code, duration_months, duration_days,
                criterion, legal_reference, review_due_on, created_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10) RETURNING id
            """, ct, practiceId, p.RecordClassCode, p.TriggerCode, p.DurationKindCode, (object?)p.Months ?? DBNull.Value, (object?)p.Days ?? DBNull.Value,
            (object?)p.Criterion ?? DBNull.Value, (object?)p.LegalReference ?? DBNull.Value, p.ReviewDueOn, actorId))!;
        await Audit(conn, tx, actorId, "retention.create", "retention_policy", id, ct);
        await SetResult(conn, tx, commandId, id, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>Approvazione del titolare. L'eventuale versione approvata precedente della stessa regola diventa «superata».</summary>
    public async Task ApproveAsync(Guid commandId, Guid policyVersionId, Guid approverId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "approve_policy", ct) is not null) return;
        await Exec(conn, tx, """
            UPDATE ps.retention_policy_versions SET status_code = 'superseded'
            WHERE status_code = 'approved' AND policy_key = (SELECT policy_key FROM ps.retention_policy_versions WHERE id = $1)
            """, ct, policyVersionId);
        await Exec(conn, tx, "UPDATE ps.retention_policy_versions SET status_code = 'approved', approved_by = $2, approved_at = now() WHERE id = $1 AND status_code = 'draft'", ct, policyVersionId, approverId);
        await Audit(conn, tx, approverId, "retention.approve", "retention_policy", policyVersionId, ct);
        await SetResult(conn, tx, commandId, policyVersionId, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Elementi custoditi dall'app la cui durata fissa approvata è trascorsa alla data <paramref name="asOf"/>.
    /// Copre richieste, incidenti chiusi e consegne di informative. La documentazione clinica non è nell'app.
    /// </summary>
    public async Task<IReadOnlyList<ExpiringItem>> ListExpiredAsync(Guid practiceId, DateOnly asOf, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            WITH pol AS (
              SELECT id, record_class_code, duration_months, duration_days
              FROM ps.retention_policy_versions
              WHERE practice_id = $1 AND status_code = 'approved' AND duration_kind_code = 'fixed'
            ), items AS (
              SELECT 'requests' AS cls, 'rights_request' AS typ, r.id, r.reference_code AS ref, (r.closed_at AT TIME ZONE 'Europe/Rome')::date AS base
              FROM ps.rights_requests r WHERE r.practice_id = $1 AND r.closed_at IS NOT NULL
              UNION ALL
              SELECT 'incidents', 'incident', i.id, i.reference_code, (i.closed_at AT TIME ZONE 'Europe/Rome')::date
              FROM ps.incidents i WHERE i.practice_id = $1 AND i.closed_at IS NOT NULL
              UNION ALL
              SELECT 'notice_proof', 'notice_delivery', n.id, n.delivery_code, (COALESCE(n.delivered_at, n.created_at) AT TIME ZONE 'Europe/Rome')::date
              FROM ps.notice_deliveries n JOIN ps.document_versions dv ON dv.id = n.document_version_id JOIN ps.documents d ON d.id = dv.document_id
              WHERE d.practice_id = $1
            )
            SELECT i.cls, i.typ, i.id, i.ref, i.base,
                   (i.base + make_interval(months => coalesce(p.duration_months, 0), days => coalesce(p.duration_days, 0)))::date AS expired_on,
                   p.id, p.duration_months, p.duration_days
            FROM items i JOIN pol p ON p.record_class_code = i.cls
            WHERE (i.base + make_interval(months => coalesce(p.duration_months, 0), days => coalesce(p.duration_days, 0)))::date <= $2
            ORDER BY expired_on, i.ref
            """, conn);
        cmd.Parameters.AddWithValue(practiceId);
        cmd.Parameters.AddWithValue(asOf);
        var list = new List<ExpiringItem>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var months = r.IsDBNull(7) ? 0 : r.GetInt32(7); var days = r.IsDBNull(8) ? 0 : r.GetInt32(8);
            list.Add(new ExpiringItem(r.GetString(0), r.GetString(1), r.GetGuid(2), r.GetString(3),
                DateOnly.FromDateTime(r.GetDateTime(4)), DateOnly.FromDateTime(r.GetDateTime(5)), r.GetGuid(6),
                $"Durata approvata {(months > 0 ? months + " mesi" : "")}{(months > 0 && days > 0 ? " + " : "")}{(days > 0 ? days + " giorni" : "")} dalla data di riferimento"));
        }
        return list;
    }

    public async Task<IReadOnlyList<PolicyRow>> ListPoliciesAsync(Guid practiceId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT id, record_class_code, trigger_code, duration_kind_code, duration_months, duration_days, criterion, review_due_on, status_code
            FROM ps.retention_policy_versions WHERE practice_id = $1 AND status_code <> 'superseded' ORDER BY record_class_code, version_number DESC
            """, conn);
        cmd.Parameters.AddWithValue(practiceId);
        var list = new List<PolicyRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new PolicyRow(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetInt32(4),
                r.IsDBNull(5) ? null : r.GetInt32(5), r.IsDBNull(6) ? null : r.GetString(6), DateOnly.FromDateTime(r.GetDateTime(7)), r.GetString(8)));
        return list;
    }

    /// <summary>Regole «illimitata» o «a criterio»: non scadono da sole, vanno riviste alla data di riesame.</summary>
    public async Task<IReadOnlyList<ManualReviewItem>> ListManualReviewAsync(Guid practiceId, DateOnly asOf, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT id, record_class_code, duration_kind_code, criterion, review_due_on FROM ps.retention_policy_versions
            WHERE practice_id = $1 AND status_code = 'approved' AND duration_kind_code <> 'fixed' AND review_due_on <= $2
            ORDER BY review_due_on
            """, conn);
        cmd.Parameters.AddWithValue(practiceId);
        cmd.Parameters.AddWithValue(asOf);
        var list = new List<ManualReviewItem>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new ManualReviewItem(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), DateOnly.FromDateTime(r.GetDateTime(4))));
        return list;
    }

    /// <summary>
    /// Registra un'eliminazione autorizzata dal titolare. La descrizione è generica e NON deve ricreare i dati eliminati
    /// (niente nomi, niente contenuti). Non cancella nulla: l'operazione la esegue il titolare, l'app la documenta.
    /// </summary>
    public async Task<Guid> RecordDisposalAsync(Guid commandId, Guid actorId, string recordClassCode, string genericDescription,
        int itemCount, Guid? policyVersionId, string reason, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "record_disposal", ct) is { } existing) return existing;
        var id = (Guid)(await Scalar(conn, tx, """
            INSERT INTO ps.disposal_records (performed_by, record_class_code, generic_description, item_count, policy_version_id, reason, created_by)
            VALUES ($1,$2,$3,$4,$5,$6,$1) RETURNING id
            """, ct, actorId, recordClassCode, genericDescription, itemCount, (object?)policyVersionId ?? DBNull.Value, reason))!;
        await Audit(conn, tx, actorId, "retention.dispose", "disposal_record", id, ct);
        await SetResult(conn, tx, commandId, id, ct);
        await tx.CommitAsync(ct);
        return id;
    }
}
