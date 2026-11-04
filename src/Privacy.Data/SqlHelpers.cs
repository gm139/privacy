using Npgsql;

namespace Privacy.Data;

/// <summary>Utilità condivise dai servizi: comandi parametrici, idempotenza, audit.</summary>
internal static class SqlHelpers
{
    /// <summary>Registra il comando; se già eseguito restituisce il risultato precedente (Guid.Empty se senza risultato).</summary>
    public static async Task<Guid?> ClaimCommandAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid commandId, string type, CancellationToken ct)
    {
        var n = await Exec(c, tx, "INSERT INTO ps.processed_commands (command_id, command_type) VALUES ($1,$2) ON CONFLICT DO NOTHING", ct, commandId, type);
        if (n == 1) return null;
        var prev = await Scalar(c, tx, "SELECT result_id FROM ps.processed_commands WHERE command_id = $1", ct, commandId);
        return prev is Guid g ? g : Guid.Empty;
    }

    public static Task SetResult(NpgsqlConnection c, NpgsqlTransaction tx, Guid commandId, Guid result, CancellationToken ct)
        => Exec(c, tx, "UPDATE ps.processed_commands SET result_id = $2 WHERE command_id = $1", ct, commandId, result);

    public static Task Audit(NpgsqlConnection c, NpgsqlTransaction tx, Guid actor, string action, string targetType, Guid target, CancellationToken ct)
        => Exec(c, tx, "INSERT INTO ps.audit_events (actor_id, action_code, outcome_code, correlation_id, target_type, target_id) VALUES ($1,$2,'success',gen_random_uuid(),$3,$4)", ct, actor, action, targetType, target);

    public static async Task<int> Exec(NpgsqlConnection c, NpgsqlTransaction tx, string sql, CancellationToken ct, params object[] p)
    {
        await using var cmd = Build(c, tx, sql, p);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public static async Task<object?> Scalar(NpgsqlConnection c, NpgsqlTransaction tx, string sql, CancellationToken ct, params object[] p)
    {
        await using var cmd = Build(c, tx, sql, p);
        return await cmd.ExecuteScalarAsync(ct);
    }

    public static NpgsqlCommand Build(NpgsqlConnection c, NpgsqlTransaction tx, string sql, object[] p)
    {
        var cmd = new NpgsqlCommand(sql, c, tx);
        foreach (var v in p) cmd.Parameters.Add(new NpgsqlParameter { Value = v is DateTimeOffset d ? d.ToUniversalTime() : v });   // Npgsql accetta solo offset 0
        return cmd;
    }
}
