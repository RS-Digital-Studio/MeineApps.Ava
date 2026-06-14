namespace BingXBot.Core.Models;

public record ScanResult(
    string Symbol,
    decimal Score,
    string SetupType,
    Dictionary<string, decimal> Indicators);
