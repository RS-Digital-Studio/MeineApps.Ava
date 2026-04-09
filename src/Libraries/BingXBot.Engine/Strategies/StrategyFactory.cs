using BingXBot.Core.Interfaces;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Zentrale Factory für Strategie-Instanzen (DRY).
/// Ersetzt duplizierte switch-Expressions in StrategyViewModel und BacktestViewModel.
/// </summary>
public static class StrategyFactory
{
    /// <summary>Alle verfügbaren Strategie-Namen. CryptoTrendPro als erster (Default).</summary>
    public static readonly string[] AvailableStrategies =
        ["CryptoTrendPro", "SK-System", "Trend-Following", "EMA Cross", "RSI Momentum", "Bollinger Breakout", "MACD", "Smart Grid", "Breakout-Pullback"];

    /// <summary>Erstellt eine neue IStrategy-Instanz basierend auf dem Namen.</summary>
    public static IStrategy Create(string name) => name switch
    {
        "CryptoTrendPro" => new CryptoTrendProStrategy(),
        "SK-System" => new SequenzKonzeptStrategy(),
        "Trend-Following" => new TrendFollowStrategy(),
        "EMA Cross" => new EmaCrossStrategy(),
        "RSI Momentum" => new RsiStrategy(),
        "Bollinger Breakout" => new BollingerStrategy(),
        "MACD" => new MacdStrategy(),
        "Smart Grid" => new GridStrategy(),
        "Breakout-Pullback" => new BreakoutPullbackStrategy(),
        _ => throw new ArgumentException($"Unbekannte Strategie: {name}")
    };
}
