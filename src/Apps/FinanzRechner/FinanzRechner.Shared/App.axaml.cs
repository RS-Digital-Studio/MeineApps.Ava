using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Extensions;
using MeineApps.Core.Premium.Ava.Services;
using FinanzRechner.Models;
using FinanzRechner.Resources.Strings;
using FinanzRechner.Services;
using FinanzRechner.ViewModels;
using FinanzRechner.ViewModels.Calculators;
using FinanzRechner.Views;

namespace FinanzRechner;

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
        _ = Services.GetRequiredService<IThemeService>();

        // Initialize localization
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

        // Initialize expense service
        var expenseService = Services.GetRequiredService<IExpenseService>();
        expenseService.InitializeAsync().ConfigureAwait(false);

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
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("FinanzRechner"));
        services.AddSingleton<IThemeService, ThemeService>();

        // Premium Services (Ads, Purchases)
        services.AddMeineAppsPremium();

        // Android-Override: Echte Rewarded Ads statt Desktop-Simulator
        if (RewardedAdServiceFactory != null)
            services.AddSingleton<IRewardedAdService>(sp => RewardedAdServiceFactory!(sp));

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // App Services
        services.AddSingleton<IFileDialogService, FileDialogService>();
        // Plattformspezifisch: Android setzt Factory, Desktop nutzt Default
        if (FileShareServiceFactory != null)
            services.AddSingleton(FileShareServiceFactory());
        else
            services.AddSingleton<IFileShareService, DesktopFileShareService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IExpenseService>(sp =>
            new ExpenseService(
                sp.GetRequiredService<INotificationService>(),
                sp.GetRequiredService<ILocalizationService>()));
        services.AddSingleton<IExportService>(sp =>
            new ExportService(
                sp.GetRequiredService<IExpenseService>(),
                sp.GetRequiredService<ILocalizationService>(),
                sp.GetRequiredService<IFileShareService>()));

        // Engine
        services.AddSingleton<FinanceEngine>();

        // ViewModels
        services.AddTransient<ExpenseTrackerViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<BudgetsViewModel>();
        services.AddTransient<RecurringTransactionsViewModel>();
        services.AddTransient<LoanViewModel>();
        services.AddTransient<CompoundInterestViewModel>();
        services.AddTransient<SavingsPlanViewModel>();
        services.AddTransient<AmortizationViewModel>();
        services.AddTransient<YieldViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
