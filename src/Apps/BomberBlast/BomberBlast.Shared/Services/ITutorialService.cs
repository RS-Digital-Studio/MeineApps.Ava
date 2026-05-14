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

    /// <summary>Ob das gesamte Tutorial (alle 3 Phasen) abgeschlossen wurde</summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Sprint 3.2 AAA-Audit #5: Aktuell laufende Phase (T1 Movement / T2 Bombs / T3 PowerUps).
    /// Bei inaktivem Tutorial: letzte erreichte bzw. Movement als Default.
    /// </summary>
    TutorialPhase CurrentPhase { get; }

    /// <summary>
    /// Sprint 3.2 AAA-Audit #5: Ob eine einzelne Tutorial-Phase abgeschlossen ist.
    /// Die 3 Phasen sind "geschuetzte Tutorial-Levels" — granulare Persistenz erlaubt
    /// Resume bei der naechsten offenen Phase statt Neustart bei Movement.
    /// </summary>
    bool IsPhaseCompleted(TutorialPhase phase);

    /// <summary>
    /// Sprint 3.2 AAA-Audit #5: Soft-Onboarding-Curve. Nach Tutorial-Abschluss liefern
    /// die ersten 2 Story-Level reduzierte Schwierigkeit. Jeder Aufruf verbraucht einen
    /// Soft-Onboarding-Level (dekrementiert den persistierten Counter) und gibt true
    /// zurueck solange noch welche uebrig sind. GameEngine ruft das bei Level-Start.
    /// </summary>
    bool ConsumeSoftOnboardingLevel();

    /// <summary>
    /// Tutorial starten — resumed bei der ersten noch nicht abgeschlossenen Phase
    /// (nicht zwingend bei Movement). Bei vollstaendig abgeschlossenem Tutorial: No-Op.
    /// </summary>
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
