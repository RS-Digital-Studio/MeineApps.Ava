using BingXBot.Engine.Filters;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

public class TradingHoursFilterTests
{
    [Fact]
    public void Krypto_Immer24x7()
    {
        // Samstag 03:00 UTC — Krypto muss offen sein
        var saturday3am = new DateTime(2026, 4, 11, 3, 0, 0, DateTimeKind.Utc);
        TradingHoursFilter.IsMarketOpen("BTC-USDT", saturday3am).Should().BeTrue();
        TradingHoursFilter.IsMarketOpen("ETH-USDT", saturday3am).Should().BeTrue();
    }

    [Fact]
    public void TradFi_AmWochenendeGeschlossen()
    {
        var saturday = new DateTime(2026, 4, 11, 12, 0, 0, DateTimeKind.Utc);
        TradingHoursFilter.IsMarketOpen("NCCOGOLD2USD-USDT", saturday).Should().BeFalse();
        TradingHoursFilter.IsMarketOpen("NCSINASDAQ1002USD-USDT", saturday).Should().BeFalse();
        TradingHoursFilter.IsMarketOpen("NCFXEUR2USD-USDT", saturday).Should().BeFalse();
        TradingHoursFilter.IsMarketOpen("NCSKTSLA2USD-USDT", saturday).Should().BeFalse();
    }

    [Fact]
    public void Commodity_WährendHandelszeitenOffen()
    {
        // Mittwoch 10:00 UTC — Commodities offen (01:00-21:00)
        var wednesday10am = new DateTime(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc);
        TradingHoursFilter.IsMarketOpen("NCCOGOLD2USD-USDT", wednesday10am).Should().BeTrue();
    }

    [Fact]
    public void Commodity_AußerhalbHandelszeitenGeschlossen()
    {
        // Mittwoch 22:00 UTC — Commodities geschlossen (>21:00)
        var wednesday10pm = new DateTime(2026, 4, 8, 22, 0, 0, DateTimeKind.Utc);
        TradingHoursFilter.IsMarketOpen("NCCOGOLD2USD-USDT", wednesday10pm).Should().BeFalse();
    }

    [Fact]
    public void Stock_NurWährendUSMarktzeiten()
    {
        // Mittwoch 15:00 UTC — US-Markt offen (10:00-21:00 mit Pre-Market)
        var wednesday3pm = new DateTime(2026, 4, 8, 15, 0, 0, DateTimeKind.Utc);
        TradingHoursFilter.IsMarketOpen("NCSKTSLA2USD-USDT", wednesday3pm).Should().BeTrue();

        // Mittwoch 08:00 UTC — zu früh für US-Aktien (<10:00)
        var wednesday8am = new DateTime(2026, 4, 8, 8, 0, 0, DateTimeKind.Utc);
        TradingHoursFilter.IsMarketOpen("NCSKTSLA2USD-USDT", wednesday8am).Should().BeFalse();
    }

    [Fact]
    public void Forex_24x5_FreitagAbendGeschlossen()
    {
        // Freitag 23:00 UTC — Forex geschlossen (>22:00 Freitag)
        var friday11pm = new DateTime(2026, 4, 10, 23, 0, 0, DateTimeKind.Utc);
        TradingHoursFilter.IsMarketOpen("NCFXEUR2USD-USDT", friday11pm).Should().BeFalse();

        // Donnerstag 23:00 UTC — Forex offen (24/5)
        var thursday11pm = new DateTime(2026, 4, 9, 23, 0, 0, DateTimeKind.Utc);
        TradingHoursFilter.IsMarketOpen("NCFXEUR2USD-USDT", thursday11pm).Should().BeTrue();
    }

    [Fact]
    public void StatusText_KryptoZeigt24x7()
    {
        var now = DateTime.UtcNow;
        TradingHoursFilter.GetMarketStatusText("BTC-USDT", now).Should().Be("24/7");
    }

    [Fact]
    public void StatusText_GeschlossenerMarktZeigtKategorie()
    {
        var saturday = new DateTime(2026, 4, 11, 12, 0, 0, DateTimeKind.Utc);
        TradingHoursFilter.GetMarketStatusText("NCCOGOLD2USD-USDT", saturday)
            .Should().Contain("geschlossen");
    }
}
