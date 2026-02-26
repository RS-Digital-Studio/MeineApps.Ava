using BomberBlast.Core;
using BomberBlast.Resources.Strings;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// Haupt-ViewModel für Navigation zwischen Views (MainMenu, Game, LevelSelect, etc.).
/// Zeigt jeweils nur eine Child-View an.
/// Hält alle Child-ViewModels für den korrekten DataContext.
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
    public ProfileViewModel ProfileVm { get; }
    public GemShopViewModel GemShopVm { get; }

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
    private bool _isShopActive;

    [ObservableProperty]
    private bool _isVictoryActive;

    [ObservableProperty]
    private bool _isStatisticsActive;

    [ObservableProperty]
    private bool _isQuickPlayActive;

    [ObservableProperty]
    private bool _isDungeonActive;

    [ObservableProperty]
    private bool _isBattlePassActive;

    [ObservableProperty]
    private bool _isLeagueActive;

    [ObservableProperty]
    private bool _isProfileActive;

    [ObservableProperty]
    private bool _isGemShopActive;

    /// <summary>
    /// Kombinierte View: Deck + Sammlung
    /// </summary>
    [ObservableProperty]
    private bool _isCardsActive;

    /// <summary>
    /// Kombinierte View: Tägliche Herausforderung + Missionen
    /// </summary>
    [ObservableProperty]
    private bool _isChallengesActive;

    // ═══════════════════════════════════════════════════════════════════════
    // TAB-STATE PROPERTIES (für kombinierte Views)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Shop: false=Shop-Tab, true=Glücksrad-Tab</summary>
    [ObservableProperty]
    private bool _isShopSpinTab;

    /// <summary>Profil: false=Profil-Tab, true=Erfolge-Tab</summary>
    [ObservableProperty]
    private bool _isProfileAchievementsTab;

    /// <summary>Einstellungen: false=Settings-Tab, true=Hilfe-Tab</summary>
    [ObservableProperty]
    private bool _isSettingsHelpTab;

    /// <summary>Karten: false=Deck-Tab, true=Sammlung-Tab</summary>
    [ObservableProperty]
    private bool _isCardsCollectionTab;

    /// <summary>Herausforderungen: false=Daily-Tab, true=Missions-Tab</summary>
    [ObservableProperty]
    private bool _isChallengesMissionsTab;

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
    /// Merkt ob Einstellungen aus dem Spiel geöffnet wurden (für Zurück-Navigation).
    /// </summary>
    private bool _returnToGameFromSettings;

    private readonly ILocalizationService _localizationService;
    private readonly IAdService _adService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IAchievementService _achievementService;
    private readonly ICoinService _coinService;
    private readonly IPurchaseService _purchaseService;
    private readonly ICloudSaveService _cloudSaveService;
    private readonly SoundManager _soundManager;
    private readonly IAppLogger _logger;

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
        ProfileViewModel profileVm,
        GemShopViewModel gemShopVm,
        ILocalizationService localization,
        IAdService adService,
        IPurchaseService purchaseService,
        IRewardedAdService rewardedAdService,
        IAchievementService achievementService,
        ICoinService coinService,
        ICloudSaveService cloudSaveService,
        SoundManager soundManager,
        IAppLogger logger)
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
        ProfileVm = profileVm;
        GemShopVm = gemShopVm;
        _localizationService = localization;
        _adService = adService;
        _purchaseService = purchaseService;
        _rewardedAdService = rewardedAdService;
        _achievementService = achievementService;
        _coinService = coinService;
        _cloudSaveService = cloudSaveService;
        _soundManager = soundManager;
        _logger = logger;

        // GameOverVm-spezifische Events (Confirmation Dialog)
        GameOverVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);

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
            var msg = localization.GetString("ShopNotEnoughCoins") ?? "Not enough coins!";
            FloatingTextRequested?.Invoke(msg, "error");
        };

        // Ad-Banner: In BomberBlast (Landscape) kein Banner - nur Rewarded Ads
        IsAdBannerVisible = false;
        if (adService.AdsEnabled)
            adService.HideBanner();

        // Ad-Unavailable Meldung anzeigen (benannte Methode statt Lambda fuer Unsubscribe)
        _rewardedAdService.AdUnavailable += OnAdUnavailable;

        // PauseVM Resume/Restart Events mit GameVM verbinden
        pauseVm.ResumeRequested += () => gameVm.ResumeCommand.Execute(null);
        pauseVm.RestartRequested += () => gameVm.RestartCommand.Execute(null);

        // Navigation + Game Juice Events per Interface verdrahten (kein Reflection)
        INavigable[] allVms = [menuVm, gameVm, levelSelectVm, settingsVm, highScoresVm, gameOverVm,
            pauseVm, helpVm, shopVm, achievementsVm, dailyChallengeVm, victoryVm, luckySpinVm,
            weeklyChallengeVm, statisticsVm, quickPlayVm, deckVm, dungeonVm, battlePassVm,
            collectionVm, leagueVm, profileVm, gemShopVm];
        foreach (var vm in allVms)
        {
            vm.NavigationRequested += request => NavigateTo(request);
            if (vm is IGameJuiceEmitter emitter)
            {
                emitter.FloatingTextRequested += (text, type) => FloatingTextRequested?.Invoke(text, type);
                emitter.CelebrationRequested += () => CelebrationRequested?.Invoke();
            }
        }

        // FloatingText für VMs die nur FloatingText haben (kein IGameJuiceEmitter)
        GameOverVm.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        ProfileVm.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);

        // Battle Pass Premium-Kauf anfordern
        BattlePassVm.PremiumPurchaseRequested += async () =>
        {
            var success = await _purchaseService.PurchaseConsumableAsync("battle_pass_premium");
            if (success)
                BattlePassVm.OnPremiumPurchaseConfirmed();
        };

        // Dungeon Ad-Run: Rewarded Ad zeigen und bei Erfolg melden (Cooldown beachten)
        DungeonVm.AdRunRequested += async () =>
        {
            var result = await _rewardedAdService.ShowAdAsync("dungeon_run");
            if (result)
            {
                RewardedAdCooldownTracker.RecordAdShown();
                DungeonVm.OnAdRunRewarded();
            }
        };

        // Dialog-Events von SettingsVM + ShopVM verdrahten
        settingsVm.AlertRequested += (t, m, b) => ShowAlertDialog(t, m, b);
        settingsVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);
        shopVm.MessageRequested += (t, m) => ShowAlertDialog(t, m, "OK");
        shopVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);
        shopVm.FloatingTextRequested += (text, type) => FloatingTextRequested?.Invoke(text, type);
        gemShopVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);

        localization.LanguageChanged += (_, _) =>
        {
            // Child VMs lesen lokalisierte Texte beim nächsten OnAppearing neu
            MenuVm.OnAppearing();
            ShopVm.UpdateLocalizedTexts();
            QuickPlayVm.UpdateLocalizedTexts();
            DeckVm.UpdateLocalizedTexts();
            DungeonVm.UpdateLocalizedTexts();
            BattlePassVm.UpdateLocalizedTexts();
            CollectionVm.UpdateLocalizedTexts();
            LeagueVm.UpdateLocalizedTexts();
            ProfileVm.UpdateLocalizedTexts();
            GemShopVm.UpdateLocalizedTexts();
        };

        // Cloud Save: Bei App-Start Cloud-Stand laden (fire-and-forget mit Error-Handling)
        _ = Task.Run(async () =>
        {
            try { await _cloudSaveService.TryLoadFromCloudAsync(); }
            catch (Exception ex) { _logger?.LogWarning($"CloudSave Init fehlgeschlagen: {ex.Message}"); }
        });

        // Menü initialisieren
        menuVm.OnAppearing();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Typsichere Navigation: Konvertiert NavigationRequest in Route-String
    /// und delegiert an die bestehende String-basierte NavigateTo-Methode.
    /// </summary>
    public void NavigateTo(NavigationRequest request)
    {
        var route = request switch
        {
            GoMainMenu => "MainMenu",
            GoLevelSelect => "LevelSelect",
            GoSettings => "Settings",
            GoShop => "Shop",
            GoAchievements => "Achievements",
            GoHighScores => "HighScores",
            GoHelp => "Help",
            GoStatistics => "Statistics",
            GoProfile => "Profile",
            GoDailyChallenge => "DailyChallenge",
            GoLuckySpin => "LuckySpin",
            GoQuickPlay => "QuickPlay",
            GoWeeklyChallenge => "WeeklyChallenge",
            GoCollection => "Collection",
            GoDeck => "Deck",
            GoDungeon => "Dungeon",
            GoBattlePass => "BattlePass",
            GoLeague => "League",
            GoGemShop => "GemShop",
            GoBack => "..",
            GoGame g => $"Game?mode={g.Mode}&level={g.Level}&difficulty={g.Difficulty}&continue={g.Continue}&boost={g.Boost}&floor={g.Floor}&seed={g.Seed}",
            GoGameOver go => $"GameOver?score={go.Score}&level={go.Level}&highscore={go.IsHighScore}&mode={go.Mode}&coins={go.Coins}&levelcomplete={go.LevelComplete}&cancontinue={go.CanContinue}&enemypts={go.EnemyPoints}&timebonus={go.TimeBonus}&effbonus={go.EfficiencyBonus}&multiplier={go.Multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)}&kills={go.Kills}&survivaltime={go.SurvivalTime.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            GoVictory v => $"Victory?score={v.Score}&coins={v.Coins}",
            GoResetThen r => $"//MainMenu/{NavigationRequestToRoute(r.Then)}",
            _ => "MainMenu"
        };
        NavigateToRoute(route);
    }

    /// <summary>
    /// Hilfsmethode für GoResetThen: Konvertiert inneren Request in Route-String.
    /// </summary>
    private string NavigationRequestToRoute(NavigationRequest request)
    {
        // Rekursiv: gleiche Logik wie NavigateTo, aber gibt String zurück
        return request switch
        {
            GoGame g => $"Game?mode={g.Mode}&level={g.Level}&difficulty={g.Difficulty}&continue={g.Continue}&boost={g.Boost}&floor={g.Floor}&seed={g.Seed}",
            GoMainMenu => "MainMenu",
            _ => "MainMenu"
        };
    }

    /// <summary>
    /// Navigiert zu einer bestimmten View. Versteckt alle anderen.
    /// Unterstützt Routen wie "Game?mode=story&amp;level=5" und
    /// zusammengesetzte Routen wie "//MainMenu/Game?mode=story".
    /// </summary>
    public async void NavigateToRoute(string route)
    {
        try
        {
        // Zusammengesetzte Routen behandeln (z.B. "//MainMenu/Game?mode=story")
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

        // Aktuellen Zustand merken (für Zurück-Navigation)
        var wasGameActive = IsGameActive;

        // Lifecycle: Game-Loop stoppen beim Verlassen der Game-View
        if (wasGameActive && baseRoute != "Game")
        {
            GameVm.OnDisappearing();
        }

        HideAll();

        // Navigations-Sound (nicht beim Game-Start, das hat eigene Sounds)
        if (baseRoute != "Game")
            _soundManager.PlaySound(SoundManager.SFX_MENU_SELECT);

        // Kein Banner in BomberBlast (Landscape) - nur Rewarded Ads
        IsAdBannerVisible = false;

        switch (baseRoute)
        {
            case "MainMenu":
                _returnToGameFromSettings = false;
                IsMainMenuActive = true;
                MenuVm.OnAppearing();
                break;

            case "Game":
                IsGameActive = true;
                // Spiel-Parameter parsen
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
                // Spiel starten (Engine initialisieren + 60fps Loop starten)
                await GameVm.OnAppearingAsync();
                break;

            case "LevelSelect":
                IsLevelSelectActive = true;
                LevelSelectVm.OnAppearing();
                break;

            case "HighScores":
                IsHighScoresActive = true;
                HighScoresVm.OnAppearing();
                break;

            case "GameOver":
                IsGameOverActive = true;
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

            // Shop (kombiniert mit Glücksrad)
            case "Shop":
                IsShopActive = true;
                IsShopSpinTab = false;
                ShopVm.OnAppearing();
                break;

            case "LuckySpin":
                IsShopActive = true;
                IsShopSpinTab = true;
                LuckySpinVm.OnAppearing();
                break;

            // Profil (kombiniert mit Erfolge)
            case "Profile":
                IsProfileActive = true;
                IsProfileAchievementsTab = false;
                ProfileVm.OnAppearing();
                break;

            case "Achievements":
                IsProfileActive = true;
                IsProfileAchievementsTab = true;
                AchievementsVm.OnAppearing();
                break;

            // Einstellungen (kombiniert mit Hilfe)
            case "Settings":
                _returnToGameFromSettings = wasGameActive;
                IsSettingsActive = true;
                IsSettingsHelpTab = false;
                SettingsVm.OnAppearing();
                break;

            case "Help":
                IsSettingsActive = true;
                IsSettingsHelpTab = true;
                break;

            // Karten (Deck + Sammlung kombiniert)
            case "Cards":
            case "Deck":
                IsCardsActive = true;
                IsCardsCollectionTab = false;
                DeckVm.OnAppearing();
                break;

            case "Collection":
                IsCardsActive = true;
                IsCardsCollectionTab = true;
                CollectionVm.OnAppearing();
                break;

            // Herausforderungen (Daily + Missions kombiniert)
            case "Challenges":
            case "DailyChallenge":
                IsChallengesActive = true;
                IsChallengesMissionsTab = false;
                DailyChallengeVm.OnAppearing();
                break;

            case "WeeklyChallenge":
                IsChallengesActive = true;
                IsChallengesMissionsTab = true;
                WeeklyChallengeVm.OnAppearing();
                break;

            case "Statistics":
                IsStatisticsActive = true;
                StatisticsVm.OnAppearing();
                break;

            case "QuickPlay":
                IsQuickPlayActive = true;
                QuickPlayVm.OnAppearing();
                break;

            case "Dungeon":
                IsDungeonActive = true;
                DungeonVm.OnAppearing();
                break;

            case "BattlePass":
                IsBattlePassActive = true;
                BattlePassVm.OnAppearing();
                break;

            case "League":
                IsLeagueActive = true;
                LeagueVm.OnAppearing();
                break;

            case "GemShop":
                IsGemShopActive = true;
                GemShopVm.OnAppearing();
                break;

            case "Victory":
                IsVictoryActive = true;
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
                // Zurück-Navigation: zum Spiel zurückkehren wenn Einstellungen aus dem Spiel geöffnet wurden
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
                    MenuVm.OnAppearing();
                }
                break;

            default:
                IsMainMenuActive = true;
                MenuVm.OnAppearing();
                break;
        }
        }
        catch (Exception ex)
        {
            _logger.LogError($"NavigateTo Fehler bei Route '{route}'", ex);
            // Fallback: Zurück zum Hauptmenü damit die App nicht hängt
            try
            {
                HideAll();
                IsMainMenuActive = true;
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
        IsShopActive = false;
        IsVictoryActive = false;
        IsStatisticsActive = false;
        IsQuickPlayActive = false;
        IsDungeonActive = false;
        IsBattlePassActive = false;
        IsLeagueActive = false;
        IsProfileActive = false;
        IsGemShopActive = false;
        IsCardsActive = false;
        IsChallengesActive = false;

        // Tab-States zurücksetzen
        IsShopSpinTab = false;
        IsProfileAchievementsTab = false;
        IsSettingsHelpTab = false;
        IsCardsCollectionTab = false;
        IsChallengesMissionsTab = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TAB-SWITCHING (für kombinierte Views)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SwitchToShopTab() { IsShopSpinTab = false; ShopVm.OnAppearing(); }

    [RelayCommand]
    private void SwitchToSpinTab() { IsShopSpinTab = true; LuckySpinVm.OnAppearing(); }

    [RelayCommand]
    private void SwitchToProfileTab() { IsProfileAchievementsTab = false; ProfileVm.OnAppearing(); }

    [RelayCommand]
    private void SwitchToAchievementsTab() { IsProfileAchievementsTab = true; AchievementsVm.OnAppearing(); }

    [RelayCommand]
    private void SwitchToSettingsTab() { IsSettingsHelpTab = false; SettingsVm.OnAppearing(); }

    [RelayCommand]
    private void SwitchToHelpTab() { IsSettingsHelpTab = true; }

    [RelayCommand]
    private void SwitchToDeckTab() { IsCardsCollectionTab = false; DeckVm.OnAppearing(); }

    [RelayCommand]
    private void SwitchToCollectionTab() { IsCardsCollectionTab = true; CollectionVm.OnAppearing(); }

    [RelayCommand]
    private void SwitchToDailyChallengeTab() { IsChallengesMissionsTab = false; DailyChallengeVm.OnAppearing(); }

    [RelayCommand]
    private void SwitchToMissionsTab() { IsChallengesMissionsTab = true; WeeklyChallengeVm.OnAppearing(); }

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
                MenuVm.OnAppearing();
            }
            return true;
        }

        // 4. Settings → zurück (zum Spiel oder Menü)
        if (IsSettingsActive)
        {
            NavigateTo(new GoBack());
            return true;
        }

        // 5. Alle anderen Sub-Views → zurück zum Hauptmenü
        if (IsGameOverActive || IsLevelSelectActive || IsHighScoresActive ||
            IsShopActive || IsVictoryActive || IsStatisticsActive || IsQuickPlayActive ||
            IsDungeonActive || IsBattlePassActive || IsLeagueActive || IsProfileActive ||
            IsGemShopActive || IsCardsActive || IsChallengesActive)
        {
            HideAll();
            IsMainMenuActive = true;
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
