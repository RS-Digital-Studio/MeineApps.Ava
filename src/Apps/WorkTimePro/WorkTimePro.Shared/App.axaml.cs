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
using WorkTimePro.Graphics;
using MeineApps.UI.SkiaSharp;

namespace WorkTimePro;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Aktuelles Android-Root-Panel — wird vom IActivityApplicationLifetime.MainViewFactory gesetzt.
    /// RunLoadingAsync greift darauf zu, um den DataContext nach Pipeline-Abschluss zu setzen.
    /// Avalonia 12 hat keinen direkten "View"-Getter auf IActivityApplicationLifetime und spiegelt
    /// MainViewFactory NICHT in ISingleViewApplicationLifetime.MainView.
    /// </summary>
    private static Control? _activityRoot;

    /// <summary>
    /// Versionsnummer aus Assembly lesen (statt hardcoded)
    /// </summary>
    private static string GetAppVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "2.0.7";
    }

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
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
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

        // Farb-Cache für SkiaSharp initialisieren
        SkiaThemeHelper.RefreshColors();

        // Window/View sofort erstellen (Avalonia braucht das synchron)
        // DataContext wird erst nach Pipeline-Abschluss gesetzt
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            var splash = new SkiaLoadingSplash { AppName = "WorkTimePro", AppVersion = $"v{GetAppVersion()}", Renderer = new WorkTimeProSplashRenderer() };
            var panel = new Panel();
            panel.Children.Add(new MainView());
            panel.Children.Add(splash);
            desktop.MainWindow.Content = panel;
            _ = RunLoadingAsync(splash);
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            // Android (Avalonia 12): IActivityApplicationLifetime.MainViewFactory ist der voll
            // unterstuetzte Render-Pfad. ISingleViewApplicationLifetime.MainView wird von Avalonia
            // selbst als "not fully supported on Android" gewarnt. MainViewFactory wird pro Activity
            // aufgerufen; die Loading-Pipeline laeuft beim ersten Aufruf (Singleton-Graph).
            // Referenz-Pattern: BomberBlast.
            activity.MainViewFactory = () =>
            {
                var splash = new SkiaLoadingSplash { AppName = "WorkTimePro", AppVersion = $"v{GetAppVersion()}", Renderer = new WorkTimeProSplashRenderer() };
                var panel = new Panel();
                panel.Children.Add(new MainView());
                panel.Children.Add(splash);
                _activityRoot = panel;
                _ = RunLoadingAsync(splash);
                return panel;
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var splash = new SkiaLoadingSplash { AppName = "WorkTimePro", AppVersion = $"v{GetAppVersion()}", Renderer = new WorkTimeProSplashRenderer() };
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

            // Mindestens 800ms anzeigen damit die Stechuhr-Animation sichtbar ist
            var remaining = 800 - (int)sw.ElapsedMilliseconds;
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
                // Android (IActivityApplicationLifetime): DataContext auf das von MainViewFactory
                // erzeugte Panel (_activityRoot) — Avalonia spiegelt MainViewFactory NICHT in
                // ISingleViewApplicationLifetime.MainView (das waere hier null).
                else if (_activityRoot != null)
                {
                    _activityRoot.DataContext = mainVm;
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
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // DataContext trotzdem setzen — sonst bleibt die MainView leer/ungebunden ohne
                // jede Rückmeldung. Das ViewModel meldet Folgefehler dann über sein eigenes
                // MessageRequested-Overlay; der Nutzer kann die App weiter bedienen / neu laden.
                try
                {
                    var mainVm = Services.GetService<MainViewModel>();
                    if (mainVm != null)
                    {
                        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                            && desktop.MainWindow != null)
                            desktop.MainWindow.DataContext = mainVm;
                        else if (_activityRoot != null)
                            _activityRoot.DataContext = mainVm;
                        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView
                                 && singleView.MainView != null)
                            singleView.MainView.DataContext = mainVm;
                    }
                }
                catch (Exception bindEx)
                {
                    Debug.WriteLine($"[WorkTimePro] Fallback-DataContext fehlgeschlagen: {bindEx}");
                }

                // Fehler kurz auf dem Splash sichtbar machen, dann ausblenden (App nicht blockieren).
                splash.StatusText = AppStrings.ErrorLoading is { } fmt
                    ? string.Format(fmt, ex.Message)
                    : ex.Message;
                splash.FadeOut();
            });
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("WorkTimePro"));

        // Premium Services (Ads, Purchases, Trial)
        services.AddMeineAppsPremium();

        // Android-Override: Echte Rewarded Ads statt Desktop-Simulator
        // lazy, Avalonia-12-Factory-Timing: Factory wird erst beim Resolve gelesen (nach MainActivity)
        services.AddSingleton<IRewardedAdService>(sp =>
            RewardedAdServiceFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance<RewardedAdService>(sp));

        // Android-Override: Echte Google Play Billing statt Stub
        // lazy, Avalonia-12-Factory-Timing
        services.AddSingleton<IPurchaseService>(sp =>
            PurchaseServiceFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance<PurchaseService>(sp));

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // App-Lifecycle-Broker: Android speist NotifyPaused/Resumed; Konsumenten stoppen
        // Timer/Render-Loops im Hintergrund (Akku).
        services.AddSingleton<IAppLifecycleService, AppLifecycleService>();

        // Plattformspezifisch: Android setzt Factory, Desktop nutzt Default
        // lazy, Avalonia-12-Factory-Timing
        services.AddSingleton<IFileShareService>(sp =>
            FileShareServiceFactory?.Invoke() ?? ActivatorUtilities.CreateInstance<DesktopFileShareService>(sp));

        // App Services
        services.AddSingleton<IDatabaseService, DatabaseService>();
        // Backup-spezifischer DB-Zugriff teilt die DatabaseService-Singleton-Instanz (ISP-Cut).
        services.AddSingleton<IBackupDataAccess>(sp => (DatabaseService)sp.GetRequiredService<IDatabaseService>());
        services.AddSingleton<ICalculationService, CalculationService>();
        services.AddSingleton<ITimeTrackingService, TimeTrackingService>();
        services.AddSingleton<ICalendarExportService>(sp =>
            new CalendarExportService(
                sp.GetRequiredService<IDatabaseService>(),
                sp.GetRequiredService<IFileShareService>()));
        services.AddSingleton<IExportService>(sp =>
            new ExportService(
                sp.GetRequiredService<IDatabaseService>(),
                sp.GetRequiredService<ICalculationService>(),
                sp.GetRequiredService<IFileShareService>(),
                sp.GetRequiredService<ICalendarExportService>()));
        services.AddSingleton<IVacationService, VacationService>();
        services.AddSingleton<IHolidayService, HolidayService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IShiftService, ShiftService>();
        services.AddSingleton<IEmployerService, EmployerService>();
        services.AddSingleton<IBackupService, BackupService>();

        // Notification + Reminder Services
        // lazy, Avalonia-12-Factory-Timing
        services.AddSingleton<INotificationService>(sp =>
            NotificationServiceFactory?.Invoke() ?? ActivatorUtilities.CreateInstance<DesktopNotificationService>(sp));
        services.AddSingleton<IReminderService, ReminderService>();

        // Haptic Feedback (Desktop: NoOp, Android setzt via HapticServiceFactory)
        // lazy, Avalonia-12-Factory-Timing
        services.AddSingleton<IHapticService>(sp =>
            HapticServiceFactory?.Invoke() ?? ActivatorUtilities.CreateInstance<NoOpHapticService>(sp));

        // ViewModels (alle Singleton - MainVM hält Child-VMs per Constructor Injection)
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
