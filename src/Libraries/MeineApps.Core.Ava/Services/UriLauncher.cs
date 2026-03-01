using System.Diagnostics;

namespace MeineApps.Core.Ava.Services;

/// <summary>
/// Plattformübergreifender URI-Launcher.
/// Android setzt PlatformOpenUri auf Intent.ActionView,
/// Desktop nutzt Process.Start als Fallback.
/// </summary>
public static class UriLauncher
{
    /// <summary>
    /// Plattform-spezifische Implementierung (wird von Android MainActivity gesetzt)
    /// </summary>
    public static Action<string>? PlatformOpenUri { get; set; }

    /// <summary>
    /// Plattform-spezifische Share-Implementierung (Android: Intent.ActionSend, Desktop: Clipboard)
    /// </summary>
    public static Action<string, string?>? PlatformShareText { get; set; }

    /// <summary>
    /// Teilt einen Text über das native Share-Sheet (Android) oder Clipboard (Desktop).
    /// </summary>
    /// <param name="text">Der zu teilende Text</param>
    /// <param name="title">Optionaler Titel für den Share-Chooser</param>
    public static void ShareText(string text, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (PlatformShareText != null)
        {
            PlatformShareText(text, title);
            return;
        }

        // Desktop-Fallback: In Zwischenablage kopieren
        try
        {
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;
            var clipboard = mainWindow?.Clipboard;
            clipboard?.SetTextAsync(text);
        }
        catch
        {
            // Clipboard nicht verfügbar
        }
    }

    /// <summary>
    /// Öffnet eine URI (URL, mailto:, etc.) im Standard-Handler des Systems
    /// </summary>
    public static void OpenUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return;

        if (PlatformOpenUri != null)
        {
            PlatformOpenUri(uri);
            return;
        }

        // Desktop-Fallback
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
            // Plattform unterstützt kein UseShellExecute
        }
    }
}
