using System;

namespace MeineApps.Core.Ava.ViewModels;

/// <summary>
/// Marker-Interface für ViewModels die Navigations-Routen anfragen.
/// Ersetzt Reflection-basiertes Event-Wiring im MainViewModel
/// (statt <c>GetEvent("NavigationRequested")</c> direkt <c>is INavigationSource</c>).
/// </summary>
/// <remarks>
/// Route-Format identisch zum bisherigen <c>Action&lt;string&gt;</c>-Pattern:
/// <list type="bullet">
/// <item><c>"DayDetailPage?date=2026-02-13"</c> — Sub-Page mit Parametern</item>
/// <item><c>".."</c> — zurück zum Parent</item>
/// <item><c>"../subpage"</c> — zum Parent, dann zu subpage</item>
/// </list>
/// </remarks>
public interface INavigationSource
{
    /// <summary>
    /// Wird gefeuert wenn das VM eine Navigation anfordert.
    /// </summary>
    event Action<string>? NavigationRequested;
}
