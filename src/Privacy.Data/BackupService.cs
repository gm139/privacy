using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using static Privacy.Data.SqlHelpers;

namespace Privacy.Data;

public sealed record BackupResult(Guid RunId, string FileName, string PrimaryPath, string? ExternalPath, long SizeBytes);

/// <summary>
/// Flusso F06: backup cifrato con esito verificabile e copia fuori dal dispositivo primario, ripristino su ambiente pulito.
/// La chiave è un parametro (decisione D-ENC aperta); non finisce mai su disco in chiaro né nel database.
/// </summary>
public sealed class BackupService
{
    private readonly Db _db;
    private readonly IPgTools _tools;
    private readonly string _primaryDir;
    private readonly string? _externalDir;

    public BackupService(Db db, IPgTools tools, string primaryDir, string? externalDir)
    {
        _db = db; _tools = tools; _primaryDir = primaryDir; _externalDir = externalDir;
    }

    public async Task<BackupResult> BackupAsync(byte[] key, string keyVersion, CancellationToken ct = default)
    {
        var runId = await StartRunAsync(ct);
        try
        {
            var dump = await _tools.DumpAsync(ct);
            var encrypted = BackupCrypto.Encrypt(key, keyVersion, dump);
            var name = $"privacy-{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{runId:N}.psbk";
            var primary = Write(_primaryDir, name, encrypted, keyVersion);
            var external = _externalDir is null ? null : Write(_externalDir, name, encrypted, keyVersion);

            // verifica: si rilegge e si decifra DAL DISCO (e dalla copia esterna), non dalla memoria
            VerifyFile(primary, key, keyVersion);
            if (external is not null) VerifyFile(external, key, keyVersion);

            await FinishRunAsync(runId, "ok", name, keyVersion, verified: true, errorCode: null, ct);
            return new BackupResult(runId, name, primary, external, encrypted.Length);
        }
        catch (Exception e)
        {
            await FinishRunAsync(runId, "failed", null, keyVersion, verified: false, errorCode: e.GetType().Name, ct);
            throw;
        }
    }

    /// <summary>Decifra il file e controlla che sia una copia di pg_dump valida (intestazione «PGDMP»).</summary>
    public static byte[] VerifyFile(string path, byte[] key, string keyVersion)
    {
        var dump = BackupCrypto.Decrypt(key, keyVersion, File.ReadAllBytes(path));
        if (dump.Length < 5 || dump[0] != (byte)'P' || dump[1] != (byte)'G' || dump[2] != (byte)'D' || dump[3] != (byte)'M' || dump[4] != (byte)'P')
            throw new BackupFormatException("il contenuto non è una copia di pg_dump");
        return dump;
    }

    /// <summary>Ripristina il file cifrato nel database di destinazione (già creato, con i ruoli dello schema).</summary>
    public async Task RestoreAsync(string path, byte[] key, string keyVersion, CancellationToken ct = default)
        => await _tools.RestoreAsync(VerifyFile(path, key, keyVersion), ct);

    /// <summary>Registra l'esito di una prova di ripristino (nel database indicato, di norma quello ripristinato).</summary>
    public static async Task RecordRestoreTestAsync(Db db, Guid backupRunId, Guid performedBy, DateTimeOffset started, string outcomeCode, string report, CancellationToken ct = default)
    {
        await using var conn = await db.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await Exec(conn, tx, """
            INSERT INTO ps.restore_tests (backup_id, started_at, finished_at, outcome_code, report, performed_by, created_by)
            VALUES ($1,$2,now(),$3,$4,$5,$5)
            """, ct, backupRunId, started, outcomeCode, report, performedBy);
        await Audit(conn, tx, performedBy, "backup.restore_test", "backup_run", backupRunId, ct);
        await tx.CommitAsync(ct);
    }

    private string Write(string dir, string name, byte[] encrypted, string keyVersion)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        var tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            fs.Write(encrypted);
            fs.Flush(flushToDisk: true);
        }
        File.Move(tmp, path, overwrite: false);
        // manifest senza dati personali: serve a capire quale chiave usare e a rilevare file incompleti
        var manifest = new { name, keyVersion, sizeBytes = encrypted.Length, sha256 = Convert.ToHexString(SHA256.HashData(encrypted)), createdAt = DateTimeOffset.UtcNow };
        File.WriteAllText(path + ".json", JsonSerializer.Serialize(manifest));
        return path;
    }

    private async Task<Guid> StartRunAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO ps.backup_runs (started_at, status_code) VALUES (now(), 'running') RETURNING id", conn);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private async Task FinishRunAsync(Guid runId, string status, string? reference, string keyVersion, bool verified, string? errorCode, CancellationToken ct)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            UPDATE ps.backup_runs SET finished_at = now(), status_code = $2, artifact_reference = $3, key_version = $4,
                verified_at = CASE WHEN $5 THEN now() END, error_code = $6 WHERE id = $1
            """, conn);
        cmd.Parameters.AddWithValue(runId); cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue((object?)reference ?? DBNull.Value); cmd.Parameters.AddWithValue(keyVersion);
        cmd.Parameters.AddWithValue(verified); cmd.Parameters.AddWithValue((object?)errorCode ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
