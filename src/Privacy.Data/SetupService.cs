using Npgsql;
using Privacy.Domain;
using static Privacy.Data.SqlHelpers;

namespace Privacy.Data;

public sealed record PracticeInput(string Name, string ControllerName, string? ControllerIdentifier, string Address, string PrivacyContact, string? Phone, string? DpoContact);

/// <summary>Prima configurazione: lo studio del titolare e il primo utente. In v1 esiste un solo studio.</summary>
public sealed class SetupService
{
    private readonly Db _db;
    public SetupService(Db db) => _db = db;

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM ps.practices)", conn);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    /// <summary>Crea lo studio (attivo) e il primo utente. Rifiuta se uno studio esiste già o se la password non rispetta la politica.</summary>
    public async Task<(Guid PracticeId, Guid UserId)> CreateFirstAsync(PracticeInput p, string username, string displayName, string password, CancellationToken ct = default)
    {
        var problems = PasswordPolicy.Check(password, username);
        if (problems.Count > 0) throw new ArgumentException("password non accettabile: " + string.Join(", ", problems), nameof(password));
        Guid practice;
        await using (var conn = await _db.OpenAsync(ct))
        await using (var tx = await conn.BeginTransactionAsync(ct))
        {
            await Exec(conn, tx, "LOCK TABLE ps.practices IN EXCLUSIVE MODE", ct);   // due avvii contemporanei non creano due studi
            if ((bool)(await Scalar(conn, tx, "SELECT EXISTS (SELECT 1 FROM ps.practices)", ct))!)
                throw new InvalidOperationException("lo studio è già configurato");
            practice = (Guid)(await Scalar(conn, tx, """
                INSERT INTO ps.practices (name, controller_name, controller_identifier, address, privacy_contact, phone, dpo_contact, status_code)
                VALUES ($1,$2,$3,$4,$5,$6,$7,'active') RETURNING id
                """, ct, p.Name, p.ControllerName, (object?)p.ControllerIdentifier ?? DBNull.Value, p.Address, p.PrivacyContact,
                (object?)p.Phone ?? DBNull.Value, (object?)p.DpoContact ?? DBNull.Value))!;
            await tx.CommitAsync(ct);
        }
        var user = await new AuthService(_db).CreateUserAsync(practice, username, displayName, password, null, ct);
        return (practice, user);
    }
}
