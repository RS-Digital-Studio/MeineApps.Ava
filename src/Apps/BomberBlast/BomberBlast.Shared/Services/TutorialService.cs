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

    private static readonly TutorialStep[] Steps =
    [
        new() { Id = 0, TextKey = "TutorialMove", Type = TutorialStepType.Move, Highlight = TutorialHighlight.InputControl },
        new() { Id = 1, TextKey = "TutorialBomb", Type = TutorialStepType.PlaceBomb, Highlight = TutorialHighlight.BombButton },
        new() { Id = 2, TextKey = "TutorialHide", Type = TutorialStepType.Warning, Highlight = TutorialHighlight.GameField },
        new() { Id = 3, TextKey = "TutorialPowerUp", Type = TutorialStepType.CollectPowerUp, Highlight = TutorialHighlight.PowerUp },
        new() { Id = 4, TextKey = "TutorialDefeatEnemies", Type = TutorialStepType.DefeatEnemies, Highlight = TutorialHighlight.GameField },
        new() { Id = 5, TextKey = "TutorialExit", Type = TutorialStepType.FindExit, Highlight = TutorialHighlight.Exit },
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

    public TutorialService(IPreferencesService preferences)
    {
        _preferences = preferences;
    }

    public void Start()
    {
        if (IsCompleted)
            return;

        _currentStepIndex = 0;
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
