using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Trading;

namespace BingXBot.Server.Services;

/// <summary>
/// HostedService (24.04.2026): Reaktiviert die Trading-Engine nach einem Server-Restart automatisch,
/// wenn vor dem letzten Shutdown der Bot lief (<see cref="BotSettings.WasRunningOnShutdown"/> = true).
///
/// Hintergrund: Der Pi-Server wurde durch update.sh / systemctl restart / Stromausfall neu gestartet.
/// Vorher lief die Engine. Nach dem Restart blieb die UI im "sucheB"-Cache und niemand merkte,
/// dass der Bot tot war — 3 Tage idle (siehe Diagnose 2026-04-24). Auto-Resume verhindert das.
///
/// Sicherheits-Bedingungen:
/// - <see cref="InitialDelay"/> 15s: Hosting-Setup, DB-Init, NTP-Sync, Tailscale-Connect, BingX-DNS
///   sollen sich nach Pi-Boot setzen koennen. 5s war zu knapp (NTP-Drift-Korrektur dauert 3-10s,
///   Tailscale-Verbindung 5-15s). Bei Server-Restart auf laufendem System wirken die 15s als
///   harmloses Wartefenster — Robert kann waehrenddessen noch manuell intervenieren.
/// - Try-Catch komplett: Resume darf den Server NICHT crashen.
/// - User-bewusste Stops (Stop/EmergencyStop in <see cref="Trading.Local.LocalBotControlService"/>)
///   setzen das Flag auf false → KEIN Re-Start. Auto-Resume greift NUR bei Crash/Reboot.
/// </summary>
public sealed class BotAutoResumeService : IHostedService, IDisposable
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(15);

    private readonly IBotControlService _botControl;
    private readonly BotSettings _botSettings;
    private readonly ScannerSettings _scannerSettings;
    private readonly ILogger<BotAutoResumeService> _logger;
    /// <summary>
    /// v1.6.5 Phase 15 — Optional. Wenn Watchdog Degraded meldet, blockiert Auto-Resume bis
    /// Probe wieder gruen ist (verhindert ConnectionLoss-Endless-Loop).
    /// </summary>
    private readonly ServerHealthWatchdog? _healthWatchdog;
    /// <summary>Phase 18 / G1 — Optional fuer Trade-Replay: liest LastHeartbeat aus DB.</summary>
    private readonly BotDatabaseService? _dbService;
    /// <summary>Phase 18 / G1 — Optional fuer Trade-Replay: holt Income-Records aus BingX (wenn Live-Mode).</summary>
    private readonly LiveTradingManager? _liveManager;
    /// <summary>Phase 18 / G1 — Drift-Schwelle, ab der ein Trade-Replay-Hint geloggt wird.</summary>
    private static readonly TimeSpan ReplayDriftThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Eigener Lebenszyklus-CTS (24.04.2026 Debugger-Fix Bug #5):
    /// NICHT den HostedService-CT verwenden — der wuerde Engine-StartAsync abbrechen, sobald
    /// der Server herunterfaehrt. Engine-Start ist nicht trivial atomar (BingX-Connect, Reconcile,
    /// PendingLimitOrder-Restore), Cancellation mitten drin koennte WasRunningOnShutdown in einem
    /// undefinierten Zustand belassen. Wir nutzen unseren eigenen CTS, den nur Dispose() canceled.
    /// </summary>
    private readonly CancellationTokenSource _lifetimeCts = new();

    public BotAutoResumeService(
        IBotControlService botControl,
        BotSettings botSettings,
        ScannerSettings scannerSettings,
        ILogger<BotAutoResumeService> logger,
        ServerHealthWatchdog? healthWatchdog = null,
        BotDatabaseService? dbService = null,
        LiveTradingManager? liveManager = null)
    {
        _botControl = botControl;
        _botSettings = botSettings;
        _scannerSettings = scannerSettings;
        _logger = logger;
        _healthWatchdog = healthWatchdog;
        _dbService = dbService;
        _liveManager = liveManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Microsoft-Best-Practice: HostedService.StartAsync NICHT blockieren — sonst haengt
        // die ganze Hosting-Pipeline. ConnectAsync (BingX) kann mehrere Sekunden dauern.
        // BEWUSST eigenen CTS uebergeben, NICHT cancellationToken (Bug #5).
        _ = Task.Run(() => ResumeAsync(_lifetimeCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Bewusst NICHT _lifetimeCts.Cancel(): wenn Engine-Start mitten in PendingLimitOrder-Reconcile
        // ist, wuerde Abbruch zu undefinierten Zustaenden fuehren. ResumeAsync ist eh entweder fertig
        // (Engine laeuft) oder blockiert auf einer Microsoft-managed Operation die selbst
        // CT-Aware ist. Beim Prozess-Tod stirbt sowieso alles.
        return Task.CompletedTask;
    }

    private async Task ResumeAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(InitialDelay, ct).ConfigureAwait(false);

            if (!_botSettings.WasRunningOnShutdown)
            {
                _logger.LogInformation(
                    "Auto-Resume: WasRunningOnShutdown=false. Bot bleibt gestoppt — User muss manuell Start druecken.");
                return;
            }

            // v1.6.5 Phase 15 — Wenn der Health-Watchdog gerade Degraded meldet (BingX nicht erreichbar),
            // pause Resume bis er wieder gruen ist. Verhindert ConnectionLoss-Endless-Loop, wenn der Pi
            // direkt nach dem Boot noch keine BingX-Verbindung hat.
            if (_healthWatchdog?.IsCurrentlyDegraded == true)
            {
                _logger.LogWarning(
                    "Auto-Resume blockiert: ServerHealthWatchdog meldet Degraded. Warte auf BingX-Recovery.");
                // Nicht hart abbrechen — bei naechstem Service-Restart wird ohnehin neu probiert.
                // In dieser Iteration einfach return.
                return;
            }

            // Phase 18 / G1 — Trade-Replay-Hint VOR Engine-Start. Wenn der Pi-Heartbeat groesser
            // als ReplayDriftThreshold ist, koennten Trades waehrend der Offline-Zeit gefuellt
            // worden sein, die der Bot nicht in seiner DB hat. Wir loggen das transparent — eine
            // automatische DB-Synthese (Trade-Pairing aus Income-Records) ist als naechster
            // Schritt vermerkt (separate UI-Action).
            await TryLogReplayHintAsync(ct).ConfigureAwait(false);

            var tfs = _scannerSettings.ActiveTimeframes?.ToList() ?? new List<TimeFrame>();
            _logger.LogInformation(
                "Auto-Resume: Engine wird im {Mode}-Modus mit Timeframes [{Tfs}] reaktiviert (vor Shutdown lief der Bot).",
                _botSettings.LastMode, string.Join(",", tfs));

            var request = new BotStartRequest(_botSettings.LastMode, InitialBalance: null, ActiveTimeframes: tfs);
            // Bewusst CancellationToken.None weiterreichen — Engine-Start ist atomar zu betrachten.
            var status = await _botControl.StartAsync(request, CancellationToken.None).ConfigureAwait(false);

            if (status.State == BotState.Running)
            {
                _logger.LogInformation("Auto-Resume erfolgreich. State={State}, Mode={Mode}.",
                    status.State, status.Mode);
            }
            else
            {
                _logger.LogWarning("Auto-Resume nicht im Running-State. State={State}, LastError={Error}.",
                    status.State, status.LastError ?? "(kein Fehler)");
            }
        }
        catch (OperationCanceledException)
        {
            // Lifecycle-CTS canceled — Server wird heruntergefahren waehrend wir noch im 15s-Delay warten.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Auto-Resume fehlgeschlagen — Bot bleibt gestoppt. User muss manuell Start druecken.");
        }
    }

    /// <summary>
    /// Phase 18 / G1 — Liest LastHeartbeatUtc aus der DB und vergleicht mit jetzt. Bei Drift
    /// > <see cref="ReplayDriftThreshold"/> wird (im Live-Mode) die BingX-Income-History fuer
    /// die Offline-Zeit abgerufen und als WARNING geloggt. Damit erkennt der User auf einen Blick:
    /// "Pi war 4 h offline, in der Zeit gab es 3 Income-Records mit Σ-PnL +12 USDT — DB-Stats
    /// koennten unvollstaendig sein". Robust: Fehler werfen den Resume nicht ab.
    /// </summary>
    private async Task TryLogReplayHintAsync(CancellationToken ct)
    {
        if (_dbService == null) return;
        try
        {
            var lastHeartbeat = await _dbService.LoadLastHeartbeatAsync().ConfigureAwait(false);
            if (lastHeartbeat == null)
            {
                _logger.LogInformation("Auto-Resume: Kein Heartbeat-Wert in DB (Frischer Pi oder erste Phase-18-Iteration) — kein Replay-Check.");
                return;
            }

            var drift = DateTime.UtcNow - lastHeartbeat.Value;
            if (drift < ReplayDriftThreshold)
            {
                _logger.LogInformation("Auto-Resume: Heartbeat-Drift {Drift} unter Schwelle ({Threshold}) — kein Replay noetig.",
                    drift, ReplayDriftThreshold);
                return;
            }

            _logger.LogWarning(
                "Auto-Resume: Heartbeat-Drift {Drift} (LastHeartbeat={LastHeartbeat:O}) — Pi war moeglicherweise offline.",
                drift, lastHeartbeat.Value);

            // Live-Mode: BingX-Income-History auswerten.
            if (_botSettings.LastMode == TradingMode.Live && _liveManager?.RestClient != null)
            {
                try
                {
                    var since = lastHeartbeat.Value.AddMinutes(-1); // 1 min Sicherheits-Padding
                    var income = await _liveManager.RestClient.GetIncomeHistoryAsync(
                        symbol: null, incomeType: "REALIZED_PNL", startTime: since, endTime: DateTime.UtcNow, limit: 100)
                        .ConfigureAwait(false);

                    if (income.Count == 0)
                    {
                        _logger.LogInformation("Auto-Resume Replay-Check: Keine REALIZED_PNL-Records in der Offline-Zeit gefunden.");
                        return;
                    }

                    decimal sumPnl = 0;
                    foreach (var rec in income) sumPnl += rec.Income;

                    _logger.LogWarning(
                        "Auto-Resume Replay-Check: {Count} REALIZED_PNL-Records waehrend Offline-Zeit, Summe-PnL = {Sum:F4} USDT. " +
                        "DB-Statistiken (DailyPnl, RollingTrades) koennten unvollstaendig sein. " +
                        "Folge-Iteration: automatisches DB-Backfill via Trade-Pairing.",
                        income.Count, sumPnl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Auto-Resume Replay-Check: Income-History-Abruf fehlgeschlagen.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-Resume Replay-Hint fehlgeschlagen — Resume laeuft trotzdem weiter.");
        }
    }

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
    }
}
