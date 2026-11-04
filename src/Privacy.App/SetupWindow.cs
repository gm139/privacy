using System.Windows.Controls;
using Privacy.Data;

namespace Privacy.App;

/// <summary>Prima configurazione dello studio e del primo utente (si apre da sola al primo avvio).</summary>
public sealed class SetupWindow : DialogForm
{
    public TextBox PracticeName { get; } public TextBox Controller { get; } public TextBox Identifier { get; } public TextBox Address { get; }
    public TextBox PrivacyContact { get; } public TextBox Phone { get; } public TextBox Dpo { get; }
    public TextBox User { get; } public TextBox DisplayName { get; } public PasswordBox Pass { get; } public PasswordBox Pass2 { get; }
    public (Guid PracticeId, Guid UserId)? Result { get; private set; }

    public SetupWindow(AppServices s) : base("Prima configurazione", 600)
    {
        AddNote("Questi dati compaiono nelle informative ai pazienti. Il titolare del trattamento è il medico (o lo studio) che decide finalità e mezzi del trattamento.");
        PracticeName = AddText("_Denominazione dello studio");
        Controller = AddText("_Titolare del trattamento (nome e cognome)");
        Identifier = AddText("Codice fiscale o partita IVA del titolare (facoltativo)");
        Address = AddText("_Indirizzo dello studio");
        PrivacyContact = AddText("Contatto per le richieste privacy (email o _PEC)");
        Phone = AddText("Telefono (facoltativo)");
        Dpo = AddText("Contatto del DPO, se nominato (facoltativo)");
        User = AddText("Nome _utente per l'accesso");
        DisplayName = AddText("Nome mostrato nel programma (es. Dott. Rossi)");
        Pass = new PasswordBox { MinWidth = 200 }; Pass2 = new PasswordBox { MinWidth = 200 };
        AddControl("_Password (almeno 12 caratteri)", Pass);
        AddControl("_Ripetere la password", Pass2);
        AddNote("Non c'è modo di recuperare una password dimenticata: annotarla in un luogo sicuro. Dopo il primo accesso conviene esportare le chiavi di recupero (scheda Backup).");
        Submit = async () =>
        {
            foreach (var (v, n) in new[] { (PracticeName.Text, "denominazione"), (Controller.Text, "titolare"), (Address.Text, "indirizzo"), (PrivacyContact.Text, "contatto privacy"), (User.Text, "nome utente"), (DisplayName.Text, "nome mostrato") })
                if (string.IsNullOrWhiteSpace(v)) return $"Campo obbligatorio: {n}.";
            if (Pass.Password != Pass2.Password) return "Le due password non coincidono.";
            static string? N(string t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
            Result = await new SetupService(s.Db).CreateFirstAsync(new PracticeInput(PracticeName.Text.Trim(), Controller.Text.Trim(), N(Identifier.Text),
                Address.Text.Trim(), PrivacyContact.Text.Trim(), N(Phone.Text), N(Dpo.Text)), User.Text.Trim(), DisplayName.Text.Trim(), Pass.Password);
            return null;
        };
    }
}
