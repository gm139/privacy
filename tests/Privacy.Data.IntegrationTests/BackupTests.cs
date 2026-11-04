using System.Diagnostics;
using Npgsql;
using Privacy.Data;
using Xunit;

namespace Privacy.Data.IntegrationTests;

public class BackupCryptoTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    [Fact] public void RoundTrip() => Assert.Equal(new byte[] { 1, 2, 3 }, BackupCrypto.Decrypt(Key, "k1", BackupCrypto.Encrypt(Key, "k1", new byte[] { 1, 2, 3 })));

    [Fact] public void WrongKey_AlteredFile_AlteredKeyVersion_AreRejected()
    {
        var file = BackupCrypto.Encrypt(Key, "k1", new byte[] { 1, 2, 3, 4 });
        Assert.Throws<BackupFormatException>(() => BackupCrypto.Decrypt(new byte[32], "k1", file));
        Assert.Throws<BackupFormatException>(() => BackupCrypto.Decrypt(Key, "k2", file));
        var tampered = (byte[])file.Clone(); tampered[^1] ^= 1;
        Assert.Throws<BackupFormatException>(() => BackupCrypto.Decrypt(Key, "k1", tampered));
        Assert.Throws<BackupFormatException>(() => BackupCrypto.Decrypt(Key, "k1", new byte[] { 1, 2, 3 }));
    }

    [Fact] public void SamePlaintext_GivesDifferentCiphertext()
        => Assert.NotEqual(BackupCrypto.Encrypt(Key, "k1", new byte[16]), BackupCrypto.Encrypt(Key, "k1", new byte[16]));
}

/// <summary>
/// T24/T25 a livello di database: backup cifrato → ripristino in un database pulito → riapplicazione delle cancellazioni.
/// Richiede PRIVACY_TEST_CONN e il contenitore Docker «privacy-test» (db/start_test_db.sh).
/// </summary>
public class BackupRestoreTests
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("PRIVACY_TEST_CONN");
    private static readonly byte[] Key = Enumerable.Range(10, 32).Select(i => (byte)i).ToArray();
    private const string Container = "privacy-test";

    private static PgProcessTools Tools(string dbToDump = "privacy", string restoreInto = "privacy_restore") => new(
        "docker", new[] { "exec", "-i", Container }, new[] { "-U", "postgres", "-Fc", dbToDump }, new[] { "-U", "postgres", "-d", restoreInto });

    private static string RepoFile(string relative)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, relative))) dir = Path.GetDirectoryName(dir);
        return dir is null ? throw new FileNotFoundException(relative) : Path.Combine(dir, relative);
    }

    private static string Docker(string stdin, params string[] args)
    {
        var psi = new ProcessStartInfo("docker") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.StandardInput.Write(stdin); p.StandardInput.Close();
        var o = p.StandardOutput.ReadToEnd(); var e = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException(e);
        return o;
    }

    private static async Task<(Db db, Guid practice, Guid user)> SetupAsync()
    {
        var db = new Db(Conn!);
        await using var c = await db.OpenAsync();
        await using var p = new NpgsqlCommand("INSERT INTO ps.practices (name, controller_name, address, privacy_contact) VALUES ('Studio','Dott.','Via 1','p@example.test') RETURNING id", c);
        var practice = (Guid)(await p.ExecuteScalarAsync())!;
        await using var u = new NpgsqlCommand("INSERT INTO ps.users (practice_id, username, display_name, password_verifier) VALUES ($1,'medico','Medico','argon2id$1$x$y$z') RETURNING id", c);
        u.Parameters.AddWithValue(practice);
        return (db, practice, (Guid)(await u.ExecuteScalarAsync())!);
    }

    private static async Task<long> Count(Db db, string sql, params object[] p)
    {
        await using var c = await db.OpenAsync();
        await using var q = new NpgsqlCommand(sql, c);
        foreach (var v in p) q.Parameters.AddWithValue(v);
        return (long)(await q.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Backup_Restore_ThenDeletionsAreReapplied_NothingComesBack()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var root = Path.Combine(Path.GetTempPath(), "privacy-bk-" + Guid.NewGuid().ToString("N"));
        var ledgerPath = Path.Combine(root, "ledger", "disposals.jsonl");
        var requests = new RightsRequestService(db);

        // dato da cancellare: richiesta chiusa, esistente al momento del backup
        var id = await requests.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, user, null, "Mario (collab.)", "other", "email",
            DateTimeOffset.UtcNow, "Copia dati", new[] { "access" }));
        await requests.DecideAsync(Guid.NewGuid(), id, user, "access", "granted", null, DateTimeOffset.UtcNow);
        await requests.CloseAsync(Guid.NewGuid(), id, user);

        // 1. backup cifrato, con copia esterna, verificato
        var backup = new BackupService(db, Tools(), Path.Combine(root, "primary"), Path.Combine(root, "external"), ledgerPath);
        Directory.CreateDirectory(Path.GetDirectoryName(ledgerPath)!); File.WriteAllText(ledgerPath, "");
        var result = await backup.BackupAsync(Key, "k1");
        Assert.True(File.Exists(result.PrimaryPath) && File.Exists(result.ExternalPath));
        Assert.True(File.Exists(result.ExternalPath + ".ledger.jsonl"));   // il registro delle eliminazioni viaggia con il backup
        Assert.Equal(1, await Count(db, "SELECT count(*) FROM ps.backup_runs WHERE id = $1 AND status_code = 'ok' AND verified_at IS NOT NULL AND artifact_reference = $2", result.RunId, result.FileName));
        Assert.DoesNotContain("Mario", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(result.PrimaryPath)));   // il file non contiene dati in chiaro
        Assert.Throws<BackupFormatException>(() => BackupService.VerifyFile(result.PrimaryPath, new byte[32], "k1"));

        // 2. DOPO il backup il titolare elimina la richiesta (registro esterno aggiornato)
        var disposal = new DisposalService(db, new DisposalLedger(ledgerPath));
        Assert.True(await disposal.DisposeAsync(Guid.NewGuid(), "rights_request", id, user, "Durata approvata trascorsa"));
        Assert.Equal(0, await Count(db, "SELECT count(*) FROM ps.rights_requests WHERE id = $1", id));

        // 3. ripristino su database pulito partendo dalla sola copia esterna e dalla chiave
        var started = DateTimeOffset.UtcNow;
        Docker("", "exec", Container, "psql", "-U", "postgres", "-qc", "DROP DATABASE IF EXISTS privacy_restore WITH (FORCE)");
        Docker("", "exec", Container, "psql", "-U", "postgres", "-qc", "CREATE DATABASE privacy_restore");
        Docker(File.ReadAllText(RepoFile("db/000_bootstrap_roles.sql")), "exec", "-i", Container, "psql", "-U", "postgres", "-d", "privacy_restore", "-q", "-v", "ON_ERROR_STOP=1");
        try
        {
            await backup.RestoreAsync(result.ExternalPath!, Key, "k1");
            var restored = new Db(Conn.Replace("Database=privacy", "Database=privacy_restore"));

            // i dati cancellati sono tornati col backup...
            Assert.Equal(1, await Count(restored, "SELECT count(*) FROM ps.rights_requests WHERE id = $1", id));
            // 4. ...e la procedura di ripristino li rielimina dal registro esterno (T25)
            var outcome = await new DisposalService(restored, new DisposalLedger(ledgerPath)).ReapplyAfterRestoreAsync(user);
            Assert.Empty(outcome.Failed);
            Assert.True(outcome.Applied >= 1);
            Assert.Equal(0, await Count(restored, "SELECT count(*) FROM ps.rights_requests WHERE id = $1", id));
            // idempotente: una seconda riapplicazione non trova più nulla da fare
            Assert.Equal(0, (await new DisposalService(restored, new DisposalLedger(ledgerPath)).ReapplyAfterRestoreAsync(user)).Applied);

            // 5. esito della prova di ripristino registrato
            await BackupService.RecordRestoreTestAsync(restored, result.RunId, user, started, "ok", "Ripristino su database pulito riuscito; cancellazioni riapplicate");
            Assert.Equal(1, await Count(restored, "SELECT count(*) FROM ps.restore_tests WHERE backup_id = $1 AND outcome_code = 'ok'", result.RunId));
        }
        finally
        {
            Docker("", "exec", Container, "psql", "-U", "postgres", "-qc", "DROP DATABASE IF EXISTS privacy_restore WITH (FORCE)");
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Backup_WithWrongTool_IsRecordedAsFailed_AndNoFileRemains()
    {
        if (Conn is null) return;
        var (db, _, _) = await SetupAsync();
        var root = Path.Combine(Path.GetTempPath(), "privacy-bk-" + Guid.NewGuid().ToString("N"));
        var broken = new PgProcessTools("docker", new[] { "exec", "-i", Container }, new[] { "-U", "postgres", "-Fc", "database_inesistente" }, Array.Empty<string>());
        var svc = new BackupService(db, broken, Path.Combine(root, "primary"), null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.BackupAsync(Key, "k1"));
        Assert.True(await Count(db, "SELECT count(*) FROM ps.backup_runs WHERE status_code = 'failed' AND error_code IS NOT NULL") >= 1);
        Assert.False(Directory.Exists(Path.Combine(root, "primary")) && Directory.GetFiles(Path.Combine(root, "primary")).Length > 0);
    }

    private static string AdminConn => Environment.GetEnvironmentVariable("PRIVACY_TEST_ADMIN_CONN") ?? "Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Password=x";

    [Fact]
    public async Task RestoreService_RestoresIntoNewDatabase_ReappliesDisposals_RecordsTest_AndNeverOverwrites()
    {
        if (Conn is null) return;
        var (db, practice, user) = await SetupAsync();
        var root = Path.Combine(Path.GetTempPath(), "privacy-rs-" + Guid.NewGuid().ToString("N"));
        var ledgerPath = Path.Combine(root, "ledger", "disposals.jsonl");
        var requests = new RightsRequestService(db);
        var id = await requests.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, user, null, "Carla (collab.)", "other", "email", DateTimeOffset.UtcNow, "Copia", new[] { "access" }));
        await requests.DecideAsync(Guid.NewGuid(), id, user, "access", "granted", null, DateTimeOffset.UtcNow);
        await requests.CloseAsync(Guid.NewGuid(), id, user);

        Directory.CreateDirectory(Path.GetDirectoryName(ledgerPath)!); File.WriteAllText(ledgerPath, "");
        var backup = new BackupService(db, Tools(), Path.Combine(root, "primary"), Path.Combine(root, "external"), ledgerPath);
        var result = await backup.BackupAsync(Key, "k1");
        Assert.True(await new DisposalService(db, new DisposalLedger(ledgerPath)).DisposeAsync(Guid.NewGuid(), "rights_request", id, user, "Durata trascorsa"));
        // copia aggiornata del registro (come farebbe un backup successivo)
        File.Copy(ledgerPath, result.ExternalPath + ".ledger.jsonl", overwrite: true);

        var target = "privacy_rs_" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var report = await RestoreService.RestoreAsync(new RestoreRequest(result.ExternalPath!, Key, "k1", AdminConn, target,
                result.ExternalPath + ".ledger.jsonl", Tools(restoreInto: target)));
            Assert.True(report.Clean, string.Join("; ", report.DisposalsToCheck) + " audit=" + report.AuditChainBrokenAtId);
            Assert.True(report.DisposalsReapplied >= 1);
            Assert.True(report.RestoreTestRecorded);

            var restored = new Db(new NpgsqlConnectionStringBuilder(AdminConn) { Database = target }.ConnectionString);
            Assert.Equal(0, await Count(restored, "SELECT count(*) FROM ps.rights_requests WHERE id = $1", id));          // T25
            Assert.Equal(1, await Count(restored, "SELECT count(*) FROM ps.restore_tests WHERE outcome_code = 'ok'"));      // T24
            Assert.Equal(1, await Count(restored, "SELECT count(*) FROM ps.disposal_items WHERE item_id = $1", id));

            // non sovrascrive mai un database esistente
            await Assert.ThrowsAsync<InvalidOperationException>(() => RestoreService.RestoreAsync(new RestoreRequest(result.ExternalPath!, Key, "k1", AdminConn, target, null, Tools(restoreInto: target))));
            // chiave errata: rifiutato prima di creare qualunque database
            var other = "privacy_rs_" + Guid.NewGuid().ToString("N")[..8];
            await Assert.ThrowsAsync<BackupFormatException>(() => RestoreService.RestoreAsync(new RestoreRequest(result.ExternalPath!, new byte[32], "k1", AdminConn, other, null, Tools(restoreInto: other))));
            Assert.Equal(0, await Count(new Db(AdminConn), "SELECT count(*) FROM pg_database WHERE datname = $1", other));
            // nome non valido (iniezione): rifiutato
            await Assert.ThrowsAsync<ArgumentException>(() => RestoreService.RestoreAsync(new RestoreRequest(result.ExternalPath!, Key, "k1", AdminConn, "x\"; DROP DATABASE privacy; --", null, Tools())));
        }
        finally
        {
            Docker("", "exec", Container, "psql", "-U", "postgres", "-qc", $"DROP DATABASE IF EXISTS {target} WITH (FORCE)");
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RestoreService_WithoutLedger_IsNotReportedAsClean()
    {
        if (Conn is null) return;
        var (db, _, _) = await SetupAsync();
        var root = Path.Combine(Path.GetTempPath(), "privacy-rs-" + Guid.NewGuid().ToString("N"));
        var result = await new BackupService(db, Tools(), Path.Combine(root, "p"), null).BackupAsync(Key, "k1");
        var target = "privacy_rs_" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var report = await RestoreService.RestoreAsync(new RestoreRequest(result.PrimaryPath, Key, "k1", AdminConn, target, null, Tools(restoreInto: target)));
            Assert.False(report.Clean);                 // senza registro esterno non si può dire che nulla sia tornato
            Assert.False(report.LedgerProvided);
        }
        finally
        {
            Docker("", "exec", Container, "psql", "-U", "postgres", "-qc", $"DROP DATABASE IF EXISTS {target} WITH (FORCE)");
            Directory.Delete(root, true);
        }
    }
}
