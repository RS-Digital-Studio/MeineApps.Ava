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
using MeineApps.UI.SkiaSharp;

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
    /// Factory fuer plattformspezifischen IPurchaseService (Android setzt AndroidPurchaseService).
    /// </summary>
    public static Func<IServiceProvider, IPurchaseService>? PurchaseServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IFileShareService.
    /// Android setzt dies auf AndroidFileShareService.
    /// </summary>
    public static Func<IFileShareService>? FileShareServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IBarcodeService.
    /// Android setzt dies auf AndroidBarcodeService (CameraX + ML Kit).
    /// </summary>
    public static Func<IBarcodeService>? BarcodeServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IHapticService.
    /// Android setzt dies auf AndroidHapticService (Vibrator).
    /// </summary>
    public static Func<IHapticService>? HapticServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IFitnessSoundService.
    /// Android setzt dies auf AndroidFitnessSoundService (System-Sound).
    /// </summary>
    public static Func<IFitnessSoundService>? SoundServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IReminderService.
    /// Android setzt dies auf AndroidReminderService (AlarmManager).
    /// </summary>
    public static Func<IServiceProvider, IReminderService>? ReminderServiceFactory { get; set; }

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

        // Android-Override: Echte Google Play Billing statt Stub
        if (PurchaseServiceFactory != null)
            services.AddSingleton<IPurchaseService>(sp => PurchaseServiceFactory!(sp));

        services.AddSingleton<IScanLimitService, ScanLimitService>();

        // File Share Service (Desktop: Datei oeffnen, Android: Share Intent)
        if (FileShareServiceFactory != null)
            services.AddSingleton<IFileShareService>(_ => FileShareServiceFactory!());
        else
            services.AddSingleton<IFileShareService, DesktopFileShareService>();

        // Barcode Service (Desktop: Fallback ohne Kamera, Android: CameraX + ML Kit)
        if (BarcodeServiceFactory != null)
            services.AddSingleton<IBarcodeService>(_ => BarcodeServiceFactory!());
        else
            services.AddSingleton<IBarcodeService, DesktopBarcodeService>();

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // App Services
        services.AddSingleton<FitnessEngine>();
        services.AddSingleton<StreakService>();
        services.AddSingleton<IAchievementService, AchievementService>();
        services.AddSingleton<ILevelService, LevelService>();
        services.AddSingleton<IChallengeService, ChallengeService>();
        services.AddSingleton<ITrackingService, TrackingService>();
        services.AddSingleton<IFoodSearchService, FoodSearchService>();
        services.AddSingleton<IBarcodeLookupService, BarcodeLookupService>();

        // Plattform-Services (Haptic, Sound, Reminders)
        if (HapticServiceFactory != null)
            services.AddSingleton<IHapticService>(_ => HapticServiceFactory!());
        else
            services.AddSingleton<IHapticService, NoOpHapticService>();

        if (SoundServiceFactory != null)
            services.AddSingleton<IFitnessSoundService>(_ => SoundServiceFactory!());
        else
            services.AddSingleton<IFitnessSoundService, NoOpFitnessSoundService>();

        if (ReminderServiceFactory != null)
            services.AddSingleton<IReminderService>(sp => ReminderServiceFactory!(sp));
        else
            services.AddSingleton<IReminderService, ReminderService>();

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
