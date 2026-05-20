using System;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Services;

/// <summary>
/// Progression-Feedback: reagiert auf Level-Up, Prestige, Workshop-Upgrade, Worker-Events,
/// Master-Tool-Unlocks und Achievements mit FloatingText/Celebration/Zeremonie/Sound/Hints.
/// Aus MainViewModel.EventHandlers.cs extrahiert — subscribed selbst auf die Service-Events
/// (analog CinematicCoordinator) und delegiert die wenigen MainViewModel-Zugriffe an
/// <see cref="IProgressionFeedbackHost"/>. Singleton im DI.
/// </summary>
public sealed class ProgressionFeedbackCoordinator : IProgressionFeedbackCoordinator, IDisposable
{
    private readonly HeaderViewModel _headerVm;
    private readonly DialogViewModel _dialogVm;
    private readonly MissionsFeatureViewModel _missionsVm;
    private readonly IUiEffectBus _uiEffectBus;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IGameStateService _gameStateService;
    private readonly IGameLoopService _gameLoopService;
    private readonly IAchievementService _achievementService;
    private readonly IWorkerService _workerService;
    private readonly IPrestigeService _prestigeService;
    private readonly IGoalService _goalService;
    private readonly IContextualHintService _contextualHintService;
    private readonly IRebirthService? _rebirthService;
    private readonly IReviewService? _reviewService;
    private readonly IPlayGamesService? _playGamesService;
    private IProgressionFeedbackHost? _host;
    private bool _started;
    private bool _disposed;

    // Wiederverwendbarer Timer für Level-Up Pulse (verhindert Timer-Leak bei rapidem Level-Up).
    private DispatcherTimer? _levelPulseTimer;

    // Milestone-Level mit Goldschrauben-Belohnung (Spieler-Level).
    private static readonly (int level, int screws)[] s_levelMilestones =
    [
        (10, 3), (25, 5), (50, 10), (100, 20), (250, 50), (500, 100), (1000, 200)
    ];

    // Workshop-Level-Meilensteine.
    private static readonly (int level, int screws)[] s_workshopMilestones =
        [(50, 2), (100, 5), (250, 10), (500, 25), (1000, 50)];

    public ProgressionFeedbackCoordinator(
        HeaderViewModel headerVm,
        DialogViewModel dialogVm,
        MissionsFeatureViewModel missionsVm,
        IUiEffectBus uiEffectBus,
        IAudioService audioService,
        ILocalizationService localizationService,
        IGameStateService gameStateService,
        IGameLoopService gameLoopService,
        IAchievementService achievementService,
        IWorkerService workerService,
        IPrestigeService prestigeService,
        IGoalService goalService,
        IContextualHintService contextualHintService,
        IRebirthService? rebirthService = null,
        IReviewService? reviewService = null,
        IPlayGamesService? playGamesService = null)
    {
        _headerVm = headerVm;
        _dialogVm = dialogVm;
        _missionsVm = missionsVm;
        _uiEffectBus = uiEffectBus;
        _audioService = audioService;
        _localizationService = localizationService;
        _gameStateService = gameStateService;
        _gameLoopService = gameLoopService;
        _achievementService = achievementService;
        _workerService = workerService;
        _prestigeService = prestigeService;
        _goalService = goalService;
        _contextualHintService = contextualHintService;
        _rebirthService = rebirthService;
        _reviewService = reviewService;
        _playGamesService = playGamesService;
    }

    public void AttachHost(IProgressionFeedbackHost host) => _host = host;

    /// <summary>Aktiviert die Event-Subscriptions. Idempotent — mehrfacher Aufruf ist sicher.</summary>
    public void StartListening()
    {
        if (_started) return;
        _started = true;
        _gameStateService.GoldenScrewsChanged += OnGoldenScrewsChanged;
        _gameStateService.LevelUp += OnLevelUp;
        _gameStateService.XpGained += OnXpGained;
        _gameStateService.WorkshopUpgraded += OnWorkshopUpgraded;
        _gameStateService.WorkerHired += OnWorkerHired;
        _gameLoopService.MasterToolUnlocked += OnMasterToolUnlocked;
        _achievementService.AchievementUnlocked += OnAchievementUnlocked;
        _prestigeService.PrestigeCompleted += OnPrestigeCompleted;
        _prestigeService.MilestoneReached += OnPrestigeMilestoneReached;
        _workerService.WorkerLevelUp += OnWorkerLevelUp;
        _workerService.InternReadyForPromotion += OnInternReadyForPromotion;
        if (_rebirthService != null)
            _rebirthService.RebirthCompleted += OnRebirthCompleted;
    }

    private bool HostIsHoldingUpgrade => _host?.IsHoldingUpgrade ?? false;
    private bool HostIsAnyDialogVisible => _host?.IsAnyDialogVisible ?? false;

    private void CheckForNewStoryChapter()
        => _dialogVm.CheckForNewStoryChapter(HostIsAnyDialogVisible, HostIsHoldingUpgrade);

    // ═══════════════════════════════════════════════════════════════════════
    // GOLDSCHRAUBEN
    // ═══════════════════════════════════════════════════════════════════════

    private void OnGoldenScrewsChanged(object? sender, GoldenScrewsChangedEventArgs e)
    {
        _headerVm.GoldenScrewsDisplay = e.NewAmount.ToString("N0");

        // Goldschrauben-Erklärung beim allerersten Erhalt
        if (e.OldAmount == 0 && e.NewAmount > 0)
            _contextualHintService.TryShowHint(ContextualHints.GoldenScrews);

        // PP-3: FloatingText bei Goldschrauben-Ausgaben
        int diff = e.NewAmount - e.OldAmount;
        if (diff < 0)
            _uiEffectBus.RaiseFloatingText($"{diff} GS", "warning");
        else if (diff > 0)
            _uiEffectBus.RaiseFloatingText($"+{diff} GS", "goldscrews");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LEVEL-UP
    // ═══════════════════════════════════════════════════════════════════════

    private void OnLevelUp(object? sender, LevelUpEventArgs e)
    {
        _headerVm.PlayerLevel = e.NewLevel;
        _headerVm.LevelProgress = _gameStateService.State.LevelProgress;

        _host?.RefreshWorkshops();

        // Automation-Unlock-Properties aktualisieren (Level-Gates können sich ändern)
        _host?.NotifyAutomationUnlockChanged();

        // Progressive Disclosure: Wird automatisch via [NotifyPropertyChangedFor] auf _playerLevel ausgelöst

        // Pulse-Animation bei JEDEM Level-Up (dezent, kein Dialog)
        _dialogVm.IsLevelUpPulsing = true;
        _levelPulseTimer?.Stop();
        _levelPulseTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _levelPulseTimer.Tick -= OnLevelPulseTimeout;
        _levelPulseTimer.Tick += OnLevelPulseTimeout;
        _levelPulseTimer.Start();

        // Sound + FloatingText bei jedem Level-Up
        _audioService.PlaySoundAsync(GameSound.ButtonTap).FireAndForget();
        _uiEffectBus.RaiseFloatingText($"Level {e.NewLevel}!", "level");

        // Milestone-Bonus prüfen (10/25/50/100/250/500/1000)
        foreach (var (level, screws) in s_levelMilestones)
        {
            if (e.NewLevel == level)
            {
                _gameStateService.AddGoldenScrews(screws);

                // Sound + Celebration nur bei Milestones
                _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();
                _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
                _uiEffectBus.RaiseCelebration();
                _uiEffectBus.RaiseCeremony(CeremonyType.LevelMilestone,
                    $"Level {e.NewLevel}!", $"+{screws} Goldschrauben");

                // FloatingText mit Level + Goldschrauben-Bonus
                _uiEffectBus.RaiseFloatingText(
                    $"Level {e.NewLevel}! +{screws} ⚙", "level");
                break;
            }
        }

        // Tab-Freischaltung: Hinweis wenn ein neuer Tab verfügbar wird
        CheckTabUnlockNotification(e.NewLevel);

        // Kontextuelle Hints bei Level-Meilensteinen (passend zu Progressive Disclosure)
        // Nicht anzeigen wenn ein anderer Dialog offen ist (z.B. Prestige-Summary)
        if (HostIsAnyDialogVisible) return;

        if (e.NewLevel == LevelThresholds.HintWorkerUnlock)
            _contextualHintService.TryShowHint(ContextualHints.WorkerUnlock);
        else if (e.NewLevel == LevelThresholds.HintQuickJobs)
            _contextualHintService.TryShowHint(ContextualHints.QuickJobs);
        else if (e.NewLevel == LevelThresholds.HintCrafting)
            _contextualHintService.TryShowHint(ContextualHints.CraftingHint);
        else if (e.NewLevel == LevelThresholds.HintManagerUnlock)
            _contextualHintService.TryShowHint(ContextualHints.ManagerUnlock);
        else if (e.NewLevel == LevelThresholds.HintAutomation)
            _contextualHintService.TryShowHint(ContextualHints.Automation);
        else if (e.NewLevel == LevelThresholds.HintMasterTools)
            _contextualHintService.TryShowHint(ContextualHints.MasterToolsUnlock);
        else if (e.NewLevel == LevelThresholds.HintPrestige)
            _contextualHintService.TryShowHint(ContextualHints.PrestigeHint);
        // F-10: Cross-Workshop-Lieferketten ab Lv 100 — Vorabhinweis bei Lv 99.
        else if (e.NewLevel == GameBalanceConstants.MaterialOrderCrossWorkshopLevel - 1)
            _contextualHintService.TryShowHint(ContextualHints.CrossWorkshopComing);
        // F-15 (Backlog): BattlePass-Discovery bei Freischaltung — wiederverwendet
        // den existierenden BattlePass-Hint statt einen neuen anzulegen.
        else if (e.NewLevel == LevelThresholds.BattlePassSection)
            _contextualHintService.TryShowHint(ContextualHints.BattlePass);

        // Story-Kapitel prüfen
        CheckForNewStoryChapter();

        // Review-Milestone prüfen
        _reviewService?.OnMilestone("level", e.NewLevel);
        CheckReviewPrompt();

        // Leaderboard-Score aktualisieren (fire-and-forget)
        if (_playGamesService?.IsSignedIn == true)
            _playGamesService.SubmitScoreAsync("leaderboard_player_level", e.NewLevel).SafeFireAndForget();
    }

    private void OnLevelPulseTimeout(object? sender, EventArgs e)
    {
        _dialogVm.IsLevelUpPulsing = false;
        _levelPulseTimer?.Stop();
    }

    /// <summary>
    /// Prüft ob beim neuen Level ein Tab freigeschaltet wird und zeigt einen Hinweis.
    /// </summary>
    private void CheckTabUnlockNotification(int newLevel)
    {
        string[] tabNames = [
            _localizationService.GetString("TabWerkstatt") ?? "Workshop",
            _localizationService.GetString("TabImperium") ?? "Empire",
            _localizationService.GetString("TabMissionen") ?? "Missions",
            _localizationService.GetString("TabGilde") ?? "Guild",
            _localizationService.GetString("TabShop") ?? "Shop"
        ];

        var unlockLevels = LevelThresholds.TabUnlockLevels;
        for (int i = 0; i < unlockLevels.Length; i++)
        {
            if (unlockLevels[i] == newLevel)
            {
                var unlockText = string.Format(
                    _localizationService.GetString("TabUnlocked") ?? "{0} freigeschaltet!",
                    tabNames[i]);
                _uiEffectBus.RaiseFloatingText(unlockText, "golden_screws");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE
    // ═══════════════════════════════════════════════════════════════════════

    private void OnPrestigeCompleted(object? sender, EventArgs e)
    {
        var prestigeCount = _gameStateService.Prestige.TotalPrestigeCount;

        // Eternal Mastery (Long-Term-Engagement): Header-Badge aktualisieren
        _host?.RefreshEternalMastery();

        // Zeremonie: Feuerwerk + Confetti + Sound
        _uiEffectBus.RaiseCelebration();
        var tierName = _localizationService.GetString("PrestigeCompleted") ?? "Prestige!";
        _uiEffectBus.RaiseCeremony(CeremonyType.Prestige, tierName, $"#{prestigeCount}");
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        _uiEffectBus.RaiseFloatingText($"Prestige #{prestigeCount}!", "level");

        // Floating-Hint mit aktuellem Eternal-Mastery-Bonus
        if (prestigeCount >= 1)
        {
            var bonusPct = GameBalanceConstants.EternalMasteryBonusPerPrestige * prestigeCount
                         + GameBalanceConstants.EternalMasteryBonusPer5Prestiges * (prestigeCount / 5)
                         + GameBalanceConstants.EternalMasteryBonusPer10Prestiges * (prestigeCount / 10);
            _uiEffectBus.RaiseFloatingText(
                $"Eternal Mastery: +{bonusPct * 100m:F1}%",
                "level");
        }

        // Ascension-Hint-Kaskade (— Reset-Hierarchie-Vereinfachung):
        //   1. Prestige → AscensionPath-Hint (Foreshadowing: "So funktioniert Ascension")
        //   3x Legende-Prestige → AscensionAvailable-Hint (Action: "Du kannst jetzt aufsteigen!")
        // So sieht der Spieler den Ascension-Tab nicht erst nach 3x Legende erstmals — er kennt
        // ihn vorher schon konzeptuell und arbeitet darauf hin.
        if (_gameStateService.Prestige.LegendeCount >= 3)
            _contextualHintService.TryShowHint(ContextualHints.AscensionAvailable);
        else if (prestigeCount == 1)
            _contextualHintService.TryShowHint(ContextualHints.AscensionPath);

        _reviewService?.OnMilestone("prestige", prestigeCount);
        CheckReviewPrompt();

        // Story-Kapitel prüfen (Prestige-bezogene Kapitel sofort triggern)
        CheckForNewStoryChapter();
    }

    private void OnPrestigeMilestoneReached(object? sender, PrestigeMilestoneEventArgs e)
    {
        var text = string.Format(
            _localizationService.GetString("PrestigeMilestoneReached") ?? "Prestige milestone! +{0} golden screws",
            e.GoldenScrewReward);
        _uiEffectBus.RaiseFloatingText(text, "currency");
        _uiEffectBus.RaiseCelebration();
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
    }

    private void OnRebirthCompleted(object? sender, WorkshopType type)
    {
        // Erster-Stern-Hint nach erstem Rebirth (erklärt Stern-Boni)
        _contextualHintService.TryShowHint(ContextualHints.FirstStar);
    }

    public void CheckReviewPrompt()
    {
        if (_reviewService?.ShouldPromptReview() == true)
        {
            _reviewService.MarkReviewPrompted();
            App.ReviewPromptRequested?.Invoke();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Worker-Level-Up: FloatingText mit Name + neuem Level und Sound.
    /// </summary>
    private void OnWorkerLevelUp(object? sender, Worker worker)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var levelUpText = string.Format(
                _localizationService.GetString("WorkerLevelUp") ?? "{0} ist jetzt Level {1}!",
                worker.Name, worker.ExperienceLevel);
            _uiEffectBus.RaiseFloatingText(levelUpText, "level");
            _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
        });
    }

    private void OnXpGained(object? sender, XpGainedEventArgs e)
    {
        _headerVm.CurrentXp = e.CurrentXp;
        _headerVm.XpForNextLevel = e.XpForNextLevel;
        // Korrekte Formel aus GameState verwenden (berücksichtigt XP-Basis des aktuellen Levels)
        _headerVm.LevelProgress = _gameStateService.State.LevelProgress;
    }

    /// <summary>
    /// v2.1.0: Praktikant hat 24h aktiv trainiert — Spieler bekommt Promotion-Dialog.
    /// Bei Annahme wird er zu E-Tier promoviert (kostenpflichtig), bei Ablehnung verlaesst er.
    /// </summary>
    private void OnInternReadyForPromotion(object? sender, Worker intern)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var title = _localizationService.GetString("InternPromotionTitle") ?? "Praktikant bereit zur Promotion";
            var msgFormat = _localizationService.GetString("InternPromotionMessage")
                            ?? "{0} hat 24h Training abgeschlossen. Behalten (E-Tier, Lohn) oder gehen lassen?";
            var keep = _localizationService.GetString("InternPromotionKeep") ?? "Behalten";
            var let = _localizationService.GetString("InternPromotionLet") ?? "Gehen lassen";

            var confirmed = await _dialogVm.ShowConfirmDialog(
                title, string.Format(msgFormat, intern.Name), keep, let);

            if (confirmed)
            {
                _workerService.PromoteIntern(intern.Id);
                _uiEffectBus.RaiseFloatingText($"{intern.Name}: E-Tier!", "level");
            }
            else
            {
                _workerService.DeclineInternPromotion(intern.Id);
            }
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP
    // ═══════════════════════════════════════════════════════════════════════

    private void OnWorkshopUpgraded(object? sender, WorkshopUpgradedEventArgs e)
    {
        // Nur den betroffenen Workshop aktualisieren statt alle
        _host?.RefreshSingleWorkshop(e.WorkshopType);

        // Workshop-Detail-Hint nach erstem Upgrade zeigen
        if (!_contextualHintService.HasSeenHint(ContextualHints.WorkshopDetail.Id))
        {
            _host?.SetTutorialHintVisible(false);
            _contextualHintService.TryShowHint(ContextualHints.WorkshopDetail);
        }
        // Long-Press-Hint nach 2. Upgrade — Spieler hat erstes Tap-Upgrade
        // erlebt, jetzt ist der Discoverability-Moment fuer "Halten = x10 / x100 Bulk".
        // Bei aktivem Hold-to-Upgrade zeigen wir den Hint NICHT (er kennt das Feature dann schon).
        else if (!HostIsHoldingUpgrade && !_contextualHintService.HasSeenHint(ContextualHints.LongPressBulk.Id))
        {
            _contextualHintService.TryShowHint(ContextualHints.LongPressBulk);
        }

        // Rebirth-Hint: Erster Workshop erreicht Level 1000
        if (e.NewLevel >= Workshop.MaxLevel)
            _contextualHintService.TryShowHint(ContextualHints.RebirthReady);

        // F-40: T4-Foreshadowing bei Workshop-Lv 450 (kurz vor der T4-Schwelle 500).
        // Erklaert dem Spieler dass das Endgame naht — Logistik-Forschung wird wichtig.
        if (e.NewLevel == 450)
            _contextualHintService.TryShowHint(ContextualHints.Tier4Coming);

        // Multiplikator-Meilensteine (Bumpy Progression)
        if (!HostIsHoldingUpgrade && Workshop.IsMilestoneLevel(e.NewLevel))
        {
            decimal milestoneMultiplier = Workshop.GetMilestoneMultiplierForLevel(e.NewLevel);
            var workshopName = _localizationService.GetString(e.WorkshopType.GetLocalizationKey());
            string boostText = $"x{milestoneMultiplier:0.#} {_localizationService.GetString("IncomeBoost") ?? "Income Boost"}!";

            _uiEffectBus.RaiseFloatingText(boostText, "golden_screws");
            _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

            // Größere Zeremonien bei höheren Meilensteinen
            if (e.NewLevel >= LevelThresholds.WorkshopCeremonyThreshold)
            {
                _uiEffectBus.RaiseCeremony(CeremonyType.WorkshopMilestone,
                    $"{workshopName} Lv.{e.NewLevel}",
                    boostText);
            }
        }

        // Workshop-Level-Milestone prüfen (nicht während Hold-to-Upgrade)
        // Schwellen weiter auseinander damit nicht bei jedem frühen Level Benachrichtigungen kommen
        if (!HostIsHoldingUpgrade)
        {
            foreach (var (level, screws) in s_workshopMilestones)
            {
                if (e.NewLevel == level)
                {
                    _gameStateService.AddGoldenScrews(screws);
                    var workshopName = _localizationService.GetString(e.WorkshopType.GetLocalizationKey());
                    _uiEffectBus.RaiseFloatingText(
                        $"{workshopName} Lv.{e.NewLevel}! +{screws} ⚙", "level");
                    _uiEffectBus.RaiseCelebration();
                    _uiEffectBus.RaiseCeremony(CeremonyType.WorkshopMilestone,
                        $"{workshopName} Lv.{e.NewLevel}!", $"+{screws} Goldschrauben");
                    _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();
                    break;
                }
            }

            // Story-Kapitel prüfen
            CheckForNewStoryChapter();
        }

        // Ziel-Cache invalidieren (Workshop-Level könnte Ziel erfüllen)
        _goalService.Invalidate();
    }

    private void OnWorkerHired(object? sender, WorkerHiredEventArgs e)
    {
        // Nur den betroffenen Workshop aktualisieren statt alle
        _host?.RefreshSingleWorkshop(e.WorkshopType);

        // Ziel-Cache invalidieren (Worker-Einstellung könnte Ziel erfüllen)
        _goalService.Invalidate();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MASTER-TOOLS + ACHIEVEMENTS
    // ═══════════════════════════════════════════════════════════════════════

    private void OnMasterToolUnlocked(object? sender, MasterToolDefinition tool)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var name = _localizationService.GetString(tool.NameKey);
            if (string.IsNullOrEmpty(name)) name = tool.Id;
            _uiEffectBus.RaiseFloatingText($"{tool.Icon} {name}!", "MasterTool");
            _uiEffectBus.RaiseCelebration();
            _uiEffectBus.RaiseCeremony(CeremonyType.MasterTool, name, $"+{(int)(tool.IncomeBonus * 100)}% Einkommen");
            _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

            _missionsVm.MasterToolsCollected = _gameStateService.State.CollectedMasterTools.Count;
        });
    }

    private void OnAchievementUnlocked(object? sender, Achievement achievement)
    {
        // Während Hold-to-Upgrade keine Dialoge anzeigen
        if (HostIsHoldingUpgrade) return;

        _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

        var title = _localizationService.GetString(achievement.TitleKey);
        _dialogVm.AchievementName = string.IsNullOrEmpty(title) ? achievement.TitleFallback : title;
        var desc = _localizationService.GetString(achievement.DescriptionKey);
        _dialogVm.AchievementDescription = string.IsNullOrEmpty(desc) ? achievement.DescriptionFallback : desc;
        _dialogVm.IsAchievementDialogVisible = true;
        _uiEffectBus.RaiseCelebration();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _levelPulseTimer?.Stop();

        if (!_started) return;
        try
        {
            _gameStateService.GoldenScrewsChanged -= OnGoldenScrewsChanged;
            _gameStateService.LevelUp -= OnLevelUp;
            _gameStateService.XpGained -= OnXpGained;
            _gameStateService.WorkshopUpgraded -= OnWorkshopUpgraded;
            _gameStateService.WorkerHired -= OnWorkerHired;
            _gameLoopService.MasterToolUnlocked -= OnMasterToolUnlocked;
            _achievementService.AchievementUnlocked -= OnAchievementUnlocked;
            _prestigeService.PrestigeCompleted -= OnPrestigeCompleted;
            _prestigeService.MilestoneReached -= OnPrestigeMilestoneReached;
            _workerService.WorkerLevelUp -= OnWorkerLevelUp;
            _workerService.InternReadyForPromotion -= OnInternReadyForPromotion;
            if (_rebirthService != null)
                _rebirthService.RebirthCompleted -= OnRebirthCompleted;
        }
        catch { /* Unsubscribe-Fehler beim Shutdown ignorieren */ }
    }
}
