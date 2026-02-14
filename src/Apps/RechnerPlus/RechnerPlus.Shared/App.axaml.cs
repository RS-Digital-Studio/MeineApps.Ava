using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using RechnerPlus.Resources.Strings;
using RechnerPlus.Services;
using RechnerPlus.ViewModels;
using RechnerPlus.Views;

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
        _ = Services.GetRequiredService<IThemeService>();

        // Initialize localization
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

        var mainVm = Services.GetRequiredService<MainViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
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
