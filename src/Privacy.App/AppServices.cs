using System.IO;
using Privacy.Data;

namespace Privacy.App;

/// <summary>Composizione dei servizi. La stringa di connessione viene dall'ambiente (PRIVACY_CONN) o da %LOCALAPPDATA%\PrivacyStudio\connection.txt.</summary>
public sealed class AppServices
{
    public Db Db { get; }
    public AuthService Auth { get; }
    public RightsRequestService Requests { get; }
    public IncidentService Incidents { get; }
    public SubjectService Subjects { get; }
    public NoticeService Notices { get; }
    public RegistryService Registry { get; }
    public CatalogService Catalog { get; }
    public RetentionService Retention { get; }
    public DisposalService Disposal { get; }
    public BackupService Backup { get; }
    public AppConfig Config { get; }
    /// <summary>Vero se gli strumenti di PostgreSQL (pg_dump) sono configurati: senza, il backup non può partire.</summary>
    public bool CanBackup => Config.PgBinDir is not null;
    private byte[]? _hmacKey;
    /// <summary>Chiave dei moduli, letta (o creata) al primo uso.</summary>
    public byte[] HmacKey => _hmacKey ??= KeyStore.GetOrCreateHmacKey();

    public AppServices(string connectionString, IPgTools? pgTools = null, AppConfig? config = null)
    {
        Config = config ?? AppConfig.Load();
        Db = new Db(connectionString);
        Auth = new AuthService(Db);
        Requests = new RightsRequestService(Db);
        Incidents = new IncidentService(Db);
        Subjects = new SubjectService(Db);
        Notices = new NoticeService(Db);
        Registry = new RegistryService(Db);
        Catalog = new CatalogService(Db);
        Retention = new RetentionService(Db);
        Disposal = new DisposalService(Db, new DisposalLedger(Config.LedgerPath));
        IPgTools tools = pgTools ?? (Config.PgBinDir is { } bin ? PgProcessTools.Native(bin, connectionString) : new UnconfiguredPgTools());
        Backup = new BackupService(Db, tools, Config.BackupDir, Config.ExternalBackupDir, Config.LedgerPath);
    }

    public static string? ReadConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("PRIVACY_CONN");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrivacyStudio", "connection.txt");
        return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
    }

    /// <summary>Studio del titolare (in v1 ce n'è uno solo).</summary>
    public async Task<Guid?> GetPracticeIdAsync()
    {
        await using var c = await Db.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand("SELECT id FROM ps.practices ORDER BY created_at LIMIT 1", c);
        return await cmd.ExecuteScalarAsync() as Guid?;
    }
}

/// <summary>Segnaposto quando pg_dump non è configurato: ogni uso dà un errore chiaro invece di un guasto oscuro.</summary>
internal sealed class UnconfiguredPgTools : IPgTools
{
    public Task<byte[]> DumpAsync(CancellationToken ct = default) => throw new InvalidOperationException("strumenti PostgreSQL non configurati (pgBinDir)");
    public Task RestoreAsync(byte[] dump, CancellationToken ct = default) => throw new InvalidOperationException("strumenti PostgreSQL non configurati (pgBinDir)");
}
