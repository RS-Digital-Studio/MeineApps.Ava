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

    /// <summary>
    /// Sprint 2.2 AAA-Audit #2: Wird beim Abschluss eines Tutorial-Schritts gefeuert
    /// (Parameter = Index des gerade abgeschlossenen Schritts). Wird vom GameEngine
    /// fuer Funnel-Telemetrie subscribed.
    /// </summary>
    event Action<int>? StepCompleted;

    /// <summary>Wird beim Erreichen des letzten Tutorial-Schritts gefeuert.</summary>
    event Action? TutorialCompleted;

    /// <summary>
    /// Sprint 3.2 AAA-Audit #5: Wird gefeuert wenn das Tutorial in eine neue Phase
    /// (T1 Movement / T2 Bombs / T3 PowerUps) wechselt. Tutorial-Overlay zeigt
    /// einen Phase-Banner ("Phase 2: Bomben") fuer 1.5s als visuelle Trennung.
    /// </summary>
    event Action<TutorialPhase>? PhaseChanged;
}
