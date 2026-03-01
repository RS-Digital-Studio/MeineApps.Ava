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
using FinanzRechner.Loading;
using FinanzRechner.Models;
using FinanzRechner.Resources.Strings;
using FinanzRechner.Services;
using FinanzRechner.ViewModels;
using FinanzRechner.ViewModels.Calculators;
using FinanzRechner.Views;
using MeineApps.UI.SkiaSharp;

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

    /// <summary>
    /// Factory fuer plattformspezifischen IPurchaseService (Android setzt AndroidPurchaseService).
    /// </summary>
    public static Func<IServiceProvider, IPurchaseService>? PurchaseServiceFactory { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // DI einrichten
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Theme initialisieren (gespeichertes Theme anwenden bevor das Fenster erstellt wird)
        var themeService = Services.GetRequiredService<IThemeService>();
        SkiaThemeHelper.RefreshColors();
        themeService.ThemeChanged += (_, _) => SkiaThemeHelper.RefreshColors();

        // Lokalisierung initialisieren
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

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
            AppName = "FinanzRechner",
            AppVersion = "v2.0.5"
        };
    }

    private async Task RunLoadingAsync(SkiaLoadingSplash splash)
    {
        try
        {
            var pipeline = new FinanzRechnerLoadingPipeline(Services);
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
            Debug.WriteLine($"[FinanzRechner] Loading-Pipeline fehlgeschlagen: {ex}");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => splash.FadeOut());
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Kern-Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("FinanzRechner"));
        services.AddSingleton<IThemeService, ThemeService>();

        // Premium-Services (Werbung, Käufe)
        services.AddMeineAppsPremium();

        // Android-Überschreibung: Echte Rewarded Ads statt Desktop-Simulator
        if (RewardedAdServiceFactory != null)
            services.AddSingleton<IRewardedAdService>(sp => RewardedAdServiceFactory!(sp));

        // Android-Überschreibung: Echte Google Play Billing statt Stub
        if (PurchaseServiceFactory != null)
            services.AddSingleton<IPurchaseService>(sp => PurchaseServiceFactory!(sp));

        // Lokalisierung
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // App-Services
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

        // Berechnungs-Engine
        services.AddSingleton<FinanceEngine>();

        // ViewModels
        services.AddSingleton<ExpenseTrackerViewModel>();
        services.AddSingleton<StatisticsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<BudgetsViewModel>();
        services.AddSingleton<RecurringTransactionsViewModel>();
        services.AddSingleton<LoanViewModel>();
        services.AddSingleton<CompoundInterestViewModel>();
        services.AddSingleton<SavingsPlanViewModel>();
        services.AddSingleton<AmortizationViewModel>();
        services.AddSingleton<YieldViewModel>();
        services.AddSingleton<InflationViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
