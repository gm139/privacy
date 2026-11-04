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
    IReadOnlyList<string> RestoreArgs, IReadOnlyDictionary<string, string>? Environment = null) : IPgTools
{
    public Task<byte[]> DumpAsync(CancellationToken ct = default) => RunAsync("pg_dump", DumpArgs, null, ct);

    public async Task RestoreAsync(byte[] dump, CancellationToken ct = default) => await RunAsync("pg_restore", RestoreArgs, dump, ct);

    private async Task<byte[]> RunAsync(string tool, IReadOnlyList<string> args, byte[]? stdin, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(Executable)
        {
            RedirectStandardInput = stdin is not null, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        foreach (var a in Prefix) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add(tool);
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
