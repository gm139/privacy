using System.Windows;
using System.Windows.Controls;
using Privacy.Data;
using Privacy.Domain;

namespace Privacy.App;

/// <summary>Creazione o rettifica dell'anagrafica di un interessato.</summary>
public sealed class SubjectFormWindow : DialogForm
{
    public TextBox First { get; } public TextBox Last { get; } public DatePicker Birth { get; }
    public TextBox TaxCode { get; } public TextBox Email { get; } public TextBox Phone { get; }
    public Guid? CreatedId { get; private set; }

    public SubjectFormWindow(AppServices s, Guid practiceId, Guid userId, SubjectRecord? existing)
        : base(existing is null ? "Nuovo interessato" : "Rettifica anagrafica")
    {
        var cmd = Guid.NewGuid();
        First = AddText("_Nome", existing?.FirstName);
        Last = AddText("_Cognome", existing?.LastName);
        Birth = AddDate("_Data di nascita (facoltativa)", existing?.BirthDate);
        TaxCode = AddText("Codice _fiscale (facoltativo)", existing?.TaxCode);
        Email = AddText("_Email (facoltativa)", existing?.Email);
        Phone = AddText("_Telefono (facoltativo)", existing?.Phone);
        if (existing is not null) AddNote("La rettifica cambia solo l'anagrafica corrente: i moduli già consegnati restano come emessi.");
        Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(First.Text) || string.IsNullOrWhiteSpace(Last.Text)) return "Nome e cognome sono obbligatori.";
            static string? N(string t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
            var input = new SubjectInput(First.Text.Trim(), Last.Text.Trim(), Birth.SelectedDate is { } d ? DateOnly.FromDateTime(d) : null, N(TaxCode.Text), N(Email.Text), N(Phone.Text));
            if (existing is null) CreatedId = await s.Subjects.CreateAsync(cmd, practiceId, userId, input);
            else await s.Subjects.RectifyAsync(cmd, existing.Id, userId, input);
            return null;
        };
    }
}

/// <summary>Rimozione di un interessato (F07): azione non reversibile, previa valutazione del titolare.</summary>
public sealed class RemoveSubjectWindow : DialogForm
{
    public TextBox Reason { get; } public TextBox Confirm { get; }
    public RemoveSubjectWindow(AppServices s, Guid userId, SubjectRecord subject) : base("Rimozione dell'interessato")
    {
        var cmd = Guid.NewGuid();
        AddNote($"Si sta per rimuovere {subject.FirstName} {subject.LastName} ({subject.InternalCode}). L'anagrafica viene azzerata e le prove di consegna rese anonime. L'operazione NON è reversibile e va fatta solo dopo la valutazione del titolare (art. 17 e obblighi di conservazione).");
        Reason = AddText("_Motivazione della valutazione del titolare", multiline: true);
        Confirm = AddText("Per confermare digitare RIMUOVI");
        Submit = async () =>
        {
            if (string.IsNullOrWhiteSpace(Reason.Text)) return "La motivazione è obbligatoria.";
            if (Confirm.Text.Trim() != "RIMUOVI") return "Per confermare digitare RIMUOVI.";
            await s.Subjects.RemoveAsync(cmd, subject.Id, userId, Reason.Text.Trim());
            return null;
        };
    }
}

/// <summary>Registrazione della consegna di un'informativa. Registra la consegna, non il consenso.</summary>
public sealed class DeliveryWindow : DialogForm
{
    public ComboBox Notice { get; } public ComboBox Channel { get; } public ComboBox Receipt { get; }
    public DateTimeField DeliveredAt { get; } public TextBox Signatory { get; } public TextBox Capacity { get; } public TextBox Note { get; }
    public string? CreatedCode { get; private set; }

    public DeliveryWindow(AppServices s, Guid userId, SubjectRecord subject, IReadOnlyList<PublishedNotice> notices,
        IReadOnlyList<CatalogItem> channels, IReadOnlyList<CatalogItem> receipts) : base("Consegna di un'informativa")
    {
        var cmd = Guid.NewGuid();
        AddNote($"Interessato: {subject.FirstName} {subject.LastName} ({subject.InternalCode})");
        Notice = AddCombo("_Informativa", notices.Select(n => new CatalogItem(n.VersionId.ToString(), $"{n.Title} - versione {n.VersionNumber}")));
        Channel = AddCombo("_Modalità di consegna", channels);
        Receipt = AddCombo("_Esito", receipts);
        DeliveredAt = AddDateTime("_Consegnata il (vuoto solo per «tentativo fallito»)", DateTimeOffset.UtcNow);
        Signatory = AddText("Nome del firmatario, se diverso dal paziente (facoltativo)");
        Capacity = AddText("Qualità del firmatario (facoltativa)");
        Note = AddText("_Note (facoltative)");
        AddNote("Se il paziente riceve ma non firma, scegliere «Consegnata senza firma»: non si registra alcun consenso o firma.");
        Submit = async () =>
        {
            var v = Sel(Notice);
            var failed = Sel(Receipt) == "failed_attempt";
            var at = DeliveredAt.Value;
            if (failed && at is not null) return "Un tentativo fallito non ha data di consegna: svuotare il campo.";
            if (!failed && at is null) return "Indicare la data di consegna.";
            static string? N(string t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
            if (N(Capacity.Text) is not null && N(Signatory.Text) is null) return "Indicare anche il nome del firmatario.";
            var (_, code) = await s.Notices.RecordDeliveryAsync(new RecordDeliveryCommand(cmd, Guid.Parse(v), subject.Id, null, userId,
                Sel(Channel), Sel(Receipt), at, N(Signatory.Text), N(Capacity.Text), N(Note.Text)), s.HmacKey);
            CreatedCode = code;
            return null;
        };
    }
}

/// <summary>Storico delle consegne di un interessato, con ricostruzione del modulo e controllo dell'integrità.</summary>
public sealed class DeliveriesHistoryWindow : Window
{
    private readonly AppServices _s; private readonly SubjectRecord _subject;
    private readonly ListBox _list = new() { MinHeight = 90 };
    private readonly TextBox _preview = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MinHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _integrity = new() { Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    private IReadOnlyList<DeliveryRow> _rows = Array.Empty<DeliveryRow>();
    private Dictionary<string, string> _receiptLabels = new();

    public int Count => _rows.Count;
    public string? LastIntegrityText => _integrity.Text;

    public DeliveriesHistoryWindow(AppServices s, SubjectRecord subject)
    {
        _s = s; _subject = subject;
        FontSize = 14; Title = $"Consegne - {subject.FirstName} {subject.LastName}"; Width = 760; SizeToContent = SizeToContent.Height; MaxHeight = 760; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var show = new Button { Content = "_Mostra modulo", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 8, 8, 0) };
        var copy = new Button { Content = "_Copia testo", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 8, 0, 0) };
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Consegne registrate", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(_list);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { show, copy } });
        panel.Children.Add(_integrity); panel.Children.Add(_preview);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        System.Windows.Automation.AutomationProperties.SetName(_list, "Consegne registrate");
        System.Windows.Automation.AutomationProperties.SetName(_preview, "Testo del modulo");
        show.Click += async (_, _) => await ShowSelectedAsync();
        copy.Click += (_, _) => { if (_preview.Text.Length > 0) Clipboard.SetText(_preview.Text); };
        Loaded += async (_, _) => await LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            if (_receiptLabels.Count == 0) _receiptLabels = (await _s.Catalog.GetAsync("cat_receipt_status")).ToDictionary(i => i.Code, i => i.Label);
            _rows = await _s.Notices.ListDeliveriesAsync(_subject.Id);
            _list.ItemsSource = _rows.Select(r => $"{r.DeliveryCode}   {r.DeliveredAt?.ToLocalTime():dd/MM/yyyy HH:mm}   {r.Title} v{r.VersionNumber}   [{(_receiptLabels.TryGetValue(r.ReceiptStatusCode, out var rl) ? rl : r.ReceiptStatusCode)}]").ToList();
        }
        catch (Exception) { _integrity.Text = "Impossibile leggere le consegne."; }
    }

    /// <summary>Per le prove e le immagini del manuale: seleziona la prima consegna e mostra il modulo.</summary>
    public async Task ShowSelectedAsyncFirst() { if (_rows.Count > 0) _list.SelectedIndex = 0; await ShowSelectedAsync(); }

    public async Task ShowSelectedAsync()
    {
        if (_list.SelectedIndex < 0) { _integrity.Text = "Selezionare una consegna."; return; }
        try
        {
            var r = await _s.Notices.ReconstructAsync(_rows[_list.SelectedIndex].Id, _s.HmacKey);
            _preview.Text = r.Text;
            _integrity.Text = r.Matches
                ? "Integrità verificata: il modulo ricostruito è identico a quello emesso (HMAC coincidente)."
                : "L'impronta NON coincide: i dati dell'interessato sono stati rimossi o la chiave è diversa. Il testo mostrato è quello ricostruibile oggi.";
        }
        catch (Exception) { _integrity.Text = "Impossibile ricostruire il modulo."; }
    }
}
