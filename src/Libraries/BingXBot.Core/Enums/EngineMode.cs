namespace BingXBot.Core.Enums;

/// <summary>
/// Welche Trading-Engine laeuft — orthogonal zu <see cref="TradingMode"/> (Live/Paper = echt vs. simuliert).
/// <see cref="Scalper"/> = der bestehende per-Symbol-Signal-Scanner (TrendFollow-Fast etc.).
/// <see cref="CrossSectional"/> = market-neutraler Momentum-Korb (long Top-K / short Bottom-K, monatlicher Rebalance).
/// </summary>
public enum EngineMode
{
    Scalper,
    CrossSectional,
}
