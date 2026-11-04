using Npgsql;
using Privacy.Domain;
using static Privacy.Data.SqlHelpers;

namespace Privacy.Data;

public sealed record RecordDeliveryCommand(Guid CommandId, Guid VersionId, Guid SubjectId, Guid? RepresentationId, Guid ActorId,
    string ChannelCode, string ReceiptStatusCode, DateTimeOffset? DeliveredAt, string? SignatoryName, string? SignatoryCapacity, string? Note);

public sealed record DeliveryRendering(Guid DeliveryId, string DeliveryCode, string Text, byte[] StoredHmac, byte[] RecomputedHmac)
{
    public bool Matches => StoredHmac.AsSpan().SequenceEqual(RecomputedHmac);
}

/// <summary>Flusso F02: informative (versioni approvate e pubblicate) e registro delle consegne.</summary>
public sealed class NoticeService
{
    private readonly Db _db;
    public NoticeService(Db db) => _db = db;

    /// <summary>Crea il documento e la versione 1 in bozza con il testo del modello (con segnaposto).</summary>
    public async Task<Guid> CreateDraftAsync(Guid commandId, Guid practiceId, Guid actorId, string title, string templateBody, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "create_notice", ct) is { } existing) return existing;
        var doc = (Guid)(await Scalar(conn, tx, "INSERT INTO ps.documents (practice_id, type_code, title, created_by) VALUES ($1,'notice',$2,$3) RETURNING id", ct, practiceId, title, actorId))!;
        var v = (Guid)(await Scalar(conn, tx, "INSERT INTO ps.document_versions (document_id, version_number, language_code, body, created_by) VALUES ($1,1,'it',$2,$3) RETURNING id", ct, doc, templateBody, actorId))!;
        await Audit(conn, tx, actorId, "notice.create", "document_version", v, ct);
        await SetResult(conn, tx, commandId, v, ct);
        await tx.CommitAsync(ct);
        return v;
    }

    /// <summary>
    /// Risolve i segnaposto con i dati correnti dello studio, collega le versioni di attività pubblicate,
    /// calcola l'HMAC del contenuto, approva e pubblica. Da qui il testo non cambia più.
    /// </summary>
    public async Task PublishAsync(Guid commandId, Guid versionId, Guid approverId, DateOnly effectiveFrom,
        IReadOnlyList<Guid> activityVersionIds, byte[] hmacKey, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "publish_notice", ct) is not null) return;

        string template; int number; Dictionary<string, string> values;
        await using (var cmd = new NpgsqlCommand("""
            SELECT dv.body, dv.version_number, p.name, p.controller_name, p.controller_identifier, p.address, p.privacy_contact, p.phone, p.dpo_contact
            FROM ps.document_versions dv JOIN ps.documents d ON d.id = dv.document_id JOIN ps.practices p ON p.id = d.practice_id
            WHERE dv.id = $1 AND dv.status_code = 'draft' FOR UPDATE OF dv
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(versionId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) throw new InvalidOperationException("versione inesistente o non in bozza");
            template = r.GetString(0); number = r.GetInt32(1);
            values = new Dictionary<string, string>
            {
                ["practice_name"] = r.GetString(2), ["controller_name"] = r.GetString(3),
                ["controller_identifier"] = r.IsDBNull(4) ? "" : r.GetString(4), ["address"] = r.GetString(5),
                ["privacy_contact"] = r.GetString(6), ["phone"] = r.IsDBNull(7) ? "" : r.GetString(7),
                ["dpo_contact"] = r.IsDBNull(8) ? "" : r.GetString(8),
                ["document_version"] = number.ToString(), ["effective_date"] = effectiveFrom.ToString("dd/MM/yyyy")
            };
        }
        var body = TemplateResolver.Resolve(template, values);   // eccezione se variabile non ammessa o dato mancante
        var hmac = NoticeDocument.Hmac(hmacKey, body);

        await Exec(conn, tx, "UPDATE ps.document_versions SET body = $2 WHERE id = $1", ct, versionId, body);
        foreach (var av in activityVersionIds.Distinct())
            await Exec(conn, tx, "INSERT INTO ps.notice_activity_versions (document_version_id, activity_version_id) VALUES ($1,$2)", ct, versionId, av);
        await Exec(conn, tx, "UPDATE ps.document_versions SET status_code = 'approved', approved_by = $2, approved_at = now(), content_hmac = $3, effective_from = $4 WHERE id = $1", ct, versionId, approverId, hmac, effectiveFrom);
        await Exec(conn, tx, "UPDATE ps.document_versions SET status_code = 'published' WHERE id = $1", ct, versionId);
        await Audit(conn, tx, approverId, "notice.publish", "document_version", versionId, ct);
        await SetResult(conn, tx, commandId, versionId, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Registra la consegna (non il consenso). Salva gli snapshot dei nomi e l'HMAC del modulo emesso.</summary>
    public async Task<(Guid Id, string DeliveryCode)> RecordDeliveryAsync(RecordDeliveryCommand c, byte[] hmacKey, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, c.CommandId, "record_delivery", ct) is { } existing)
        {
            var code = (string)(await Scalar(conn, tx, "SELECT delivery_code FROM ps.notice_deliveries WHERE id = $1", ct, existing))!;
            return (existing, code);
        }
        string name, body; int number;
        await using (var cmd = new NpgsqlCommand("""
            SELECT s.first_name || ' ' || s.last_name, dv.body, dv.version_number
            FROM ps.data_subjects s, ps.document_versions dv WHERE s.id = $1 AND dv.id = $2
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(c.SubjectId); cmd.Parameters.AddWithValue(c.VersionId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) throw new InvalidOperationException("interessato o versione inesistente");
            name = r.GetString(0); body = r.GetString(1); number = r.GetInt32(2);
        }
        var deliveryCode = NoticeDocument.NewDeliveryCode();
        DateOnly? date = c.DeliveredAt is { } da ? DeadlineCalculator.ReceivedDate(da) : null;   // tentativo fallito: nessuna data di consegna
        var text = NoticeDocument.Render(new NoticeRenderInput(body, number, deliveryCode, name, date, c.SignatoryName, c.SignatoryCapacity));
        var hmac = NoticeDocument.Hmac(hmacKey, text);
        var id = (Guid)(await Scalar(conn, tx, """
            INSERT INTO ps.notice_deliveries (document_version_id, subject_id, representation_id, delivery_code, delivered_at, channel_code,
                receipt_status_code, subject_name_snapshot, signatory_name_snapshot, signatory_capacity_snapshot, rendered_hmac, recorded_by, note, created_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$12) RETURNING id
            """, ct, c.VersionId, c.SubjectId, (object?)c.RepresentationId ?? DBNull.Value, deliveryCode, (object?)c.DeliveredAt ?? DBNull.Value,
            c.ChannelCode, c.ReceiptStatusCode, name, (object?)c.SignatoryName ?? DBNull.Value, (object?)c.SignatoryCapacity ?? DBNull.Value,
            hmac, c.ActorId, (object?)c.Note ?? DBNull.Value))!;
        await Audit(conn, tx, c.ActorId, "notice.deliver", "notice_delivery", id, ct);
        await SetResult(conn, tx, c.CommandId, id, ct);
        await tx.CommitAsync(ct);
        return (id, deliveryCode);
    }

    /// <summary>Ricostruisce il modulo di una consegna passata dai soli dati immutabili e ne ricalcola l'HMAC.</summary>
    public async Task<DeliveryRendering> ReconstructAsync(Guid deliveryId, byte[] hmacKey, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT n.delivery_code, dv.body, dv.version_number, n.subject_name_snapshot, n.delivered_at,
                   n.signatory_name_snapshot, n.signatory_capacity_snapshot, n.rendered_hmac
            FROM ps.notice_deliveries n JOIN ps.document_versions dv ON dv.id = n.document_version_id WHERE n.id = $1
            """, conn);
        cmd.Parameters.AddWithValue(deliveryId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new InvalidOperationException("consegna inesistente");
        DateOnly? date = r.IsDBNull(4) ? null : DeadlineCalculator.ReceivedDate(r.GetFieldValue<DateTimeOffset>(4));
        var code = r.GetString(0);
        var text = NoticeDocument.Render(new NoticeRenderInput(r.GetString(1), r.GetInt32(2), code, r.GetString(3),
            date, r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6)));
        return new DeliveryRendering(deliveryId, code, text, r.GetFieldValue<byte[]>(7), NoticeDocument.Hmac(hmacKey, text));
    }
}
