using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Extensions;
using MeineApps.Core.Premium.Ava.Services;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;
using FitnessRechner.ViewModels;
using FitnessRechner.ViewModels.Calculators;
using FitnessRechner.Views;

namespace FitnessRechner;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Factory fuer plattformspezifischen IRewardedAdService (Android setzt RewardedAdHelper).
    /// Nimmt IServiceProvider entgegen fuer Lazy-Resolution von Abhaengigkeiten.
    /// </summary>
    public static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IFileShareService.
    /// Android setzt dies auf AndroidFileShareService.
    /// </summary>
    public static Func<IFileShareService>? FileShareServiceFactory { get; set; }

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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var vm = Services.GetRequiredService<MainViewModel>();
            singleViewPlatform.MainView = new MainView
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("FitnessRechner"));
        services.AddSingleton<IThemeService, ThemeService>();

        // Premium Services (Ads, Purchases)
        services.AddMeineAppsPremium();

        // Android-Override: Echte Rewarded Ads statt Desktop-Simulator
        if (RewardedAdServiceFactory != null)
            services.AddSingleton<IRewardedAdService>(sp => RewardedAdServiceFactory!(sp));

        services.AddSingleton<IScanLimitService, ScanLimitService>();

        // File Share Service (Desktop: Datei oeffnen, Android: Share Intent)
        if (FileShareServiceFactory != null)
            services.AddSingleton<IFileShareService>(_ => FileShareServiceFactory!());
        else
            services.AddSingleton<IFileShareService, DesktopFileShareService>();

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // App Services
        services.AddSingleton<FitnessEngine>();
        services.AddSingleton<ITrackingService, TrackingService>();
        services.AddSingleton<IFoodSearchService, FoodSearchService>();
        services.AddSingleton<IBarcodeLookupService, BarcodeLookupService>();
        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<ProgressViewModel>();
        services.AddTransient<FoodSearchViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<TrackingViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<BarcodeScannerViewModel>();

        // Calculator ViewModels
        services.AddTransient<BmiViewModel>();
        services.AddTransient<CaloriesViewModel>();
        services.AddTransient<WaterViewModel>();
        services.AddTransient<IdealWeightViewModel>();
        services.AddTransient<BodyFatViewModel>();
    }
}
