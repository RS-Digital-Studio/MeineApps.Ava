using BingXBot.Core.Models;

namespace BingXBot.Contracts.Services;

/// <summary>
/// Liefert die Liste aller registrierten Strategien + deren Parameter-Metadaten.
/// Im Client: Cache nach Erstzugriff.
/// </summary>
public interface IStrategyCatalog
{
    Task<IReadOnlyList<StrategyDescriptor>> GetAllAsync(CancellationToken ct = default);
}

/// <summary>
/// Beschreibt eine Strategie fuer UI — Name + Anzeigename + Parameter-Liste.
/// StrategyParameter selbst ist bereits ein POCO in BingXBot.Core.Models.
/// </summary>
public record StrategyDescriptor(
    string Name,
    string DisplayName,
    string Description,
    IReadOnlyList<StrategyParameter> Parameters);
