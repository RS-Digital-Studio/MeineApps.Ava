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
using HandwerkerImperium.Loading;
using HandwerkerImperium.Resources.Strings;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
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
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Setup DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Initialize localization
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

        // Farb-Cache für SkiaSharp initialisieren
        SkiaThemeHelper.RefreshColors();

        // Statische Renderer mit AI-Asset-Service initialisieren
        var assetService = Services.GetService<IGameAssetService>();
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
            _ = RunLoadingAsync(splash);

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
            _ = RunLoadingAsync(splash);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static SkiaLoadingSplash CreateSplash()
    {
        return new SkiaLoadingSplash
        {
            AppName = "HandwerkerImperium",
            AppVersion = "v2.0.14",
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

            // Mindestens 2s anzeigen damit die Splash-Animation sichtbar ist
            var remaining = 2000 - (int)sw.ElapsedMilliseconds;
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

    /// <summary>
    /// Disposed alle IDisposable-Singletons (GameLoopService, GameJuiceEngine, MainViewModel, FirebaseService etc.).
    /// Wird bei Desktop-Shutdown aufgerufen, auf Android via MainActivity.OnDestroy().
    /// </summary>
    public static void DisposeServices()
    {
        if (Services == null) return;

        try
        {
            // GameLoopService hält den 1s-Takt DispatcherTimer
            (Services.GetService<IGameLoopService>() as IDisposable)?.Dispose();

            // GameJuiceEngine hält SKPaint/SKFont/SKPath Instanzen
            (Services.GetService<GameJuiceEngine>() as IDisposable)?.Dispose();

            // MainViewModel hält Referenzen auf alle Child-VMs und Event-Subscriptions
            (Services.GetService<MainViewModel>() as IDisposable)?.Dispose();

            // SeasonalEventService hält OrderCompleted-Subscription
            (Services.GetService<ISeasonalEventService>() as IDisposable)?.Dispose();

            // BattlePassService hält OrderCompleted/MiniGameResult/WorkshopUpgraded-Subscriptions
            (Services.GetService<IBattlePassService>() as IDisposable)?.Dispose();

            // ShopViewModel hält PremiumStatusChanged/MoneyChanged/GoldenScrewsChanged-Subscriptions
            Services.GetService<ShopViewModel>()?.Dispose();

            // FirebaseService hält HttpClient
            (Services.GetService<IFirebaseService>() as IDisposable)?.Dispose();

            // Icon-System: Avalonia-Bitmap-Cache + SkiaSharp-Paints/Filter freigeben
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
        services.AddSingleton<ISaveGameService, SaveGameService>();
        services.AddSingleton<IGameLoopService, GameLoopService>();
        services.AddSingleton<IAchievementService, AchievementService>();
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
        services.AddSingleton<IOfflineProgressService, OfflineProgressService>();
        services.AddSingleton<IOrderGeneratorService, OrderGeneratorService>();
        services.AddSingleton<IPrestigeService, PrestigeService>();
        services.AddSingleton<IContextualHintService, ContextualHintService>();

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
        services.AddSingleton<IFirebaseService, FirebaseService>();
        services.AddSingleton<IGuildService, GuildService>();
        services.AddSingleton<ICraftingService, CraftingService>();
        services.AddSingleton<IFriendService, FriendService>();

        // Masterplan-Services (Phasen 2-6)
        services.AddSingleton<ILeaderboardService, LeaderboardService>();
        services.AddSingleton<IAscensionService, AscensionService>();
        services.AddSingleton<IGuildWarService, GuildWarService>();
        services.AddSingleton<IBountyService, BountyService>();
        services.AddSingleton<IGiftService, GiftService>();
        services.AddSingleton<IGuildChatService, GuildChatService>();
        services.AddSingleton<ICosmeticService, CosmeticService>();
        services.AddSingleton<IGoalService, GoalService>();

        // Gilden-Overhaul Services (AAA-System)
        services.AddSingleton<IGuildResearchService, GuildResearchService>();
        services.AddSingleton<IGuildWarSeasonService, GuildWarSeasonService>();
        services.AddSingleton<IGuildHallService, GuildHallService>();
        services.AddSingleton<IGuildBossService, GuildBossService>();
        services.AddSingleton<IGuildTipService, GuildTipService>();
        services.AddSingleton<IGuildAchievementService, GuildAchievementService>();

        // ViewModels (Singleton because MainViewModel holds references to child VMs)
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
        services.AddSingleton<CraftingViewModel>();
        services.AddSingleton<LuckySpinViewModel>();
    }
}
