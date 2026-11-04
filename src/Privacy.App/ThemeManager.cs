using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Privacy.App;

public enum ThemeChoice { System, Light, Dark }

/// <summary>Preferenza di aspetto dell'utente (file %LOCALAPPDATA%\PrivacyStudio\ui.json). Nessun dato personale.</summary>
public static class UiSettings
{
    private static string PathOf => Path.Combine(AppConfig.Dir, "ui.json");

    public static ThemeChoice LoadTheme()
    {
        try
        {
            if (!File.Exists(PathOf)) return ThemeChoice.System;
            using var doc = JsonDocument.Parse(File.ReadAllText(PathOf));
            return doc.RootElement.TryGetProperty("theme", out var t) && Enum.TryParse<ThemeChoice>(t.GetString(), true, out var c) ? c : ThemeChoice.System;
        }
        catch (Exception) { return ThemeChoice.System; }   // file illeggibile: si segue Windows
    }

    public static void SaveTheme(ThemeChoice c)
    {
        try { Directory.CreateDirectory(AppConfig.Dir); File.WriteAllText(PathOf, JsonSerializer.Serialize(new { theme = c.ToString().ToLowerInvariant() })); }
        catch (Exception) { /* la preferenza non è essenziale */ }
    }
}

/// <summary>
/// Tema dell'interfaccia: «Come Windows» (segue chiaro/scuro e colore d'accento del sistema, anche a programma aperto), «Chiaro» o «Scuro».
/// Usa il tema Fluent di WPF; i colori di messaggio (errore, esito positivo, testo secondario) si adattano al tema.
/// </summary>
public static class ThemeManager
{
    public static ThemeChoice Current { get; private set; } = ThemeChoice.System;

    public static void Apply(ThemeChoice choice)
    {
        Current = choice;
        Application.Current.ThemeMode = choice switch { ThemeChoice.Light => ThemeMode.Light, ThemeChoice.Dark => ThemeMode.Dark, _ => ThemeMode.System };
    }

    public static bool IsDark => Current == ThemeChoice.Dark || (Current == ThemeChoice.System && SystemPrefersDark());

    private static bool SystemPrefersDark()
    {
        try { return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int v && v == 0; }
        catch (Exception) { return false; }
    }

    private static SolidColorBrush B(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }

    public static Brush Error => B(IsDark ? "#FF8A80" : "#B3261E");
    public static Brush Success => B(IsDark ? "#7DDC9B" : "#1E7B46");
    public static Brush Muted => B(IsDark ? "#A8B0B8" : "#5B6775");
}
