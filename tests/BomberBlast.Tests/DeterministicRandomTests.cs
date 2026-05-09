using BomberBlast.Core;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für DeterministicRandom (Phase 18b — AAA-Audit E4).
/// Validiert: Same-Seed-Determinismus, Verteilungs-Plausibilität, State-Roundtrip.
/// </summary>
public class DeterministicRandomTests
{
    [Fact]
    public void SameSeed_LiefertIdentischeSequenz()
    {
        var r1 = new DeterministicRandom(42);
        var r2 = new DeterministicRandom(42);

        for (int i = 0; i < 1000; i++)
        {
            r1.NextUInt64().Should().Be(r2.NextUInt64(),
                $"xoshiro256+ muss Bit-für-Bit identisch sein bei gleichem Seed (Iter {i})");
        }
    }

    [Fact]
    public void DifferentSeeds_LiefernUnterschiedlicheSequenzen()
    {
        var r1 = new DeterministicRandom(42);
        var r2 = new DeterministicRandom(43);

        var s1 = r1.NextUInt64();
        var s2 = r2.NextUInt64();
        s1.Should().NotBe(s2);
    }

    [Fact]
    public void Next_LiefertImmerImBereich()
    {
        var r = new DeterministicRandom(123);
        for (int i = 0; i < 10_000; i++)
        {
            var v = r.Next(100);
            v.Should().BeInRange(0, 99);
        }
    }

    [Fact]
    public void Next_ZeroOrNegativeMax_LiefertZero()
    {
        var r = new DeterministicRandom(0);
        r.Next(0).Should().Be(0);
        r.Next(-5).Should().Be(0);
    }

    [Fact]
    public void NextDouble_LiefertImmerImBereich01()
    {
        var r = new DeterministicRandom(7);
        for (int i = 0; i < 10_000; i++)
        {
            var v = r.NextDouble();
            v.Should().BeGreaterThanOrEqualTo(0.0);
            v.Should().BeLessThan(1.0);
        }
    }

    [Fact]
    public void NextDouble_VerteilungIstUngefaehrUniform()
    {
        var r = new DeterministicRandom(99);
        var bins = new int[10];
        const int samples = 100_000;

        for (int i = 0; i < samples; i++)
        {
            var v = r.NextDouble();
            var bin = (int)(v * 10);
            bins[Math.Min(bin, 9)]++;
        }

        // Jeder Bin sollte ~10% haben (Toleranz ±1%)
        foreach (var count in bins)
        {
            var pct = count * 100.0 / samples;
            pct.Should().BeInRange(8.5, 11.5);
        }
    }

    [Fact]
    public void NextSingle_LiefertFloatImBereich01()
    {
        var r = new DeterministicRandom(5);
        for (int i = 0; i < 1000; i++)
        {
            var v = r.NextSingle();
            v.Should().BeInRange(0f, 0.9999f);
        }
    }

    [Fact]
    public void State_RoundtripFunktioniert()
    {
        var r1 = new DeterministicRandom(42);
        // Erst ein paar Werte verbrauchen
        for (int i = 0; i < 100; i++) r1.NextUInt64();

        var state = r1.GetState();
        var snapshot1 = r1.NextUInt64();

        var r2 = new DeterministicRandom(0);
        r2.SetState(state.s0, state.s1, state.s2, state.s3);
        var snapshot2 = r2.NextUInt64();

        snapshot2.Should().Be(snapshot1, "State-Roundtrip muss exakt fortsetzen");
    }

    [Fact]
    public void Seed_Zero_LiefertGueltigeSequenz()
    {
        // SplitMix64-Seed-Expansion verhindert dass Seed=0 → schwacher State
        var r = new DeterministicRandom(0);
        var v1 = r.NextUInt64();
        var v2 = r.NextUInt64();
        v1.Should().NotBe(0UL);
        v2.Should().NotBe(0UL);
        v1.Should().NotBe(v2);
    }

    [Fact]
    public void NextRange_RespektiertMinMax()
    {
        var r = new DeterministicRandom(1);
        for (int i = 0; i < 1000; i++)
        {
            var v = r.Next(50, 100);
            v.Should().BeInRange(50, 99);
        }
    }
}
