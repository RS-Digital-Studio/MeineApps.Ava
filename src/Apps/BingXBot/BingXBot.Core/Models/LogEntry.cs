using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Category,
    string Message,
    string? Symbol = null);
