using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Extensions;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Controls;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Loading;
using HandwerkerImperium.Resources.Strings;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
using HandwerkerImperium.ViewModels.Guild;
using HandwerkerImperium.ViewModels.MiniGames;
using HandwerkerImperium.Views;
using MeineApps.UI.SkiaSharp;

namespace HandwerkerImperium;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Factory fuer plattformspezifischen IRewardedAdService (Android setzt RewardedAdHelper).
    /// Nimmt IServiceProvider entgegen fuer Lazy-Resolution von Abhaengigkeiten.
    /// </summary>
    public static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IPurchaseService (Android setzt AndroidPurchaseService).
    /// </summary>
    public static Func<IServiceProvider, IPurchaseService>? PurchaseServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IAudioService (Android setzt nativen Audio-Service).
    /// </summary>
    public static Func<IServiceProvider, IAudioService>? AudioServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen INotificationService (Android setzt nativen Notification-Service).
    /// </summary>
    public static Func<IServiceProvider, INotificationService>? NotificationServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IPlayGamesService (Android setzt Google Play Games Service).
    /// </summary>
    public static Func<IServiceProvider, IPlayGamesService>? PlayGamesServiceFactory { get; set; }

    /// <summary>
    /// Statisches Event fuer Review-Prompt (MainActivity verdrahtet den In-App-Review-Flow).
    /// </summary>
    public static Action? ReviewPromptRequested;

    /// <summary>
    /// F-20: Plattform-Hook fuer FLAG_KEEP_SCREEN_ON (Android).
    /// MainActivity setzt das Lambda; SettingsViewModel ruft es bei Toggle/Resume auf.
    /// Pass-Spieler-Sweetener — Aufrufer prueft IsPremium.
    /// </summary>
    public static Action<bool>? PlatformKeepScreenOn { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        // Plattform-Default fuer FpsProfile: Android startet auf Medium (Battery-schonender Standard),
        // Desktop auf High. Wird bei geladenem SaveGame durch SettingsData.GraphicsQuality ueberschrieben.
        Graphics.FpsProfile.Current = OperatingSystem.IsAndroid()
            ? Models.Enums.GraphicsQuality.Medium
            : Models.Enums.GraphicsQuality.High;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Setup DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Logger für statische AsyncExtensions setzen (Release-sicheres Logging)
        Helpers.AsyncExtensions.Logger = Services.GetService<ILogService>();

        // Ascension GS-Bonus + Challenge-Constraints an GameStateService anbinden (vermeidet zirkuläre DI-Abhängigkeit)
        var ascensionService = Services.GetService<IAscensionService>();
        if (ascensionService != null && Services.GetService<IGameStateService>() is GameStateService gss)
        {
            gss.ExternalGoldenScrewBonusProvider = ascensionService.GetGoldenScrewBonus;
            gss.ChallengeConstraints = Services.GetService<IChallengeConstraintService>();
        }

        // v2.0.36: MasteryService eager auflösen, damit er sich auf PerfectRatingIncremented
        // subscribed (passiert im Constructor). Ohne expliziten Resolve waere er erst nach
        // dem ersten MainViewModel-Resolve aktiv — und nur falls er injiziert wird.
        _ = Services.GetService<IMiniGameMasteryService>();

        // Initialize localization
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

        // Farb-Cache für SkiaSharp initialisieren
        SkiaThemeHelper.RefreshColors();

        // Statische Renderer mit AI-Asset-Service initialisieren
        var assetService = Services.GetService<IGameAssetService>();
        GameAssetService.Current = assetService;
        if (assetService != null)
        {
            MeisterHansRenderer.Initialize(assetService);
            WorkerAvatarRenderer.InitializeAssetService(assetService);
            WorkshopGameCardRenderer.Initialize(assetService);
            Icons.GameIcon.Initialize(assetService);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            var splash = CreateSplash();
            var panel = new Panel();
            panel.Children.Add(new MainView());
            panel.Children.Add(splash);
            desktop.MainWindow.Content = panel;
            RunLoadingAsync(splash).SafeFireAndForget();

            // Desktop: Beim Herunterfahren alle IDisposable-Singletons disposen
            desktop.ShutdownRequested += (_, _) => DisposeServices();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var splash = CreateSplash();
            var panel = new Panel();
            panel.Children.Add(new MainView());
            panel.Children.Add(splash);
            singleViewPlatform.MainView = panel;
            RunLoadingAsync(splash).SafeFireAndForget();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static SkiaLoadingSplash CreateSplash()
    {
        // Version aus Assembly lesen statt hardcoded
        var version = typeof(App).Assembly.GetName().Version;
        var appVersion = version != null
            ? $"v{version.Major}.{version.Minor}.{version.Build}"
            : "v2.0.31";

        return new SkiaLoadingSplash
        {
            AppName = "HandwerkerImperium",
            AppVersion = appVersion,
            Renderer = new HandwerkerImperiumSplashRenderer()
        };
    }

    private async Task RunLoadingAsync(SkiaLoadingSplash splash)
    {
        try
        {
            var pipeline = new HandwerkerImperiumLoadingPipeline(Services);
            pipeline.ProgressChanged += (progress, text) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    splash.Progress = progress;
                    splash.StatusText = text;
                });

            var sw = Stopwatch.StartNew();
            await pipeline.ExecuteAsync();

            // Mindestens 800ms anzeigen damit die Splash-Animation sichtbar ist
            var remaining = Models.GameBalanceConstants.SplashMinimumDisplayMs - (int)sw.ElapsedMilliseconds;
            if (remaining > 0) await Task.Delay(remaining);

            var mainVm = Services.GetRequiredService<MainViewModel>();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                    && desktop.MainWindow != null)
                    desktop.MainWindow.DataContext = mainVm;
                else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform
                         && singleViewPlatform.MainView != null)
                    singleViewPlatform.MainView.DataContext = mainVm;

                splash.FadeOut();
            });
        }
        catch
        {
            // Loading-Fehler still behandelt, Splash trotzdem ausblenden
            Avalonia.Threading.Dispatcher.UIThread.Post(() => splash.FadeOut());
        }
    }

    private static bool _servicesDisposed;

    /// <summary>
    /// Disposed alle IDisposable-Singletons.
    /// Kritische Services (Game-Loop + GPU-Ressourcen) werden explizit zuerst disposed,
    /// danach uebernimmt der ServiceProvider.Dispose() ALLE uebrigen IDisposable-Singletons automatisch
    /// (verhindert Silent-Leaks bei neuen Services — keine manuelle Liste mehr noetig).
    /// Idempotent via _servicesDisposed-Flag (OnDestroy kann mehrfach feuern).
    /// </summary>
    public static void DisposeServices()
    {
        if (Services == null || _servicesDisposed) return;
        _servicesDisposed = true;

        try
        {
            // 1. GameLoopService MUSS zuerst stoppen — sonst tickt er noch waehrend andere Services
            //    im ServiceProvider.Dispose()-Durchlauf bereits gecleaned sind (NRE/ODE-Gefahr).
            (Services.GetService<IGameLoopService>() as IDisposable)?.Dispose();

            // 2. GPU-Ressourcen (SKPaint/SKFont/SKPath) deterministisch freigeben BEVOR
            //    der Provider die Render-subscriberenden VMs kippt.
            (Services.GetService<GameJuiceEngine>() as IDisposable)?.Dispose();

            // 3. Gesamten DI-Container disposen: automatisch alle registrierten IDisposable-Singletons
            //    (inkl. MainViewModel, SeasonalEventService, BattlePassService, FirebaseService,
            //    alle Guild-Services, Achievement/Challenge/Mission-Services, ShopViewModel etc.).
            //    Reverse-Resolution-Order — genau die richtige Dispose-Reihenfolge.
            if (Services is IDisposable providerDisposable)
                providerDisposable.Dispose();

            // 4. Icon-System: Static Cleanup (nicht im DI-Container registriert)
            Icons.GameIcon.ClearCache();
            Icons.GameIconRenderer.Cleanup();

            // 5. Statische Renderer-Caches (Shader/MaskFilter/Path/Bitmaps in static Feldern,
            //    nicht im DI-Container) deterministisch freigeben — die DisposeStaticResources-
            //    Methoden waren dokumentiert ("bei App-Shutdown aufrufen"), wurden aber nie aufgerufen.
            Graphics.InventGameRenderer.DisposeStaticResources();
            Graphics.BlueprintGameRenderer.DisposeStaticResources();
            Graphics.CraftTextures.DisposeStaticResources();
            Graphics.FireworksRenderer.DisposeStaticResources();
            Graphics.GameCardRenderer.DisposeStaticResources();
            Graphics.LoadingScreenRenderer.DisposeStaticResources();
        }
        catch
        {
            // Fehler beim Dispose beim Herunterfahren sind unkritisch
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("HandwerkerImperium"));

        // AI-Asset-Loading (WebP-Bitmaps mit LRU-Cache)
        services.AddSingleton<IGameAssetService, GameAssetService>();

        // Premium Services (Ads, Purchases)
        services.AddMeineAppsPremium();

        // Android-Override: Echte Rewarded Ads + Google Play Billing statt Desktop-Defaults.
        // WICHTIG (Avalonia 12): Die Factory-Pruefung MUSS lazy IM Resolve-Lambda passieren, NICHT
        // als Build-Zeit-Guard (if Factory != null). ConfigureServices + BuildServiceProvider laufen
        // in AvaloniaAndroidApplication.OnCreate (Application-Ebene) VOR MainActivity.OnCreate, wo die
        // Platform-Factories gesetzt werden. Ein Build-Zeit-Guard brennt sonst dauerhaft den
        // Desktop-Simulator/-Stub ein: RewardedAdService liefert true OHNE Video (Belohnung trotzdem),
        // IAP/Restore tot. Das Lambda liest die Factory erst beim ersten Resolve (MainViewModel-Graph
        // in der Loading-Pipeline, via Dispatcher.UIThread.InvokeAsync) — also NACH der Setzung.
        services.AddSingleton<IRewardedAdService>(sp =>
            RewardedAdServiceFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance<RewardedAdService>(sp));
        services.AddSingleton<IPurchaseService>(sp =>
            PurchaseServiceFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance<PurchaseService>(sp));

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // Frame-Clock (P1): zentraler 30Hz-Render-Tick fuer Visual-Renderer
        services.AddSingleton<IFrameClock, FrameClockService>();

        // UI-Effekt-Bus: entkoppelt FloatingText/Celebration/Ceremony-Ausloeser von den
        // View-Sinks. Auslöser injizieren IUiEffectBus, Views abonnieren ihn im Code-Behind.
        services.AddSingleton<IUiEffectBus, UiEffectBus>();

        // Eternal Mastery (Long-Term-Engagement): permanenter Bonus pro Prestige
        services.AddSingleton<IEternalMasteryService, EternalMasteryService>();

        // Bounded-Context-Facaden mit echten Konsumenten (Service-Sprawl).
        // GuildFacade: 9 Subsysteme — Konsument GuildViewModel.
        // MissionsFacade: 5 Subsysteme — Konsument MissionsFeatureViewModel.
        // Weitere Facaden (Worker/Progression/Content/Platform/Onboarding) waren am
        // 12.05.2026 spekulativ angelegt und ohne Konsument — geloescht im Review-Pass.
        services.AddSingleton<IMissionsFacade, MissionsFacade>();

        // Game Services
        services.AddSingleton<IGameStateService, GameStateService>();
        services.AddSingleton<IGameIntegrityService, GameIntegrityService>();
        services.AddSingleton<ISaveGameService, SaveGameService>();
        services.AddSingleton<IGameLoopService, GameLoopService>();
        services.AddSingleton<IAchievementService, AchievementService>();
        // v2.0.36: Mini-Game-Mastery (Bronze/Silver/Gold pro Mini-Game-Type) — subscribed
        // direkt auf IGameStateService.PerfectRatingIncremented im Constructor.
        services.AddSingleton<IMiniGameMasteryService, MiniGameMasteryService>();

        // Android-Override (lazy IM Lambda, siehe Avalonia-12-Hinweis beim RewardedAd-Block oben):
        // Audio/Notification/Play-Games fallen sonst auf ihre Desktop-Defaults zurueck, weil der
        // Build-Zeit-Guard zur ConfigureServices-Zeit greift, bevor MainActivity.OnCreate die
        // Factories setzt. Fallback via ActivatorUtilities (konstruiert den Default-Typ DI-aufgeloest).
        services.AddSingleton<IAudioService>(sp =>
            AudioServiceFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance<AudioService>(sp));
        services.AddSingleton<INotificationService>(sp =>
            NotificationServiceFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance<NotificationService>(sp));
        services.AddSingleton<IPlayGamesService>(sp =>
            PlayGamesServiceFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance<PlayGamesService>(sp));

        services.AddSingleton<IDailyRewardService, DailyRewardService>();
        services.AddSingleton<IIncomeCalculatorService, IncomeCalculatorService>();
        services.AddSingleton<IOfflineProgressService, OfflineProgressService>();
        services.AddSingleton<IOrderGeneratorService, OrderGeneratorService>();
        services.AddSingleton<IPrestigeService, PrestigeService>();
        services.AddSingleton<IChallengeConstraintService, ChallengeConstraintService>();
        services.AddSingleton<IContextualHintService, ContextualHintService>();
        // v2.0.36: Notification-Center (Bell-UI) ersetzt Dialog-Stacking beim Re-Open.
        services.AddSingleton<INotificationCenterService, NotificationCenterService>();
        services.AddSingleton<NotificationCenterViewModel>();
        // WhatsNew-Dialog fuer Update-Spieler.
        services.AddSingleton<IWhatsNewService, WhatsNewService>();
        // v2.1.0: Reputation-Shop (3. Waehrung neben Geld + GS).
        services.AddSingleton<IReputationShopService, ReputationShopService>();
        services.AddSingleton<ReputationShopViewModel>();
        // v2.1.0 Sprint-3 Big Bets: Co-op-Auftraege + Worker-Auktionen via Firebase.
        services.AddSingleton<IGuildCoopOrderService, GuildCoopOrderService>();
        services.AddSingleton<IWorkerAuctionService, WorkerAuctionService>();
        services.AddSingleton<ViewModels.Guild.GuildCoopOrderViewModel>();
        // V7 (, Plan Section 3.9): Mega-Projekt-VM
        services.AddSingleton<ViewModels.Guild.GuildMegaProjectViewModel>();
        services.AddSingleton<ViewModels.Auctions.WorkerAuctionViewModel>();

        // New Game Services (v2.0)
        services.AddSingleton<IWorkerService, WorkerService>();
        services.AddSingleton<IBuildingService, BuildingService>();
        services.AddSingleton<IResearchService, ResearchService>();
        services.AddSingleton<IEventService, EventService>();
        services.AddSingleton<IQuickJobService, QuickJobService>();
        services.AddSingleton<IDailyChallengeService, DailyChallengeService>();
        services.AddSingleton<IStoryService, StoryService>();
        services.AddSingleton<IReviewService, ReviewService>();

        // Visual Effects Engine
        services.AddSingleton<GameJuiceEngine>();

        // Neue Feature-Services (8)
        services.AddSingleton<IWeeklyMissionService, WeeklyMissionService>();
        services.AddSingleton<IWelcomeBackService, WelcomeBackService>();
        services.AddSingleton<ILuckySpinService, LuckySpinService>();
        services.AddSingleton<IEquipmentService, EquipmentService>();
        services.AddSingleton<IManagerService, ManagerService>();
        services.AddSingleton<ITournamentService, TournamentService>();
        services.AddSingleton<ISeasonalEventService, SeasonalEventService>();
        services.AddSingleton<IBattlePassService, BattlePassService>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IFirebaseService, FirebaseService>();

        // Telemetrie-Infrastruktur (REST via FirebaseService — plattformuebergreifend, keine nativen SDKs).
        services.AddSingleton<IAnalyticsService, AnalyticsService>();
        services.AddSingleton<IRemoteConfigService, RemoteConfigService>();
        services.AddSingleton<ICloudSaveService, CloudSaveService>();

        // Cross-Promotion zwischen den 11 eigenen Apps (House-Ads, zero-cost).
        services.AddSingleton<ICrossPromoService, CrossPromoService>();
        services.AddSingleton<CrossPromoViewModel>();

        // FTUE-Service + UI-Spotlight-Overlay (10-Step-Tutorial).
        services.AddSingleton<IFtueService, FtueService>();
        services.AddSingleton<FtueOverlayViewModel>();
        // FTUE-Progress-Verdrahtung mit Game-Events (F-03): ohne diesen Tracker
        // schreitet die FTUE nicht voran und Start() wird nie aufgerufen.
        services.AddSingleton<FtueProgressTracker>();

        // Friend-Invite Reward-Loop (K-Factor-Driver, ~30% Free-Installs bei
        // Voodoo / Lion). Server-Endpoint fuer Anti-Cheat ist Folge-Sprint.
        services.AddSingleton<IReferralService, ReferralService>();
        // F-02: Settings-Card UI fuer Referral (Code anzeigen / teilen / eingeben / Tier-Belohnung)
        services.AddSingleton<ReferralCardViewModel>();

        // Limited-Time-Events (FOMO + Re-Engagement). 4 Templates:
        // DoubleReward, BossRush, CoopMarathon, MiniGameMastery.
        services.AddSingleton<ILiveEventService, LiveEventService>();
        // F-01: Score-Tracker verdrahtet AddScore() mit OrderCompleted / PerfectRatingIncremented.
        // Singleton, IDisposable — wird ueber Container-Dispose aufgeraeumt.
        services.AddSingleton<LiveEventScoreTracker>();
        // F-16: Dashboard-Banner-Chip fuer Live-Events (Titel, Score, Countdown, Tap-to-Claim).
        services.AddSingleton<LiveEventBannerViewModel>();

        // Cinematic-Logik aus
        // MainViewModel extrahiert. Eigener Coordinator subscribed auf PrestigeService
        // und hat die Lokalisierung + Audio-Track-Steuerung.
        services.AddSingleton<ICinematicCoordinator, CinematicCoordinator>();

        // Reputation-Tier-Up-Effekte (FloatingText/Celebration/Audio/Achievement-Dialog)
        // aus MainViewModel.OnReputationTierChanged extrahiert.
        services.AddSingleton<IReputationTierEffects, ReputationTierEffects>();

        // Spielstart-Sequenz (Load, Cloud-Save, Welcome-Flow, GameLoop-Start)
        // aus MainViewModel.Init.cs extrahiert.
        services.AddSingleton<IGameStartupCoordinator, GameStartupCoordinator>();

        // Progression-Feedback (Level/Prestige/Workshop/Worker/MasterTool/Achievement)
        // aus MainViewModel.EventHandlers.cs extrahiert — subscribed selbst auf die Service-Events.
        services.AddSingleton<IProgressionFeedbackCoordinator, ProgressionFeedbackCoordinator>();

        // Per-Tick-UI-Orchestrierung (1 Hz) aus MainViewModel.GameTick.cs extrahiert.
        services.AddSingleton<IGameTickCoordinator, GameTickCoordinator>();
        // Daily-Bundle-Foundation (UI-Wiring kommt in spaeterem Sprint)
        services.AddSingleton<IDailyBundleService, DailyBundleService>();

        services.AddSingleton<IGuildService, GuildService>();
        services.AddSingleton<IGuildInviteService, GuildInviteService>();
        services.AddSingleton<ICraftingService, CraftingService>();
        services.AddSingleton<IAutoProductionService, AutoProductionService>();
        // V7 (): Lager-Service mit Slots/Stack-Limits/Reservierung
        services.AddSingleton<IWarehouseService, WarehouseService>();
        // Lazy-Wrapper bricht den DI-Zirkel CraftingService <-> WarehouseService (WarehouseService
        // haengt von ICraftingService ab; CraftingService braucht die effektiven Lager-Grenzen).
        services.AddSingleton(sp => new Lazy<IWarehouseService>(sp.GetRequiredService<IWarehouseService>));
        // V7 (): Material-Markt
        services.AddSingleton<IMarketService, MarketService>();
        // V7 (, Plan Section 3.9): Gilden-Mega-Projekte
        services.AddSingleton<IGuildMegaProjectService, GuildMegaProjectService>();
        // V7 (Plan Section 7.2): Procedural Material-Icon-Renderer (ersetzt externe AI-Assets)
        services.AddSingleton<HandwerkerImperium.Graphics.MaterialIconRenderer>();

        // Late-Game-Services (Ascension, Rebirth, VIP)
        services.AddSingleton<IAscensionService, AscensionService>();
        services.AddSingleton<IRebirthService, RebirthService>();
        services.AddSingleton<IVipService, VipService>();

        // Masterplan-Services
        // LeaderboardService + BountyService entfernt (19.03.2026): Keine UI-Integration, kein Aufrufer
        services.AddSingleton<IGuildChatService, GuildChatService>();
        services.AddSingleton<IGoalService, GoalService>();

        // Gilden-Overhaul Services 
        services.AddSingleton<IGuildResearchService, GuildResearchService>();
        services.AddSingleton<IGuildWarSeasonService, GuildWarSeasonService>();
        services.AddSingleton<IGuildHallService, GuildHallService>();
        services.AddSingleton<IGuildBossService, GuildBossService>();
        services.AddSingleton<IGuildTipService, GuildTipService>();
        services.AddSingleton<IGuildAchievementService, GuildAchievementService>();
        services.AddSingleton<IGuildTickService, GuildTickService>();

        // Facade uber alle 7 Gilden-Services - reduziert GuildViewModel-Ctor von 14 auf 7 Parameter
        services.AddSingleton<IGuildFacade, GuildFacade>();

        // Phase-1-Services (MainViewModel-Zerlegung, 17.04.2026). Delegieren vorerst an MainViewModel.
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogOrchestrator, DialogOrchestrator>();
        services.AddSingleton<IMiniGameNavigator, MiniGameNavigator>();

        // ViewModels (Singleton because MainViewModel holds references to child VMs)
        services.AddSingleton<MiniGameViewModels>();
        services.AddSingleton<DialogViewModel>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogViewModel>());
        services.AddSingleton<MissionsFeatureViewModel>();

        // Feature-VMs ( der MainViewModel-Zerlegung, 17.04.2026)
        services.AddSingleton<GoalBannerViewModel>();
        services.AddSingleton<HeaderViewModel>();
        services.AddSingleton<PrestigeBannerViewModel>();
        services.AddSingleton<WelcomeFlowViewModel>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<AchievementsViewModel>();
        services.AddSingleton<OrderViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ShopViewModel>();
        services.AddSingleton<StatisticsViewModel>();
        services.AddSingleton<WorkshopViewModel>();
        services.AddSingleton<SawingGameViewModel>();
        services.AddSingleton<PipePuzzleViewModel>();
        services.AddSingleton<WiringGameViewModel>();
        services.AddSingleton<PaintingGameViewModel>();
        services.AddSingleton<RoofTilingGameViewModel>();
        services.AddSingleton<BlueprintGameViewModel>();
        services.AddSingleton<DesignPuzzleGameViewModel>();
        services.AddSingleton<InspectionGameViewModel>();
        services.AddSingleton<ForgeGameViewModel>();
        services.AddSingleton<InventGameViewModel>();
        services.AddSingleton<WorkerMarketViewModel>();
        services.AddSingleton<WorkerProfileViewModel>();
        services.AddSingleton<BuildingsViewModel>();
        services.AddSingleton<ResearchViewModel>();
        services.AddSingleton<ManagerViewModel>();
        services.AddSingleton<TournamentViewModel>();
        services.AddSingleton<SeasonalEventViewModel>();
        services.AddSingleton<BattlePassViewModel>();
        services.AddSingleton<GuildViewModel>();
        services.AddSingleton<GuildWarSeasonViewModel>();
        services.AddSingleton<GuildBossViewModel>();
        services.AddSingleton<GuildHallViewModel>();
        // Thin-Wrapper-Sub-VMs ( 17.04.2026) werden im GuildViewModel-Ctor manuell erstellt
        // (zirkuläre DI vermieden: Sub-VM haelt Referenz auf Parent-GuildViewModel).
        services.AddSingleton<CraftingViewModel>();
        services.AddSingleton<WarehouseSectionViewModel>();
        // V7 (): Markt-Sub-Tab im Shop
        services.AddSingleton<MarketViewModel>();
        services.AddSingleton<LuckySpinViewModel>();
        services.AddSingleton<AscensionViewModel>();
    }
}
