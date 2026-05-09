using BomberBlast.Core.Audio;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für SoundVariationPool (Phase 16). Validiert Pool-Registrierung, Anti-Repeat-Logik,
/// Fallback-Verhalten bei unbekannten Keys und Pool-Aufzählung für Preload.
/// </summary>
public class SoundVariationPoolTests
{
    [Fact]
    public void Unregistrierter_Key_GibtBasisKeyZurueck()
    {
        var pool = new SoundVariationPool();
        pool.PickVariant("explosion").Should().Be("explosion");
    }

    [Fact]
    public void RegisterPool_OhneSuffixe_ZaehltAlsBasisKeyOnly()
    {
        var pool = new SoundVariationPool();
        pool.RegisterPool("explosion");
        // Mit "leerem Pool" wird der Basis-Key registriert (Pool-Größe 1)
        pool.GetPoolSize("explosion").Should().Be(1);
        pool.PickVariant("explosion").Should().Be("explosion");
    }

    [Fact]
    public void RegisterPool_MitSuffixen_BautKeysKorrekt()
    {
        var pool = new SoundVariationPool();
        pool.RegisterPool("place_bomb", "a", "b", "c");

        var keys = pool.EnumerateAllVariantKeys().ToList();
        keys.Should().Contain(new[] { "place_bomb_a", "place_bomb_b", "place_bomb_c" });
        pool.GetPoolSize("place_bomb").Should().Be(3);
    }

    [Fact]
    public void PickVariant_WaehltAusPool()
    {
        var pool = new SoundVariationPool();
        pool.RegisterPool("explosion", "a", "b", "c");

        for (int i = 0; i < 50; i++)
        {
            var picked = pool.PickVariant("explosion");
            picked.Should().BeOneOf("explosion_a", "explosion_b", "explosion_c");
        }
    }

    [Fact]
    public void PickVariant_VerhindertDirekteWiederholung()
    {
        var pool = new SoundVariationPool();
        pool.RegisterPool("explosion", "a", "b", "c", "d");

        // 100 Picks: keine zwei direkt gleiche
        string? prev = null;
        for (int i = 0; i < 100; i++)
        {
            var picked = pool.PickVariant("explosion");
            if (prev != null)
                picked.Should().NotBe(prev, "direkte Wiederholung muss vermieden werden");
            prev = picked;
        }
    }

    [Fact]
    public void PickVariant_PoolMitEinemElement_GibtImmerDiesesZurueck()
    {
        var pool = new SoundVariationPool();
        pool.RegisterPool("victory", "a");

        for (int i = 0; i < 20; i++)
            pool.PickVariant("victory").Should().Be("victory_a");
    }

    [Fact]
    public void EnumerateAllVariantKeys_LiefertAlleRegistriertenKeysFuerPreload()
    {
        var pool = new SoundVariationPool();
        pool.RegisterPool("place_bomb", "a", "b");
        pool.RegisterPool("explosion", "a", "b", "c");
        pool.RegisterPool("powerup", "a");

        var keys = pool.EnumerateAllVariantKeys().ToList();
        keys.Should().HaveCount(2 + 3 + 1);
    }
}
