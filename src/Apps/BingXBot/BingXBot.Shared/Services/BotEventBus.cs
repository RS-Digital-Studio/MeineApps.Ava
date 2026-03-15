using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Services;

/// <summary>
/// Zentraler Event-Bus für die Kommunikation zwischen ViewModels.
/// Alle ViewModels können Events publizieren/subscriben ohne direkte Referenzen.
/// Registriert als Singleton im DI-Container.
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

    public void PublishTrade(CompletedTrade trade) => TradeCompleted?.Invoke(this, trade);
    public void PublishLog(LogEntry entry) => LogEmitted?.Invoke(this, entry);
    public void PublishBotState(BotState state) => BotStateChanged?.Invoke(this, state);
    public void PublishBacktestCompleted(BacktestCompletedArgs args) => BacktestCompleted?.Invoke(this, args);
}

/// <summary>
/// Argumente für das BacktestCompleted-Event.
/// </summary>
public class BacktestCompletedArgs
{
    public List<CompletedTrade> Trades { get; set; } = new();
    public string StrategyName { get; set; } = "";
    public string Symbol { get; set; } = "";
}
