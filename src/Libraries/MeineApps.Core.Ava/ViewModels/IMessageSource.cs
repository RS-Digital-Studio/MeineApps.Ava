using System;

namespace MeineApps.Core.Ava.ViewModels;

/// <summary>
/// Marker-Interface für ViewModels die User-Messages an die UI weiterreichen
/// (typisch: Fehler-/Info-Dialoge). Ersetzt Reflection-basiertes Event-Wiring
/// im MainViewModel.
/// </summary>
public interface IMessageSource
{
    /// <summary>
    /// Wird gefeuert wenn das VM eine Nachricht anzeigen will.
    /// Argument 1 = Titel, Argument 2 = Nachricht.
    /// </summary>
    event Action<string, string>? MessageRequested;
}
