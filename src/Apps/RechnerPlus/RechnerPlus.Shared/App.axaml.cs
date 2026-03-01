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
using MeineApps.UI.Controls;
using RechnerPlus.Loading;
using RechnerPlus.Resources.Strings;
using RechnerPlus.Services;
using RechnerPlus.ViewModels;
using RechnerPlus.Views;
using MeineApps.UI.SkiaSharp;

namespace RechnerPlus;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Factory für plattformspezifischen Haptic-Service (Android setzt in MainActivity).</summary>
    public static Func<IServiceProvider, IHapticService>? HapticServiceFactory { get; set; }

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
        IImage? icon = null;
        try
        {
            var iconUri = new Uri("avares://RechnerPlus.Shared/Assets/icon.png");
            using var stream = Avalonia.Platform.AssetLoader.Open(iconUri);
            icon = new Bitmap(stream);
        }
        catch { /* Fallback: kein Icon */ }

        return new SkiaLoadingSplash
        {
            AppName = "RechnerPlus",
            AppVersion = "v2.0.5"
        };
    }

    private async Task RunLoadingAsync(SkiaLoadingSplash splash)
    {
        try
        {
            var pipeline = new RechnerPlusLoadingPipeline(Services);
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
            Debug.WriteLine($"[RechnerPlus] Loading-Pipeline fehlgeschlagen: {ex}");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => splash.FadeOut());
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("RechnerPlus"));
        services.AddSingleton<IThemeService, ThemeService>();

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // CalcLib
        services.AddSingleton<MeineApps.CalcLib.CalculatorEngine>();
        services.AddSingleton<MeineApps.CalcLib.ExpressionParser>();
        services.AddSingleton<MeineApps.CalcLib.IHistoryService, MeineApps.CalcLib.HistoryService>();

        // Haptic Feedback (Desktop: NoOp)
        if (HapticServiceFactory != null)
            services.AddSingleton(HapticServiceFactory);
        else
            services.AddSingleton<IHapticService>(new NoOpHapticService());

        // ViewModels (alle Singleton - werden von MainViewModel gehalten)
        services.AddSingleton<CalculatorViewModel>(sp =>
            new CalculatorViewModel(
                sp.GetRequiredService<MeineApps.CalcLib.CalculatorEngine>(),
                sp.GetRequiredService<MeineApps.CalcLib.ExpressionParser>(),
                sp.GetRequiredService<ILocalizationService>(),
                sp.GetRequiredService<MeineApps.CalcLib.IHistoryService>(),
                sp.GetRequiredService<IPreferencesService>(),
                sp.GetRequiredService<IHapticService>()));
        services.AddSingleton<ConverterViewModel>(sp =>
            new ConverterViewModel(sp.GetRequiredService<ILocalizationService>()));
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
