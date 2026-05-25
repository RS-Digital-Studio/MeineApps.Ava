namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Deck-Beschränkung pro Karte (DESIGN.md Kapitel 5.5).
    /// </summary>
    public enum DeckLimit
    {
        /// <summary>Standard: bis 3 Kopien pro Deck (limitiert durch Sammelbarkeit).</summary>
        Unlimited = 0,
        /// <summary>Maximal 2 Kopien pro Deck (typisch für Selten/Epic ohne Unique-Effekt).</summary>
        MaxTwo = 1,
        /// <summary>Nur 1x pro Deck (typisch für Epic/Legendaer mit starken Effekten).</summary>
        OneOnly = 2
    }
}
