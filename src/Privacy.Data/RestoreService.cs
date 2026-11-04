using System.Reflection;
using Npgsql;

namespace Privacy.Data;

public sealed record RestoreRequest(
    string BackupFile, byte[] Key, string KeyVersion,
    string AdminConnectionString,      // utente con CREATE DATABASE e CREATEROLE, database di manutenzione (es. postgres)
    string TargetDatabase,             // database NUOVO: se esiste, il ripristino si rifiuta
    string? LedgerFile,                // registro esterno delle eliminazioni (copia accanto al backup o più recente)
    IPgTools Tools);                   // strumenti già configurati per ripristinare in TargetDatabase

public sealed record RestoreReport(
    string TargetDatabase, int DisposalsReapplied, int DisposalsAlreadyAbsent,
    IReadOnlyList<string> DisposalsToCheck, long? AuditChainBrokenAtId, bool RestoreTestRecorded, bool LedgerProvided)
{
    /// <summary>Esito pieno: catena di audit integra, nessuna eliminazione da verificare a mano e registro esterno fornito.</summary>
    public bool Clean => AuditChainBrokenAtId is null && DisposalsToCheck.Count == 0 && LedgerProvided;
}

/// <summary>
/// Ripristino su ambiente pulito (T24/T25): decifra, crea un database NUOVO con i ruoli dello schema, ripristina,
/// riapplica le eliminazioni dal registro esterno, controlla la catena di audit e registra la prova di ripristino.
/// Non sovrascrive mai un database esistente. Le password dei ruoli dello schema vanno impostate dall'amministratore.
/// </summary>
public static class RestoreService
{
    public static string BootstrapSql()
    {
        using var s = typeof(RestoreService).Assembly.GetManifestResourceStream("000_bootstrap_roles.sql")
                      ?? throw new InvalidOperationException("script dei ruoli non incluso");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    public static async Task<RestoreReport> RestoreAsync(RestoreRequest req, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        if (!System.Text.RegularExpressions.Regex.IsMatch(req.TargetDatabase, "^[a-z][a-z0-9_]{0,62}$"))
            throw new ArgumentException("nome del database non valido (minuscole, cifre, trattino basso)", nameof(req));

        // 1. il file deve decifrarsi e essere una copia valida PRIMA di toccare qualunque database
        var dump = BackupService.VerifyFile(req.BackupFile, req.Key, req.KeyVersion);

        // 2. database nuovo (mai esistente) e ruoli dello schema
        var adminMaintenance = new NpgsqlConnectionStringBuilder(req.AdminConnectionString) { Pooling = false };
        await using (var c = new NpgsqlConnection(adminMaintenance.ConnectionString))
        {
            await c.OpenAsync(ct);
            await using (var chk = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = $1", c))
            {
                chk.Parameters.AddWithValue(req.TargetDatabase);
                if (await chk.ExecuteScalarAsync(ct) is not null)
                    throw new InvalidOperationException($"il database «{req.TargetDatabase}» esiste già: il ripristino non sovrascrive");
            }
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{req.TargetDatabase}\"", c);
            await create.ExecuteNonQueryAsync(ct);
        }
        var adminTarget = new NpgsqlConnectionStringBuilder(req.AdminConnectionString) { Database = req.TargetDatabase, Pooling = false };
        await using (var c = new NpgsqlConnection(adminTarget.ConnectionString))
        {
            await c.OpenAsync(ct);
            await using var boot = new NpgsqlCommand(BootstrapSql(), c);
            await boot.ExecuteNonQueryAsync(ct);
        }

        // 3. ripristino dei dati
        await req.Tools.RestoreAsync(dump, ct);

        // 4. riapplicazione delle eliminazioni dal registro esterno
        var targetDb = new Db(adminTarget.ConnectionString);
        Guid? actor;
        await using (var c = await targetDb.OpenAsync(ct))
        {
            await using var q = new NpgsqlCommand("SELECT id FROM ps.users WHERE enabled ORDER BY created_at LIMIT 1", c);
            actor = await q.ExecuteScalarAsync(ct) as Guid?;
        }
        int applied = 0, absent = 0; var toCheck = new List<string>();
        var ledgerProvided = req.LedgerFile is not null && File.Exists(req.LedgerFile);
        if (ledgerProvided)
        {
            if (actor is null) toCheck.Add("nessun utente abilitato nella copia: impossibile registrare le eliminazioni");
            else
            {
                var r = await new DisposalService(targetDb, new DisposalLedger(req.LedgerFile!)).ReapplyAfterRestoreAsync(actor.Value, ct);
                applied = r.Applied; absent = r.AlreadyAbsent;
                toCheck.AddRange(r.Failed.Select(f => $"{f.Entry.ItemType} {f.Entry.ItemId}: {f.Error}"));
            }
        }

        // 5. integrità della catena di audit e registrazione della prova
        long? broken;
        await using (var c = await targetDb.OpenAsync(ct))
        {
            await using var q = new NpgsqlCommand("SELECT ps.verify_audit_chain()", c);
            var v = await q.ExecuteScalarAsync(ct);
            broken = v is long l ? l : null;
        }
        var recorded = false;
        if (actor is { } performer)
        {
            // il nome del file contiene l'id dell'esecuzione di backup; nella copia quella riga esiste (era «in corso» al momento del dump)
            var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(req.BackupFile), @"-([0-9a-f]{32})\.psbk$");
            await using var c = await targetDb.OpenAsync(ct);
            await using var find = new NpgsqlCommand("SELECT id FROM ps.backup_runs WHERE id = $1", c);
            find.Parameters.AddWithValue(m.Success ? Guid.ParseExact(m.Groups[1].Value, "N") : Guid.Empty);
            if (await find.ExecuteScalarAsync(ct) is Guid runId)
            {
                var outcome = broken is null && toCheck.Count == 0 && ledgerProvided ? "ok" : "partial";
                var report = $"Ripristino in «{req.TargetDatabase}»: eliminazioni riapplicate {applied}, già assenti {absent}, da verificare {toCheck.Count}; " +
                             $"catena di audit {(broken is null ? "integra" : "interrotta all'id " + broken)}; registro esterno {(ledgerProvided ? "fornito" : "NON fornito")}.";
                await BackupService.RecordRestoreTestAsync(targetDb, runId, performer, started, outcome, report, ct);
                recorded = true;
            }
        }
        return new RestoreReport(req.TargetDatabase, applied, absent, toCheck, broken, recorded, ledgerProvided);
    }
}
