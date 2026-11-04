using System.IO;
using System.Security.Cryptography;

namespace Privacy.App;

/// <summary>
/// Chiave HMAC dei moduli. SOLUZIONE PROVVISORIA in attesa della decisione D-ENC: la chiave è generata al primo uso e
/// protetta con DPAPI (legata all'utente Windows). Conseguenza: senza lo stesso utente Windows la chiave non si recupera,
/// quindi va esportata nella procedura di recupero chiavi prima dell'uso con dati reali.
/// </summary>
public static class KeyStore
{
    public const string BackupKeyVersion = "k1";
    private static readonly byte[] Entropy = "PrivacyStudio.hmac.v1"u8.ToArray();

    public static byte[] GetOrCreateHmacKey() => GetOrCreate("hmac.key");

    /// <summary>Chiave con cui si cifrano i backup (distinta dalla chiave dei moduli).</summary>
    public static byte[] GetOrCreateBackupKey() => GetOrCreate("backup.key");

    public static bool KeysExist() => File.Exists(Path.Combine(AppConfig.Dir, "hmac.key")) || File.Exists(Path.Combine(AppConfig.Dir, "backup.key"));

    private static byte[] GetOrCreate(string file)
    {
        var path = Path.Combine(AppConfig.Dir, file);
        if (File.Exists(path)) return ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(AppConfig.Dir);
        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(path, ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser));
        return key;
    }

    /// <summary>Esporta le due chiavi cifrate con una frase segreta: da conservare FUORI dal PC, con i backup.</summary>
    public static byte[] ExportRecovery(string passphrase)
        => Privacy.Domain.KeyRecovery.Wrap(passphrase, GetOrCreateHmacKey().Concat(GetOrCreateBackupKey()).ToArray());

    /// <summary>Reinstalla le chiavi da un file di recupero (sostituisce quelle presenti).</summary>
    public static void ImportRecovery(string passphrase, byte[] file)
    {
        var secret = Privacy.Domain.KeyRecovery.Unwrap(passphrase, file);
        if (secret.Length != 64) throw new Privacy.Domain.KeyRecoveryException("file di recupero non valido");
        Directory.CreateDirectory(AppConfig.Dir);
        File.WriteAllBytes(Path.Combine(AppConfig.Dir, "hmac.key"), ProtectedData.Protect(secret[..32], Entropy, DataProtectionScope.CurrentUser));
        File.WriteAllBytes(Path.Combine(AppConfig.Dir, "backup.key"), ProtectedData.Protect(secret[32..], Entropy, DataProtectionScope.CurrentUser));
    }
}
