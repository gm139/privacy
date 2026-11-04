using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Privacy.Data;

namespace Privacy.App;

/// <summary>Nuova regola di conservazione (bozza). Non esiste durata predefinita: la sceglie il titolare.</summary>
public sealed class NewPolicyWindow : DialogForm
{
    public ComboBox RecordClass { get; } public ComboBox Trigger { get; } public ComboBox Kind { get; }
    public TextBox Months { get; } public TextBox Days { get; } public TextBox Criterion { get; } public TextBox Legal { get; } public DatePicker Review { get; }

    public NewPolicyWindow(AppServices s, Guid practiceId, Guid userId, IReadOnlyList<CatalogItem> classes, IReadOnlyList<CatalogItem> triggers, IReadOnlyList<CatalogItem> kinds)
        : base("Nuova regola di conservazione", 560)
    {
        var cmd = Guid.NewGuid();
        RecordClass = AddCombo("_Tipo di documentazione", classes);
        Trigger = AddCombo("Da quale _evento decorre la durata", triggers);
        Kind = AddCombo("_Natura della durata (determinata, illimitata motivata, a criterio)", kinds);
        Months = AddText("Durata in _mesi (solo per «determinata»)");
        Days = AddText("Durata in _giorni (facoltativa, si somma ai mesi)");
        Criterion = AddText("Criterio o motivazione (obbligatorio se non è «determinata»)", multiline: true);
        Legal = AddText("Riferimento normativo o scelta del titolare (facoltativo)");
        Review = AddDate("_Riesame della regola entro", DateOnly.FromDateTime(DateTime.Today.AddYears(1)));
        AddNote("La regola nasce in bozza e non ha effetto finché il titolare non la approva. Le durate le decide il titolare (con il consulente, se lo ritiene): il programma non ne propone.");
        Submit = async () =>
        {
            static int? Num(string t, string what) { if (string.IsNullOrWhiteSpace(t)) return null; return int.TryParse(t.Trim(), out var n) && n >= 0 ? n : throw new FormatException($"{what}: inserire un numero intero non negativo."); }
            var months = Num(Months.Text, "Mesi"); var days = Num(Days.Text, "Giorni");
            if (Review.SelectedDate is not { } r) return "Indicare la data di riesame della regola.";
            await s.Retention.CreateDraftAsync(cmd, practiceId, userId, new PolicyDraft(Sel(RecordClass), Sel(Trigger), Sel(Kind), months, days,
                string.IsNullOrWhiteSpace(Criterion.Text) ? null : Criterion.Text.Trim(), string.IsNullOrWhiteSpace(Legal.Text) ? null : Legal.Text.Trim(), DateOnly.FromDateTime(r)));
            return null;
        };
    }
}

/// <summary>Eliminazione guidata degli elementi scaduti selezionati. Irreversibile: richiede motivazione e conferma scritta.</summary>
public sealed class DisposeItemsWindow : DialogForm
{
    public TextBox Reason { get; } public TextBox Confirm { get; }
    public int Disposed { get; private set; }

    public DisposeItemsWindow(AppServices s, Guid userId, IReadOnlyList<ExpiringItem> items) : base("Eliminazione di elementi scaduti", 560)
    {
        AddNote($"Si stanno per eliminare {items.Count} element{(items.Count == 1 ? "o" : "i")}: " + string.Join(", ", items.Take(8).Select(i => i.Reference)) + (items.Count > 8 ? "…" : "") +
                ". L'operazione cancella i dati dal database e NON è reversibile. L'elenco delle eliminazioni (solo identificativi) viene salvato anche in un file separato, per riapplicarle dopo un eventuale ripristino da backup.");
        Reason = AddText("_Motivazione (autorizzazione del titolare)", multiline: true);
        Confirm = AddText("Per confermare digitare ELIMINA");
        Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(Reason.Text)) return "La motivazione è obbligatoria.";
            if (Confirm.Text.Trim() != "ELIMINA") return "Per confermare digitare ELIMINA.";
            foreach (var i in items)
                if (await s.Disposal.DisposeAsync(Guid.NewGuid(), i.ItemType, i.ItemId, userId, Reason.Text.Trim(), i.PolicyVersionId)) Disposed++;
            return null;
        };
    }
}

/// <summary>Frase segreta (due volte) per esportare le chiavi di recupero.</summary>
public sealed class ExportKeysWindow : DialogForm
{
    public TextBox Pass { get; } public TextBox Pass2 { get; }
    public byte[]? Result { get; private set; }
    public ExportKeysWindow() : base("Esporta le chiavi di recupero", 560)
    {
        AddNote("Le chiavi servono a decifrare i backup e a verificare i moduli già emessi. Senza questo file, se il computer o il profilo Windows vanno persi, i backup non si possono aprire. " +
                "Conservare il file fuori dal PC (es. chiavetta in cassaforte) e la frase segreta separatamente.");
        Pass = AddText("_Frase segreta (almeno 14 caratteri)");
        Pass2 = AddText("_Ripetere la frase segreta");
        Submit = () =>
        {
            if (Pass.Text != Pass2.Text) return Task.FromResult<string?>("Le due frasi non coincidono.");
            Result = KeyStore.ExportRecovery(Pass.Text);   // ArgumentException se troppo corta: mostrata da DialogForm
            return Task.FromResult<string?>(null);
        };
    }
}

/// <summary>Reinstalla le chiavi da un file di recupero.</summary>
public sealed class ImportKeysWindow : DialogForm
{
    public TextBox Pass { get; } public TextBox? Confirm { get; }
    public ImportKeysWindow(byte[] file) : base("Importa le chiavi di recupero", 560)
    {
        var exists = KeyStore.KeysExist();
        Pass = AddText("_Frase segreta del file");
        if (exists) { AddNote("ATTENZIONE: su questo computer ci sono già delle chiavi. Importando si SOSTITUISCONO: i backup e i moduli cifrati con le chiavi attuali non saranno più apribili."); Confirm = AddText("Per confermare digitare SOSTITUISCI"); }
        Submit = () =>
        {
            if (exists && Confirm!.Text.Trim() != "SOSTITUISCI") return Task.FromResult<string?>("Per confermare digitare SOSTITUISCI.");
            try { KeyStore.ImportRecovery(Pass.Text, file); }
            catch (Privacy.Domain.KeyRecoveryException e) { return Task.FromResult<string?>(e.Message + "."); }
            return Task.FromResult<string?>(null);
        };
    }
}

/// <summary>Controlli di sicurezza condivisi dalla scheda Backup.</summary>
public static class BackupAdvice
{
    /// <summary>Messaggio sullo stato dei backup rispetto all'obiettivo proposto di 24 ore (da confermare con il medico).</summary>
    public static string Status(IReadOnlyList<BackupRunRow> runs, DateTimeOffset now, bool canBackup)
    {
        if (!canBackup) return "Backup non configurato: indicare la cartella degli strumenti PostgreSQL (pgBinDir) in config.json.";
        var last = runs.Where(r => r.StatusCode == "ok" && r.VerifiedAt is not null).OrderByDescending(r => r.StartedAt).FirstOrDefault();
        if (last is null) return "ATTENZIONE: nessun backup verificato.";
        var age = now - last.StartedAt;
        return age > TimeSpan.FromHours(24)
            ? $"ATTENZIONE: l'ultimo backup verificato risale a {(int)age.TotalHours} ore fa (obiettivo proposto: 24 ore)."
            : $"Ultimo backup verificato: {last.StartedAt.ToLocalTime():dd/MM/yyyy HH:mm}.";
    }
}
