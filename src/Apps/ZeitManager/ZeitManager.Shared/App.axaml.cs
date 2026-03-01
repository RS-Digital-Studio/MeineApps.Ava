using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.UI.Controls;
using ZeitManager.Loading;
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

        // Theme initialisieren
        var themeService = Services.GetRequiredService<IThemeService>();
        SkiaThemeHelper.RefreshColors();
        themeService.ThemeChanged += (_, _) => SkiaThemeHelper.RefreshColors();

        // Localization initialisieren
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

        // Window/View sofort erstellen (Avalonia braucht das synchron)
        // DataContext wird erst nach Pipeline-Abschluss gesetzt
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            var splash = new SkiaLoadingSplash { AppName = "ZeitManager", AppVersion = "v2.0.5" };
            var panel = new Panel();
            panel.Children.Add(new MainView());
            panel.Children.Add(splash);
            desktop.MainWindow.Content = panel;
            _ = RunLoadingAsync(splash);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            // Android: MainView + Splash in ein Panel wrappen
            var splash = new SkiaLoadingSplash { AppName = "ZeitManager", AppVersion = "v2.0.5" };
            var panel = new Panel();
            panel.Children.Add(new MainView());
            panel.Children.Add(splash);
            singleViewPlatform.MainView = panel;
            _ = RunLoadingAsync(splash);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Führt die Loading-Pipeline aus und setzt nach Abschluss den DataContext.
    /// </summary>
    private async Task RunLoadingAsync(SkiaLoadingSplash splash)
    {
        try
        {
            var pipeline = new ZeitManagerLoadingPipeline(Services);
            pipeline.ProgressChanged += (progress, text) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    splash.Progress = progress;
                    splash.StatusText = text;
                });

            var sw = Stopwatch.StartNew();
            await pipeline.ExecuteAsync();

            // Mindestens 500ms anzeigen (Desktop ist zu schnell)
            var remaining = 500 - (int)sw.ElapsedMilliseconds;
            if (remaining > 0) await Task.Delay(remaining);

            // DataContext setzen (ViewModel wurde in Pipeline erstellt)
            var mainVm = Services.GetRequiredService<MainViewModel>();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                    && desktop.MainWindow != null)
                {
                    desktop.MainWindow.DataContext = mainVm;
                }
                else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform
                         && singleViewPlatform.MainView != null)
                {
                    // MainView ist jetzt ein Panel - DataContext auf das Panel setzen
                    singleViewPlatform.MainView.DataContext = mainVm;
                }

                splash.FadeOut();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ZeitManager] Loading-Pipeline fehlgeschlagen: {ex}");
            // Splash trotzdem ausblenden, damit App nicht blockiert
            Avalonia.Threading.Dispatcher.UIThread.Post(() => splash.FadeOut());
        }
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
        services.AddSingleton<IHapticService, NoOpHapticService>();

        // Platform-specific services (Android registers AndroidNotificationService, Desktop uses default)
        if (ConfigurePlatformServices != null)
            ConfigurePlatformServices(services);
        else
            services.AddSingleton<INotificationService, DesktopNotificationService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<TimerViewModel>();
        services.AddSingleton<StopwatchViewModel>();
        services.AddSingleton<PomodoroViewModel>();
        services.AddSingleton<AlarmViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AlarmOverlayViewModel>();
        services.AddSingleton<ShiftScheduleViewModel>();
    }
}
