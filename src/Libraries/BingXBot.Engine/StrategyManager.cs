using System.Collections.Concurrent;
using BingXBot.Core.Interfaces;

namespace BingXBot.Engine;

/// <summary>
/// Verwaltet Strategie-Instanzen pro Symbol (Thread-safe).
/// Klont die Template-Strategie für jedes Symbol, damit jede Instanz
/// ihren eigenen Zustand (z.B. Warmup) hat.
/// </summary>
public class StrategyManager
{
    private readonly ConcurrentDictionary<string, IStrategy> _symbolStrategies = new();
    private readonly object _templateLock = new();
    private IStrategy? _templateStrategy;

    /// <summary>Aktuelle Template-Strategie (wird für neue Symbole geklont)</summary>
    public IStrategy? CurrentTemplate
    {
        get { lock (_templateLock) { return _templateStrategy; } }
    }

    /// <summary>Setzt die Template-Strategie und entfernt alle Symbol-Instanzen</summary>
    public void SetStrategy(IStrategy strategy)
    {
        lock (_templateLock)
        {
            _templateStrategy = strategy;
            _symbolStrategies.Clear();
        }
    }

    /// <summary>Gibt die Strategie-Instanz für ein Symbol zurück (klont beim ersten Aufruf)</summary>
    public IStrategy GetOrCreateForSymbol(string symbol)
    {
        IStrategy? template;
        lock (_templateLock)
        {
            template = _templateStrategy;
        }

        if (template == null)
            throw new InvalidOperationException("Keine Strategie gesetzt");

        return _symbolStrategies.GetOrAdd(symbol, _ => template.Clone());
    }

    /// <summary>Entfernt die Strategie-Instanz für ein Symbol</summary>
    public void RemoveSymbol(string symbol) => _symbolStrategies.TryRemove(symbol, out _);

    /// <summary>Entfernt alle Symbol-Instanzen</summary>
    public void Reset() => _symbolStrategies.Clear();
}
