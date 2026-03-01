using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Extensions;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Controls;
using WorkTimePro.Loading;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;
using WorkTimePro.ViewModels;
using WorkTimePro.Views;
using MeineApps.UI.SkiaSharp;

namespace WorkTimePro;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Factory fuer plattformspezifischen IFileShareService.
    /// Wird von Android-MainActivity gesetzt bevor DI gestartet wird.
    /// </summary>
    public static Func<IFileShareService>? FileShareServiceFactory { get; set; }

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
    /// Factory fuer plattformspezifischen INotificationService.
    /// Android setzt AndroidNotificationService, Desktop nutzt DesktopNotificationService.
    /// </summary>
    public static Func<INotificationService>? NotificationServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IHapticService.
    /// Android setzt AndroidHapticService, Desktop nutzt NoOpHapticService.
    /// </summary>
    public static Func<IHapticService>? HapticServiceFactory { get; set; }

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

        // Localization initialisieren
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

        // Theme initialisieren
        var themeService = Services.GetRequiredService<IThemeService>();
        SkiaThemeHelper.RefreshColors();
        themeService.ThemeChanged += (_, _) => SkiaThemeHelper.RefreshColors();

        // Window/View sofort erstellen (Avalonia braucht das synchron)
        // DataContext wird erst nach Pipeline-Abschluss gesetzt
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            var splash = new SkiaLoadingSplash { AppName = "WorkTimePro", AppVersion = "v2.0.5" };
            var panel = new Panel();
            panel.Children.Add(new MainView());
            panel.Children.Add(splash);
            desktop.MainWindow.Content = panel;
            _ = RunLoadingAsync(splash);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var splash = new SkiaLoadingSplash { AppName = "WorkTimePro", AppVersion = "v2.0.5" };
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
            var pipeline = new WorkTimeProLoadingPipeline(Services);
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
                    singleViewPlatform.MainView.DataContext = mainVm;
                }

                splash.FadeOut();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorkTimePro] Loading-Pipeline fehlgeschlagen: {ex}");
            // Splash trotzdem ausblenden, damit App nicht blockiert
            Avalonia.Threading.Dispatcher.UIThread.Post(() => splash.FadeOut());
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("WorkTimePro"));
        services.AddSingleton<IThemeService, ThemeService>();

        // Premium Services (Ads, Purchases, Trial)
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

        // Plattformspezifisch: Android setzt Factory, Desktop nutzt Default
        if (FileShareServiceFactory != null)
            services.AddSingleton(FileShareServiceFactory());
        else
            services.AddSingleton<IFileShareService, DesktopFileShareService>();

        // App Services
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<ICalculationService, CalculationService>();
        services.AddSingleton<ITimeTrackingService, TimeTrackingService>();
        services.AddSingleton<IExportService>(sp =>
            new ExportService(
                sp.GetRequiredService<IDatabaseService>(),
                sp.GetRequiredService<ICalculationService>(),
                sp.GetRequiredService<IFileShareService>()));
        services.AddSingleton<IVacationService, VacationService>();
        services.AddSingleton<IHolidayService, HolidayService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IShiftService, ShiftService>();
        services.AddSingleton<IEmployerService, EmployerService>();
        services.AddSingleton<ICalendarSyncService, CalendarSyncService>();
        services.AddSingleton<IBackupService, BackupService>();

        // Achievement Service
        services.AddSingleton<IAchievementService, AchievementService>();

        // Notification + Reminder Services
        if (NotificationServiceFactory != null)
            services.AddSingleton(NotificationServiceFactory());
        else
            services.AddSingleton<INotificationService, DesktopNotificationService>();
        services.AddSingleton<IReminderService, ReminderService>();

        // Haptic Feedback (Desktop: NoOp, Android setzt via HapticServiceFactory)
        if (HapticServiceFactory != null)
            services.AddSingleton(HapticServiceFactory());
        else
            services.AddSingleton<IHapticService, NoOpHapticService>();

        // ViewModels (alle Singleton - MainVM hält Child-VMs per Constructor Injection)
        services.AddSingleton<AchievementViewModel>();
        services.AddSingleton<WeekOverviewViewModel>();
        services.AddSingleton<CalendarViewModel>();
        services.AddSingleton<StatisticsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<DayDetailViewModel>();
        services.AddSingleton<MonthOverviewViewModel>();
        services.AddSingleton<YearOverviewViewModel>();
        services.AddSingleton<VacationViewModel>();
        services.AddSingleton<ShiftPlanViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
