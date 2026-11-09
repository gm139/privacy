namespace Privacy.Domain;

/// <summary>
/// Controllo formale del codice fiscale (16 caratteri, con carattere di controllo) e della partita IVA (11 cifre, con cifra di controllo).
/// Serve a scoprire subito un errore di battitura: il codice finisce nelle informative ai pazienti.
/// </summary>
public static class CodiceFiscale
{
    /// <summary>Restituisce un messaggio d'errore, oppure null se il valore è vuoto o formalmente valido.</summary>
    public static string? Check(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        if (v.All(char.IsAsciiDigit))
            return v.Length == 11
                ? (PartitaIvaOk(v) ? null : "La partita IVA non è valida: la cifra di controllo non torna. Controllare le 11 cifre.")
                : "Il codice fiscale deve avere 16 caratteri (lettere e cifre); la partita IVA 11 cifre.";

        if (v.Length != 16) return "Il codice fiscale deve avere 16 caratteri.";
        if (!v.All(char.IsAsciiLetterOrDigit)) return "Il codice fiscale può contenere solo lettere e cifre.";
        return v[15] == CheckChar(v)
            ? null
            : "Il codice fiscale non è valido: il carattere di controllo non torna. Controllare di averlo digitato correttamente (attenzione a 0/O e 1/I).";
    }

    public static bool IsValid(string? value) => Check(value) is null;

    // valori per le posizioni dispari (1ª, 3ª, ...): cifre e lettere secondo la tabella dell'Agenzia delle Entrate
    private static readonly int[] OddDigits = [1, 0, 5, 7, 9, 13, 15, 17, 19, 21];
    private static readonly int[] OddLetters = [1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18, 20, 11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23];

    /// <summary>Carattere di controllo: la lettera che rende valido il codice (posizione 16).</summary>
    public static char CheckChar(string cf)
    {
        var sum = 0;
        for (var i = 0; i < 15; i++)
            sum += i % 2 == 0 ? OddValue(cf[i]) : EvenValue(cf[i]);
        return (char)('A' + sum % 26);
    }

    private static int OddValue(char c) => char.IsAsciiDigit(c) ? OddDigits[c - '0'] : OddLetters[c - 'A'];

    private static int EvenValue(char c) => char.IsAsciiDigit(c) ? c - '0' : c - 'A';

    /// <summary>Partita IVA: somma delle cifre in posizione dispari più il doppio (meno 9 se a due cifre) delle pari.</summary>
    private static bool PartitaIvaOk(string v)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var d = v[i] - '0';
            if (i % 2 == 0) sum += d;
            else { var x = d * 2; sum += x > 9 ? x - 9 : x; }
        }
        return (10 - sum % 10) % 10 == v[10] - '0';
    }
}
