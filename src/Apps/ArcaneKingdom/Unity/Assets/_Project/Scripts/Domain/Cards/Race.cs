namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Die fünf spielbaren Rassen aus dem Designplan v4. Jede Rasse hat eine eigene Held-Passiv-Fähigkeit
    /// (siehe <see cref="ArcaneKingdom.Domain.Hero.HeroDefinition"/>) und einen eigenen Kartenpool.
    /// Götter (<see cref="Goetter"/>) sind eine Premium-Rasse — sie existieren nur als 4★–6★-Karten und
    /// können ausschließlich über Fusion erhalten werden (siehe DESIGN.md Kap. 5 + Kap. 6 Götter-Crafting).
    /// </summary>
    public enum Race
    {
        /// <summary>Ritter / Helden — Tank/Support. Hohe HP, Schilde, Heilung, Team-Buffs.</summary>
        Ritter = 0,

        /// <summary>Götter — Prestige-Rasse, nur 4★ bis 6★, ausschließlich über Crafting erhältlich.</summary>
        Goetter = 1,

        /// <summary>Elfen — Speed/Kontrolle. Schnelle Angriffe, Schlaf/Verlangsamung, Eigenheilung.</summary>
        Elfen = 2,

        /// <summary>Tiergeister / Waldgeister — Rudel-Synergien. Wölfe, Drachen, Baumgeister.</summary>
        Tiergeister = 3,

        /// <summary>Dämonen — Hohe ATK. Lebensraub, Gift/Fluch, riskante Mechaniken.</summary>
        Daemonen = 4
    }
}
