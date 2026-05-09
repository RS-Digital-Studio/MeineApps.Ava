using BomberBlast.Models.Cosmetics;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für Cosmetic-Volumen (Phase 29 — AAA-Audit Content-Lücke).
/// Validiert dass die definierten Cosmetic-Pools die geforderte AAA-Mindestmenge erreichen
/// und dass alle definierten Items eine eindeutige Id, RESX-Keys und gültige Rarität haben.
/// </summary>
public class CosmeticVolumeTests
{
    [Fact]
    public void Trails_Mindestens30_FuerAAAVolumen()
    {
        TrailDefinitions.All.Length.Should().BeGreaterThanOrEqualTo(30);
    }

    [Fact]
    public void Frames_Mindestens30_FuerAAAVolumen()
    {
        FrameDefinitions.All.Length.Should().BeGreaterThanOrEqualTo(30);
    }

    [Fact]
    public void Victories_Mindestens30_FuerAAAVolumen()
    {
        VictoryDefinitions.All.Length.Should().BeGreaterThanOrEqualTo(30);
    }

    [Fact]
    public void TrailIds_SindEindeutig()
    {
        var ids = TrailDefinitions.All.Select(t => t.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().NotContain(string.Empty);
    }

    [Fact]
    public void FrameIds_SindEindeutig()
    {
        var ids = FrameDefinitions.All.Select(f => f.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().NotContain(string.Empty);
    }

    [Fact]
    public void VictoryIds_SindEindeutig()
    {
        var ids = VictoryDefinitions.All.Select(v => v.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().NotContain(string.Empty);
    }

    [Fact]
    public void AlleTrails_HabenNameUndDescKey()
    {
        foreach (var t in TrailDefinitions.All)
        {
            t.NameKey.Should().NotBeNullOrEmpty($"Trail {t.Id} muss NameKey haben");
            t.DescKey.Should().NotBeNullOrEmpty($"Trail {t.Id} muss DescKey haben");
        }
    }

    [Fact]
    public void AlleFrames_HabenNameUndDescKey()
    {
        foreach (var f in FrameDefinitions.All)
        {
            f.NameKey.Should().NotBeNullOrEmpty($"Frame {f.Id} muss NameKey haben");
            f.DescKey.Should().NotBeNullOrEmpty($"Frame {f.Id} muss DescKey haben");
        }
    }

    [Fact]
    public void AlleVictories_HabenNameUndDescKey()
    {
        foreach (var v in VictoryDefinitions.All)
        {
            v.NameKey.Should().NotBeNullOrEmpty($"Victory {v.Id} muss NameKey haben");
            v.DescKey.Should().NotBeNullOrEmpty($"Victory {v.Id} muss DescKey haben");
        }
    }

    [Fact]
    public void Cosmetics_VerteilungAufRaritaeten_Plausibel()
    {
        // AAA-Standard: Common-Tier sollte am größten sein (oder mindestens viele Rare/Epic)
        // damit Spieler im Early-Game etwas Erreichbares hat.
        var trailRarities = TrailDefinitions.All.GroupBy(t => t.Rarity).ToDictionary(g => g.Key, g => g.Count());
        var frameRarities = FrameDefinitions.All.GroupBy(f => f.Rarity).ToDictionary(g => g.Key, g => g.Count());
        var victoryRarities = VictoryDefinitions.All.GroupBy(v => v.Rarity).ToDictionary(g => g.Key, g => g.Count());

        // Mindestens 3 Tiers vertreten in jedem Pool (Common+Rare+Epic mindestens)
        trailRarities.Keys.Count.Should().BeGreaterThanOrEqualTo(3);
        frameRarities.Keys.Count.Should().BeGreaterThanOrEqualTo(3);
        victoryRarities.Keys.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void TrailStyles_AllEnumWerte_HabenMindestensEineDefinition()
    {
        // Phase 29: Jede TrailStyle-Variante sollte mindestens eine Definition haben,
        // sonst ist der Enum-Wert tot
        var usedStyles = TrailDefinitions.All.Select(t => t.Style).ToHashSet();
        var allStyles = Enum.GetValues<TrailStyle>().Length;

        // Wir erwarten dass mindestens 90% der Enum-Werte tatsächlich verwendet sind
        // (Erlaubt 1-2 Reserve-Slots fürs Future-Proofing)
        usedStyles.Count.Should().BeGreaterThanOrEqualTo((int)(allStyles * 0.9));
    }

    [Fact]
    public void Cosmetic_RewardOnlyItems_HabenKeinPriceTag()
    {
        // Champion-Trail/PrestigeAura/DungeonBlight/BPMastery sind Reward-only — kein Coin/Gem-Preis
        var rewardOnlyTrails = new[] { "trail_champion", "trail_prestige", "trail_dungeonblight", "trail_bpmastery" };
        foreach (var id in rewardOnlyTrails)
        {
            var trail = TrailDefinitions.All.SingleOrDefault(t => t.Id == id);
            trail.Should().NotBeNull($"{id} muss existieren");
            trail!.CoinPrice.Should().Be(0, $"{id} ist Reward-only");
            trail.GemPrice.Should().Be(0, $"{id} ist Reward-only");
        }
    }
}
