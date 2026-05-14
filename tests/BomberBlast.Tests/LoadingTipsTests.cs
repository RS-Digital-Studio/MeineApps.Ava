using BomberBlast.Loading;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für LoadingTips (Phase 28 — PR4).
/// </summary>
public class LoadingTipsTests
{
    [Fact]
    public void TotalTipCount_HatMindestens30Tipps()
    {
        // AAA-Standard: Genshin Impact hat 200+ Tipps. Indie sollte mind. 30 schaffen.
        LoadingTips.TotalTipCount.Should().BeGreaterThanOrEqualTo(30);
    }

    [Fact]
    public void GetRandomTip_LiefertNonEmptyString()
    {
        for (int i = 0; i < 50; i++)
        {
            var tip = LoadingTips.GetRandomTip();
            tip.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetRandomTip_MitWeltIndex_FunktioniertOhneCrash()
    {
        for (int world = 0; world < 10; world++)
        {
            var tip = LoadingTips.GetRandomTip(world);
            tip.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetRandomTip_VermeidetDirekteWiederholung()
    {
        // Über viele Picks sollte keine direkte Wiederholung entstehen.
        // Sehr großer Pool → Anti-Repeat-Garantie ist statistisch fast immer erfüllt.
        string? prev = null;
        int matches = 0;
        for (int i = 0; i < 200; i++)
        {
            var tip = LoadingTips.GetRandomTip();
            if (tip == prev) matches++;
            prev = tip;
        }
        // Bei 33+ Pool-Größe und Anti-Repeat-Logik sollten 0 direkte Wiederholungen entstehen.
        matches.Should().Be(0, "Anti-Repeat-Logik muss alle direkten Wiederholungen verhindern");
    }
}
