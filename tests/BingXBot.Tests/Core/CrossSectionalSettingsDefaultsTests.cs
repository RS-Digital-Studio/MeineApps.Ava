using BingXBot.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

// Guard-Test gegen versehentliche Default-Drift der Cross-Sectional-Momentum-Strategie.
// Diese Werte sind backtest-validiert: in ALLEN 4 Marktphasen positiv bei L60 / ~woechentlichem
// Rebalance / risk-adjusted auf dem Top-50-Universum INKL. TradFi mit 3L-3S (min +28,3 %).
// Eine unbeabsichtigte Aenderung (z.B. LeverageCap auf 5, Top-100 oder zurueck auf L120/R21 =
// nur 2/4 Phasen, min −50,6 %) macht die Strategie phasen-instabil — der Test schlaegt dann
// bewusst fehl und zwingt zur Re-Validierung.
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
        s.LookbackCandles.Should().Be(60, "L60 ≈ 10 Tage — Top-50/154 USDT min +28,3 % vs. L120 min −50,6 % (nur 2/4)");
        s.RebalanceDays.Should().Be(9, "≈ woechentlicher Rebalance (Momentum-Literatur, validiert)");
    }
}
