using SQLite;

namespace BingXBot.Core.Data;

/// <summary>
/// v1.6.3 Phase 14 — DB-Persistenz fuer Settings-Audit-Trail.
/// Tabelle: SettingsChanges. Schema-Migration v12.
/// Snapshot-Spalte ist nullable und wird nur 1× pro SaveAllAsync-Call gefuellt
/// (sonst quadratische DB-Groesse bei vielen Field-Aenderungen).
/// </summary>
[Table("SettingsChanges")]
public class SettingsChangeEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public DateTime Timestamp { get; set; }

    [Indexed, MaxLength(120)]
    public string Field { get; set; } = "";

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    [MaxLength(40)]
    public string Source { get; set; } = "";

    public string? Snapshot { get; set; }
}
