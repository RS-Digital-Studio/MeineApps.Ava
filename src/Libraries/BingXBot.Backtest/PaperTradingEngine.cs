using BingXBot.Backtest.Simulation;
using BingXBot.Core.Configuration;

namespace BingXBot.Backtest;

/// <summary>
/// Konfiguriert einen SimulatedExchange für Paper-Trading mit echten Live-Daten.
/// Die eigentliche Trading-Logik läuft in der TradingEngine.
/// </summary>
public static class PaperTradingEngine
{
    /// <summary>
    /// Erstellt einen SimulatedExchange für Paper-Trading.
    /// </summary>
    public static SimulatedExchange CreatePaperExchange(BacktestSettings? settings = null)
    {
        return new SimulatedExchange(settings ?? new BacktestSettings { InitialBalance = 10000m });
    }
}
