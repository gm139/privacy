using System.Security.Cryptography;
using System.Text;

namespace Privacy.Data;

public sealed class BackupFormatException : Exception
{
    public BackupFormatException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Cifratura dei backup: AES-256-GCM. Formato: "PSBK1" | nonce (12) | tag (16) | testo cifrato.
/// La versione della chiave è legata ai dati autenticati: una copia con versione alterata non si decifra.
/// Limite attuale: il file è trattato in memoria; per database grandi servirà una cifratura a flusso.
/// </summary>
public static class BackupCrypto
{
    private static readonly byte[] Magic = "PSBK1"u8.ToArray();

    public static byte[] Encrypt(byte[] key, string keyVersion, byte[] plaintext)
    {
        if (key.Length != 32) throw new ArgumentException("chiave da 32 byte richiesta", nameof(key));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[plaintext.Length];
        using var gcm = new AesGcm(key, 16);
        gcm.Encrypt(nonce, plaintext, cipher, tag, Aad(keyVersion));
        return Magic.Concat(nonce).Concat(tag).Concat(cipher).ToArray();
    }

    public static byte[] Decrypt(byte[] key, string keyVersion, byte[] file)
    {
        if (file.Length < Magic.Length + 28 || !file.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new BackupFormatException("file di backup non riconosciuto");
        var nonce = file.AsSpan(Magic.Length, 12);
        var tag = file.AsSpan(Magic.Length + 12, 16);
        var cipher = file.AsSpan(Magic.Length + 28);
        var plain = new byte[cipher.Length];
        try
        {
            using var gcm = new AesGcm(key, 16);
            gcm.Decrypt(nonce, cipher, tag, plain, Aad(keyVersion));
        }
        catch (CryptographicException e) { throw new BackupFormatException("backup alterato o chiave errata", e); }
        return plain;
    }

    private static byte[] Aad(string keyVersion) => Encoding.UTF8.GetBytes("PSBK1|" + keyVersion);
}
