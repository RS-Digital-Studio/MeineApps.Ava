using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Tutorial-Service: 6 interaktive Schritte beim ersten Spielstart.
/// Persistiert den Abschluss-Status via IPreferencesService.
/// </summary>
public sealed class TutorialService : ITutorialService
{
    private const string TUTORIAL_COMPLETED_KEY = "TutorialCompleted";
    private readonly IPreferencesService _preferences;

    /// <summary>
    /// Sprint 3.2 AAA-Audit #5: Schritte sind in 3 Phasen gruppiert.
    /// IsFirstOfPhase=true loest beim Tutorial-Overlay einen Phase-Banner aus
    /// ("Phase 1: Bewegung" / "Phase 2: Bomben" / "Phase 3: Power-Ups").
    /// Soft-Onboarding-Curve: Erst Movement, dann Bomb-Risiken, dann komplexere Mechaniken.
    /// </summary>
    private static readonly TutorialStep[] Steps =
    [
        // Phase 1 — Movement (Genre-Neulinge): nur DPad, kein Bomben-Risiko
        new() { Id = 0, TextKey = "TutorialMove", Type = TutorialStepType.Move,
                Highlight = TutorialHighlight.InputControl,
                Phase = TutorialPhase.Movement, IsFirstOfPhase = true },
        // Phase 2 — Bomb-Mechanik: Bombe legen + sicheres Verstecken
        new() { Id = 1, TextKey = "TutorialBomb", Type = TutorialStepType.PlaceBomb,
                Highlight = TutorialHighlight.BombButton,
                Phase = TutorialPhase.Bombs, IsFirstOfPhase = true },
        new() { Id = 2, TextKey = "TutorialHide", Type = TutorialStepType.Warning,
                Highlight = TutorialHighlight.GameField,
                Phase = TutorialPhase.Bombs },
        // Phase 3 — Power-Ups + Combat (Tiefe der Mechanik)
        new() { Id = 3, TextKey = "TutorialPowerUp", Type = TutorialStepType.CollectPowerUp,
                Highlight = TutorialHighlight.PowerUp,
                Phase = TutorialPhase.PowerUps, IsFirstOfPhase = true },
        new() { Id = 4, TextKey = "TutorialDefeatEnemies", Type = TutorialStepType.DefeatEnemies,
                Highlight = TutorialHighlight.GameField,
                Phase = TutorialPhase.PowerUps },
        new() { Id = 5, TextKey = "TutorialExit", Type = TutorialStepType.FindExit,
                Highlight = TutorialHighlight.Exit,
                Phase = TutorialPhase.PowerUps },
    ];

    private int _currentStepIndex = -1;

    public bool IsActive => _currentStepIndex >= 0 && _currentStepIndex < Steps.Length;
    public TutorialStep? CurrentStep => IsActive ? Steps[_currentStepIndex] : null;
    public bool IsCompleted => _preferences.Get(TUTORIAL_COMPLETED_KEY, false);

    /// <summary>
    /// Sprint 2.2 AAA-Audit #2: Wird vom GameEngine subscribed, feuert Funnel-Event
    /// fuer jeden abgeschlossenen Tutorial-Schritt + ein Final-Complete-Event.
    /// </summary>
    public event Action<int>? StepCompleted;
    /// <summary>Wird beim Erreichen des letzten Schritts gefeuert.</summary>
    public event Action? TutorialCompleted;

    /// <summary>
    /// Sprint 3.2 AAA-Audit #5: Wird gefeuert wenn ein neuer Tutorial-Phase-Schritt aktiv wird
    /// (T1 Movement / T2 Bombs / T3 PowerUps). Tutorial-Overlay zeigt dann einen
    /// Phase-Banner ("Phase 2: Bomb-Mechanik") fuer 1.5s.
    /// </summary>
    public event Action<TutorialPhase>? PhaseChanged;

    public TutorialService(IPreferencesService preferences)
    {
        _preferences = preferences;
    }

    public void Start()
    {
        if (IsCompleted)
            return;

        _currentStepIndex = 0;
        // Sprint 3.2 AAA-Audit #5: Erste Phase ankuendigen (Phase 1 Movement)
        if (Steps[0].IsFirstOfPhase)
            PhaseChanged?.Invoke(Steps[0].Phase);
    }

    public void NextStep()
    {
        if (!IsActive)
            return;

        // Sprint 2.2: Step-Index VOR dem Inkrement merken (das war der gerade abgeschlossene Schritt).
        int completedStepIndex = _currentStepIndex;
        _currentStepIndex++;

        StepCompleted?.Invoke(completedStepIndex);

        if (_currentStepIndex >= Steps.Length)
        {
            // Tutorial abgeschlossen
            _currentStepIndex = -1;
            _preferences.Set(TUTORIAL_COMPLETED_KEY, true);
            TutorialCompleted?.Invoke();
        }
        else
        {
            // Sprint 3.2 AAA-Audit #5: Phase-Wechsel ankuendigen wenn der neue Step
            // der erste seiner Phase ist (T1 → T2 → T3).
            var newStep = Steps[_currentStepIndex];
            if (newStep.IsFirstOfPhase)
                PhaseChanged?.Invoke(newStep.Phase);
        }
    }

    public void Skip()
    {
        _currentStepIndex = -1;
        _preferences.Set(TUTORIAL_COMPLETED_KEY, true);
    }

    public bool CheckStepCompletion(TutorialStepType actionType)
    {
        if (!IsActive || CurrentStep == null)
            return false;

        if (CurrentStep.Type == actionType)
        {
            NextStep();
            return true;
        }

        return false;
    }

    public void Reset()
    {
        _currentStepIndex = -1;
        _preferences.Set(TUTORIAL_COMPLETED_KEY, false);
    }
}
