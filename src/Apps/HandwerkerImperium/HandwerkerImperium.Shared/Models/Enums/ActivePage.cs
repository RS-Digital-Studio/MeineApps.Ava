namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Alle navigierbaren Seiten in der App. Ersetzt 35+ einzelne IsXxxActive-Booleans
/// durch eine einzige zentrale Source-of-Truth für den aktiven Seitenstand.
/// </summary>
public enum ActivePage
{
    // Haupt-Tabs (Tab-Bar sichtbar)
    Dashboard,
    Buildings,      // Imperium-Tab
    Missionen,
    Guild,
    Shop,

    // Detail-/Sub-Seiten (Tab-Bar versteckt)
    Statistics,
    Achievements,
    Settings,
    WorkshopDetail,
    OrderDetail,
    WorkerMarket,
    Research,
    Manager,
    Tournament,
    SeasonalEvent,
    BattlePass,
    Crafting,
    Ascension,
    Prestige,       // v2.0.37: Single-Page-View statt Modal-Stack
    Market,         // V7 (Phase 3 Ressourcen-Plan): Material-Markt
    GuildBuildSite, // V7 (Phase 4 Ressourcen-Plan, Plan Section 3.9): Gilden-Mega-Projekt-Bauplatz

    // MiniGames
    SawingGame,
    PipePuzzle,
    WiringGame,
    PaintingGame,
    RoofTilingGame,
    BlueprintGame,
    DesignPuzzleGame,
    InspectionGame,
    ForgeGame,
    InventGame,

    // Gilden-Sub-Seiten
    GuildResearch,
    GuildMembers,
    GuildInvite,
    GuildWarSeason,
    GuildBoss,
    GuildHall,
    GuildAchievements,
    GuildChat,
    GuildWar,
}
