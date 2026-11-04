using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Privacy.Domain;

public sealed class TemplateException : Exception
{
    public TemplateException(string message) : base(message) { }
}

/// <summary>Risolve i segnaposto {{variabile}} di un modello con un catalogo CHIUSO di variabili, alla pubblicazione.</summary>
public static class TemplateResolver
{
    public static readonly IReadOnlySet<string> AllowedVariables = new HashSet<string>
    {
        "practice_name", "controller_name", "controller_identifier", "address", "privacy_contact",
        "phone", "dpo_contact", "document_version", "effective_date"
    };

    private static readonly Regex Placeholder = new(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);

    /// <summary>Le parti guida «[Completare: ...]» del testo di partenza non devono arrivare in un documento pubblicato.</summary>
    public static bool HasPendingGuidance(string text) => text.Contains("[Completare", StringComparison.OrdinalIgnoreCase);

    public static string Resolve(string template, IReadOnlyDictionary<string, string> values) =>
        HasPendingGuidance(template) ? throw new TemplateException("restano parti guida «[Completare...]» da sostituire") :
        Placeholder.Replace(template, m =>
        {
            var name = m.Groups[1].Value;
            if (!AllowedVariables.Contains(name)) throw new TemplateException($"variabile non ammessa: {name}");
            if (!values.TryGetValue(name, out var v) || string.IsNullOrWhiteSpace(v)) throw new TemplateException($"valore mancante per {name}");
            return v;
        });
}

/// <summary>Dati IMMUTABILI da cui si ricostruisce il modulo di una consegna: nessun dato corrente dello studio.</summary>
public sealed record NoticeRenderInput(string Body, int VersionNumber, string DeliveryCode, string SubjectName,
    DateOnly? Date, string? SignatoryName, string? SignatoryCapacity);

/// <summary>Modulo consegnato: testo canonico deterministico e relativo HMAC (non hash nudo).</summary>
public static class NoticeDocument
{
    public static string Render(NoticeRenderInput i)
    {
        var sb = new StringBuilder();
        sb.Append(i.Body.Replace("\r\n", "\n").TrimEnd()).Append("\n\n");
        sb.Append("---\n");
        sb.Append($"Versione: {i.VersionNumber}   Codice: {i.DeliveryCode}\n");
        sb.Append($"Paziente: {i.SubjectName}\n");
        if (i.Date is { } d) sb.Append($"Data: {d:yyyy-MM-dd}\n");
        if (i.SignatoryName is not null)
            sb.Append($"Firmatario: {i.SignatoryName}{(i.SignatoryCapacity is null ? "" : $" ({i.SignatoryCapacity})")}\n");
        sb.Append("Ricevuta (spazio per la firma, facoltativa): ____________________\n");
        return sb.ToString();
    }

    public static byte[] Hmac(byte[] key, string renderedText) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(renderedText));

    /// <summary>Codice di consegna nello stesso formato del valore predefinito del database.</summary>
    public static string NewDeliveryCode() => "PS-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
}
