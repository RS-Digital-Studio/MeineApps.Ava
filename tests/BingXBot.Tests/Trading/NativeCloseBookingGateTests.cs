using BingXBot.Core.Configuration;
using BingXBot.Core.Interfaces;
using BingXBot.Engine;
using BingXBot.Trading;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Trading;

// Atomares Buchungs-Gate fuer native Closes (TryClaimNativeCloseBooking / ClearNativeCloseBooking).
// Hintergrund: WS-Handler, Ticker-Mikro-Race und Orphan-Rekonstruktion konkurrieren um denselben
// Close — genau EIN Pfad darf den CompletedTrade buchen. TryAdd entscheidet das Rennen.
// internal-Methoden via InternalsVisibleTo (BingXBot.Tests) erreichbar.
public class NativeCloseBookingGateTests
{
    private static LiveTradingService CreateService()
    {
        return new LiveTradingService(
            restClient: new FakeExchangeClient(),
            publicClient: Substitute.For<IPublicMarketDataClient>(),
            strategyManager: new StrategyManager(),
            riskSettings: new RiskSettings(),
            scannerSettings: new ScannerSettings(),
            eventBus: new BotEventBus(),
            botSettings: new BotSettings());
    }

    [Fact]
    public void TryClaimNativeCloseBooking_ErsterClaim_True()
    {
        var service = CreateService();
        service.TryClaimNativeCloseBooking("BTC-USDT_Buy").Should().BeTrue();
    }

    [Fact]
    public void TryClaimNativeCloseBooking_ZweiterClaimSelberKey_False()
    {
        // Der erste Pfad gewinnt das Rennen, der zweite Aufruf auf denselben Key wird abgelehnt
        // (verhindert Doppel-Buchung desselben Closes).
        var service = CreateService();
        service.TryClaimNativeCloseBooking("BTC-USDT_Buy").Should().BeTrue();
        service.TryClaimNativeCloseBooking("BTC-USDT_Buy").Should().BeFalse();
    }

    [Fact]
    public void ClearNativeCloseBooking_ErlaubtErneutenClaim()
    {
        // Beim neuen Entry desselben Symbols+Side gibt OnSignalCreated das Gate frei →
        // der naechste native Close dieses Keys muss wieder buchen koennen.
        var service = CreateService();
        service.TryClaimNativeCloseBooking("BTC-USDT_Buy").Should().BeTrue();
        service.TryClaimNativeCloseBooking("BTC-USDT_Buy").Should().BeFalse();

        service.ClearNativeCloseBooking("BTC-USDT_Buy");

        service.TryClaimNativeCloseBooking("BTC-USDT_Buy").Should().BeTrue("nach Clear ist der Key wieder frei");
    }

    [Fact]
    public void TryClaimNativeCloseBooking_VerschiedeneKeys_Unabhaengig()
    {
        // Ein Claim auf BTC-Buy darf einen Claim auf ETH-Sell nicht blockieren.
        var service = CreateService();
        service.TryClaimNativeCloseBooking("BTC-USDT_Buy").Should().BeTrue();
        service.TryClaimNativeCloseBooking("ETH-USDT_Sell").Should().BeTrue();
        // Selber Key bleibt jeweils gesperrt.
        service.TryClaimNativeCloseBooking("BTC-USDT_Buy").Should().BeFalse();
        service.TryClaimNativeCloseBooking("ETH-USDT_Sell").Should().BeFalse();
    }

    [Fact]
    public void ClearNativeCloseBooking_UnbekannterKey_KeinFehler()
    {
        // Clear auf einen nie beanspruchten Key ist ein No-Op (TryRemove ignoriert fehlende Keys).
        var service = CreateService();
        var clear = () => service.ClearNativeCloseBooking("NEVER-CLAIMED_Buy");
        clear.Should().NotThrow();
        // Danach laesst sich der Key normal beanspruchen.
        service.TryClaimNativeCloseBooking("NEVER-CLAIMED_Buy").Should().BeTrue();
    }
}
