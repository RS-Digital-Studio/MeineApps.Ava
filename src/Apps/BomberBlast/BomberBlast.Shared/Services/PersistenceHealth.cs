using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.Logging;

namespace BomberBlast.Services;

/// <summary>
/// Zentrale Anlaufstelle fuer Persistenz-Korruptions-Events.
///
/// Wenn irgendein Service beim Laden aus Preferences einen JSON-Parse-Fehler hat,
/// ruft er <see cref="ReportCorruption"/> auf. Das setzt einen statischen Flag und
/// loggt den Vorfall, damit:
///
/// 1. CloudSaveService beim naechsten Sync Cloud-Pull BEVORZUGT (statt Local-First),
///    um zu verhindern, dass ein einzelner Parse-Fehler die Cloud mit Leer-State
///    ueberschreibt → Total-Data-Loss auf allen Geraeten.
/// 2. Der Fehler in LogCat / ILogger sichtbar ist (nicht silent).
///
/// Statisch, damit Services ohne DI-Abhaengigkeit auf einen Logger ihre Corruption melden koennen.
/// Logger wird zentral in App.axaml.cs nach DI-Build gesetzt (analog ShaderEffects.Logger).
/// </summary>
public static class PersistenceHealth
{
    /// <summary>Wird zentral in App.axaml.cs nach DI-Build gesetzt.</summary>
    public static ILogger? Logger { get; set; }

    /// <summary>True wenn seit App-Start mindestens eine Corruption erkannt wurde.</summary>
    public static bool WasCorruptionDetected { get; private set; }

    /// <summary>Meldet eine Corruption (JSON-Parse-Fehler beim Laden aus Preferences).</summary>
    /// <param name="serviceName">Name des Services (z.B. "CoinService") fuer Log-Output.</param>
    /// <param name="ex">Die gefangene Exception (kann null sein wenn JSON gar nicht parst).</param>
    public static void ReportCorruption(string serviceName, Exception? ex = null)
    {
        WasCorruptionDetected = true;
        if (ex != null)
            Logger?.LogError(ex, "[PersistenceHealth] Korrupte Daten in {Service} erkannt. Cloud-Pull wird bevorzugt.", serviceName);
        else
            Logger?.LogWarning("[PersistenceHealth] Korrupte/leere Daten in {Service} erkannt. Cloud-Pull wird bevorzugt.", serviceName);
    }

    /// <summary>Nach erfolgreichem Cloud-Pull zuruecksetzen.</summary>
    public static void ClearCorruptionFlag()
    {
        WasCorruptionDetected = false;
    }
}
