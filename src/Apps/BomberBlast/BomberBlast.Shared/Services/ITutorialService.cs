using BomberBlast.Models;

namespace BomberBlast.Services;

/// <summary>
/// Service für das interaktive Tutorial beim ersten Spielstart
/// </summary>
public interface ITutorialService
{
    /// <summary>Ob das Tutorial aktiv ist</summary>
    bool IsActive { get; }

    /// <summary>Aktueller Schritt (null wenn nicht aktiv)</summary>
    TutorialStep? CurrentStep { get; }

    /// <summary>Ob das Tutorial bereits abgeschlossen wurde</summary>
    bool IsCompleted { get; }

    /// <summary>Tutorial starten (nur wenn noch nicht abgeschlossen)</summary>
    void Start();

    /// <summary>Zum nächsten Schritt wechseln</summary>
    void NextStep();

    /// <summary>Tutorial überspringen und als abgeschlossen markieren</summary>
    void Skip();

    /// <summary>Prüft ob eine bestimmte Aktion den aktuellen Schritt abschließt</summary>
    bool CheckStepCompletion(TutorialStepType actionType);

    /// <summary>Tutorial-Fortschritt zurücksetzen</summary>
    void Reset();
}
