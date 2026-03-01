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
using FitnessRechner.Loading;
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
            desktop.MainWindow = new MainWindow();
            var splash = CreateSplash();
            var panel = new Panel();
            panel.Children.Add(new MainView());
            panel.Children.Add(splash);
            desktop.MainWindow.Content = panel;
            _ = RunLoadingAsync(splash);
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
            AppName = "FitnessRechner",
            AppVersion = "v2.0.5"
        };
    }

    private async Task RunLoadingAsync(SkiaLoadingSplash splash)
    {
        try
        {
            var pipeline = new FitnessRechnerLoadingPipeline(Services);
            pipeline.ProgressChanged += (progress, text) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    splash.Progress = progress;
                    splash.StatusText = text;
                });

            var sw = Stopwatch.StartNew();
            await pipeline.ExecuteAsync();

            var remaining = 500 - (int)sw.ElapsedMilliseconds;
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[FitnessRechner] Loading-Pipeline fehlgeschlagen: {ex}");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => splash.FadeOut());
        }
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
        services.AddSingleton<IFitnessEngine, FitnessEngine>();
        services.AddSingleton<IStreakService, StreakService>();
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

        // ViewModels (Haupt-VMs als Singleton, Calculator-VMs als Transient)
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ProgressViewModel>();
        services.AddSingleton<FoodSearchViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Calculator-VMs als Transient (jedes Oeffnen erzeugt frische Instanz)
        services.AddTransient<BmiViewModel>();
        services.AddTransient<CaloriesViewModel>();
        services.AddTransient<WaterViewModel>();
        services.AddTransient<IdealWeightViewModel>();
        services.AddTransient<BodyFatViewModel>();
        services.AddTransient<BarcodeScannerViewModel>();

        // Func<T> Factories fuer Calculator-VMs (Constructor Injection statt Service-Locator)
        services.AddSingleton<Func<BmiViewModel>>(sp => () => sp.GetRequiredService<BmiViewModel>());
        services.AddSingleton<Func<CaloriesViewModel>>(sp => () => sp.GetRequiredService<CaloriesViewModel>());
        services.AddSingleton<Func<WaterViewModel>>(sp => () => sp.GetRequiredService<WaterViewModel>());
        services.AddSingleton<Func<IdealWeightViewModel>>(sp => () => sp.GetRequiredService<IdealWeightViewModel>());
        services.AddSingleton<Func<BodyFatViewModel>>(sp => () => sp.GetRequiredService<BodyFatViewModel>());
        services.AddSingleton<Func<BarcodeScannerViewModel>>(sp => () => sp.GetRequiredService<BarcodeScannerViewModel>());
    }
}
