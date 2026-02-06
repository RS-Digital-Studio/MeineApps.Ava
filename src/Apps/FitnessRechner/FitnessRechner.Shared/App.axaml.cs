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

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            // Setup DI
            System.Diagnostics.Debug.WriteLine("FitnessRechner: Setting up DI...");
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();
            System.Diagnostics.Debug.WriteLine("FitnessRechner: DI built successfully");

            // Initialize localization
            var locService = Services.GetRequiredService<ILocalizationService>();
            locService.Initialize();
            LocalizationManager.Initialize(locService);
            System.Diagnostics.Debug.WriteLine("FitnessRechner: Localization initialized");

            // Initialize theme (must be resolved to apply saved theme at startup)
            _ = Services.GetRequiredService<IThemeService>();
            System.Diagnostics.Debug.WriteLine("FitnessRechner: Theme initialized");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = Services.GetRequiredService<MainViewModel>()
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                System.Diagnostics.Debug.WriteLine("FitnessRechner: Resolving MainViewModel...");
                var vm = Services.GetRequiredService<MainViewModel>();
                System.Diagnostics.Debug.WriteLine("FitnessRechner: MainViewModel resolved, creating MainView...");
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = vm
                };
                System.Diagnostics.Debug.WriteLine("FitnessRechner: MainView created successfully");
            }

            base.OnFrameworkInitializationCompleted();
            System.Diagnostics.Debug.WriteLine("FitnessRechner: Framework initialization completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FitnessRechner FATAL: {ex}");
            throw;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("FitnessRechner"));
        services.AddSingleton<IThemeService, ThemeService>();

        // Premium Services (Ads, Purchases)
        services.AddMeineAppsPremium();

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // App Services
        services.AddSingleton<FitnessEngine>();
        services.AddSingleton<ITrackingService, TrackingService>();
        services.AddSingleton<IFoodSearchService, FoodSearchService>();
        services.AddSingleton<IBarcodeLookupService, BarcodeLookupService>();
        // ViewModels
        services.AddTransient<MainViewModel>();
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
