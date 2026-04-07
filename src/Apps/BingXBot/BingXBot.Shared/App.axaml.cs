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

        // DB initialisieren + Settings laden (Task.Run verhindert SynchronizationContext-Deadlock)
        try
        {
            var db = Services.GetRequiredService<BotDatabaseService>();
            Task.Run(() => db.InitializeAsync()).GetAwaiter().GetResult();

            // Alle persistierten Settings aus DB laden und auf DI-Singletons schreiben
            var saved = Task.Run(() => db.LoadSettingsAsync()).GetAwaiter().GetResult();
            RestoreSettingsFromDb(saved);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DB-Init/Settings fehlgeschlagen: {ex.Message}");
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

    /// <summary>
    /// Schreibt alle persistierten Settings aus der DB auf die DI-Singletons.
    /// Wird einmalig beim App-Start aufgerufen.
    /// </summary>
    private void RestoreSettingsFromDb(BotSettings saved)
    {
        var risk = Services.GetRequiredService<RiskSettings>();
        var scanner = Services.GetRequiredService<ScannerSettings>();
        var bot = Services.GetRequiredService<BotSettings>();

        // Risk-Settings
        risk.MaxPositionSizePercent = saved.Risk.MaxPositionSizePercent;
        risk.MaxMarginPerTradePercent = saved.Risk.MaxMarginPerTradePercent;
        risk.MaxDailyDrawdownPercent = saved.Risk.MaxDailyDrawdownPercent;
        risk.MaxTotalDrawdownPercent = saved.Risk.MaxTotalDrawdownPercent;
        risk.MaxOpenPositions = saved.Risk.MaxOpenPositions;
        risk.MaxOpenPositionsPerSymbol = saved.Risk.MaxOpenPositionsPerSymbol;
        risk.MaxLeverage = saved.Risk.MaxLeverage;
        risk.CheckCorrelation = saved.Risk.CheckCorrelation;
        risk.MaxCorrelation = saved.Risk.MaxCorrelation;
        risk.EnableTrailingStop = saved.Risk.EnableTrailingStop;
        risk.TrailingStopPercent = saved.Risk.TrailingStopPercent;
        risk.MinLiquidationDistancePercent = saved.Risk.MinLiquidationDistancePercent;
        risk.MaxNetExposurePercent = saved.Risk.MaxNetExposurePercent;
        risk.ConsiderFundingRate = saved.Risk.ConsiderFundingRate;
        risk.MaxAdverseFundingRatePercent = saved.Risk.MaxAdverseFundingRatePercent;
        risk.CooldownHours = saved.Risk.CooldownHours;
        risk.EnableCooldownEscalation = saved.Risk.EnableCooldownEscalation;
        risk.MaxCooldownHours = saved.Risk.MaxCooldownHours;
        risk.EnableEquityCurveTrading = saved.Risk.EnableEquityCurveTrading;
        risk.EquityCurvePeriod = saved.Risk.EquityCurvePeriod;
        risk.EnableMomentumDecay = saved.Risk.EnableMomentumDecay;
        risk.EnableMultiStageExit = saved.Risk.EnableMultiStageExit;
        risk.Tp1CloseRatio = saved.Risk.Tp1CloseRatio;
        risk.Tp2CloseRatio = saved.Risk.Tp2CloseRatio;
        risk.MaxHoldHours = saved.Risk.MaxHoldHours;
        risk.MaxHoldHoursAfterTp1 = saved.Risk.MaxHoldHoursAfterTp1;
        risk.SmartBreakevenAtrMultiplier = saved.Risk.SmartBreakevenAtrMultiplier;
        risk.MinRiskRewardRatio = saved.Risk.MinRiskRewardRatio;

        // Scanner-Settings (inkl. Watchlist)
        scanner.MinVolume24h = saved.Scanner.MinVolume24h;
        scanner.MinPriceChange = saved.Scanner.MinPriceChange;
        scanner.ScanTimeFrame = saved.Scanner.ScanTimeFrame;
        scanner.MaxResults = saved.Scanner.MaxResults;
        scanner.Whitelist = saved.Scanner.Whitelist;
        scanner.Blacklist = saved.Scanner.Blacklist;
        scanner.UseM15EntryTiming = saved.Scanner.UseM15EntryTiming;

        // Bot-Settings
        bot.LastMode = saved.LastMode;
        bot.LastStrategyName = saved.LastStrategyName;
        bot.LastTradingModePreset = saved.LastTradingModePreset;
        bot.PaperInitialBalance = saved.PaperInitialBalance;
        bot.ShowBtcTicker = saved.ShowBtcTicker;
        bot.EnableDesktopNotifications = saved.EnableDesktopNotifications;
        bot.SimulatedFundingRatePercent = saved.SimulatedFundingRatePercent;
        bot.AtiMinTradesBeforeLearning = saved.AtiMinTradesBeforeLearning;
        bot.AtiAutoSaveIntervalMinutes = saved.AtiAutoSaveIntervalMinutes;
    }

    /// <summary>
    /// Speichert ALLE aktuellen Settings (Risk + Scanner + Bot) in die DB.
    /// Zentrale Methode die von allen ViewModels aufgerufen werden soll.
    /// SemaphoreSlim schuetzt gegen parallele fire-and-forget Aufrufe.
    /// </summary>
    private static readonly SemaphoreSlim _saveSemaphore = new(1, 1);
    public static async Task SaveAllSettingsAsync()
    {
        if (!await _saveSemaphore.WaitAsync(500)) return; // Skip wenn bereits am Speichern
        try
        {
            var db = Services.GetService<BotDatabaseService>();
            if (db == null) return;

            var bot = Services.GetRequiredService<BotSettings>();
            bot.Risk = Services.GetRequiredService<RiskSettings>();
            bot.Scanner = Services.GetRequiredService<ScannerSettings>();

            await db.SaveSettingsAsync(bot);
        }
        finally { _saveSemaphore.Release(); }
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
        services.AddSingleton<MultiModeOrchestrator>();

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
