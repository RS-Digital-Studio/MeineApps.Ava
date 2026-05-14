using BomberBlast.Core.Audio;
using BomberBlast.Graphics;
using BomberBlast.Models.League;
using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Property-Based-Style-Tests (Phase 26 — T1).
///
/// <para>Alternative zu FsCheck: Manuell randomisierte Test-Loops mit hoher Iteration-Count
/// (1.000–10.000). Validiert mathematische Invarianten der Pure-Logic-Klassen.</para>
///
/// <para>Convention: <c>property_</c>-Prefix für Property-Tests. Iteration-Count 1.000 als Default
/// (schnell genug fürs CI, aber breit genug um Edge-Cases zu fangen).</para>
/// </summary>
public class PropertyBasedTests
{
    private const int DefaultIterations = 1000;

    [Fact]
    public void property_ScreenShakeTrauma_BleibtImmerImBereich01()
    {
        var rng = new Random(42); // Deterministisch für CI-Reproduzierbarkeit
        var shake = new ScreenShake();
        var traumaField = typeof(ScreenShake).GetField("_trauma",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        for (int iter = 0; iter < DefaultIterations; iter++)
        {
            // Zufällige Trigger-Sequenz
            var amount = (float)rng.NextDouble() * 1.5f - 0.25f; // [-0.25, 1.25] → testet auch Negativ-Werte
            shake.AddTrauma(amount);
            shake.Update((float)rng.NextDouble() * 0.05f);

            var trauma = (float)traumaField!.GetValue(shake)!;
            trauma.Should().BeGreaterThanOrEqualTo(0f, $"Iter {iter}: Trauma muss >= 0 sein");
            trauma.Should().BeLessThanOrEqualTo(1f, $"Iter {iter}: Trauma muss <= 1 sein");
        }
    }

    [Fact]
    public void property_AudioSpatialPan_LiefertImmerImBereichMinus1Plus1()
    {
        var rng = new Random(42);
        for (int iter = 0; iter < DefaultIterations; iter++)
        {
            var soundX = rng.Next(-100, 100);
            var playerX = rng.Next(-100, 100);
            var gridW = rng.Next(0, 30); // 0 → Edge-Case (kein Crash, return 0)

            var pan = AudioSpatial.CalculatePan(soundX, playerX, gridW);

            pan.Should().BeGreaterThanOrEqualTo(-1f);
            pan.Should().BeLessThanOrEqualTo(1f);
        }
    }

    [Fact]
    public void property_AudioSpatialDistanceVolume_LiefertImmerImBereich01()
    {
        var rng = new Random(42);
        for (int iter = 0; iter < DefaultIterations; iter++)
        {
            var sx = rng.Next(0, 30);
            var sy = rng.Next(0, 30);
            var px = rng.Next(0, 30);
            var py = rng.Next(0, 30);
            var fullR = rng.Next(0, 8);
            var silenceR = rng.Next(fullR + 1, 20);

            var vol = AudioSpatial.CalculateDistanceVolume(sx, sy, px, py, fullR, silenceR);

            vol.Should().BeGreaterThanOrEqualTo(0f);
            vol.Should().BeLessThanOrEqualTo(1f);
        }
    }

    [Fact]
    public void property_AudioSpatialDistanceVolume_MonotonInDistanz()
    {
        // Eigenschaft: Bei steigender Distanz darf Volume NICHT steigen
        var rng = new Random(42);
        for (int iter = 0; iter < 200; iter++)
        {
            var px = rng.Next(0, 20);
            var py = rng.Next(0, 20);
            var fullR = rng.Next(0, 5);
            var silenceR = rng.Next(fullR + 5, 15);

            float prevVol = float.MaxValue;
            // Spirale aus dem Player rauslaufen — Distanz wächst monoton
            for (int d = 0; d < silenceR + 5; d++)
            {
                var vol = AudioSpatial.CalculateDistanceVolume(px + d, py, px, py, fullR, silenceR);
                vol.Should().BeLessThanOrEqualTo(prevVol + 0.001f, // Epsilon für FP
                    $"Volume darf nicht steigen wenn Distanz wächst (d={d})");
                prevVol = vol;
            }
        }
    }

    [Fact]
    public void property_EqualPowerCrossfade_SummeQuadratIstEins()
    {
        var rng = new Random(42);
        for (int iter = 0; iter < DefaultIterations; iter++)
        {
            var t = (float)rng.NextDouble();
            var (oldVol, newVol) = AudioSpatial.EqualPowerCrossfade(t);

            // Equal-Power-Garantie: oldVol² + newVol² ≈ 1 (sin²+cos²=1)
            var sum = oldVol * oldVol + newVol * newVol;
            sum.Should().BeApproximately(1f, 0.001f);
        }
    }

    [Fact]
    public void property_LeagueSubTier_RoundtripIstKonsistent()
    {
        // Eigenschaft: GetSubTier(GetSubTierThreshold(t, sub)) == sub für alle gültigen Tier+Sub
        foreach (var tier in new[] { LeagueTier.Bronze, LeagueTier.Silver, LeagueTier.Gold, LeagueTier.Platinum })
        {
            foreach (var sub in new[] { LeagueSubTier.I, LeagueSubTier.II, LeagueSubTier.III })
            {
                var threshold = tier.GetSubTierThreshold(sub);
                var subRecovered = tier.GetSubTier(threshold);
                subRecovered.Should().Be(sub, $"Tier={tier} Sub={sub} Threshold={threshold}");
            }
        }
    }

    [Fact]
    public void property_LeagueSubTier_OberhalbDerCeilingIstNaechsterSubTier()
    {
        // Eigenschaft: 1 Punkt unter Ceiling → aktueller Sub. Ceiling selbst → nächster Sub.
        foreach (var tier in new[] { LeagueTier.Bronze, LeagueTier.Silver, LeagueTier.Gold, LeagueTier.Platinum })
        {
            // I → II Übergang
            var ceilingI = tier.GetSubTierCeiling(LeagueSubTier.I);
            tier.GetSubTier(ceilingI - 1).Should().Be(LeagueSubTier.I);
            tier.GetSubTier(ceilingI).Should().Be(LeagueSubTier.II);
        }
    }

    [Fact]
    public void property_EventCalendar_DeterministischUndStabil()
    {
        var svc1 = new EventCalendarService();
        var svc2 = new EventCalendarService();
        var rng = new Random(42);

        // 100 zufällige Wochen — beide Service-Instanzen müssen identisches Event liefern
        for (int iter = 0; iter < 100; iter++)
        {
            var year = 2020 + rng.Next(0, 30);
            var week = rng.Next(1, 53);

            var ev1 = svc1.GetEventForWeek(year, week);
            var ev2 = svc2.GetEventForWeek(year, week);

            ev1.Type.Should().Be(ev2.Type, $"Year={year} Week={week} muss deterministisch sein");
            ev1.Multiplier.Should().Be(ev2.Multiplier);
        }
    }

    [Fact]
    public void property_SoundVariationPool_LiefertImmerKeyAusPool()
    {
        var pool = new SoundVariationPool();
        pool.RegisterPool("test", "a", "b", "c", "d");
        var validKeys = new HashSet<string> { "test_a", "test_b", "test_c", "test_d" };

        for (int iter = 0; iter < DefaultIterations; iter++)
        {
            var picked = pool.PickVariant("test");
            validKeys.Should().Contain(picked, $"Iter {iter}: Pick muss aus Pool kommen");
        }
    }

    [Fact]
    public void property_HardwareProfile_ParticleCapNiemalsAufNullOderNegativ()
    {
        var prefs = new InMemoryPreferences();
        var svc = new HardwareProfileService(prefs);

        foreach (HardwareTier tier in Enum.GetValues<HardwareTier>())
        {
            svc.SetUserOverride(tier);

            // Auch mit Battery + Thermal kombiniert: Cap muss > 0 bleiben
            svc.BatterySaveActive = true;
            svc.ThermalThrottleActive = true;
            svc.GetMaxParticles().Should().BeGreaterThan(0, $"Tier {tier} mit Battery+Thermal");

            svc.BatterySaveActive = false;
            svc.ThermalThrottleActive = false;
            svc.GetMaxParticles().Should().BeGreaterThanOrEqualTo(300, $"Tier {tier} normal");
        }
    }

    [Fact]
    public void property_LuckySpinPity_GarantiertJackpotInnerhalbVon51Spins()
    {
        // Iteration 50× — bei jedem Versuch sollte der Pity-Counter spätestens nach 51 Spins
        // einen Jackpot liefern, EGAL wie das Random-Glück fällt.
        for (int run = 0; run < 50; run++)
        {
            var prefs = new InMemoryPreferences();
            var svc = new LuckySpinService(prefs);
            var jackpotIdx = svc.GetRewards().Single(r => r.IsJackpot).Index;

            bool hit = false;
            for (int s = 0; s < 51; s++)
            {
                if (svc.Spin() == jackpotIdx)
                {
                    hit = true;
                    break;
                }
            }
            hit.Should().BeTrue($"Run {run}: Pity muss spätestens bei Spin 51 garantieren");
        }
    }
}
