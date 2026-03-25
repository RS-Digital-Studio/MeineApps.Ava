using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Eigenstaendiges ViewModel fuer den Missionen-Bereich.
/// Enthaelt Daily Challenges, Weekly Missions, Quick Jobs, Lucky Spin,
/// Streak-Rettung, Welcome-Back-Angebote und Meisterwerkzeuge-Info.
/// Extrahiert aus MainViewModel.Missions.cs (19.03.2026).
/// </summary>
public sealed partial class MissionsFeatureViewModel : ViewModelBase, IDisposable
{
    private readonly IWeeklyMissionService _weeklyMissionService;
    private readonly ILuckySpinService _luckySpinService;
    private readonly IQuickJobService _quickJobService;
    private readonly IDailyChallengeService _dailyChallengeService;
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService _dialogService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IWelcomeBackService _welcomeBackService;
    private readonly IAdService _adService;
    private readonly IContextualHintService _contextualHintService;
    private bool _disposed;

    // Dirty-Flag fuer RefreshChallenges: Vermeidet neue List-Allokation + PropertyChanged wenn kein Progress
    private bool _challengesDirty = true;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS (Kommunikation zurueck zu MainViewModel)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>FloatingText im Dashboard anzeigen (Text, Kategorie).</summary>
    public event Action<string, string>? FloatingTextRequested;

    /// <summary>Confetti-Celebration im Dashboard ausloesen.</summary>
    public event Action? CelebrationRequested;

    /// <summary>MiniGame-Navigation anfordern (Route, OrderId).</summary>
    public event Action<string, string>? NavigateToMiniGameRequested;

    /// <summary>Verzögerte Dialoge prüfen (nach Welcome-Dialog-Dismiss).</summary>
    public event Action? CheckDeferredDialogsRequested;

    /// <summary>Streak wurde gerettet - MainViewModel muss LoginStreak-Properties aktualisieren.</summary>
    public event Action? StreakRescued;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - TAB-NAVIGATION (Heute / Wettbewerbe)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isTodayTabActive = true;

    [ObservableProperty]
    private bool _isCompetitionsTabActive;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - QUICK JOBS
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private List<QuickJob> _quickJobs = [];

    [ObservableProperty]
    private bool _allQuickJobsDone;

    [ObservableProperty]
    private bool _isQuickJobsExpanded = true;

    [ObservableProperty]
    private string _quickJobsExpandIconKind = "ChevronUp";

    [ObservableProperty]
    private string _quickJobTimerDisplay = string.Empty;

    // QuickJob-Timer: Letzte Minuten/Sekunden fuer int-Vergleich statt String-Allokation
    private int _lastQjMins = -1, _lastQjSecs = -1;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - DAILY CHALLENGES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private List<DailyChallenge> _dailyChallenges = [];

    [ObservableProperty]
    private bool _hasDailyChallenges;

    [ObservableProperty]
    private bool _isChallengesExpanded = false;

    [ObservableProperty]
    private bool _canClaimAllBonus;

    [ObservableProperty]
    private string _challengesExpandIconKind = "ChevronDown";

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - WEEKLY MISSIONS
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private List<WeeklyMission> _weeklyMissions = [];

    [ObservableProperty]
    private bool _hasWeeklyMissions;

    [ObservableProperty]
    private bool _allWeeklyMissionsCompleted;

    [ObservableProperty]
    private bool _canClaimWeeklyBonus;

    [ObservableProperty]
    private string _weeklyMissionResetDisplay = "";

    [ObservableProperty]
    private bool _isWeeklyMissionsExpanded = false;

    [ObservableProperty]
    private string _weeklyMissionsExpandIconKind = "ChevronDown";

    /// <summary>
    /// Anzahl claimbarer Daily Challenges + Weekly Missions (fuer Tab-Bar Badge).
    /// </summary>
    [ObservableProperty]
    private int _claimableMissionsCount;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - WELCOME BACK OFFER
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isWelcomeOfferVisible;

    [ObservableProperty]
    private string _welcomeOfferTitle = "";

    [ObservableProperty]
    private string _welcomeOfferDescription = "";

    [ObservableProperty]
    private string _welcomeOfferMoneyReward = "";

    [ObservableProperty]
    private string _welcomeOfferScrewReward = "";

    [ObservableProperty]
    private string _welcomeOfferTimerDisplay = "";

    /// <summary>
    /// Ob im Welcome-Back-Dialog auch Offline-Earnings angezeigt werden sollen.
    /// </summary>
    [ObservableProperty]
    private bool _hasOfflineEarningsInWelcome;

    /// <summary>
    /// Formatierte Anzeige der Offline-Earnings im Welcome-Back-Dialog.
    /// </summary>
    [ObservableProperty]
    private string _combinedOfflineDisplay = "";

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - LUCKY SPIN
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _hasFreeSpin;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - STREAK-RETTUNG
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _canRescueStreak;

    [ObservableProperty]
    private string _streakRescueText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - MEISTERWERKZEUGE
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _masterToolsCollected;

    [ObservableProperty]
    private int _masterToolsTotal;

    [ObservableProperty]
    private string _masterToolsBonusDisplay = "";

    // ═══════════════════════════════════════════════════════════════════════
    // REFERENZ auf LuckySpinViewModel (fuer ShowLuckySpin/HideLuckySpin)
    // ═══════════════════════════════════════════════════════════════════════

    public LuckySpinViewModel LuckySpinViewModel { get; }

    // Pending-Offline-Earnings (wird von MainViewModel.Init gesetzt)
    internal decimal PendingOfflineEarnings { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public MissionsFeatureViewModel(
        IWeeklyMissionService weeklyMissionService,
        ILuckySpinService luckySpinService,
        IQuickJobService quickJobService,
        IDailyChallengeService dailyChallengeService,
        IGameStateService gameStateService,
        IAudioService audioService,
        ILocalizationService localizationService,
        IDialogService dialogService,
        IRewardedAdService rewardedAdService,
        IWelcomeBackService welcomeBackService,
        IAdService adService,
        IContextualHintService contextualHintService,
        LuckySpinViewModel luckySpinViewModel)
    {
        _weeklyMissionService = weeklyMissionService;
        _luckySpinService = luckySpinService;
        _quickJobService = quickJobService;
        _dailyChallengeService = dailyChallengeService;
        _gameStateService = gameStateService;
        _audioService = audioService;
        _localizationService = localizationService;
        _dialogService = dialogService;
        _rewardedAdService = rewardedAdService;
        _welcomeBackService = welcomeBackService;
        _adService = adService;
        _contextualHintService = contextualHintService;
        LuckySpinViewModel = luckySpinViewModel;

        // Service-Events subscriben
        _dailyChallengeService.ChallengeProgressChanged += OnChallengeProgressChanged;
        _weeklyMissionService.MissionProgressChanged += OnWeeklyMissionProgressChanged;
        _welcomeBackService.OfferGenerated += OnWelcomeOfferGenerated;
    }

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
        FloatingTextRequested?.Invoke($"+{_weeklyMissionService.AllCompletedBonusScrews} \u2699", "golden_screws");
        RefreshWeeklyMissions();
    }

    [RelayCommand]
    private void ToggleWeeklyMissionsExpanded()
    {
        IsWeeklyMissionsExpanded = !IsWeeklyMissionsExpanded;
        WeeklyMissionsExpandIconKind = IsWeeklyMissionsExpanded ? "ChevronUp" : "ChevronDown";
    }

    public void RefreshWeeklyMissions()
    {
        var state = _gameStateService.State.WeeklyMissionState;
        if (state?.Missions == null || state.Missions.Count == 0)
        {
            HasWeeklyMissions = false;
            return;
        }

        // Display-Properties befuellen (Lokalisierung + Formatierung)
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
                rewardParts.Add($"{mission.GoldenScrewReward} \u2699");
            mission.RewardDisplay = string.Join(" + ", rewardParts);

            // Fortschritts-Anzeige
            mission.ProgressDisplay = $"{mission.CurrentValue} / {mission.TargetValue}";
        }

        WeeklyMissions = new List<WeeklyMission>(state.Missions);
        HasWeeklyMissions = true;
        AllWeeklyMissionsCompleted = state.Missions.All(m => m.IsCompleted);
        CanClaimWeeklyBonus = AllWeeklyMissionsCompleted && !state.AllCompletedBonusClaimed;
        UpdateClaimableMissionsCount();

        // Reset-Timer berechnen (naechster Montag)
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
        if (HasOfflineEarningsInWelcome && PendingOfflineEarnings > 0)
        {
            _gameStateService.AddMoney(PendingOfflineEarnings);
            FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(PendingOfflineEarnings)}", "money");
            PendingOfflineEarnings = 0;
        }

        _welcomeBackService.ClaimOffer();
        HasOfflineEarningsInWelcome = false;
        CombinedOfflineDisplay = "";
        IsWelcomeOfferVisible = false;
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        CelebrationRequested?.Invoke();
        CheckDeferredDialogsRequested?.Invoke();
    }

    [RelayCommand]
    private void DismissWelcomeOffer()
    {
        _welcomeBackService.DismissOffer();
        IsWelcomeOfferVisible = false;
        CheckDeferredDialogsRequested?.Invoke();
    }

    internal void OnWelcomeOfferGenerated()
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
            HasOfflineEarningsInWelcome = PendingOfflineEarnings > 0;
            CombinedOfflineDisplay = HasOfflineEarningsInWelcome
                ? $"+{MoneyFormatter.FormatCompact(PendingOfflineEarnings)}"
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
        _adService.HideBanner();
        _contextualHintService.TryShowHint(ContextualHints.LuckySpin);
    }

    [RelayCommand]
    private void HideLuckySpin()
    {
        LuckySpinViewModel.StopCountdownTimer();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TAB-NAVIGATION COMMANDS (Heute / Wettbewerbe)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectTodayTab()
    {
        IsTodayTabActive = true;
        IsCompetitionsTabActive = false;
    }

    [RelayCommand]
    private void SelectCompetitionsTab()
    {
        IsTodayTabActive = false;
        IsCompetitionsTabActive = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STREAK-RETTUNG COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void RescueStreak()
    {
        var state = _gameStateService.State;
        if (state.GoldenScrews < 3) return;  // BAL-7: Von 5 auf 3 reduziert

        _gameStateService.TrySpendGoldenScrews(3);
        state.DailyRewardStreak = Math.Max(1, state.StreakBeforeBreak);
        state.StreakRescueUsed = true;
        _gameStateService.MarkDirty();

        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        CanRescueStreak = false;

        // MainViewModel muss Dashboard-Header-Badges aktualisieren
        StreakRescued?.Invoke();

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

        // Tageslimit pruefen (verhindert Reward-Farming)
        if (_quickJobService?.IsDailyLimitReached == true)
        {
            int maxDaily = _quickJobService?.MaxDailyJobs ?? 20;
            var template = _localizationService.GetString("QuickJobDailyLimit");
            var limitText = !string.IsNullOrEmpty(template)
                ? string.Format(template, maxDaily)
                : $"Tageslimit erreicht ({maxDaily}/Tag)";
            FloatingTextRequested?.Invoke(limitText, "Warning");
            return;
        }

        // MiniGame-Navigation anfordern (MainViewModel routet zum richtigen MiniGame)
        var route = job.MiniGameType.GetRoute();
        NavigateToMiniGameRequested?.Invoke(route, "");
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
        _dialogService.ShowAlertDialog(title, sb.ToString().TrimEnd(), _localizationService.GetString("OK"));
    }

    /// <summary>
    /// Gibt die lokalisierte Freischaltbedingung fuer ein Meisterwerkzeug zurueck.
    /// Werte muessen mit MasterTool.CheckEligibility() uebereinstimmen!
    /// </summary>
    private string GetMasterToolCondition(string toolId)
    {
        return toolId switch
        {
            "mt_golden_hammer" => $"Workshop Lv. 75",
            "mt_diamond_saw" => $"Workshop Lv. 150",
            "mt_titanium_pliers" => $"150 {_localizationService.GetString("Orders") ?? "Aufträge"}",
            "mt_brass_level" => $"300 Mini-Games",
            "mt_silver_wrench" => $"Workshop Lv. 300",
            "mt_jade_brush" => $"75 {_localizationService.GetString("PerfectRating") ?? "Perfect"}",
            "mt_crystal_chisel" => $"1x {_localizationService.GetString("PrestigeBronze") ?? "Bronze-Prestige"}",
            "mt_obsidian_drill" => $"Workshop Lv. 750",
            "mt_ruby_blade" => $"1x {_localizationService.GetString("PrestigeSilver") ?? "Silber-Prestige"}",
            "mt_emerald_toolbox" => $"Workshop Lv. 1500",
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
            _dialogService.ShowAlertDialog(
                _localizationService.GetString("ChallengeRetried"),
                challenge.DisplayDescription,
                _localizationService.GetString("OK"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // REFRESH-METHODEN
    // ═══════════════════════════════════════════════════════════════════════

    public void RefreshQuickJobs()
    {
        QuickJobs = _quickJobService.GetAvailableJobs();
        // Empty State fuer Quick Jobs
        AllQuickJobsDone = QuickJobs.Count == 0 || QuickJobs.All(j => j.IsCompleted);
    }

    public void RefreshChallenges()
    {
        // Dirty-Flag: Nur neue Liste erstellen wenn sich Progress tatsaechlich geaendert hat
        if (!_challengesDirty) return;
        _challengesDirty = false;

        var state = _dailyChallengeService.GetState();
        // Neue Liste erstellen, damit PropertyChanged zuverlaessig feuert
        DailyChallenges = new List<DailyChallenge>(state.Challenges);
        HasDailyChallenges = state.Challenges.Count > 0;
        CanClaimAllBonus = _dailyChallengeService.AreAllCompleted && !state.AllCompletedBonusClaimed;
        UpdateClaimableMissionsCount();
    }

    /// <summary>
    /// Setzt das Dirty-Flag und erzwingt ein Refresh beim naechsten Aufruf.
    /// Wird von MainViewModel bei Sprachwechsel und State-Reset aufgerufen.
    /// </summary>
    public void MarkChallengesDirty()
    {
        _challengesDirty = true;
    }

    /// <summary>
    /// Berechnet die Anzahl claimbarer Daily Challenges + Weekly Missions.
    /// For-Loop statt LINQ um GC-Allokationen (Lambda-Closures) zu vermeiden.
    /// </summary>
    private void UpdateClaimableMissionsCount()
    {
        int dailyClaimable = 0;
        var dailies = DailyChallenges;
        for (int i = 0; i < dailies.Count; i++)
        {
            var c = dailies[i];
            if (c.IsCompleted && !c.IsClaimed) dailyClaimable++;
        }

        int weeklyClaimable = 0;
        var weeklies = WeeklyMissions;
        for (int i = 0; i < weeklies.Count; i++)
        {
            var m = weeklies[i];
            if (m.IsCompleted && !m.IsClaimed) weeklyClaimable++;
        }

        ClaimableMissionsCount = dailyClaimable + weeklyClaimable;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAMETICK-INTEGRATION (aufgerufen von MainViewModel.OnGameTick)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert QuickJob-Timer und Rotation. Wird jede Sekunde vom GameTick aufgerufen.
    /// </summary>
    public void UpdateQuickJobTimer()
    {
        if (_quickJobService.NeedsRotation())
        {
            _quickJobService.RotateIfNeeded();
            RefreshQuickJobs();
        }
        // Int-Vergleich statt String-Allokation pro Tick
        var remaining = _quickJobService.TimeUntilNextRotation;
        int qjMins = (int)remaining.TotalMinutes;
        int qjSecs = remaining.Seconds;
        if (qjMins != _lastQjMins || qjSecs != _lastQjSecs)
        {
            _lastQjMins = qjMins;
            _lastQjSecs = qjSecs;
            QuickJobTimerDisplay = qjMins >= 1 ? $"{qjMins}:{qjSecs:D2}" : $"0:{qjSecs:D2}";
        }
    }

    /// <summary>
    /// Periodisches Update fuer Lucky Spin und Welcome Back Timer.
    /// Wird alle 10 Ticks vom GameTick aufgerufen.
    /// </summary>
    public void UpdatePeriodicState()
    {
        var newFreeSpin = _luckySpinService.HasFreeSpin;
        if (newFreeSpin != HasFreeSpin) HasFreeSpin = newFreeSpin;

        // Welcome Back Timer aktualisieren
        if (IsWelcomeOfferVisible)
        {
            var state = _gameStateService.State;
            var offer = state.ActiveWelcomeBackOffer;
            if (offer == null || offer.IsExpired)
            {
                IsWelcomeOfferVisible = false;
            }
            else
            {
                var offerRemaining = offer.TimeRemaining;
                WelcomeOfferTimerDisplay = offerRemaining.TotalHours >= 1
                    ? $"{(int)offerRemaining.TotalHours}h {offerRemaining.Minutes:D2}m"
                    : $"{offerRemaining.Minutes}m";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENT-HANDLER (Service-Events)
    // ═══════════════════════════════════════════════════════════════════════

    private void OnChallengeProgressChanged(object? sender, EventArgs e)
    {
        _challengesDirty = true;
        Dispatcher.UIThread.Post(RefreshChallenges);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;

        _dailyChallengeService.ChallengeProgressChanged -= OnChallengeProgressChanged;
        _weeklyMissionService.MissionProgressChanged -= OnWeeklyMissionProgressChanged;
        _welcomeBackService.OfferGenerated -= OnWelcomeOfferGenerated;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
