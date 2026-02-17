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
    /// Factory fuer plattformspezifischen ISoundService (Android setzt AndroidSoundService).
    /// </summary>
    public static Func<IServiceProvider, ISoundService>? SoundServiceFactory { get; set; }

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
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("BomberBlast"));
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
        services.AddSingleton<IProgressService, ProgressService>();
        services.AddSingleton<IHighScoreService, HighScoreService>();
        // Android-Override: Echte Sounds statt NullSoundService
        if (SoundServiceFactory != null)
            services.AddSingleton<ISoundService>(sp => SoundServiceFactory!(sp));
        else
            services.AddSingleton<ISoundService, NullSoundService>();
        services.AddSingleton<IGameStyleService, GameStyleService>();
        services.AddSingleton<ICoinService, CoinService>();
        services.AddSingleton<IShopService, ShopService>();
        services.AddSingleton<ITutorialService, TutorialService>();
        services.AddSingleton<IDailyRewardService, DailyRewardService>();
        services.AddSingleton<IDailyChallengeService, DailyChallengeService>();
        services.AddSingleton<ICustomizationService, CustomizationService>();
        services.AddSingleton<IReviewService, ReviewService>();
        services.AddSingleton<IAchievementService, AchievementService>();
        services.AddSingleton<SoundManager>();
        services.AddSingleton<SpriteSheet>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<GameRenderer>();
        services.AddSingleton<GameEngine>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainMenuViewModel>();
        services.AddTransient<GameViewModel>();
        services.AddTransient<LevelSelectViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<HighScoresViewModel>();
        services.AddTransient<GameOverViewModel>();
        services.AddTransient<PauseViewModel>();
        services.AddTransient<HelpViewModel>();
        services.AddTransient<ShopViewModel>();
        services.AddTransient<AchievementsViewModel>();
        services.AddTransient<DailyChallengeViewModel>();
    }
}
