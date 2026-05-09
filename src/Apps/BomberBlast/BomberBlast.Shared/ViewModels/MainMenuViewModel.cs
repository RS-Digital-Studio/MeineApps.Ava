using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using BomberBlast.Models;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für das Hauptmenü.
/// Navigation zu Spielmodi, Meta-Features und Utility-Seiten.
/// </summary>
public sealed partial class MainMenuViewModel : ViewModelBase, INavigable, IGameJuiceEmitter, IDisposable
{
    private readonly IProgressService _progressService;
    private readonly IPurchaseService _purchaseService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ILocalizationService _localizationService;
    private readonly IDailyRewardService _dailyRewardService;
    private readonly IReviewService _reviewService;
    private readonly IDailyChallengeService _dailyChallengeService;
    private readonly IWeeklyChallengeService _weeklyChallengeService;
    private readonly IDailyMissionService _dailyMissionService;
    private readonly IBattlePassService _battlePassService;
    private readonly ILeagueService _leagueService;
    private readonly IStarterPackService _starterPackService;
    private readonly IPreferencesService _preferencesService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IEventService _eventService;
    // Dashboard-Aggregation (v2.0.43): die HEUTE-/KARRIERE-Cards lesen direkt aus diesen
    // Services. DailyHubViewModel ist abgeschafft — alle Werte werden hier zusammengefuehrt.
    private readonly ILuckySpinService _luckySpinService;
    private readonly IRotatingDealsService _rotatingDealsService;
    private readonly IBossRushService _bossRushService;
    private readonly ICustomizationService _customizationService;
    private readonly IMasterModeService _masterModeService;
    private readonly IAchievementService _achievementService;
    private readonly ICollectionService _collectionService;
    private readonly ICardService _cardService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Event für typsichere Navigation zu anderen Views.
    /// </summary>
    public event Action<NavigationRequest>? NavigationRequested;

    /// <summary>Floating-Text anzeigen (z.B. Daily Bonus)</summary>
    public event Action<string, string>? FloatingTextRequested;

    /// <summary>Celebration-Effekt (Confetti)</summary>
    public event Action? CelebrationRequested;

    /// <summary>In-App Review anfordern (Android: ReviewManagerFactory)</summary>
    public event Action? ReviewRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _showContinueButton;

    [ObservableProperty]
    private string _versionText = "v1.0.0 - RS-Digital";

    [ObservableProperty]
    private string _coinsText = "0";

    [ObservableProperty]
    private int _coinBalance;

    [ObservableProperty]
    private string _totalEarnedText = "";

    [ObservableProperty]
    private string _gemsText = "0";

    /// <summary>v2.0.46 — Easing-Pop bei Currency-Increment (Quick-Win Audit Sektion 3.3)</summary>
    [ObservableProperty]
    private bool _isCoinsPulse;

    [ObservableProperty]
    private bool _isGemsPulse;

    partial void OnCoinsTextChanged(string value) => TriggerCoinsPulse();
    partial void OnGemsTextChanged(string value) => TriggerGemsPulse();

    private void TriggerCoinsPulse()
    {
        IsCoinsPulse = true;
        // Auto-Reset nach 250ms damit der Pulse ein 1-Shot-Trigger ist
        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(280);
            IsCoinsPulse = false;
        });
    }

    private void TriggerGemsPulse()
    {
        IsGemsPulse = true;
        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(280);
            IsGemsPulse = false;
        });
    }

    /// <summary>Ob die heutige Daily Challenge noch nicht gespielt wurde</summary>
    [ObservableProperty]
    private bool _isDailyChallengeNew;

    /// <summary>Ob es offene Missionen gibt (tägliche oder wöchentliche)</summary>
    [ObservableProperty]
    private bool _hasNewMissions;

    // Feature-Freischaltung (progressive Sichtbarkeit im Hauptmenü)
    [ObservableProperty] private bool _isShopUnlocked;
    [ObservableProperty] private bool _isDailyChallengeUnlocked;
    [ObservableProperty] private bool _isQuickPlayUnlocked;
    [ObservableProperty] private bool _isSurvivalUnlocked;
    [ObservableProperty] private bool _isProfileUnlocked;
    [ObservableProperty] private bool _isAchievementsUnlocked;
    [ObservableProperty] private bool _isWeeklyChallengeUnlocked;
    [ObservableProperty] private bool _isLuckySpinUnlocked;
    [ObservableProperty] private bool _isStatisticsUnlocked;
    [ObservableProperty] private bool _isDeckUnlocked;
    [ObservableProperty] private bool _isDailyMissionsUnlocked;   // L17 (neu v2.0.34 — schließt Dead-Zone L15→L20)
    [ObservableProperty] private bool _isCustomizationUnlocked;   // L18 (neu v2.0.34 — Skins/Trails/Victories)
    [ObservableProperty] private bool _isDungeonUnlocked;
    [ObservableProperty] private bool _isBattlePassUnlocked;
    [ObservableProperty] private bool _isCollectionUnlocked;
    [ObservableProperty] private bool _isLeagueUnlocked;

    // "NEU!"-Badges fuer frisch freigeschaltete Features (noch nicht vom Spieler besucht)
    [ObservableProperty] private bool _isShopNew;
    [ObservableProperty] private bool _isQuickPlayNew;
    [ObservableProperty] private bool _isSurvivalNew;
    [ObservableProperty] private bool _isDailyChallengeNewBadge;
    [ObservableProperty] private bool _isLuckySpinNew;
    [ObservableProperty] private bool _isAchievementsNew;
    [ObservableProperty] private bool _isStatisticsNew;
    [ObservableProperty] private bool _isCollectionNew;
    [ObservableProperty] private bool _isDeckNew;
    [ObservableProperty] private bool _isDailyMissionsNew;
    [ObservableProperty] private bool _isWeeklyMissionsNew;
    [ObservableProperty] private bool _isCustomizationNew;
    [ObservableProperty] private bool _isDungeonNew;
    [ObservableProperty] private bool _isLeagueNew;
    [ObservableProperty] private bool _isBattlePassNew;

    /// <summary>Ob das Starterpaket-Angebot angezeigt werden soll</summary>
    [ObservableProperty] private bool _isStarterPackAvailable;

    // Daily Reward Popup
    [ObservableProperty]
    private bool _isRewardPopupVisible;

    [ObservableProperty]
    private string _rewardPopupTitle = "";

    [ObservableProperty]
    private string _rewardClaimText = "";

    public ObservableCollection<DailyRewardDisplayItem> RewardDays { get; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether the player has progress to continue (alias for ShowContinueButton).
    /// </summary>
    public bool HasProgress => ShowContinueButton;

    public MainMenuViewModel(IProgressService progressService, IPurchaseService purchaseService, ICoinService coinService,
        IGemService gemService, ILocalizationService localizationService, IDailyRewardService dailyRewardService,
        IReviewService reviewService, IDailyChallengeService dailyChallengeService,
        IWeeklyChallengeService weeklyChallengeService, IDailyMissionService dailyMissionService,
        IBattlePassService battlePassService, ILeagueService leagueService,
        IStarterPackService starterPackService, IPreferencesService preferencesService,
        IRewardedAdService rewardedAdService, IEventService eventService,
        // Dashboard-Services (v2.0.43)
        ILuckySpinService luckySpinService, IRotatingDealsService rotatingDealsService,
        IBossRushService bossRushService, ICustomizationService customizationService,
        IMasterModeService masterModeService, IAchievementService achievementService,
        ICollectionService collectionService, ICardService cardService)
    {
        _progressService = progressService;
        _purchaseService = purchaseService;
        _coinService = coinService;
        _gemService = gemService;
        _localizationService = localizationService;
        _dailyRewardService = dailyRewardService;
        _reviewService = reviewService;
        _dailyChallengeService = dailyChallengeService;
        _weeklyChallengeService = weeklyChallengeService;
        _dailyMissionService = dailyMissionService;
        _battlePassService = battlePassService;
        _leagueService = leagueService;
        _starterPackService = starterPackService;
        _preferencesService = preferencesService;
        _rewardedAdService = rewardedAdService;
        _eventService = eventService;
        _luckySpinService = luckySpinService;
        _rotatingDealsService = rotatingDealsService;
        _bossRushService = bossRushService;
        _customizationService = customizationService;
        _masterModeService = masterModeService;
        _achievementService = achievementService;
        _collectionService = collectionService;
        _cardService = cardService;

        // Live-Update bei Coin-/Gem-Änderungen (z.B. aus Shop, Rewarded Ads)
        _coinService.BalanceChanged += OnBalanceChanged;
        _gemService.BalanceChanged += OnBalanceChanged;

        // Version aus dem eigenen Assembly lesen (GetEntryAssembly() gibt null auf Android)
        var assembly = typeof(MainMenuViewModel).Assembly;
        var infoVersion = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (infoVersion != null)
        {
            // InformationalVersion kann "+commitHash" enthalten → nur den Teil vor '+' nehmen
            var plusIndex = infoVersion.IndexOf('+');
            if (plusIndex > 0) infoVersion = infoVersion[..plusIndex];
            VersionText = $"v{infoVersion} - RS-Digital";
        }
        else
        {
            var version = assembly.GetName().Version;
            VersionText = version != null
                ? $"v{version.Major}.{version.Minor}.{version.Build} - RS-Digital"
                : "v2.0.7 - RS-Digital";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when the view appears. Refreshes continue button visibility.
    /// Prüft und vergibt täglichen Bonus.
    /// </summary>
    public void OnAppearing()
    {
        ShowContinueButton = _progressService.HighestCompletedLevel > 0;

        // 7-Tage Daily Reward: Popup anzeigen statt auto-claim
        if (_dailyRewardService.IsRewardAvailable)
        {
            ShowRewardPopup();
        }

        // In-App Review prüfen
        if (_reviewService.ShouldPromptReview())
        {
            _reviewService.MarkReviewPrompted();
            // ReviewRequested Event wird in MainViewModel behandelt (Android: ReviewManagerFactory)
            ReviewRequested?.Invoke();
        }

        CoinBalance = _coinService.Balance;
        CoinsText = _coinService.Balance.ToString("N0");
        GemsText = _gemService.Balance.ToString("N0");
        TotalEarnedText = string.Format(
            _localizationService.GetString("TotalEarned") ?? "Total: {0}",
            _coinService.TotalEarned.ToString("N0"));
        IsDailyChallengeNew = !_dailyChallengeService.IsCompletedToday;
        HasNewMissions = !_weeklyChallengeService.IsAllComplete || !_dailyMissionService.IsAllComplete;

        OnPropertyChanged(nameof(HasProgress));

        // Dashboard-Refresh: HEUTE-Panel + KARRIERE-Panel + Hero-Section + Modi-Strip + Event-Banner
        // (Implementiert in MainMenuViewModel.Dashboard.cs)
        RefreshDashboard();

        // Comeback-Bonus prüfen (>3 Tage inaktiv → 2000 Coins + 5 Gems)
        var comebackBonus = _dailyRewardService.CheckComebackBonus();
        if (comebackBonus.HasValue)
        {
            var (coins, gems) = comebackBonus.Value;
            _coinService.AddCoins(coins);
            _gemService.AddGems(gems);

            var comebackTitle = _localizationService.GetString("ComebackTitle") ?? "Welcome back!";
            var comebackText = string.Format(
                _localizationService.GetString("ComebackBonus") ?? "+{0} Coins, +{1} Gems",
                coins.ToString("N0"), gems);
            FloatingTextRequested?.Invoke($"{comebackTitle} {comebackText}", "gold");
            CelebrationRequested?.Invoke();
        }

        // Letzte Aktivität aktualisieren (für zukünftige Comeback-Prüfung)
        _dailyRewardService.UpdateLastActivity();

        // Saisonales Event-Greeting (v2.0.42, Plan Task 3.4) — einmal pro Tag pro Event
        TryShowEventGreeting();

        // Onboarding-Modal (v2.0.43, Plan Phase 4) — einmal pro Installation,
        // erklaert das neue Dashboard-Layout fuer Bestand-Spieler.
        TryShowOnboarding();

        // Starterpaket-Eligibility pruefen
        _starterPackService.CheckEligibility(_progressService.HighestCompletedLevel);
        IsStarterPackAvailable = _starterPackService.IsAvailable;

        // Progressive Feature-Freischaltung basierend auf hoechstem abgeschlossenen Level
        UpdateFeatureUnlocks();

        // Feature-Celebration bei erstmaligem Unlock
        CheckForNewFeatureUnlocks();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PROGRESSIVE FEATURE-FREISCHALTUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prueft ob ein Feature bereits vom Spieler besucht wurde.
    /// </summary>
    private bool HasSeenFeature(string feature) =>
        _preferencesService.Get($"feature_seen_{feature}", false);

    /// <summary>
    /// Markiert ein Feature als gesehen (entfernt "NEU!"-Badge).
    /// Wird beim Navigieren zum Feature aufgerufen.
    /// </summary>
    public void MarkFeatureSeen(string feature)
    {
        _preferencesService.Set($"feature_seen_{feature}", true);

        // Entsprechendes "NEU!"-Badge Property zuruecksetzen
        switch (feature)
        {
            case "shop": IsShopNew = false; break;
            case "quickplay": IsQuickPlayNew = false; break;
            case "survival": IsSurvivalNew = false; break;
            case "daily_challenge": IsDailyChallengeNewBadge = false; break;
            case "lucky_spin": IsLuckySpinNew = false; break;
            case "achievements": IsAchievementsNew = false; break;
            case "statistics": IsStatisticsNew = false; break;
            case "collection": IsCollectionNew = false; break;
            case "deck": IsDeckNew = false; break;
            case "daily_missions": IsDailyMissionsNew = false; break;
            case "weekly_missions": IsWeeklyMissionsNew = false; break;
            case "customization": IsCustomizationNew = false; break;
            case "dungeon": IsDungeonNew = false; break;
            case "league": IsLeagueNew = false; break;
            case "battle_pass": IsBattlePassNew = false; break;
        }
    }

    /// <summary>
    /// Setzt Feature-Sichtbarkeit und "NEU!"-Badges basierend auf Spielfortschritt.
    /// Level 0-2: Nur Story, Settings, Help
    /// Level 3-4: + Shop
    /// Level 5-7: + Survival, QuickPlay
    /// Level 8-9: + DailyChallenge, LuckySpin
    /// Level 10-14: + Achievements, Statistics, Collection (1. Boss besiegt)
    /// Level 15-16: + Deck, WeeklyMissions
    /// Level 17: + DailyMissions (eigener Meilenstein, v2.0.34 — schließt Dead-Zone L15→L20)
    /// Level 18: + Customization (Player-Skins, Trails, Victory-Emotes, v2.0.34)
    /// Level 19: (kein neuer Unlock — Hype-Build vor Dungeon)
    /// Level 20-29: + Dungeon
    /// Level 30+: + League, BattlePass
    /// </summary>
    private void UpdateFeatureUnlocks()
    {
        int level = _progressService.HighestCompletedLevel;

        // Story, Settings, Help, Profile: Immer sichtbar
        IsProfileUnlocked = true;

        // Level 3+: Shop
        IsShopUnlocked = level >= 3;
        IsShopNew = IsShopUnlocked && !HasSeenFeature("shop");

        // Level 5+: Survival, QuickPlay
        IsSurvivalUnlocked = level >= 5;
        IsQuickPlayUnlocked = level >= 5;
        IsSurvivalNew = IsSurvivalUnlocked && !HasSeenFeature("survival");
        IsQuickPlayNew = IsQuickPlayUnlocked && !HasSeenFeature("quickplay");

        // Level 8+: DailyChallenge, LuckySpin
        IsDailyChallengeUnlocked = level >= 8;
        IsLuckySpinUnlocked = level >= 8;
        IsDailyChallengeNewBadge = IsDailyChallengeUnlocked && !HasSeenFeature("daily_challenge");
        IsLuckySpinNew = IsLuckySpinUnlocked && !HasSeenFeature("lucky_spin");

        // Level 10+: Achievements, Statistics, Collection (1. Boss besiegt)
        IsAchievementsUnlocked = level >= 10;
        IsStatisticsUnlocked = level >= 10;
        IsCollectionUnlocked = level >= 10;
        IsAchievementsNew = IsAchievementsUnlocked && !HasSeenFeature("achievements");
        IsStatisticsNew = IsStatisticsUnlocked && !HasSeenFeature("statistics");
        IsCollectionNew = IsCollectionUnlocked && !HasSeenFeature("collection");

        // Level 15+: Deck + WeeklyMissions
        IsDeckUnlocked = level >= 15;
        IsWeeklyChallengeUnlocked = level >= 15;
        IsDeckNew = IsDeckUnlocked && !HasSeenFeature("deck");
        IsWeeklyMissionsNew = IsWeeklyChallengeUnlocked && !HasSeenFeature("weekly_missions");

        // Level 17+: DailyMissions (eigener Meilenstein, schliesst Dead-Zone L15→L20)
        IsDailyMissionsUnlocked = level >= 17;
        IsDailyMissionsNew = IsDailyMissionsUnlocked && !HasSeenFeature("daily_missions");

        // Level 18+: Customization (Player-Skins, Trails, Victory-Emotes)
        IsCustomizationUnlocked = level >= 18;
        IsCustomizationNew = IsCustomizationUnlocked && !HasSeenFeature("customization");

        // Level 20+: Dungeon
        IsDungeonUnlocked = level >= 20;
        IsDungeonNew = IsDungeonUnlocked && !HasSeenFeature("dungeon");

        // Level 30+: League, BattlePass
        IsLeagueUnlocked = level >= 30;
        IsBattlePassUnlocked = level >= 30;
        IsLeagueNew = IsLeagueUnlocked && !HasSeenFeature("league");
        IsBattlePassNew = IsBattlePassUnlocked && !HasSeenFeature("battle_pass");
    }

    /// <summary>
    /// Prüft ob seit dem letzten Besuch neue Features freigeschaltet wurden.
    /// Zeigt eine Celebration + FloatingText für das wichtigste neue Feature.
    /// Verwendet "feature_celebration_level" um nur einmal pro Level-Schwelle zu feuern.
    /// </summary>
    private void CheckForNewFeatureUnlocks()
    {
        int level = _progressService.HighestCompletedLevel;
        int lastCelebratedLevel = _preferencesService.Get("feature_celebration_level", 0);
        if (level <= lastCelebratedLevel) return;

        // Feature-Schwellen mit zugehörigem Feature-Namen (höchste Priorität zuerst)
        (int threshold, string featureName)[] featureThresholds =
        [
            (100, "Master Mode"),          // Endgame-Unlock (v2.0.35) — New Game+
            (30, "League + Battle Pass"),
            (20, "Dungeon"),
            (18, "Customization"),        // Player-Skins, Trails, Victory-Emotes (v2.0.34)
            (17, "Daily Missions"),        // Eigener Meilenstein (v2.0.34 — Dead-Zone L15→L20)
            (15, "Deck + Weekly Missions"),
            (10, "Achievements + Collection"),
            (8, "Daily Challenge + Lucky Spin"),
            (5, "Survival + Quick Play"),
            (3, "Shop")
        ];

        // Höchste neu erreichte Schwelle finden
        foreach (var (threshold, featureName) in featureThresholds)
        {
            if (level >= threshold && lastCelebratedLevel < threshold)
            {
                var title = _localizationService.GetString("FeatureUnlocked") ?? "New Feature!";
                var descFormat = _localizationService.GetString("FeatureUnlockedDesc") ?? "{0} is now available!";
                var desc = string.Format(descFormat, featureName);

                FloatingTextRequested?.Invoke($"{title} {desc}", "gold");
                CelebrationRequested?.Invoke();
                break; // Nur die wichtigste Celebration zeigen
            }
        }

        _preferencesService.Set("feature_celebration_level", level);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY REWARD POPUP
    // ═══════════════════════════════════════════════════════════════════════

    private void ShowRewardPopup()
    {
        RewardPopupTitle = _localizationService.GetString("DailyRewardTitle") ?? "Daily Bonus";
        RewardClaimText = _localizationService.GetString("DailyRewardCollect") ?? "Collect";

        RewardDays.Clear();
        var rewards = _dailyRewardService.GetRewards();
        foreach (var r in rewards)
        {
            RewardDays.Add(new DailyRewardDisplayItem
            {
                Day = r.Day,
                DayText = string.Format(
                    _localizationService.GetString("DailyRewardDay") ?? "Day {0}", r.Day),
                CoinsText = $"+{r.Coins:N0}",
                HasExtraLife = r.ExtraLives > 0,
                IsClaimed = r.IsClaimed,
                IsCurrentDay = r.IsCurrentDay,
                IsFuture = !r.IsClaimed && !r.IsCurrentDay
            });
        }

        IsRewardPopupVisible = true;
    }

    [RelayCommand]
    private void ClaimDailyReward()
    {
        ApplyDailyReward(multiplier: 1);
        IsRewardPopupVisible = false;
    }

    /// <summary>
    /// Rewarded-Ad für 2x Daily-Reward (v2.0.34). Premium-User sehen keine Ad und kriegen
    /// trotzdem 2x automatisch (als Premium-Bonus). RewardedAdCooldownTracker wird beachtet.
    /// </summary>
    [RelayCommand]
    private async Task ClaimDailyRewardDoubledAsync()
    {
        // Premium-User: Direkter 2x-Claim ohne Ad
        if (_purchaseService.IsPremium)
        {
            ApplyDailyReward(multiplier: 2);
            IsRewardPopupVisible = false;
            return;
        }

        // Cooldown: 60s zwischen allen Rewarded-Placements
        if (!RewardedAdCooldownTracker.CanShowAd)
        {
            var cooldownMsg = _localizationService.GetString("AdCooldownActive") ?? "Please wait before watching another ad.";
            FloatingTextRequested?.Invoke(cooldownMsg, "error");
            return;
        }

        var adSuccess = await _rewardedAdService.ShowAdAsync("double_daily_reward");
        if (adSuccess)
        {
            RewardedAdCooldownTracker.RecordAdShown();
            ApplyDailyReward(multiplier: 2);
        }
        else
        {
            // Ad fehlgeschlagen → normaler 1x Claim (Spieler nicht bestrafen)
            ApplyDailyReward(multiplier: 1);
        }
        IsRewardPopupVisible = false;
    }

    /// <summary>
    /// Gemeinsame Claim-Logik für 1x und 2x Daily-Reward.
    /// </summary>
    private void ApplyDailyReward(int multiplier)
    {
        var reward = _dailyRewardService.ClaimReward();
        if (reward == null) return;

        int coins = reward.Coins * multiplier;
        int gems = reward.Gems * multiplier;
        int extraLives = reward.ExtraLives * multiplier;

        _coinService.AddCoins(coins);
        if (gems > 0) _gemService.AddGems(gems);

        // Battle Pass XP + Liga-Punkte für täglichen Login (immer 1x, kein Farming)
        _battlePassService.AddXp(BattlePassXpSources.DailyLogin, "daily_login");
        _leagueService.AddPoints(5);

        var dayText = string.Format(
            _localizationService.GetString("DailyRewardDay") ?? "Day {0}",
            reward.Day);
        var coinsLabel = _localizationService.GetString("Coins") ?? "Coins";
        var bonusText = $"{dayText}: +{coins:N0} {coinsLabel}!";
        if (gems > 0)
        {
            var gemsLabel = _localizationService.GetString("Gems") ?? "Gems";
            bonusText += $" +{gems} {gemsLabel}!";
        }
        if (extraLives > 0)
        {
            bonusText += $" +{extraLives} " +
                (_localizationService.GetString("DailyRewardExtraLife") ?? "Extra Life");
        }

        var prefix = multiplier > 1
            ? (_localizationService.GetString("DailyRewardDoubled") ?? "DOUBLED! ")
            : "";
        FloatingTextRequested?.Invoke(prefix + bonusText, "gold");
        CelebrationRequested?.Invoke();

        // Coin-/Gem-Anzeige aktualisieren
        CoinBalance = _coinService.Balance;
        CoinsText = _coinService.Balance.ToString("N0");
        GemsText = _gemService.Balance.ToString("N0");
    }

    [RelayCommand]
    private void DismissRewardPopup()
    {
        // Popup schließen OHNE zu claimen (naechster Besuch zeigt es erneut)
        IsRewardPopupVisible = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void StoryMode()
    {
        NavigationRequested?.Invoke(new GoLevelSelect());
    }

    [RelayCommand]
    private void Continue()
    {
        int nextLevel = Math.Min(
            _progressService.HighestCompletedLevel + 1,
            _progressService.TotalLevels);
        NavigationRequested?.Invoke(new GoGame(Mode: "story", Level: nextLevel));
    }

    [RelayCommand]
    private void QuickPlay()
    {
        MarkFeatureSeen("quickplay");
        NavigationRequested?.Invoke(new GoQuickPlay());
    }

    [RelayCommand]
    private void SurvivalMode()
    {
        MarkFeatureSeen("survival");
        NavigationRequested?.Invoke(new GoGame(Mode: "survival"));
    }

    [RelayCommand]
    private void HighScores()
    {
        NavigationRequested?.Invoke(new GoHighScores());
    }

    [RelayCommand]
    private void Help()
    {
        NavigationRequested?.Invoke(new GoHelp());
    }

    [RelayCommand]
    private void Settings()
    {
        NavigationRequested?.Invoke(new GoSettings());
    }

    [RelayCommand]
    private void Shop()
    {
        MarkFeatureSeen("shop");
        NavigationRequested?.Invoke(new GoShop());
    }

    [RelayCommand]
    private void Achievements()
    {
        MarkFeatureSeen("achievements");
        NavigationRequested?.Invoke(new GoAchievements());
    }

    [RelayCommand]
    private void DailyChallenge()
    {
        MarkFeatureSeen("daily_challenge");
        NavigationRequested?.Invoke(new GoDailyChallenge());
    }

    [RelayCommand]
    private void LuckyWheel()
    {
        MarkFeatureSeen("lucky_spin");
        NavigationRequested?.Invoke(new GoLuckySpin());
    }

    [RelayCommand]
    private void WeeklyChallenge()
    {
        MarkFeatureSeen("weekly_missions");
        NavigationRequested?.Invoke(new GoWeeklyChallenge());
    }

    [RelayCommand]
    private void Statistics()
    {
        MarkFeatureSeen("statistics");
        NavigationRequested?.Invoke(new GoStatistics());
    }

    [RelayCommand]
    private void Profile()
    {
        NavigationRequested?.Invoke(new GoProfile());
    }

    /// <summary>
    /// Tägliche + wöchentliche Missionen — wird vom HEUTE-Panel der Dashboard-Card "Missions"
    /// aus aufgerufen (v2.0.43, ersetzt den L17-Standalone-Button im alten Layout).
    /// </summary>
    [RelayCommand]
    private void Missions()
    {
        MarkFeatureSeen("daily_missions");
        MarkFeatureSeen("weekly_missions");
        NavigationRequested?.Invoke(new GoWeeklyChallenge());
    }

    [RelayCommand]
    private void Deck()
    {
        MarkFeatureSeen("deck");
        NavigationRequested?.Invoke(new GoDeck());
    }

    [RelayCommand]
    private void Dungeon()
    {
        MarkFeatureSeen("dungeon");
        NavigationRequested?.Invoke(new GoDungeon());
    }

    [RelayCommand]
    private void BattlePass()
    {
        MarkFeatureSeen("battle_pass");
        NavigationRequested?.Invoke(new GoBattlePass());
    }

    [RelayCommand]
    private void Collection()
    {
        MarkFeatureSeen("collection");
        NavigationRequested?.Invoke(new GoCollection());
    }

    [RelayCommand]
    private void League()
    {
        MarkFeatureSeen("league");
        NavigationRequested?.Invoke(new GoLeague());
    }

    [RelayCommand]
    private void GoToGemShop() => NavigationRequested?.Invoke(new GoGemShop());

    /// <summary>
    /// Starterpaket kaufen. Nutzt IPurchaseService wenn verfügbar, sonst Coins-Fallback (1999).
    /// </summary>
    [RelayCommand]
    private void BuyStarterPack()
    {
        if (_starterPackService.IsAlreadyPurchased) return;

        // Versuch: Coin-basierter Kauf als Fallback (1999 Coins)
        if (_coinService.Balance >= 1999)
        {
            _coinService.TrySpendCoins(1999);
            _starterPackService.MarkAsPurchased();

            var packTitle = _localizationService.GetString("StarterPackTitle") ?? "Starter Pack";
            var packDesc = _localizationService.GetString("StarterPackDesc") ?? "2500 Coins + 10 Gems + 2 Rare Cards!";
            FloatingTextRequested?.Invoke($"{packTitle}: {packDesc}", "gold");
            CelebrationRequested?.Invoke();

            IsStarterPackAvailable = false;

            // Coin-/Gem-Anzeige aktualisieren
            CoinBalance = _coinService.Balance;
            CoinsText = _coinService.Balance.ToString("N0");
            GemsText = _gemService.Balance.ToString("N0");
        }
        else
        {
            // Nicht genug Coins → Info anzeigen
            var insufficientText = _localizationService.GetString("ShopNotEnoughCoins") ?? "Not enough Coins!";
            FloatingTextRequested?.Invoke(insufficientText, "red");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SAISONALES EVENT-GREETING (v2.0.42, Plan Task 3.4)
    // ═══════════════════════════════════════════════════════════════════════

    private const string EventGreetingShownKey = "EventGreetingShown_v1";

    /// <summary>
    /// Zeigt einmal pro Tag pro aktivem Event ein Floating-Greeting beim Erscheinen des MainMenus.
    /// Persistiert per UTC-Datum in Preferences damit Halloween/Christmas/etc. nicht spamt.
    /// </summary>
    private void TryShowEventGreeting()
    {
        var ev = _eventService.CurrentEvent;
        if (ev == null) return;

        // Persistenter Schluessel: "Halloween_2026-10-30" — eindeutig pro Event-Type + UTC-Tag.
        var todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var stateKey = $"{EventGreetingShownKey}_{ev.Type}_{todayKey}";
        var alreadyShown = _preferencesService.Get(stateKey, false);
        if (alreadyShown) return;

        var greeting = _localizationService.GetString(ev.GreetingKey) ?? ev.Type.ToString();
        FloatingTextRequested?.Invoke(greeting, "gold");
        CelebrationRequested?.Invoke();

        _preferencesService.Set(stateKey, true);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BALANCE CHANGED
    // ═══════════════════════════════════════════════════════════════════════

    private void OnBalanceChanged(object? sender, EventArgs e)
    {
        CoinBalance = _coinService.Balance;
        CoinsText = _coinService.Balance.ToString("N0");
        GemsText = _gemService.Balance.ToString("N0");
        TotalEarnedText = string.Format(
            _localizationService.GetString("TotalEarned") ?? "Total: {0}",
            _coinService.TotalEarned.ToString("N0"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        _coinService.BalanceChanged -= OnBalanceChanged;
        _gemService.BalanceChanged -= OnBalanceChanged;
    }
}
