using System.Security.Cryptography;

namespace Privacy.Domain;

/// <summary>
/// Verificatore di password nel formato «algoritmo$versione$iterazioni$sale$hash» (base64).
/// PBKDF2-HMAC-SHA256 con sale casuale; il numero di iterazioni è nel verificatore, quindi si può alzare nel tempo
/// e <see cref="NeedsRehash"/> segnala i verificatori da aggiornare al prossimo accesso riuscito.
/// </summary>
public static class PasswordHasher
{
    public const string Algorithm = "pbkdf2-sha256";
    public const int CurrentIterations = 600_000;   // indicazione OWASP per PBKDF2-HMAC-SHA256
    private const int SaltBytes = 16, HashBytes = 32;

    public static string Hash(string password, int iterations = CurrentIterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Algorithm}$1${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string verifier)
    {
        if (!TryParse(verifier, out var iterations, out var salt, out var expected)) return false;
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static bool NeedsRehash(string verifier) => !TryParse(verifier, out var it, out _, out _) || it < CurrentIterations;

    /// <summary>Verificatore fittizio con lo stesso costo, per non distinguere «utente inesistente» dai tempi di risposta.</summary>
    public static readonly string Dummy = Hash("dummy-password-not-used");

    private static bool TryParse(string verifier, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0; salt = hash = Array.Empty<byte>();
        var p = verifier.Split('$');
        if (p.Length != 5 || p[0] != Algorithm || p[1] != "1" || !int.TryParse(p[2], out iterations) || iterations < 1) return false;
        try { salt = Convert.FromBase64String(p[3]); hash = Convert.FromBase64String(p[4]); return hash.Length > 0; }
        catch (FormatException) { return false; }
    }
}

/// <summary>Regole minime sulla password scelta dall'utente.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    /// <summary>Elenco dei motivi di rifiuto (vuoto se accettabile). Lunghezza prima della complessità; nessuna scadenza periodica.</summary>
    public static IReadOnlyList<string> Check(string password, string username)
    {
        var errs = new List<string>();
        if (password.Length < MinLength) errs.Add($"almeno {MinLength} caratteri");
        if (password.Distinct().Count() < 5) errs.Add("troppo ripetitiva");
        if (!string.IsNullOrWhiteSpace(username) && password.Contains(username, StringComparison.OrdinalIgnoreCase)) errs.Add("contiene il nome utente");
        return errs;
    }
}
