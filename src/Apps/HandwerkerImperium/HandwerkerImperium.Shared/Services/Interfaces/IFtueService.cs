using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// AAA-Audit P0: First-Time-User-Experience-Service. Foundation fuer einen scripted
/// 8-12-Schritt-Tutorial-Flow mit Spotlight-Overlay.
///
/// Aktuell: Daten-Modell + State-Machine + Analytics-Hooks.
/// Folge-Sprint: Avalonia-Spotlight-Overlay-Renderer + AXAML-Integration in MainView.
///
/// Pacing-Vorbild (Egg Inc.): Erste 30s = erste Werkstatt bauen → erstes Geld → erster
/// Worker → erstes Upgrade. Dopamine-Trigger nach jedem Schritt.
/// </summary>
public interface IFtueService
{
    /// <summary>True wenn der FTUE noch laeuft (nicht abgeschlossen + nicht uebersprungen).</summary>
    bool IsActive { get; }

    /// <summary>Aktuell aktiver Step (null wenn FTUE inaktiv).</summary>
    FtueStep? CurrentStep { get; }

    /// <summary>Alle Steps des FTUE (read-only).</summary>
    IReadOnlyList<FtueStep> AllSteps { get; }

    /// <summary>Startet den FTUE. Idempotent — wenn bereits abgeschlossen, no-op.</summary>
    void Start();

    /// <summary>Schliesst den aktuellen Schritt ab und schaltet zum naechsten weiter.</summary>
    void CompleteCurrentStep();

    /// <summary>Triggert Schritt-Completion via erwartete Aktion (z.B. „BuyFirstUpgrade").</summary>
    void OnPlayerAction(FtueExpectedAction action);

    /// <summary>Bricht FTUE komplett ab (Spieler waehlt „Skip Tutorial"). Persistent.</summary>
    void SkipAll();

    /// <summary>Event: Step hat gewechselt — UI sollte Spotlight-Overlay aktualisieren.</summary>
    event EventHandler<FtueStep?>? CurrentStepChanged;

    /// <summary>Event: FTUE komplett fertig oder uebersprungen.</summary>
    event EventHandler? FtueFinished;
}
