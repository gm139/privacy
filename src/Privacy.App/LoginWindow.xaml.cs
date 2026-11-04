using System.Windows;
using Privacy.Data;

namespace Privacy.App;

public partial class LoginWindow : Window
{
    private readonly AppServices _services;
    private readonly Guid _practiceId;
    public LoginResult? Result { get; private set; }

    public LoginWindow(AppServices services, Guid practiceId)
    {
        InitializeComponent();
        _services = services; _practiceId = practiceId;
        Loaded += (_, _) => UserBox.Focus();
    }

    /// <summary>Messaggi uniformi: non rivelano se l'utente esiste e non mostrano dettagli tecnici.</summary>
    public static string MessageFor(LoginResult r) => r.Outcome switch
    {
        LoginOutcome.Locked => $"Troppi tentativi. Riprovare dopo le {r.LockedUntil?.ToLocalTime():HH:mm}.",
        LoginOutcome.Disabled => "Accesso non consentito. Rivolgersi al titolare.",
        _ => "Nome utente o password non corretti."
    };

    private async void OnLogin(object sender, RoutedEventArgs e)
    {
        await TryLoginAsync(UserBox.Text, PasswordBox.Password);
    }

    public async Task TryLoginAsync(string user, string password)
    {
        LoginButton.IsEnabled = false;
        StatusText.Text = "";
        try
        {
            var result = await _services.Auth.LoginAsync(_practiceId, user, password);
            if (result.Outcome == LoginOutcome.Success) { Result = result; DialogResult = true; return; }
            StatusText.Text = MessageFor(result);
            PasswordBox.Clear();
            PasswordBox.Focus();
        }
        catch (Exception)
        {
            // nessun dettaglio tecnico a video (potrebbe contenere dati); l'errore va nel registro tecnico, privo di contenuti
            StatusText.Text = "Impossibile completare l'accesso. Verificare che il database sia attivo.";
        }
        finally { LoginButton.IsEnabled = true; }
    }
}
