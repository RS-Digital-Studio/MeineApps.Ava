using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using SQLite;

namespace BingXBot.Core.Data;

[Table("Trades")]
public class TradeEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Symbol { get; set; } = "";
    public int Side { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal Pnl { get; set; }
    public decimal Fee { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public string Reason { get; set; } = "";
    public int Mode { get; set; }

    public CompletedTrade ToRecord() => new(
        Symbol,
        (Side)Side,
        EntryPrice,
        ExitPrice,
        Quantity,
        Pnl,
        Fee,
        EntryTime,
        ExitTime,
        Reason,
        (TradingMode)Mode);

    public static TradeEntity FromRecord(CompletedTrade t) => new()
    {
        Symbol = t.Symbol,
        Side = (int)t.Side,
        EntryPrice = t.EntryPrice,
        ExitPrice = t.ExitPrice,
        Quantity = t.Quantity,
        Pnl = t.Pnl,
        Fee = t.Fee,
        EntryTime = t.EntryTime,
        ExitTime = t.ExitTime,
        Reason = t.Reason,
        Mode = (int)t.Mode
    };
}
