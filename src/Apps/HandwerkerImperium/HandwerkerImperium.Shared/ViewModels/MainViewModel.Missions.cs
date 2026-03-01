using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Daily Challenges, Weekly Missions, Quick Jobs, Lucky Spin,
// Streak-Rettung, Welcome-Back-Angebote, Meisterwerkzeuge-Info
public partial class MainViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // WEEKLY MISSIONS COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ClaimWeeklyMission(string missionId)
    {
        _weeklyMissionService.ClaimMission(missionId);
        _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
        RefreshWeeklyMissions();
    }

    [RelayCommand]
    private void ClaimAllWeeklyBonus()
    {
        _weeklyMissionService.ClaimAllCompletedBonus();
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        CelebrationRequested?.Invoke();
        FloatingTextRequested?.Invoke($"+50 GS", "golden_screws");
        RefreshWeeklyMissions();
    }

    [RelayCommand]
    private void ToggleWeeklyMissionsExpanded()
    {
        IsWeeklyMissionsExpanded = !IsWeeklyMissionsExpanded;
        WeeklyMissionsExpandIconKind = IsWeeklyMissionsExpanded ? "ChevronUp" : "ChevronDown";
    }

    private void RefreshWeeklyMissions()
    {
        var state = _gameStateService.State.WeeklyMissionState;
        if (state?.Missions == null || state.Missions.Count == 0)
        {
            HasWeeklyMissions = false;
            return;
        }

        // Display-Properties befüllen (Lokalisierung + Formatierung)
        foreach (var mission in state.Missions)
        {
            // Lokalisierte Beschreibung mit TargetValue
            var descKey = $"WeeklyMission_{mission.Type}";
            var descTemplate = _localizationService.GetString(descKey) ?? mission.Type.ToString();
            mission.DisplayDescription = mission.Type == WeeklyMissionType.EarnMoney
                ? string.Format(descTemplate, MoneyFormatter.FormatCompact(mission.TargetValue))
                : string.Format(descTemplate, mission.TargetValue);

            // Belohnungs-Anzeige
            var rewardParts = new List<string>();
            if (mission.MoneyReward > 0)
                rewardParts.Add(MoneyFormatter.FormatCompact(mission.MoneyReward));
            if (mission.XpReward > 0)
                rewardParts.Add($"{mission.XpReward} XP");
            if (mission.GoldenScrewReward > 0)
                rewardParts.Add($"{mission.GoldenScrewReward} GS");
            mission.RewardDisplay = string.Join(" + ", rewardParts);

            // Fortschritts-Anzeige
            mission.ProgressDisplay = $"{mission.CurrentValue} / {mission.TargetValue}";
        }

        WeeklyMissions = new List<WeeklyMission>(state.Missions);
        HasWeeklyMissions = true;
        AllWeeklyMissionsCompleted = state.Missions.All(m => m.IsCompleted);
        CanClaimWeeklyBonus = AllWeeklyMissionsCompleted && !state.AllCompletedBonusClaimed;
        UpdateClaimableMissionsCount();

        // Reset-Timer berechnen (nächster Montag)
        var now = DateTime.UtcNow;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        var resetLabel = _localizationService.GetString("WeeklyMissionReset") ?? "Resets in {0} days";
        WeeklyMissionResetDisplay = string.Format(resetLabel, daysUntilMonday);
    }

    private void OnWeeklyMissionProgressChanged()
    {
        Dispatcher.UIThread.Post(RefreshWeeklyMissions);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WELCOME BACK OFFER COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ClaimWelcomeOffer()
    {
        // Offline-Earnings miteinsammeln wenn im Dialog angezeigt
        if (HasOfflineEarningsInWelcome && _pendingOfflineEarnings > 0)
        {
            _gameStateService.AddMoney(_pendingOfflineEarnings);
            FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}", "money");
            _pendingOfflineEarnings = 0;
        }

        _welcomeBackService.ClaimOffer();
        HasOfflineEarningsInWelcome = false;
        CombinedOfflineDisplay = "";
        IsWelcomeOfferVisible = false;
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        CelebrationRequested?.Invoke();
    }

    [RelayCommand]
    private void DismissWelcomeOffer()
    {
        _welcomeBackService.DismissOffer();
        IsWelcomeOfferVisible = false;
    }

    private void OnWelcomeOfferGenerated()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var offer = _gameStateService.State.ActiveWelcomeBackOffer;
            if (offer == null || offer.IsExpired) return;

            WelcomeOfferTitle = _localizationService.GetString("WelcomeBackTitle") ?? "Welcome Back!";
            WelcomeOfferDescription = offer.Type switch
            {
                WelcomeBackOfferType.Premium => _localizationService.GetString("WelcomeBackPremium") ?? "Premium welcome package!",
                WelcomeBackOfferType.StarterPack => _localizationService.GetString("StarterPackTitle") ?? "Starter pack bonus!",
                _ => _localizationService.GetString("WelcomeBackStandard") ?? "We missed you!"
            };
            WelcomeOfferMoneyReward = MoneyFormatter.FormatCompact(offer.MoneyReward);
            WelcomeOfferScrewReward = offer.GoldenScrewReward > 0 ? $"+{offer.GoldenScrewReward}" : "";

            var remaining = offer.TimeRemaining;
            WelcomeOfferTimerDisplay = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
                : $"{remaining.Minutes}m";

            // Wenn Offline-Earnings vorhanden → im Welcome-Dialog mit anzeigen
            HasOfflineEarningsInWelcome = _pendingOfflineEarnings > 0;
            CombinedOfflineDisplay = HasOfflineEarningsInWelcome
                ? $"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}"
                : "";

            IsWelcomeOfferVisible = true;
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LUCKY SPIN COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ShowLuckySpin()
    {
        LuckySpinViewModel.Refresh();
        LuckySpinViewModel.StartCountdownTimer();
        IsLuckySpinVisible = true;
        _adService.HideBanner();
    }

    [RelayCommand]
    private void HideLuckySpin()
    {
        LuckySpinViewModel.StopCountdownTimer();
        IsLuckySpinVisible = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STREAK-RETTUNG COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void RescueStreak()
    {
        var state = _gameStateService.State;
        if (state.GoldenScrews < 5) return;

        _gameStateService.TrySpendGoldenScrews(5);
        state.DailyRewardStreak = Math.Max(1, state.StreakBeforeBreak);
        state.StreakRescueUsed = true;
        _gameStateService.MarkDirty();

        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        CanRescueStreak = false;

        var rescuedMsg = _localizationService.GetString("StreakRescued") ?? "Streak rescued!";
        FloatingTextRequested?.Invoke(rescuedMsg, "golden_screws");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // QUICK JOB + DAILY CHALLENGE COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void StartQuickJob(QuickJob? job)
    {
        if (job == null || job.IsCompleted) return;

        // Tageslimit prüfen (verhindert Reward-Farming)
        if ((_quickJobService as QuickJobService)?.IsDailyLimitReached == true)
        {
            int maxDaily = _quickJobService?.MaxDailyJobs ?? 20;
            var template = _localizationService.GetString("QuickJobDailyLimit");
            var limitText = !string.IsNullOrEmpty(template)
                ? string.Format(template, maxDaily)
                : $"Tageslimit erreicht ({maxDaily}/Tag)";
            FloatingTextRequested?.Invoke(limitText, "Warning");
            return;
        }

        _activeQuickJob = job;
        _quickJobMiniGamePlayed = false;
        _gameStateService.State.ActiveQuickJob = job;
        var route = job.MiniGameType.GetRoute();
        DeactivateAllTabs();
        NavigateToMiniGame(route, "");
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void ToggleChallengesExpanded()
    {
        IsChallengesExpanded = !IsChallengesExpanded;
        ChallengesExpandIconKind = IsChallengesExpanded ? "ChevronUp" : "ChevronDown";
    }

    [RelayCommand]
    private void ToggleQuickJobsExpanded()
    {
        IsQuickJobsExpanded = !IsQuickJobsExpanded;
        QuickJobsExpandIconKind = IsQuickJobsExpanded ? "ChevronUp" : "ChevronDown";
    }

    [RelayCommand]
    private void ShowMasterToolsInfo()
    {
        var state = _gameStateService.State;
        var allTools = MasterTool.GetAllDefinitions();
        var collected = state.CollectedMasterTools;
        var totalBonus = MasterTool.GetTotalIncomeBonus(collected);

        // Kompakten Info-Text zusammenbauen (1 Zeile pro Tool)
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{_localizationService.GetString("IncomeBonus") ?? "Einkommensbonus"}: +{totalBonus:P0}");
        sb.AppendLine();

        foreach (var tool in allTools)
        {
            bool isCollected = collected.Contains(tool.Id);
            var name = _localizationService.GetString(tool.NameKey) ?? tool.Id;
            var bonus = $"+{(int)(tool.IncomeBonus * 100)}%";

            if (isCollected)
            {
                sb.AppendLine($"\u2713 {name} ({bonus})");
            }
            else
            {
                var condition = GetMasterToolCondition(tool.Id);
                sb.AppendLine($"\u2717 {name} ({bonus})");
                sb.AppendLine($"   \u2192 {condition}");
            }
        }

        var title = _localizationService.GetString("MasterTools") ?? "Meisterwerkzeuge";
        ShowAlertDialog(title, sb.ToString().TrimEnd(), _localizationService.GetString("OK"));
    }

    /// <summary>
    /// Gibt die lokalisierte Freischaltbedingung für ein Meisterwerkzeug zurück.
    /// </summary>
    private string GetMasterToolCondition(string toolId)
    {
        return toolId switch
        {
            "mt_golden_hammer" => $"Workshop Lv. 25",
            "mt_diamond_saw" => $"Workshop Lv. 50",
            "mt_titanium_pliers" => $"50 {_localizationService.GetString("Orders") ?? "Aufträge"}",
            "mt_brass_level" => $"100 Mini-Games",
            "mt_silver_wrench" => $"Workshop Lv. 100",
            "mt_jade_brush" => $"25 {_localizationService.GetString("PerfectRating") ?? "Perfect"}",
            "mt_crystal_chisel" => $"1x {_localizationService.GetString("PrestigeBronze") ?? "Bronze-Prestige"}",
            "mt_obsidian_drill" => $"Workshop Lv. 250",
            "mt_ruby_blade" => $"1x {_localizationService.GetString("PrestigeSilver") ?? "Silber-Prestige"}",
            "mt_emerald_toolbox" => $"Workshop Lv. 500",
            "mt_dragon_anvil" => $"1x {_localizationService.GetString("PrestigeGold") ?? "Gold-Prestige"}",
            "mt_master_crown" => $"{_localizationService.GetString("AllToolsCollected") ?? "Alle anderen Werkzeuge"}",
            _ => "?"
        };
    }

    [RelayCommand]
    private void ClaimChallengeReward(DailyChallenge? challenge)
    {
        if (challenge == null) return;
        _dailyChallengeService.ClaimReward(challenge.Id);
        RefreshChallenges();
    }

    [RelayCommand]
    private void ClaimAllChallengesBonus()
    {
        _dailyChallengeService.ClaimAllCompletedBonus();
        RefreshChallenges();
    }

    [RelayCommand]
    private async Task RetryChallengeWithAdAsync(DailyChallenge? challenge)
    {
        if (challenge == null || challenge.IsCompleted || challenge.HasRetriedWithAd || challenge.CurrentValue == 0)
            return;

        var success = await _rewardedAdService.ShowAdAsync("daily_challenge_retry");
        if (success)
        {
            _dailyChallengeService.RetryChallenge(challenge.Id);
            RefreshChallenges();
            ShowAlertDialog(
                _localizationService.GetString("ChallengeRetried"),
                challenge.DisplayDescription,
                _localizationService.GetString("OK"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // REFRESH-METHODEN (Missions)
    // ═══════════════════════════════════════════════════════════════════════

    private void RefreshQuickJobs()
    {
        QuickJobs = _quickJobService.GetAvailableJobs();
        // Empty State für Quick Jobs (Task #8)
        AllQuickJobsDone = QuickJobs.Count == 0 || QuickJobs.All(j => j.IsCompleted);
    }

    private void RefreshChallenges()
    {
        var state = _dailyChallengeService.GetState();
        // Neue Liste erstellen, damit PropertyChanged zuverlässig feuert
        // (gleiche Referenz wird vom CommunityToolkit-Setter ignoriert)
        DailyChallenges = new List<DailyChallenge>(state.Challenges);
        HasDailyChallenges = state.Challenges.Count > 0;
        CanClaimAllBonus = _dailyChallengeService.AreAllCompleted && !state.AllCompletedBonusClaimed;
        UpdateClaimableMissionsCount();
    }

    /// <summary>
    /// Berechnet die Anzahl claimbarer Daily Challenges + Weekly Missions.
    /// </summary>
    private void UpdateClaimableMissionsCount()
    {
        var dailyClaimable = DailyChallenges.Count(c => c.IsCompleted && !c.IsClaimed);
        var weeklyClaimable = WeeklyMissions.Count(m => m.IsCompleted && !m.IsClaimed);
        ClaimableMissionsCount = dailyClaimable + weeklyClaimable;
    }
}
