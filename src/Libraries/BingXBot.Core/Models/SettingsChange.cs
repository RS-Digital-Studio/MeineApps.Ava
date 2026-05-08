namespace BingXBot.Core.Models;

/// <summary>
/// v1.6.3 Phase 14 — Settings-Change-Audit-Trail-Eintrag.
/// Wird pro geaendertes Feld in <c>LocalSettingsService.Save*Async</c> erstellt.
/// Dient zwei Zielen: (a) "Wann wurde Setting X geaendert?", (b) Voraussetzung fuer
/// Phase 13 Trade-Replay (Settings-Snapshot zum Trade-Zeitpunkt).
/// </summary>
public sealed record SettingsChange(
    DateTime Timestamp,
    /// <summary>Voll qualifizierter Pfad, z.B. "Risk.MaxPositionSizePercent" oder "Scanner.EnableFundingRateBonus".</summary>
    string Field,
    string? OldValue,
    string? NewValue,
    /// <summary>"User-Desktop", "User-Mobile", "Auto-Resume", "Migration", "Server-Boot".</summary>
    string Source,
    /// <summary>JSON-Snapshot der vollstaendigen BotSettings nach der Aenderung. 1× pro SaveAllAsync-Call.</summary>
    string? Snapshot);
