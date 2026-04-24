using BingXBot.Core.Enums;

namespace BingXBot.Core.Services;

/// <summary>
/// Zentrale Fee- und Net-PnL-Berechnung. Live, Paper und Backtest nutzen denselben Code —
/// verhindert Drift zwischen Pfaden, die historisch eigene PnL-Formeln inline hatten.
///
/// Notional = <c>quantity × price</c>. Fee = <c>notional × feeRate</c>. Krypto-Perpetuals haben
/// separate Maker/Taker-Raten — der Aufrufer waehlt die passende pro Execution.
/// </summary>
public static class FeeCalculator
{
    /// <summary>
    /// Fee fuer eine Order-Execution. <paramref name="feeRate"/> z.B. 0.0005 (0,05 %, BingX-Default-Taker).
    /// </summary>
    public static decimal CalculateFee(decimal quantity, decimal price, decimal feeRate)
        => Math.Abs(quantity) * Math.Abs(price) * Math.Max(0m, feeRate);

    /// <summary>
    /// Netto-PnL einer geschlossenen Position nach Abzug von Entry- + Exit-Fee.
    /// Konvention: Long = <c>(exit − entry) × qty</c>, Short = <c>(entry − exit) × qty</c>.
    /// Fees werden an BEIDEN Enden beruecksichtigt (Entry + Exit).
    /// </summary>
    public static decimal CalculateNetPnl(
        Side side,
        decimal entryPrice,
        decimal exitPrice,
        decimal quantity,
        decimal takerFeeRate)
    {
        var raw = side == Side.Buy
            ? (exitPrice - entryPrice) * quantity
            : (entryPrice - exitPrice) * quantity;

        var totalFee = CalculateFee(quantity, entryPrice, takerFeeRate)
                     + CalculateFee(quantity, exitPrice, takerFeeRate);

        return raw - totalFee;
    }

    /// <summary>
    /// Total-Fee eines Trades (Entry + Exit). Getrennte Funktion damit Aufrufer die Fee
    /// separat in CompletedTrade speichern koennen.
    /// </summary>
    public static decimal CalculateTotalFee(
        decimal entryPrice,
        decimal exitPrice,
        decimal quantity,
        decimal takerFeeRate)
        => CalculateFee(quantity, entryPrice, takerFeeRate)
         + CalculateFee(quantity, exitPrice, takerFeeRate);
}
