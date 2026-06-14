using BingXBot.Contracts.Api;
using Microsoft.AspNetCore.SignalR;
using BotLogLevel = BingXBot.Core.Enums.LogLevel;

namespace BingXBot.Server.Hubs;

/// <summary>
/// SignalR-Hub fuer Live-Updates an alle verbundenen Clients.
/// Clients rufen SubscribeSymbol/UnsubscribeSymbol/SetLogFilter auf,
/// empfangen Events wie TickerUpdate/TradeClosed/LogEmitted via Hub-Methoden.
///
/// Broadcast erfolgt durch den BotHubEventForwarder — dieser subscribt auf BotEventBus
/// und leitet alle Events an `IHubContext&lt;BotHub&gt;.Clients.All.SendAsync(...)` weiter.
/// </summary>
public class BotHub : Hub
{
    private readonly ILogger<BotHub> _logger;

    // PERF-3 — Aktive Connection-Anzahl. SignalR instanziiert den Hub transient pro Connection,
    // deshalb ist der Counter static (Interlocked-synchronisiert). Der BotHubEventForwarder liest
    // ihn, um bei 0 verbundenen Clients keinen SendAsync-Task samt Continuation zu allokieren —
    // der Pi laeuft 24/7 meist ohne verbundenen Client.
    private static volatile int _connectionCount;

    /// <summary>PERF-3 — Anzahl aktuell verbundener SignalR-Clients (fuer Broadcast-Kurzschluss).</summary>
    public static int ConnectionCount => _connectionCount;

    public BotHub(ILogger<BotHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        Interlocked.Increment(ref _connectionCount);
        _logger.LogInformation("Client verbunden: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref _connectionCount);
        _logger.LogInformation("Client getrennt: {ConnectionId} (Error={Err})",
            Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }

    // Client -> Server: Symbol-Subscription fuer gezielte Ticker-Updates
    public Task SubscribeSymbol(string symbol) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"sym:{symbol}");

    public Task UnsubscribeSymbol(string symbol) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"sym:{symbol}");

    public Task SetLogFilter(BotLogLevel level)
    {
        // Filter wird in Phase 3.5 ausgewertet — erstmal No-Op (Client filtert clientseitig)
        return Task.CompletedTask;
    }

    // Heartbeat
    public long Ping() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
