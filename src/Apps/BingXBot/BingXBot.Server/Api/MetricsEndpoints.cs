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
            LiveTradingManager liveManager,
            BingXBot.Trading.CrossSectional.CrossSectionalManager xsecManager) =>
        {
            // Aggregierte Snapshot-Daten — schnelle In-Memory-Reads, kein DB-Query.
            // Cross-Sectional laeuft NICHT ueber den LiveTradingManager → frueher meldete dieser
            // Endpoint im Xsec-Modus konstant isRunning=false/mode=Idle/risk=null (live 12.06.2026
            // diagnostiziert: externes Monitoring sah den Bot faelschlich als "down"). Beide Engines
            // pruefen.
            var rm = liveManager.Service?.RiskManager;
            var xsecRunning = xsecManager.IsRunning;
            var mode = liveManager.IsRunning ? "Live (Scalper)"
                : xsecRunning ? "Live (Cross-Sectional)"
                : "Idle";
            var snapshot = new
            {
                generatedAtUtc = DateTime.UtcNow,
                bot = new
                {
                    isRunning = liveManager.IsRunning || xsecRunning,
                    isConnected = liveManager.IsConnected || xsecManager.IsConnected,
                    mode
                },
                // Cross-Sectional hat keinen RiskManager (market-neutraler Korb statt per-Trade-Risk) —
                // stattdessen Korb-Health + Tick-Liveness melden.
                xsec = xsecRunning ? new
                {
                    basketSize = xsecManager.CurrentBasket?.Count ?? 0,
                    lastTickUtc = xsecManager.LastTickUtc
                } : null,
                risk = rm == null ? null : new
                {
                    dailyPnl = rm.DailyPnl,
                    totalPnl = rm.TotalPnl,
                    consecutiveLosses = rm.CurrentConsecutiveLosses,
                    rollingTradesCount = rm.RecentTrades.Count,
                    rollingWinRate = rm.RollingWinRate,
                    rollingProfitFactor = rm.RollingProfitFactor,
                    rollingSharpeRatio = rm.RollingSharpeRatio
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
