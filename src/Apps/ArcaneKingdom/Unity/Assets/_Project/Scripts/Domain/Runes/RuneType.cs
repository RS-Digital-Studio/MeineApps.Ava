namespace ArcaneKingdom.Domain.Runes
{
    /// <summary>
    /// Runen-Typen (DESIGN.md Kapitel 7.2).
    /// </summary>
    public enum RuneType
    {
        Angriff = 0,        // + X % ATK aller Deck-Karten
        Verteidigung = 1,   // + X % HP aller Deck-Karten
        Geschwindigkeit = 2,// -1 Rundenwarten bei Spezialattacken
        Element = 3,        // + X % Schaden passendem Element
        Hero = 4,           // + X Helden-HP
        Kombo = 5,          // Set-Bonus
        Mana = 6            // +1 Start-Mana (TBD, Saison-exklusiv)
    }
}
