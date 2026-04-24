using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Interfaces;
using BingXBot.Engine;
using BingXBot.Engine.News;
using BingXBot.Exchange;
using BingXBot.Server;
using BingXBot.Server.Api;
using BingXBot.Server.Auth;
using BingXBot.Server.Hubs;
using BingXBot.Trading;
using BingXBot.Trading.Local;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ============ DI-Container ============
var services = builder.Services;

// Server-Internals
services.AddSingleton<AuthTokenStore>();
services.AddSingleton<PairingService>();
services.AddSingleton<PiCredentialStore>();
services.AddSingleton<ISecureStorageService>(sp => sp.GetRequiredService<PiCredentialStore>());

// Plattform-Pfade (Linux — siehe DatabasePathOverride fuer Server:DataDirectory)
services.AddSingleton<IAppPaths, AppPaths>();

// Settings (werden aus DB beim Start geladen)
services.AddSingleton<BotSettings>();
services.AddSingleton<RiskSettings>();
services.AddSingleton<ScannerSettings>();
services.AddSingleton<BacktestSettings>();

// Engine-Services
services.AddSingleton<BotDatabaseService>();
services.AddSingleton<BotEventBus>();
services.AddSingleton<BingXBot.Exchange.RateLimiter>();

// Getrennte HttpClient-Instanzen pro Konsument — verhindert Cross-Contamination von
// DefaultRequestHeaders/Timeouts zwischen BingX-Public-API und News-API. Ein gemeinsamer
// Singleton-HttpClient waere ein Race-Risiko, sobald ein Service per Request Default-Header
// (Auth, Accept, ...) setzt — der andere Consumer sieht sie dann ebenfalls.
services.AddSingleton<BingXPublicClient>(sp => new BingXPublicClient(
    new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
    sp.GetRequiredService<BingXBot.Exchange.RateLimiter>(),
    sp.GetRequiredService<ILogger<BingXPublicClient>>()));
services.AddSingleton<IPublicMarketDataClient>(sp => sp.GetRequiredService<BingXPublicClient>());
services.AddSingleton<StrategyManager>();

// News-Filter (SK-System Punkt 11 — Masterclass-Compliance):
// HTTP-Impl nur wenn "News:Endpoint" konfiguriert — sonst Stub (leere Liste, graceful degradation).
// Der Stub ist bewusst der Default: ohne Endpoint wollen wir keinen Crash, keine Blockade.
// Produktions-Setup: "News:Endpoint" = TradingEconomics-URL mit ?c=guest:guest&importance=3
// (oder bezahlter API-Key via "News:ApiKey").
services.AddSingleton<IEconomicCalendarService>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var endpoint = cfg.GetValue<string>("News:Endpoint");
    if (string.IsNullOrWhiteSpace(endpoint))
        return new StubEconomicCalendarService();

    // Dedizierte HttpClient-Instanz — nicht aus dem DI-Singleton (siehe BingXPublicClient oben).
    var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    var logger = sp.GetRequiredService<ILogger<HttpEconomicCalendarService>>();
    var config = new HttpEconomicCalendarConfig
    {
        Endpoint = endpoint,
        ApiKey = cfg.GetValue<string>("News:ApiKey"),
        Format = Enum.TryParse<NewsFeedFormat>(cfg.GetValue<string>("News:Format"), true, out var fmt)
            ? fmt
            : NewsFeedFormat.TradingEconomics
    };
    return new HttpEconomicCalendarService(http, config, logger);
});
services.AddSingleton<PaperTradingService>(sp =>
{
    var svc = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<PaperTradingService>(sp);
    svc.SetScannerResultsCache(sp.GetService<ScannerResultsCache>());
    return svc;
});
services.AddSingleton<LiveTradingManager>();

// Local-Impls (Wrapper um Engine-Services)
services.AddSingleton<ScannerResultsCache>();
services.AddSingleton<LocalBotEventStream>();
services.AddSingleton<IBotEventStream>(sp => sp.GetRequiredService<LocalBotEventStream>());
services.AddSingleton<ISettingsService, LocalSettingsService>();
services.AddSingleton<IAccountService, LocalAccountService>();
services.AddSingleton<ITradeHistoryService, LocalTradeHistoryService>();
services.AddSingleton<IBotControlService, LocalBotControlService>();
services.AddSingleton<IBacktestControlService, LocalBacktestService>();
services.AddSingleton<IStrategyCatalog, LocalStrategyCatalog>();

// Hub-Forwarder als HostedService: faengt EventBus-Events ab -> SignalR
services.AddHostedService<BotHubEventForwarder>();

// Auto-Resume (24.04.2026): Reaktiviert die Engine nach Server-Restart, wenn vor dem
// Shutdown der Bot lief (BotSettings.WasRunningOnShutdown=true). Verhindert "stiller-Bot"-
// Szenarien nach update.sh / Pi-Reboot. Reihenfolge wichtig: NACH BotHubEventForwarder
// registrieren, damit SignalR die ersten Resume-Logs/State-Events ueberhaupt forwarden kann.
services.AddHostedService<BingXBot.Server.Services.BotAutoResumeService>();

// FCM-Push (Stub ohne Firebase-Service-Account, Push-Versand wird geloggt statt geschickt).
// Aktivierung: firebase-service-account.json in DataDirectory ablegen + FirebaseAdmin NuGet hinzufuegen.
services.AddSingleton<BingXBot.Server.Services.FcmDeviceStore>();
services.AddHostedService<BingXBot.Server.Services.FcmPushService>();

// Log-Ringpuffer: liefert den Server-seitigen /api/v1/logs-Endpoint. Clients sehen nach
// Reconnect die letzten N Eintraege statt einer leeren Log-Ansicht.
services.AddSingleton<BingXBot.Server.Services.LogBufferService>();

// Connection-Health-Watchdog: Ueberwacht alle 30s ob der Live-Exchange-Client noch verbunden
// ist und pushed einen ConnectionDegraded-Event ueber den SignalR-Hub wenn sich der Status
// aendert. Edge-Transition-basiert — kein Spam bei stabilem Zustand.
services.AddHostedService<BingXBot.Server.Services.ServerHealthWatchdog>();

// SignalR
services.AddSignalR(opts =>
{
    opts.EnableDetailedErrors = builder.Environment.IsDevelopment();
    opts.KeepAliveInterval = TimeSpan.FromSeconds(15);
}).AddJsonProtocol();

// Rate-Limiting fuer sicherheitskritische Endpoints.
// Jeder Endpoint hat einen eigenen Bucket — verhindert Cross-Endpoint-DoS
// (sonst koennte /auth/refresh-Spam das /pair/* fuer legitime User blockieren).
services.AddRateLimiter(opts =>
{
    // Pair-Init: max 5 Requests pro 5 Minuten (verhindert Code-Datei-Ueberschreibung)
    opts.AddFixedWindowLimiter("pair-init", o =>
    {
        o.Window = TimeSpan.FromMinutes(5);
        o.PermitLimit = 5;
        o.QueueLimit = 0;
    });
    // Pair-Complete: eigener Bucket, 10/5min (5 Retries pro pairingId sind intern geprueft)
    opts.AddFixedWindowLimiter("pair-complete", o =>
    {
        o.Window = TimeSpan.FromMinutes(5);
        o.PermitLimit = 10;
        o.QueueLimit = 0;
    });
    // Refresh: eigener Bucket, haeufiger als Pair weil Auto-Refresh bei token-expiry
    opts.AddFixedWindowLimiter("refresh", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 10;
        o.QueueLimit = 0;
    });
    // Credentials-Read: GET /credentials/status — Dashboard-Polls sind OK, 60/min pro Client.
    opts.AddFixedWindowLimiter("credentials-read", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 60;
        o.QueueLimit = 0;
    });
    // Credentials-Write: PUT /credentials — API-Key-Aenderungen sollen selten sein, 3/min Anti-Spam.
    // Vorher: Status-GET + Write-PUT teilten sich denselben Bucket — normale Dashboard-Polls
    // haetten einen legitimen Key-Set-PUT blockieren koennen.
    opts.AddFixedWindowLimiter("credentials-write", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 3;
        o.QueueLimit = 0;
    });
    // Settings-PUT: schuetzt DB-WAL und Engine-Singletons vor hochfrequentem Save-Spam
    // (ein malicious Client koennte sonst 1000x/s saven → Lock-Contention, Engine-Stall).
    opts.AddFixedWindowLimiter("settings", o =>
    {
        o.Window = TimeSpan.FromSeconds(10);
        o.PermitLimit = 20;
        o.QueueLimit = 0;
    });
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// CORS absichern: AllowAnyOrigin ist fuer einen Pi im LAN/Tailscale akzeptabel, aber schuetzt
// nicht gegen XSS auf anderen Seiten im gleichen Netz. Konfiguration "Cors:Origins" in
// appsettings.json erlaubt Whitelist (Komma-getrennt). Ohne Konfiguration: AllowAnyOrigin
// (Backwards-Compat, Warn-Log beim Start).
services.AddCors(p => p.AddDefaultPolicy(b =>
{
    var cfg = builder.Configuration;
    var origins = cfg.GetValue<string>("Cors:Origins");
    if (!string.IsNullOrWhiteSpace(origins))
    {
        var list = origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        b.WithOrigins(list).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
    else
    {
        // Dev-Fallback + explizite localhost-Ports, damit Desktop-Client im LAN + Tailscale laeuft
        // aber random Webseiten NICHT Preflight-frei gegen unseren Pi requesten koennen.
        b.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    }
}));

// ============ App bauen ============
var app = builder.Build();

// Datenbank + Settings laden — DB-Pfad aus Server:DataDirectory (kollidiert nicht mit Desktop)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotDatabaseService>();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var dataDir = cfg.GetValue<string>("Server:DataDirectory");
    if (string.IsNullOrWhiteSpace(dataDir))
    {
        dataDir = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BingXBot", "Server")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "bingxbot");
    }
    Directory.CreateDirectory(dataDir);
    db.DatabasePathOverride = Path.Combine(dataDir, "bot.db");

    await db.InitializeAsync();
    var saved = await db.LoadSettingsAsync();
    // Auto-Resume-Flag seit 24.04.2026 separater DB-Key (statt Teil von BotSettings-JSON).
    // Der Bot-Singleton haelt den Flag in-memory, aber die Quelle der Wahrheit ist `AutoResumeFlag`.
    saved.WasRunningOnShutdown = await db.LoadAutoResumeFlagAsync();
    ApplySettingsToSingletons(scope.ServiceProvider, saved);

    // Backtest-Jobs aus DB laden + orphans als Failed markieren.
    var backtestService = scope.ServiceProvider.GetRequiredService<BingXBot.Contracts.Services.IBacktestControlService>();
    if (backtestService is BingXBot.Trading.Local.LocalBacktestService localBt)
        await localBt.RestoreFromDbAsync();
}

app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<BearerAuthMiddleware>();

// Endpoints
app.MapAuthEndpoints();
app.MapStatusEndpoints();
app.MapBotControlEndpoints();
app.MapSettingsEndpoints();
app.MapTradesAndLogsEndpoints();
app.MapBacktestEndpoints();

// SignalR-Hub
app.MapHub<BotHub>(BingXBot.Contracts.Api.ApiRoutes.BotHubPath);

// Start: Event-Stream hochfahren (Local-Impl ist No-Op, aber Semantik halten).
// Singletons brauchen keinen Scope — Scope-Erstellung hier war redundant und irrefuehrend bei
// Test-Mocking (Scoped-Services wuerden verschieden von dem oben genutzten Scope).
{
    var stream = app.Services.GetRequiredService<IBotEventStream>();
    await stream.StartAsync();
}

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("BingXBot.Server laeuft auf {Urls}", string.Join(", ", app.Urls.DefaultIfEmpty("http://0.0.0.0:5050")));
logger.LogInformation("Pairing-Code via 'journalctl -u bingxbot -f' oder /var/lib/bingxbot/pairing-code.txt");

app.Run();


// Helper: Settings aus DB auf Singleton-Instanzen schreiben (analog App.axaml.cs)
static void ApplySettingsToSingletons(IServiceProvider sp, BotSettings saved)
{
    var risk = sp.GetRequiredService<RiskSettings>();
    var scanner = sp.GetRequiredService<ScannerSettings>();
    var bot = sp.GetRequiredService<BotSettings>();
    var backtest = sp.GetRequiredService<BacktestSettings>();

    // ============ Risk ============
    // Cap/Drawdown/Position-Konfiguration
    risk.MaxPositionSizePercent = saved.Risk.MaxPositionSizePercent;
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
    risk.MaxDailyRiskPercent = saved.Risk.MaxDailyRiskPercent;          // ← war 24.04.2026 ungemappt → User-Wert ging verloren
    // Dictionaries: nur uebernehmen wenn nicht-leer (Schutz vor Default-Reset bei alten Migration-Snapshots).
    if (saved.Risk.CategorySettings is { Count: > 0 })
        risk.CategorySettings = saved.Risk.CategorySettings;
    if (saved.Risk.PipScalingByTf is { Count: > 0 })
        risk.PipScalingByTf = saved.Risk.PipScalingByTf;
    if (saved.Risk.SlBufferPipsByTf is { Count: > 0 })
        risk.SlBufferPipsByTf = saved.Risk.SlBufferPipsByTf;

    // ============ Scanner ============
    // Kernparameter
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
    if (saved.Scanner.EnabledCategories is { Count: > 0 })
        scanner.EnabledCategories = saved.Scanner.EnabledCategories;
    scanner.MinVolume24hTradFi = saved.Scanner.MinVolume24hTradFi;
    scanner.MinPriceChangeTradFi = saved.Scanner.MinPriceChangeTradFi;
    // Bias-Flip + Counter-Trend-Scalper (v1.2.7+)
    scanner.EnableBiasFlip = saved.Scanner.EnableBiasFlip;
    scanner.EnableCounterTrendScalp = saved.Scanner.EnableCounterTrendScalp;
    // SK-Buch Strukturpunkte-Doku Compliance (v1.2.8) — alle BOS/Pivot/Swing-Filter
    scanner.ImpulseAtrMultiplier = saved.Scanner.ImpulseAtrMultiplier;
    scanner.RequireBosVolumeBreakout = saved.Scanner.RequireBosVolumeBreakout;
    scanner.BosVolumeMultiplier = saved.Scanner.BosVolumeMultiplier;
    scanner.RequireBosCloseBreak = saved.Scanner.RequireBosCloseBreak;
    scanner.BosAnchorSwingStrength = saved.Scanner.BosAnchorSwingStrength;
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
    // IsHedgeModeActive wird BEWUSST nicht gemappt: wird zur Laufzeit aus BingX gelesen (REST-Call IsHedgeModeAsync()).

    // Legacy-M5-Migration (19.04.2026: M5-Navigator → M15).
    // Übersetzt alte persistierte ActiveTimeframes mit M5 auf M15 und entfernt verwaiste M5-Dictionary-Keys.
    scanner.MigrateLegacyM5();
    risk.MigrateLegacyM5();

    // ============ Backtest ============ (24.04.2026: Health-Finding DI-1 — war nie gemappt)
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

    // Bot
    bot.LastMode = saved.LastMode;
    bot.LastStrategyName = saved.LastStrategyName;
    bot.PaperInitialBalance = saved.PaperInitialBalance;
    bot.ShowBtcTicker = saved.ShowBtcTicker;
    bot.EnableDesktopNotifications = saved.EnableDesktopNotifications;
    bot.SimulatedFundingRatePercent = saved.SimulatedFundingRatePercent;
    bot.WasRunningOnShutdown = saved.WasRunningOnShutdown;

    // Referenzen in BotSettings zeigen auf die DI-Singletons
    bot.Risk = risk;
    bot.Scanner = scanner;
    bot.Backtest = backtest;
}
