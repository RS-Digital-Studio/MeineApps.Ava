using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using ZeitManager.Resources.Strings;
using ZeitManager.Services;
using ZeitManager.ViewModels;
using ZeitManager.Views;
using MeineApps.UI.SkiaSharp;

namespace ZeitManager;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Platform-specific service registrations. Set before app initialization (e.g. in MainActivity).
    /// </summary>
    public static Action<IServiceCollection>? ConfigurePlatformServices { get; set; }

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

        // Initialize theme (apply saved theme before window is created)
        var themeService = Services.GetRequiredService<IThemeService>();
        SkiaThemeHelper.RefreshColors();
        themeService.ThemeChanged += (_, _) => SkiaThemeHelper.RefreshColors();

        // Initialize localization
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

        // Initialize database and alarm scheduler (fire-and-forget, DB access is guarded by SemaphoreSlim)
        _ = InitializeServicesAsync();

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

    private static async Task InitializeServicesAsync()
    {
        var dbService = Services.GetRequiredService<IDatabaseService>();
        await dbService.InitializeAsync();

        var alarmScheduler = Services.GetRequiredService<IAlarmSchedulerService>();
        await alarmScheduler.InitializeAsync();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("ZeitManager"));
        services.AddSingleton<IThemeService, ThemeService>();

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // App Services
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<ITimerService, TimerService>();
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<IAlarmSchedulerService, AlarmSchedulerService>();
        services.AddSingleton<IShiftScheduleService, ShiftScheduleService>();
        services.AddSingleton<IShakeDetectionService, DesktopShakeDetectionService>();

        // Platform-specific services (Android registers AndroidNotificationService, Desktop uses default)
        if (ConfigurePlatformServices != null)
            ConfigurePlatformServices(services);
        else
            services.AddSingleton<INotificationService, DesktopNotificationService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<TimerViewModel>();
        services.AddTransient<StopwatchViewModel>();
        services.AddTransient<PomodoroViewModel>();
        services.AddTransient<AlarmViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<AlarmOverlayViewModel>();
        services.AddTransient<ShiftScheduleViewModel>();
    }
}
