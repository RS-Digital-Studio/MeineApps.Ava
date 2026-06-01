using BingXBot.Core.Models;

namespace BingXBot.Core.Interfaces;

public interface IStrategy
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<StrategyParameter> Parameters { get; }
    SignalResult Evaluate(MarketContext context);
    void WarmUp(IReadOnlyList<Candle> history);
    void Reset();
    IStrategy Clone();

    /// <summary>
    /// Ob die Strategie W1/D1-Fahrplan-Kontext (Higher-Timeframe-Candles) benoetigt. SK nutzt den
    /// Fahrplan (true). Reine Navigator-Strategien wie TrendFollow (H4-Donchian) brauchen ihn nicht
    /// → der Scan kann die schweren W1/D1-Klines-Fetches pro Symbol einsparen (entlastet das
    /// Pi-Rate-Limit-Budget, ermoeglicht ein groesseres Scan-Universum). Die D1-Kerze fuer BTC
    /// (BTC-Health) wird unabhaengig davon geladen. Default true (Backward-Compat).
    /// </summary>
    bool RequiresHigherTimeframeContext => true;
}
