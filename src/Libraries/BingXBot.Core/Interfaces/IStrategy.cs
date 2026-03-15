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
}
