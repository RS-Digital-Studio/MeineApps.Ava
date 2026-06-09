using BingXBot.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

// Guard-Test gegen versehentliche Default-Drift der Cross-Sectional-Momentum-Strategie.
// Diese Werte sind backtest-validiert: in ALLEN 4 Marktphasen positiv bei L120 / ~monatlichem
// Rebalance / risk-adjusted / 1× Leverage auf dem Top-50-Universum INKL. TradFi mit 3L-3S.
// Eine unbeabsichtigte Aenderung (z.B. LeverageCap auf 5 oder Top-100) macht die Strategie
// phasen-instabil — der Test schlaegt dann bewusst fehl und zwingt zur Re-Validierung.
public class CrossSectionalSettingsDefaultsTests
{
    [Fact]
    public void Defaults_EntsprechenBacktestValidiertemProfil()
    {
        var s = new CrossSectionalSettings();

        s.LongK.Should().Be(3, "3L-3S ist das phasen-robusteste Profil auf Top-50");
        s.ShortK.Should().Be(3);
        s.UniverseTopN.Should().Be(50, "Top-100 verwaessert das Ranking (keine phasen-robuste Config)");
        s.LeverageCap.Should().Be(1, "lev5 macht die Strategie im Bear-Markt zur Lotterie (-81 %)");
        s.IncludeTradFi.Should().BeTrue("ohne TradFi-Dispersion kippt auch Top-50 in keiner Config");
        s.LookbackCandles.Should().Be(120, "L120 ≈ 20 Tage Momentum-Fenster (validiert)");
        s.RebalanceDays.Should().Be(21, "≈ monatlicher Rebalance");
    }
}
