using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
public partial class MainMenuViewModel : ObservableObject, INavigable, IGameJuiceEmitter, IDisposable
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
        IStarterPackService starterPackService, IPreferencesService preferencesService)
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

        // Starterpaket-Eligibility pruefen
        _starterPackService.CheckEligibility(_progressService.HighestCompletedLevel);
        IsStarterPackAvailable = _starterPackService.IsAvailable;

        // Progressive Feature-Freischaltung basierend auf hoechstem abgeschlossenen Level
        UpdateFeatureUnlocks();
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
    /// Level 15-19: + Deck, DailyMissions, WeeklyMissions
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

        // Level 15+: Deck, DailyMissions, WeeklyMissions
        IsDeckUnlocked = level >= 15;
        IsWeeklyChallengeUnlocked = level >= 15;
        IsDeckNew = IsDeckUnlocked && !HasSeenFeature("deck");
        IsDailyMissionsNew = level >= 15 && !HasSeenFeature("daily_missions");
        IsWeeklyMissionsNew = IsWeeklyChallengeUnlocked && !HasSeenFeature("weekly_missions");

        // Level 20+: Dungeon
        IsDungeonUnlocked = level >= 20;
        IsDungeonNew = IsDungeonUnlocked && !HasSeenFeature("dungeon");

        // Level 30+: League, BattlePass
        IsLeagueUnlocked = level >= 30;
        IsBattlePassUnlocked = level >= 30;
        IsLeagueNew = IsLeagueUnlocked && !HasSeenFeature("league");
        IsBattlePassNew = IsBattlePassUnlocked && !HasSeenFeature("battle_pass");
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
        var reward = _dailyRewardService.ClaimReward();
        if (reward != null)
        {
            _coinService.AddCoins(reward.Coins);

            // Gem-Bonus vergeben (Tag 7: +10 Gems)
            if (reward.Gems > 0)
            {
                _gemService.AddGems(reward.Gems);
            }

            // Battle Pass XP + Liga-Punkte für täglichen Login
            _battlePassService.AddXp(BattlePassXpSources.DailyLogin, "daily_login");
            _leagueService.AddPoints(5);

            var dayText = string.Format(
                _localizationService.GetString("DailyRewardDay") ?? "Day {0}",
                reward.Day);
            var coinsLabel = _localizationService.GetString("Coins") ?? "Coins";
            var bonusText = $"{dayText}: +{reward.Coins:N0} {coinsLabel}!";

            if (reward.Gems > 0)
            {
                var gemsLabel = _localizationService.GetString("Gems") ?? "Gems";
                bonusText += $" +{reward.Gems} {gemsLabel}!";
            }

            if (reward.ExtraLives > 0)
            {
                bonusText += $" +{reward.ExtraLives} " +
                    (_localizationService.GetString("DailyRewardExtraLife") ?? "Extra Life");
            }

            FloatingTextRequested?.Invoke(bonusText, "gold");
            CelebrationRequested?.Invoke();
        }

        IsRewardPopupVisible = false;

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
