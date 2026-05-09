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
    /// Plattform-spezifische File-Share-Implementierung (filePath, mimeType, title).
    /// Android: Intent.ActionSend via FileProvider. Desktop: öffnet Explorer am Datei-Ort als Fallback.
    /// </summary>
    public static Action<string, string, string?>? PlatformShareFile { get; set; }

    /// <summary>
    /// Plattform-spezifisches Öffnen einer Datei mit dem Standard-Handler (filePath, mimeType).
    /// Android: Intent.ActionView via FileProvider. Desktop: Process.Start.
    /// </summary>
    public static Action<string, string>? PlatformOpenFile { get; set; }

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
            if (clipboard != null)
            {
                // Avalonia 12 Clipboard-API: DataTransfer + DataTransferItem
                var data = new Avalonia.Input.DataTransfer();
                data.Add(Avalonia.Input.DataTransferItem.CreateText(text));
                _ = clipboard.SetDataAsync(data);
            }
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

    /// <summary>Teilt eine lokale Datei über das native Share-Sheet (Android) oder öffnet den
    /// Explorer am Datei-Ort (Desktop-Fallback).</summary>
    public static void ShareFile(string filePath, string mimeType, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        if (PlatformShareFile != null)
        {
            PlatformShareFile(filePath, mimeType, title);
            return;
        }

        // Desktop-Fallback: Explorer am Datei-Ort öffnen
        try
        {
            var dir = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch
        {
            // Plattform unterstützt kein UseShellExecute
        }
    }

    /// <summary>Öffnet eine lokale Datei mit dem Standard-Handler des Systems.</summary>
    public static void OpenFile(string filePath, string mimeType = "*/*")
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        if (PlatformOpenFile != null)
        {
            PlatformOpenFile(filePath, mimeType);
            return;
        }

        // Desktop-Fallback: Datei mit Standard-Programm öffnen
        try
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch
        {
            // Plattform unterstützt kein UseShellExecute
        }
    }
}
