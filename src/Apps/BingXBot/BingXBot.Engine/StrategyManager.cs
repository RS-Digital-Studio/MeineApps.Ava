using System.Collections.Concurrent;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;

namespace BingXBot.Engine;

/// <summary>
/// Verwaltet Strategie-Instanzen pro (Symbol, Navigator-TF) — Thread-safe.
/// Multi-TF Standalone (15.04.2026): Jede Navigator-TF hat eigenen Klon pro Symbol,
/// damit State (Signal-Cooldown, Triple-Entry-Flags, Ampel) TF-getrennt ist.
/// </summary>
public class StrategyManager
{
    // Key = "{symbol}|{tf}" — TF-getrennte Klone (eine Sequenz pro TF pro Symbol)
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

    /// <summary>Gibt die Strategie-Instanz für (Symbol, TF) zurück. Klont beim ersten Aufruf.</summary>
    public IStrategy GetOrCreateForSymbol(string symbol, TimeFrame tf)
    {
        lock (_templateLock)
        {
            if (_templateStrategy == null)
                throw new InvalidOperationException("Keine Strategie gesetzt");

            var key = BuildKey(symbol, tf);
            return _symbolStrategies.GetOrAdd(key, _ => _templateStrategy.Clone());
        }
    }

    /// <summary>Legacy-Overload für Backtest (nutzt H4 als Default-TF).</summary>
    public IStrategy GetOrCreateForSymbol(string symbol) => GetOrCreateForSymbol(symbol, TimeFrame.H4);

    /// <summary>Entfernt alle Strategie-Instanzen für ein Symbol (alle TFs)</summary>
    public void RemoveSymbol(string symbol)
    {
        var prefix = symbol + "|";
        foreach (var k in _symbolStrategies.Keys)
        {
            if (k.StartsWith(prefix, StringComparison.Ordinal))
                _symbolStrategies.TryRemove(k, out _);
        }
    }

    /// <summary>Entfernt alle Symbol-Instanzen</summary>
    public void Reset() => _symbolStrategies.Clear();

    private static string BuildKey(string symbol, TimeFrame tf) => symbol + "|" + tf;
}
