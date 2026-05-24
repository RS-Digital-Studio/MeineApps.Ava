namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Karten-Seltenheitsstufen (DESIGN.md Kapitel 5.2).
    /// </summary>
    public enum Rarity
    {
        Gewoehnlich = 0,   // 1 Stern, grauer Rahmen
        Ungewoehnlich = 1, // 2 Sterne, gruener Rahmen
        Selten = 2,        // 3 Sterne, blauer Rahmen
        Epic = 3,          // 4 Sterne, lila Rahmen
        Legendaer = 4      // 5 Sterne, goldener Rahmen
    }
}
