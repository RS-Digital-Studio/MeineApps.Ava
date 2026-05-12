namespace BomberBlast.Models;

/// <summary>
/// Ein einzelner Tutorial-Schritt
/// </summary>
public class TutorialStep
{
    /// <summary>Eindeutige ID des Schritts</summary>
    public int Id { get; init; }

    /// <summary>RESX-Key für den Anweisungstext</summary>
    public string TextKey { get; init; } = "";

    /// <summary>Art des Tutorial-Schritts</summary>
    public TutorialStepType Type { get; init; }

    /// <summary>Bereich den der Pfeil/Highlight zeigen soll</summary>
    public TutorialHighlight Highlight { get; init; }

    /// <summary>
    /// Sprint 3.2 AAA-Audit #5: Phase-Zuordnung (T1 Movement / T2 Bombs / T3 PowerUps).
    /// Wird vor jedem ersten Schritt einer neuen Phase als Phase-Banner angezeigt.
    /// </summary>
    public TutorialPhase Phase { get; init; }

    /// <summary>True wenn dieser Schritt der ERSTE seiner Phase ist (loest Phase-Banner aus).</summary>
    public bool IsFirstOfPhase { get; init; }
}

/// <summary>
/// Sprint 3.2 AAA-Audit #5: Tutorial-Phase ("3 Tutorial-Levels" laut Audit).
/// Statt 3 separater Maps integriert in das bestehende 6-Schritte-Tutorial-System
/// als logische Phasen-Gruppierung mit Phase-Banner-Anzeige.
/// </summary>
public enum TutorialPhase
{
    /// <summary>T1 Movement: D-Pad-Bewegung, Coins sammeln. Keine Bomben.</summary>
    Movement = 0,
    /// <summary>T2 Bomb-Mechanik: Bombe legen, sicheres Verstecken, Block-Zerstoerung.</summary>
    Bombs = 1,
    /// <summary>T3 Power-Ups: Pickup von Fire/Speed/Bomb, Boss-Lite-Encounter.</summary>
    PowerUps = 2,
}

/// <summary>
/// Typ des Tutorial-Schritts (bestimmt wann er als abgeschlossen gilt)
/// </summary>
public enum TutorialStepType
{
    /// <summary>Spieler muss sich bewegen</summary>
    Move,
    /// <summary>Spieler muss Bombe legen</summary>
    PlaceBomb,
    /// <summary>Warnung nach Bombenlegung (Auto-Weiter nach Verzögerung)</summary>
    Warning,
    /// <summary>Spieler muss PowerUp einsammeln</summary>
    CollectPowerUp,
    /// <summary>Spieler muss alle Gegner besiegen</summary>
    DefeatEnemies,
    /// <summary>Spieler muss zum Exit gehen</summary>
    FindExit
}

/// <summary>
/// Bereich der im Tutorial hervorgehoben wird
/// </summary>
public enum TutorialHighlight
{
    /// <summary>D-Pad / Joystick Bereich (links unten)</summary>
    InputControl,
    /// <summary>Bomb-Button (rechts unten)</summary>
    BombButton,
    /// <summary>Gesamtes Spielfeld</summary>
    GameField,
    /// <summary>PowerUp (dynamisch)</summary>
    PowerUp,
    /// <summary>Exit-Zelle (dynamisch)</summary>
    Exit
}
