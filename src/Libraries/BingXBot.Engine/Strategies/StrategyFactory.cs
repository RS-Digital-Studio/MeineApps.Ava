using BingXBot.Core.Interfaces;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Factory für Strategie-Instanzen. Nach dem SK-Buch-Refactoring (12.04.2026) ist
/// das SK-System die einzige Strategie.
/// </summary>
public static class StrategyFactory
{
    /// <summary>Verfügbare Strategien — nur SK-System (Buch-konform).</summary>
    public static readonly string[] AvailableStrategies = ["SK-System"];

    /// <summary>Erstellt eine neue IStrategy-Instanz basierend auf dem Namen.</summary>
    public static IStrategy Create(string name) => name switch
    {
        "SK-System" => new SequenzKonzeptStrategy(),
        _ => throw new ArgumentException($"Unbekannte Strategie: {name}")
    };
}
