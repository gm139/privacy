using Npgsql;
using static Privacy.Data.SqlHelpers;

namespace Privacy.Data;

public sealed record ReportIncidentCommand(Guid CommandId, Guid PracticeId, Guid ActorId,
    DateTimeOffset DiscoveredAt, DateTimeOffset? AwarenessAt, string Description, string ClassificationCode = "to_assess");

public sealed record AssessIncidentCommand(Guid CommandId, Guid IncidentId, Guid ActorId, string RiskLevelCode, string Reason,
    string Consequences, string NotificationDecisionCode, string SubjectCommunicationDecisionCode,
    long? ApproxSubjects, long? ApproxRecords, string? DelayReason, IReadOnlyList<string> DataCategoryCodes);

public sealed record IncidentSummary(Guid Id, string ReferenceCode, string StatusCode, string ClassificationCode,
    DateTimeOffset? AwarenessAt, DateTimeOffset? NotificationDeadlineAt);

/// <summary>Flusso F04: incidenti. Nessuna decisione automatica: il servizio registra, il medico decide.</summary>
public sealed class IncidentService
{
    private readonly Db _db;
    public IncidentService(Db db) => _db = db;

    public async Task<Guid> ReportAsync(ReportIncidentCommand c, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, c.CommandId, "report_incident", ct) is { } existing) return existing;

        await Exec(conn, tx, "SELECT pg_advisory_xact_lock(hashtext('inc:' || $1::text))", ct, c.PracticeId);
        var year = TimeZoneInfo.ConvertTime(c.DiscoveredAt, TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")).Year;
        var seq = (long)(await Scalar(conn, tx,
            "SELECT count(*) + 1 FROM ps.incidents WHERE practice_id = $1 AND reference_code LIKE $2", ct, c.PracticeId, $"I-{year}-%"))!;
        var id = (Guid)(await Scalar(conn, tx, """
            INSERT INTO ps.incidents (practice_id, reference_code, discovered_at, awareness_at, description, classification_code, assignee_id, created_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$7) RETURNING id
            """, ct, c.PracticeId, $"I-{year}-{seq:0000}", c.DiscoveredAt, (object?)c.AwarenessAt ?? DBNull.Value,
            c.Description, c.ClassificationCode, c.ActorId))!;
        await Audit(conn, tx, c.ActorId, "incident.report", "incident", id, ct);
        await SetResult(conn, tx, c.CommandId, id, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>Imposta l'istante di consapevolezza, da cui partono le 72 ore (il database ricalcola la scadenza; la modifica è tracciata dall'audit).</summary>
    public async Task SetAwarenessAsync(Guid commandId, Guid incidentId, Guid actorId, DateTimeOffset awarenessAt, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "incident_awareness", ct) is not null) return;
        await Exec(conn, tx, "UPDATE ps.incidents SET awareness_at = $2, status_code = CASE WHEN status_code = 'reported' THEN 'assessment' ELSE status_code END WHERE id = $1", ct, incidentId, awarenessAt);
        await Audit(conn, tx, actorId, "incident.awareness", "incident", incidentId, ct);
        await SetResult(conn, tx, commandId, incidentId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<Guid> AssessAsync(AssessIncidentCommand a, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, a.CommandId, "assess_incident", ct) is { } existing) return existing;
        var id = (Guid)(await Scalar(conn, tx, """
            INSERT INTO ps.incident_assessments (incident_id, assessed_at, assessed_by, risk_level_code, reason, approx_subject_count,
                approx_record_count, consequences, notification_decision_code, subject_communication_decision_code, delay_reason, created_by)
            VALUES ($1,now(),$2,$3,$4,$5,$6,$7,$8,$9,$10,$2) RETURNING id
            """, ct, a.IncidentId, a.ActorId, a.RiskLevelCode, a.Reason, (object?)a.ApproxSubjects ?? DBNull.Value,
            (object?)a.ApproxRecords ?? DBNull.Value, a.Consequences, a.NotificationDecisionCode,
            a.SubjectCommunicationDecisionCode, (object?)a.DelayReason ?? DBNull.Value))!;
        foreach (var cat in a.DataCategoryCodes.Distinct())
            await Exec(conn, tx, "INSERT INTO ps.incident_data_categories (assessment_id, category_code) VALUES ($1,$2)", ct, id, cat);
        await Audit(conn, tx, a.ActorId, "incident.assess", "incident", a.IncidentId, ct);
        await SetResult(conn, tx, a.CommandId, id, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>Azione sull'incidente. Per la notifica al Garante servono protocollo/ricevuta, inseriti dopo l'azione effettiva.</summary>
    public async Task AddActionAsync(Guid commandId, Guid incidentId, Guid actorId, string typeCode, DateTimeOffset occurredAt,
        string description, string? protocolReference, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "incident_action", ct) is not null) return;
        await Exec(conn, tx, """
            INSERT INTO ps.incident_actions (incident_id, type_code, occurred_at, description, actor_id, protocol_reference, created_by)
            VALUES ($1,$2,$3,$4,$5,$6,$5)
            """, ct, incidentId, typeCode, occurredAt, description, actorId, (object?)protocolReference ?? DBNull.Value);
        await Audit(conn, tx, actorId, "incident.action." + typeCode, "incident", incidentId, ct);
        await SetResult(conn, tx, commandId, incidentId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task CloseAsync(Guid commandId, Guid incidentId, Guid actorId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "close_incident", ct) is not null) return;
        await Exec(conn, tx, "UPDATE ps.incidents SET status_code = 'closed', closed_at = now() WHERE id = $1", ct, incidentId);
        await Audit(conn, tx, actorId, "incident.close", "incident", incidentId, ct);
        await SetResult(conn, tx, commandId, incidentId, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Incidenti aperti, con scadenza di notifica più vicina per prima (quelli senza consapevolezza in coda).</summary>
    public async Task<IReadOnlyList<IncidentSummary>> ListOpenAsync(Guid practiceId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT id, reference_code, status_code, classification_code, awareness_at, notification_deadline_at
            FROM ps.incidents WHERE practice_id = $1 AND closed_at IS NULL
            ORDER BY notification_deadline_at NULLS LAST, discovered_at
            """, conn);
        cmd.Parameters.AddWithValue(practiceId);
        var list = new List<IncidentSummary>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new IncidentSummary(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3),
                r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5)));
        return list;
    }
}
