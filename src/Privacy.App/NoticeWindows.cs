using System.Windows.Controls;
using Privacy.Data;
using Privacy.Domain;

namespace Privacy.App;

/// <summary>Nuova informativa: modello con variabili, collegamento al registro, approvazione e pubblicazione.</summary>
public sealed class NewNoticeWindow : DialogForm
{
    public const string StarterText =
        "INFORMATIVA SUL TRATTAMENTO DEI DATI PERSONALI (artt. 13 e 14 Reg. UE 2016/679)\n\n" +
        "Titolare del trattamento: {{controller_name}} - {{practice_name}}, {{address}}. Contatti per la privacy: {{privacy_contact}}.\n\n" +
        "[Completare: finalità del trattamento e relative basi giuridiche]\n\n" +
        "[Completare: categorie di dati trattati e fonte, se non raccolti presso l'interessato]\n\n" +
        "[Completare: destinatari o categorie di destinatari]\n\n" +
        "[Completare: periodo di conservazione o criteri per determinarlo]\n\n" +
        "[Completare: diritti dell'interessato, come esercitarli e diritto di reclamo al Garante]\n\n" +
        "Versione {{document_version}} in vigore dal {{effective_date}}.";

    public TextBox Title_ { get; } public TextBox Body { get; } public DatePicker From { get; }
    public IReadOnlyList<(CheckBox Box, string Code)> Activities { get; }
    public Guid? CreatedVersionId { get; private set; }

    public NewNoticeWindow(AppServices s, Guid practiceId, Guid userId, IReadOnlyList<PublishedActivity> activities) : base("Nuova informativa", 640)
    {
        var createCmd = Guid.NewGuid(); var publishCmd = Guid.NewGuid();
        Title_ = AddText("_Titolo", "Informativa ai pazienti");
        Body = AddText("_Testo (modificabile)", StarterText, multiline: true);
        Body.MinHeight = 260;
        From = AddDate("_In vigore dal", DateOnly.FromDateTime(DateTime.Today));
        Activities = AddChecks("Attività del registro a cui l'informativa si riferisce (almeno una, solo attività pubblicate)",
            activities.Select(a => new CatalogItem(a.VersionId.ToString(), $"{a.Code} - {a.Name} (v{a.VersionNumber})")));
        AddNote("Variabili ammesse: " + string.Join(", ", TemplateResolver.AllowedVariables.Order().Select(v => "{{" + v + "}}")) +
                ". Le parti «[Completare: ...]» sono un promemoria e vanno sostituite: il testo di partenza NON è un'informativa valida finché non lo approvate " +
                "(e, se lo ritenete, non lo fate verificare dal consulente). Una volta pubblicata, la versione non si modifica.");
        Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(Title_.Text)) return "Indicare un titolo.";
            if (From.SelectedDate is not { } d) return "Indicare la data di entrata in vigore.";
            var linked = Activities.Where(a => a.Box.IsChecked == true).Select(a => Guid.Parse(a.Code)).ToList();
            if (linked.Count == 0) return "Selezionare almeno un'attività del registro (deve essere pubblicata).";
            try
            {
                var v = await s.Notices.CreateDraftAsync(createCmd, practiceId, userId, Title_.Text.Trim(), Body.Text);
                await s.Notices.PublishAsync(publishCmd, v, userId, DateOnly.FromDateTime(d), linked, s.HmacKey);
                CreatedVersionId = v;
            }
            catch (TemplateException e) { return "Testo non pubblicabile: " + e.Message + "."; }
            return null;
        };
    }
}
