using Avalonia;
using Avalonia.Media;
using SkiaSharp;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// Konvertiert Avalonia Theme-Farben zu SKColor.
/// Zentraler Zugriff auf Theme-Ressourcen für alle SkiaSharp-Renderer.
/// Muss bei Theme-Wechsel via RefreshColors() aktualisiert werden.
/// </summary>
public static class SkiaThemeHelper
{
    // Primär-/Akzent-Farben
    public static SKColor Primary { get; private set; } = new(0x63, 0x66, 0xF1);
    public static SKColor PrimaryHover { get; private set; } = new(0x81, 0x8C, 0xF8);
    public static SKColor Secondary { get; private set; } = new(0x8B, 0x5C, 0xF6);
    public static SKColor Accent { get; private set; } = new(0x22, 0xD3, 0xEE);

    // Feature-Akzente (ZeitManager etc.)
    public static SKColor TimerAccent { get; private set; } = new(0xF5, 0x9E, 0x0B);
    public static SKColor StopwatchAccent { get; private set; } = new(0x22, 0xD3, 0xEE);
    public static SKColor AlarmAccent { get; private set; } = new(0xA7, 0x8B, 0xFA);
    public static SKColor PomodoroAccent { get; private set; } = new(0xEF, 0x44, 0x44);

    // Hintergrund-Farben
    public static SKColor Background { get; private set; } = new(0x0F, 0x17, 0x2A);
    public static SKColor Surface { get; private set; } = new(0x1E, 0x29, 0x3B);
    public static SKColor Card { get; private set; } = new(0x33, 0x41, 0x55);

    // Text-Farben
    public static SKColor TextPrimary { get; private set; } = new(0xF8, 0xFA, 0xFC);
    public static SKColor TextSecondary { get; private set; } = new(0xCB, 0xD5, 0xE1);
    public static SKColor TextMuted { get; private set; } = new(0x94, 0xA3, 0xB8);

    // Border-Farben
    public static SKColor Border { get; private set; } = new(0x47, 0x55, 0x69);
    public static SKColor BorderSubtle { get; private set; } = new(0x33, 0x41, 0x55);

    // Semantische Farben
    public static SKColor Success { get; private set; } = new(0x22, 0xC5, 0x5E);
    public static SKColor Warning { get; private set; } = new(0xF5, 0x9E, 0x0B);
    public static SKColor Error { get; private set; } = new(0xEF, 0x44, 0x44);
    public static SKColor Info { get; private set; } = new(0x3B, 0x82, 0xF6);

    // Theme-Typ
    public static bool IsDarkTheme { get; private set; } = true;

    /// <summary>
    /// Liest eine Farb-Ressource aus dem aktuellen Theme und gibt sie als SKColor zurück.
    /// </summary>
    public static SKColor GetColor(string resourceKey, SKColor fallback)
    {
        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out var value) == true
            && value is Color color)
        {
            return ToSKColor(color);
        }
        return fallback;
    }

    /// <summary>
    /// Konvertiert eine Avalonia Color zu SKColor.
    /// </summary>
    public static SKColor ToSKColor(Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// Erstellt eine SKColor mit angepasster Transparenz.
    /// </summary>
    public static SKColor WithAlpha(SKColor color, byte alpha)
    {
        return color.WithAlpha(alpha);
    }

    /// <summary>
    /// Mischt zwei Farben linear (0.0 = color1, 1.0 = color2).
    /// </summary>
    public static SKColor Lerp(SKColor color1, SKColor color2, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(color1.Red + (color2.Red - color1.Red) * t),
            (byte)(color1.Green + (color2.Green - color1.Green) * t),
            (byte)(color1.Blue + (color2.Blue - color1.Blue) * t),
            (byte)(color1.Alpha + (color2.Alpha - color1.Alpha) * t));
    }

    /// <summary>
    /// Macht eine Farbe heller (factor > 1) oder dunkler (factor < 1).
    /// </summary>
    public static SKColor AdjustBrightness(SKColor color, float factor)
    {
        return new SKColor(
            (byte)Math.Clamp(color.Red * factor, 0, 255),
            (byte)Math.Clamp(color.Green * factor, 0, 255),
            (byte)Math.Clamp(color.Blue * factor, 0, 255),
            color.Alpha);
    }

    /// <summary>
    /// Aktualisiert alle gecachten Farben aus dem aktuellen Theme.
    /// Muss bei Theme-Wechsel aufgerufen werden (ThemeService.ThemeChanged).
    /// </summary>
    public static void RefreshColors()
    {
        // Primär
        Primary = GetColor("PrimaryColor", new SKColor(0x63, 0x66, 0xF1));
        PrimaryHover = GetColor("PrimaryHoverColor", new SKColor(0x81, 0x8C, 0xF8));
        Secondary = GetColor("SecondaryColor", new SKColor(0x8B, 0x5C, 0xF6));
        Accent = GetColor("AccentColor", new SKColor(0x22, 0xD3, 0xEE));

        // Feature-Akzente
        TimerAccent = GetColor("TimerAccentColor", new SKColor(0xF5, 0x9E, 0x0B));
        StopwatchAccent = GetColor("StopwatchAccentColor", new SKColor(0x22, 0xD3, 0xEE));
        AlarmAccent = GetColor("AlarmAccentColor", new SKColor(0xA7, 0x8B, 0xFA));
        PomodoroAccent = GetColor("PomodoroAccentColor", new SKColor(0xEF, 0x44, 0x44));

        // Hintergrund
        Background = GetColor("BackgroundColor", new SKColor(0x0F, 0x17, 0x2A));
        Surface = GetColor("SurfaceColor", new SKColor(0x1E, 0x29, 0x3B));
        Card = GetColor("CardColor", new SKColor(0x33, 0x41, 0x55));

        // Text
        TextPrimary = GetColor("TextPrimaryColor", new SKColor(0xF8, 0xFA, 0xFC));
        TextSecondary = GetColor("TextSecondaryColor", new SKColor(0xCB, 0xD5, 0xE1));
        TextMuted = GetColor("TextMutedColor", new SKColor(0x94, 0xA3, 0xB8));

        // Border
        Border = GetColor("BorderColor", new SKColor(0x47, 0x55, 0x69));
        BorderSubtle = GetColor("BorderSubtleColor", new SKColor(0x33, 0x41, 0x55));

        // Semantisch
        Success = GetColor("SuccessColor", new SKColor(0x22, 0xC5, 0x5E));
        Warning = GetColor("WarningColor", new SKColor(0xF5, 0x9E, 0x0B));
        Error = GetColor("ErrorColor", new SKColor(0xEF, 0x44, 0x44));
        Info = GetColor("InfoColor", new SKColor(0x3B, 0x82, 0xF6));

        // Theme-Typ (Daylight hat hellen Background)
        IsDarkTheme = Background.Red < 128;
    }
}
