using Npgsql;
using static Privacy.Data.SqlHelpers;

namespace Privacy.Data;

public sealed record SubjectInput(string FirstName, string LastName, DateOnly? BirthDate, string? TaxCode, string? Email, string? Phone);

public sealed record SubjectRecord(Guid Id, string InternalCode, string FirstName, string LastName, DateOnly? BirthDate,
    string? TaxCode, string? Email, string? Phone, string StatusCode);

/// <summary>Anagrafica degli interessati: creazione, rettifica (art. 16) e rimozione (F07, previa valutazione del titolare).</summary>
public sealed class SubjectService
{
    private readonly Db _db;
    public SubjectService(Db db) => _db = db;

    public async Task<Guid> CreateAsync(Guid commandId, Guid practiceId, Guid actorId, SubjectInput s, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "create_subject", ct) is { } existing) return existing;
        await Exec(conn, tx, "SELECT pg_advisory_xact_lock(hashtext('subj:' || $1::text))", ct, practiceId);
        var seq = (long)(await Scalar(conn, tx, "SELECT count(*) + 1 FROM ps.data_subjects WHERE practice_id = $1", ct, practiceId))!;
        var id = (Guid)(await Scalar(conn, tx, """
            INSERT INTO ps.data_subjects (practice_id, internal_code, first_name, last_name, birth_date, tax_code, email, phone, created_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9) RETURNING id
            """, ct, practiceId, $"P-{seq:000000}", s.FirstName, s.LastName, (object?)s.BirthDate ?? DBNull.Value,
            (object?)s.TaxCode ?? DBNull.Value, (object?)s.Email ?? DBNull.Value, (object?)s.Phone ?? DBNull.Value, actorId))!;
        await Audit(conn, tx, actorId, "subject.create", "data_subject", id, ct);
        await SetResult(conn, tx, commandId, id, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>
    /// Rettifica dell'anagrafica corrente. Le consegne e i documenti già emessi NON cambiano (usano gli snapshot);
    /// la rettifica è annotata in audit. Un interessato rimosso non è modificabile (lo impedisce il database).
    /// </summary>
    public async Task RectifyAsync(Guid commandId, Guid subjectId, Guid actorId, SubjectInput s, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "rectify_subject", ct) is not null) return;
        await Exec(conn, tx, """
            UPDATE ps.data_subjects SET first_name = $2, last_name = $3, birth_date = $4, tax_code = $5, email = $6, phone = $7 WHERE id = $1
            """, ct, subjectId, s.FirstName, s.LastName, (object?)s.BirthDate ?? DBNull.Value,
            (object?)s.TaxCode ?? DBNull.Value, (object?)s.Email ?? DBNull.Value, (object?)s.Phone ?? DBNull.Value);
        await Audit(conn, tx, actorId, "subject.rectify", "data_subject", subjectId, ct);
        await SetResult(conn, tx, commandId, subjectId, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Rimozione dell'interessato (procedura ps.remove_subject): anagrafica azzerata, snapshot delle consegne anonimizzati,
    /// file collegati svuotati, disposal_record e audit. Serve la motivazione della valutazione del titolare.
    /// </summary>
    public async Task RemoveAsync(Guid commandId, Guid subjectId, Guid actorId, string ownerAssessmentReason, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        if (await ClaimCommandAsync(conn, tx, commandId, "remove_subject", ct) is not null) return;
        await Exec(conn, tx, "SELECT ps.remove_subject($1, $2, $3)", ct, subjectId, actorId, ownerAssessmentReason);
        await SetResult(conn, tx, commandId, subjectId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<SubjectRecord?> GetAsync(Guid subjectId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id, internal_code, first_name, last_name, birth_date, tax_code, email, phone, status_code FROM ps.data_subjects WHERE id = $1", conn);
        cmd.Parameters.AddWithValue(subjectId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new SubjectRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3),
            r.IsDBNull(4) ? null : DateOnly.FromDateTime(r.GetDateTime(4)), r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetString(8));
    }

    /// <summary>Ricerca per nome, cognome o codice interno (senza distinguere maiuscole). Gli interessati rimossi restano in elenco, anonimi.</summary>
    public async Task<IReadOnlyList<SubjectRecord>> SearchAsync(Guid practiceId, string? text, int limit = 200, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT id, internal_code, first_name, last_name, birth_date, tax_code, email, phone, status_code FROM ps.data_subjects
            WHERE practice_id = $1 AND ($2::text IS NULL OR (first_name || ' ' || last_name) ILIKE '%' || $2 || '%' OR internal_code ILIKE '%' || $2 || '%')
            ORDER BY last_name, first_name LIMIT $3
            """, conn);
        cmd.Parameters.AddWithValue(practiceId);
        cmd.Parameters.AddWithValue(string.IsNullOrWhiteSpace(text) ? DBNull.Value : text.Trim());
        cmd.Parameters.AddWithValue(limit);
        var list = new List<SubjectRecord>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new SubjectRecord(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3),
                r.IsDBNull(4) ? null : DateOnly.FromDateTime(r.GetDateTime(4)), r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetString(8)));
        return list;
    }
}
