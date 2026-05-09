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

        // Android-Override: Echte Rewarded Ads statt Desktop-Simulator
        if (RewardedAdServiceFactory != null)
            services.AddSingleton<IRewardedAdService>(sp => RewardedAdServiceFactory!(sp));

        // Android-Override: Echte Google Play Billing statt Stub
        if (PurchaseServiceFactory != null)
            services.AddSingleton<IPurchaseService>(sp => PurchaseServiceFactory!(sp));

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // Game Services
        services.AddSingleton<IGameStateService, GameStateService>();
        services.AddSingleton<IGameIntegrityService, GameIntegrityService>();
        services.AddSingleton<ISaveGameService, SaveGameService>();
        services.AddSingleton<IGameLoopService, GameLoopService>();
        services.AddSingleton<IAchievementService, AchievementService>();
        // v2.0.36: Mini-Game-Mastery (Bronze/Silver/Gold pro Mini-Game-Type) — subscribed
        // direkt auf IGameStateService.PerfectRatingIncremented im Constructor.
        services.AddSingleton<IMiniGameMasteryService, MiniGameMasteryService>();
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IPlayGamesService, PlayGamesService>();

        // Android-Override: Plattformspezifischer Audio-Service
        if (AudioServiceFactory != null)
            services.AddSingleton<IAudioService>(sp => AudioServiceFactory!(sp));

        // Android-Override: Plattformspezifischer Notification-Service
        if (NotificationServiceFactory != null)
            services.AddSingleton<INotificationService>(sp => NotificationServiceFactory!(sp));

        // Android-Override: Google Play Games Service
        if (PlayGamesServiceFactory != null)
            services.AddSingleton<IPlayGamesService>(sp => PlayGamesServiceFactory!(sp));

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
        // v2.0.39 Audit-Fix U1: WhatsNew-Dialog fuer Update-Spieler.
        services.AddSingleton<IWhatsNewService, WhatsNewService>();
        // v2.1.0: Reputation-Shop (3. Waehrung neben Geld + GS).
        services.AddSingleton<IReputationShopService, ReputationShopService>();
        services.AddSingleton<ReputationShopViewModel>();
        // v2.1.0 Sprint-3 Big Bets: Co-op-Auftraege + Worker-Auktionen via Firebase.
        services.AddSingleton<IGuildCoopOrderService, GuildCoopOrderService>();
        services.AddSingleton<IWorkerAuctionService, WorkerAuctionService>();
        services.AddSingleton<ViewModels.Guild.GuildCoopOrderViewModel>();
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

        // Neue Feature-Services (Welle 1-8)
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
        // P1.3 AAA-Audit: Daily-Bundle-Foundation (UI-Wiring kommt in spaeterem Sprint)
        services.AddSingleton<IDailyBundleService, DailyBundleService>();

        services.AddSingleton<IGuildService, GuildService>();
        services.AddSingleton<IGuildInviteService, GuildInviteService>();
        services.AddSingleton<ICraftingService, CraftingService>();
        services.AddSingleton<IAutoProductionService, AutoProductionService>();

        // Late-Game-Services (Ascension, Rebirth, VIP)
        services.AddSingleton<IAscensionService, AscensionService>();
        services.AddSingleton<IRebirthService, RebirthService>();
        services.AddSingleton<IVipService, VipService>();

        // Masterplan-Services
        // LeaderboardService + BountyService entfernt (19.03.2026): Keine UI-Integration, kein Aufrufer
        services.AddSingleton<IGuildChatService, GuildChatService>();
        services.AddSingleton<IGoalService, GoalService>();

        // Gilden-Overhaul Services (AAA-System)
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

        // Feature-VMs (Phase 3 der MainViewModel-Zerlegung, 17.04.2026)
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
        // Thin-Wrapper-Sub-VMs (Phase 4 17.04.2026) werden im GuildViewModel-Ctor manuell erstellt
        // (zirkuläre DI vermieden: Sub-VM haelt Referenz auf Parent-GuildViewModel).
        services.AddSingleton<CraftingViewModel>();
        services.AddSingleton<LuckySpinViewModel>();
        services.AddSingleton<AscensionViewModel>();
    }
}
