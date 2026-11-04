using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Npgsql;
using Privacy.Data;

namespace Privacy.App;

/// <summary>Campo data + ora (ora italiana). Vuoto = nessun valore.</summary>
public sealed class DateTimeField
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
    public DatePicker Date { get; } = new();
    public TextBox Time { get; } = new() { Width = 70, Margin = new Thickness(8, 0, 0, 0) };
    public FrameworkElement Visual { get; }

    public DateTimeField(DateTimeOffset? initial)
    {
        Visual = new StackPanel { Orientation = Orientation.Horizontal, Children = { Date, Time } };
        System.Windows.Automation.AutomationProperties.SetName(Time, "Ora (HH:mm)");
        if (initial is { } v)
        {
            var local = TimeZoneInfo.ConvertTime(v, Rome);
            Date.SelectedDate = local.DateTime.Date;
            Time.Text = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
    }

    public DateTimeOffset? Value
    {
        get
        {
            if (Date.SelectedDate is not { } d) return null;
            if (!TimeSpan.TryParseExact(string.IsNullOrWhiteSpace(Time.Text) ? "00:00" : Time.Text.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out var t))
                throw new FormatException("Ora non valida: usare il formato HH:mm (es. 09:30).");
            var local = DateTime.SpecifyKind(d.Date + t, DateTimeKind.Unspecified);
            if (Rome.IsInvalidTime(local)) throw new FormatException("Orario inesistente (cambio d'ora).");
            return new DateTimeOffset(local, Rome.GetUtcOffset(local));
        }
    }
}

/// <summary>Finestra di inserimento costruita da campi con etichette collegate (tastiera e lettori di schermo).</summary>
public class DialogForm : Window
{
    private readonly StackPanel _fields = new() { Margin = new Thickness(20, 20, 20, 8) };
    private readonly TextBlock _error = new() { Foreground = ThemeManager.Error, TextWrapping = TextWrapping.Wrap, MinHeight = 20, Margin = new Thickness(0, 4, 0, 8) };
    private readonly Button _ok = new() { Content = "_Conferma", IsDefault = true, MinWidth = 100 };
    private readonly Button _cancel = new() { Content = "A_nnulla", IsCancel = true, MinWidth = 100, Margin = new Thickness(8, 0, 0, 0) };
    private int _tab;

    /// <summary>Restituisce un messaggio d'errore da mostrare, oppure null se l'operazione è riuscita (la finestra si chiude).</summary>
    public Func<Task<string?>>? Submit { get; set; }

    /// <summary>Ultimo messaggio d'errore mostrato (per le prove automatiche).</summary>
    public string ErrorText => _error.Text;

    public DialogForm(string title, double width = 500)
    {
        FontSize = 14; Title = title; Width = width; MaxHeight = 760; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(20, 0, 20, 16), Children = { _ok, _cancel } };
        var bottom = new StackPanel { Children = { new Border { Child = _error, Margin = new Thickness(20, 0, 20, 0) }, buttons } };
        var dock = new DockPanel();
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(new ScrollViewer { Content = _fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = dock;
        _ok.Click += async (_, _) => await RunSubmitAsync();
    }

    public async Task<bool> RunSubmitAsync()
    {
        if (Submit is null) return false;
        _ok.IsEnabled = false; _error.Text = "";
        try
        {
            var err = await Submit();
            if (err is null) { if (IsVisible) { try { DialogResult = true; } catch (InvalidOperationException) { Close(); } } return true; }
            _error.Text = err;
        }
        catch (FormatException e) { _error.Text = e.Message; }
        catch (PostgresException e) { _error.Text = FriendlyDb(e); }
        catch (ArgumentException e) { _error.Text = e.Message; }
        catch (Exception) { _error.Text = "Operazione non riuscita. Riprovare; se persiste, contattare l'assistenza."; }
        finally { _ok.IsEnabled = true; }
        return false;
    }

    /// <summary>Le regole del database sono scritte in italiano e senza dati personali: si mostrano. Il resto resta generico.</summary>
    public static string FriendlyDb(PostgresException e) => e.SqlState switch
    {
        "P0001" or "23514" or "check_violation" or "restrict_violation" or "23001" => e.MessageText,
        "23505" => "Esiste già un elemento con questi dati.",
        _ => "Operazione rifiutata dal database. Controllare i dati inseriti."
    };

    private Label Lbl(string text, Control target) => new() { Content = text, Target = target, Padding = new Thickness(0, 10, 0, 2) };

    public TextBox AddText(string label, string? initial = null, bool multiline = false)
    {
        var box = new TextBox { Text = initial ?? "", TabIndex = _tab++ };
        if (multiline) { box.AcceptsReturn = true; box.TextWrapping = TextWrapping.Wrap; box.MinHeight = 80; box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; }
        System.Windows.Automation.AutomationProperties.SetName(box, label.Replace("_", ""));
        _fields.Children.Add(Lbl(label, box)); _fields.Children.Add(box);
        return box;
    }

    /// <summary>Modello per elenchi con testo a capo (nessuna barra di scorrimento orizzontale).</summary>
    public static DataTemplate WrapTemplate()
    {
        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("."));
        tb.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        return new DataTemplate { VisualTree = tb };
    }

    /// <summary>Valore scelto in un elenco; se non è stata fatta alcuna scelta, errore comprensibile (nessuna scelta predefinita).</summary>
    public static string Sel(ComboBox box)
    {
        if (box.SelectedValue is string v) return v;
        var name = System.Windows.Automation.AutomationProperties.GetName(box);
        throw new FormatException($"Selezionare un valore: {name}.");
    }

    /// <summary>Aggiunge un controllo già costruito (es. PasswordBox) con la sua etichetta.</summary>
    public void AddControl(string label, Control control)
    {
        control.TabIndex = _tab++;
        System.Windows.Automation.AutomationProperties.SetName(control, label.Replace("_", ""));
        _fields.Children.Add(Lbl(label, control)); _fields.Children.Add(control);
    }

    public ComboBox AddCombo(string label, IEnumerable<CatalogItem> items)
    {
        var box = new ComboBox { ItemsSource = items.ToList(), DisplayMemberPath = nameof(CatalogItem.Label), SelectedValuePath = nameof(CatalogItem.Code), TabIndex = _tab++ };
        if (box.Items.Count == 1) box.SelectedIndex = 0;   // nessuna scelta preselezionata, salvo quando non ce n'è un'altra
        System.Windows.Automation.AutomationProperties.SetName(box, label.Replace("_", ""));
        _fields.Children.Add(Lbl(label, box)); _fields.Children.Add(box);
        return box;
    }

    public DateTimeField AddDateTime(string label, DateTimeOffset? initial)
    {
        var f = new DateTimeField(initial);
        f.Date.TabIndex = _tab++; f.Time.TabIndex = _tab++;
        _fields.Children.Add(Lbl(label, f.Date)); _fields.Children.Add(f.Visual);
        return f;
    }

    public DatePicker AddDate(string label, DateOnly? initial)
    {
        var d = new DatePicker { TabIndex = _tab++, SelectedDate = initial?.ToDateTime(TimeOnly.MinValue) };
        System.Windows.Automation.AutomationProperties.SetName(d, label.Replace("_", ""));
        _fields.Children.Add(Lbl(label, d)); _fields.Children.Add(d);
        return d;
    }

    /// <summary>Elenco di caselle SENZA alcuna preselezione.</summary>
    public IReadOnlyList<(CheckBox Box, string Code)> AddChecks(string label, IEnumerable<CatalogItem> items)
    {
        _fields.Children.Add(new TextBlock { Text = label.Replace("_", ""), Margin = new Thickness(0, 10, 0, 2), FontWeight = FontWeights.SemiBold });
        var list = new List<(CheckBox, string)>();
        foreach (var i in items)
        {
            var cb = new CheckBox { Content = i.Label, TabIndex = _tab++, Margin = new Thickness(0, 2, 0, 2) };
            _fields.Children.Add(cb); list.Add((cb, i.Code));
        }
        return list;
    }

    public void AddNote(string text) => _fields.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Foreground = ThemeManager.Muted });
}
