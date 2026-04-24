using BingXBot.Trading;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Trading;

// Regression-Tests fuer den TP-nach-Limit-Fill Bug (24.04.2026):
// Der Bug entstand weil _pendingLimitOrders seit Commit 6c49e61 mit "{Symbol}#{SequenceId}"
// indiziert wird (BuildPendingKey), aber der Reconcile-Loop "kvp.Key" direkt als BingX-Symbol
// an REST-APIs + Positions-Lookups weitergab. Folge: Filled-Detection schlug nie an →
// PlaceTpLimitOrdersAfterFillAsync wurde nie aufgerufen → Positionen ohne TPs.
//
// Diese Tests sichern das Key-Format ab. Wenn jemand den Separator oder das Roundtrip-Verhalten
// aendert, muessen die Reconcile-Stellen ebenfalls angepasst werden.
public class PendingOrderKeyTests
{
    [Fact]
    public void BuildPendingKey_MitSymbolUndSequenceId_ErzeugtKeyMitHashSeparator()
    {
        var key = LiveTradingService.BuildPendingKey("BTC-USDT", "A12345_Prim");

        key.Should().Be("BTC-USDT#A12345_Prim");
    }

    [Fact]
    public void BuildPendingKey_OhneSequenceId_ErzeugtKeyMitUnderscoreFallback()
    {
        // Wenn keine SequenceId vorhanden, wird "_" als Platzhalter genutzt — sodass das
        // Key-Format einheitlich bleibt (immer genau ein "#" drin).
        var key = LiveTradingService.BuildPendingKey("ETH-USDT", null);

        key.Should().Be("ETH-USDT#_");
    }

    [Fact]
    public void BuildPendingKey_FuerPrimUndAddSibling_ErzeugtUnterschiedlicheKeys()
    {
        // Kritische Invariante: Prim/Add-Geschwister des gleichen Symbols duerfen NICHT
        // kollidieren, sonst ueberschreibt das Dictionary eine Order.
        var primKey = LiveTradingService.BuildPendingKey("BTC-USDT", "A12345_Prim");
        var addKey = LiveTradingService.BuildPendingKey("BTC-USDT", "A12345_Add");

        primKey.Should().NotBe(addKey);
    }

    [Fact]
    public void ExtractSymbolFromPendingKey_NeuesFormatMitHash_LiefertNurSymbol()
    {
        // Das ist die Stelle wo der Bug bisher lag: Wenn man den Key direkt als Symbol
        // verwendet, matcht kein p.Symbol (weil die BingX-Position "BTC-USDT" heisst,
        // nicht "BTC-USDT#A12345_Prim"). Der Helper trennt das sauber.
        var symbol = LiveTradingService.ExtractSymbolFromPendingKey("BTC-USDT#A12345_Prim");

        symbol.Should().Be("BTC-USDT");
    }

    [Fact]
    public void ExtractSymbolFromPendingKey_LegacyKeyOhneHash_LiefertKeyUnveraendert()
    {
        // Backward-Compat: Vor dem Architektur-Split war der Key nur das Symbol.
        // Restore-Pfad aus DB toleriert solche Legacy-Eintraege.
        var symbol = LiveTradingService.ExtractSymbolFromPendingKey("ETH-USDT");

        symbol.Should().Be("ETH-USDT");
    }

    [Fact]
    public void ExtractSymbolFromPendingKey_MitUnderscoreFallback_LiefertNurSymbol()
    {
        var symbol = LiveTradingService.ExtractSymbolFromPendingKey("SOL-USDT#_");

        symbol.Should().Be("SOL-USDT");
    }

    [Theory]
    [InlineData("BTC-USDT", "A12345_Prim")]
    [InlineData("ETH-USDT", "B67890_Add")]
    [InlineData("SOL-USDT", null)]
    [InlineData("1000PEPE-USDT", "C_L500")]
    public void Roundtrip_BuildThenExtract_LiefertUrspruenglichesSymbol(string symbol, string? sequenceId)
    {
        // Dieser Test sichert die Kern-Invariante des Key-Systems ab:
        // Egal wie die SequenceId aussieht — das Symbol muss beim Extract wieder heraus kommen,
        // damit REST-API-Calls und Positions-Lookups funktionieren.
        var key = LiveTradingService.BuildPendingKey(symbol, sequenceId);
        var extracted = LiveTradingService.ExtractSymbolFromPendingKey(key);

        extracted.Should().Be(symbol);
    }
}
