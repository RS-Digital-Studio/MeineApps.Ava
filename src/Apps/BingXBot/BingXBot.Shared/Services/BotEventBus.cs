using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Services;

/// <summary>
/// Zentraler Event-Bus für die Kommunikation zwischen ViewModels.
/// Alle ViewModels können Events publizieren/subscriben ohne direkte Referenzen.
/// Registriert als Singleton im DI-Container.
/// Performance: PublishLog prüft HasLogSubscribers VOR Allokation der LogEntry.
/// Caller sollten HasLogSubscribers nutzen um String-Interpolation zu vermeiden.
/// </summary>
public class BotEventBus
{
    /// <summary>Trade abgeschlossen (von Bot oder Backtest).</summary>
    public event EventHandler<CompletedTrade>? TradeCompleted;

    /// <summary>Log-Eintrag (von Bot, Backtest, Scanner etc.).</summary>
    public event EventHandler<LogEntry>? LogEmitted;

    /// <summary>Bot-Status geändert (Running, Paused, Stopped etc.).</summary>
    public event EventHandler<BotState>? BotStateChanged;

    /// <summary>Backtest abgeschlossen mit Ergebnissen.</summary>
    public event EventHandler<BacktestCompletedArgs>? BacktestCompleted;

    /// <summary>Desktop-Benachrichtigung angefordert (Trade-Events, Warnungen).</summary>
    public event EventHandler<NotificationArgs>? NotificationRequested;

    /// <summary>Margin-Warnung: Position nähert sich Liquidationspreis.</summary>
    public event EventHandler<MarginWarningArgs>? MarginWarning;

    /// <summary>Ob mindestens ein Subscriber für LogEmitted registriert ist. Prüfen VOR LogEntry-Allokation.</summary>
    public bool HasLogSubscribers => LogEmitted != null;

    public void PublishTrade(CompletedTrade trade) => TradeCompleted?.Invoke(this, trade);

    /// <summary>Publiziert einen Log-Eintrag. Wird ignoriert wenn kein Subscriber registriert ist.</summary>
    public void PublishLog(LogEntry entry)
    {
        LogEmitted?.Invoke(this, entry);
    }

    public void PublishBotState(BotState state) => BotStateChanged?.Invoke(this, state);
    public void PublishBacktestCompleted(BacktestCompletedArgs args) => BacktestCompleted?.Invoke(this, args);

    /// <summary>Sendet eine Desktop-Benachrichtigung (Titel + Nachricht).</summary>
    public void PublishNotification(string title, string message) =>
        NotificationRequested?.Invoke(this, new NotificationArgs(title, message));

    /// <summary>Sendet eine Margin-Warnung für eine Position.</summary>
    public void PublishMarginWarning(string symbol, decimal currentPrice, decimal liquidationPrice, decimal distancePercent) =>
        MarginWarning?.Invoke(this, new MarginWarningArgs(symbol, currentPrice, liquidationPrice, distancePercent));
}

/// <summary>Argumente für das BacktestCompleted-Event.</summary>
public class BacktestCompletedArgs
{
    public List<CompletedTrade> Trades { get; set; } = new();
    public string StrategyName { get; set; } = "";
    public string Symbol { get; set; } = "";
}

/// <summary>Desktop-Benachrichtigung.</summary>
public record NotificationArgs(string Title, string Message);

/// <summary>Margin-Warnung: Position nähert sich Liquidation.</summary>
public record MarginWarningArgs(string Symbol, decimal CurrentPrice, decimal LiquidationPrice, decimal DistancePercent);
