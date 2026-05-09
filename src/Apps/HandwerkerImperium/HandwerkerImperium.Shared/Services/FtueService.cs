using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// AAA-Audit P0: FTUE-Service-Skelett. State-Machine + Analytics-Hooks. Spotlight-Overlay-
/// Renderer + UI-Integration sind bewusst separat (Folge-Sprint).
///
/// Pattern:
/// - 10-Schritt-Default-Sequenz (Welcome → Erstes Upgrade → Erster Worker → Erster Auftrag → ...)
/// - State persistiert im GameState.Tutorial.Ftue (V7-Migration optional, default-Init reicht).
/// - Idempotent: Schritte sind ueber stabile IDs deduped.
/// - Telemetrie: Jeder Step-Completion + Skip wird als Analytics-Event gefeuert.
/// </summary>
public sealed class FtueService : IFtueService
{
    private readonly IGameStateService _gameStateService;
    private readonly IAnalyticsService? _analytics;

    /// <summary>Default-FTUE-Sequenz (10 Steps). Reihenfolge stabil.</summary>
    private static readonly FtueStep[] s_defaultSteps =
    [
        new() { Id = "ftue_welcome",          Order = 0, TitleKey = "FtueWelcomeTitle",          TextKey = "FtueWelcomeText",          ExpectedAction = FtueExpectedAction.TapContinue,           CanSkip = false },
        new() { Id = "ftue_first_upgrade",    Order = 1, TitleKey = "FtueFirstUpgradeTitle",     TextKey = "FtueFirstUpgradeText",     ExpectedAction = FtueExpectedAction.BuyFirstUpgrade,        SpotlightAutomationId = "Dashboard_Btn_WorkshopUpgrade", CanSkip = false },
        new() { Id = "ftue_first_order",      Order = 2, TitleKey = "FtueFirstOrderTitle",       TextKey = "FtueFirstOrderText",       ExpectedAction = FtueExpectedAction.AcceptFirstOrder,       SpotlightAutomationId = "Dashboard_Btn_AcceptOrder",     CanSkip = false },
        new() { Id = "ftue_first_minigame",   Order = 3, TitleKey = "FtueFirstMiniGameTitle",    TextKey = "FtueFirstMiniGameText",    ExpectedAction = FtueExpectedAction.CompleteFirstMiniGame,                                                          CanSkip = false },
        new() { Id = "ftue_money_explained",  Order = 4, TitleKey = "FtueMoneyExplainedTitle",   TextKey = "FtueMoneyExplainedText",   ExpectedAction = FtueExpectedAction.TapContinue,           SpotlightAutomationId = "Header_Txt_Money" },
        new() { Id = "ftue_first_worker",     Order = 5, TitleKey = "FtueFirstWorkerTitle",      TextKey = "FtueFirstWorkerText",      ExpectedAction = FtueExpectedAction.HireFirstWorker,        SpotlightAutomationId = "Dashboard_Btn_HireWorker" },
        new() { Id = "ftue_xp_explained",     Order = 6, TitleKey = "FtueXpExplainedTitle",      TextKey = "FtueXpExplainedText",      ExpectedAction = FtueExpectedAction.ReachLevel2,            SpotlightAutomationId = "Header_Txt_Level" },
        new() { Id = "ftue_screws_explained", Order = 7, TitleKey = "FtueScrewsExplainedTitle",  TextKey = "FtueScrewsExplainedText",  ExpectedAction = FtueExpectedAction.TapContinue,           SpotlightAutomationId = "Header_Txt_GoldenScrews" },
        new() { Id = "ftue_imperium_intro",   Order = 8, TitleKey = "FtueImperiumIntroTitle",    TextKey = "FtueImperiumIntroText",    ExpectedAction = FtueExpectedAction.TapContinue,           SpotlightAutomationId = "TabBar_Btn_Imperium" },
        new() { Id = "ftue_complete",         Order = 9, TitleKey = "FtueCompleteTitle",         TextKey = "FtueCompleteText",         ExpectedAction = FtueExpectedAction.TapContinue },
    ];

    public IReadOnlyList<FtueStep> AllSteps => s_defaultSteps;

    public bool IsActive
    {
        get
        {
            var ftue = _gameStateService.State.Tutorial.Ftue;
            return ftue.CurrentStepIndex >= 0
                && !ftue.IsCompleted
                && !ftue.WasSkipped;
        }
    }

    public FtueStep? CurrentStep
    {
        get
        {
            if (!IsActive) return null;
            var idx = _gameStateService.State.Tutorial.Ftue.CurrentStepIndex;
            if (idx < 0 || idx >= s_defaultSteps.Length) return null;
            return s_defaultSteps[idx];
        }
    }

    public event EventHandler<FtueStep?>? CurrentStepChanged;
    public event EventHandler? FtueFinished;

    public FtueService(IGameStateService gameStateService, IAnalyticsService? analytics = null)
    {
        _gameStateService = gameStateService;
        _analytics = analytics;
    }

    public void Start()
    {
        var ftue = _gameStateService.State.Tutorial.Ftue;
        if (ftue.IsCompleted || ftue.WasSkipped) return;
        if (ftue.CurrentStepIndex >= 0) return; // Bereits gestartet

        ftue.CurrentStepIndex = 0;
        ftue.StartedAtIso = DateTime.UtcNow.ToString("O");
        TrackStepEvent("ftue_started", s_defaultSteps[0]);
        CurrentStepChanged?.Invoke(this, s_defaultSteps[0]);
    }

    public void CompleteCurrentStep()
    {
        var step = CurrentStep;
        if (step == null) return;

        var ftue = _gameStateService.State.Tutorial.Ftue;
        ftue.CompletedStepIds.Add(step.Id);
        TrackStepEvent("ftue_step_completed", step);

        var nextIdx = ftue.CurrentStepIndex + 1;
        if (nextIdx >= s_defaultSteps.Length)
        {
            ftue.IsCompleted = true;
            ftue.CompletedAtIso = DateTime.UtcNow.ToString("O");
            ftue.CurrentStepIndex = -1;
            CurrentStepChanged?.Invoke(this, null);
            FtueFinished?.Invoke(this, EventArgs.Empty);
            _analytics?.TrackEvent("ftue_completed", new Dictionary<string, object?>
            {
                ["steps_completed"] = ftue.CompletedStepIds.Count,
            });
            return;
        }

        ftue.CurrentStepIndex = nextIdx;
        CurrentStepChanged?.Invoke(this, s_defaultSteps[nextIdx]);
    }

    public void OnPlayerAction(FtueExpectedAction action)
    {
        var step = CurrentStep;
        if (step == null) return;
        if (step.ExpectedAction != action) return;

        // Idempotenz: Wenn bereits completed, ignorieren (z.B. doppeltes Event vom Service).
        if (_gameStateService.State.Tutorial.Ftue.CompletedStepIds.Contains(step.Id)) return;

        CompleteCurrentStep();
    }

    public void SkipAll()
    {
        var ftue = _gameStateService.State.Tutorial.Ftue;
        if (ftue.IsCompleted || ftue.WasSkipped) return;

        ftue.WasSkipped = true;
        ftue.CompletedAtIso = DateTime.UtcNow.ToString("O");
        ftue.CurrentStepIndex = -1;

        _analytics?.TrackEvent("ftue_skipped", new Dictionary<string, object?>
        {
            ["last_step_index"] = ftue.CurrentStepIndex,
            ["steps_completed"] = ftue.CompletedStepIds.Count,
        });

        CurrentStepChanged?.Invoke(this, null);
        FtueFinished?.Invoke(this, EventArgs.Empty);
    }

    private void TrackStepEvent(string eventName, FtueStep step)
    {
        _analytics?.TrackEvent(eventName, new Dictionary<string, object?>
        {
            ["step_id"] = step.Id,
            ["step_order"] = step.Order,
        });
    }
}
