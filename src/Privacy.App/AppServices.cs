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
    private byte[]? _hmacKey;
    /// <summary>Chiave dei moduli, letta (o creata) al primo uso.</summary>
    public byte[] HmacKey => _hmacKey ??= KeyStore.GetOrCreateHmacKey();

    public AppServices(string connectionString)
    {
        Db = new Db(connectionString);
        Auth = new AuthService(Db);
        Requests = new RightsRequestService(Db);
        Incidents = new IncidentService(Db);
        Subjects = new SubjectService(Db);
        Notices = new NoticeService(Db);
        Registry = new RegistryService(Db);
        Catalog = new CatalogService(Db);
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
