using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Risk;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Engine;

public class CorrelationCheckerTests
{
    [Fact]
    public void CalculatePearson_PerfectCorrelation_ShouldReturn1()
    {
        var x = new decimal[] { 1, 2, 3, 4, 5 };
        var y = new decimal[] { 2, 4, 6, 8, 10 };
        var result = CorrelationChecker.CalculatePearson(x, y);
        result.Should().BeApproximately(1m, 0.01m);
    }

    [Fact]
    public void CalculatePearson_NegativeCorrelation_ShouldReturnMinus1()
    {
        var x = new decimal[] { 1, 2, 3, 4, 5 };
        var y = new decimal[] { 10, 8, 6, 4, 2 };
        var result = CorrelationChecker.CalculatePearson(x, y);
        result.Should().BeApproximately(-1m, 0.01m);
    }

    [Fact]
    public void CalculatePearson_NoCorrelation_ShouldBeNearZero()
    {
        var x = new decimal[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var y = new decimal[] { 5, 2, 8, 1, 7, 3, 6, 4 };
        var result = CorrelationChecker.CalculatePearson(x, y);
        Math.Abs(result).Should().BeLessThan(0.5m);
    }

    [Fact]
    public void CalculatePearson_EqualArrays_ShouldReturn1()
    {
        // Zwei identische Reihen haben Korrelation 1
        var x = new decimal[] { 10, 20, 30, 40, 50 };
        var y = new decimal[] { 10, 20, 30, 40, 50 };
        var result = CorrelationChecker.CalculatePearson(x, y);
        result.Should().BeApproximately(1m, 0.001m);
    }

    [Fact]
    public void CalculatePearson_WenigerAlsZweiPunkte_GibtNullZurueck()
    {
        // Weniger als 2 Datenpunkte → keine sinnvolle Korrelation → 0
        var einElement = new decimal[] { 42m };
        var result = CorrelationChecker.CalculatePearson(einElement, einElement);
        result.Should().Be(0m, "Weniger als 2 Punkte → kein Pearson-Koeffizient definiert");
    }

    [Fact]
    public void CalculatePearson_VerschiedeneArrayLaengen_GibtNullZurueck()
    {
        // Arrays unterschiedlicher Länge → 0 (kein Crash)
        var x = new decimal[] { 1, 2, 3 };
        var y = new decimal[] { 1, 2 };
        var result = CorrelationChecker.CalculatePearson(x, y);
        result.Should().Be(0m, "Ungleiche Array-Längen → 0, kein Absturz");
    }

    [Fact]
    public void CalculatePearson_BtcPreisNiveau_KeinOverflow()
    {
        // Bitcoin-Preise im Bereich 50000+ dürfen bei decimal-Multiplikation nicht überlaufen.
        // Pearson rechnet intern in double um genau das zu verhindern.
        var btcPreise = Enumerable.Range(0, 50).Select(i => 50000m + i * 100m).ToArray();
        var ethPreise = Enumerable.Range(0, 50).Select(i => 3000m + i * 6m).ToArray(); // Korreliert

        var act = () => CorrelationChecker.CalculatePearson(btcPreise, ethPreise);
        act.Should().NotThrow("BTC-Preise bei Pearson dürfen keinen Overflow verursachen");

        var result = CorrelationChecker.CalculatePearson(btcPreise, ethPreise);
        result.Should().BeApproximately(1m, 0.01m, "Beide Reihen steigen linear → perfekte Korrelation");
    }

    // === Neue Tests: IsCorrelatedAsync mit IPublicMarketDataClient (17.03.2026) ===

    [Fact]
    public async Task IsCorrelated_KeineOffenenPositionen_GibtFalseZurueck()
    {
        // 0 offene Positionen → sofort false, ohne API-Aufruf
        var mockClient = Substitute.For<IPublicMarketDataClient>();

        var result = await CorrelationChecker.IsCorrelatedAsync(
            "BTC-USDT",
            new List<Position>(),
            maxCorrelation: 0.7m,
            mockClient);

        result.Should().BeFalse("Keine offenen Positionen → keine Korrelation prüfbar");
        // API darf gar nicht aufgerufen werden
        await mockClient.DidNotReceive().GetKlinesAsync(
            Arg.Any<string>(), Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsCorrelated_HochKorreliertesSymbol_GibtTrueZurueck()
    {
        // BTC und ETH bewegen sich synchron → Korrelation nahe 1 → blockiert
        var mockClient = Substitute.For<IPublicMarketDataClient>();

        // Gleichläufige Preisreihen (Korrelation ≈ 1.0)
        var btcCandles = Enumerable.Range(0, 50)
            .Select(i => new Candle(DateTime.UtcNow.AddHours(-50 + i), 50000m + i * 100m,
                50100m + i * 100m, 49900m + i * 100m, 50000m + i * 100m, 1000m, DateTime.UtcNow.AddHours(-49 + i)))
            .ToList();

        var ethCandles = Enumerable.Range(0, 50)
            .Select(i => new Candle(DateTime.UtcNow.AddHours(-50 + i), 3000m + i * 6m,
                3006m + i * 6m, 2994m + i * 6m, 3000m + i * 6m, 5000m, DateTime.UtcNow.AddHours(-49 + i)))
            .ToList();

        mockClient.GetKlinesAsync("BTC-USDT", TimeFrame.H1, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(btcCandles);
        mockClient.GetKlinesAsync("ETH-USDT", TimeFrame.H1, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ethCandles);

        var ethPosition = new Position("ETH-USDT", Side.Buy, 3000m, 3050m, 1m, 50m, 10m, MarginType.Cross, DateTime.UtcNow);

        var result = await CorrelationChecker.IsCorrelatedAsync(
            "BTC-USDT",
            new List<Position> { ethPosition },
            maxCorrelation: 0.8m,
            mockClient);

        result.Should().BeTrue("Gleichläufige BTC/ETH Preise haben Korrelation > 0.8");
    }

    [Fact]
    public async Task IsCorrelated_UnkorreliertesSymbol_GibtFalseZurueck()
    {
        // Zwei unkorrelierte Symbole → Korrelation nahe 0 → erlaubt
        var mockClient = Substitute.For<IPublicMarketDataClient>();

        var btcCandles = Enumerable.Range(0, 50)
            .Select(i => new Candle(DateTime.UtcNow.AddHours(-50 + i), 50000m + i * 100m,
                50100m + i * 100m, 49900m + i * 100m, 50000m + i * 100m, 1000m, DateTime.UtcNow.AddHours(-49 + i)))
            .ToList();

        // SHIB läuft gegenläufig (negative Korrelation)
        var shibCandles = Enumerable.Range(0, 50)
            .Select(i => new Candle(DateTime.UtcNow.AddHours(-50 + i), 0.00002m - i * 0.0000001m,
                0.000021m, 0.000019m, 0.00002m - i * 0.0000001m, 1000000m, DateTime.UtcNow.AddHours(-49 + i)))
            .ToList();

        mockClient.GetKlinesAsync("BTC-USDT", TimeFrame.H1, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(btcCandles);
        mockClient.GetKlinesAsync("SHIB-USDT", TimeFrame.H1, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(shibCandles);

        var shibPosition = new Position("SHIB-USDT", Side.Buy, 0.00002m, 0.000019m, 1000000m, -10m, 10m, MarginType.Cross, DateTime.UtcNow);

        // MaxCorrelation = 0.8 → negative Korrelation (-1.0) hat |Betrag| > 0.8 → blockiert!
        var result = await CorrelationChecker.IsCorrelatedAsync(
            "BTC-USDT",
            new List<Position> { shibPosition },
            maxCorrelation: 0.8m,
            mockClient);

        // |−1.0| = 1.0 > 0.8 → IsCorrelated = true
        // Das ist korrekt: auch stark negativ korrelierte Symbole werden blockiert
        result.Should().BeTrue("Stark negativ korrelierte Symbole (|r| > Schwelle) müssen ebenfalls blockiert werden");
    }

    [Fact]
    public async Task IsCorrelated_GleichesSymbol_WirdUebersprungen()
    {
        // Wenn das neue Symbol identisch mit einer offenen Position ist,
        // soll die Korrelations-Prüfung für genau dieses Symbol übersprungen werden.
        var mockClient = Substitute.For<IPublicMarketDataClient>();

        var btcCandles = Enumerable.Range(0, 50)
            .Select(i => new Candle(DateTime.UtcNow.AddHours(-50 + i), 50000m + i * 100m,
                50100m + i * 100m, 49900m + i * 100m, 50000m + i * 100m, 1000m, DateTime.UtcNow.AddHours(-49 + i)))
            .ToList();

        mockClient.GetKlinesAsync(Arg.Any<string>(), Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(btcCandles);

        // Position ist ebenfalls BTC-USDT → gleiche Symbol → wird im Loop mit `continue` übersprungen
        var btcPosition = new Position("BTC-USDT", Side.Buy, 50000m, 50500m, 0.1m, 50m, 10m, MarginType.Cross, DateTime.UtcNow);

        var result = await CorrelationChecker.IsCorrelatedAsync(
            "BTC-USDT",
            new List<Position> { btcPosition },
            maxCorrelation: 0.8m,
            mockClient);

        // Gleiche Symbole werden übersprungen → keine Korrelation festgestellt → false
        result.Should().BeFalse("Gleiche Symbole werden übersprungen, keine Eigenkorrelation prüfen");
    }

    [Fact]
    public async Task IsCorrelated_ZuWenigKlinesVomServer_GibtFalseZurueck()
    {
        // Wenn der Server weniger als 20 Klines zurückgibt, kann keine sinnvolle
        // Korrelation berechnet werden → nicht blockieren (false).
        var mockClient = Substitute.For<IPublicMarketDataClient>();

        // Nur 5 Klines → zu wenig
        var wenigCandles = TestHelper.GenerateTestCandles(5).ToList();
        mockClient.GetKlinesAsync(Arg.Any<string>(), Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(wenigCandles);

        var ethPosition = new Position("ETH-USDT", Side.Buy, 3000m, 3050m, 1m, 50m, 10m, MarginType.Cross, DateTime.UtcNow);

        var result = await CorrelationChecker.IsCorrelatedAsync(
            "BTC-USDT",
            new List<Position> { ethPosition },
            maxCorrelation: 0.8m,
            mockClient);

        result.Should().BeFalse("Zu wenig Marktdaten → Korrelation nicht prüfbar → nicht blockieren");
    }

    // === Neue Tests: preloadedNewSymbolKlines + Parallelisierung (17.03.2026 Agent-Review) ===

    [Fact]
    public async Task IsCorrelated_VorgeladeneKlines_ApiWirdNichtFuerNeuesSymbolAufgerufen()
    {
        // Wenn preloadedNewSymbolKlines übergeben werden, darf der Client für das neue
        // Symbol (BTC-USDT) NICHT aufgerufen werden. Das spart 1 API-Call pro Scan.
        var mockClient = Substitute.For<IPublicMarketDataClient>();

        // Vorgeladene BTC-Klines (werden direkt übergeben, kein API-Aufruf nötig)
        var btcCandles = Enumerable.Range(0, 50)
            .Select(i => new Candle(DateTime.UtcNow.AddHours(-50 + i), 50000m + i * 100m,
                50100m + i * 100m, 49900m + i * 100m, 50000m + i * 100m, 1000m, DateTime.UtcNow.AddHours(-49 + i)))
            .ToList();

        // ETH-Klines müssen über API geladen werden (offene Position)
        var ethCandles = Enumerable.Range(0, 50)
            .Select(i => new Candle(DateTime.UtcNow.AddHours(-50 + i), 3000m + i * 6m,
                3006m + i * 6m, 2994m + i * 6m, 3000m + i * 6m, 5000m, DateTime.UtcNow.AddHours(-49 + i)))
            .ToList();

        mockClient.GetKlinesAsync("ETH-USDT", TimeFrame.H1, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ethCandles);

        var ethPosition = new Position("ETH-USDT", Side.Buy, 3000m, 3050m, 1m, 50m, 10m, MarginType.Cross, DateTime.UtcNow);

        await CorrelationChecker.IsCorrelatedAsync(
            "BTC-USDT",
            new List<Position> { ethPosition },
            maxCorrelation: 0.8m,
            mockClient,
            preloadedNewSymbolKlines: btcCandles);

        // BTC-USDT darf NICHT per API geladen werden (war bereits vorgeladen)
        await mockClient.DidNotReceive().GetKlinesAsync(
            "BTC-USDT", Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());

        // ETH-USDT MUSS per API geladen worden sein (offene Position, nicht vorgeladen)
        await mockClient.Received(1).GetKlinesAsync(
            "ETH-USDT", Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsCorrelated_DreiPositionen_AlleKlinesParallelGeladen()
    {
        // Bei 3 offenen Positionen (ETH, SOL, BNB) müssen genau 3 Klines-Calls
        // gemacht werden (einer pro Position). Task.WhenAll statt sequentiell.
        // Nachweis: Alle 3 Symbole wurden exakt einmal angefragt.
        var mockClient = Substitute.For<IPublicMarketDataClient>();

        // Unkorrrelierte Candles für alle Symbole (damit kein früher Abbruch)
        var btcCandles = Enumerable.Range(0, 50)
            .Select(i => new Candle(DateTime.UtcNow.AddHours(-50 + i), 50000m + i * 100m,
                50100m + i * 100m, 49900m + i * 100m, 50000m + i * 100m, 1000m, DateTime.UtcNow.AddHours(-49 + i)))
            .ToList();

        // Alle Positions-Symbole geben zufällig aussehende Candles zurück (keine Korrelation mit BTC)
        var zufallCandles = TestHelper.GenerateTestCandles(50, startPrice: 10m, volatility: 0.5m);

        mockClient.GetKlinesAsync(Arg.Any<string>(), Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(zufallCandles);

        var positionen = new List<Position>
        {
            new("ETH-USDT", Side.Buy, 3000m, 3050m, 1m, 50m, 10m, MarginType.Cross, DateTime.UtcNow),
            new("SOL-USDT", Side.Buy, 150m, 155m, 10m, 50m, 10m, MarginType.Cross, DateTime.UtcNow),
            new("BNB-USDT", Side.Buy, 600m, 610m, 2m, 20m, 10m, MarginType.Cross, DateTime.UtcNow),
        };

        await CorrelationChecker.IsCorrelatedAsync(
            "BTC-USDT",
            positionen,
            maxCorrelation: 0.95m, // Sehr hoher Schwellwert → kein Abbruch nach erstem Treffer
            mockClient,
            preloadedNewSymbolKlines: btcCandles);

        // ETH, SOL, BNB müssen je genau 1x geladen worden sein
        await mockClient.Received(1).GetKlinesAsync(
            "ETH-USDT", Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await mockClient.Received(1).GetKlinesAsync(
            "SOL-USDT", Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await mockClient.Received(1).GetKlinesAsync(
            "BNB-USDT", Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());

        // BTC-USDT darf nicht zusätzlich geladen worden sein (war vorgeladen)
        await mockClient.DidNotReceive().GetKlinesAsync(
            "BTC-USDT", Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsCorrelated_ApiWirftException_GibtFalseZurueck()
    {
        // Wenn der API-Aufruf für eine der Positionen fehlschlägt (z.B. Netzwerkfehler),
        // soll IsCorrelatedAsync false zurückgeben und NICHT die ganze Trading-Pipeline abbrechen.
        var mockClient = Substitute.For<IPublicMarketDataClient>();

        // BTC vorgeladen, ETH-Aufruf schlägt fehl
        var btcCandles = Enumerable.Range(0, 50)
            .Select(i => new Candle(DateTime.UtcNow.AddHours(-50 + i), 50000m + i * 100m,
                50100m + i * 100m, 49900m + i * 100m, 50000m + i * 100m, 1000m, DateTime.UtcNow.AddHours(-49 + i)))
            .ToList();

        // Task.FromException erzeugt einen "faulted" Task - wirft erst beim await, nicht beim Erstellen.
        // So verhält sich auch ein echter HttpClient-Fehler.
        mockClient.GetKlinesAsync("ETH-USDT", Arg.Any<TimeFrame>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<List<Candle>>(new HttpRequestException("Netzwerk nicht erreichbar")));

        var ethPosition = new Position("ETH-USDT", Side.Buy, 3000m, 3050m, 1m, 50m, 10m, MarginType.Cross, DateTime.UtcNow);

        var result = await CorrelationChecker.IsCorrelatedAsync(
            "BTC-USDT",
            new List<Position> { ethPosition },
            maxCorrelation: 0.8m,
            mockClient,
            preloadedNewSymbolKlines: btcCandles);

        result.Should().BeFalse("API-Fehler beim Korrelations-Check darf nicht blockieren (false statt Exception)");
    }
}
