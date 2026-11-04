using System.Diagnostics;

namespace Privacy.Data;

/// <summary>Strumenti PostgreSQL per backup e ripristino (pg_dump / pg_restore).</summary>
public interface IPgTools
{
    /// <summary>Copia logica consistente del database operativo (formato personalizzato di pg_dump).</summary>
    Task<byte[]> DumpAsync(CancellationToken ct = default);
    /// <summary>Ripristina la copia nel database di destinazione, già creato e vuoto.</summary>
    Task RestoreAsync(byte[] dump, CancellationToken ct = default);
}

/// <summary>
/// Esegue pg_dump / pg_restore come processi. <paramref name="Prefix"/> permette di anteporre un wrapper
/// (es. «docker exec -i contenitore» nei test). La password, se serve, passa da variabile d'ambiente del processo figlio,
/// mai dalla riga di comando.
/// </summary>
public sealed record PgProcessTools(string Executable, IReadOnlyList<string> Prefix, IReadOnlyList<string> DumpArgs,
    IReadOnlyList<string> RestoreArgs, IReadOnlyDictionary<string, string>? Environment = null, bool Wrapper = true) : IPgTools
{
    /// <summary>
    /// Strumenti installati sulla macchina: <paramref name="binDir"/> è la cartella con pg_dump e pg_restore.
    /// Host, porta, database e utente vengono dalla stringa di connessione; la password passa da PGPASSWORD (non dalla riga di comando).
    /// </summary>
    public static PgProcessTools Native(string binDir, string connectionString, string? restoreDatabase = null)
    {
        var b = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        var common = new List<string> { "-h", b.Host ?? "127.0.0.1", "-p", b.Port.ToString(), "-U", b.Username ?? "privacy_app", "-w" };
        var env = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(b.Password)) env["PGPASSWORD"] = b.Password;
        var dump = common.Concat(new[] { "-Fc", b.Database ?? "privacy" }).ToList();
        var restore = common.Concat(new[] { "-d", restoreDatabase ?? "privacy_restore" }).ToList();
        return new PgProcessTools(binDir, Array.Empty<string>(), dump, restore, env, Wrapper: false);
    }

    public Task<byte[]> DumpAsync(CancellationToken ct = default) => RunAsync("pg_dump", DumpArgs, null, ct);

    public async Task RestoreAsync(byte[] dump, CancellationToken ct = default) => await RunAsync("pg_restore", RestoreArgs, dump, ct);

    private async Task<byte[]> RunAsync(string tool, IReadOnlyList<string> args, byte[]? stdin, CancellationToken ct)
    {
        var fileName = Wrapper ? Executable : Path.Combine(Executable, OperatingSystem.IsWindows() ? tool + ".exe" : tool);
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardInput = stdin is not null, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        foreach (var a in Prefix) psi.ArgumentList.Add(a);
        if (Wrapper) psi.ArgumentList.Add(tool);
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (Environment is not null) foreach (var kv in Environment) psi.Environment[kv.Key] = kv.Value;

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"{tool} non avviabile");
        var outTask = ReadAllAsync(p.StandardOutput.BaseStream, ct);
        var errTask = p.StandardError.ReadToEndAsync(ct);
        if (stdin is not null)
        {
            await p.StandardInput.BaseStream.WriteAsync(stdin, ct);
            p.StandardInput.Close();
        }
        var output = await outTask;
        var err = await errTask;
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
            // l'errore di pg_* può citare dati: se ne conserva solo l'inizio e non si registra nei log applicativi
            throw new InvalidOperationException($"{tool} terminato con codice {p.ExitCode}: {err[..Math.Min(err.Length, 300)]}");
        return output;
    }

    private static async Task<byte[]> ReadAllAsync(Stream s, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
