using Npgsql;
using Privacy.Domain;
using static Privacy.Data.SqlHelpers;

namespace Privacy.Data;

public enum LoginOutcome { Success, InvalidCredentials, Locked, Disabled }

public sealed record LoginResult(LoginOutcome Outcome, Guid? UserId, string? DisplayName, DateTimeOffset? LockedUntil, bool MustChangePassword = false);

/// <summary>
/// Accesso (v1: un solo utente, il medico). Il blocco dopo troppi tentativi è nel database, quindi resiste al riavvio (T19).
/// Messaggi di esito uniformi: utente inesistente e password errata non si distinguono. Mai password o verificatori in audit o log.
/// </summary>
public sealed class AuthService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly Db _db;
    public AuthService(Db db) => _db = db;

    public async Task<Guid> CreateUserAsync(Guid practiceId, string username, string displayName, string password, Guid? createdBy = null, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var id = await CreateUserInTransactionAsync(conn, tx, practiceId, username, displayName, password, createdBy, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>
    /// Creazione dell'utente dentro una transazione già aperta. Serve alla prima configurazione, che deve creare
    /// studio e primo utente nello stesso commit (altrimenti un errore lascia uno studio senza utenti).
    /// </summary>
    internal static async Task<Guid> CreateUserInTransactionAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid practiceId,
        string username, string displayName, string password, Guid? createdBy, CancellationToken ct)
    {
        var problems = PasswordPolicy.Check(password, username);
        if (problems.Count > 0) throw new ArgumentException("password non accettabile: " + string.Join(", ", problems), nameof(password));
        var id = (Guid)(await Scalar(conn, tx, """
            INSERT INTO ps.users (practice_id, username, display_name, password_verifier, created_by) VALUES ($1,$2,$3,$4,$5) RETURNING id
            """, ct, practiceId, username, displayName, PasswordHasher.Hash(password), (object?)createdBy ?? DBNull.Value))!;
        await Audit(conn, tx, id, "user.create", "user", id, ct);
        return id;
    }

    public async Task<LoginResult> LoginAsync(Guid practiceId, string username, string password, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        var at = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        Guid id = default; string? verifier = null, display = null; bool enabled = false; DateTimeOffset? lockedUntil = null; bool found = false;
        await using (var cmd = new NpgsqlCommand("""
            SELECT id, password_verifier, display_name, enabled, locked_until FROM ps.users
            WHERE practice_id = $1 AND username_norm = lower(btrim($2)) FOR UPDATE
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(practiceId); cmd.Parameters.AddWithValue(username);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                found = true; id = r.GetGuid(0); verifier = r.GetString(1); display = r.GetString(2); enabled = r.GetBoolean(3);
                lockedUntil = r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4);
            }
        }

        if (!found)
        {
            PasswordHasher.Verify(password, PasswordHasher.Dummy);   // stesso costo temporale
            await AuditAnonymous(conn, tx, "login", "failure", ct);
            await tx.CommitAsync(ct);
            return new LoginResult(LoginOutcome.InvalidCredentials, null, null, null);
        }
        if (!enabled)
        {
            PasswordHasher.Verify(password, verifier!);
            await AuditAs(conn, tx, id, "login", "user", id, ct, "denied");
            await tx.CommitAsync(ct);
            return new LoginResult(LoginOutcome.Disabled, null, null, null);
        }
        if (lockedUntil is { } until && until > at)
        {
            // durante il blocco non si valuta nemmeno la password: nessun aiuto a chi indovina
            await AuditAs(conn, tx, id, "login", "user", id, ct, "denied");
            await tx.CommitAsync(ct);
            return new LoginResult(LoginOutcome.Locked, null, null, until);
        }

        if (!PasswordHasher.Verify(password, verifier!))
        {
            var failedNow = (int)(await Scalar(conn, tx, """
                UPDATE ps.users SET failed_attempts = failed_attempts + 1,
                    locked_until = CASE WHEN failed_attempts + 1 >= $2 THEN $3 ELSE locked_until END
                WHERE id = $1 RETURNING failed_attempts
                """, ct, id, MaxFailedAttempts, at + LockDuration))!;
            await AuditAs(conn, tx, id, "login", "user", id, ct, "failure");
            await tx.CommitAsync(ct);
            return failedNow >= MaxFailedAttempts
                ? new LoginResult(LoginOutcome.Locked, null, null, at + LockDuration)
                : new LoginResult(LoginOutcome.InvalidCredentials, null, null, null);
        }

        // successo: azzera i contatori; se il verificatore è vecchio lo aggiorna con i parametri correnti
        if (PasswordHasher.NeedsRehash(verifier!))
            await Exec(conn, tx, "UPDATE ps.users SET password_verifier = $2 WHERE id = $1", ct, id, PasswordHasher.Hash(password));
        await Exec(conn, tx, "UPDATE ps.users SET failed_attempts = 0, locked_until = NULL WHERE id = $1", ct, id);
        await AuditAs(conn, tx, id, "login", "user", id, ct, "success");
        await tx.CommitAsync(ct);
        return new LoginResult(LoginOutcome.Success, id, display, null);
    }

    /// <summary>Cambio password: richiede la password attuale; aggiorna credentials_changed_at (invalida le sessioni emesse prima).</summary>
    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var row = await Scalar(conn, tx, "SELECT password_verifier || '|' || username FROM ps.users WHERE id = $1 AND enabled FOR UPDATE", ct, userId) as string;
        if (row is null) return false;
        var sep = row.LastIndexOf('|');
        var (verifier, username) = (row[..sep], row[(sep + 1)..]);
        if (!PasswordHasher.Verify(currentPassword, verifier))
        {
            await AuditAs(conn, tx, userId, "password.change", "user", userId, ct, "failure");
            await tx.CommitAsync(ct);
            return false;
        }
        var problems = PasswordPolicy.Check(newPassword, username);
        if (problems.Count > 0) throw new ArgumentException("password non accettabile: " + string.Join(", ", problems), nameof(newPassword));
        await Exec(conn, tx, "UPDATE ps.users SET password_verifier = $2, credentials_changed_at = now(), failed_attempts = 0, locked_until = NULL WHERE id = $1", ct, userId, PasswordHasher.Hash(newPassword));
        await AuditAs(conn, tx, userId, "password.change", "user", userId, ct, "success");
        await tx.CommitAsync(ct);
        return true;
    }

    private static Task AuditAs(NpgsqlConnection c, NpgsqlTransaction tx, Guid actor, string action, string targetType, Guid target, CancellationToken ct, string outcome)
        => Exec(c, tx, "INSERT INTO ps.audit_events (actor_id, action_code, outcome_code, correlation_id, target_type, target_id) VALUES ($1,$2,$3,gen_random_uuid(),$4,$5)", ct, actor, action, outcome, targetType, target);

    private static Task AuditAnonymous(NpgsqlConnection c, NpgsqlTransaction tx, string action, string outcome, CancellationToken ct)
        => Exec(c, tx, "INSERT INTO ps.audit_events (action_code, outcome_code, correlation_id) VALUES ($1,$2,gen_random_uuid())", ct, action, outcome);
}
