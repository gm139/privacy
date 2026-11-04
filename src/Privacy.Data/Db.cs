using Npgsql;

namespace Privacy.Data;

/// <summary>Punto unico di creazione delle connessioni (ruolo operativo privacy_app).</summary>
public sealed class Db
{
    private readonly NpgsqlDataSource _source;
    public Db(string connectionString) => _source = NpgsqlDataSource.Create(connectionString);

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = await _source.OpenConnectionAsync(ct);
        return conn;
    }

    /// <summary>Calcola la scadenza con la funzione SQL ps.compute_due_date (fonte di verità per il database).</summary>
    public async Task<DateOnly> ComputeDueDateAsync(DateOnly received, int months, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT ps.compute_due_date($1, $2)", conn);
        cmd.Parameters.AddWithValue(received);
        cmd.Parameters.AddWithValue(months);
        return DateOnly.FromDateTime((DateTime)(await cmd.ExecuteScalarAsync(ct))!);
    }
}
