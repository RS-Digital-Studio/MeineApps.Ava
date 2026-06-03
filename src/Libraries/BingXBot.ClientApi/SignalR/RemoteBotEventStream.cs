using BingXBot.ClientApi.Connection;
using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using BotLogLevel = BingXBot.Core.Enums.LogLevel;

namespace BingXBot.ClientApi.SignalR;

/// <summary>
/// Remote-Impl von IBotEventStream: Verbindet per SignalR mit dem BingXBot-Server
/// und leitet alle empfangenen Events an die entsprechenden C#-Events weiter.
///
/// Auto-Reconnect ist aktiv (Standard-Backoff: 0, 2, 10, 30 Sekunden). Beim Verbindungsverlust
/// wird ConnectionStatus auf Reconnecting gesetzt — UI kann das visualisieren.
/// </summary>
public sealed class RemoteBotEventStream : IBotEventStream
{
    private readonly ServerConnection _connection;
    private readonly ILogger<RemoteBotEventStream> _logger;
    private HubConnection? _hub;
    // BaseUrl der aktuell aufgebauten SignalR-Verbindung. Damit unterscheidet der Changed-Handler
    // einen reinen Token-Refresh (kein Neustart noetig) von einem echten Server-Wechsel.
    private string? _connectedBaseUrl;
    private ConnectionStatus _status = ConnectionStatus.Disconnected;

    public ConnectionStatus Connection
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            ConnectionChanged?.Invoke(value);
        }
    }

    public event Action<ConnectionStatus>? ConnectionChanged;
    public event Action<BotStateChangedDto>? BotStateChanged;
    public event Action<TradeDto>? TradeOpened;
    public event Action<TradeDto>? TradeClosed;
    public event Action<PositionDto>? PositionUpdated;
    public event Action<EquityPointDto>? EquityUpdate;
    public event Action<MarginWarningDto>? MarginWarning;
    public event Action<TickerUpdateDto>? TickerUpdate;
    public event Action<TickerUpdateDto>? BtcPriceUpdate;
    public event Action<ScannerResultDto>? ScannerResult;
    public event Action<LogEntryDto>? LogEmitted;
    public event Action<ActivityFeedDto>? ActivityFeed;
    public event Action<BacktestProgressDto>? BacktestProgress;
    public event Action<BacktestResultDto>? BacktestCompleted;
    public event Action<FullSettingsDto>? SettingsChanged;
    public event Action<ConnectionDegradedDto>? ConnectionDegraded;
    /// <summary>v1.5.2 Phase 4 — Decision-Trail Live-Forward von Server.</summary>
    public event Action<EvaluationDecisionDto>? EvaluationDecided;

    public RemoteBotEventStream(ServerConnection connection, ILogger<RemoteBotEventStream> logger)
    {
        _connection = connection;
        _logger = logger;
        _connection.Changed += profile =>
        {
            // Token-Refresh aendert nur Token/RefreshToken — der AccessTokenProvider holt den neuen
            // Token beim naechsten (Re)Connect dynamisch. Ein SignalR-Neustart bei jedem Refresh waere
            // unnoetig und riss die Verbindung ab (Event-Luecke + Konflikt mit WithAutomaticReconnect).
            // Nur bei echtem Server-Wechsel (BaseUrl) oder Entkopplung neu verbinden.
            var newUrl = _connection.IsPaired ? profile?.BaseUrl : null;
            if (!string.Equals(newUrl, _connectedBaseUrl, StringComparison.OrdinalIgnoreCase))
                _ = RestartIfConnectedAsync();
        };
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_connection.Profile is not { } profile)
        {
            Connection = ConnectionStatus.Disconnected;
            return;
        }

        await StopAsync(ct).ConfigureAwait(false);

        var hubUrl = profile.BaseUrl.TrimEnd('/') + ApiRoutes.BotHubPath;
        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Token als Bearer + access_token-Query (SignalR WebSocket Standard)
                options.AccessTokenProvider = () => Task.FromResult<string?>(_connection.CurrentToken);
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        WireHandlers(_hub);

        _hub.Reconnecting += _ => { Connection = ConnectionStatus.Reconnecting; return Task.CompletedTask; };
        _hub.Reconnected += _ => { Connection = ConnectionStatus.Connected; return Task.CompletedTask; };
        _hub.Closed += ex =>
        {
            Connection = ConnectionStatus.Disconnected;
            _logger.LogWarning("SignalR-Hub geschlossen: {Error}", ex?.Message);
            return Task.CompletedTask;
        };

        try
        {
            Connection = ConnectionStatus.Connecting;
            await _hub.StartAsync(ct).ConfigureAwait(false);
            Connection = ConnectionStatus.Connected;
            _connectedBaseUrl = profile.BaseUrl;
            _logger.LogInformation("Mit BingXBot-Server verbunden: {Url}", hubUrl);
        }
        catch (Exception ex)
        {
            Connection = ConnectionStatus.Degraded;
            _logger.LogError(ex, "SignalR-Verbindungsaufbau fehlgeschlagen");
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_hub == null) return;
        try { await _hub.StopAsync(ct).ConfigureAwait(false); } catch { }
        await _hub.DisposeAsync().ConfigureAwait(false);
        _hub = null;
        _connectedBaseUrl = null;
        Connection = ConnectionStatus.Disconnected;
    }

    // SignalR-Overload-Quirk: InvokeAsync(method, arg1, cancellationToken) existiert als
    // `InvokeAsync(string, object?, CancellationToken)`-Ueberladung NUR wenn der Parameter-Typ
    // object? ist. Bei string/enum-Argumenten waehlt der Compiler `InvokeAsync(string, object?[], CancellationToken)`,
    // was dazu fuehrt dass `ct` als zweites Argument interpretiert wird. Explizit via Object-Array
    // aufrufen um das zu vermeiden.
    public Task SubscribeSymbolAsync(string symbol, CancellationToken ct = default) =>
        _hub?.InvokeCoreAsync(HubMethods.SubscribeSymbol, new object[] { symbol }, ct) ?? Task.CompletedTask;

    public Task UnsubscribeSymbolAsync(string symbol, CancellationToken ct = default) =>
        _hub?.InvokeCoreAsync(HubMethods.UnsubscribeSymbol, new object[] { symbol }, ct) ?? Task.CompletedTask;

    public Task SetLogFilterAsync(BotLogLevel minLevel, CancellationToken ct = default) =>
        _hub?.InvokeCoreAsync(HubMethods.SetLogFilter, new object[] { minLevel }, ct) ?? Task.CompletedTask;

    private async Task RestartIfConnectedAsync()
    {
        if (_connection.IsPaired) await StartAsync();
        else await StopAsync();
    }

    private void WireHandlers(HubConnection hub)
    {
        hub.On<BotStateChangedDto>(HubMethods.BotStateChanged, dto => BotStateChanged?.Invoke(dto));
        hub.On<TradeDto>(HubMethods.TradeOpened, dto => TradeOpened?.Invoke(dto));
        hub.On<TradeDto>(HubMethods.TradeClosed, dto => TradeClosed?.Invoke(dto));
        hub.On<PositionDto>(HubMethods.PositionUpdated, dto => PositionUpdated?.Invoke(dto));
        hub.On<EquityPointDto>(HubMethods.EquityUpdate, dto => EquityUpdate?.Invoke(dto));
        hub.On<MarginWarningDto>(HubMethods.MarginWarning, dto => MarginWarning?.Invoke(dto));
        hub.On<TickerUpdateDto>(HubMethods.TickerUpdate, dto => TickerUpdate?.Invoke(dto));
        hub.On<TickerUpdateDto>(HubMethods.BtcPriceUpdate, dto => BtcPriceUpdate?.Invoke(dto));
        hub.On<ScannerResultDto>(HubMethods.ScannerResult, dto => ScannerResult?.Invoke(dto));
        hub.On<LogEntryDto>(HubMethods.LogEmitted, dto => LogEmitted?.Invoke(dto));
        // 04.05.2026 — Batched Logs (Server-seitig 250 ms Buffer): in einzelne LogEmitted-Events splitten,
        // damit bestehende ViewModel-Subscriber nichts ändern müssen.
        hub.On<IReadOnlyList<LogEntryDto>>(HubMethods.LogBatch, batch =>
        {
            if (batch == null) return;
            foreach (var dto in batch)
                LogEmitted?.Invoke(dto);
        });
        hub.On<ActivityFeedDto>(HubMethods.ActivityFeed, dto => ActivityFeed?.Invoke(dto));
        hub.On<BacktestProgressDto>(HubMethods.BacktestProgress, dto => BacktestProgress?.Invoke(dto));
        hub.On<BacktestResultDto>(HubMethods.BacktestCompleted, dto => BacktestCompleted?.Invoke(dto));
        hub.On<FullSettingsDto>(HubMethods.SettingsChanged, dto => SettingsChanged?.Invoke(dto));
        hub.On<ConnectionDegradedDto>(HubMethods.ConnectionDegraded, dto => ConnectionDegraded?.Invoke(dto));
        hub.On<EvaluationDecisionDto>(HubMethods.EvaluationDecided, dto => EvaluationDecided?.Invoke(dto));
    }

    public void Dispose()
    {
        _ = StopAsync();
    }
}
