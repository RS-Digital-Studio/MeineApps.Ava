namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Deck-Beschraenkung pro Karte (DESIGN.md Kapitel 5.5).
    /// </summary>
    public enum DeckLimit
    {
        /// <summary>Standard: bis 3 Kopien pro Deck (limitiert durch Sammelbarkeit).</summary>
        Unlimited = 0,
        /// <summary>Maximal 2 Kopien pro Deck (typisch fuer Selten/Epic ohne Unique-Effekt).</summary>
        MaxTwo = 1,
        /// <summary>Nur 1x pro Deck (typisch fuer Epic/Legendaer mit starken Effekten).</summary>
        OneOnly = 2
    }
}
