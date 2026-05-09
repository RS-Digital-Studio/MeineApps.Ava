namespace HandwerkerImperium.Models;

/// <summary>
/// AAA-Audit P0 (FTUE-System): Definition eines einzelnen Tutorial-Schritts.
///
/// Inspiration: Supercell/Habby haben 30+ FTUE-Schritte mit Hand-Animation, Spotlight-Overlay,
/// Cooldown-Skip-Block und Dopamine-Trigger nach jedem Click. Dieses Modell bildet die
/// Grundlage fuer einen vergleichbaren First-Run-Flow.
///
/// Aktuell als Foundation: Daten-Modell + Service-Skelett. Volle Spotlight-Overlay-
/// Implementierung ist in einem Folge-Sprint angesiedelt (laut Audit ~2-3 Wochen).
/// </summary>
public sealed class FtueStep
{
    /// <summary>Stabile ID (z.B. „ftue_first_workshop", „ftue_first_order"). Wird persistiert.</summary>
    public string Id { get; init; } = "";

    /// <summary>Reihenfolge-Index. Schritte werden aufsteigend ausgespielt.</summary>
    public int Order { get; init; }

    /// <summary>RESX-Key fuer den Titel-Text der Spotlight-Bubble.</summary>
    public string TitleKey { get; init; } = "";

    /// <summary>RESX-Key fuer den Inhalts-Text.</summary>
    public string TextKey { get; init; } = "";

    /// <summary>
    /// Optionale Spotlight-Target-ID. Wenn gesetzt, sollte das UI ein Spotlight-Overlay
    /// auf das Element mit dieser AutomationId rendern (z.B. „Dashboard_Btn_FirstUpgrade").
    /// </summary>
    public string? SpotlightAutomationId { get; init; }

    /// <summary>
    /// Erwartete Spieler-Aktion zum Fortschreiten (z.B. Tap auf Spotlight-Target,
    /// Auftrag annehmen, Worker einstellen). Fuer Telemetrie und Skip-Detection.
    /// </summary>
    public FtueExpectedAction ExpectedAction { get; init; } = FtueExpectedAction.TapAnywhere;

    /// <summary>
    /// True wenn der Spieler diesen Schritt skippen darf. Onboarding-kritische Schritte
    /// (z.B. erstes Workshop-Upgrade) sollten nicht skippbar sein.
    /// </summary>
    public bool CanSkip { get; init; } = true;
}

/// <summary>Erwartete Spieler-Aktion zum Fortschritt eines FTUE-Schritts.</summary>
public enum FtueExpectedAction
{
    /// <summary>Spieler tippt irgendwohin (Default).</summary>
    TapAnywhere,
    /// <summary>Spieler tippt auf das Spotlight-Target.</summary>
    TapSpotlight,
    /// <summary>Spieler kauft erstes Workshop-Upgrade.</summary>
    BuyFirstUpgrade,
    /// <summary>Spieler nimmt einen Auftrag an.</summary>
    AcceptFirstOrder,
    /// <summary>Spieler stellt einen Worker ein.</summary>
    HireFirstWorker,
    /// <summary>Spieler schliesst seinen ersten MiniGame-Auftrag ab.</summary>
    CompleteFirstMiniGame,
    /// <summary>Spieler erreicht Spieler-Level 2.</summary>
    ReachLevel2,
    /// <summary>Spieler tippt explizit „Weiter".</summary>
    TapContinue,
}

/// <summary>
/// AAA-Audit P0: Persistenter FTUE-State. Lebt im GameState.Tutorial.Ftue (V7-Migration).
/// </summary>
public sealed class FtueState
{
    /// <summary>Aktuell aktiver Step-Index (0-basiert). -1 wenn FTUE noch nicht gestartet.</summary>
    public int CurrentStepIndex { get; set; } = -1;

    /// <summary>True wenn FTUE komplett abgeschlossen.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>True wenn der Spieler das gesamte FTUE manuell abgebrochen hat.</summary>
    public bool WasSkipped { get; set; }

    /// <summary>IDs der bereits absolvierten Steps (idempotent fuer Replay-Safety).</summary>
    public HashSet<string> CompletedStepIds { get; set; } = [];

    /// <summary>Wann FTUE gestartet wurde (UTC, ISO 8601).</summary>
    public string? StartedAtIso { get; set; }

    /// <summary>Wann FTUE abgeschlossen wurde (UTC, ISO 8601).</summary>
    public string? CompletedAtIso { get; set; }
}
