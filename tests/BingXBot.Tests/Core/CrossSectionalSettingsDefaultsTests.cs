using BingXBot.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

// Guard-Test gegen versehentliche Default-Drift der Cross-Sectional-Momentum-Strategie.
// Diese Werte sind backtest-validiert (Fein-Sweep 13.06.2026): in ALLEN 4 Marktphasen positiv bei
// L60 / 9-Tage-Rebalance / risk-adjusted auf dem Top-50-Universum INKL. TradFi mit 3L-3S — robust
// ueber Top-50 UND Top-80 sowie lev1/lev2 (Plateau, kein Peak).
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
        // 2x = bewusste User-Entscheidung (10.06.2026): auf dem Live-Profil in allen 4 Phasen
        // positiv (Σ +548 % vs. +250 % bei 1x); ab 3x kippt Recovery (−12 %), 5x Lotterie (−36 %).
        s.LeverageCap.Should().Be(2, "Hebel-Sweep auf dem Live-Profil: 2x robust, 3x+ phasen-instabil");
        s.IncludeTradFi.Should().BeTrue("ohne TradFi-Dispersion kippt auch Top-50 in keiner Config");
        s.LookbackCandles.Should().Be(60, "L60 ≈ 10 Tage Momentum-Fenster (Fein-Sweep-Optimum 13.06.2026)");
        s.RebalanceDays.Should().Be(9, "9-Tage-Rebalance (R54-Kerzen-Optimum, robust ueber beide Universen)");
    }
}
