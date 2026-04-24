using BingXBot.Contracts.Dto;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
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
    private readonly ILogger<ServerHealthWatchdog> _logger;
    private readonly TimeSpan _interval;
    private bool _lastDegraded;

    public ServerHealthWatchdog(
        LocalBotEventStream stream,
        LiveTradingManager liveManager,
        BotSettings botSettings,
        IConfiguration config,
        ILogger<ServerHealthWatchdog> logger)
    {
        _stream = stream;
        _liveManager = liveManager;
        _botSettings = botSettings;
        _logger = logger;
        var secs = Math.Max(5, config.GetValue<int>("Server:HealthWatchdogIntervalSeconds", 30));
        _interval = TimeSpan.FromSeconds(secs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ServerHealthWatchdog gestartet (Intervall {Seconds}s)", _interval.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(_interval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            try { CheckAndPush(); }
            catch (Exception ex)
            {
                // Watchdog darf NIE den Prozess killen — wir wollen wissen dass er lebt.
                _logger.LogWarning(ex, "ServerHealthWatchdog-Iteration fehlgeschlagen");
            }
        }
    }

    private void CheckAndPush()
    {
        // Nur im Live-Mode relevant — Paper-Engine hat keine echte BingX-Verbindung.
        if (_botSettings.LastMode != TradingMode.Live) return;

        // "Degraded" = User hat Live-Mode gewaehlt, LiveTradingManager ist aber NICHT connected.
        // Bei Stopped-State des Bots auch keine Warnung, der User hat ihn bewusst abgeschaltet.
        var degraded = _liveManager is { IsRunning: true, IsConnected: false };

        if (degraded == _lastDegraded) return; // Edge-Transition: nichts geaendert.
        _lastDegraded = degraded;

        var reason = degraded
            ? "BingX-Exchange-Verbindung unterbrochen (WebSocket/REST)."
            : "BingX-Exchange-Verbindung wiederhergestellt.";
        _logger.LogWarning("ConnectionDegraded => {Degraded}: {Reason}", degraded, reason);
        _stream.PublishConnectionDegraded(new ConnectionDegradedDto(degraded, reason, DateTime.UtcNow));
    }
}
