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
using HandwerkerRechner.Loading;
using HandwerkerRechner.Models;
using HandwerkerRechner.Resources.Strings;
using HandwerkerRechner.Services;
using HandwerkerRechner.ViewModels;
using HandwerkerRechner.ViewModels.Floor;
using HandwerkerRechner.ViewModels.Premium;
using HandwerkerRechner.Views;
using HandwerkerRechner.Graphics;
using MeineApps.UI.SkiaSharp;

namespace HandwerkerRechner;

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
    /// Factory fuer plattformspezifischen IPhotoPickerService (Android setzt Intent-basiert).
    /// Desktop nutzt Default DesktopPhotoPickerService wenn nicht gesetzt.
    /// </summary>
    public static Func<IPhotoPickerService>? PhotoPickerServiceFactory { get; set; }

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

        // Farb-Cache für SkiaSharp initialisieren
        SkiaThemeHelper.RefreshColors();

        // Initialize localization
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
        // Gleiche Versions-Quelle wie SettingsViewModel.AppVersion (Assembly-Version)
        return new SkiaLoadingSplash
        {
            AppName = "HandwerkerRechner",
            AppVersion = $"v{System.Reflection.Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString(3) ?? "2.0.0"}",
            Renderer = new HandwerkerRechnerSplashRenderer()
        };
    }

    private async Task RunLoadingAsync(SkiaLoadingSplash splash)
    {
        try
        {
            var pipeline = new HandwerkerRechnerLoadingPipeline(Services);
            pipeline.ProgressChanged += (progress, text) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    splash.Progress = progress;
                    splash.StatusText = text;
                });

            var sw = Stopwatch.StartNew();
            await pipeline.ExecuteAsync();

            // Mindestens 800ms anzeigen damit die Splash-Animation sichtbar ist
            var remaining = 800 - (int)sw.ElapsedMilliseconds;
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
            Debug.WriteLine($"[HandwerkerRechner] Loading-Pipeline fehlgeschlagen: {ex}");

            // Best-effort-Recovery: MainViewModel direkt auf dem UI-Thread auflösen und setzen,
            // sonst bleibt die MainView ohne DataContext = tote leere UI (im Release still).
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var mainVm = Services.GetRequiredService<MainViewModel>();

                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                        && desktop.MainWindow != null)
                        desktop.MainWindow.DataContext = mainVm;
                    else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform
                             && singleViewPlatform.MainView != null)
                        singleViewPlatform.MainView.DataContext = mainVm;
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"[HandwerkerRechner] MainViewModel-Fallback fehlgeschlagen: {fallbackEx}");

                    // Letzter Ausweg: Fehlertext statt leerer UI anzeigen
                    if (splash.Parent is Panel panel)
                    {
                        panel.Children.Add(new TextBlock
                        {
                            Text = $"HandwerkerRechner could not be started.\n{fallbackEx.Message}",
                            Foreground = Brushes.White,
                            TextWrapping = TextWrapping.Wrap,
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(24),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        });
                    }
                }

                splash.FadeOut();
            });
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("HandwerkerRechner"));
        services.AddSingleton<IUnitConverterService, UnitConverterService>();
        services.AddSingleton<ICalculationHistoryService, CalculationHistoryService>();

        // Premium Services (Ads, Purchases)
        services.AddMeineAppsPremium();

        // Android-Override: Echte Rewarded Ads statt Desktop-Simulator
        // lazy, Avalonia-12-Factory-Timing: Factory wird erst beim Resolve (nach MainActivity) gelesen
        services.AddSingleton<IRewardedAdService>(sp =>
            RewardedAdServiceFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance<RewardedAdService>(sp));

        // Android-Override: Echte Google Play Billing statt Stub
        // lazy, Avalonia-12-Factory-Timing
        services.AddSingleton<IPurchaseService>(sp =>
            PurchaseServiceFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance<PurchaseService>(sp));

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // App Services
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IFavoritesService, FavoritesService>();
        services.AddSingleton<IProjectTemplateService, ProjectTemplateService>();
        services.AddSingleton<IMaterialPriceService, MaterialPriceService>();
        services.AddSingleton<IQuoteService, QuoteService>();

        // Export Services - Plattformspezifisch: Android setzt Factory, Desktop nutzt Default
        // lazy, Avalonia-12-Factory-Timing: Factory wird erst beim Resolve (nach MainActivity) gelesen
        services.AddSingleton<IFileShareService>(sp =>
            FileShareServiceFactory?.Invoke() ?? ActivatorUtilities.CreateInstance<DesktopFileShareService>(sp));
        services.AddSingleton<IMaterialExportService, MaterialExportService>();

        // Photo-Picker: Plattformspezifisch via Factory oder Desktop-Default
        // lazy, Avalonia-12-Factory-Timing
        services.AddSingleton<IPhotoPickerService>(sp =>
            PhotoPickerServiceFactory?.Invoke() ?? ActivatorUtilities.CreateInstance<DesktopPhotoPickerService>(sp));

        // Engine
        services.AddSingleton<CraftEngine>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ProjectsViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<ProjectTemplatesViewModel>();
        services.AddSingleton<QuoteViewModel>();

        // Floor Calculator ViewModels
        services.AddTransient<TileCalculatorViewModel>();
        services.AddTransient<PaintCalculatorViewModel>();
        services.AddTransient<WallpaperCalculatorViewModel>();
        services.AddTransient<FlooringCalculatorViewModel>();
        services.AddTransient<ConcreteCalculatorViewModel>();

        // Profi-Werkzeuge ViewModels
        services.AddTransient<HourlyRateViewModel>();
        services.AddTransient<MaterialCompareViewModel>();
        services.AddTransient<AreaMeasureViewModel>();

        // Premium Calculator ViewModels
        services.AddTransient<DrywallViewModel>();
        services.AddTransient<ElectricalViewModel>();
        services.AddTransient<MetalViewModel>();
        services.AddTransient<GardenViewModel>();
        services.AddTransient<RoofSolarViewModel>();
        services.AddTransient<StairsViewModel>();
        services.AddTransient<PlasterViewModel>();
        services.AddTransient<ScreedViewModel>();
        services.AddTransient<InsulationViewModel>();
        services.AddTransient<CableSizingViewModel>();
        services.AddTransient<GroutViewModel>();

        // Zentraler Factory-Service für alle 19 Calculator-VMs (statt 19 einzelne Func<T>)
        services.AddSingleton<ICalculatorFactoryService, CalculatorFactoryService>();
    }
}
