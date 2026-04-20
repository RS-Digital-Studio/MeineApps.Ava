using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementierung der <see cref="IGuildFacade"/> — reiner Service-Container (Pass-Through).
/// Bundelt die 7 Gilden-Subsystem-Services fuer Konsumenten (GuildViewModel u.a.) auf
/// eine einzige Ctor-Abhaengigkeit. Keine eigene Logik, kein Caching, kein State.
/// </summary>
public sealed class GuildFacade : IGuildFacade
{
    public IGuildService Guild { get; }
    public IGuildResearchService Research { get; }
    public IGuildChatService Chat { get; }
    public IGuildWarSeasonService WarSeason { get; }
    public IGuildBossService Boss { get; }
    public IGuildHallService Hall { get; }
    public IGuildTipService Tip { get; }
    public IGuildAchievementService Achievement { get; }

    public GuildFacade(
        IGuildService guild,
        IGuildResearchService research,
        IGuildChatService chat,
        IGuildWarSeasonService warSeason,
        IGuildBossService boss,
        IGuildHallService hall,
        IGuildTipService tip,
        IGuildAchievementService achievement)
    {
        Guild = guild;
        Research = research;
        Chat = chat;
        WarSeason = warSeason;
        Boss = boss;
        Hall = hall;
        Tip = tip;
        Achievement = achievement;
    }
}
