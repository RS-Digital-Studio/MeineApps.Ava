using BingXBot.Core.Interfaces;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Zentrale Factory für Strategie-Instanzen (DRY).
/// Ersetzt duplizierte switch-Expressions in StrategyViewModel und BacktestViewModel.
/// </summary>
public static class StrategyFactory
{
    /// <summary>Alle verfügbaren Strategie-Namen.</summary>
    public static readonly string[] AvailableStrategies = ["EMA Cross", "RSI", "Bollinger Bands", "MACD", "Grid"];

    /// <summary>Erstellt eine neue IStrategy-Instanz basierend auf dem Namen.</summary>
    public static IStrategy Create(string name) => name switch
    {
        "EMA Cross" => new EmaCrossStrategy(),
        "RSI" => new RsiStrategy(),
        "Bollinger Bands" => new BollingerStrategy(),
        "MACD" => new MacdStrategy(),
        "Grid" => new GridStrategy(),
        _ => throw new ArgumentException($"Unbekannte Strategie: {name}")
    };
}
