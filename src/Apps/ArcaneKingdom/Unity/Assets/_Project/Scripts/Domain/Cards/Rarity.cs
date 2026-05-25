namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Sechs Seltenheitsstufen aus der Karten-Pyramide (Designplan v4 Kap. 4.1).
    /// Höhere Seltenheiten haben aufwendigere Rahmen mit animierten Elementen.
    /// 6★ Mythisch sind die seltensten Karten — nur 5 Stück im Spiel (eine pro Rasse).
    /// </summary>
    public enum Rarity
    {
        /// <summary>1★ Grau / Eisen. Basis-ATK 100–250 / HP 300–700. Häufigste Drops, massenhaft für Fusion.</summary>
        Gewoehnlich = 0,

        /// <summary>2★ Grün / Bronze. Basis-ATK 200–400 / HP 600–1100. Fusion-Material für 3★.</summary>
        Ungewoehnlich = 1,

        /// <summary>3★ Blau / Silber. Basis-ATK 300–550 / HP 900–1600. Kern-Fusionsmaterial.</summary>
        Selten = 2,

        /// <summary>4★ Lila / Amethyst. Basis-ATK 450–750 / HP 1200–2000. Crafting Tier 3 (ab LV 50).</summary>
        Epic = 3,

        /// <summary>5★ Gold / leuchtend. Basis-ATK 650–1200 / HP 1700–2500. Legendary Crafting (LV 70+).</summary>
        Legendaer = 4,

        /// <summary>6★ Celestial / animiert. Basis-ATK 900–1500 / HP 2200–3200. Nur 5 Stück, einzigartig.</summary>
        Mythisch = 5
    }
}
