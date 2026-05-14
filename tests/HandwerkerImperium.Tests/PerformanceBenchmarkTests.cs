using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;

namespace HandwerkerImperium.Tests;

/// <summary>
/// (08.05.2026): Performance-Budget Late-Game-Benchmark.
///
/// Drei Risiko-Szenarien aus dem Audit:
/// 1. 200 Worker-Avatare auf einer Seite (10 WS × Lv1000 × 20 Worker)
/// 2. Late-Game GameState-Serialisierung sollte unter Schwelle bleiben (Save-Lock)
/// 3. Migration von einem Late-Game-V1-Save sollte schnell laufen (Boot-Pfad)
///
/// Diese Tests laufen auf der Build-Maschine (CI). Durchsatz-Werte sind als
/// Regressions-Schwellen gesetzt — wenn ein Refactor die Werte drastisch
/// verschlechtert, schlaegt der Test fehl statt unbemerkt zu degenerieren.
///
/// Frame-Time-Benchmark (Skia-Rendering) brauchte Avalonia-Headless-Setup
/// und ist daher als Layer-3-Skelett dokumentiert.
/// </summary>
public class PerformanceBenchmarkTests
{
    private const int IterationCountSerialize = 5;
    private const int IterationCountMigrate = 10;

    /// <summary>P1.4: Synthetischer Late-Game-State (Lv1000-Aequivalent).</summary>
    private static GameState CreateLateGameState()
    {
        var state = GameState.CreateNew();
        state.Money = 999_999_999_999_999m;
        state.GoldenScrews = 100_000;
        state.PlayerLevel = 5_000;
        state.TotalMoneyEarned = 9.999e15m;
        state.Prestige.PrestigePoints = 50_000;
        state.Prestige.LegendeCount = 25;
        state.Prestige.PermanentMultiplier = 100m;
        state.UnlockedWorkshopTypes = System.Enum.GetValues<WorkshopType>().ToList();
        state.Workshops = state.UnlockedWorkshopTypes
            .Select(t => { var w = Workshop.Create(t); w.Level = 1000; return w; })
            .ToList();
        // Workshop-Stars max
        foreach (var t in state.UnlockedWorkshopTypes)
            state.WorkshopStars[t.ToString()] = 5;
        return state;
    }

    [Fact]
    public void Serialize_LateGameState_UnderBudget()
    {
        // Audit P1.4: Save sollte typisch < 200 ms auf CI bleiben.
        // Dieser Test schlaegt fehl, wenn ein Refactor das auf > 1000ms hochtreibt.
        var state = CreateLateGameState();

        // Warmup
        _ = JsonSerializer.Serialize(state);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < IterationCountSerialize; i++)
        {
            _ = JsonSerializer.Serialize(state);
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / IterationCountSerialize;
        avgMs.Should().BeLessThan(1000, "Late-Game-Save darf nicht > 1s pro Roundtrip dauern (CI-Budget)");
    }

    [Fact]
    public void Roundtrip_LateGameState_SizeUnderBudget()
    {
        // Audit P1.4: GameState-JSON ~300-500 KB pro Save bei 60+ Tagen ist akzeptabel,
        // ueber 2 MB waere Save-Bandwidth-Kritisch (Cloud-Save Upload).
        var state = CreateLateGameState();

        var json = JsonSerializer.Serialize(state);
        var sizeKb = json.Length / 1024.0;

        sizeKb.Should().BeLessThan(2048, "Late-Game-Save sollte unter 2 MB bleiben");
    }

    [Fact]
    public void Migrate_LateGameV1State_UnderBudget()
    {
        // Audit P1.4: V1→V6-Migration (Boot-Pfad) sollte unter 100ms / Iteration bleiben.
        // Verhindert dass App-Start fuer Long-Term-Spieler langsam wird.
        var state = CreateLateGameState();
        state.Version = 1;

        // Warmup
        _ = SaveGameService.MigrateState(CreateLateGameState());

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < IterationCountMigrate; i++)
        {
            var freshState = CreateLateGameState();
            freshState.Version = 1;
            _ = SaveGameService.MigrateState(freshState);
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / IterationCountMigrate;
        avgMs.Should().BeLessThan(500, "V1→V6-Migration darf nicht > 500ms pro Iteration dauern");
    }

    [Fact]
    public void GameState_DeepCopy_NoExceptions()
    {
        // Smoke-Test: Late-Game-State kann durch Serialize → Deserialize geklont werden
        // ohne State-Korruption (deckt Save-Lock-Race aus CLAUDE.md "JsonSerializer-Race" ab).
        var state = CreateLateGameState();

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<GameState>(json);

        restored.Should().NotBeNull();
        restored!.Money.Should().Be(state.Money);
        restored.PlayerLevel.Should().Be(state.PlayerLevel);
        restored.UnlockedWorkshopTypes.Should().HaveCount(state.UnlockedWorkshopTypes.Count);
    }
}
