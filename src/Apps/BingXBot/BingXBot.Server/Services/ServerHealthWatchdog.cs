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
public sealed class ServerHealthWatchdog : BackgroundService
{
    private readonly LocalBotEventStream _stream;
    private readonly LiveTradingManager _liveManager;
    private readonly BotSettings _botSettings;
    private readonly IPublicMarketDataClient _publicClient;
    private readonly ILogger<ServerHealthWatchdog> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _probeTimeout;
    private bool _lastDegraded;
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

        // v1.6.5 Phase 15 — Active-Probe via leichter Public-API-Endpoint (GetAllTickersAsync).
        // BingX hat keinen dedizierten ServerTime-Endpoint im IPublicMarketDataClient — Tickers
        // ist der naechstleichte (1 GET, ~80kB Response). Mit 5s-Timeout.
        bool probeOk;
        try
        {
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            probeCts.CancelAfter(_probeTimeout);
            await _publicClient.GetAllTickersAsync(probeCts.Token).ConfigureAwait(false);
            probeOk = true;
        }
        catch
        {
            probeOk = false;
        }

        if (probeOk)
        {
            _consecutiveProbeFailures = 0;
            // Liveness-Check (passive Variante als Backup): wenn IsConnected=false trotz
            // erfolgreichem Public-Probe, ist nur die Auth-API tot — eskaliert auch.
            var passiveDegraded = _liveManager is { IsRunning: true, IsConnected: false };
            if (!passiveDegraded && _lastDegraded)
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
