namespace ArcaneKingdom.Domain.Economy
{
    /// <summary>
    /// Spielwährungen (DESIGN.md Kapitel 17).
    /// Energie ist eine Spezialwaehrung mit Cap + Bonus-Überlauf — siehe PlayerCurrencies.
    /// </summary>
    public enum Currency
    {
        Gold = 0,
        Diamond = 1,
        Energy = 2,
        GuildPoints = 3,
        UniversalScraps = 4,
        MeritPoints = 5,
        ArenaTickets = 6  // TBD: Phase 2
    }
}
