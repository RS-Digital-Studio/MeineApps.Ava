using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BingXBot.Core.Configuration;
using BingXBot.Core.Interfaces;
using BingXBot.Engine;
using BingXBot.Engine.ATI;
using BingXBot.Exchange;
using BingXBot.Services;
using BingXBot.ViewModels;
using BingXBot.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BingXBot;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // DI-Container synchron aufbauen
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // DB synchron initialisieren BEVOR Views/VMs darauf zugreifen
        // (Desktop-App, schnelle lokale SQLite-Init, kein UI-Blocking-Problem)
        try
        {
            var db = Services.GetRequiredService<BotDatabaseService>();
            db.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DB-Init fehlgeschlagen: {ex.Message}");
        }

        // Fenster erstellen (VMs können jetzt sicher auf DB zugreifen)
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            var mainView = new MainView();
            desktop.MainWindow.Content = mainView;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder => builder
            .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug));

        // Settings (Singleton)
        services.AddSingleton<RiskSettings>();
        services.AddSingleton<ScannerSettings>();
        services.AddSingleton<BacktestSettings>();
        services.AddSingleton<BotSettings>();

        // Services
        services.AddSingleton<BotDatabaseService>();
        services.AddSingleton<ISecureStorageService, SecureStorageService>();
        services.AddSingleton<BotEventBus>();

        // Öffentlicher BingX-Client (kein API-Key nötig)
        services.AddSingleton<HttpClient>();
        services.AddSingleton<RateLimiter>();
        services.AddSingleton<BingXPublicClient>();
        services.AddSingleton<IPublicMarketDataClient>(sp => sp.GetRequiredService<BingXPublicClient>());

        // Engine
        services.AddSingleton<StrategyManager>();

        // ATI - Adaptive Trading Intelligence
        services.AddSingleton<AdaptiveTradingIntelligence>();

        // Trading Services
        services.AddSingleton<PaperTradingService>();
        services.AddSingleton<LiveTradingManager>();

        // ViewModels (Singleton)
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<StrategyViewModel>();
        services.AddSingleton<BacktestViewModel>();
        services.AddSingleton<TradeHistoryViewModel>();
        services.AddSingleton<ScannerViewModel>();
        services.AddSingleton<RiskSettingsViewModel>();
        services.AddSingleton<LogViewModel>();
        services.AddSingleton<SettingsViewModel>();
    }
}
