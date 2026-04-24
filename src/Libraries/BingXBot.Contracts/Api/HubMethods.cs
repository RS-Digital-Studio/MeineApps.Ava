namespace BingXBot.Contracts.Api;

/// <summary>
/// Konstanten fuer SignalR-Hub-Methoden. Server invokt -> Client empfaengt.
/// </summary>
public static class HubMethods
{
    // Server -> Client
    public const string BotStateChanged = nameof(BotStateChanged);
    public const string TickerUpdate = nameof(TickerUpdate);
    public const string BtcPriceUpdate = nameof(BtcPriceUpdate);
    public const string TradeOpened = nameof(TradeOpened);
    public const string TradeClosed = nameof(TradeClosed);
    public const string PositionUpdated = nameof(PositionUpdated);
    public const string EquityUpdate = nameof(EquityUpdate);
    public const string LogEmitted = nameof(LogEmitted);
    public const string ActivityFeed = nameof(ActivityFeed);
    public const string MarginWarning = nameof(MarginWarning);
    public const string BacktestProgress = nameof(BacktestProgress);
    public const string BacktestCompleted = nameof(BacktestCompleted);
    public const string ScannerResult = nameof(ScannerResult);
    public const string ConnectionDegraded = nameof(ConnectionDegraded);
    public const string SettingsChanged = nameof(SettingsChanged);

    // Client -> Server (Invoke)
    public const string SubscribeSymbol = nameof(SubscribeSymbol);
    public const string UnsubscribeSymbol = nameof(UnsubscribeSymbol);
    public const string SetLogFilter = nameof(SetLogFilter);
    public const string Ping = nameof(Ping);
}
