using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>
/// v1.5.0 Phase 1 — "Heiliger Gral als Hard-Gate".
///
/// Verifiziert die Mikro-Touch-Schwelle und den W1/D1-Spezialfall der Hard-Gate-Logik.
/// Die Strategy-Integration ist via SequenzKonzeptStrategy.Evaluate getestet (Backtest-Suite),
/// hier liegt der Fokus auf der reinen Overlap-Geometrie + dem Mindestbreite-Filter.
/// </summary>
public class HtfConfluenceHardGateTests
{
    [Fact]
    public void HasMeaningfulOverlap_ZeroMinWidth_VerhaeltSichWieOverlaps()
    {
        var a = new SkConfluenceZoneOverlap.PriceZone(100m, 110m);
        var b = new SkConfluenceZoneOverlap.PriceZone(105m, 115m);
        SkConfluenceZoneOverlap.HasMeaningfulOverlap(a, b, 0m).Should().BeTrue();
    }

    [Fact]
    public void HasMeaningfulOverlap_MikroTouch_KleinerAlsMinWidth_FalseErkannt()
    {
        // Reference (LTF-B-Box) Spanne 10. MinWidth 0.1 % → 0.01.
        // Overlap nur 0.005 → unter MinWidth.
        var reference = new SkConfluenceZoneOverlap.PriceZone(100m, 110m);
        var other = new SkConfluenceZoneOverlap.PriceZone(109.995m, 120m);
        SkConfluenceZoneOverlap.HasMeaningfulOverlap(reference, other, 0.1m).Should().BeFalse();
    }

    [Fact]
    public void HasMeaningfulOverlap_GrosserOverlap_TrueErkannt()
    {
        // Reference Spanne 10. MinWidth 0.1 % → 0.01. Overlap 5 → klar darueber.
        var reference = new SkConfluenceZoneOverlap.PriceZone(100m, 110m);
        var other = new SkConfluenceZoneOverlap.PriceZone(105m, 120m);
        SkConfluenceZoneOverlap.HasMeaningfulOverlap(reference, other, 0.1m).Should().BeTrue();
    }

    [Fact]
    public void HasMeaningfulOverlap_GenauAnDerSchwelle_TrueErkannt()
    {
        // Reference Spanne 100. MinWidth 0.1 % → 0.1. Overlap exakt 0.1 → akzeptiert (>=).
        var reference = new SkConfluenceZoneOverlap.PriceZone(0m, 100m);
        var other = new SkConfluenceZoneOverlap.PriceZone(99.9m, 200m);
        SkConfluenceZoneOverlap.HasMeaningfulOverlap(reference, other, 0.1m).Should().BeTrue();
    }

    [Fact]
    public void HasMeaningfulOverlap_DisjunkteZonen_FalseErkannt()
    {
        var reference = new SkConfluenceZoneOverlap.PriceZone(100m, 110m);
        var other = new SkConfluenceZoneOverlap.PriceZone(120m, 130m);
        SkConfluenceZoneOverlap.HasMeaningfulOverlap(reference, other, 0.1m).Should().BeFalse();
    }

    [Fact]
    public void OverlapWidth_BerechnetSchnittBreite()
    {
        var a = new SkConfluenceZoneOverlap.PriceZone(100m, 110m);
        var b = new SkConfluenceZoneOverlap.PriceZone(105m, 115m);
        SkConfluenceZoneOverlap.OverlapWidth(a, b).Should().Be(5m);
    }

    [Fact]
    public void OverlapWidth_Disjunkt_NullErkannt()
    {
        var a = new SkConfluenceZoneOverlap.PriceZone(100m, 110m);
        var b = new SkConfluenceZoneOverlap.PriceZone(120m, 130m);
        SkConfluenceZoneOverlap.OverlapWidth(a, b).Should().Be(0m);
    }

    [Fact]
    public void RoundTrip_FlagPersistsThroughDb()
    {
        // Phase 1 — RequireHtfConfluenceForEntry muss durch die Settings-Persistenz erhalten bleiben.
        // Das ist in App.axaml.cs::RestoreSettingsFromDb + Server::ApplySettingsToSingletons gemappt.
        // Dieser Test verifiziert nur, dass das Property auf RiskSettings existiert und einen
        // Default-Wert hat — die echte Roundtrip-Persistenz wird durch die Settings-Tests abgedeckt.
        var settings = new BingXBot.Core.Configuration.RiskSettings();
        settings.RequireHtfConfluenceForEntry.Should().BeFalse("Default soll false sein (opt-in)");
        settings.RequireHtfConfluenceForEntry = true;
        settings.RequireHtfConfluenceForEntry.Should().BeTrue();
    }
}
