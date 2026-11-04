using System.Windows;
using Privacy.Data;

namespace Privacy.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var smoke = e.Args.Contains("--smoke");
        try
        {
            var cs = AppServices.ReadConnectionString();
            if (cs is null)
            {
                MessageBox.Show("Configurazione del database mancante.", "Privacy Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(2); return;
            }
            var services = new AppServices(cs);
#if DEBUG
            if (smoke) { Shutdown(await SmokeTest.RunAsync(services)); return; }
#endif
            var practice = await services.GetPracticeIdAsync();
            if (practice is null)
            {
                MessageBox.Show("Studio non configurato.", "Privacy Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(3); return;
            }
            var login = new LoginWindow(services, practice.Value);
            if (login.ShowDialog() != true || login.Result is not { UserId: not null } r) { Shutdown(0); return; }
            var main = new MainWindow(services, practice.Value, r.DisplayName ?? "");
            main.Closed += (_, _) => Shutdown(0);
            main.Show();
        }
        catch (Exception)
        {
            MessageBox.Show("Avvio non riuscito. Verificare che il database sia attivo.", "Privacy Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
