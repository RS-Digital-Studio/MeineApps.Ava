using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Kontextuelles Tutorial: Hints erscheinen beim ersten Benutzen eines Features.
/// Tracking via GameState.SeenHints (HashSet, JSON-persistiert).
/// </summary>
public sealed class ContextualHintService : IContextualHintService
{
    private readonly IGameStateService _gameStateService;

    public event EventHandler<ContextualHint?>? HintChanged;

    public ContextualHint? ActiveHint { get; private set; }

    public ContextualHintService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
    }

    public bool TryShowHint(ContextualHint hint)
    {
        // Nicht anzeigen wenn bereits gesehen
        if (HasSeenHint(hint.Id)) return false;

        // Nicht anzeigen wenn ein anderer Hint gerade aktiv ist
        if (ActiveHint != null) return false;

        ActiveHint = hint;
        HintChanged?.Invoke(this, hint);
        return true;
    }

    public void DismissHint()
    {
        if (ActiveHint == null) return;

        // Als gesehen markieren
        _gameStateService.State.SeenHints.Add(ActiveHint.Id);
        _gameStateService.MarkDirty();

        ActiveHint = null;
        HintChanged?.Invoke(this, null);
    }

    public bool HasSeenHint(string hintId)
    {
        return _gameStateService.State.SeenHints.Contains(hintId);
    }

    public void ResetAllHints()
    {
        _gameStateService.State.SeenHints.Clear();
        _gameStateService.State.SeenMiniGameTutorials.Clear();
        _gameStateService.State.HasSeenTutorialHint = false;
        _gameStateService.MarkDirty();
    }
}
