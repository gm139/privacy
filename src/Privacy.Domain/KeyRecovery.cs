using System.Security.Cryptography;
using System.Text;

namespace Privacy.Domain;

public sealed class KeyRecoveryException : Exception
{
    public KeyRecoveryException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Esportazione delle chiavi per il recupero (procedura D-ENC provvisoria): le chiavi sono cifrate con una frase segreta
/// scelta dal titolare (PBKDF2-SHA256 + AES-256-GCM). Il file si conserva fuori dal PC, insieme ai backup.
/// Formato: "PSKR1" | sale (16) | iterazioni (4, big endian) | nonce (12) | tag (16) | testo cifrato.
/// </summary>
public static class KeyRecovery
{
    private static readonly byte[] Magic = "PSKR1"u8.ToArray();
    public const int Iterations = 600_000;
    public const int MinPassphraseLength = 14;

    public static byte[] Wrap(string passphrase, byte[] secret, int iterations = Iterations)
    {
        if (passphrase.Length < MinPassphraseLength) throw new ArgumentException($"la frase segreta deve avere almeno {MinPassphraseLength} caratteri", nameof(passphrase));
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[secret.Length];
        var it = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(it, iterations);
        using var gcm = new AesGcm(Derive(passphrase, salt, iterations), 16);
        gcm.Encrypt(nonce, secret, cipher, tag, Magic);
        return Magic.Concat(salt).Concat(it).Concat(nonce).Concat(tag).Concat(cipher).ToArray();
    }

    public static byte[] Unwrap(string passphrase, byte[] file)
    {
        const int header = 5 + 16 + 4 + 12 + 16;
        if (file.Length <= header || !file.AsSpan(0, 5).SequenceEqual(Magic)) throw new KeyRecoveryException("file di recupero non riconosciuto");
        var salt = file.AsSpan(5, 16).ToArray();
        var iterations = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(21, 4));
        if (iterations < 1000 || iterations > 10_000_000) throw new KeyRecoveryException("file di recupero non valido");
        var plain = new byte[file.Length - header];
        try
        {
            using var gcm = new AesGcm(Derive(passphrase, salt, iterations), 16);
            gcm.Decrypt(file.AsSpan(25, 12), file.AsSpan(header), file.AsSpan(37, 16), plain, Magic);
        }
        catch (CryptographicException e) { throw new KeyRecoveryException("frase segreta errata o file alterato", e); }
        return plain;
    }

    private static byte[] Derive(string passphrase, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, iterations, HashAlgorithmName.SHA256, 32);
}
