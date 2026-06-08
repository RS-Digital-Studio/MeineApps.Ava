#nullable enable

namespace HandwerkerImperium.Domain.Runtime
{
    /// <summary>
    /// Laufzeit-Zustand der Meta-Progression (über dem P0-Idle-Loop): trennt klar <b>akt-intern</b>
    /// (Reset bei Prestige) von <b>permanent</b> (überlebt alle Prestiges) gemäß PROGRESSION_BALANCING §3.
    /// Unity-frei; der Game-Layer mappt diese Felder auf die Save-Slices (Town/Franchise/Mastery/…).
    /// </summary>
    public sealed class MetaState
    {
        // ── Akt-intern (Reset bei Prestige) ────────────────────────────────
        public int CurrentStar = 1;
        public long OrdersServed;
        public int RestorationPhases;

        // ── Permanent (nie reset) ──────────────────────────────────────────
        public int PrestigeCount;
        public int CityIndex;
        public decimal PrestigeMultiplier = 1m; // kumulativ (×3 / ×12 / ×60)
        public decimal PrestigeCurrency;        // Imperium-Marken-Quelle (PP)
        public int MasteryLevel;
        public double MasteryXp;
        public int MeistergradGrade;
        public decimal Renommee;
        public int AvailableMarks;              // Perkboard-Währung
    }
}
