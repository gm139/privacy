using System.Text;

namespace Privacy.Data;

/// <summary>Testo leggibile del registro alla data T (art. 30). Le etichette dei codici vengono dai cataloghi; un codice senza etichetta si mostra com'è.</summary>
public static class RegistryExportFormatter
{
    public static string Format(IReadOnlyList<ActivityExport> activities, DateOnly asOf, string practiceName, IReadOnlyDictionary<string, string> labels)
    {
        string L(string code) => labels.TryGetValue(code, out var l) ? l : code;
        var sb = new StringBuilder();
        sb.AppendLine($"REGISTRO DELLE ATTIVITÀ DI TRATTAMENTO - {practiceName}");
        sb.AppendLine($"Situazione alla data {asOf:dd/MM/yyyy} (art. 30 Reg. UE 2016/679)");
        sb.AppendLine();
        if (activities.Count == 0) sb.AppendLine("Nessuna attività pubblicata a questa data.");
        foreach (var a in activities)
        {
            sb.AppendLine($"{a.Code} - {a.Name} (revisione {a.VersionNumber}, in vigore dal {a.EffectiveFrom:dd/MM/yyyy}; riesame entro {a.ReviewDueOn:dd/MM/yyyy})");
            if (!string.IsNullOrWhiteSpace(a.Description)) sb.AppendLine($"  Descrizione: {a.Description}");
            foreach (var p in a.Purposes)
            {
                sb.AppendLine($"  Finalità: {p.Name}{(string.IsNullOrWhiteSpace(p.Description) ? "" : " - " + p.Description)}");
                sb.AppendLine($"    Valutazione sul consenso: {L(p.ConsentAssessmentCode)}");
                foreach (var g in p.Grounds)
                    sb.AppendLine($"    Base giuridica: {L(g.Article6Code)}{(g.Article9Code is null ? "" : "; condizione art. 9: " + L(g.Article9Code))}" +
                                  $"{(string.IsNullOrWhiteSpace(g.NationalReference) ? "" : "; riferimento: " + g.NationalReference)} ({g.Scope})");
            }
            sb.AppendLine($"  Categorie di dati: {string.Join(", ", a.DataCategories.Select(L))}");
            sb.AppendLine($"  Categorie di interessati: {string.Join(", ", a.SubjectCategories.Select(L))}");
            sb.AppendLine($"  Destinatari: {(a.RecipientCategories.Count == 0 ? "non indicati" : string.Join(", ", a.RecipientCategories.Select(L)))}");
            sb.AppendLine();
        }
        sb.AppendLine("Nota: l'esportazione non include ancora sistemi, misure di sicurezza e incarichi dei fornitori, né i termini di conservazione per finalità.");
        return sb.ToString();
    }
}
