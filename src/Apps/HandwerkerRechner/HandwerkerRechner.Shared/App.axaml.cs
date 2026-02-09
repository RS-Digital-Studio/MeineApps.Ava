using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Extensions;
using MeineApps.Core.Premium.Ava.Services;
using HandwerkerRechner.Models;
using HandwerkerRechner.Resources.Strings;
using HandwerkerRechner.Services;
using HandwerkerRechner.ViewModels;
using HandwerkerRechner.ViewModels.Floor;
using HandwerkerRechner.ViewModels.Premium;
using HandwerkerRechner.Views;

namespace HandwerkerRechner;

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

        // Initialize theme (must be resolved to apply saved theme at startup)
        _ = Services.GetRequiredService<IThemeService>();

        // Initialize localization
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

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
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("HandwerkerRechner"));
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IUnitConverterService, UnitConverterService>();
        services.AddSingleton<ICalculationHistoryService, CalculationHistoryService>();

        // Premium Services (Ads, Purchases)
        services.AddMeineAppsPremium();

        // Android-Override: Echte Rewarded Ads statt Desktop-Simulator
        if (RewardedAdServiceFactory != null)
            services.AddSingleton<IRewardedAdService>(sp => RewardedAdServiceFactory!(sp));

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // App Services
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IPremiumAccessService, PremiumAccessService>();

        // Export Services
        services.AddSingleton<IFileShareService, DesktopFileShareService>();
        services.AddSingleton<IMaterialExportService, MaterialExportService>();

        // Engine
        services.AddSingleton<CraftEngine>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ProjectsViewModel>();

        // Floor Calculator ViewModels
        services.AddTransient<TileCalculatorViewModel>();
        services.AddTransient<PaintCalculatorViewModel>();
        services.AddTransient<WallpaperCalculatorViewModel>();
        services.AddTransient<FlooringCalculatorViewModel>();

        // Premium Calculator ViewModels
        services.AddTransient<DrywallViewModel>();
        services.AddTransient<ElectricalViewModel>();
        services.AddTransient<MetalViewModel>();
        services.AddTransient<GardenViewModel>();
        services.AddTransient<RoofSolarViewModel>();
    }
}
