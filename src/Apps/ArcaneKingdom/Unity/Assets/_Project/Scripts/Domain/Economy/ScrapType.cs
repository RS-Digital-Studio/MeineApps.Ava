namespace ArcaneKingdom.Domain.Economy
{
    /// <summary>
    /// Upgrade-Stein-Typen für Karten-Leveling (DESIGN.md Kapitel 6.2).
    /// NICHT zu verwechseln mit Universal Scraps (Craft-Währung).
    /// </summary>
    public enum ScrapType
    {
        Common = 0,     // LV 0 -> 4
        Rare = 1,       // LV 5 -> 9
        Epic = 2,       // LV 10 -> 14
        Legendary = 3   // LV 15
    }
}
