using System.Windows.Controls;
using Privacy.Data;

namespace Privacy.App;

/// <summary>
/// Prima configurazione, in DUE passaggi per non nascondere campi sotto la riga di scorrimento:
/// 1) dati dello studio; 2) nome utente e password (con la casella per vedere la password).
/// Si apre da sola al primo avvio.
/// </summary>
public sealed class SetupWindow : DialogForm
{
    public TextBox PracticeName { get; } public TextBox Controller { get; } public TextBox Identifier { get; } public TextBox Address { get; }
    public TextBox PrivacyContact { get; } public TextBox Phone { get; } public TextBox Dpo { get; }
    // creati al secondo passaggio
    public TextBox User { get; private set; } = null!; public TextBox DisplayName { get; private set; } = null!;
    public PasswordField Pass { get; private set; } = null!; public PasswordField Pass2 { get; private set; } = null!;
    public (Guid PracticeId, Guid UserId)? Result { get; private set; }

    public SetupWindow(AppServices s) : base("Prima configurazione - passaggio 1 di 2: lo studio", 600)
    {
        AddNote("Questi dati compaiono nelle informative ai pazienti. Il titolare del trattamento è il medico (o lo studio) che decide finalità e mezzi del trattamento. Nel passaggio successivo si sceglie la password di accesso.");
        PracticeName = AddText("_Denominazione dello studio");
        Controller = AddText("_Titolare del trattamento (nome e cognome)");
        Identifier = AddText("Codice fiscale o partita IVA del titolare (facoltativo)");
        Address = AddText("_Indirizzo dello studio");
        PrivacyContact = AddText("Contatto per le richieste privacy (email o _PEC)");
        Phone = AddText("Telefono (facoltativo)");
        Dpo = AddText("Contatto del DPO, se nominato (facoltativo)");
        OkText = "_Avanti";

        static string? N(string t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
        Submit = () =>
        {
            foreach (var (v, n) in new[] { (PracticeName.Text, "denominazione"), (Controller.Text, "titolare"), (Address.Text, "indirizzo"), (PrivacyContact.Text, "contatto privacy") })
                if (string.IsNullOrWhiteSpace(v)) return Task.FromResult<string?>($"Campo obbligatorio: {n}.");

            // passaggio 2: credenziali
            ClearFields();
            Title = "Prima configurazione - passaggio 2 di 2: il tuo accesso";
            AddNote("Scegli il nome utente e la password con cui il medico entra nel programma. La password deve avere almeno 12 caratteri. Non esiste un modo per recuperarla se dimenticata: annotarla in un luogo sicuro.");
            User = AddText("Nome _utente per l'accesso");
            DisplayName = AddText("Nome mostrato nel programma (es. Dott. Rossi)");
            Pass = AddPassword("_Password (almeno 12 caratteri)", withToggle: false);
            Pass2 = AddPassword("_Ripetere la password", withToggle: true, linked: Pass);   // la casella sotto il secondo campo mostra entrambe
            AddNote("Dopo il primo accesso conviene esportare le chiavi di recupero (scheda Backup).");
            OkText = "_Crea lo studio";
            StayOpenOnce();
            Submit = async () =>
            {
                if (string.IsNullOrWhiteSpace(User.Text) || string.IsNullOrWhiteSpace(DisplayName.Text)) return "Nome utente e nome mostrato sono obbligatori.";
                if (Pass.Value.Length == 0) return "Inserire la password.";
                if (Pass.Value != Pass2.Value) return "Le due password non coincidono.";
                Result = await new SetupService(s.Db).CreateFirstAsync(new PracticeInput(PracticeName.Text.Trim(), Controller.Text.Trim(), N(Identifier.Text),
                    Address.Text.Trim(), PrivacyContact.Text.Trim(), N(Phone.Text), N(Dpo.Text)), User.Text.Trim(), DisplayName.Text.Trim(), Pass.Value);
                return null;
            };
            return Task.FromResult<string?>(null);
        };
    }
}
