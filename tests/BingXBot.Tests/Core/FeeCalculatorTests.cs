using BingXBot.Core.Enums;
using BingXBot.Core.Services;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

// Tests fuer FeeCalculator (P1-2, 24.04.2026). Zentrale Fee-/Net-PnL-Berechnung,
// geteilt zwischen Live + Paper + Backtest.
public class FeeCalculatorTests
{
    [Fact]
    public void CalculateFee_StandardKrypto_LiefertErwartetenWert()
    {
        // 1 BTC @ 50000 USDT × 0,05 % (BingX-Taker) = 25 USDT
        var fee = FeeCalculator.CalculateFee(quantity: 1m, price: 50000m, feeRate: 0.0005m);
        fee.Should().Be(25m);
    }

    [Fact]
    public void CalculateFee_ZeroRate_LiefertNull()
    {
        FeeCalculator.CalculateFee(1m, 50000m, 0m).Should().Be(0m);
    }

    [Fact]
    public void CalculateFee_NegativeRate_WirdAufNullGeclampt()
    {
        FeeCalculator.CalculateFee(1m, 50000m, -0.001m).Should().Be(0m);
    }

    [Fact]
    public void CalculateFee_NegativeQuantity_WirdAbsolut()
    {
        // Close-Seite kann theoretisch negative Qty liefern — absolut rechnen.
        FeeCalculator.CalculateFee(-1m, 50000m, 0.0005m).Should().Be(25m);
    }

    [Fact]
    public void CalculateNetPnl_Long_Gewinn_ZiehtBeideFeesAb()
    {
        // Long 1 BTC: Entry 50000, Exit 51000 → Raw 1000. Fees: 25 + 25.5 = 50.5. Net 949.5
        var pnl = FeeCalculator.CalculateNetPnl(Side.Buy, 50000m, 51000m, 1m, 0.0005m);
        pnl.Should().Be(949.5m);
    }

    [Fact]
    public void CalculateNetPnl_Short_Gewinn()
    {
        // Short 1 BTC: Entry 50000, Exit 49000 → Raw 1000. Fees: 25 + 24.5 = 49.5. Net 950.5
        var pnl = FeeCalculator.CalculateNetPnl(Side.Sell, 50000m, 49000m, 1m, 0.0005m);
        pnl.Should().Be(950.5m);
    }

    [Fact]
    public void CalculateNetPnl_Long_Verlust()
    {
        // Long 1 BTC: Entry 50000, Exit 49000 → Raw -1000. Fees: 25 + 24.5 = 49.5. Net -1049.5
        var pnl = FeeCalculator.CalculateNetPnl(Side.Buy, 50000m, 49000m, 1m, 0.0005m);
        pnl.Should().Be(-1049.5m);
    }

    [Fact]
    public void CalculateTotalFee_SummiertBeideSeiten()
    {
        // Entry 50000, Exit 51000, 1 BTC × 0,05 % = 25 + 25.5 = 50.5
        var total = FeeCalculator.CalculateTotalFee(50000m, 51000m, 1m, 0.0005m);
        total.Should().Be(50.5m);
    }

    [Fact]
    public void CalculateNetPnl_SameEntryAndExit_LiefertNegativenFeeWert()
    {
        // Keine Bewegung → nur Fees ziehen = negativer Net-PnL
        var pnl = FeeCalculator.CalculateNetPnl(Side.Buy, 50000m, 50000m, 1m, 0.0005m);
        pnl.Should().Be(-50m); // 2x 25 Fee, keine Raw-Marge
    }
}
