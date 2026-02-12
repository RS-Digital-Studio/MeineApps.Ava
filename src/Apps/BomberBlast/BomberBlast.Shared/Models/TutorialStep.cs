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
