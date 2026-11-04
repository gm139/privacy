#if DEBUG
using Npgsql;
using Privacy.Data;

namespace Privacy.App;

/// <summary>Solo in sviluppo (build Debug): crea uno studio e un utente sintetici, apre le finestre ed esercita login e caricamento.</summary>
internal static class SmokeTest
{
    public static async Task<int> RunAsync(AppServices s)
    {
        const string user = "smoke", pass = "frase di prova molto lunga 123";
        Guid practice;
        await using (var c = await s.Db.OpenAsync())
        {
            await using var p = new NpgsqlCommand("INSERT INTO ps.practices (name, controller_name, address, privacy_contact) VALUES ('Studio smoke','Dott. Smoke','Via Prova 1','privacy@example.test') RETURNING id", c);
            practice = (Guid)(await p.ExecuteScalarAsync())!;
        }
        var userId = await s.Auth.CreateUserAsync(practice, user, "Dott. Smoke", pass);
        await s.Requests.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, userId, null, "Mario (collab.)", "other", "email",
            DateTimeOffset.UtcNow.AddDays(-25), "Copia dei dati", new[] { "access" }));
        await s.Incidents.ReportAsync(new ReportIncidentCommand(Guid.NewGuid(), practice, userId, DateTimeOffset.UtcNow.AddHours(-10), DateTimeOffset.UtcNow.AddHours(-10), "Smarrimento dispositivo", "breach"));

        var login = new LoginWindow(s, practice);
        login.Show();
        await login.TryLoginAsync(user, "password errata di prova");
        Console.WriteLine("login errato -> messaggio: " + login.StatusText.Text);
        if (string.IsNullOrEmpty(login.StatusText.Text)) { Console.Error.WriteLine("SMOKE FAIL: messaggio di errore assente"); return 9; }
        await login.TryLoginAsync(user, pass);
        if (login.Result?.Outcome != LoginOutcome.Success) { Console.Error.WriteLine("SMOKE FAIL: login"); return 10; }

        var main = new MainWindow(s, practice, login.Result.DisplayName ?? "");
        main.Show();
        await main.LoadAsync();
        Console.WriteLine($"richieste={main.RequestCount} incidenti={main.IncidentCount}");
        var ok = main.RequestCount == 1 && main.IncidentCount == 1;
        main.Close(); login.Close();
        Console.WriteLine(ok ? "SMOKE OK" : "SMOKE FAIL: conteggi");
        return ok ? 0 : 11;
    }
}
#endif
