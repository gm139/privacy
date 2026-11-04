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
            if (e.Args.Contains("--reapply-disposals")) { Shutdown(await ReapplyDisposalsAsync(services)); return; }
            var practice = await services.GetPracticeIdAsync();
            if (practice is null)
            {
                MessageBox.Show("Studio non configurato.", "Privacy Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(3); return;
            }
            var login = new LoginWindow(services, practice.Value);
            if (login.ShowDialog() != true || login.Result is not { UserId: not null } r) { Shutdown(0); return; }
            var main = new MainWindow(services, practice.Value, r.UserId.Value, r.DisplayName ?? "");
            main.Closed += (_, _) => Shutdown(0);
            main.Show();
        }
        catch (Exception ex)
        {
            if (smoke) { Console.Error.WriteLine("SMOKE FAIL (eccezione): " + ex); Shutdown(99); return; }   // niente finestre bloccanti nelle prove automatiche
            MessageBox.Show("Avvio non riuscito. Verificare che il database sia attivo.", "Privacy Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Da eseguire UNA volta dopo ogni ripristino da backup: rielimina ciò che era già stato cancellato (registro esterno).
    /// Uso: Privacy.App.exe --reapply-disposals
    /// </summary>
    private static async Task<int> ReapplyDisposalsAsync(AppServices services)
    {
        var practice = await services.GetPracticeIdAsync();
        Guid? actor = null;
        if (practice is not null)
        {
            await using var c = await services.Db.OpenAsync();
            await using var q = new Npgsql.NpgsqlCommand("SELECT id FROM ps.users WHERE practice_id = $1 AND enabled ORDER BY created_at LIMIT 1", c);
            q.Parameters.AddWithValue(practice.Value);
            actor = await q.ExecuteScalarAsync() as Guid?;
        }
        if (actor is null) { MessageBox.Show("Nessun utente abilitato: impossibile registrare l'operazione.", "Privacy Studio", MessageBoxButton.OK, MessageBoxImage.Warning); return 4; }
        var r = await services.Disposal.ReapplyAfterRestoreAsync(actor.Value);
        var text = $"Eliminazioni riapplicate: {r.Applied}\nGià assenti (normale): {r.AlreadyAbsent}\nDa verificare a mano: {r.Failed.Count}";
        if (r.Failed.Count > 0) text += "\n\n" + string.Join("\n", r.Failed.Take(10).Select(f => $"{f.Entry.ItemType} {f.Entry.ItemId}: {f.Error}"));
        MessageBox.Show(text, "Riapplicazione delle eliminazioni", MessageBoxButton.OK, r.Failed.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        return r.Failed.Count == 0 ? 0 : 5;
    }
}
