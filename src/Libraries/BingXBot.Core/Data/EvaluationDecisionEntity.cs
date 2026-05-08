using SQLite;

namespace BingXBot.Core.Data;

/// <summary>
/// v1.5.2 Phase 4 — DB-Persistenz fuer Decision-Trail-Eintraege.
/// Tabelle: EvaluationDecisions. Schema-Migration v11.
///
/// CategoriesJson + HardFiltersJson sind JSON-Arrays (string[]) — sqlite-net hat keine
/// native String-Array-Spalte. Beim Lesen via JsonSerializer.Deserialize zurueckkonvertieren.
/// </summary>
[Table("EvaluationDecisions")]
public class EvaluationDecisionEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public DateTime Timestamp { get; set; }

    [Indexed]
    public string Symbol { get; set; } = "";

    public int Tf { get; set; }                     // (int)TimeFrame

    public string SequenceState { get; set; } = "";

    public decimal? Point0 { get; set; }
    public decimal? PointA { get; set; }
    public decimal? PointB { get; set; }

    public bool Triggered { get; set; }

    [Indexed]
    public string? RejectionReason { get; set; }

    public int ConfluenceScore { get; set; }
    public string CategoriesJson { get; set; } = "[]";
    public string HardFiltersJson { get; set; } = "[]";
}
