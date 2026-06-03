using BingXBot.Contracts.Services;
using BingXBot.Engine.Strategies;

namespace BingXBot.Trading.Local;

/// <summary>
/// Server-seitiger Strategie-Katalog: Liefert alle registrierten Strategien + deren Parameter-Metadaten.
/// Aktiv ist nur noch die TrendFollow-Familie (Live-Default: TrendFollow-Fast).
/// </summary>
public sealed class LocalStrategyCatalog : IStrategyCatalog
{
    public Task<IReadOnlyList<StrategyDescriptor>> GetAllAsync(CancellationToken ct = default)
    {
        var descriptors = StrategyFactory.AvailableStrategies
            .Select(name =>
            {
                var s = StrategyFactory.Create(name);
                return new StrategyDescriptor(
                    Name: s.Name,
                    DisplayName: s.Name,
                    Description: GetDescription(s.Name),
                    Parameters: s.Parameters);
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<StrategyDescriptor>>(descriptors);
    }

    private static string GetDescription(string name) => name switch
    {
        "TrendFollow-Fast" => "Trend-Following (schnell) — Donchian(10)-Breakout in Trend-Richtung, EMA(34)+ADX/DMI-Filter, Market-Entry, ATR-SL ×2.75, RRR 1.5/3.0.",
        _ => string.Empty
    };
}
