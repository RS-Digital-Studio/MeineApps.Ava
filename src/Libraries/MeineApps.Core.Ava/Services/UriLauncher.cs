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
