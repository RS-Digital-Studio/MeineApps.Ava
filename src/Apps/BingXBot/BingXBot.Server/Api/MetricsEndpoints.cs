using BingXBot.Contracts.Api;
using BingXBot.Server.Auth;
using BingXBot.Server.Services;
using BingXBot.Trading;

namespace BingXBot.Server.Api;

/// <summary>
/// Phase 18 / G4 — Server-interne Metrics als JSON-Snapshot.
/// Bewusst kein externer OTel/Prometheus-Exporter — dafuer waeren NuGet-Packages noetig
/// (OpenTelemetry, OpenTelemetry.Exporter.Prometheus.AspNetCore), die den Dependency-Graph
/// aufblaehen. Der Plan-Autor selbst notiert: "niedriger Hebel weil Phase 4 schon viel abdeckt".
/// Stattdessen aggregieren wir direkt aus den bestehenden Services und liefern einen kompakten
/// JSON-Snapshot, den ein externer Pruefer (curl-Skript, Grafana via JSON-API-Plugin) abrufen kann.
///
/// Erweiterung zu OTel ist als Folge-Iteration einfach moeglich:
/// - ActivitySource im Trading-Layer ergaenzen
/// - Im OTel-Setup AddOpenTelemetry().WithTracing(...).WithMetrics(...)
/// - /metrics-Endpoint liefert dann zusaetzlich Prometheus-Format
/// </summary>
public static class MetricsEndpoints
{
    public static void MapMetricsEndpoints(this WebApplication app)
    {
        app.MapGet(ApiRoutes.MetricsInternal, (
            BotDatabaseService dbService,
            FcmDeviceStore fcmStore,
            AuthTokenStore tokens,
            LiveTradingManager liveManager) =>
        {
            // Aggregierte Snapshot-Daten — schnelle In-Memory-Reads, kein DB-Query.
            var rm = liveManager.Service?.RiskManager;
            var snapshot = new
            {
                generatedAtUtc = DateTime.UtcNow,
                bot = new
                {
                    isRunning = liveManager.IsRunning,
                    isConnected = liveManager.IsConnected,
                    mode = liveManager.IsRunning ? "Live" : "Idle"
                },
                risk = rm == null ? null : new
                {
                    dailyPnl = rm.DailyPnl,
                    totalPnl = rm.TotalPnl,
                    consecutiveLosses = rm.CurrentConsecutiveLosses,
                    rollingTradesCount = rm.RecentTrades.Count,
                    rollingWinRate = rm.RollingWinRate,
                    rollingProfitFactor = rm.RollingProfitFactor,
                    rollingSharpeRatio = rm.RollingSharpeRatio,
                    newsCheckFailureCount = rm.NewsCheckFailureCount
                },
                fcm = new
                {
                    devicesRegistered = fcmStore.AllDevices.Count
                },
                auth = new
                {
                    activeTokenLifetimeSeconds = tokens.TokenLifetimeSeconds
                }
            };
            return Results.Ok(snapshot);
        });
    }
}
