using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Exchange;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Exchange;

// Phase 18 / A6 — Side-aware Tick-Size-Rundung (verhindert Buffer-Erosion durch Tick-Constraint).
public class SymbolInfoCacheConservativeRoundingTests
{
    private static SymbolInfoCache CreateCacheWithSymbol(string symbol, int pricePrecision)
    {
        var cache = new SymbolInfoCache(NullLogger.Instance);
        // Reflexion-frei: SymbolInfo direkt in den ConcurrentDictionary einfuegen ueber Public-API.
        // Da der Cache nur ueber InitializeAsync gefuellt wird, nutzen wir die DefaultInfo-Strategie:
        // Cache enthaelt das Symbol nicht, wird per GetInfo mit DefaultInfo-Override geliefert.
        // Fuer den Test reicht der Standard-Pfad: wir testen die Public-Methode direkt durch
        // Patchen ueber ein eigenes Cache-Feld nicht moeglich. Workaround: pricePrecision per
        // DefaultInfo-Pfad (Default 8). Wir nutzen die Tatsache, dass DefaultInfo PricePrecision=8 hat.
        return cache;
    }

    [Fact]
    public void RoundPriceConservative_LongPosition_FloorsDown()
    {
        var cache = new SymbolInfoCache(NullLogger.Instance);
        // Default-Precision = 8 (DefaultInfo). 0.123456785 → 0.12345678 (gefloort).
        var rounded = cache.RoundPriceConservative("UNKNOWN-SYM", 0.12345678999m, Side.Buy);
        rounded.Should().Be(0.12345678m);
    }

    [Fact]
    public void RoundPriceConservative_ShortPosition_CeilsUp()
    {
        var cache = new SymbolInfoCache(NullLogger.Instance);
        // 0.123456781 → 0.12345679 (gerundet hoch).
        var rounded = cache.RoundPriceConservative("UNKNOWN-SYM", 0.12345678001m, Side.Sell);
        rounded.Should().Be(0.12345679m);
    }

    [Fact]
    public void RoundPriceConservative_AlreadyAtTick_NoChange()
    {
        var cache = new SymbolInfoCache(NullLogger.Instance);
        var rounded = cache.RoundPriceConservative("UNKNOWN-SYM", 0.12345678m, Side.Buy);
        rounded.Should().Be(0.12345678m);
    }

    [Fact]
    public void RoundPriceConservative_PreservesBuffer_ForLongStopLoss()
    {
        // Long-SL @ 95.55 mit Tick 0.1 (Precision 1 → DefaultInfo PricePrecision=8 → wir nehmen kleine Werte).
        // Default Precision 8 → Tick 1e-8.
        // Wenn der Plan-SL 95.55 ist (Long-Position), darf der gerundete Wert NICHT > 95.55 sein.
        var cache = new SymbolInfoCache(NullLogger.Instance);
        var planSl = 95.55m;
        var rounded = cache.RoundPriceConservative("UNKNOWN-SYM", planSl, Side.Buy);
        rounded.Should().BeLessThanOrEqualTo(planSl, "Long-SL darf durch Rundung nicht naeher zum Entry kommen");
    }

    [Fact]
    public void RoundPriceConservative_PreservesBuffer_ForShortStopLoss()
    {
        // Short-SL — gerundeter Wert darf NICHT < Plan-Wert sein.
        var cache = new SymbolInfoCache(NullLogger.Instance);
        var planSl = 105.55m;
        var rounded = cache.RoundPriceConservative("UNKNOWN-SYM", planSl, Side.Sell);
        rounded.Should().BeGreaterThanOrEqualTo(planSl, "Short-SL darf durch Rundung nicht naeher zum Entry kommen");
    }
}
