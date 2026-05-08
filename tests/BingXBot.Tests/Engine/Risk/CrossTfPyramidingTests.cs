using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Risk;

// v1.7.0 Phase 16 — Cross-TF-Position-Pyramidisierung (User-Ausnahme).
// Default false. Pro-Trader-Praxis fuer Trend-Confirmation-Adds.
//
// Hinweis: Die volle RiskManager-Integration (Multi-Entry-Lifecycle) ist umfangreich. Diese
// Tests decken die Datenstruktur + Settings-Konsistenz ab. Die volle Trade-Lifecycle-
// Integration (gemeinsamer SL, separate TPs) erweitert TradingServiceBase und ist als
// separater Implementation-Schritt vorgesehen.
public class CrossTfPyramidingTests
{
    [Fact]
    public void RiskSettings_EnableCrossTfPyramiding_DefaultFalse()
    {
        var rs = new RiskSettings();
        rs.EnableCrossTfPyramiding.Should().BeFalse("Default ist OPT-IN, User-Ausnahme");
    }

    [Fact]
    public void RiskSettings_PyramidDefaults()
    {
        var rs = new RiskSettings();
        rs.PyramidMaxAddOns.Should().Be(1);
        rs.PyramidScalePercent.Should().Be(0.5m);
    }

    [Fact]
    public void PositionExitState_DefaultsHaveNoPyramidEntries()
    {
        var state = new PositionExitState();
        state.PyramidAddOnCount.Should().Be(0);
        state.PyramidEntries.Should().NotBeNull();
        state.PyramidEntries.Should().BeEmpty();
    }

    [Fact]
    public void PositionExitState_AddPyramidEntry_TracksCount()
    {
        var state = new PositionExitState();
        state.PyramidEntries.Add(new PyramidEntry(
            Tf: TimeFrame.H4,
            EntryTimeUtc: DateTime.UtcNow,
            EntryPrice: 50100m,
            Quantity: 0.05m,
            TakeProfit1: 51000m,
            TakeProfit2: 52000m));
        state.PyramidAddOnCount = state.PyramidEntries.Count;
        state.PyramidAddOnCount.Should().Be(1);
        state.PyramidEntries[0].Tf.Should().Be(TimeFrame.H4);
        state.PyramidEntries[0].Quantity.Should().Be(0.05m);
    }

    [Fact]
    public void Pyramid_AddOnSize_HalfOfOriginal()
    {
        // Plan-Spez: Add-On = OriginalQuantity * PyramidScalePercent (Default 0.5).
        var rs = new RiskSettings { PyramidScalePercent = 0.5m };
        var originalQuantity = 0.10m;
        var addOnQty = originalQuantity * rs.PyramidScalePercent;
        addOnQty.Should().Be(0.05m);
    }

    [Fact]
    public void Pyramid_AddOnSize_CustomPercent()
    {
        var rs = new RiskSettings { PyramidScalePercent = 0.25m };
        var originalQuantity = 0.10m;
        var addOnQty = originalQuantity * rs.PyramidScalePercent;
        addOnQty.Should().Be(0.025m);
    }

    [Fact]
    public void Pyramid_MaxAddOns_BlocksThird()
    {
        // Bei MaxAddOns = 1 darf der zweite Add-On versuch nicht mehr durchgehen.
        var rs = new RiskSettings { PyramidMaxAddOns = 1 };
        var state = new PositionExitState { PyramidAddOnCount = 1 }; // 1× bereits hinzugefuegt
        var canAddAnother = state.PyramidAddOnCount < rs.PyramidMaxAddOns;
        canAddAnother.Should().BeFalse("MaxAddOns=1 blockt den zweiten Add");
    }
}
