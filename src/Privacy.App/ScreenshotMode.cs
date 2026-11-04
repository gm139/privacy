#if DEBUG
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Npgsql;
using Privacy.Data;

namespace Privacy.App;

/// <summary>
/// Solo sviluppo (build Debug): genera le immagini delle schermate per il manuale, con dati INVENTATI, su un database dimostrativo.
/// Uso: Privacy.App.exe --screenshots "C:\cartella"   (PRIVACY_CONN deve puntare a un database vuoto di dimostrazione)
/// </summary>
internal static class ScreenshotMode
{
    private static string _dir = "";

    public static async Task<int> RunAsync(AppServices real, string dir)
    {
        _dir = dir; Directory.CreateDirectory(dir);
        var tmp = Path.Combine(Path.GetTempPath(), "privacy-shots-" + Guid.NewGuid().ToString("N"));
        var cfg = new AppConfig(Path.Combine(tmp, "ledger", "disposals.jsonl"), Path.Combine(tmp, "backup"), Path.Combine(tmp, "esterna"), @"C:\Program Files\PostgreSQL\17\bin");
        var s = new AppServices(Environment.GetEnvironmentVariable("PRIVACY_CONN")!, new FakePgTools(), cfg);
        var setupSvc = new SetupService(s.Db);
        if (await setupSvc.IsConfiguredAsync()) { Console.Error.WriteLine("il database dimostrativo non è vuoto"); return 2; }

        // 01 - prima configurazione (compilata con dati inventati)
        var setup = new SetupWindow(s);
        setup.PracticeName.Text = "Studio Medico Rossi"; setup.Controller.Text = "Dott. Mario Rossi"; setup.Identifier.Text = "RSSMRA70A01B157X";
        setup.Address.Text = "Via Roma 10, 25100 Brescia (BS)"; setup.PrivacyContact.Text = "privacy@studio-rossi.example"; setup.Phone.Text = "030 0000000";
        setup.User.Text = "dott.rossi"; setup.DisplayName.Text = "Dott. Rossi"; setup.Pass.Password = "frase di prova molto lunga 123"; setup.Pass2.Password = "frase di prova molto lunga 123";
        await Snap(setup, "01-prima-configurazione");
        if (!await setup.RunSubmitAsync() || setup.Result is null) { Console.Error.WriteLine("setup fallito: " + setup.ErrorText); return 3; }
        var (practice, user) = setup.Result.Value;
        setup.Close();

        // 02 - accesso
        var login = new LoginWindow(s, practice);
        login.UserBox.Text = "dott.rossi"; login.PasswordBox.Password = "xxxxxxxxxxxx";
        await Snap(login, "02-accesso"); login.Close();

        await Seed(s, practice, user);

        var main = new MainWindow(s, practice, user, "Dott. Rossi") { Width = 1080, Height = 640 };
        main.Show(); await main.LoadAsync(); await Idle();
        var names = new[] { "03-richieste-in-scadenza", "04-incidenti-aperti", "05-conservazione", "06-backup", "07-registro", "08-pazienti", "09-informative" };
        for (var i = 0; i < names.Length; i++) { main.Tabs.SelectedIndex = i; await Snap(main, names[i]); }

        var cat = s.Catalog;
        // 10 - nuova richiesta
        var nr = new NewRequestWindow(s, practice, user, await cat.GetAsync("cat_requester_capacity"), await cat.GetAsync("cat_request_channel"), await cat.GetAsync("cat_right"));
        nr.Requester.Text = "Elena Marchi"; nr.Capacity.SelectedValue = "data_subject"; nr.Channel.SelectedValue = "email"; nr.Text.Text = "Chiedo una copia dei dati personali che mi riguardano.";
        nr.Rights.First(r => r.Code == "access").Box.IsChecked = true;
        await Snap(nr, "10-nuova-richiesta"); nr.Close();

        // 11 - dettaglio richiesta
        var reqs = await s.Requests.ListDueAsync(practice, DateOnly.FromDateTime(DateTime.Today), 400);
        var rd = new RequestDetailWindow(s, practice, user, reqs[0].Id);
        await Snap(rd, "11-dettaglio-richiesta", load: () => rd.LoadAsync()); rd.Close();

        // 12 - dettaglio incidente
        var incs = await s.Incidents.ListOpenAsync(practice);
        var idw = new IncidentDetailWindow(s, user, incs[0].Id);
        await Snap(idw, "12-dettaglio-incidente", load: () => idw.LoadAsync()); idw.Close();

        // 13 - editor di un'attività del registro
        var acts = await s.Registry.ListActivitiesAsync(practice);
        var draft = acts.First(a => a.StatusCode == "draft");
        var ed = new ActivityEditorWindow(s, practice, user, draft.VersionId) { Height = 800 };
        await Snap(ed, "13-registro-editor", load: () => ed.LoadAsync()); ed.Close();

        // 14 - nuova finalità
        var pw = new PurposeWindow(s, user, draft.VersionId, null, await cat.GetAsync("cat_consent_assessment"), await cat.GetAsync("cat_article6"), await cat.GetAsync("cat_article9"));
        pw.PurposeName.Text = "Erogazione del servizio di visita a distanza"; pw.Description.Text = "Visite e consulti svolti in videochiamata.";
        pw.Assessment.Text = "Necessario per l'esecuzione della prestazione sanitaria richiesta dal paziente."; pw.Consent.SelectedValue = "not_required";
        pw.Article6.SelectedValue = "contract"; pw.Article9.SelectedValue = "health_care"; pw.Scope.Text = "Prestazione sanitaria";
        await Snap(pw, "14-nuova-finalita"); pw.Close();

        // 15 - nuova informativa
        var nn = new NewNoticeWindow(s, practice, user, await s.Registry.ListPublishedAsync(practice)) { MaxHeight = 900 };
        nn.Activities[0].Box.IsChecked = true;
        await Snap(nn, "15-nuova-informativa"); nn.Close();

        // 16 - consegna e 17 - storico
        var subjects = await s.Subjects.SearchAsync(practice, "Verdi");
        var dw = new DeliveryWindow(s, user, subjects[0], await s.Notices.ListPublishedAsync(practice), await cat.GetAsync("cat_delivery_channel"), await cat.GetAsync("cat_receipt_status"));
        dw.Notice.SelectedIndex = 0; dw.Channel.SelectedValue = "in_person"; dw.Receipt.SelectedValue = "delivered_unsigned";
        await Snap(dw, "16-consegna-informativa"); dw.Close();
        var hw = new DeliveriesHistoryWindow(s, subjects[0]);
        hw.Show(); await hw.LoadAsync(); await hw.ShowSelectedAsyncFirst(); await Idle();
        await Snap(hw, "17-storico-consegne"); hw.Close();

        // 18 - eliminazione guidata (elementi scaduti)
        var expired = await s.Retention.ListExpiredAsync(practice, DateOnly.FromDateTime(DateTime.Today));
        if (expired.Count > 0) { var dis = new DisposeItemsWindow(s, user, expired); dis.Reason.Text = "Durata di conservazione approvata trascorsa."; await Snap(dis, "18-eliminazione-guidata"); dis.Close(); }

        // 19 - chiavi di recupero ; 20 - esportazione del registro
        var ek = new ExportKeysWindow(); ek.Pass.Text = "una frase segreta molto lunga"; ek.Pass2.Text = "una frase segreta molto lunga";
        await Snap(ek, "19-chiavi-di-recupero"); ek.Close();
        var export = await s.Registry.ExportAsOfAsync(practice, DateOnly.FromDateTime(DateTime.Today));
        var labels = new Dictionary<string, string>();
        foreach (var t in new[] { "cat_consent_assessment", "cat_article6", "cat_article9", "cat_data_category", "cat_subject_category", "cat_recipient_category" })
            foreach (var i in await cat.GetAsync(t)) labels[i.Code] = i.Label;
        var rx = new RegistryExportWindow(RegistryExportFormatter.Format(export, DateOnly.FromDateTime(DateTime.Today), "Studio Medico Rossi", labels), DateOnly.FromDateTime(DateTime.Today)) { Height = 720 };
        await Snap(rx, "20-esportazione-registro"); rx.Close();

        main.Close();
        try { Directory.Delete(tmp, true); } catch (IOException) { }
        Console.WriteLine("SCREENSHOTS OK " + Directory.GetFiles(dir, "*.png").Length);
        return 0;
    }

    private static async Task Seed(AppServices s, Guid practice, Guid user)
    {
        // interessati
        var names = new[] { ("Anna", "Verdi"), ("Luca", "Bianchi"), ("Giulia", "Neri"), ("Paolo", "Gialli"), ("Marta", "Conti"), ("Sara", "Ferri") };
        foreach (var (f, l) in names) await s.Subjects.CreateAsync(Guid.NewGuid(), practice, user, new SubjectInput(f, l, null, null, null, null));

        // registro: due attività pubblicate (una con riesame scaduto) e una in bozza
        async Task<Guid> Activity(string code, string name, string desc, string purpose, DateOnly review, bool publish, string[] data)
        {
            var v = await s.Registry.CreateActivityAsync(Guid.NewGuid(), practice, user, code, name);
            await s.Registry.UpdateDraftAsync(v, user, desc, review);
            await s.Registry.SetCategoriesAsync(v, user, data, new[] { "patients" }, new[] { "none_internal" });
            if (!publish) return v;
            var health = data.Contains("health_relation");
            await s.Registry.AddPurposeAsync(v, user, purpose, desc, "Trattamento necessario per l'esecuzione del rapporto con il paziente.", "not_required",
                new[] { new GroundExport("contract", health ? "health_care" : null, null, purpose) });
            await s.Registry.SubmitForReviewAsync(v, user);
            await s.Registry.PublishAsync(Guid.NewGuid(), v, user, DateOnly.FromDateTime(DateTime.Today.AddMonths(-8)), (await s.Registry.GetVersionAsync(v))!.RowVersion);
            return v;
        }
        var today = DateOnly.FromDateTime(DateTime.Today);
        await Activity("A1", "Cura dei pazienti", "Gestione del rapporto di cura con i pazienti.", "Cura e assistenza", today.AddMonths(10), true, new[] { "identification", "contact", "health_relation" });
        await Activity("A2", "Gestione amministrativa e fatturazione", "Fatturazione e adempimenti contabili.", "Adempimenti amministrativi e fiscali", today.AddDays(-12), true, new[] { "identification", "contact" });
        var a3 = await s.Registry.CreateActivityAsync(Guid.NewGuid(), practice, user, "A3", "Visite a distanza");
        await s.Registry.UpdateDraftAsync(a3, user, "Visite e consulti in videochiamata.", today.AddYears(1));
        await s.Registry.SetCategoriesAsync(a3, user, new[] { "identification", "health_relation" }, new[] { "patients" }, new[] { "it_provider" });

        // informativa e consegne
        var acts = await s.Registry.ListPublishedAsync(practice);
        var nv = await s.Notices.CreateAndPublishAsync(Guid.NewGuid(), practice, user, "Informativa ai pazienti",
            "INFORMATIVA SUL TRATTAMENTO DEI DATI PERSONALI (artt. 13 e 14 Reg. UE 2016/679)\n\nTitolare del trattamento: {{controller_name}} - {{practice_name}}, {{address}}. Contatti per le richieste privacy: {{privacy_contact}}.\n\n" +
            "Finalità: erogazione della prestazione sanitaria richiesta e adempimenti amministrativi e fiscali connessi.\n\nI diritti dell'interessato possono essere esercitati scrivendo al contatto indicato; è sempre possibile proporre reclamo al Garante per la protezione dei dati personali.\n\n" +
            "Versione {{document_version}} in vigore dal {{effective_date}}.", today.AddMonths(-6), acts.Select(a => a.VersionId).ToList(), s.HmacKey);
        var anna = (await s.Subjects.SearchAsync(practice, "Verdi"))[0]; var luca = (await s.Subjects.SearchAsync(practice, "Bianchi"))[0];
        await s.Notices.RecordDeliveryAsync(new RecordDeliveryCommand(Guid.NewGuid(), nv, anna.Id, null, user, "in_person", "delivered_unsigned", DateTimeOffset.UtcNow.AddDays(-20), null, null, null), s.HmacKey);
        await s.Notices.RecordDeliveryAsync(new RecordDeliveryCommand(Guid.NewGuid(), nv, luca.Id, null, user, "in_person", "signed_receipt", DateTimeOffset.UtcNow.AddDays(-9), null, null, null), s.HmacKey);

        // richieste: una in scadenza, una scaduta da poco (prorogata), una chiusa da eliminare
        var r1 = await s.Requests.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, user, null, "Chiara Fontana", "data_subject", "pec", DateTimeOffset.UtcNow.AddDays(-24), "Richiesta di accesso ai dati personali.", new[] { "access" }));
        var r2 = await s.Requests.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, user, null, "Davide Riva (collaboratore)", "other", "email", DateTimeOffset.UtcNow.AddDays(-33), "Richiesta di rettifica dei dati di contatto.", new[] { "rectification" }));
        await s.Requests.ExtendAsync(Guid.NewGuid(), r2, user, DateOnly.FromDateTime(DateTime.Today.AddDays(40)), "Complessità della richiesta.", DateTimeOffset.UtcNow.AddDays(-5));
        await s.Requests.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, user, null, "Franca Moretti", "heir_or_interested", "post", DateTimeOffset.UtcNow.AddDays(-3), "Richiesta di accesso ai dati di un familiare deceduto.", new[] { "access" }));
        var old = await s.Requests.RegisterAsync(new RegisterRequestCommand(Guid.NewGuid(), practice, user, null, "Ugo Serra", "data_subject", "email", DateTimeOffset.UtcNow.AddDays(-40), "Richiesta di accesso.", new[] { "access" }));
        await s.Requests.DecideAsync(Guid.NewGuid(), old, user, "access", "granted", null, DateTimeOffset.UtcNow.AddDays(-30));
        await s.Requests.CloseAsync(Guid.NewGuid(), old, user);

        // incidenti
        var i1 = await s.Incidents.ReportAsync(new ReportIncidentCommand(Guid.NewGuid(), practice, user, DateTimeOffset.UtcNow.AddHours(-60), DateTimeOffset.UtcNow.AddHours(-58), "Smarrimento di una chiavetta USB contenente una copia di lavoro di agende degli appuntamenti.", "breach"));
        await s.Incidents.AddActionAsync(Guid.NewGuid(), i1, user, "containment", DateTimeOffset.UtcNow.AddHours(-57), "Ricerca della chiavetta nello studio; modificate le credenziali di accesso ai servizi collegati.", null);
        await s.Incidents.ReportAsync(new ReportIncidentCommand(Guid.NewGuid(), practice, user, DateTimeOffset.UtcNow.AddHours(-5), null, "Possibile accesso non autorizzato a una casella email dello studio: in corso di verifica.", "to_assess"));

        // conservazione (valori INVENTATI per la dimostrazione, non indicazioni)
        var pol = await s.Retention.CreateDraftAsync(Guid.NewGuid(), practice, user, new PolicyDraft("requests", "request_closed", "fixed", 24, null, null, "Scelta dimostrativa", today.AddYears(1)));
        await s.Retention.ApproveAsync(Guid.NewGuid(), pol, user);
        var pol2 = await s.Retention.CreateDraftAsync(Guid.NewGuid(), practice, user, new PolicyDraft("clinical", "last_visit", "indefinite", null, null, "Da stabilire con il consulente: la documentazione clinica è gestita fuori dal programma.", null, today.AddDays(-20)));
        await s.Retention.ApproveAsync(Guid.NewGuid(), pol2, user);
        await s.Retention.CreateDraftAsync(Guid.NewGuid(), practice, user, new PolicyDraft("incidents", "incident_closed", "fixed", 60, null, null, "Bozza dimostrativa", today.AddYears(1)));
        // invecchia la richiesta chiusa (solo database dimostrativo, come amministratore)
        await using (var c = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PRIVACY_CONN")!) { Username = "postgres", Password = Environment.GetEnvironmentVariable("PRIVACY_DEMO_ADMIN_PASSWORD") ?? "x" }.ConnectionString))
        {
            await c.OpenAsync();
            await using var q = new NpgsqlCommand("UPDATE ps.rights_requests SET closed_at = now() - interval '3 years' WHERE id = $1", c);
            q.Parameters.AddWithValue(old);
            await q.ExecuteNonQueryAsync();
        }

        // backup (strumenti simulati)
        var key = KeyStore.GetOrCreateBackupKey();
        await s.Backup.BackupAsync(key, KeyStore.BackupKeyVersion);
        await s.Backup.BackupAsync(key, KeyStore.BackupKeyVersion);
    }

    private static Task Idle() => Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task;

    private static async Task Snap(Window w, string name, Func<Task>? load = null)
    {
        if (!w.IsVisible) w.Show();
        if (load is not null) await load();
        await Task.Delay(250); await Idle(); w.UpdateLayout(); await Idle();
        var root = (FrameworkElement)VisualTreeHelper.GetChild(w, 0);
        const double scale = 2;
        var pw = (int)Math.Ceiling(root.ActualWidth * scale); var ph = (int)Math.Ceiling(root.ActualHeight * scale);
        var rtb = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        rtb.Render(root);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
        await using var fs = File.Create(Path.Combine(_dir, name + ".png")); enc.Save(fs);
    }
}
#endif
