using BingXBot.Contracts.Dto;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Trading;
using BingXBot.Trading.Local;

namespace BingXBot.Server.Services;

/// <summary>
/// HostedService: Prueft periodisch (alle 30 s) ob der BingX-Live-Exchange-Client verbunden ist
/// und pushed bei Status-Wechsel einen <see cref="ConnectionDegradedDto"/>-Event ueber den
/// <see cref="LocalBotEventStream"/> an alle SignalR-Clients.
///
/// Triggert NUR im Live-Mode. Im Paper-Mode ist die BingX-Verbindung irrelevant (SimulatedExchange
/// hat keine echte Netzwerk-Verbindung). "Degraded" = "Bot laeuft weiter, aber kann aktuell keine
/// Orders platzieren" — typischer Fall: BingX-WS-Disconnect, Pi verliert kurz Internet.
///
/// Edge-Transition-Logik: Event wird NUR bei Aenderung gefeuert, nicht periodisch — spart UI-
/// Spam und Hub-Bandbreite. Initial-Zustand gilt als "OK" bis erster Disconnect beobachtet wird.
/// </summary>
public sealed partial class ServerHealthWatchdog : BackgroundService
{
    private readonly LocalBotEventStream _stream;
    private readonly LiveTradingManager _liveManager;
    private readonly BotSettings _botSettings;
    private readonly IPublicMarketDataClient _publicClient;
    private readonly ILogger<ServerHealthWatchdog> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _probeTimeout;
    private readonly TimeSpan _clockDriftWarn;
    private readonly TimeSpan _clockDriftDegrade;
    private bool _lastDegraded;
    private bool _clockDriftDegraded;
    /// <summary>v1.6.5 Phase 15 — Probe-Fail-Counter (2× in Folge → Degraded).</summary>
    private int _consecutiveProbeFailures;
    /// <summary>v1.6.5 Phase 15 — True wenn der letzte Probe-Failure-Stand gerade durch eine Recovery zurueckgesetzt wurde.</summary>
    public bool IsCurrentlyDegraded => _lastDegraded;

    public ServerHealthWatchdog(
        LocalBotEventStream stream,
        LiveTradingManager liveManager,
        BotSettings botSettings,
        IPublicMarketDataClient publicClient,
        IConfiguration config,
        ILogger<ServerHealthWatchdog> logger)
    {
        _stream = stream;
        _liveManager = liveManager;
        _botSettings = botSettings;
        _publicClient = publicClient;
        _logger = logger;
        var secs = Math.Max(5, config.GetValue<int>("Server:HealthWatchdogIntervalSeconds", 30));
        _interval = TimeSpan.FromSeconds(secs);
        var probeMs = Math.Max(1000, config.GetValue<int>("Server:HealthProbeTimeoutMs", 5000));
        _probeTimeout = TimeSpan.FromMilliseconds(probeMs);
        // Phase 18 / A3 — Clock-Drift-Schwellen. Default: Warning ab 2 s, Degraded ab 4 s.
        // BingX recvWindow ist 5 s — bei 4 s sind wir noch unter dem Reject-Limit, aber bereits Disaster-Mode-nah.
        var driftWarnMs = Math.Max(500, config.GetValue<int>("Server:ClockDriftWarnMs", 2000));
        var driftDegradeMs = Math.Max(driftWarnMs + 500, config.GetValue<int>("Server:ClockDriftDegradeMs", 4000));
        _clockDriftWarn = TimeSpan.FromMilliseconds(driftWarnMs);
        _clockDriftDegrade = TimeSpan.FromMilliseconds(driftDegradeMs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ServerHealthWatchdog gestartet (Intervall {Seconds}s)", _interval.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(_interval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            try { await CheckAndPushAsync(stoppingToken).ConfigureAwait(false); }
            catch (Exception ex)
            {
                // Watchdog darf NIE den Prozess killen — wir wollen wissen dass er lebt.
                _logger.LogWarning(ex, "ServerHealthWatchdog-Iteration fehlgeschlagen");
            }
        }
    }

    private async Task CheckAndPushAsync(CancellationToken ct)
    {
        // Nur im Live-Mode relevant — Paper-Engine hat keine echte BingX-Verbindung.
        if (_botSettings.LastMode != TradingMode.Live) return;
        if (_liveManager is not { IsRunning: true })
        {
            // Bot nicht laufend → Probe pausiert (kein Spam, kein Quote-Verbrauch im Idle).
            // State auch zuruecksetzen, sonst bleibt _lastDegraded "true" haengen wenn der User
            // den Bot stoppt waehrend Degraded-State.
            if (_lastDegraded) ResetDegraded("Bot nicht laufend, Probe pausiert");
            return;
        }

        // Phase 18 / A3 + C2 — Active-Probe via /server/time (~50 Bytes statt ~80 kB Tickers).
        // Liefert gleichzeitig BingX-Server-UTC fuer Clock-Drift-Detection. Mit Probe-Timeout.
        DateTime? serverTime = null;
        bool probeOk;
        try
        {
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            probeCts.CancelAfter(_probeTimeout);
            serverTime = await _publicClient.GetServerTimeAsync(probeCts.Token).ConfigureAwait(false);
            probeOk = true;
        }
        catch
        {
            probeOk = false;
        }

        if (probeOk)
        {
            _consecutiveProbeFailures = 0;

            // Phase 18 / A3 — Clock-Drift-Detection (BingX recvWindow 5 s).
            // Bei Drift > 4 s sind ALLE signed Orders dem Risiko ausgesetzt mit "Timestamp out of recvWindow"
            // abgelehnt zu werden. Stiller Disaster-Mode — Bot scannt, evaluiert, aber keine Order kommt durch.
            if (serverTime.HasValue)
            {
                var drift = (DateTime.UtcNow - serverTime.Value).Duration();
                if (drift >= _clockDriftDegrade)
                {
                    if (!_clockDriftDegraded)
                    {
                        _clockDriftDegraded = true;
                        FireDegraded($"Clock-Drift {drift.TotalMilliseconds:F0} ms ≥ {_clockDriftDegrade.TotalMilliseconds:F0} ms (BingX recvWindow 5 s) — sudo systemctl restart chronyd auf dem Pi.");
                    }
                    else
                    {
                        _logger.LogWarning("Clock-Drift {DriftMs} ms bleibt > {LimitMs} ms — Trading-Orders koennen abgelehnt werden",
                            drift.TotalMilliseconds, _clockDriftDegrade.TotalMilliseconds);
                    }
                    return; // Drift-Degraded ueberlagert Liveness-OK
                }

                if (drift >= _clockDriftWarn)
                {
                    _logger.LogWarning("Clock-Drift {DriftMs} ms zwischen Pi und BingX (Warning ab {WarnMs} ms, Degrade ab {DegradeMs} ms)",
                        drift.TotalMilliseconds, _clockDriftWarn.TotalMilliseconds, _clockDriftDegrade.TotalMilliseconds);
                }
                else if (_clockDriftDegraded)
                {
                    // Recovery — Drift wieder unter Warn-Schwelle.
                    _clockDriftDegraded = false;
                    if (_lastDegraded) ResetDegraded($"Clock-Drift {drift.TotalMilliseconds:F0} ms wieder im Toleranzbereich.");
                }
            }

            // Liveness-Check (passive Variante als Backup): wenn IsConnected=false trotz
            // erfolgreichem Public-Probe, ist nur die Auth-API tot — eskaliert auch.
            var passiveDegraded = _liveManager is { IsRunning: true, IsConnected: false };
            if (!passiveDegraded && _lastDegraded && !_clockDriftDegraded)
                ResetDegraded("BingX-Exchange wieder erreichbar (Probe + Liveness OK).");
            else if (passiveDegraded && !_lastDegraded)
                FireDegraded("BingX REST/WS unverbunden trotz erfolgreichem Public-Probe (Auth-Token?).");
            return;
        }

        // Probe failed.
        _consecutiveProbeFailures++;
        if (_consecutiveProbeFailures >= 2 && !_lastDegraded)
            FireDegraded($"BingX-Public-API nicht erreichbar nach {_consecutiveProbeFailures} Versuchen.");
    }

    private void FireDegraded(string reason)
    {
        _lastDegraded = true;
        _logger.LogWarning("ConnectionDegraded => true: {Reason}", reason);
        _stream.PublishConnectionDegraded(new ConnectionDegradedDto(true, reason, DateTime.UtcNow));
    }

    private void ResetDegraded(string reason)
    {
        _lastDegraded = false;
        _consecutiveProbeFailures = 0;
        _logger.LogInformation("ConnectionDegraded => false: {Reason}", reason);
        _stream.PublishConnectionDegraded(new ConnectionDegradedDto(false, reason, DateTime.UtcNow));
    }

    /// <summary>
    /// v1.6.5 Phase 15 — Pure-Function-Helper fuer Test-Coverage. Kapselt die Edge-Transition-
    /// Logik ohne BackgroundService-/IConfig-/Logger-Dependencies.
    /// </summary>
    public static ProbeOutcome EvaluateProbe(
        bool isBotRunning, bool probeOk, bool passiveLiveDegraded,
        bool currentlyDegraded, int consecutiveFailuresBefore, int failureThreshold = 2)
    {
        if (!isBotRunning)
            return new ProbeOutcome(NewDegraded: false, NewConsecutiveFailures: 0, FireEvent: currentlyDegraded);

        if (probeOk)
        {
            // Public-API erreichbar. Aber wenn passive (LiveTradingManager.IsConnected) noch unverbunden
            // ist, ist die Auth-API tot — eskaliert auch.
            var newDegraded = passiveLiveDegraded;
            var fireEvent = newDegraded != currentlyDegraded;
            return new ProbeOutcome(NewDegraded: newDegraded, NewConsecutiveFailures: 0, FireEvent: fireEvent);
        }

        // Probe failed.
        var newCount = consecutiveFailuresBefore + 1;
        var shouldDegrade = newCount >= failureThreshold && !currentlyDegraded;
        return new ProbeOutcome(
            NewDegraded: shouldDegrade ? true : currentlyDegraded,
            NewConsecutiveFailures: newCount,
            FireEvent: shouldDegrade);
    }
}

/// <summary>v1.6.5 Phase 15 — Pure-Function-Output fuer Probe-Auswertung.</summary>
public sealed record ProbeOutcome(bool NewDegraded, int NewConsecutiveFailures, bool FireEvent);

/// <summary>
/// Phase 18 / A3 — Pure-Function-Output fuer Clock-Drift-Auswertung.
/// </summary>
/// <param name="NewDriftDegraded">Soll der Drift-Degraded-State nach diesem Probe gesetzt sein?</param>
/// <param name="ShouldWarn">Drift im Warning-Bereich (zwischen Warn und Degrade) — Log-Warning.</param>
/// <param name="ShouldFireDegradedEvent">Edge-Transition false→true — neues Event publishen.</param>
/// <param name="ShouldFireRecoveryEvent">Edge-Transition true→false — Reset-Event publishen.</param>
public sealed record ClockDriftOutcome(
    bool NewDriftDegraded,
    bool ShouldWarn,
    bool ShouldFireDegradedEvent,
    bool ShouldFireRecoveryEvent);

public sealed partial class ServerHealthWatchdog
{
    /// <summary>
    /// Phase 18 / A3 — Pure-Function fuer Clock-Drift-Bewertung. Kapselt die Edge-Transition-Logik
    /// fuer Test-Coverage analog zu <see cref="EvaluateProbe"/>.
    /// </summary>
    public static ClockDriftOutcome EvaluateClockDrift(
        TimeSpan absoluteDrift,
        TimeSpan warnThreshold,
        TimeSpan degradeThreshold,
        bool currentlyDriftDegraded)
    {
        if (absoluteDrift >= degradeThreshold)
        {
            return new ClockDriftOutcome(
                NewDriftDegraded: true,
                ShouldWarn: false,
                ShouldFireDegradedEvent: !currentlyDriftDegraded,
                ShouldFireRecoveryEvent: false);
        }

        if (absoluteDrift >= warnThreshold)
        {
            // Warn-Bereich: nicht degraded (noch nicht), aber Log-Warning. Wenn vorher degraded → recover.
            return new ClockDriftOutcome(
                NewDriftDegraded: false,
                ShouldWarn: true,
                ShouldFireDegradedEvent: false,
                ShouldFireRecoveryEvent: currentlyDriftDegraded);
        }

        // Drift unter Warn-Schwelle.
        return new ClockDriftOutcome(
            NewDriftDegraded: false,
            ShouldWarn: false,
            ShouldFireDegradedEvent: false,
            ShouldFireRecoveryEvent: currentlyDriftDegraded);
    }
}
