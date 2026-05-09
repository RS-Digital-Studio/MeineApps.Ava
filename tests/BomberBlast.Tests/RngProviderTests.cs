using BomberBlast.Core;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>Tests für IRngProvider-Implementationen (Phase 18d).</summary>
public class RngProviderTests
{
    [Fact]
    public void SystemRng_Next_LiefertImBereich()
    {
        var rng = new SystemRngProvider(seed: 42);
        for (int i = 0; i < 1000; i++)
        {
            rng.Next(100).Should().BeInRange(0, 99);
        }
    }

    [Fact]
    public void SystemRng_NextZeroOrNegative_LiefertZero()
    {
        var rng = new SystemRngProvider();
        rng.Next(0).Should().Be(0);
        rng.Next(-5).Should().Be(0);
    }

    [Fact]
    public void DeterministicRng_SameSeed_LiefertIdentischeSequenz()
    {
        var rng1 = new DeterministicRngProvider(seed: 42);
        var rng2 = new DeterministicRngProvider(seed: 42);

        for (int i = 0; i < 1000; i++)
        {
            rng1.Next(1000).Should().Be(rng2.Next(1000));
        }
    }

    [Fact]
    public void DeterministicRng_StateRoundtrip()
    {
        var rng = new DeterministicRngProvider(seed: 123);
        for (int i = 0; i < 50; i++) rng.Next(100);
        var state = rng.GetState();
        var snapshot = rng.Next(int.MaxValue);

        var rng2 = new DeterministicRngProvider(seed: 999); // anderer Seed
        rng2.SetState(state.Item1, state.Item2, state.Item3, state.Item4);
        rng2.Next(int.MaxValue).Should().Be(snapshot);
    }

    [Fact]
    public void DeterministicRng_NextDouble_ImBereich01()
    {
        var rng = new DeterministicRngProvider(seed: 1);
        for (int i = 0; i < 1000; i++)
        {
            var v = rng.NextDouble();
            v.Should().BeGreaterThanOrEqualTo(0.0);
            v.Should().BeLessThan(1.0);
        }
    }

    [Fact]
    public void NextRange_RespektiertMinMax()
    {
        IRngProvider sys = new SystemRngProvider(seed: 5);
        IRngProvider det = new DeterministicRngProvider(seed: 5);
        for (int i = 0; i < 200; i++)
        {
            sys.Next(50, 100).Should().BeInRange(50, 99);
            det.Next(50, 100).Should().BeInRange(50, 99);
        }
    }
}
