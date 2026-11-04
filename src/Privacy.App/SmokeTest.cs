#if DEBUG
using System.IO;
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
        nr.Capacity.SelectedIndex = 0; nr.Channel.SelectedIndex = 0; nr.Requester.Text = "Giulia Neri (smoke)"; nr.Text.Text = "Chiedo la rettifica";
        if (await nr.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: richiesta senza diritti accettata"); return 12; }
        nr.Rights.First(r => r.Code == "rectification").Box.IsChecked = true;
        if (!await nr.RunSubmitAsync() || nr.CreatedId is null) { Console.Error.WriteLine("SMOKE FAIL: nuova richiesta"); return 13; }
        var rd = new RequestDetailWindow(s, practice, userId, nr.CreatedId.Value);
        rd.Show(); await rd.LoadAsync();
        Console.WriteLine("dettaglio richiesta: " + rd.HeaderText);
        ok &= rd.HeaderText.Contains("R-") && rd.HeaderText.Contains("aperta");
        rd.Close();

        var ni = new NewIncidentWindow(s, practice, userId, await cat.GetAsync("cat_incident_classification"));
        ni.Classification.SelectedIndex = 0; ni.Description.Text = "Accesso non autorizzato (smoke)";
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


        // F01 dall'interfaccia: nuova attività -> editor -> finalità -> categorie -> pubblicazione -> sola lettura
        var na = new NewActivityWindow(s, practice, userId);
        na.Code.Text = "UI1"; na.ActivityName.Text = "Gestione degli appuntamenti (smoke)";
        if (!await na.RunSubmitAsync() || na.CreatedVersionId is not { } uiVersion) { Console.Error.WriteLine("SMOKE FAIL: nuova attività"); return 21; }
        var editor = new ActivityEditorWindow(s, practice, userId, uiVersion);
        editor.Show(); await editor.LoadAsync();
        editor.DescriptionBox.Text = "Agenda e promemoria"; editor.ReviewPicker.SelectedDate = DateTime.Today.AddYears(1);
        editor.Check("cat_data_category", "identification"); editor.Check("cat_subject_category", "patients"); editor.Check("cat_recipient_category", "none_internal");
        await editor.SaveAsync();
        var pw = new PurposeWindow(s, userId, uiVersion, null, await cat.GetAsync("cat_consent_assessment"), await cat.GetAsync("cat_article6"), await cat.GetAsync("cat_article9"));
        pw.PurposeName.Text = "Organizzazione degli appuntamenti"; pw.Description.Text = "Gestione dell'agenda"; pw.Assessment.Text = "Necessario per erogare la prestazione"; pw.Scope.Text = "Agenda";
        if (await pw.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: finalità senza scelte accettata"); return 22; }
        pw.Consent.SelectedValue = "not_required"; pw.Article6.SelectedValue = "contract";
        if (!await pw.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: finalità: " + pw.ErrorText); return 23; }
        await editor.LoadAsync();
        var detail = (await s.Registry.GetVersionAsync(uiVersion))!;
        ok &= detail.Purposes.Count == 1 && detail.DataCategories.Count == 1;
        await s.Registry.SubmitForReviewAsync(uiVersion, userId);
        await s.Registry.PublishAsync(Guid.NewGuid(), uiVersion, userId, DateOnly.FromDateTime(DateTime.Today), (await s.Registry.GetVersionAsync(uiVersion))!.RowVersion);
        await editor.LoadAsync();
        Console.WriteLine("editor dopo la pubblicazione: " + editor.HeaderText + " | " + editor.StatusText);
        ok &= editor.HeaderText.Contains("pubblicata") && editor.StatusText.Contains("non è modificabile");
        editor.Close();
        var exported = await s.Registry.ExportAsOfAsync(practice, DateOnly.FromDateTime(DateTime.Today));
        ok &= exported.Count == 1 && exported[0].Code == "UI1";

        // F02: registro attività pubblicato -> informativa -> interessato -> consegna -> storico -> rimozione
        var v = await s.Registry.CreateActivityAsync(Guid.NewGuid(), practice, userId, "A1", "Cura dei pazienti");
        await s.Registry.UpdateDraftAsync(v, userId, "Cartella clinica", new DateOnly(2027, 11, 1));
        await s.Registry.AddPurposeAsync(v, userId, "Cura", "Terapia", "Art. 9(2)(h)", "not_required", new[] { new GroundExport("contract", "health_care", null, "Cura") });
        await s.Registry.SetCategoriesAsync(v, userId, new[] { "identification", "health_relation" }, new[] { "patients" }, new[] { "none_internal" });
        await s.Registry.SubmitForReviewAsync(v, userId);
        int rv;
        await using (var c = await s.Db.OpenAsync())
        {
            await using var q = new NpgsqlCommand("SELECT row_version FROM ps.activity_versions WHERE id = $1", c);
            q.Parameters.AddWithValue(v);
            rv = (int)(await q.ExecuteScalarAsync())!;
        }
        await s.Registry.PublishAsync(Guid.NewGuid(), v, userId, new DateOnly(2026, 6, 1), rv);

        var nn = new NewNoticeWindow(s, practice, userId, await s.Registry.ListPublishedAsync(practice));
        nn.Activities[0].Box.IsChecked = true;
        if (await nn.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: testo di partenza con parti da completare pubblicato"); return 15; }
        nn.Body.Text = "Titolare {{controller_name}}, {{address}}. Contatti {{privacy_contact}}. Versione {{document_version}} dal {{effective_date}}.";
        if (!await nn.RunSubmitAsync() || nn.CreatedVersionId is null) { Console.Error.WriteLine("SMOKE FAIL: pubblicazione informativa: " + nn.ErrorText); return 16; }

        var sf = new SubjectFormWindow(s, practice, userId, null);
        sf.First.Text = "Anna"; sf.Last.Text = "Verdi (smoke)";
        if (!await sf.RunSubmitAsync() || sf.CreatedId is null) { Console.Error.WriteLine("SMOKE FAIL: nuovo interessato"); return 17; }
        var subject = (await s.Subjects.GetAsync(sf.CreatedId.Value))!;

        var dw = new DeliveryWindow(s, userId, subject, await s.Notices.ListPublishedAsync(practice), await cat.GetAsync("cat_delivery_channel"), await cat.GetAsync("cat_receipt_status"));
        dw.Channel.SelectedIndex = 0; dw.Receipt.SelectedValue = "delivered_unsigned";
        if (!await dw.RunSubmitAsync() || dw.CreatedCode is null) { Console.Error.WriteLine("SMOKE FAIL: consegna"); return 18; }
        Console.WriteLine("consegna: " + dw.CreatedCode);

        var hw = new DeliveriesHistoryWindow(s, subject);
        hw.Show(); await hw.LoadAsync();
        ok &= hw.Count == 1;
        hw.Close();
        var delivery = (await s.Notices.ListDeliveriesAsync(subject.Id)).Single();
        var rebuilt = await s.Notices.ReconstructAsync(delivery.Id, s.HmacKey);
        Console.WriteLine("integrità prima della rimozione: " + rebuilt.Matches);
        ok &= rebuilt.Matches;

        var rm = new RemoveSubjectWindow(s, userId, subject);
        rm.Reason.Text = "Valutazione del titolare (smoke)";
        if (await rm.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: rimozione senza conferma"); return 19; }
        rm.Confirm.Text = "RIMUOVI";
        if (!await rm.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: rimozione"); return 20; }
        var after = await s.Notices.ReconstructAsync(delivery.Id, s.HmacKey);
        Console.WriteLine("integrità dopo la rimozione: " + after.Matches);
        ok &= !after.Matches && (await s.Subjects.GetAsync(subject.Id))!.StatusCode == "removed";
        await main.LoadAsync();
        Console.WriteLine($"pazienti={main.SubjectCount} informative={main.NoticeCount}");
        ok &= main.SubjectCount == 1 && main.NoticeCount == 1 && main.ActivityCount == 2;

        // F05/F06: conservazione, eliminazione guidata, backup (con strumenti simulati) e chiavi di recupero
        var tmp = Path.Combine(Path.GetTempPath(), "privacy-smoke-" + Guid.NewGuid().ToString("N"));
        var cfg = new AppConfig(Path.Combine(tmp, "ledger", "disposals.jsonl"), Path.Combine(tmp, "backup"), Path.Combine(tmp, "external"), "simulato");
        var s2 = new AppServices(Environment.GetEnvironmentVariable("PRIVACY_CONN")!, new FakePgTools(), cfg);
        var closedId = await s.Requests.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, userId, null, "Paolo (smoke)", "other", "email", DateTimeOffset.UtcNow, "Copia", new[] { "access" }));
        await s.Requests.DecideAsync(Guid.NewGuid(), closedId, userId, "access", "granted", null, DateTimeOffset.UtcNow);
        await s.Requests.CloseAsync(Guid.NewGuid(), closedId, userId);

        var np = new NewPolicyWindow(s2, practice, userId, await cat.GetAsync("cat_record_class"), await cat.GetAsync("cat_retention_trigger"), await cat.GetAsync("cat_duration_kind"));
        if (await np.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: regola senza scelte accettata"); return 24; }
        np.RecordClass.SelectedValue = "requests"; np.Trigger.SelectedValue = "request_closed"; np.Kind.SelectedValue = "fixed"; np.Months.Text = "0";
        if (!await np.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: nuova regola: " + np.ErrorText); return 25; }
        var policy = (await s2.Retention.ListPoliciesAsync(practice)).Single();
        await s2.Retention.ApproveAsync(Guid.NewGuid(), policy.Id, userId);
        var expired = await s2.Retention.ListExpiredAsync(practice, DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        ok &= expired.Any(x => x.ItemId == closedId);

        var dis = new DisposeItemsWindow(s2, userId, expired.Where(x => x.ItemId == closedId).ToList());
        dis.Reason.Text = "Durata approvata trascorsa (smoke)";
        if (await dis.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: eliminazione senza conferma"); return 26; }
        dis.Confirm.Text = "ELIMINA";
        if (!await dis.RunSubmitAsync() || dis.Disposed != 1) { Console.Error.WriteLine("SMOKE FAIL: eliminazione: " + dis.ErrorText); return 27; }
        ok &= await s.Requests.GetDetailAsync(closedId) is null;
        ok &= new DisposalLedger(cfg.LedgerPath).Read().Any(x => x.ItemId == closedId);

        var br = await s2.Backup.BackupAsync(KeyStore.GetOrCreateBackupKey(), KeyStore.BackupKeyVersion);
        ok &= File.Exists(br.PrimaryPath) && File.Exists(br.ExternalPath) && File.Exists(br.ExternalPath + ".ledger.jsonl");
        BackupService.VerifyFile(br.ExternalPath!, KeyStore.GetOrCreateBackupKey(), KeyStore.BackupKeyVersion);
        try { BackupService.VerifyFile(br.ExternalPath!, new byte[32], KeyStore.BackupKeyVersion); ok = false; } catch (BackupFormatException) { }

        var hmacBefore = KeyStore.GetOrCreateHmacKey(); var backupBefore = KeyStore.GetOrCreateBackupKey();
        var recovery = KeyStore.ExportRecovery("frase di recupero molto lunga");
        KeyStore.ImportRecovery("frase di recupero molto lunga", recovery);
        ok &= KeyStore.GetOrCreateHmacKey().SequenceEqual(hmacBefore) && KeyStore.GetOrCreateBackupKey().SequenceEqual(backupBefore);

        var ex = new ExportKeysWindow(); ex.Pass.Text = "frase di recupero molto lunga"; ex.Pass2.Text = "un'altra frase diversa lunga";
        if (await ex.RunSubmitAsync()) { Console.Error.WriteLine("SMOKE FAIL: frasi diverse accettate"); return 28; }
        ex.Pass2.Text = ex.Pass.Text;
        if (!await ex.RunSubmitAsync() || ex.Result is null) { Console.Error.WriteLine("SMOKE FAIL: esportazione chiavi: " + ex.ErrorText); return 29; }

        await main.LoadAsync();
        Console.WriteLine($"regole={main.PolicyCount} scaduti={main.ExpiredCount} backup={main.BackupRunCount} | {main.BackupStatusText}");
        ok &= main.PolicyCount == 1 && main.BackupRunCount >= 1 && main.BackupStatusText.Contains("non configurato");
        try { Directory.Delete(tmp, true); } catch (IOException) { }
        main.Close(); login.Close();
        Console.WriteLine(ok ? "SMOKE OK" : "SMOKE FAIL: conteggi");
        return ok ? 0 : 11;
    }
}
/// <summary>Strumenti PostgreSQL simulati per la prova automatica: restituiscono un file con l'intestazione di pg_dump.</summary>
internal sealed class FakePgTools : IPgTools
{
    public Task<byte[]> DumpAsync(CancellationToken ct = default) => Task.FromResult("PGDMP-simulato"u8.ToArray());
    public Task RestoreAsync(byte[] dump, CancellationToken ct = default) => Task.CompletedTask;
}
#endif
