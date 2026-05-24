namespace ArcaneKingdom.Domain.Guild
{
    /// <summary>
    /// Rollen in einer Gilde (DESIGN.md Kap. 12.3).
    /// </summary>
    public enum GuildRole
    {
        Member = 0,
        Veteran = 1,
        Officer = 2,
        Leader = 3
    }

    public enum GuildJoinPolicy
    {
        Open = 0,        // Sofort beitretbar
        OnRequest = 1,   // Antrag erforderlich
        Closed = 2       // Nur per Einladung
    }
}
