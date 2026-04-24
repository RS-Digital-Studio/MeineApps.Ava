using BingXBot.Contracts.Services;
using BingXBot.Engine.Strategies;

namespace BingXBot.Trading.Local;

/// <summary>
/// Server-seitiger Strategie-Katalog: Liefert alle registrierten Strategien + deren Parameter-Metadaten.
/// Nach dem SK-Buch-Refactoring ist nur noch "SK-System" aktiv.
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
        "SK-System" => "Sequenz-Konzept-Strategie — strikt 1:1 nach Tradebook Stefan Kassing. Fibonacci-Retracements, MTFA Weekly→M30, Fix-Pip-SL.",
        _ => string.Empty
    };
}
