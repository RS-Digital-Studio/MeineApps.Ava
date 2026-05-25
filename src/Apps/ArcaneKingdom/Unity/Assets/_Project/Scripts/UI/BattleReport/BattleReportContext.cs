#nullable enable
namespace ArcaneKingdom.UI.BattleReport
{
    /// <summary>
    /// Daten-Transfer fuer den BattleReport-Screen.
    /// Wird vom BattleScreen nach Kampfende befuellt, der Report liest beim OnEnter.
    /// </summary>
    public sealed class BattleReportContext
    {
        public bool IsVictory { get; set; }
        public bool IsDraw { get; set; }
        /// <summary>Sterne 0-4 (0 bei Niederlage).</summary>
        public int Stars { get; set; }
        public long GoldReward { get; set; }
        public int ExpReward { get; set; }
        /// <summary>Gegner-Name (z.B. "[NEXUS] Sturmreiterin") fuer Arena/PvP. Bei PvE leer.</summary>
        public string? OpponentName { get; set; }
        public int? OpponentLevel { get; set; }
        /// <summary>Rang-Aenderung in der Arena (+12 / -10 etc.).</summary>
        public int? RankDelta { get; set; }
        public int? NewRank { get; set; }
        /// <summary>Node-Id (PvE) oder leer.</summary>
        public string? NodeId { get; set; }

        public void Reset()
        {
            IsVictory = false;
            IsDraw = false;
            Stars = 0;
            GoldReward = 0;
            ExpReward = 0;
            OpponentName = null;
            OpponentLevel = null;
            RankDelta = null;
            NewRank = null;
            NodeId = null;
        }
    }
}
