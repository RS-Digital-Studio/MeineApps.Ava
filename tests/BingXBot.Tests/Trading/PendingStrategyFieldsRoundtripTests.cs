using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BingXBot.Tests.Trading;

// v1.4.0 Phase 0.7 (Finding 0.7) — Strategy-Felder im PendingLimitOrderState.
//
// Vor v1.4.0 wurden NavPointA / IsGklSetup / GklTimeframe / RunnerHardCap /
// IsCounterTrendScalp / PositionScaleOverride beim Snapshot/Restore nicht mit-persistiert.
// Wenn das Signal nach 30 s+ rekonstruiert werden musste (Verwaist-Cleanup oder
// Restart), fielen diese Felder weg → A-Bruch-BE-Trigger feuerte nie, Runner inactive,
// HighProb-Boost futsch.
//
// Migration v10 ist additiv (JSON-Blob): alte Eintraege deserialisieren mit Default-Werten,
// neue Server-Versionen brechen nicht.
public class PendingStrategyFieldsRoundtripTests
{
    [Fact]
    public void PersistAndRestore_PreservesNavPointA()
    {
        var state = new PendingLimitOrderState
        {
            OrderId = "tp1",
            Symbol = "BTC-USDT",
            IsLong = true,
            InvalidationLevel = 49500m,
            PlacedAt = DateTime.UtcNow,
            NavPointA = 51234.56m,
        };

        var json = JsonSerializer.Serialize(state);
        var loaded = JsonSerializer.Deserialize<PendingLimitOrderState>(json);

        loaded.Should().NotBeNull();
        loaded!.NavPointA.Should().Be(51234.56m);
    }

    [Fact]
    public void PersistAndRestore_PreservesRunnerFields()
    {
        var state = new PendingLimitOrderState
        {
            OrderId = "tp2",
            Symbol = "ETH-USDT",
            IsLong = false,
            InvalidationLevel = 3100m,
            PlacedAt = DateTime.UtcNow,
            RunnerHardCap = 2800m,
            IsGklSetup = true,
            GklTimeframe = TimeFrame.D1,
        };

        var json = JsonSerializer.Serialize(state);
        var loaded = JsonSerializer.Deserialize<PendingLimitOrderState>(json);

        loaded.Should().NotBeNull();
        loaded!.RunnerHardCap.Should().Be(2800m);
        loaded.IsGklSetup.Should().BeTrue();
        loaded.GklTimeframe.Should().Be(TimeFrame.D1);
    }

    [Fact]
    public void PersistAndRestore_CounterTrendAndPositionScale()
    {
        var state = new PendingLimitOrderState
        {
            OrderId = "ct1",
            Symbol = "SOL-USDT",
            IsLong = true,
            InvalidationLevel = 100m,
            PlacedAt = DateTime.UtcNow,
            IsCounterTrendScalp = true,
            PositionScaleOverride = 0.5m,
        };

        var json = JsonSerializer.Serialize(state);
        var loaded = JsonSerializer.Deserialize<PendingLimitOrderState>(json);

        loaded.Should().NotBeNull();
        loaded!.IsCounterTrendScalp.Should().BeTrue();
        loaded.PositionScaleOverride.Should().Be(0.5m);
    }

    [Fact]
    public void LegacyEntry_WithoutFields_DefaultsToNullOrZero()
    {
        // Legacy-JSON aus pre-v1.4.0 Persistenz — neue Felder fehlen.
        // Backwards-Compat: Deserialisierung bricht nicht, neue Felder bekommen Defaults.
        var legacyJson = """
        {
            "OrderId": "old-order",
            "Symbol": "BTC-USDT",
            "IsLong": true,
            "InvalidationLevel": 50000.0,
            "PlacedAt": "2026-04-20T10:00:00Z",
            "TakeProfit": 51000.0
        }
        """;

        var loaded = JsonSerializer.Deserialize<PendingLimitOrderState>(legacyJson);

        loaded.Should().NotBeNull();
        loaded!.NavPointA.Should().Be(0m);
        loaded.RunnerHardCap.Should().Be(0m);
        loaded.IsGklSetup.Should().BeFalse();
        loaded.GklTimeframe.Should().BeNull();
        loaded.IsCounterTrendScalp.Should().BeFalse();
        loaded.PositionScaleOverride.Should().BeNull();
    }
}
