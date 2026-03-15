namespace BingXBot.Core.Models;

public record AccountInfo(
    decimal Balance,
    decimal AvailableBalance,
    decimal UnrealizedPnl,
    decimal UsedMargin);
