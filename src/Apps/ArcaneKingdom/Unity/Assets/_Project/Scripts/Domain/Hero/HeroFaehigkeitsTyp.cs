namespace ArcaneKingdom.Domain.Hero
{
    /// <summary>
    /// Typen der Helden-Passiv-Fähigkeit (Designplan v4 Kap. 2.1).
    /// Es gibt genau eine Passiv pro Rasse — sie wirkt automatisch für den gesamten Kampf,
    /// ohne dass der Spieler sie aktivieren muss.
    /// </summary>
    public enum HeroFaehigkeitsTyp
    {
        /// <summary>
        /// Ritter — Königliche Aura: Eigene Karten starten mit +5% HP.
        /// Magnitude = HP-Bonus in Prozent (Default 5).
        /// </summary>
        KoeniglicheAura = 0,

        /// <summary>
        /// Götter — Göttlicher Segen: Einmal pro Kampf verhindert ein Tod (Karte überlebt mit 1 HP).
        /// Magnitude = Anzahl Rettungen pro Kampf (Default 1).
        /// </summary>
        GoettlicherSegen = 1,

        /// <summary>
        /// Elfen — Waldläufer: Erste eigene Karte jeder Runde kostet 0 COST.
        /// Magnitude = COST-Reduktion (Default 0 = komplett kostenlos).
        /// </summary>
        Waldlaeufer = 2,

        /// <summary>
        /// Tiergeister — Rudelbund: +3% ATK für jede Tiergeist-Karte im Deck (stapelbar).
        /// Magnitude = ATK-Bonus pro Tiergeist in Prozent (Default 3).
        /// </summary>
        Rudelbund = 3,

        /// <summary>
        /// Dämonen — Lebensraub-Aura: 20% aller Karten-Schäden heilen Helden-HP.
        /// Magnitude = Lebensraub-Prozent (Default 20).
        /// </summary>
        LebensraubAura = 4
    }
}
