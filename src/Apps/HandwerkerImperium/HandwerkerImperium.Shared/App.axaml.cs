using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Extensions;
using MeineApps.Core.Premium.Ava.Services;
using HandwerkerImperium.Resources.Strings;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
using HandwerkerImperium.Views;

namespace HandwerkerImperium;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Factory fuer plattformspezifischen IRewardedAdService (Android setzt RewardedAdHelper).
    /// Nimmt IServiceProvider entgegen fuer Lazy-Resolution von Abhaengigkeiten.
    /// </summary>
    public static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory { get; set; }

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

        // Initialize localization
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

        // Initialize theme (must be resolved to apply saved theme at startup)
        _ = Services.GetRequiredService<IThemeService>();

        var mainVm = Services.GetRequiredService<MainViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = mainVm
            };
        }

        // Initialize game state, orders, offline progress, daily rewards
        mainVm.Initialize();

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("HandwerkerImperium"));
        services.AddSingleton<IThemeService, ThemeService>();

        // Premium Services (Ads, Purchases)
        services.AddMeineAppsPremium();

        // Android-Override: Echte Rewarded Ads statt Desktop-Simulator
        if (RewardedAdServiceFactory != null)
            services.AddSingleton<IRewardedAdService>(sp => RewardedAdServiceFactory!(sp));

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // Game Services
        services.AddSingleton<IGameStateService, GameStateService>();
        services.AddSingleton<ISaveGameService, SaveGameService>();
        services.AddSingleton<IGameLoopService, GameLoopService>();
        services.AddSingleton<IAchievementService, AchievementService>();
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<IDailyRewardService, DailyRewardService>();
        services.AddSingleton<IOfflineProgressService, OfflineProgressService>();
        services.AddSingleton<IOrderGeneratorService, OrderGeneratorService>();
        services.AddSingleton<IPrestigeService, PrestigeService>();
        services.AddSingleton<ITutorialService, TutorialService>();

        // New Game Services (v2.0)
        services.AddSingleton<IWorkerService, WorkerService>();
        services.AddSingleton<IBuildingService, BuildingService>();
        services.AddSingleton<IResearchService, ResearchService>();
        services.AddSingleton<IEventService, EventService>();
        services.AddSingleton<IQuickJobService, QuickJobService>();
        services.AddSingleton<IDailyChallengeService, DailyChallengeService>();

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
        services.AddSingleton<WorkerMarketViewModel>();
        services.AddSingleton<WorkerProfileViewModel>();
        services.AddSingleton<BuildingsViewModel>();
        services.AddSingleton<ResearchViewModel>();
    }
}
