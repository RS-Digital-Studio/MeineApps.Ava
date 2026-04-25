using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BingXBot.Core.Configuration;
using BingXBot.Core.Interfaces;
using BingXBot.Engine;
using BingXBot.Engine.News;
using BingXBot.Exchange;
using BingXBot.ClientApi.Connection;
using BingXBot.ClientApi.Pairing;
using BingXBot.ClientApi.Services;
using BingXBot.ClientApi.SignalR;
using BingXBot.Contracts.Services;
using BingXBot.Services;
using BingXBot.Trading;
using BingXBot.Trading.Local;
using BingXBot.ViewModels;
using BingXBot.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Hinweis: IAppPaths-Default = AppPaths (Desktop/Linux). Android darf vor OnFrameworkInitializationCompleted
// über App.AppPathsFactory eine eigene Implementierung injizieren, z.B. mit Context.FilesDir.

namespace BingXBot;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Optionale Factory für IAppPaths. Android setzt diese in MainActivity vor dem DI-Build,
    /// damit Sandbox-spezifische Pfade (Context.FilesDir) statt der Default-Environment-Folder
    /// verwendet werden. Null = Standard <see cref="AppPaths"/> (Windows/Linux).
    /// </summary>
    public static Func<IAppPaths>? AppPathsFactory { get; set; }

    /// <summary>
    /// True wenn die App als SingleView-Lifetime läuft (Android). ViewLocator nutzt das, um
    /// zuerst die <c>XyzViewMobile</c>-Variante zu finden und bei Bedarf auf Desktop-Views zurückzufallen.
    /// Wird in <see cref="OnFrameworkInitializationCompleted"/> gesetzt.
    /// </summary>
    public static bool IsMobileShell { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // Initial Dark — wird nach Settings-Load in OnFrameworkInitializationCompleted ggf. ueberschrieben.
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    /// <summary>
    /// Setzt das Theme zur Laufzeit basierend auf User-Vorliebe. System = folgt OS.
    /// Wird sowohl beim Startup (nach Settings-Load) als auch bei UI-Switch aufgerufen.
    /// </summary>
    public static void ApplyTheme(BingXBot.Core.Configuration.ThemePreference pref)
    {
        if (Current is null) return;
        Current.RequestedThemeVariant = pref switch
        {
            BingXBot.Core.Configuration.ThemePreference.Light => Avalonia.Styling.ThemeVariant.Light,
            BingXBot.Core.Configuration.ThemePreference.System => Avalonia.Styling.ThemeVariant.Default,
            _ => Avalonia.Styling.ThemeVariant.Dark
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // DI-Container synchron aufbauen. ValidateOnBuild=true prueft beim Bootstrap, dass ALLE
        // registrierten Services konstruierbar sind — wenn ein Konstruktor-Param nicht resolvbar
        // ist (wie v1.3.5 IRateLimiter), crasht der App-Start hier mit klarer Fehlermeldung,
        // statt erst beim ersten tatsaechlichen Resolve. Fehler im Dev-Run statt beim User.
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        // Flag setzen (kein Service-Zugriff der werfen könnte)
        try
        {
            var bot = Services.GetRequiredService<BotSettings>();
            bot.UseRemoteMode = IsRemoteModeEnabled();

            // Server-Profil SYNCHRON laden — SettingsViewModel.UpdateServerStatus() soll beim ersten
            // Rendern korrekte Daten zeigen (sonst kurzzeitig "Nicht verbunden" + bingxbot.local-Default).
            // File.ReadAllText + JsonSerializer ist schnell (< 1ms), rechtfertigt die Synchronizität.
            if (bot.UseRemoteMode)
            {
                try { Services.GetRequiredService<BingXBot.ClientApi.Connection.ServerConnection>().LoadPersistedProfile(); }
                catch (Exception profEx) { System.Diagnostics.Debug.WriteLine($"ServerProfile-Load fehlgeschlagen: {profEx.Message}"); }
            }
        }
        catch { /* DI-Fehler ignorieren — App muss trotzdem starten */ }

        // UI sofort hochfahren — ViewModels tolerieren nicht-initialisierte DB/Settings
        // (zeigen Empty-States bis Hintergrund-Init abgeschlossen ist).
        // ViewLocator (in App.axaml als DataTemplate registriert) löst VM → View automatisch auf:
        // Content = MainViewModel → rendert MainView (Desktop) oder MainViewMobile (Android).
        var mainVm = Services.GetRequiredService<MainViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            IsMobileShell = false;
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
                Content = mainVm
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // Android/iOS — ViewLocator wählt automatisch MainViewMobile für MainViewModel.
            IsMobileShell = true;
            singleView.MainView = new Avalonia.Controls.ContentControl { Content = mainVm };
        }

        // Schwere Init im Hintergrund starten — UI bleibt responsive, Fehler crashen nicht die App.
        // ContinueWith-Guard faengt Exceptions vor dem try/catch in InitializeBackgroundAsync
        // (z.B. NullRef vor dem ersten try). Sonst wuerde die unbeobachtete Task-Exception
        // potentiell den Prozess beenden (TaskScheduler.UnobservedTaskException).
        _ = Task.Run(InitializeBackgroundAsync).ContinueWith(
            t => System.Diagnostics.Debug.WriteLine($"BG-Init failed: {t.Exception}"),
            TaskContinuationOptions.OnlyOnFaulted);

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Läuft asynchron NACH dem UI-Start: Remote-Profil laden, DB initialisieren,
    /// Settings aus DB bzw. Server laden. Exceptions werden nur geloggt — die App startet auch
    /// wenn DB/Netz nicht funktionieren (Empty-State in ViewModels).
    /// </summary>
    private static async Task InitializeBackgroundAsync()
    {
        // Remote-Profil-Load läuft bereits synchron in OnFrameworkInitializationCompleted.
        // Hier nicht nochmal — würde SetProfile erneut feuern und HttpClient.BaseAddress neu setzen.

        if (!IsRemoteModeEnabled())
        {
            try
            {
                var db = Services.GetRequiredService<BotDatabaseService>();
                await db.InitializeAsync().ConfigureAwait(false);

                var saved = await db.LoadSettingsAsync().ConfigureAwait(false);
                // RestoreSettingsFromDb greift auf DI-Singletons zu — zurück auf UI-Thread marshalen
                // ist nicht zwingend (POCO-Setter), aber wir machen es sicherheitshalber.
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    RestoreSettingsFromDb(saved);
                    ApplyTheme(saved.ThemePreference);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB-Init/Settings fehlgeschlagen: {ex.Message}");
            }
        }
        else
        {
            try
            {
                await RefreshRemoteSettingsAsync().ConfigureAwait(false);
                var stream = Services.GetRequiredService<IBotEventStream>();

                // v1.3.0 H1: Multi-Client-Settings-Sync — wenn ein anderer Client Settings
                // aendert, feuert der Server SettingsChanged. RemoteSettingsService publisht das
                // weiter an subscribte ViewModels (z.B. RiskSettingsView). Wire-up NACH DI-Build
                // vermeidet zirkulaere Registrierung zwischen Stream und SettingsService.
                if (Services.GetService<ISettingsService>() is RemoteSettingsService rs)
                {
                    stream.SettingsChanged += rs.RaiseChanged;
                }

                // Auto-Sync aktivieren: Ctor subscribed ConnectionChanged, damit bei jedem
                // Re-Connect die Server-Settings erneut in die DI-Singletons gespielt werden.
                // Eager-Resolve VOR StartAsync garantiert, dass das erste Connected-Event
                // den Handler trifft (auch wenn der Debounce den initialen Refresh-Call schluckt,
                // weil der App-Start-Refresh zwei Zeilen drueber schon lief).
                _ = Services.GetRequiredService<RemoteSettingsAutoSync>();

                await stream.StartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Remote-Init fehlgeschlagen: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Schreibt alle persistierten Settings aus der DB auf die DI-Singletons.
    /// Wird einmalig beim App-Start aufgerufen.
    /// </summary>
    private static void RestoreSettingsFromDb(BotSettings saved)
    {
        var risk = Services.GetRequiredService<RiskSettings>();
        var scanner = Services.GetRequiredService<ScannerSettings>();
        var bot = Services.GetRequiredService<BotSettings>();
        var backtest = Services.GetRequiredService<BacktestSettings>();

        // ============ Risk-Settings ============
        // Cap/Drawdown/Position-Konfiguration
        risk.MaxPositionSizePercent = saved.Risk.MaxPositionSizePercent;
        // MaxMarginPerTradePercent: Mindestens so groß wie MaxPositionSizePercent
        // (alte DB-Werte von 1-2% blockierten die Position auf winzige Beträge)
        risk.MaxMarginPerTradePercent = Math.Max(saved.Risk.MaxMarginPerTradePercent, saved.Risk.MaxPositionSizePercent);
        risk.MaxDailyDrawdownPercent = saved.Risk.MaxDailyDrawdownPercent;
        risk.MaxTotalDrawdownPercent = saved.Risk.MaxTotalDrawdownPercent;
        risk.MaxOpenPositions = saved.Risk.MaxOpenPositions;
        risk.MaxOpenPositionsPerSymbol = saved.Risk.MaxOpenPositionsPerSymbol;
        risk.MaxLeverage = saved.Risk.MaxLeverage;
        risk.Tp1CloseRatio = saved.Risk.Tp1CloseRatio;
        risk.Tp2CloseRatio = saved.Risk.Tp2CloseRatio;
        risk.MinRiskRewardRatio = saved.Risk.MinRiskRewardRatio;
        // SK-Buch + Strukturpunkte-Doku Compliance (v1.2.8)
        risk.BCZoneEntryStrategy = saved.Risk.BCZoneEntryStrategy;
        risk.EntryMode = saved.Risk.EntryMode;                              // User-Ausnahme: EntryMode.Both bleibt drin
        risk.RequireWickRejectionInBZone = saved.Risk.RequireWickRejectionInBZone;
        risk.RequireBoxCloseOnEntry = saved.Risk.RequireBoxCloseOnEntry;
        risk.HighProbabilityPositionMultiplier = saved.Risk.HighProbabilityPositionMultiplier;
        // Runner-TP (opt-in)
        risk.EnableRunner = saved.Risk.EnableRunner;
        risk.RunnerPercent = saved.Risk.RunnerPercent;
        risk.RunnerTrailingAtrMultiplier = saved.Risk.RunnerTrailingAtrMultiplier;
        // News + DailyRisk (User-Ausnahme: MaxDailyRiskPercent bleibt drin)
        risk.NewsBlackoutMinutes = saved.Risk.NewsBlackoutMinutes;
        risk.MaxRiskPercentPerTrade = saved.Risk.MaxRiskPercentPerTrade;
        risk.MaxDailyLossPercent = saved.Risk.MaxDailyLossPercent;
        risk.MaxDailyRiskPercent = saved.Risk.MaxDailyRiskPercent;          // war 24.04.2026 ungemappt
        if (saved.Risk.CategorySettings != null && saved.Risk.CategorySettings.Count > 0)
            risk.CategorySettings = saved.Risk.CategorySettings;
        if (saved.Risk.PipScalingByTf is { Count: > 0 })
            risk.PipScalingByTf = saved.Risk.PipScalingByTf;
        if (saved.Risk.SlBufferPipsByTf is { Count: > 0 })
            risk.SlBufferPipsByTf = saved.Risk.SlBufferPipsByTf;

        // ============ Scanner-Settings ============
        scanner.MinVolume24h = saved.Scanner.MinVolume24h;
        scanner.MinPriceChange = saved.Scanner.MinPriceChange;
        scanner.ScanTimeFrame = saved.Scanner.ScanTimeFrame;
        scanner.MaxResults = saved.Scanner.MaxResults;
        scanner.Mode = saved.Scanner.Mode;
        scanner.OnlyTopByVolume = saved.Scanner.OnlyTopByVolume;
        scanner.TopCoinsCount = saved.Scanner.TopCoinsCount;
        scanner.ScanIntervalSeconds = saved.Scanner.ScanIntervalSeconds;
        scanner.Whitelist = saved.Scanner.Whitelist;
        scanner.Blacklist = saved.Scanner.Blacklist;
        scanner.EnableTradFi = saved.Scanner.EnableTradFi;
        if (saved.Scanner.EnabledCategories != null && saved.Scanner.EnabledCategories.Count > 0)
            scanner.EnabledCategories = saved.Scanner.EnabledCategories;
        scanner.MinVolume24hTradFi = saved.Scanner.MinVolume24hTradFi;
        scanner.MinPriceChangeTradFi = saved.Scanner.MinPriceChangeTradFi;
        // Bias-Flip + Counter-Trend-Scalper (v1.2.7+)
        scanner.EnableBiasFlip = saved.Scanner.EnableBiasFlip;
        scanner.EnableCounterTrendScalp = saved.Scanner.EnableCounterTrendScalp;
        // SK-Buch Strukturpunkte-Doku Compliance (v1.2.8) — BOS/Pivot/Swing-Filter
        scanner.ImpulseAtrMultiplier = saved.Scanner.ImpulseAtrMultiplier;
        scanner.RequireBosVolumeBreakout = saved.Scanner.RequireBosVolumeBreakout;
        scanner.BosVolumeMultiplier = saved.Scanner.BosVolumeMultiplier;
        scanner.RequireBosCloseBreak = saved.Scanner.RequireBosCloseBreak;
        scanner.BosAnchorSwingStrength = saved.Scanner.BosAnchorSwingStrength;
        scanner.BosAnchorLeftBars = saved.Scanner.BosAnchorLeftBars;
        scanner.BosAnchorRightBars = saved.Scanner.BosAnchorRightBars;
        scanner.AdaptiveSwingStrength = saved.Scanner.AdaptiveSwingStrength;
        scanner.SwingStrengthMin = saved.Scanner.SwingStrengthMin;
        scanner.SwingStrengthMax = saved.Scanner.SwingStrengthMax;
        scanner.SwingStrengthAtrThresholdLow = saved.Scanner.SwingStrengthAtrThresholdLow;
        scanner.SwingStrengthAtrThresholdHigh = saved.Scanner.SwingStrengthAtrThresholdHigh;
        scanner.PivotLeftBars = saved.Scanner.PivotLeftBars;
        scanner.PivotRightBars = saved.Scanner.PivotRightBars;
        // MTA + Confluence-Overlap
        scanner.BlockLtfEntryWhenHtfInTargetZone = saved.Scanner.BlockLtfEntryWhenHtfInTargetZone;
        scanner.EnableConfluenceOverlapDetection = saved.Scanner.EnableConfluenceOverlapDetection;

        // Multi-TF Standalone (15.04.2026)
        if (saved.Scanner.ActiveTimeframes is { Count: > 0 })
            scanner.ActiveTimeframes = saved.Scanner.ActiveTimeframes;
        if (saved.Scanner.MinVolume24hByTf is { Count: > 0 })
            scanner.MinVolume24hByTf = saved.Scanner.MinVolume24hByTf;
        if (saved.Scanner.MinPriceChangeByTf is { Count: > 0 })
            scanner.MinPriceChangeByTf = saved.Scanner.MinPriceChangeByTf;
        if (saved.Scanner.MaxResultsByTf is { Count: > 0 })
            scanner.MaxResultsByTf = saved.Scanner.MaxResultsByTf;
        // TradFi-By-TF (24.04.2026: Symmetrie-Fix nach Debugger-Audit — beide Dictionaries waren ungemappt).
        if (saved.Scanner.MinVolume24hTradFiByTf is { Count: > 0 })
            scanner.MinVolume24hTradFiByTf = saved.Scanner.MinVolume24hTradFiByTf;
        if (saved.Scanner.MinPriceChangeTradFiByTf is { Count: > 0 })
            scanner.MinPriceChangeTradFiByTf = saved.Scanner.MinPriceChangeTradFiByTf;

        // Legacy-M5-Migration (19.04.2026: M5-Navigator → M15).
        // Übersetzt alte persistierte ActiveTimeframes mit M5 auf M15 und entfernt verwaiste M5-Dictionary-Keys.
        scanner.MigrateLegacyM5();
        risk.MigrateLegacyM5();

        // ============ Backtest-Settings ============ (24.04.2026: Health-Finding DI-1 — war nie gemappt)
        backtest.InitialBalance = saved.Backtest.InitialBalance;
        backtest.MakerFee = saved.Backtest.MakerFee;
        backtest.TakerFee = saved.Backtest.TakerFee;
        backtest.SlippagePercent = saved.Backtest.SlippagePercent;
        backtest.SimulateFundingRate = saved.Backtest.SimulateFundingRate;
        backtest.SimulatedFundingRatePercent = saved.Backtest.SimulatedFundingRatePercent;
        backtest.UseDynamicSlippage = saved.Backtest.UseDynamicSlippage;
        backtest.SpreadPercent = saved.Backtest.SpreadPercent;
        backtest.MaxLatencyMs = saved.Backtest.MaxLatencyMs;
        backtest.OrderRejectionPercent = saved.Backtest.OrderRejectionPercent;
        backtest.MinSlippageAtrMultiplier = saved.Backtest.MinSlippageAtrMultiplier;
        backtest.MaxSlippageAtrMultiplier = saved.Backtest.MaxSlippageAtrMultiplier;
        backtest.Tp1CloseRatio = saved.Backtest.Tp1CloseRatio;
        backtest.Tp2CloseRatio = saved.Backtest.Tp2CloseRatio;
        backtest.MinRiskRewardRatio = saved.Backtest.MinRiskRewardRatio;
        backtest.HtfTimeFrame = saved.Backtest.HtfTimeFrame;
        backtest.EntryTimeFrame = saved.Backtest.EntryTimeFrame;

        // ============ Bot-Settings ============
        bot.LastMode = saved.LastMode;
        bot.LastStrategyName = saved.LastStrategyName;
        bot.PaperInitialBalance = saved.PaperInitialBalance;
        bot.ShowBtcTicker = saved.ShowBtcTicker;
        bot.EnableDesktopNotifications = saved.EnableDesktopNotifications;
        bot.SimulatedFundingRatePercent = saved.SimulatedFundingRatePercent;
        bot.WasRunningOnShutdown = saved.WasRunningOnShutdown;              // 24.04.2026: konsistent zum Server
        // Referenzen in BotSettings auf die DI-Singletons zeigen (analog Server Program.cs)
        bot.Risk = risk;
        bot.Scanner = scanner;
        bot.Backtest = backtest;
    }

    /// <summary>
    /// Legacy-Wrapper für ViewModels die noch nicht auf <see cref="ISettingsPersistenceService"/> umgestellt sind.
    /// Neue ViewModels sollen den Service per DI injizieren — diese Methode ist nur Übergang.
    /// </summary>
    public static Task SaveAllSettingsAsync()
    {
        var svc = Services.GetService<ISettingsPersistenceService>();
        return svc?.SaveAllAsync() ?? Task.CompletedTask;
    }

    /// <summary>Prueft ob ein Server-Profil persistiert ist. Haengt NICHT am DI, laeuft vor Build.</summary>
    internal static bool IsRemoteModeEnabled()
    {
        try
        {
            var paths = AppPathsFactory?.Invoke() ?? new AppPaths();
            return File.Exists(paths.ClientProfilePath);
        }
        catch { return false; }
    }

    /// <summary>
    /// Holt den kompletten Settings-Snapshot vom Server (Bot/Risk/Scanner/Backtest) und
    /// synct die lokalen DI-Singletons fuer Binding-Kontinuitaet. Der Server ist im Remote-Mode
    /// die Authority fuer alle vier Blocks — nicht nur Bot.
    ///
    /// Wird beim App-Start einmal aufgerufen und danach vom <see cref="RemoteSettingsAutoSync"/>
    /// bei jedem Re-Connect — deshalb internal statt private (Zugriff fuer den Service).
    /// </summary>
    internal static async Task RefreshRemoteSettingsAsync()
    {
        try
        {
            var settings = Services.GetRequiredService<ISettingsService>();
            var snapshot = await settings.GetAsync();

            // Server-Authority fuer alle 4 Sub-Bloecke in die Client-Singletons spielen.
            // snapshot.Bot haelt zwar eine Nav-Ref auf Risk/Scanner/Backtest, aber wir wollen die
            // lokalen DI-Instanzen behalten (ViewModels halten Refs darauf) — daher die Werte
            // via RestoreSettingsFromDb pro Block "per Zuweisung" kopieren.
            var pseudoDbSettings = snapshot.Bot;
            pseudoDbSettings.Risk = snapshot.Risk;
            pseudoDbSettings.Scanner = snapshot.Scanner;
            pseudoDbSettings.Backtest = snapshot.Backtest;
            RestoreSettingsFromDb(pseudoDbSettings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Remote-Settings-Refresh fehlgeschlagen: {ex.Message}");
        }
    }

    // Internal fuer DI-Container-Validation-Tests (InternalsVisibleTo=BingXBot.Tests).
    // WICHTIG: Bei Aenderungen am Konstruktor-Vertrag eines Services (Interface-Extract etc.)
    // IMMER auch die Registrierung hier nachziehen. ValidateOnBuild=true im Startup faengt das.
    internal static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder => builder
            .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug));

        // Pfade (plattformabhängig — Android setzt Factory vor DI-Build)
        services.AddSingleton<IAppPaths>(_ => AppPathsFactory?.Invoke() ?? new AppPaths());

        // Settings (Singleton)
        services.AddSingleton<RiskSettings>();
        services.AddSingleton<ScannerSettings>();
        services.AddSingleton<BacktestSettings>();
        services.AddSingleton<BotSettings>();

        // Services
        services.AddSingleton<BotDatabaseService>();
        services.AddSingleton<ISecureStorageService, SecureStorageService>();
        services.AddSingleton<BotEventBus>();

        // Settings-Persistenz (ersetzt statische App.SaveAllSettingsAsync)
        services.AddSingleton<ISettingsPersistenceService, SettingsPersistenceService>();

        // Öffentlicher BingX-Client (kein API-Key nötig)
        services.AddSingleton<HttpClient>();
        services.AddSingleton<RateLimiter>();
        // IRateLimiter binden: BingXRestClient/BingXPublicClient nehmen jetzt das Interface (P3-2).
        // Gleiche Instanz wie die konkrete Klasse — sonst zwei Limiter = doppeltes Request-Budget.
        services.AddSingleton<IRateLimiter>(sp => sp.GetRequiredService<RateLimiter>());
        services.AddSingleton<BingXPublicClient>();
        services.AddSingleton<IPublicMarketDataClient>(sp => sp.GetRequiredService<BingXPublicClient>());

        // Engine
        services.AddSingleton<StrategyManager>();

        // News-Filter (SK-System Punkt 11 — Masterclass-Compliance):
        // Standalone-Desktop/Android: Default Stub (keine Netz-Abhängigkeit, graceful degradation).
        // Remote-Modus ignoriert den Service — der RiskManager lebt auf dem Server.
        // Aktivierung HTTP: BotSettings um News-Endpoint erweitern und hier einen HttpEconomicCalendarService
        // registrieren (analog BingXBot.Server/Program.cs).
        services.AddSingleton<IEconomicCalendarService, StubEconomicCalendarService>();

        // Trading Services — ScannerResultsCache wird via Setter injiziert (vermeidet zirkuläre DI)
        services.AddSingleton<PaperTradingService>(sp =>
        {
            var svc = ActivatorUtilities.CreateInstance<PaperTradingService>(sp);
            svc.SetScannerResultsCache(sp.GetService<ScannerResultsCache>());
            return svc;
        });
        services.AddSingleton<LiveTradingManager>();

        // Service-Fassaden fuer Bot-Steuerung + Settings + Events.
        // Modus-Umschaltung: Server-URL persistiert => Remote-Impls (HTTP+SignalR). Sonst Local-Impls.
        var remoteMode = IsRemoteModeEnabled();
        services.AddSingleton<ScannerResultsCache>();

        // LocalBotEventStream nur im Local-Modus registrieren — im Remote-Modus wird RemoteBotEventStream
        // als IBotEventStream gebunden, LocalBotEventStream wird nie aufgeloest (tote Instanz + tote
        // Subscriptions auf BotEventBus). Conditional-Registrierung spart Memory und vermeidet
        // irrefuehrende Diagnose-Logs beim Debuggen.
        if (!remoteMode) services.AddSingleton<LocalBotEventStream>();

        // ClientApi-Infrastruktur IMMER registrieren — im Local-Modus ungenutzt, aber fuer UI
        // (Pairing-Screen + Connection-Status-Anzeige) verfuegbar.
        // DI-Zirkel (Connection braucht HttpClient mit Handler, Handler braucht Connection)
        // wird per Func-Provider aufgeloest: Late-Binding, keine Reflection.
        services.AddSingleton<PairingClient>();
        services.AddSingleton(sp =>
        {
            ServerConnection? connRef = null;
            var handler = new BingXBot.ClientApi.Http.TokenRefreshHandler(
                connectionProvider: () => connRef ?? throw new InvalidOperationException("ServerConnection nicht initialisiert"),
                pairingProvider: () => sp.GetRequiredService<PairingClient>())
            {
                InnerHandler = new HttpClientHandler()
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            connRef = new ServerConnection(client, sp.GetRequiredService<IAppPaths>());
            return connRef;
        });

        if (remoteMode)
        {
            // Remote-Mode: Alle Interfaces an ClientApi-Remote-Impls gebunden.
            services.AddSingleton<IBotEventStream, RemoteBotEventStream>();
            services.AddSingleton<ISettingsService, RemoteSettingsService>();
            services.AddSingleton<IAccountService, RemoteAccountService>();
            services.AddSingleton<ITradeHistoryService, RemoteTradeHistoryService>();
            services.AddSingleton<IBotControlService, RemoteBotControlService>();
            services.AddSingleton<IBacktestControlService, RemoteBacktestService>();
            // Strategy-Katalog kann lokal bleiben (kein Netz-Call noetig).
            services.AddSingleton<IStrategyCatalog, LocalStrategyCatalog>();

            // Auto-Sync: Bei jedem (Re-)Connect die Server-Settings neu in die Client-DI-
            // Singletons spielen (Risk/Scanner/Bot/Backtest). Vermeidet den "Client haelt
            // alten Stand und ueberschreibt Server-Werte beim naechsten Save"-Fall.
            // Delegate-Wrapper auf die static RefreshRemoteSettingsAsync — der Service ist
            // generisch und kennt App.axaml.cs nicht direkt.
            services.AddSingleton<RemoteSettingsAutoSync>(sp =>
                new RemoteSettingsAutoSync(sp.GetRequiredService<IBotEventStream>(), RefreshRemoteSettingsAsync));
        }
        else
        {
            // Standalone-Desktop: Engine laeuft im Prozess.
            services.AddSingleton<IBotEventStream>(sp => sp.GetRequiredService<LocalBotEventStream>());
            services.AddSingleton<ISettingsService, LocalSettingsService>();
            services.AddSingleton<IAccountService, LocalAccountService>();
            services.AddSingleton<ITradeHistoryService, LocalTradeHistoryService>();
            services.AddSingleton<IBotControlService, LocalBotControlService>();
            services.AddSingleton<IBacktestControlService, LocalBacktestService>();
            services.AddSingleton<IStrategyCatalog, LocalStrategyCatalog>();
        }

        // ViewModels: Dashboard ist Startup-Page (eager Singleton).
        // Alle anderen werden per Lazy<T> injiziert und erst bei Navigation resolved.
        // Spart ~6 transitive VM-Ctors beim Startup (SignalR, Backtest-Engine, Scanner-Init).
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<StrategyViewModel>();
        services.AddSingleton<BacktestViewModel>();
        services.AddSingleton<TradeHistoryViewModel>();
        services.AddSingleton<ScannerViewModel>();
        services.AddSingleton<RiskSettingsViewModel>();
        services.AddSingleton<LogViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Lazy-Wrapper: DI-Container unterstuetzt Lazy<T> nicht out-of-the-box.
        // Transient-Registrierung genuegt (Lazy selbst ist billig; die gewrappten VMs bleiben Singleton).
        services.AddTransient(typeof(Lazy<>), typeof(LazyDiService<>));
    }
}

/// <summary>
/// Lazy&lt;T&gt;-DI-Wrapper: Erlaubt Lazy-Injection fuer beliebige Service-Typen.
/// Microsoft.Extensions.DependencyInjection kann Lazy&lt;T&gt; nicht auto-aufloesen.
/// </summary>
internal sealed class LazyDiService<T>(IServiceProvider sp) : Lazy<T>(() => sp.GetRequiredService<T>()!) where T : notnull { }
