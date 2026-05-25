namespace ArcaneKingdom.Domain.Economy
{
    /// <summary>
    /// Sternkarten-Stufen aus dem Login-Belohnungssystem (Designplan v4 Oekosystem Kap. 5.2).
    /// Sammelkarten ohne Kampfwert — werden im Sternkarten-Tempel gegen Belohnungen eingetauscht.
    /// </summary>
    public enum SternkartenStufe
    {
        /// <summary>Bronze — Standard-Tag, 1 Sternpunkt im Tempel.</summary>
        Bronze = 0,

        /// <summary>Silber — Wochen-Meilensteine (Tag 7, 14), 5 Sternpunkte.</summary>
        Silber = 1,

        /// <summary>Gold — 3-Wochen-Meilenstein + Monatsende, 15 Sternpunkte.</summary>
        Gold = 2,

        /// <summary>Platin — Nur am Tag 30, 50 Sternpunkte.</summary>
        Platin = 3
    }

    /// <summary>
    /// Sternpunkte-Werte aus Designplan v4 Kap. 5.2 + 5.3.
    /// Bei taeglichem Login pro Monat: 22 Bronze + 2 Silber + 2 Gold + 1 Platin = 112 Sternpunkte.
    /// </summary>
    public static class SternkartenWerte
    {
        public static int GetSternpunkte(SternkartenStufe stufe) => stufe switch
        {
            SternkartenStufe.Bronze => 1,
            SternkartenStufe.Silber => 5,
            SternkartenStufe.Gold   => 15,
            SternkartenStufe.Platin => 50,
            _                        => 0
        };

        /// <summary>Sternkarten-Tempel-Eintausch-Kosten (Designplan v4 Oeko Kap. 5.3).</summary>
        public const int CostRandom2Star      = 30;    // Zufaellige 2★-Karte
        public const int CostChosen3Star      = 80;    // Waehlbare 3★-Karte
        public const int CostExclusive3Star   = 150;   // Sternkarten-exklusive 3★ (rotiert alle 2 Monate)
        public const int CostExclusive4Star   = 350;   // Sternkarten-exklusive 4★ (rotiert alle 3 Monate)
        public const int CostLegendaryScrap   = 100;   // 1x Legendary Scrap (fuer LV 15-Upgrade)
        public const int CostMythicFragment   = 500;   // 1/3 eines Mythischen Kerns (fuer 6★-Crafting)

        /// <summary>Anzahl Mythic-Fragmente die einen vollen Mythischen Kern ergeben.</summary>
        public const int MythicFragmentsPerCore = 3;
    }
}
