using BomberBlast.Models.BattlePass;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für BattlePass-Saison-Theme-System (Phase 19 — AAA-Audit L1).
/// Validiert Theme-Rotation, Akzent-Farben, deterministische Saison-Zuordnung.
/// </summary>
public class BattlePassThemeTests
{
    [Fact]
    public void Saison1_IstClassicTheme()
    {
        BattlePassThemeExtensions.GetThemeForSeason(1).Should().Be(BattlePassTheme.Classic);
        BattlePassThemeExtensions.GetThemeForSeason(0).Should().Be(BattlePassTheme.Classic);
        BattlePassThemeExtensions.GetThemeForSeason(-5).Should().Be(BattlePassTheme.Classic);
    }

    [Theory]
    [InlineData(2, BattlePassTheme.Cyberpunk)]
    [InlineData(3, BattlePassTheme.Halloween)]
    [InlineData(4, BattlePassTheme.Winter)]
    [InlineData(5, BattlePassTheme.Summer)]
    [InlineData(6, BattlePassTheme.Mech)]
    [InlineData(7, BattlePassTheme.Underwater)]
    [InlineData(8, BattlePassTheme.Sengoku)]
    [InlineData(9, BattlePassTheme.DiaDeLosMuertos)]
    [InlineData(10, BattlePassTheme.Steampunk)]
    public void Saison2BisN_RotiertDurchThemes(int season, BattlePassTheme expected)
    {
        BattlePassThemeExtensions.GetThemeForSeason(season).Should().Be(expected);
    }

    [Fact]
    public void RotationWiederholt_NachAblauf()
    {
        // 9 Themes ausser Classic. Index = (season - 2) % 9.
        // Saison 11 → idx 0 → Cyberpunk, Saison 19 → idx 8 → Steampunk, Saison 20 → idx 0 wieder
        BattlePassThemeExtensions.GetThemeForSeason(11).Should().Be(BattlePassTheme.Cyberpunk);
        BattlePassThemeExtensions.GetThemeForSeason(19).Should().Be(BattlePassTheme.Steampunk);
        BattlePassThemeExtensions.GetThemeForSeason(20).Should().Be(BattlePassTheme.Cyberpunk);
    }

    [Fact]
    public void GetAccentColorHex_LiefertGueltigesHex()
    {
        foreach (BattlePassTheme theme in Enum.GetValues<BattlePassTheme>())
        {
            var hex = theme.GetAccentColorHex();
            hex.Should().StartWith("#");
            hex.Length.Should().Be(7); // #RRGGBB
        }
    }

    [Fact]
    public void GetSecondaryColorHex_LiefertGueltigesHex()
    {
        foreach (BattlePassTheme theme in Enum.GetValues<BattlePassTheme>())
        {
            var hex = theme.GetSecondaryColorHex();
            hex.Should().StartWith("#");
            hex.Length.Should().Be(7);
        }
    }

    [Fact]
    public void GetNameKey_FolgtRESXKonvention()
    {
        BattlePassTheme.Cyberpunk.GetNameKey().Should().Be("BattlePassTheme_Cyberpunk");
        BattlePassTheme.Halloween.GetLoreKey().Should().Be("BattlePassTheme_Halloween_Lore");
    }

    [Fact]
    public void BattlePassData_Theme_LiefertSaisonsTheme()
    {
        var data = new BattlePassData { SeasonNumber = 3 };
        data.Theme.Should().Be(BattlePassTheme.Halloween);

        data.SeasonNumber = 1;
        data.Theme.Should().Be(BattlePassTheme.Classic);
    }

    [Fact]
    public void GetIconHint_LiefertNonEmptyString()
    {
        foreach (BattlePassTheme theme in Enum.GetValues<BattlePassTheme>())
        {
            var hint = theme.GetIconHint();
            hint.Should().NotBeNullOrEmpty();
        }
    }
}
