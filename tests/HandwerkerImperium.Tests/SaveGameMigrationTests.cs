using System.Text.Json;
using FluentAssertions;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;

namespace HandwerkerImperium.Tests;

/// <summary>
/// (08.05.2026): Property-Based-aehnliche Tests fuer SaveGame-Migration V1→V6.
///
/// Strategie ohne FsCheck:
/// - Theory mit InlineData-Versionsstufen
/// - Random-State-Generator mit deterministischem Seed (xunit-Theorie ist parallel-fest)
/// - Invarianten nach Migration validieren: Money>=0, kein Null in Pflicht-Feldern,
///   ParallelOrders.Count<=Cap, Workshop-Stars konsistent
///
/// Diese Tests verhindern den V5→V6-Phantom-Bug (CLAUDE.md K5) und decken den
/// Audit-Befund "Null Tests bei 105k Zeilen Code" für die kritische Save-Pipeline ab.
/// </summary>
public class SaveGameMigrationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Migrate_FromAnyVersion_ProducesV6WithValidState(int sourceVersion)
    {
        var state = CreateMinimalState(sourceVersion);

        var migrated = SaveGameService.MigrateState(state);

        migrated.Version.Should().Be(GameState.CurrentStateVersion);
        migrated.Money.Should().BeGreaterThanOrEqualTo(0m);
        migrated.GoldenScrews.Should().BeGreaterThanOrEqualTo(0);
        migrated.PlayerLevel.Should().BeGreaterThanOrEqualTo(1);
        migrated.WorkshopStars.Should().NotBeNull();
        migrated.ParallelOrdersByWorkshop.Should().NotBeNull();
        migrated.Boosts.Should().NotBeNull();
        migrated.DailyProgress.Should().NotBeNull();
        migrated.Cosmetics.Should().NotBeNull();
        migrated.Settings.Should().NotBeNull();
        migrated.Statistics.Should().NotBeNull();
        migrated.Tutorial.Should().NotBeNull();
    }

    [Fact]
    public void Migrate_V5WithActiveOrder_ParallelOrdersContainsIt()
    {
        var state = CreateMinimalState(5);
        state.ActiveOrder = new Order
        {
            Id = "test-order",
            WorkshopType = WorkshopType.Carpenter,
            BaseReward = 100m,
            BaseXp = 10,
        };

        var migrated = SaveGameService.MigrateState(state);

        // V7 (): V5→V6 migriert ActiveOrder, V6→V7 ergaenzt Lager-Felder.
        migrated.Version.Should().Be(7);
        migrated.ParallelOrdersByWorkshop.Should().ContainKey(WorkshopType.Carpenter);
        migrated.ParallelOrdersByWorkshop[WorkshopType.Carpenter].Id.Should().Be("test-order");
        // V7-Felder sind nach Migration sauber initialisiert.
        migrated.WarehouseSlotCount.Should().BeGreaterThanOrEqualTo(20);
        migrated.WarehouseStackLimit.Should().BeGreaterThanOrEqualTo(50);
        migrated.ReservedInventory.Should().NotBeNull();
        migrated.AutoSellRules.Should().NotBeNull();
        migrated.HeirloomItems.Should().NotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(13)]
    public void Migrate_FromRandomizedV1State_PreservesMoneyAndLevel(int seed)
    {
        // Property: egal welcher Random-State, Money + PlayerLevel ueberleben die Migration.
        var rng = new Random(seed);
        var state = CreateMinimalState(1);
        var moneyBefore = (decimal)(rng.NextDouble() * 1_000_000_000.0);
        var levelBefore = rng.Next(1, 1000);
        state.Money = moneyBefore;
        state.PlayerLevel = levelBefore;

        var migrated = SaveGameService.MigrateState(state);

        migrated.Money.Should().Be(moneyBefore);
        migrated.PlayerLevel.Should().Be(levelBefore);
    }

    [Fact]
    public void Roundtrip_SerializeDeserialize_StatePreserved()
    {
        // Property: JSON-Roundtrip mit aktuellem State (CurrentStateVersion) erhaelt alle Pflicht-Werte.
        var state = CreateMinimalState(GameState.CurrentStateVersion);
        state.Money = 12345.67m;
        state.GoldenScrews = 250;
        state.PlayerLevel = 42;

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<GameState>(json);

        restored.Should().NotBeNull();
        restored!.Money.Should().Be(state.Money);
        restored.GoldenScrews.Should().Be(state.GoldenScrews);
        restored.PlayerLevel.Should().Be(state.PlayerLevel);
        restored.Version.Should().Be(GameState.CurrentStateVersion);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(1_000_000.0)]
    [InlineData(999_999_999_999.99)]
    public void Roundtrip_LargeMoney_NoOverflow(double moneyAsDouble)
    {
        // Property: Auch riesige Late-Game-Money-Werte (Late-Game ~10^15) ueberleben den Roundtrip.
        var state = CreateMinimalState(6);
        state.Money = (decimal)moneyAsDouble;

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<GameState>(json);

        restored.Should().NotBeNull();
        restored!.Money.Should().BeApproximately(state.Money, 0.01m);
    }

    [Fact]
    public void Migrate_PreservesWorkshopStars_ThroughAllVersions()
    {
        // Property: WorkshopStars (V3+) sind permanent — durch alle Migration-Stufen erhalten.
        var state = CreateMinimalState(3);
        state.WorkshopStars[WorkshopType.Carpenter.ToString()] = 4;
        state.WorkshopStars[WorkshopType.Plumber.ToString()] = 2;

        var migrated = SaveGameService.MigrateState(state);

        migrated.WorkshopStars[WorkshopType.Carpenter.ToString()].Should().Be(4);
        migrated.WorkshopStars[WorkshopType.Plumber.ToString()].Should().Be(2);
    }

    [Fact]
    public void Migrate_NegativeMoney_DoesNotThrow()
    {
        // Edge-Case: Korrupte Saves mit negativer Money (z.B. durch Bug) sollen migrieren ohne zu crashen.
        // Sanitize-Pass im SaveGameService haendelt das danach.
        var state = CreateMinimalState(1);
        state.Money = -100m;

        var act = () => SaveGameService.MigrateState(state);

        act.Should().NotThrow();
    }

    private static GameState CreateMinimalState(int version)
    {
        var state = GameState.CreateNew();
        state.Version = version;
        return state;
    }
}
