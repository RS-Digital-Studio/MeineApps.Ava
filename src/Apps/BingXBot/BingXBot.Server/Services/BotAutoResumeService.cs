using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
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

    /// <summary>
    /// Dispose-Guard: Der Service ist als Singleton UND via <c>AddHostedService(sp =&gt; sp.GetRequiredService&lt;…&gt;())</c>
    /// registriert — der DI-Container trackt dieselbe Instanz ueber beide Descriptors und ruft <see cref="Dispose"/>
    /// beim Shutdown zweimal auf. Ohne Guard wirft das zweite <c>_lifetimeCts.Cancel()</c> eine
    /// <see cref="ObjectDisposedException"/> (Dispose MUSS idempotent sein — .NET-Guideline).
    /// </summary>
    private bool _disposed;

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

        // Periodischer Trade-Backfill (entkoppelt von der Heartbeat-Drift-Bedingung): faengt
        // verschwundene Live-Trades ein, die bei LAUFENDEM Bot durch native SL/TP-Fills geschlossen
        // wurden und vom WebSocket-/Orphan-Pfad nicht als CompletedTrade gebucht wurden. Dedup-aware
        // (BackfillIncomeRecordsAsync gegen alle Live-Trades), daher gefahrlos wiederholbar.
        _ = Task.Run(() => PeriodicBackfillLoopAsync(_lifetimeCts.Token), CancellationToken.None);
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
                    // limit = Page-Size (GetIncomeHistoryAsync paginiert intern bis alle Records im
                    // Fenster geholt sind). 1000 = BingX-Max → weniger Paging-Calls.
                    var income = await _liveManager.RestClient.GetIncomeHistoryAsync(
                        symbol: null, incomeType: "REALIZED_PNL", startTime: since, endTime: DateTime.UtcNow, limit: 1000)
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
                        "Versuche DB-Backfill...",
                        income.Count, sumPnl);

                    // Phase 18 / H3 — Auto-DB-Backfill der verpassten Trades.
                    var summary = await BackfillIncomeRecordsAsync(income, lastHeartbeat.Value).ConfigureAwait(false);
                    if (summary.ErrorMessage != null)
                        _logger.LogWarning("Auto-Resume Backfill: {Error}", summary.ErrorMessage);
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

    /// <summary>
    /// Periodischer Trade-Backfill-Loop (alle 30 min). Holt die REALIZED_PNL-Income-Records der
    /// letzten 3 h von BingX und bucht fehlende Trades dedup-aware in die DB. Deckt den Fall ab,
    /// dass eine Live-Position bei laufendem Bot durch nativen SL/TP geschlossen wird, der zugehoerige
    /// CompletedTrade aber weder ueber den WebSocket-Fill-Handler (nur Bot-TP-Limits) noch ueber den
    /// Orphan-Reconcile gebucht wurde — genau die Ursache der verschwundenen LAB/BNB-Trades.
    /// </summary>
    private async Task PeriodicBackfillLoopAsync(CancellationToken ct)
    {
        // Erst den initialen Resume durchlaufen lassen (15 s InitialDelay + Puffer).
        try { await Task.Delay(TimeSpan.FromMinutes(2), ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    // BackfillFromBingxAsync prueft selbst auf Live-RestClient (kein Throw bei Paper/Stopped).
                    var summary = await BackfillFromBingxAsync(DateTime.UtcNow.AddHours(-3), null, ct).ConfigureAwait(false);
                    if (summary.Backfilled > 0)
                        _logger.LogInformation("Periodischer Backfill: {Count} verpasste Live-Trades nachgebucht.", summary.Backfilled);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Periodischer Trade-Backfill-Tick fehlgeschlagen.");
                }
            }
        }
        catch (OperationCanceledException) { /* Shutdown */ }
    }

    /// Snapshot-Report-Fix Befund 1 / A0.4 — Admin-Backfill der Trade-History aus BingX-Income-Records
    /// fuer einen frei waehlbaren Zeitraum. Erstmals oeffentlich exponiert, damit der Admin-Endpoint
    /// <c>/api/v1/admin/backfill-trades</c> nach Persistenz-Lecks die verlorenen Trades nachholen kann.
    /// Liefert <see cref="BackfillSummary"/> mit Counts statt void zurueck, damit Caller HTTP-Response
    /// bauen kann.
    /// </summary>
    public async Task<BackfillSummary> BackfillFromBingxAsync(DateTime fromUtc, DateTime? toUtc = null, CancellationToken ct = default)
    {
        if (_dbService == null)
            return new BackfillSummary(0, 0, 0, "DB-Service nicht verfuegbar — Backfill nicht moeglich.");

        if (_liveManager?.RestClient == null)
            return new BackfillSummary(0, 0, 0, "Live-RestClient nicht verbunden — BingX-Income nicht erreichbar. /api/v1/bot/start vorher ausfuehren.");

        var endUtc = toUtc ?? DateTime.UtcNow;
        if (endUtc <= fromUtc)
            return new BackfillSummary(0, 0, 0, "to muss spaeter sein als from.");

        try
        {
            var income = await _liveManager.RestClient.GetIncomeHistoryAsync(
                symbol: null, incomeType: "REALIZED_PNL",
                startTime: fromUtc, endTime: endUtc, limit: 1000) // Page-Size; intern paginiert
                .ConfigureAwait(false);

            if (income.Count == 0)
                return new BackfillSummary(0, 0, 0, $"Keine REALIZED_PNL-Records zwischen {fromUtc:O} und {endUtc:O} gefunden.");

            return await BackfillIncomeRecordsAsync(income, fromUtc).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin-Backfill fehlgeschlagen (from={From:O}, to={To:O})", fromUtc, endUtc);
            return new BackfillSummary(0, 0, 0, $"Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Phase 18 / H3 — Backfill verpasster Trades aus BingX-Income-Records in die lokale DB.
    /// Strategie:
    /// 1. Bestehende Backfilled-Trades (Reason="Backfilled (Pi offline)") aus DB laden zur Dedup-Erkennung.
    /// 2. Pro Income-Record: Time-Match mit Toleranz 1 s — Skip wenn schon backfilled.
    /// 3. Synthetischen CompletedTrade bauen (Best-Effort: EntryPrice/Quantity unbekannt = 0,
    ///    EntryTime = ExitTime = IncomeRecord.Time, Pnl direkt vom IncomeRecord).
    /// 4. SaveTradeAsync + RiskManager.UpdateDailyStats nur fuer Trades vom heutigen UTC-Tag.
    /// Im Fehlerfall: Logging, kein Throw — Resume laeuft weiter.
    /// </summary>
    private async Task<BackfillSummary> BackfillIncomeRecordsAsync(List<IncomeRecord> income, DateTime lastHeartbeat)
    {
        if (_dbService == null) return new BackfillSummary(0, 0, 0, "DB-Service nicht verfuegbar.");

        try
        {
            // Dedup gegen ALLE Live-Trades (nicht nur "Backfilled"-Reason): Verhindert, dass ein
            // bereits real gebuchter Trade (OnSlTpHit-/TP-Fill-Pfad) vom periodischen Backfill ein
            // zweites Mal als synthetischer Trade eingespielt wird. Key = (Symbol, ExitTime-Sekunde),
            // Match mit 1 s Toleranz. Frueher dedupte nur gegen Reason="Backfilled" → reale Live-Closes
            // wurden bei laufendem Bot doppelt verbucht bzw. der Backfill lief gar nicht (nur bei Drift).
            var existing = await _dbService.GetTradesAsync(modeFilter: TradingMode.Live, limit: 1000).ConfigureAwait(false);
            var existingTradeKeys = new HashSet<(string Symbol, long Sec)>();
            foreach (var t in existing)
            {
                var sec = t.ExitTime.Ticks / TimeSpan.TicksPerSecond;
                existingTradeKeys.Add((t.Symbol, sec));
            }

            var todayUtc = DateTime.UtcNow.Date;
            var rm = _liveManager?.Service?.RiskManager;
            int backfilled = 0, skipped = 0, todayCount = 0;

            foreach (var rec in income)
            {
                // Nur REALIZED_PNL-Records werden zu CompletedTrades — andere (FUNDING_FEE, TRADING_FEE)
                // koennen wir nicht 1:1 als Trade-Close einspielen.
                if (!string.Equals(rec.IncomeType, "REALIZED_PNL", StringComparison.OrdinalIgnoreCase))
                    continue;

                var keyTime = rec.Time.Ticks / TimeSpan.TicksPerSecond;
                if (existingTradeKeys.Contains((rec.Symbol, keyTime)) ||
                    existingTradeKeys.Contains((rec.Symbol, keyTime - 1)) ||
                    existingTradeKeys.Contains((rec.Symbol, keyTime + 1)))
                {
                    skipped++;
                    continue;
                }

                // Synthetischer CompletedTrade — Best-Effort, da Income-Record nur PnL kennt.
                var synth = new CompletedTrade(
                    Symbol: rec.Symbol,
                    Side: Side.Buy, // Side aus rec.Info ist meist nicht eindeutig — Default Buy
                    EntryPrice: 0m,
                    ExitPrice: 0m,
                    Quantity: 0m,
                    Pnl: rec.Income,
                    Fee: 0m,
                    EntryTime: rec.Time,
                    ExitTime: rec.Time,
                    Reason: "Backfilled (Pi offline)",
                    Mode: TradingMode.Live,
                    NavigatorTimeframe: TimeFrame.H4);

                try
                {
                    await _dbService.SaveTradeAsync(synth).ConfigureAwait(false);
                    backfilled++;

                    // RiskManager.UpdateDailyStats nur fuer heute (DailyPnl ist tagesbasiert).
                    if (rm != null && rec.Time.Date == todayUtc)
                    {
                        rm.UpdateDailyStats(synth);
                        todayCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Auto-Resume Backfill: SaveTrade fehlgeschlagen fuer {Symbol} @ {Time}",
                        rec.Symbol, rec.Time);
                }
            }

            _logger.LogInformation(
                "Auto-Resume Backfill abgeschlossen: {Backfilled} Trades neu in DB, {Skipped} bereits bekannt, " +
                "{TodayCount} davon in DailyPnl uebernommen.",
                backfilled, skipped, todayCount);
            return new BackfillSummary(backfilled, skipped, todayCount, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-Resume Backfill fehlgeschlagen — DB-Stats bleiben unvollstaendig, manueller Eingriff noetig.");
            return new BackfillSummary(0, 0, 0, $"Fehler: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
    }
}

/// <summary>
/// Snapshot-Report-Fix Befund 1 / A0.4 — Ergebnis eines Trade-Backfill-Laufs.
/// <see cref="ErrorMessage"/> != null => Operation gescheitert; sonst alle Counts gefuellt.
/// </summary>
public sealed record BackfillSummary(int Backfilled, int Skipped, int TodayApplied, string? ErrorMessage);
