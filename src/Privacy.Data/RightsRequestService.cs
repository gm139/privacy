using Npgsql;
using static Privacy.Data.SqlHelpers;

namespace Privacy.Data;

public sealed record RegisterRequestCommand(
    Guid CommandId, Guid PracticeId, Guid ActorId, Guid? SubjectId, string? RequesterName,
    string CapacityCode, string ChannelCode, DateTimeOffset ReceivedAt, string RequestText,
    IReadOnlyList<string> RightCodes);

public sealed record RequestSummary(Guid Id, string ReferenceCode, string StatusCode,
    DateOnly InitialDueOn, DateOnly EffectiveDueOn, DateTimeOffset ReceivedAt);

public sealed record RequestRight(string RightCode, bool Decided, bool ResponseSent);
public sealed record RequestEvent(DateTimeOffset OccurredAt, string TypeCode, string Description);
public sealed record RequestDetail(Guid Id, string ReferenceCode, string StatusCode, string? RequesterName, string RequestText,
    DateTimeOffset ReceivedAt, DateOnly InitialDueOn, DateOnly EffectiveDueOn, bool Closed,
    IReadOnlyList<RequestRight> Rights, IReadOnlyList<RequestEvent> Events);

/// <summary>
/// Flusso F03: richieste dei diritti. Ogni comando è una transazione unica (richiesta + eventi + audit)
/// e ritentabile senza duplicati grazie a ps.processed_commands. Le regole (termini, chiusura) restano nel database.
/// </summary>
public sealed class RightsRequestService
{
    private readonly Db _db;
    public RightsRequestService(Db db) => _db = db;

    /// <summary>Registra una richiesta. Ritentando lo stesso CommandId restituisce la richiesta già creata.</summary>
    public async Task<Guid> RegisterAsync(RegisterRequestCommand c, CancellationToken ct = default)
    {
        if (c.RightCodes.Count == 0) throw new ArgumentException("almeno un diritto", nameof(c));
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        if (await ClaimCommandAsync(conn, tx, c.CommandId, "register_request", ct) is { } existing) return existing;

        // numero progressivo per studio e anno, serializzato per evitare duplicati di riferimento
        await Exec(conn, tx, "SELECT pg_advisory_xact_lock(hashtext('req:' || $1::text))", ct, c.PracticeId);
        var year = TimeZoneInfo.ConvertTime(c.ReceivedAt, TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")).Year;
        // numero progressivo per studio e anno: si parte dal MASSIMO esistente, non dal conteggio, perché
        // un'eliminazione fisica (dispose_item) non deve far riusare un riferimento già assegnato
        var seq = (long)(await Scalar(conn, tx, """
            SELECT (COALESCE(MAX(substring(reference_code from '([0-9]+)$')::int), 0) + 1)::bigint
            FROM ps.rights_requests WHERE practice_id = $1 AND reference_code LIKE $2
            """, ct, c.PracticeId, $"R-{year}-%"))!;
        var reference = $"R-{year}-{seq:0000}";

        var id = (Guid)(await Scalar(conn, tx, """
            INSERT INTO ps.rights_requests (practice_id, subject_id, requester_name, requester_capacity_code, reference_code,
                received_at, channel_code, request_text, assignee_id, created_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$9) RETURNING id
            """, ct, c.PracticeId, (object?)c.SubjectId ?? DBNull.Value, (object?)c.RequesterName ?? DBNull.Value,
            c.CapacityCode, reference, c.ReceivedAt, c.ChannelCode, c.RequestText, c.ActorId))!;

        foreach (var r in c.RightCodes.Distinct())
            await Exec(conn, tx, "INSERT INTO ps.request_rights (request_id, right_code) VALUES ($1,$2)", ct, id, r);

        await AddEvent(conn, tx, id, "received", c.ActorId, $"Richiesta ricevuta ({c.ChannelCode})", ct);
        await Audit(conn, tx, c.ActorId, "request.register", id, ct);
        await Exec(conn, tx, "UPDATE ps.processed_commands SET result_id = $2 WHERE command_id = $1", ct, c.CommandId, id);
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>Proroga motivata (art. 12(3)); il database rifiuta date non in avanti o oltre il limite.</summary>
    public async Task ExtendAsync(Guid commandId, Guid requestId, Guid actorId, DateOnly newDueOn, string reason,
        DateTimeOffset? notifiedAt, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "extend_request", ct) is not null) return;
        await Exec(conn, tx, """
            INSERT INTO ps.request_extensions (request_id, new_due_on, reason, decided_at, notified_at, created_by)
            VALUES ($1,$2,$3,now(),$4,$5)
            """, ct, requestId, newDueOn, reason, (object?)notifiedAt ?? DBNull.Value, actorId);
        await AddEvent(conn, tx, requestId, "extension", actorId, $"Proroga fino al {newDueOn:yyyy-MM-dd}: {reason}", ct);
        await Audit(conn, tx, actorId, "request.extend", requestId, ct);
        await Exec(conn, tx, "UPDATE ps.processed_commands SET result_id = $2 WHERE command_id = $1", ct, commandId, requestId);
        await tx.CommitAsync(ct);
    }

    /// <summary>Decisione su un diritto, con eventuale data di invio del riscontro.</summary>
    public async Task DecideAsync(Guid commandId, Guid requestId, Guid actorId, string rightCode, string outcomeCode,
        string? reason, DateTimeOffset? sentAt, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "decide_request", ct) is not null) return;
        // un diritto si decide una volta sola: senza questo controllo la decisione verrebbe duplicata (e l'audit direbbe «riuscito»)
        var open = (bool)(await Scalar(conn, tx, """
            SELECT EXISTS (SELECT 1 FROM ps.request_rights rr
              WHERE rr.request_id = $1 AND rr.right_code = $2
                AND NOT EXISTS (SELECT 1 FROM ps.request_decisions d WHERE d.request_id = rr.request_id AND d.right_code = rr.right_code))
            """, ct, requestId, rightCode))!;
        if (!open) throw new InvalidOperationException("diritto inesistente o già deciso");
        await Exec(conn, tx, """
            INSERT INTO ps.request_decisions (request_id, right_code, outcome_code, reason, decided_by, decided_at, sent_at, created_by)
            VALUES ($1,$2,$3,$4,$5,now(),$6,$5)
            """, ct, requestId, rightCode, outcomeCode, (object?)reason ?? DBNull.Value, actorId, (object?)sentAt ?? DBNull.Value);
        await AddEvent(conn, tx, requestId, "decision", actorId, $"Decisione su {rightCode}: {outcomeCode}", ct);
        await Audit(conn, tx, actorId, "request.decide", requestId, ct);
        await Exec(conn, tx, "UPDATE ps.processed_commands SET result_id = $2 WHERE command_id = $1", ct, commandId, requestId);
        await tx.CommitAsync(ct);
    }

    /// <summary>Chiusura: il database la rifiuta se manca una decisione con riscontro inviato per ogni diritto.</summary>
    public async Task CloseAsync(Guid commandId, Guid requestId, Guid actorId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "close_request", ct) is not null) return;
        var state = (string?)(await Scalar(conn, tx, "SELECT status_code FROM ps.rights_requests WHERE id = $1", ct, requestId));
        if (state is null) throw new InvalidOperationException("richiesta inesistente");
        if (state is "closed" or "cancelled") throw new InvalidOperationException("richiesta già chiusa");
        await Exec(conn, tx, "UPDATE ps.rights_requests SET status_code = 'closed', closed_at = now() WHERE id = $1", ct, requestId);
        await AddEvent(conn, tx, requestId, "response_sent", actorId, "Richiesta chiusa", ct);
        await Audit(conn, tx, actorId, "request.close", requestId, ct);
        await Exec(conn, tx, "UPDATE ps.processed_commands SET result_id = $2 WHERE command_id = $1", ct, commandId, requestId);
        await tx.CommitAsync(ct);
    }

    /// <summary>Richieste aperte con scadenza entro <paramref name="withinDays"/> giorni (o già scadute), le più urgenti per prime.</summary>
    public async Task<IReadOnlyList<RequestSummary>> ListDueAsync(Guid practiceId, DateOnly today, int withinDays, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT id, reference_code, status_code, initial_due_on, effective_due_on, received_at
            FROM ps.rights_requests
            WHERE practice_id = $1 AND closed_at IS NULL AND effective_due_on <= $2
            ORDER BY effective_due_on, received_at
            """, conn);
        cmd.Parameters.AddWithValue(practiceId);
        cmd.Parameters.AddWithValue(today.AddDays(withinDays));
        var list = new List<RequestSummary>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new RequestSummary(r.GetGuid(0), r.GetString(1), r.GetString(2),
                DateOnly.FromDateTime(r.GetDateTime(3)), DateOnly.FromDateTime(r.GetDateTime(4)),
                r.GetFieldValue<DateTimeOffset>(5)));
        return list;
    }

    public async Task<RequestDetail?> GetDetailAsync(Guid requestId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        RequestDetail? head = null;
        await using (var cmd = new NpgsqlCommand("""
            SELECT id, reference_code, status_code, requester_name, request_text, received_at, initial_due_on, effective_due_on, closed_at IS NOT NULL
            FROM ps.rights_requests WHERE id = $1
            """, conn))
        {
            cmd.Parameters.AddWithValue(requestId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            head = new RequestDetail(r.GetGuid(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4),
                r.GetFieldValue<DateTimeOffset>(5), DateOnly.FromDateTime(r.GetDateTime(6)), DateOnly.FromDateTime(r.GetDateTime(7)), r.GetBoolean(8),
                Array.Empty<RequestRight>(), Array.Empty<RequestEvent>());
        }
        var rights = new List<RequestRight>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT rr.right_code, d.id IS NOT NULL, d.sent_at IS NOT NULL
            FROM ps.request_rights rr LEFT JOIN ps.request_decisions d ON d.request_id = rr.request_id AND d.right_code = rr.right_code
            WHERE rr.request_id = $1 ORDER BY rr.right_code
            """, conn))
        {
            cmd.Parameters.AddWithValue(requestId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) rights.Add(new RequestRight(r.GetString(0), r.GetBoolean(1), r.GetBoolean(2)));
        }
        var events = new List<RequestEvent>();
        await using (var cmd = new NpgsqlCommand("SELECT occurred_at, event_type_code, description FROM ps.request_events WHERE request_id = $1 ORDER BY occurred_at, created_at", conn))
        {
            cmd.Parameters.AddWithValue(requestId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) events.Add(new RequestEvent(r.GetFieldValue<DateTimeOffset>(0), r.GetString(1), r.GetString(2)));
        }
        return head with { Rights = rights, Events = events };
    }

    // ---- utilità interne

    private static Task AddEvent(NpgsqlConnection c, NpgsqlTransaction tx, Guid requestId, string type, Guid actor, string text, CancellationToken ct)
        => Exec(c, tx, "INSERT INTO ps.request_events (request_id, event_type_code, actor_id, description, created_by) VALUES ($1,$2,$3,$4,$3)", ct, requestId, type, actor, text);

    private static Task Audit(NpgsqlConnection c, NpgsqlTransaction tx, Guid actor, string action, Guid target, CancellationToken ct)
        => SqlHelpers.Audit(c, tx, actor, action, "rights_request", target, ct);
}
