using BomberBlast.Core;
using BomberBlast.Resources.Strings;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// Main ViewModel that manages navigation between views (MainMenu, Game, LevelSelect, etc.).
/// Acts as a view-switcher: only one child view is visible at a time.
/// Holds child ViewModels so each view gets the correct DataContext.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS (Game Juice)
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    /// <summary>
    /// Event für Android-Toast bei Double-Back-to-Exit Hinweis
    /// </summary>
    public event Action<string>? ExitHintRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // CHILD VIEWMODELS
    // ═══════════════════════════════════════════════════════════════════════

    public MainMenuViewModel MenuVm { get; }
    public GameViewModel GameVm { get; }
    public LevelSelectViewModel LevelSelectVm { get; }
    public SettingsViewModel SettingsVm { get; }
    public HighScoresViewModel HighScoresVm { get; }
    public GameOverViewModel GameOverVm { get; }
    public PauseViewModel PauseVm { get; }
    public HelpViewModel HelpVm { get; }
    public ShopViewModel ShopVm { get; }
    public AchievementsViewModel AchievementsVm { get; }
    public DailyChallengeViewModel DailyChallengeVm { get; }
    public VictoryViewModel VictoryVm { get; }
    public LuckySpinViewModel LuckySpinVm { get; }
    public WeeklyChallengeViewModel WeeklyChallengeVm { get; }
    public StatisticsViewModel StatisticsVm { get; }
    public QuickPlayViewModel QuickPlayVm { get; }
    public DeckViewModel DeckVm { get; }
    public DungeonViewModel DungeonVm { get; }
    public BattlePassViewModel BattlePassVm { get; }
    public CollectionViewModel CollectionVm { get; }
    public LeagueViewModel LeagueVm { get; }

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isMainMenuActive = true;

    [ObservableProperty]
    private bool _isGameActive;

    [ObservableProperty]
    private bool _isLevelSelectActive;

    [ObservableProperty]
    private bool _isSettingsActive;

    [ObservableProperty]
    private bool _isHighScoresActive;

    [ObservableProperty]
    private bool _isGameOverActive;

    [ObservableProperty]
    private bool _isHelpActive;

    [ObservableProperty]
    private bool _isShopActive;

    [ObservableProperty]
    private bool _isAchievementsActive;

    [ObservableProperty]
    private bool _isDailyChallengeActive;

    [ObservableProperty]
    private bool _isVictoryActive;

    [ObservableProperty]
    private bool _isLuckySpinActive;

    [ObservableProperty]
    private bool _isWeeklyChallengeActive;

    [ObservableProperty]
    private bool _isStatisticsActive;

    [ObservableProperty]
    private bool _isQuickPlayActive;

    [ObservableProperty]
    private bool _isDeckActive;

    [ObservableProperty]
    private bool _isDungeonActive;

    [ObservableProperty]
    private bool _isBattlePassActive;

    [ObservableProperty]
    private bool _isCollectionActive;

    [ObservableProperty]
    private bool _isLeagueActive;

    /// <summary>
    /// Ad-Banner-Spacer: sichtbar in Menü-Views, versteckt im Game-View
    /// </summary>
    [ObservableProperty]
    private bool _isAdBannerVisible;

    // ═══════════════════════════════════════════════════════════════════════
    // DIALOG PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isAlertDialogVisible;

    [ObservableProperty]
    private string _alertDialogTitle = "";

    [ObservableProperty]
    private string _alertDialogMessage = "";

    [ObservableProperty]
    private string _alertDialogButtonText = "";

    [ObservableProperty]
    private bool _isConfirmDialogVisible;

    [ObservableProperty]
    private string _confirmDialogTitle = "";

    [ObservableProperty]
    private string _confirmDialogMessage = "";

    [ObservableProperty]
    private string _confirmDialogAcceptText = "";

    [ObservableProperty]
    private string _confirmDialogCancelText = "";

    private TaskCompletionSource<bool>? _confirmDialogTcs;

    /// <summary>
    /// Tracks whether Settings was opened from within a game (for back-navigation).
    /// </summary>
    private bool _returnToGameFromSettings;

    private readonly ILocalizationService _localizationService;
    private readonly IAdService _adService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IAchievementService _achievementService;
    private readonly ICoinService _coinService;
    private readonly ICloudSaveService _cloudSaveService;
    private readonly SoundManager _soundManager;

    /// <summary>
    /// Zeitpunkt des letzten Back-Presses (für Double-Back-to-Exit)
    /// </summary>
    private DateTime _lastBackPressTime = DateTime.MinValue;

    /// <summary>
    /// Zaehlt Fehlversuche pro Level (fuer Level-Skip nach 3x Game Over)
    /// </summary>
    private readonly Dictionary<int, int> _levelFailCounts = new();

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public MainViewModel(
        MainMenuViewModel menuVm,
        GameViewModel gameVm,
        LevelSelectViewModel levelSelectVm,
        SettingsViewModel settingsVm,
        HighScoresViewModel highScoresVm,
        GameOverViewModel gameOverVm,
        PauseViewModel pauseVm,
        HelpViewModel helpVm,
        ShopViewModel shopVm,
        AchievementsViewModel achievementsVm,
        DailyChallengeViewModel dailyChallengeVm,
        VictoryViewModel victoryVm,
        LuckySpinViewModel luckySpinVm,
        WeeklyChallengeViewModel weeklyChallengeVm,
        StatisticsViewModel statisticsVm,
        QuickPlayViewModel quickPlayVm,
        DeckViewModel deckVm,
        DungeonViewModel dungeonVm,
        BattlePassViewModel battlePassVm,
        CollectionViewModel collectionVm,
        LeagueViewModel leagueVm,
        ILocalizationService localization,
        IAdService adService,
        IPurchaseService purchaseService,
        IRewardedAdService rewardedAdService,
        IAchievementService achievementService,
        ICoinService coinService,
        ICloudSaveService cloudSaveService,
        SoundManager soundManager)
    {
        MenuVm = menuVm;
        GameVm = gameVm;
        LevelSelectVm = levelSelectVm;
        SettingsVm = settingsVm;
        HighScoresVm = highScoresVm;
        GameOverVm = gameOverVm;
        PauseVm = pauseVm;
        HelpVm = helpVm;
        ShopVm = shopVm;
        AchievementsVm = achievementsVm;
        DailyChallengeVm = dailyChallengeVm;
        VictoryVm = victoryVm;
        LuckySpinVm = luckySpinVm;
        WeeklyChallengeVm = weeklyChallengeVm;
        StatisticsVm = statisticsVm;
        QuickPlayVm = quickPlayVm;
        DeckVm = deckVm;
        DungeonVm = dungeonVm;
        BattlePassVm = battlePassVm;
        CollectionVm = collectionVm;
        LeagueVm = leagueVm;
        _localizationService = localization;
        _adService = adService;
        _rewardedAdService = rewardedAdService;
        _achievementService = achievementService;
        _coinService = coinService;
        _cloudSaveService = cloudSaveService;
        _soundManager = soundManager;

        // Game Juice Events weiterleiten
        GameOverVm.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        GameOverVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);
        MenuVm.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        MenuVm.CelebrationRequested += () => CelebrationRequested?.Invoke();
        LevelSelectVm.CelebrationRequested += () => CelebrationRequested?.Invoke();
        LevelSelectVm.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);

        // Achievement-Toast bei Unlock (mit Coin-Belohnung)
        _achievementService.AchievementUnlocked += (_, achievement) =>
        {
            var name = localization.GetString(achievement.NameKey) ?? achievement.NameKey;
            string text = achievement.CoinReward > 0
                ? $"Achievement: {name}! +{achievement.CoinReward} Coins"
                : $"Achievement: {name}!";
            FloatingTextRequested?.Invoke(text, "gold");
        };

        // Shop: Kauf-Feedback
        ShopVm.PurchaseSucceeded += name =>
        {
            FloatingTextRequested?.Invoke(name, "success");
            CelebrationRequested?.Invoke();
        };
        ShopVm.InsufficientFunds += () =>
        {
            var msg = localization.GetString("ShopNotEnoughCoins") ?? "Nicht genug Coins!";
            FloatingTextRequested?.Invoke(msg, "error");
        };

        // Ad-Banner starten
        if (adService.AdsEnabled && !purchaseService.IsPremium)
            adService.ShowBanner();

        // Ad-Spacer-Sichtbarkeit (Menü-Views: sichtbar, Game-View: versteckt)
        IsAdBannerVisible = adService.BannerVisible;
        adService.AdsStateChanged += (_, _) => IsAdBannerVisible = adService.BannerVisible && !IsGameActive;

        // Ad-Unavailable Meldung anzeigen (benannte Methode statt Lambda fuer Unsubscribe)
        _rewardedAdService.AdUnavailable += OnAdUnavailable;

        // PauseVM Resume/Restart Events mit GameVM verbinden
        pauseVm.ResumeRequested += () => gameVm.ResumeCommand.Execute(null);
        pauseVm.RestartRequested += () => gameVm.RestartCommand.Execute(null);

        // Wire up navigation from child VMs
        WireNavigation(menuVm);
        WireNavigation(gameVm);
        WireNavigation(levelSelectVm);
        WireNavigation(settingsVm);
        WireNavigation(highScoresVm);
        WireNavigation(gameOverVm);
        WireNavigation(pauseVm);
        WireNavigation(helpVm);
        WireNavigation(shopVm);
        WireNavigation(achievementsVm);
        WireNavigation(dailyChallengeVm);
        WireNavigation(victoryVm);
        WireNavigation(luckySpinVm);
        WireNavigation(weeklyChallengeVm);
        WireNavigation(statisticsVm);
        WireNavigation(quickPlayVm);
        WireNavigation(deckVm);
        WireNavigation(dungeonVm);
        WireNavigation(battlePassVm);
        WireNavigation(collectionVm);
        WireNavigation(leagueVm);

        // Lucky Spin Game Juice Events weiterleiten
        LuckySpinVm.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        LuckySpinVm.CelebrationRequested += () => CelebrationRequested?.Invoke();

        // Daily Challenge Game Juice Events weiterleiten
        DailyChallengeVm.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        DailyChallengeVm.CelebrationRequested += () => CelebrationRequested?.Invoke();

        // Weekly Challenge Game Juice Events weiterleiten
        WeeklyChallengeVm.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        WeeklyChallengeVm.CelebrationRequested += () => CelebrationRequested?.Invoke();

        // Deck Game Juice Events weiterleiten
        DeckVm.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        DeckVm.CelebrationRequested += () => CelebrationRequested?.Invoke();

        // Dungeon Game Juice Events weiterleiten
        DungeonVm.FloatingTextRequested += (_, args) => FloatingTextRequested?.Invoke(args.text, args.type);
        DungeonVm.CelebrationRequested += (_, _) => CelebrationRequested?.Invoke();

        // Battle Pass Game Juice Events weiterleiten
        BattlePassVm.FloatingTextRequested += (_, args) => FloatingTextRequested?.Invoke(args.text, args.type);
        BattlePassVm.CelebrationRequested += (_, _) => CelebrationRequested?.Invoke();

        // Battle Pass Premium-Kauf anfordern
        BattlePassVm.PremiumPurchaseRequested += async () =>
        {
            // TODO Phase 9: IPurchaseService.PurchaseAsync("battle_pass_premium")
            // Vorerst direkt aktivieren (Test-Modus)
            BattlePassVm.OnPremiumPurchaseConfirmed();
        };

        // Dungeon Ad-Run: Rewarded Ad zeigen und bei Erfolg melden
        DungeonVm.AdRunRequested += async () =>
        {
            var result = await _rewardedAdService.ShowAdAsync("dungeon_run");
            if (result) DungeonVm.OnAdRunRewarded();
        };

        // Wire up dialog events from SettingsVM + ShopVM
        settingsVm.AlertRequested += (t, m, b) => ShowAlertDialog(t, m, b);
        settingsVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);
        shopVm.MessageRequested += (t, m) => ShowAlertDialog(t, m, "OK");
        shopVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);

        localization.LanguageChanged += (_, _) =>
        {
            // Child VMs re-read their localized texts on next OnAppearing
            MenuVm.OnAppearing();
            ShopVm.UpdateLocalizedTexts();
            QuickPlayVm.UpdateLocalizedTexts();
            DeckVm.UpdateLocalizedTexts();
            DungeonVm.UpdateLocalizedTexts();
            BattlePassVm.UpdateLocalizedTexts();
            CollectionVm.UpdateLocalizedTexts();
            LeagueVm.UpdateLocalizedTexts();
        };

        // Cloud Save: Bei App-Start Cloud-Stand laden (fire-and-forget)
        _ = _cloudSaveService.TryLoadFromCloudAsync();

        // Initialize menu
        menuVm.OnAppearing();
    }

    private void WireNavigation(ObservableObject vm)
    {
        var navEvent = vm.GetType().GetEvent("NavigationRequested");
        if (navEvent != null)
        {
            navEvent.AddEventHandler(vm, new Action<string>(route => NavigateTo(route)));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Navigate to a specific view. Hides all others.
    /// Supports routes like "Game?mode=story&amp;level=5" and
    /// compound routes like "//MainMenu/Game?mode=story".
    /// </summary>
    public async void NavigateTo(string route)
    {
        try
        {
        // Handle compound routes (e.g., "//MainMenu/Game?mode=story")
        if (route.StartsWith("//"))
        {
            var withoutPrefix = route[2..];
            var slashIndex = withoutPrefix.IndexOf('/');
            if (slashIndex >= 0)
                route = withoutPrefix[(slashIndex + 1)..];
            else
                route = withoutPrefix;
        }

        var baseRoute = route.Contains('?') ? route[..route.IndexOf('?')] : route;

        // Save current state before hiding (needed for back-navigation)
        var wasGameActive = IsGameActive;

        // Lifecycle: stop game loop when leaving Game view
        if (wasGameActive && baseRoute != "Game")
        {
            GameVm.OnDisappearing();
        }

        HideAll();

        // Navigations-Sound (nicht beim Game-Start, das hat eigene Sounds)
        if (baseRoute != "Game")
            _soundManager.PlaySound(SoundManager.SFX_MENU_SELECT);

        switch (baseRoute)
        {
            case "MainMenu":
                _returnToGameFromSettings = false;
                IsMainMenuActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                MenuVm.OnAppearing();
                break;

            case "Game":
                IsGameActive = true;
                IsAdBannerVisible = false; // Kein Spacer im Spiel (Banner ist oben)
                // Parse game parameters
                if (route.Contains('?'))
                {
                    var query = route[(route.IndexOf('?') + 1)..];
                    var mode = "quick";
                    var level = 1;
                    var difficulty = 5;
                    var continueMode = false;
                    var boost = "";
                    var floor = 0;
                    var seed = 0;
                    foreach (var param in query.Split('&'))
                    {
                        var parts = param.Split('=');
                        if (parts.Length == 2)
                        {
                            if (parts[0] == "mode") mode = parts[1];
                            if (parts[0] == "level") int.TryParse(parts[1], out level);
                            if (parts[0] == "difficulty") int.TryParse(parts[1], out difficulty);
                            if (parts[0] == "continue") bool.TryParse(parts[1], out continueMode);
                            if (parts[0] == "boost") boost = parts[1];
                            if (parts[0] == "floor") int.TryParse(parts[1], out floor);
                            if (parts[0] == "seed") int.TryParse(parts[1], out seed);
                        }
                    }
                    GameVm.SetParameters(mode, level, continueMode, boost, difficulty, floor, seed);
                }
                // Start the game (initializes engine + starts 60fps loop)
                await GameVm.OnAppearingAsync();
                break;

            case "LevelSelect":
                IsLevelSelectActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                LevelSelectVm.OnAppearing();
                break;

            case "Settings":
                _returnToGameFromSettings = wasGameActive;
                IsSettingsActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                SettingsVm.OnAppearing();
                break;

            case "HighScores":
                IsHighScoresActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                HighScoresVm.OnAppearing();
                break;

            case "GameOver":
                IsGameOverActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                if (route.Contains('?'))
                {
                    var query = route[(route.IndexOf('?') + 1)..];
                    var score = 0;
                    var level = 0;
                    var isHighScore = false;
                    var mode = "story";
                    var coins = 0;
                    var levelComplete = false;
                    var canContinue = false;
                    var enemyPts = 0;
                    var timeBonus = 0;
                    var effBonus = 0;
                    var multiplier = 1f;
                    var survivalKills = 0;
                    var survivalTime = 0f;
                    foreach (var param in query.Split('&'))
                    {
                        var parts = param.Split('=');
                        if (parts.Length == 2)
                        {
                            switch (parts[0])
                            {
                                case "score": int.TryParse(parts[1], out score); break;
                                case "level": int.TryParse(parts[1], out level); break;
                                case "highscore": bool.TryParse(parts[1], out isHighScore); break;
                                case "mode": mode = parts[1]; break;
                                case "coins": int.TryParse(parts[1], out coins); break;
                                case "levelcomplete": bool.TryParse(parts[1], out levelComplete); break;
                                case "cancontinue": bool.TryParse(parts[1], out canContinue); break;
                                case "enemypts": int.TryParse(parts[1], out enemyPts); break;
                                case "timebonus": int.TryParse(parts[1], out timeBonus); break;
                                case "effbonus": int.TryParse(parts[1], out effBonus); break;
                                case "multiplier": float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out multiplier); break;
                                case "kills": int.TryParse(parts[1], out survivalKills); break;
                                case "survivaltime": float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out survivalTime); break;
                            }
                        }
                    }

                    // Fehlversuche pro Level tracken (fuer Level-Skip)
                    var fails = 0;
                    if (!levelComplete && mode == "story" && level > 0)
                    {
                        fails = _levelFailCounts.GetValueOrDefault(level) + 1;
                        _levelFailCounts[level] = fails;
                    }
                    else if (levelComplete && level > 0)
                    {
                        _levelFailCounts.Remove(level);
                    }

                    GameOverVm.SetParameters(score, level, isHighScore, mode, coins, levelComplete, canContinue, fails,
                        enemyPts, timeBonus, effBonus, multiplier, survivalKills, survivalTime);

                    // Daily Challenge: Score melden + Streak-Bonus vergeben
                    if (mode == "daily" && score > 0)
                    {
                        DailyChallengeVm.SubmitScore(score);
                    }

                    // Level Complete → Confetti + Floating Text
                    if (levelComplete)
                    {
                        CelebrationRequested?.Invoke();
                        FloatingTextRequested?.Invoke(
                            _localizationService.GetString("LevelComplete") ?? "Level Complete!",
                            "success");
                    }
                }
                break;

            case "Shop":
                IsShopActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                ShopVm.OnAppearing();
                break;

            case "Help":
                IsHelpActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                break;

            case "Achievements":
                IsAchievementsActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                AchievementsVm.OnAppearing();
                break;

            case "DailyChallenge":
                IsDailyChallengeActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                DailyChallengeVm.OnAppearing();
                break;

            case "LuckySpin":
                IsLuckySpinActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                LuckySpinVm.OnAppearing();
                break;

            case "WeeklyChallenge":
                IsWeeklyChallengeActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                WeeklyChallengeVm.OnAppearing();
                break;

            case "Statistics":
                IsStatisticsActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                StatisticsVm.OnAppearing();
                break;

            case "QuickPlay":
                IsQuickPlayActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                QuickPlayVm.OnAppearing();
                break;

            case "Deck":
                IsDeckActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                DeckVm.OnAppearing();
                break;

            case "Dungeon":
                IsDungeonActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                DungeonVm.OnAppearing();
                break;

            case "BattlePass":
                IsBattlePassActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                BattlePassVm.OnAppearing();
                break;

            case "Collection":
                IsCollectionActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                CollectionVm.OnAppearing();
                break;

            case "League":
                IsLeagueActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                LeagueVm.OnAppearing();
                break;

            case "Victory":
                IsVictoryActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                VictoryVm.OnAppearing();
                // Query-Parameter parsen (score, coins)
                if (route.Contains('?'))
                {
                    var vQuery = route[(route.IndexOf('?') + 1)..];
                    var vScore = 0;
                    var vCoins = 0;
                    foreach (var param in vQuery.Split('&'))
                    {
                        var parts = param.Split('=');
                        if (parts.Length == 2)
                        {
                            if (parts[0] == "score") int.TryParse(parts[1], out vScore);
                            if (parts[0] == "coins") int.TryParse(parts[1], out vCoins);
                        }
                    }
                    VictoryVm.SetScore(vScore);
                    // Coins gutschreiben
                    if (vCoins > 0) _coinService.AddCoins(vCoins);
                }
                CelebrationRequested?.Invoke();
                FloatingTextRequested?.Invoke(
                    _localizationService.GetString("VictoryTitle") ?? "Victory!",
                    "gold");
                break;

            case "..":
                // Back-navigation: return to Game if Settings was opened from Game
                if (_returnToGameFromSettings)
                {
                    _returnToGameFromSettings = false;
                    IsGameActive = true;
                    IsAdBannerVisible = false;
                    await GameVm.OnAppearingAsync();
                }
                else
                {
                    IsMainMenuActive = true;
                    IsAdBannerVisible = _adService.BannerVisible;
                    MenuVm.OnAppearing();
                }
                break;

            default:
                IsMainMenuActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                MenuVm.OnAppearing();
                break;
        }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NavigateTo Fehler bei Route '{route}': {ex.Message}");
            // Fallback: Zurück zum Hauptmenü damit die App nicht hängt
            try
            {
                HideAll();
                IsMainMenuActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                MenuVm.OnAppearing();
            }
            catch { /* Letzter Ausweg - App lebt weiter */ }
        }
    }

    private void HideAll()
    {
        IsMainMenuActive = false;
        IsGameActive = false;
        IsLevelSelectActive = false;
        IsSettingsActive = false;
        IsHighScoresActive = false;
        IsGameOverActive = false;
        IsHelpActive = false;
        IsShopActive = false;
        IsAchievementsActive = false;
        IsDailyChallengeActive = false;
        IsVictoryActive = false;
        IsLuckySpinActive = false;
        IsWeeklyChallengeActive = false;
        IsStatisticsActive = false;
        IsQuickPlayActive = false;
        IsDeckActive = false;
        IsDungeonActive = false;
        IsBattlePassActive = false;
        IsCollectionActive = false;
        IsLeagueActive = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BACK-NAVIGATION (Android Hardware-Zurücktaste)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Hierarchische Back-Navigation. Gibt true zurück wenn das Event behandelt wurde,
    /// false wenn die App geschlossen werden soll.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Offene Dialoge schließen (höchste Priorität)
        if (IsConfirmDialogVisible)
        {
            CancelConfirm();
            return true;
        }
        if (IsAlertDialogVisible)
        {
            DismissAlert();
            return true;
        }

        // 2. Score-Double Overlay → überspringen
        if (GameVm.ShowScoreDoubleOverlay)
        {
            GameVm.SkipDoubleScoreCommand.Execute(null);
            return true;
        }

        // 3. Im Spiel: Pause/Resume
        if (IsGameActive)
        {
            if (GameVm.IsPaused)
            {
                // Pause → Resume
                GameVm.ResumeCommand.Execute(null);
            }
            else if (GameVm.State == Core.GameState.Playing)
            {
                // Spielend → Pause
                GameVm.PauseCommand.Execute(null);
            }
            else
            {
                // Andere Game-States (Starting, PlayerDied etc.) → zum Menü
                GameVm.OnDisappearing();
                HideAll();
                IsMainMenuActive = true;
                IsAdBannerVisible = _adService.BannerVisible;
                MenuVm.OnAppearing();
            }
            return true;
        }

        // 4. Settings → zurück (zum Spiel oder Menü)
        if (IsSettingsActive)
        {
            NavigateTo("..");
            return true;
        }

        // 5. Alle anderen Sub-Views → zurück zum Hauptmenü
        if (IsGameOverActive || IsLevelSelectActive || IsHighScoresActive ||
            IsHelpActive || IsShopActive || IsAchievementsActive || IsDailyChallengeActive || IsVictoryActive || IsLuckySpinActive || IsWeeklyChallengeActive || IsStatisticsActive || IsQuickPlayActive || IsDeckActive || IsDungeonActive || IsBattlePassActive || IsCollectionActive || IsLeagueActive)
        {
            HideAll();
            IsMainMenuActive = true;
            IsAdBannerVisible = _adService.BannerVisible;
            MenuVm.OnAppearing();
            return true;
        }

        // 6. Hauptmenü → Double-Back-to-Exit
        if (IsMainMenuActive)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastBackPressTime).TotalSeconds < 2)
                return false; // App schließen

            _lastBackPressTime = now;
            var msg = _localizationService.GetString("PressBackAgainToExit") ?? "Press back again to exit";
            ExitHintRequested?.Invoke(msg);
            return true;
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DIALOGS
    // ═══════════════════════════════════════════════════════════════════════

    private void ShowAlertDialog(string title, string message, string buttonText)
    {
        AlertDialogTitle = title;
        AlertDialogMessage = message;
        AlertDialogButtonText = buttonText;
        IsAlertDialogVisible = true;
    }

    [RelayCommand]
    private void DismissAlert()
    {
        IsAlertDialogVisible = false;
    }

    private Task<bool> ShowConfirmDialog(string title, string message, string acceptText, string cancelText)
    {
        ConfirmDialogTitle = title;
        ConfirmDialogMessage = message;
        ConfirmDialogAcceptText = acceptText;
        ConfirmDialogCancelText = cancelText;
        _confirmDialogTcs = new TaskCompletionSource<bool>();
        IsConfirmDialogVisible = true;
        return _confirmDialogTcs.Task;
    }

    [RelayCommand]
    private void AcceptConfirm()
    {
        IsConfirmDialogVisible = false;
        _confirmDialogTcs?.TrySetResult(true);
    }

    [RelayCommand]
    private void CancelConfirm()
    {
        IsConfirmDialogVisible = false;
        _confirmDialogTcs?.TrySetResult(false);
    }

    /// <summary>
    /// Benannter Handler fuer AdUnavailable (statt Lambda, damit Unsubscribe moeglich)
    /// </summary>
    private void OnAdUnavailable()
    {
        ShowAlertDialog(AppStrings.AdVideoNotAvailableTitle, AppStrings.AdVideoNotAvailableMessage, AppStrings.OK);
    }
}
