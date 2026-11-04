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

        var main = new MainWindow(s, practice, userId, login.Result.DisplayName ?? "");
        main.Show();
        await main.LoadAsync();
        Console.WriteLine($"richieste={main.RequestCount} incidenti={main.IncidentCount}");
        var ok = main.RequestCount == 1 && main.IncidentCount == 1;

        // schermate di registrazione: compilate e inviate via codice
        var cat = new CatalogService(s.Db);
        var nr = new NewRequestWindow(s, practice, userId, await cat.GetAsync("cat_requester_capacity"), await cat.GetAsync("cat_request_channel"), await cat.GetAsync("cat_right"));
        nr.Requester.Text = "Giulia Neri (smoke)"; nr.Text.Text = "Chiedo la rettifica";
        if (await nr.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: richiesta senza diritti accettata"); return 12; }
        nr.Rights.First(r => r.Code == "rectification").Box.IsChecked = true;
        if (!await nr.RunSubmitAsync() || nr.CreatedId is null) { Console.Error.WriteLine("SMOKE FAIL: nuova richiesta"); return 13; }
        var rd = new RequestDetailWindow(s, practice, userId, nr.CreatedId.Value);
        rd.Show(); await rd.LoadAsync();
        Console.WriteLine("dettaglio richiesta: " + rd.HeaderText);
        ok &= rd.HeaderText.Contains("R-") && rd.HeaderText.Contains("aperta");
        rd.Close();

        var ni = new NewIncidentWindow(s, practice, userId, await cat.GetAsync("cat_incident_classification"));
        ni.Description.Text = "Accesso non autorizzato (smoke)";
        ni.Awareness.Date.SelectedDate = DateTime.Today; ni.Awareness.Time.Text = "08:00";
        if (!await ni.RunSubmitAsync() || ni.CreatedId is null) { Console.Error.WriteLine("SMOKE FAIL: nuovo incidente"); return 14; }
        var idw = new IncidentDetailWindow(s, userId, ni.CreatedId.Value);
        idw.Show(); await idw.LoadAsync();
        Console.WriteLine("dettaglio incidente: " + idw.HeaderText);
        ok &= idw.HeaderText.Contains("I-") && idw.HeaderText.Contains("aperto");
        idw.Close();
        await main.LoadAsync();
        Console.WriteLine($"dopo inserimenti: richieste={main.RequestCount} incidenti={main.IncidentCount}");
        ok &= main.RequestCount == 2 && main.IncidentCount == 2;
        main.Close(); login.Close();
        Console.WriteLine(ok ? "SMOKE OK" : "SMOKE FAIL: conteggi");
        return ok ? 0 : 11;
    }
}
#endif
