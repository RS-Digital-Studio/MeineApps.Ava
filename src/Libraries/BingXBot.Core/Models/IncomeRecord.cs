namespace BingXBot.Core.Models;

/// <summary>
/// Fund-Flow-Eintrag (BingX /user/income): Realisierte PnL, Funding-Fees, Trading-Fees etc.
/// </summary>
public record IncomeRecord(
    string Symbol,
    string IncomeType,
    decimal Income,
    string Asset,
    string Info,
    DateTime Time);
