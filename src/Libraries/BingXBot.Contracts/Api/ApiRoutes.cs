namespace BingXBot.Contracts.Api;

/// <summary>
/// Zentraler Katalog aller REST-Pfade zwischen Client und Pi-Server.
/// Aenderungen hier muessen in Server-Endpoints UND Client-Impls synchron nachgezogen werden.
/// </summary>
public static class ApiRoutes
{
    public const string ApiPrefix = "/api/v1";

    // Pairing & Auth (6-stelliger Code, kein QR)
    public const string PairInit = ApiPrefix + "/pair/init";
    public const string PairComplete = ApiPrefix + "/pair/complete";
    public const string PairCancel = ApiPrefix + "/pair/cancel";
    public const string AuthRefresh = ApiPrefix + "/auth/refresh";
    /// <summary>Phase 18 / G3 — Revoke des aktuell verwendeten Tokens (Single-Device-Logout).</summary>
    public const string AuthLogout = ApiPrefix + "/auth/logout";
    /// <summary>Phase 18 / G3 — Revoke aller Tokens ausser dem aktuell verwendeten (Logout-überall).</summary>
    public const string AuthLogoutOthers = ApiPrefix + "/auth/logout-others";
    /// <summary>Phase 18 / G4 — Internal Metrics-Snapshot (JSON, keine externe Exporter-Dependency).</summary>
    public const string MetricsInternal = ApiPrefix + "/metrics/internal";

    // Health (auth-frei, fuer Liveness-Check)
    public const string Health = ApiPrefix + "/health";

    // Status & Account
    public const string Status = ApiPrefix + "/status";
    public const string Account = ApiPrefix + "/account";
    public const string Positions = ApiPrefix + "/positions";
    public const string OpenOrders = ApiPrefix + "/open-orders";
    public const string Equity = ApiPrefix + "/equity";

    // Bot-Control
    public const string BotStart = ApiPrefix + "/bot/start";
    public const string BotStop = ApiPrefix + "/bot/stop";
    public const string BotEmergencyStop = ApiPrefix + "/bot/emergency-stop";
    public const string PositionClose = ApiPrefix + "/position/{symbol}/close";

    // Settings
    public const string Settings = ApiPrefix + "/settings";
    public const string SettingsRisk = ApiPrefix + "/settings/risk";
    public const string SettingsScanner = ApiPrefix + "/settings/scanner";
    public const string SettingsBot = ApiPrefix + "/settings/bot";
    public const string SettingsBacktest = ApiPrefix + "/settings/backtest";

    // Trades & Logs
    public const string Trades = ApiPrefix + "/trades";
    public const string TradesSummary = ApiPrefix + "/trades/summary";
    public const string Logs = ApiPrefix + "/logs";
    public const string ScannerResults = ApiPrefix + "/scanner/results";

    // Backtest
    public const string BacktestStart = ApiPrefix + "/backtest/start";
    public const string BacktestStatus = ApiPrefix + "/backtest/{jobId}";
    public const string BacktestResult = ApiPrefix + "/backtest/{jobId}/result";
    public const string BacktestCancel = ApiPrefix + "/backtest/{jobId}/cancel";

    // Credentials (BingX API-Key/Secret auf dem Pi)
    public const string CredentialsStatus = ApiPrefix + "/credentials/status";
    public const string Credentials = ApiPrefix + "/credentials";

    // Android-FCM Device-Registrierung (optional, Phase 5.7)
    public const string DevicesFcm = ApiPrefix + "/devices/fcm";

    // v1.5.3 Phase 5 — Per-TF + Per-Category Trade-Stats
    public const string StatsBreakdown = ApiPrefix + "/stats/breakdown";

    // v1.6.3 Phase 14 — Settings-Change-Audit-Trail
    public const string SettingsHistory = ApiPrefix + "/settings/history";

    // v1.6.4 Phase 13 — Trade-Replay
    public const string BacktestReplayTrade = ApiPrefix + "/backtest/replay-trade/{tradeId}";

    // Snapshot-Report-Fix Befund 1 / A0.4 — Admin-One-Shot-Backfill der Trade-History aus
    // BingX-Income-Records. Nur fuer authentifizierte Clients; ohne diesen Endpoint mussten
    // verlorene Trades manuell ueber den Auto-Resume-Drift-Pfad eingespielt werden.
    public const string AdminBackfillTrades = ApiPrefix + "/admin/backfill-trades";

    // SignalR-Hub
    public const string BotHubPath = "/hubs/bot";
}
