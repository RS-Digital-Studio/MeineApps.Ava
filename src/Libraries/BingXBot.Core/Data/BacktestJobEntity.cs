using SQLite;

namespace BingXBot.Core.Data;

/// <summary>
/// Persistiert Backtest-Jobs — ueberlebt Server-Neustarts damit Clients den Status abfragen koennen.
/// Running/Queued-Jobs werden beim Server-Start als Failed markiert (orphan-Cleanup).
/// </summary>
[Table("BacktestJobs")]
public class BacktestJobEntity
{
    /// <summary>
    /// Aktuelle Schema-Version der JSON-Payloads (Request + Result).
    /// Bei DTO-Aenderungen erhoehen — aeltere Jobs werden beim Restore als Failed markiert
    /// statt silent fehlzuschlagen.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    [PrimaryKey]
    public string JobId { get; set; } = string.Empty;

    /// <summary>BacktestJobState als String (Queued/Running/Completed/Cancelled/Failed).</summary>
    public string State { get; set; } = "Queued";

    /// <summary>Schema-Version der JSON-Payloads (siehe CurrentSchemaVersion).</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>RequestDto als JSON serialisiert.</summary>
    public string RequestJson { get; set; } = string.Empty;

    /// <summary>ResultDto als JSON serialisiert (nur bei Completed gesetzt).</summary>
    public string? ResultJson { get; set; }

    /// <summary>Fehlermeldung bei Failed.</summary>
    public string? Error { get; set; }

    public DateTime QueuedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Fortschritt 0..1 fuer Running-Jobs.</summary>
    public float Progress { get; set; }
}
