using System.IO;
using System.Windows;
using Privacy.Data;

namespace Privacy.App;

public partial class App : Application
{
    public App()
    {
        // un errore non gestito lascia una traccia scritta: senza, una finestra che si chiude non dice nulla
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogError("processo", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { LogError("attività", e.Exception); e.SetObserved(); };
    }

    private void OnDispatcherException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogError("interfaccia", e.Exception);
        MessageBox.Show("Errore imprevisto. Il dettaglio è stato scritto in:" + Environment.NewLine + ErrorLogPath +
                        Environment.NewLine + Environment.NewLine + "Il programma verrà chiuso; i dati già salvati non sono toccati.",
                        "Privacy Studio", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(10);
    }

    internal static string ErrorLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrivacyStudio", "errori.log");

    private static void LogError(string kind, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ErrorLogPath)!);
            File.AppendAllText(ErrorLogPath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\t{kind}\t{ex}{Environment.NewLine}");
        }
        catch { /* se non si può scrivere il registro non si complica l'errore originale */ }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Apply(e.Args.Contains("--screenshots") ? ThemeChoiceFromArgs(e.Args) : UiSettings.LoadTheme());
        var smoke = e.Args.Contains("--smoke");
        try
        {
            if (e.Args.Contains("--restore")) { Shutdown(await RestoreCommandAsync(e.Args)); return; }
            var cs = AppServices.ReadConnectionString();
            if (cs is null)
            {
                MessageBox.Show("Configurazione del database mancante.", "Privacy Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(2); return;
            }
            var services = new AppServices(cs);
#if DEBUG
            if (smoke) { Shutdown(await SmokeTest.RunAsync(services)); return; }
            if (e.Args.Contains("--seed-demo")) { Shutdown(await ScreenshotMode.SeedOnlyAsync()); return; }
            if (e.Args.Contains("--screenshots")) { Shutdown(await ScreenshotMode.RunAsync(services, e.Args[Array.IndexOf(e.Args, "--screenshots") + 1])); return; }
#endif
            // allinea il file esterno delle eliminazioni con il database: copre un arresto fra il commit e la scrittura del file
            await SyncLedgerAsync(services);
            if (e.Args.Contains("--reapply-disposals")) { Shutdown(await ReapplyDisposalsAsync(services)); return; }
            var practice = await services.GetPracticeIdAsync();
            if (practice is null)
            {
                // primo avvio: configurazione dello studio e del primo utente
                var setup = new SetupWindow(services);
                if (setup.ShowDialog() != true || setup.Result is null) { Shutdown(3); return; }
                practice = setup.Result.Value.PracticeId;
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
    /// Riallinea il registro esterno delle eliminazioni con il database. Un arresto fra il commit dell'eliminazione
    /// e la scrittura del file lascerebbe il database senza la copia esterna: dopo un ripristino i dati tornerebbero visibili.
    /// </summary>
    private static async Task SyncLedgerAsync(AppServices services)
    {
        try { await services.Disposal.SyncLedgerAsync(); }
        catch (Exception ex)
        {
            MessageBox.Show("Registro esterno delle eliminazioni non allineato: " + ex.Message +
                            "\n\nLe eliminazioni registrate nel database potrebbero non essere nel file esterno.", "Privacy Studio",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
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

    /// <summary>
    /// Ripristino su ambiente pulito. Uso:
    ///   set PRIVACY_ADMIN_CONN=Host=...;Port=5432;Database=postgres;Username=postgres;Password=...
    ///   Privacy.App.exe --restore "D:\Backup\privacy-....psbk" --database privacy [--ledger "D:\...\disposals.jsonl"]
    /// La stringa di amministratore passa dall'ambiente (non dalla riga di comando, visibile ad altri processi).
    /// </summary>
    private static async Task<int> RestoreCommandAsync(string[] args)
    {
        string? Arg(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var file = Arg("--restore"); var database = Arg("--database");
        var admin = Environment.GetEnvironmentVariable("PRIVACY_ADMIN_CONN");
        var config = AppConfig.Load();
        if (file is null || database is null || string.IsNullOrWhiteSpace(admin) || config.PgBinDir is null)
        {
            MessageBox.Show("Uso: Privacy.App.exe --restore <file.psbk> --database <nome nuovo> [--ledger <file>]\n" +
                            "Servono: variabile PRIVACY_ADMIN_CONN (amministratore del database) e pgBinDir in config.json.", "Ripristino", MessageBoxButton.OK, MessageBoxImage.Information);
            return 2;
        }
        var ledger = Arg("--ledger") ?? (File.Exists(file + ".ledger.jsonl") ? file + ".ledger.jsonl" : null);
        try
        {
            var report = await RestoreService.RestoreAsync(new RestoreRequest(file, KeyStore.GetOrCreateBackupKey(), KeyStore.BackupKeyVersion, admin, database, ledger,
                PgProcessTools.Native(config.PgBinDir, admin, database)));
            var text = $"Ripristino in «{report.TargetDatabase}» completato.\n" +
                       $"Eliminazioni riapplicate: {report.DisposalsReapplied}; già assenti: {report.DisposalsAlreadyAbsent}; da verificare a mano: {report.DisposalsToCheck.Count}\n" +
                       $"Catena di audit: {(report.AuditChainBrokenAtId is null ? "integra" : "INTERROTTA all'id " + report.AuditChainBrokenAtId)}\n" +
                       $"Registro esterno delle eliminazioni: {(report.LedgerProvided ? "usato" : "NON fornito: i dati cancellati dopo il backup potrebbero essere tornati visibili")}\n" +
                       $"Prova di ripristino registrata: {(report.RestoreTestRecorded ? "sì" : "no")}\n\n" +
                       "Prossimi passi: impostare le password dei ruoli privacy_app e privacy_migrator, puntare l'applicazione al nuovo database e controllare i dati.";
            if (report.DisposalsToCheck.Count > 0) text += "\n\nDa verificare:\n" + string.Join("\n", report.DisposalsToCheck.Take(10));
            MessageBox.Show(text, "Ripristino", MessageBoxButton.OK, report.Clean ? MessageBoxImage.Information : MessageBoxImage.Warning);
            return report.Clean ? 0 : 5;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or BackupFormatException
                                   or Npgsql.PostgresException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Ripristino non eseguito: " + ex.Message, "Ripristino", MessageBoxButton.OK, MessageBoxImage.Error);
            return 3;
        }
    }

    private static ThemeChoice ThemeChoiceFromArgs(string[] args)
    {
        var i = Array.IndexOf(args, "--theme");
        return i >= 0 && i + 1 < args.Length && Enum.TryParse<ThemeChoice>(args[i + 1], true, out var c) ? c : ThemeChoice.Light;
    }
}
