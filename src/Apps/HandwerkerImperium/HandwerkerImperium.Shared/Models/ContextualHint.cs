namespace HandwerkerImperium.Models;

/// <summary>
/// Einzelner kontextueller Hinweis, der beim ersten Benutzen eines Features erscheint.
/// </summary>
public class ContextualHint
{
    /// <summary>
    /// Eindeutige ID des Hints (wird in GameState.Tutorial.SeenHints gespeichert).
    /// </summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// RESX-Key für den Titel.
    /// </summary>
    public string TitleKey { get; init; } = "";

    /// <summary>
    /// RESX-Key für den Beschreibungstext.
    /// </summary>
    public string TextKey { get; init; } = "";

    /// <summary>
    /// Position der Bubble relativ zum Ziel-Element.
    /// </summary>
    public HintPosition Position { get; init; } = HintPosition.Below;

    /// <summary>
    /// Ob dieser Hint als zentrierter Dialog statt als Tooltip-Bubble angezeigt wird.
    /// </summary>
    public bool IsDialog { get; init; }
}

/// <summary>
/// Position der Tooltip-Bubble relativ zum Ziel-Element.
/// </summary>
public enum HintPosition
{
    Above,
    Below
}

/// <summary>
/// Alle vordefinierten kontextuellen Hints.
/// </summary>
public static class ContextualHints
{
    // Willkommen (zentrierter Dialog, allererster Start)
    public static readonly ContextualHint Welcome = new()
    {
        Id = "welcome", TitleKey = "HintWelcomeTitle", TextKey = "HintWelcomeText",
        Position = HintPosition.Below, IsDialog = true
    };

    // Erste Werkstatt erkunden
    public static readonly ContextualHint FirstWorkshop = new()
    {
        Id = "first_workshop", TitleKey = "HintFirstWorkshopTitle", TextKey = "HintFirstWorkshopText",
        Position = HintPosition.Below
    };

    // Werkstatt-Detail: Upgrade
    public static readonly ContextualHint WorkshopDetail = new()
    {
        Id = "workshop_detail", TitleKey = "HintWorkshopDetailTitle", TextKey = "HintWorkshopDetailText",
        Position = HintPosition.Above
    };

    // Erster Auftrag annehmen
    public static readonly ContextualHint FirstOrder = new()
    {
        Id = "first_order", TitleKey = "HintFirstOrderTitle", TextKey = "HintFirstOrderText",
        Position = HintPosition.Above
    };

    // Erster Auftrag abgeschlossen
    public static readonly ContextualHint OrderCompleted = new()
    {
        Id = "order_completed", TitleKey = "HintOrderCompletedTitle", TextKey = "HintOrderCompletedText",
        Position = HintPosition.Below
    };

    // Mitarbeiter freigeschaltet (Level 3)
    public static readonly ContextualHint WorkerUnlock = new()
    {
        Id = "worker_unlock", TitleKey = "HintWorkerUnlockTitle", TextKey = "HintWorkerUnlockText",
        Position = HintPosition.Below
    };

    // Shop-Tab (erster Besuch)
    public static readonly ContextualHint ShopHint = new()
    {
        Id = "shop_hint", TitleKey = "HintShopTitle", TextKey = "HintShopText",
        Position = HintPosition.Above
    };

    // Forschung (erster Research-Tab-Besuch)
    public static readonly ContextualHint ResearchHint = new()
    {
        Id = "research_hint", TitleKey = "HintResearchTitle", TextKey = "HintResearchText",
        Position = HintPosition.Below
    };

    // Gebäude (erster Buildings-Tab-Besuch)
    public static readonly ContextualHint BuildingHint = new()
    {
        Id = "building_hint", TitleKey = "HintBuildingTitle", TextKey = "HintBuildingText",
        Position = HintPosition.Below
    };

    // Tägliche Herausforderungen (erster Missionen-Tab-Besuch)
    public static readonly ContextualHint DailyChallenge = new()
    {
        Id = "daily_challenge", TitleKey = "HintDailyChallengeTitle", TextKey = "HintDailyChallengeText",
        Position = HintPosition.Below
    };

    // Quick Jobs (Level 10 oder erster QuickJobs-Tab-Besuch)
    public static readonly ContextualHint QuickJobs = new()
    {
        Id = "quick_jobs", TitleKey = "HintQuickJobsTitle", TextKey = "HintQuickJobsText",
        Position = HintPosition.Below
    };

    // Prestige verfügbar (Level 50)
    public static readonly ContextualHint PrestigeHint = new()
    {
        Id = "prestige_hint", TitleKey = "HintPrestigeTitle", TextKey = "HintPrestigeText",
        Position = HintPosition.Below
    };

    // Gilden (erster Guild-Tab-Besuch)
    public static readonly ContextualHint GuildHint = new()
    {
        Id = "guild_hint", TitleKey = "HintGuildTitle", TextKey = "HintGuildText",
        Position = HintPosition.Below
    };

    // Crafting freigeschaltet
    public static readonly ContextualHint CraftingHint = new()
    {
        Id = "crafting_hint", TitleKey = "HintCraftingTitle", TextKey = "HintCraftingText",
        Position = HintPosition.Below
    };

    // Battle Pass verfügbar
    public static readonly ContextualHint BattlePass = new()
    {
        Id = "battle_pass", TitleKey = "HintBattlePassTitle", TextKey = "HintBattlePassText",
        Position = HintPosition.Below
    };

    // Glücksrad (Tag 2)
    public static readonly ContextualHint LuckySpin = new()
    {
        Id = "lucky_spin", TitleKey = "HintLuckySpinTitle", TextKey = "HintLuckySpinText",
        Position = HintPosition.Below
    };

    // Automatisierung freigeschaltet (Level 15)
    public static readonly ContextualHint Automation = new()
    {
        Id = "automation", TitleKey = "HintAutomationTitle", TextKey = "HintAutomationText",
        Position = HintPosition.Below
    };

    // Vorarbeiter freigeschaltet (Level 10)
    public static readonly ContextualHint ManagerUnlock = new()
    {
        Id = "manager_unlock", TitleKey = "HintManagerUnlockTitle", TextKey = "HintManagerUnlockText",
        Position = HintPosition.Below
    };

    // Meisterwerkzeuge freigeschaltet (Level 20)
    public static readonly ContextualHint MasterToolsUnlock = new()
    {
        Id = "master_tools_unlock", TitleKey = "HintMasterToolsUnlockTitle", TextKey = "HintMasterToolsUnlockText",
        Position = HintPosition.Below
    };

    // Ascension verfügbar (erstmals CanAscend == true, nach 3x Legende-Prestige)
    public static readonly ContextualHint AscensionAvailable = new()
    {
        Id = "ascension_available", TitleKey = "HintAscensionAvailableTitle", TextKey = "HintAscensionAvailableText",
        Position = HintPosition.Below, IsDialog = true
    };

    // Rebirth bereit (erster Workshop erreicht Level 1000)
    public static readonly ContextualHint RebirthReady = new()
    {
        Id = "rebirth_ready", TitleKey = "HintRebirthReadyTitle", TextKey = "HintRebirthReadyText",
        Position = HintPosition.Below, IsDialog = true
    };

    // Erster Stern (nach erstem Rebirth, erklärt Stern-Boni)
    public static readonly ContextualHint FirstStar = new()
    {
        Id = "first_star", TitleKey = "HintFirstStarTitle", TextKey = "HintFirstStarText",
        Position = HintPosition.Below, IsDialog = true
    };

    // Goldschrauben erklärt (beim ersten Erhalt der Premium-Währung)
    public static readonly ContextualHint GoldenScrews = new()
    {
        Id = "golden_screws", TitleKey = "HintGoldenScrewsTitle", TextKey = "HintGoldenScrewsText",
        Position = HintPosition.Below, IsDialog = true
    };

    // Aufträge erklärt (nach FirstWorkshop-Hint, leitet zum Auftrag-Annehmen)
    public static readonly ContextualHint AcceptOrder = new()
    {
        Id = "accept_order", TitleKey = "HintAcceptOrderTitle", TextKey = "HintAcceptOrderText",
        Position = HintPosition.Below
    };

    // ONB-1: Auftragstypen
    public static readonly ContextualHint OrderTypes = new()
    {
        Id = "order_types", TitleKey = "HintOrderTypesTitle", TextKey = "HintOrderTypesText",
        Position = HintPosition.Below, IsDialog = true
    };

    // ONB-2: Reputation
    public static readonly ContextualHint ReputationHint = new()
    {
        Id = "reputation_hint", TitleKey = "HintReputationTitle", TextKey = "HintReputationText",
        Position = HintPosition.Below, IsDialog = true
    };
}
