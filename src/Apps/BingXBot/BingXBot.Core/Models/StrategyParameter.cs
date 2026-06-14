namespace BingXBot.Core.Models;

public record StrategyParameter(
    string Name,
    string Description,
    string ValueType,
    object DefaultValue,
    object? MinValue = null,
    object? MaxValue = null,
    object? StepSize = null);
