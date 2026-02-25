using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Extensions;
using MeineApps.Core.Premium.Ava.Services;
using BomberBlast.Core;
using BomberBlast.Graphics;
using BomberBlast.Input;
using BomberBlast.Resources.Strings;
using BomberBlast.Services;
using BomberBlast.ViewModels;
using BomberBlast.Views;
using MeineApps.UI.SkiaSharp;

namespace BomberBlast;

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
    /// Factory fuer plattformspezifischen ISoundService (Android setzt AndroidSoundService).
    /// </summary>
    public static Func<IServiceProvider, ISoundService>? SoundServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IPlayGamesService (Android setzt AndroidPlayGamesService).
    /// </summary>
    public static Func<IServiceProvider, IPlayGamesService>? PlayGamesServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen ICloudSaveService (Android setzt CloudSaveService).
    /// </summary>
    public static Func<IServiceProvider, ICloudSaveService>? CloudSaveServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IVibrationService (Android setzt AndroidVibrationService).
    /// </summary>
    public static Func<IServiceProvider, IVibrationService>? VibrationServiceFactory { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Setup DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Statischer Logger für ShaderEffects (nicht DI-verwaltet)
        ShaderEffects.Logger = Services.GetRequiredService<IAppLogger>();

        // Lazy-Injection: AchievementService in Services verdrahten (vermeidet zirkuläre DI)
        var achievementService = Services.GetRequiredService<IAchievementService>();
        Services.GetRequiredService<IBattlePassService>().SetAchievementService(achievementService);
        Services.GetRequiredService<ICardService>().SetAchievementService(achievementService);
        Services.GetRequiredService<ILeagueService>().SetAchievementService(achievementService);
        Services.GetRequiredService<IDailyMissionService>().SetAchievementService(achievementService);

        // Lazy-Injection: Mission-Services in GemService + CardService verdrahten (Phase 9.4)
        var weeklyService = Services.GetRequiredService<IWeeklyChallengeService>();
        var dailyMissionService = Services.GetRequiredService<IDailyMissionService>();
        Services.GetRequiredService<IGemService>().SetMissionServices(weeklyService, dailyMissionService);
        Services.GetRequiredService<ICardService>().SetMissionServices(weeklyService, dailyMissionService);

        // Lazy-Injection: CustomizationService braucht IGemService für Gem-Skins
        var gemService = Services.GetRequiredService<IGemService>();
        if (Services.GetRequiredService<ICustomizationService>() is CustomizationService customization)
            customization.SetGemService(gemService);

        // Lazy-Injection: DungeonService braucht IDungeonUpgradeService für permanente Upgrades
        var dungeonUpgradeService = Services.GetRequiredService<IDungeonUpgradeService>();
        if (Services.GetRequiredService<IDungeonService>() is DungeonService dungeonSvc)
            dungeonSvc.SetUpgradeService(dungeonUpgradeService);

        // Initialize localization
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

        // Initialize theme (must be resolved to apply saved theme at startup)
        var themeService = Services.GetRequiredService<IThemeService>();
        SkiaThemeHelper.RefreshColors();
        themeService.ThemeChanged += (_, _) => SkiaThemeHelper.RefreshColors();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddSingleton<IAppLogger, AppLogger>();

        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("BomberBlast"));
        services.AddSingleton<IThemeService, ThemeService>();

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

        // Google Play Games Services
        if (PlayGamesServiceFactory != null)
            services.AddSingleton<IPlayGamesService>(sp => PlayGamesServiceFactory!(sp));
        else
            services.AddSingleton<IPlayGamesService, NullPlayGamesService>();

        // Game Services
        services.AddSingleton<IProgressService, ProgressService>();
        services.AddSingleton<IHighScoreService, HighScoreService>();
        // Android-Override: Echte Sounds statt NullSoundService
        if (SoundServiceFactory != null)
            services.AddSingleton<ISoundService>(sp => SoundServiceFactory!(sp));
        else
            services.AddSingleton<ISoundService, NullSoundService>();
        services.AddSingleton<IGameStyleService, GameStyleService>();
        services.AddSingleton<ICoinService, CoinService>();
        services.AddSingleton<IGemService, GemService>();
        services.AddSingleton<IShopService, ShopService>();
        services.AddSingleton<ITutorialService, TutorialService>();
        services.AddSingleton<IDailyRewardService, DailyRewardService>();
        services.AddSingleton<IDailyChallengeService, DailyChallengeService>();
        services.AddSingleton<ICustomizationService, CustomizationService>();
        services.AddSingleton<IReviewService, ReviewService>();
        services.AddSingleton<IAchievementService, AchievementService>();
        services.AddSingleton<IDiscoveryService, DiscoveryService>();
        services.AddSingleton<ILuckySpinService, LuckySpinService>();
        services.AddSingleton<IWeeklyChallengeService, WeeklyChallengeService>();
        services.AddSingleton<IDailyMissionService, DailyMissionService>();
        services.AddSingleton<ICardService, CardService>();
        services.AddSingleton<IDungeonService, DungeonService>();
        services.AddSingleton<IDungeonUpgradeService, DungeonUpgradeService>();
        services.AddSingleton<IBattlePassService, BattlePassService>();
        services.AddSingleton<ICollectionService, CollectionService>();
        services.AddSingleton<IFirebaseService, FirebaseService>();
        services.AddSingleton<ILeagueService, LeagueService>();

        // Cloud Save (Android-Override: Echte Google Drive Cloud Saves statt NullCloudSaveService)
        if (CloudSaveServiceFactory != null)
            services.AddSingleton<ICloudSaveService>(sp => CloudSaveServiceFactory!(sp));
        else
            services.AddSingleton<ICloudSaveService, NullCloudSaveService>();

        services.AddSingleton<IStarterPackService, StarterPackService>();
        services.AddSingleton<IRotatingDealsService, RotatingDealsService>();

        // Vibration (Android-Override: Echte Vibration statt NullVibrationService)
        if (VibrationServiceFactory != null)
            services.AddSingleton<IVibrationService>(sp => VibrationServiceFactory!(sp));
        else
            services.AddSingleton<IVibrationService, NullVibrationService>();
        services.AddSingleton<IGameTrackingService, GameTrackingService>();
        services.AddSingleton<SoundManager>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<GameRenderer>();
        services.AddSingleton<GameEngine>();

        // ViewModels (alle Singleton: werden von MainViewModel gehalten, dürfen nicht doppelt existieren)
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainMenuViewModel>();
        services.AddSingleton<GameViewModel>();
        services.AddSingleton<LevelSelectViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<HighScoresViewModel>();
        services.AddSingleton<GameOverViewModel>();
        services.AddSingleton<PauseViewModel>();
        services.AddSingleton<HelpViewModel>();
        services.AddSingleton<ShopViewModel>();
        services.AddSingleton<AchievementsViewModel>();
        services.AddSingleton<DailyChallengeViewModel>();
        services.AddSingleton<VictoryViewModel>();
        services.AddSingleton<LuckySpinViewModel>();
        services.AddSingleton<WeeklyChallengeViewModel>();
        services.AddSingleton<StatisticsViewModel>();
        services.AddSingleton<QuickPlayViewModel>();
        services.AddSingleton<DeckViewModel>();
        services.AddSingleton<DungeonViewModel>();
        services.AddSingleton<BattlePassViewModel>();
        services.AddSingleton<CollectionViewModel>();
        services.AddSingleton<LeagueViewModel>();
        services.AddSingleton<ProfileViewModel>();
        services.AddSingleton<GemShopViewModel>();
    }
}
