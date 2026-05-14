using System.Diagnostics;
using BomberBlast.Core.Audio;
using BomberBlast.Core.Combat;
using BomberBlast.Graphics;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Performance-Smoke-Tests (Phase 26 — T3).
///
/// <para>BenchmarkDotNet wäre ideal, ist aber Multi-Project-Setup mit eigenem CI-Runner.
/// Smoke-Tests sind ein einfacher Stopwatch-basierter Schwellwert-Check pro Hot-Path:
/// Wenn die Operation X im Erwartungsfenster bleibt, sind keine Performance-Regressions
/// in den Pure-Logic-Klassen entstanden.</para>
///
/// <para>Schwellwerte sind großzügig (×3-5 vom realistischen Idle-CI-Wert) damit auf
/// langsamen CI-Runnern keine False-Positives entstehen. Bei deutlicher Verschlechterung
/// (Faktor &gt;10) schlägt der Test an.</para>
/// </summary>
public class PerformanceSmokeTests
{
    [Fact]
    public void perf_ComboSystem_10000Kills_UnterEinerSekunde()
    {
        var combo = new ComboSystem();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
        {
            combo.RegisterKill();
            combo.Update(0.001f);
        }
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(1000,
            $"10k ComboSystem-Operationen sollten unter 1s liegen, waren {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void perf_ScreenShake_60Sekunden_60FpsSimulation()
    {
        var shake = new ScreenShake();
        // 60s × 60fps = 3600 Update-Calls + alle 5 Frames ein Trigger
        var sw = Stopwatch.StartNew();
        for (int frame = 0; frame < 3600; frame++)
        {
            if (frame % 5 == 0) shake.AddTrauma(0.1f);
            shake.Update(0.0167f);
        }
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(500,
            "60s ScreenShake-Simulation sollte unter 500ms laufen");
    }

    [Fact]
    public void perf_AudioBusMixer_10000VolumeChanges_UnterEinerSekunde()
    {
        var prefs = new InMemoryPreferences();
        var mixer = new AudioBusMixer(prefs);
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 10_000; i++)
        {
            mixer.SetBusVolume(AudioBus.Sfx, (i % 100) / 100f);
            mixer.GetEffectiveVolume(AudioBus.Sfx, 1f);
        }
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public void perf_SoundPool_10000Picks_UnterEinerSekunde()
    {
        var pool = new SoundVariationPool();
        pool.RegisterPool("explosion", "a", "b", "c", "d");
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 10_000; i++)
        {
            pool.PickVariant("explosion");
        }
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public void perf_ParticleSystem_1000Emissions_UnterEinerSekunde()
    {
        var ps = new ParticleSystem();
        var sw = Stopwatch.StartNew();
        // Emit-Loop simuliert ULTRA-Combo-Slow-Motion (viele Burst-Emissions)
        for (int i = 0; i < 1000; i++)
        {
            ps.Emit(100, 100, 5, new SkiaSharp.SKColor(255, 100, 50));
            ps.Update(0.0167f);
        }
        sw.Stop();
        ps.Dispose();

        sw.ElapsedMilliseconds.Should().BeLessThan(1500,
            "1000 ParticleSystem-Cycles sollten unter 1.5s liegen");
    }

    [Fact]
    public void perf_AudioSpatialPan_100000Calls_UnterEinerSekunde()
    {
        var sw = Stopwatch.StartNew();
        float total = 0;
        for (int i = 0; i < 100_000; i++)
        {
            total += AudioSpatial.CalculatePan(i % 15, i / 15 % 10, 15);
        }
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(1000,
            "100k AudioSpatial.CalculatePan-Calls sollten unter 1s liegen");
        // Sicherstellen dass JIT die Loop nicht weg-optimiert
        total.Should().NotBe(float.NaN);
    }
}
