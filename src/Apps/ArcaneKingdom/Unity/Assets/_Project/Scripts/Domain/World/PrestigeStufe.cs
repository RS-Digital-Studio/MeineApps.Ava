namespace ArcaneKingdom.Domain.World
{
    /// <summary>
    /// Prestige-Stufen einer Welt (Designplan v4 Oekosystem Kap. 6).
    /// Voraussetzung jeder Stufe: Alle Level der Welt auf 3 Sternen.
    /// Beim Aufwerten werden die Sterne zurueckgesetzt, das Revenue/Day der Welt bleibt erhalten.
    /// </summary>
    public enum PrestigeStufe
    {
        /// <summary>Start-Schwierigkeit. Basis-Drops und -Gold.</summary>
        Normal = 0,

        /// <summary>Prestige I — 100.000 Gold. Gegner +30% ATK/HP, +50% Gold/Drop, 2★ Karten droppen häufiger.</summary>
        I = 1,

        /// <summary>Prestige II — 500.000 Gold. Gegner +60% ATK/HP, +100% Gold/Drop, 3★ Karten droppen, Runen-Fragmente.</summary>
        II = 2,

        /// <summary>Prestige III — 2.000.000 Gold. Gegner +100% ATK/HP, +200% Gold, 3–4★ Drops, Epic Scraps.</summary>
        III = 3,

        /// <summary>Prestige IV (MAX) — 5.000.000 Gold. Endgame, exklusive Prestige-Karte pro Welt.</summary>
        IV = 4
    }

    /// <summary>
    /// Aufwerten-Kosten + Boni pro Prestige-Stufe.
    /// </summary>
    public static class PrestigeStufeBalancing
    {
        /// <summary>Aufwerten-Kosten in Gold von der aktuellen Stufe zur naechsten.</summary>
        public static long GetUpgradeGoldCost(PrestigeStufe currentStufe) => currentStufe switch
        {
            PrestigeStufe.Normal => 100_000,
            PrestigeStufe.I      => 500_000,
            PrestigeStufe.II     => 2_000_000,
            PrestigeStufe.III    => 5_000_000,
            PrestigeStufe.IV     => -1,        // MAX erreicht — kein weiteres Upgrade
            _                    => -1
        };

        /// <summary>Multiplikator fuer Gegner-Stats (ATK & HP).</summary>
        public static float GetEnemyStatMultiplier(PrestigeStufe stufe) => stufe switch
        {
            PrestigeStufe.Normal => 1.00f,
            PrestigeStufe.I      => 1.30f,
            PrestigeStufe.II     => 1.60f,
            PrestigeStufe.III    => 2.00f,
            PrestigeStufe.IV     => 2.50f,
            _                    => 1.00f
        };

        /// <summary>Multiplikator fuer Gold-Drops.</summary>
        public static float GetGoldDropMultiplier(PrestigeStufe stufe) => stufe switch
        {
            PrestigeStufe.Normal => 1.00f,
            PrestigeStufe.I      => 1.50f,
            PrestigeStufe.II     => 2.00f,
            PrestigeStufe.III    => 3.00f,
            PrestigeStufe.IV     => 4.00f,
            _                    => 1.00f
        };

        /// <summary>Multiplikator fuer das passive Tages-Income (Revenue/Day) der Welt.</summary>
        public static float GetDailyRevenueMultiplier(PrestigeStufe stufe) => stufe switch
        {
            PrestigeStufe.Normal => 1.00f,
            PrestigeStufe.I      => 2.00f,
            PrestigeStufe.II     => 4.00f,
            PrestigeStufe.III    => 8.00f,
            PrestigeStufe.IV     => 16.00f,
            _                    => 1.00f
        };
    }
}
