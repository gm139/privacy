using System.IO;
using System.Text.Json;

namespace Privacy.App;

/// <summary>
/// Impostazioni locali (%LOCALAPPDATA%\PrivacyStudio\config.json). Nessun segreto qui: la password del database sta nella
/// stringa di connessione, le chiavi in KeyStore. Esempio:
/// { "ledgerPath": "D:\\Privacy\\disposals.jsonl", "backupDir": "D:\\Backup", "externalBackupDir": "E:\\Copia", "pgBinDir": "C:\\Program Files\\PostgreSQL\\17\\bin" }
/// </summary>
public sealed record AppConfig(string LedgerPath, string BackupDir, string? ExternalBackupDir, string? PgBinDir)
{
    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrivacyStudio");

    public static AppConfig Load()
    {
        var defaults = new AppConfig(Path.Combine(Dir, "ledger", "disposals.jsonl"), Path.Combine(Dir, "backups"), null, null);
        var path = Path.Combine(Dir, "config.json");
        if (!File.Exists(path)) return defaults;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        string? Get(string name) => doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString() : null;
        return new AppConfig(Get("ledgerPath") ?? defaults.LedgerPath, Get("backupDir") ?? defaults.BackupDir, Get("externalBackupDir"), Get("pgBinDir"));
    }
}
