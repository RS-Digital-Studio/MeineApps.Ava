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

    public BotHub(ILogger<BotHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client verbunden: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
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
