using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Extensions;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;
using WorkTimePro.ViewModels;
using WorkTimePro.Views;

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
            singleViewPlatform.MainView = new MainView
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        // Reminder-Service initialisieren (nach DI + UI Setup)
        _ = InitializeServicesAsync();

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeServicesAsync()
    {
        try
        {
            var reminderService = Services.GetRequiredService<IReminderService>();
            await reminderService.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeServicesAsync Fehler: {ex.Message}");
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

        // ViewModels (child VMs are Transient, MainVM is Singleton since it holds them)
        services.AddTransient<WeekOverviewViewModel>();
        services.AddTransient<CalendarViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DayDetailViewModel>();
        services.AddTransient<MonthOverviewViewModel>();
        services.AddTransient<YearOverviewViewModel>();
        services.AddTransient<VacationViewModel>();
        services.AddTransient<ShiftPlanViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
