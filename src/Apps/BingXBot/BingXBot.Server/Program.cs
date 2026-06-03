using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Interfaces;
using BingXBot.Engine;
using BingXBot.Exchange;
using BingXBot.Server;
using BingXBot.Server.Api;
using BingXBot.Server.Auth;
using BingXBot.Server.Hubs;
using BingXBot.Trading;
using BingXBot.Trading.Local;
using BingXBot.Trading.Telemetry;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// DI-Validation: ValidateOnBuild prueft beim Bootstrap, dass alle registrierten Services
// konstruierbar sind. Bei fehlender Interface-Registrierung (siehe v1.3.5 IRateLimiter-Bug)
// crasht der Server beim Start mit klarer Fehlermeldung, statt erst beim ersten Resolve.
// ValidateScopes: verhindert dass Singletons versehentlich Scoped-Services halten.
builder.Host.UseDefaultServiceProvider((ctx, opts) =>
{
    opts.ValidateOnBuild = true;
    opts.ValidateScopes = true;
});

// ============ DI-Container ============
var services = builder.Services;

// Phase 18 / H6 — OpenTelemetry: Tracing + Metrics aus dem BotTelemetry-ActivitySource.
// Prometheus-Exporter exposed unter /metrics fuer Grafana/Alertmanager-Integration.
services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(BotTelemetry.SourceName))
    .WithMetrics(m =>
    {
        m.AddMeter(BotTelemetry.SourceName);
        m.AddPrometheusExporter();
    });

// Server-Internals
services.AddSingleton<AuthTokenStore>();
// Phase 18 / G3 — Periodischer Cleanup expired Bearer/Refresh-Tokens (24h-Tick).
services.AddHostedService<BingXBot.Server.Services.AuthTokenCleanupService>();
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
// IRateLimiter-Binding (P3-2, v1.3.4): BingXRestClient + BingXPublicClient nehmen das Interface.
// Gleiche Instanz wie die konkrete Klasse, sonst verdoppeln wir das Request-Budget.
services.AddSingleton<BingXBot.Exchange.IRateLimiter>(sp => sp.GetRequiredService<BingXBot.Exchange.RateLimiter>());

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
services.AddSingleton<PaperTradingService>(sp =>
{
    var svc = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<PaperTradingService>(sp);
    svc.SetScannerResultsCache(sp.GetService<ScannerResultsCache>());
    return svc;
});
services.AddSingleton<LiveTradingManager>();


// v1.5.3 Phase 5 — Trade-Stats-Aggregator (Singleton, lebt mit dem Server).
services.AddSingleton<BingXBot.Trading.Stats.TradeStatsAggregator>();

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
// Snapshot-Report-Fix A0.4: Auch als Singleton registrieren, damit AdminEndpoints den Service
// per DI bekommen und ein One-Shot-Backfill triggern koennen. Der HostedService nutzt dieselbe
// Instanz (factory-Pattern), sonst wuerde Auto-Resume doppelt feuern.
services.AddSingleton<BingXBot.Server.Services.BotAutoResumeService>();
services.AddHostedService(sp => sp.GetRequiredService<BingXBot.Server.Services.BotAutoResumeService>());

// v1.6.1 Phase 11 — DB-Archivierung (monatlich 04:00 UTC nach DbBackupService).
services.AddHostedService<BingXBot.Server.Services.DbArchiveService>();

// v1.6.6 Phase 17 — Adaptive TF-Disable (Singleton + HostedService).
services.AddSingleton<BingXBot.Server.Services.AdaptiveTfDisableService>();
services.AddHostedService<BingXBot.Server.Services.AdaptiveTfDisableService>(sp =>
    sp.GetRequiredService<BingXBot.Server.Services.AdaptiveTfDisableService>());
// Static-Bridge wird beim Boot weiter unten verdrahtet (TradingServiceBase.AdaptiveTfDisableProbe).

// FCM-Push (Stub ohne Firebase-Service-Account, Push-Versand wird geloggt statt geschickt).
// Aktivierung: firebase-service-account.json in DataDirectory ablegen + FirebaseAdmin NuGet hinzufuegen.
services.AddSingleton<BingXBot.Server.Services.FcmDeviceStore>();
services.AddHostedService<BingXBot.Server.Services.FcmPushService>();
// Phase 18 / F2 — FCM-Token-Cleanup (24h-Tick, entfernt Devices > 30 Tage inaktiv).
services.AddHostedService<BingXBot.Server.Services.FcmTokenCleanupService>();

// Log-Ringpuffer: liefert den Server-seitigen /api/v1/logs-Endpoint. Clients sehen nach
// Reconnect die letzten N Eintraege statt einer leeren Log-Ansicht.
services.AddSingleton<BingXBot.Server.Services.LogBufferService>();

// Connection-Health-Watchdog: Ueberwacht alle 30s ob der Live-Exchange-Client noch verbunden
// ist und pushed einen ConnectionDegraded-Event ueber den SignalR-Hub wenn sich der Status
// aendert. Edge-Transition-basiert — kein Spam bei stabilem Zustand.
// v1.6.5 Phase 15 — Watchdog als Singleton + HostedService, damit BotAutoResumeService die
// Probe-Status-Property (IsCurrentlyDegraded) lesen kann.
services.AddSingleton<BingXBot.Server.Services.ServerHealthWatchdog>();
services.AddHostedService<BingXBot.Server.Services.ServerHealthWatchdog>(sp =>
    sp.GetRequiredService<BingXBot.Server.Services.ServerHealthWatchdog>());

// DB-Backup-Service: Taegliches Backup der bot.db (03:00 UTC, 7 Tage rotierend).
// Schuetzt vor Pi-SD-Karten-Korruption — ohne Backup = Total-Wissensverlust bei SD-Ausfall.
services.AddHostedService<BingXBot.Server.Services.DbBackupService>();

// Snapshot-Report-Fix Befund 1 / A0.2: Equity-Snapshot-Tracker fuer Remote-Mode.
// Im Remote-Mode laeuft der DashboardViewModel-Timer nicht — Pi hat keine UI. Ohne diesen Service
// wuerde die EquitySnapshots-Tabelle leer bleiben und Remote-Clients saehen keine Equity-Kurve.
// Schreibt periodisch (5 min Default) + sofort nach jedem Trade-Close, publiziert via EventBus
// an SignalR-Clients.
services.AddHostedService<BingXBot.Server.Services.EquitySnapshotService>();

// Snapshot-Report-Fix Befund 1 / A0.3: Server-seitige Log-Persistenz in die DB.
// Subscribed BotEventBus.LogEmitted und persistiert die Eintraege im 250-ms-Batch in LogEntries.
// Ohne diesen Service haelt der Server-Log nur den In-Memory-Ringpuffer (LogBufferService) und
// alles vor dem letzten Restart ist verloren. Settings-gated via BotSettings.EnableDbLogPersistence.
services.AddHostedService<BingXBot.Server.Services.DbLogPersistenceService>();

// Stale-Engine-Detector: FCM-Push wenn Bot "Running" sagt aber seit 6h kein Scanner/Trade.
// Deckt das "stiller-Bot"-Szenario das UI-Watchdog nur lokal sieht — jetzt push-aktiv.
services.AddHostedService<BingXBot.Server.Services.StaleEngineDetector>();

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

// 04.05.2026 — IMarketCapProvider in den Static-Bridge einhängen
// (CoinGecko-HTTP-Logic im Engine-Layer, kein Layer-Verletzung mehr in Core).
BingXBot.Engine.Helpers.MarketCapRefreshHelper.Configure(
    new BingXBot.Engine.Helpers.CoinGeckoMarketCapProvider());

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

    // DB-Integrity-Check direkt nach Init: Bei stiller SQLite-Korruption (z.B. Pi-SD-Ausfall, unerwarteter
    // Reboot waehrend Write) NICHT mit kaputter DB weitermachen — sonst werden Trades/Settings auf einer
    // kaputten Basis geschrieben und beim naechsten Lesen gehen Daten verloren. Bei Fehler: Exception,
    // systemd restart-Loop stoppt beim ersten Fail, Robert sieht das im journalctl.
    var integrity = await db.RunIntegrityCheckAsync();
    if (!integrity.Ok)
    {
        var errorMsg = $"DB-Integrity-Check FEHLGESCHLAGEN: {integrity.Details}. " +
                       "Server wird NICHT gestartet, um keine weiteren Writes auf kaputte DB zu machen. " +
                       "Manuell pruefen: /var/lib/bingxbot/bot.db oder aus Backup (bot-YYYY-MM-DD.db) restaurieren.";
        app.Logger.LogCritical(errorMsg);
        throw new InvalidOperationException(errorMsg);
    }
    app.Logger.LogInformation("DB-Integrity-Check: ok");

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
// Snapshot-Report-Fix Befund 1 / A0.4 — Admin-Operations (Trade-Backfill aus BingX-Income).
app.MapAdminEndpoints();
app.MapSettingsEndpoints();
// Phase 18 / G4 — interner Metrics-Snapshot-Endpoint (JSON).
app.MapMetricsEndpoints();
// Phase 18 / H6 — OpenTelemetry-Prometheus-Scraping-Endpoint /metrics (Standard-Pfad).
app.MapPrometheusScrapingEndpoint();
app.MapTradesAndLogsEndpoints();
app.MapBacktestEndpoints();
// v1.5.3 Phase 5 — Stats-Breakdown
app.MapStatsEndpoints();

// SignalR-Hub
app.MapHub<BotHub>(BingXBot.Contracts.Api.ApiRoutes.BotHubPath);

// Start: Event-Stream hochfahren (Local-Impl ist No-Op, aber Semantik halten).
// Singletons brauchen keinen Scope — Scope-Erstellung hier war redundant und irrefuehrend bei
// Test-Mocking (Scoped-Services wuerden verschieden von dem oben genutzten Scope).
{
    var stream = app.Services.GetRequiredService<IBotEventStream>();
    await stream.StartAsync();
}

// v1.5.3 Phase 5 — TradeStatsAggregator initial aus DB rebuilden, damit Stats nach
// Server-Restart erhalten bleiben (kein eigener DB-Tisch noetig — Trades-Tabelle ist die Quelle).
{
    var aggregator = app.Services.GetRequiredService<BingXBot.Trading.Stats.TradeStatsAggregator>();
    var statsDb = app.Services.GetRequiredService<BotDatabaseService>();
    try
    {
        var pastTrades = await statsDb.GetTradesAsync(modeFilter: null, limit: 10_000).ConfigureAwait(false);
        aggregator.ReplayFromTrades(pastTrades);
    }
    catch (Exception replayEx)
    {
        var statsLogger = app.Services.GetRequiredService<ILogger<BingXBot.Trading.Stats.TradeStatsAggregator>>();
        statsLogger.LogWarning(replayEx, "Trade-Stats Replay aus DB fehlgeschlagen — Aggregator startet leer");
    }
}

// v1.6.6 Phase 17 — Static-Bridge: TradingServiceBase pruet auf AdaptiveTfDisableService bei
// jedem Scan-Loop-Iter. Ohne diese Bridge wuerde der Service zwar laufen, aber die Engine
// die disableten TFs trotzdem scannen.
{
    var tfDisable = app.Services.GetRequiredService<BingXBot.Server.Services.AdaptiveTfDisableService>();
    BingXBot.Trading.TradingServiceBase.AdaptiveTfDisableProbe = tf => tfDisable.IsTfDisabled(tf);
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
    // v1.5.4 Phase 7 — Funding-Rate Soft-Bonus
    scanner.EnableFundingRateBonus = saved.Scanner.EnableFundingRateBonus;
    scanner.FundingRateBonusThresholdPercent = saved.Scanner.FundingRateBonusThresholdPercent;
    // v1.6.2 Phase 12 — Slippage-Guard
    scanner.SlippageGuardEnabled = saved.Scanner.SlippageGuardEnabled;
    scanner.MaxSlippagePercent = saved.Scanner.MaxSlippagePercent;
    if (saved.Scanner.MaxSlippagePercentByCategory is { Count: > 0 })
        scanner.MaxSlippagePercentByCategory = saved.Scanner.MaxSlippagePercentByCategory;
    // v1.6.6 Phase 17 — Adaptive TF-Disable
    scanner.EnableAdaptiveTfDisable = saved.Scanner.EnableAdaptiveTfDisable;
    scanner.AdaptiveTfMinTrades = saved.Scanner.AdaptiveTfMinTrades;
    scanner.AdaptiveTfMinWinRate = saved.Scanner.AdaptiveTfMinWinRate;
    scanner.AdaptiveTfDisableHours = saved.Scanner.AdaptiveTfDisableHours;
    // v1.7.0 Phase 16 — Cross-TF-Pyramiding (User-Ausnahme)
    risk.EnableCrossTfPyramiding = saved.Risk.EnableCrossTfPyramiding;
    risk.PyramidMaxAddOns = saved.Risk.PyramidMaxAddOns;
    risk.PyramidScalePercent = saved.Risk.PyramidScalePercent;
    // Stale-Pending-Limit-Order-Expiry (Default 6h)
    risk.PendingLimitOrderMaxAgeHours = saved.Risk.PendingLimitOrderMaxAgeHours;
    // Runner-TP (opt-in)
    risk.EnableRunner = saved.Risk.EnableRunner;
    risk.RunnerPercent = saved.Risk.RunnerPercent;
    risk.RunnerTrailingAtrMultiplier = saved.Risk.RunnerTrailingAtrMultiplier;
    risk.BreakevenTriggerRMultiple = saved.Risk.BreakevenTriggerRMultiple;
    risk.MaxRiskPercentPerTrade = saved.Risk.MaxRiskPercentPerTrade;
    risk.MaxDailyLossPercent = saved.Risk.MaxDailyLossPercent;
    risk.MaxDailyRiskPercent = saved.Risk.MaxDailyRiskPercent;          // ← war 24.04.2026 ungemappt → User-Wert ging verloren
    // Konfigurierbare Risk-Schwellen (vorher hardcoded)
    risk.MaxTotalMarginPercent = saved.Risk.MaxTotalMarginPercent;
    risk.LossStreakHalveAtCount = saved.Risk.LossStreakHalveAtCount;
    risk.LossStreakPauseAtCount = saved.Risk.LossStreakPauseAtCount;
    risk.MinPositionSizeRetentionPercent = saved.Risk.MinPositionSizeRetentionPercent;
    risk.EnableLossStreakDampening = saved.Risk.EnableLossStreakDampening;
    // Adaptive-Sizing-/Schutz-Features (waren ungemappt → fielen bei JEDEM Server-Restart auf Default
    // zurueck: Korrelations-Filter auf 0=aus, Vol-Targeting/Equity-Scaling aus). User-Werte gingen verloren.
    risk.MaxCorrelatedExposurePercent = saved.Risk.MaxCorrelatedExposurePercent;
    risk.EnableVolatilityTargeting = saved.Risk.EnableVolatilityTargeting;
    risk.VolatilityTargetPercent = saved.Risk.VolatilityTargetPercent;
    risk.VolatilityScaleCap = saved.Risk.VolatilityScaleCap;
    risk.EnableEquityCurveScaling = saved.Risk.EnableEquityCurveScaling;
    risk.EquityCurveScalingThresholdPercent = saved.Risk.EquityCurveScalingThresholdPercent;
    // Dictionaries: nur uebernehmen wenn nicht-leer (Schutz vor Default-Reset bei alten Migration-Snapshots).
    if (saved.Risk.CategorySettings is { Count: > 0 })
        risk.CategorySettings = saved.Risk.CategorySettings;
    if (saved.Risk.PipScalingByTf is { Count: > 0 })
        risk.PipScalingByTf = saved.Risk.PipScalingByTf;
    if (saved.Risk.SlBufferPipsByTf is { Count: > 0 })
        risk.SlBufferPipsByTf = saved.Risk.SlBufferPipsByTf;

    // ============ Scanner ============
    // Kernparameter
#pragma warning disable CS0618 // Legacy-Felder weiterhin persistieren bis v1.4-Migration abgeschlossen
    scanner.MinVolume24h = saved.Scanner.MinVolume24h;
    scanner.MinPriceChange = saved.Scanner.MinPriceChange;
    scanner.ScanTimeFrame = saved.Scanner.ScanTimeFrame;
    scanner.MaxResults = saved.Scanner.MaxResults;
#pragma warning restore CS0618
    scanner.Mode = saved.Scanner.Mode;
    scanner.OnlyTopByVolume = saved.Scanner.OnlyTopByVolume;
    scanner.TopCoinsCount = saved.Scanner.TopCoinsCount;
    scanner.ScanIntervalSeconds = saved.Scanner.ScanIntervalSeconds;
    scanner.Whitelist = saved.Scanner.Whitelist;
    scanner.Blacklist = saved.Scanner.Blacklist;
    scanner.EnableTradFi = saved.Scanner.EnableTradFi;
    if (saved.Scanner.EnabledCategories is { Count: > 0 })
        scanner.EnabledCategories = saved.Scanner.EnabledCategories;
#pragma warning disable CS0618
    scanner.MinVolume24hTradFi = saved.Scanner.MinVolume24hTradFi;
    scanner.MinPriceChangeTradFi = saved.Scanner.MinPriceChangeTradFi;
#pragma warning restore CS0618

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
    // v1.5.5 Phase 9 — Trade-Push Toggle
    bot.EnableTradePushNotifications = saved.EnableTradePushNotifications;

    // Referenzen in BotSettings zeigen auf die DI-Singletons
    bot.Risk = risk;
    bot.Scanner = scanner;
    bot.Backtest = backtest;
}
