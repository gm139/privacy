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
    private static readonly byte[] Entropy = "PrivacyStudio.hmac.v1"u8.ToArray();

    public static byte[] GetOrCreateHmacKey()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrivacyStudio");
        var path = Path.Combine(dir, "hmac.key");
        if (File.Exists(path)) return ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(dir);
        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(path, ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser));
        return key;
    }
}
