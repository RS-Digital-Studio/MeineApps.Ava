using System.Collections.Generic;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Helpers;

/// <summary>
/// P1.5 AAA-Audit (08.05.2026 Sprint A — Helper-Variante):
///
/// Sicherheits-konservative Variante der PageNavigationViewModel-Extraktion. Statt
/// die kompletten 38 IsXxxActive-Properties zu extrahieren (was 49 AXAML-Files
/// patchen würde — hohes Regressions-Risiko), zieht dieser Helper nur die statische
/// Logik aus MainViewModel:
///
/// - <see cref="MainTabs"/>: Set der 5 Top-Level-Tabs
/// - <see cref="GetPropertyNameFor"/>: Mapping ActivePage → IsXxxActive-Property-Name
/// - <see cref="IsTabToTabTransition"/>: Tab-Wechsel-Erkennung (für Stack-Management)
/// - <see cref="ManageStack"/>: Push/Pop/Clear-Logik mit Cap
///
/// Effekt: ~70 Zeilen aus MainViewModel raus, AXAML unverändert, kein Regressions-Risiko.
/// Die volle MainViewModel-Reduktion (Sprint A im Audit) ist ein eigener 3-5-Tage-Sprint.
/// </summary>
internal static class PageNavigationHelper
{
    /// <summary>5 Top-Level-Tabs (kein Sub-Stack, keine Back-History).</summary>
    public static readonly HashSet<ActivePage> MainTabs =
    [
        ActivePage.Dashboard, ActivePage.Buildings, ActivePage.Missionen,
        ActivePage.Guild, ActivePage.Shop
    ];

    /// <summary>True wenn die Navigation zwischen Top-Level-Tabs (oder Settings) erfolgt.</summary>
    public static bool IsTabToTabTransition(ActivePage from, ActivePage to)
    {
        if (MainTabs.Contains(from) && MainTabs.Contains(to)) return true;
        if (from == ActivePage.Settings && MainTabs.Contains(to)) return true;
        if (MainTabs.Contains(from) && to == ActivePage.Settings) return true;
        return false;
    }

    /// <summary>Mapping ActivePage → Property-Name für gezielte PropertyChanged-Notifications.</summary>
    public static string? GetPropertyNameFor(ActivePage page) => page switch
    {
        ActivePage.Dashboard => "IsDashboardActive",
        ActivePage.Shop => "IsShopActive",
        ActivePage.Statistics => "IsStatisticsActive",
        ActivePage.Achievements => "IsAchievementsActive",
        ActivePage.Settings => "IsSettingsActive",
        ActivePage.WorkshopDetail => "IsWorkshopDetailActive",
        ActivePage.OrderDetail => "IsOrderDetailActive",
        ActivePage.SawingGame => "IsSawingGameActive",
        ActivePage.PipePuzzle => "IsPipePuzzleActive",
        ActivePage.WiringGame => "IsWiringGameActive",
        ActivePage.PaintingGame => "IsPaintingGameActive",
        ActivePage.RoofTilingGame => "IsRoofTilingGameActive",
        ActivePage.BlueprintGame => "IsBlueprintGameActive",
        ActivePage.DesignPuzzleGame => "IsDesignPuzzleGameActive",
        ActivePage.InspectionGame => "IsInspectionGameActive",
        ActivePage.WorkerMarket => "IsWorkerMarketActive",
        ActivePage.Buildings => "IsBuildingsActive",
        ActivePage.Research => "IsResearchActive",
        ActivePage.Manager => "IsManagerActive",
        ActivePage.Tournament => "IsTournamentActive",
        ActivePage.SeasonalEvent => "IsSeasonalEventActive",
        ActivePage.BattlePass => "IsBattlePassActive",
        ActivePage.Guild => "IsGuildActive",
        ActivePage.Missionen => "IsMissionenActive",
        ActivePage.GuildResearch => "IsGuildResearchActive",
        ActivePage.GuildMembers => "IsGuildMembersActive",
        ActivePage.GuildInvite => "IsGuildInviteActive",
        ActivePage.GuildWarSeason => "IsGuildWarSeasonActive",
        ActivePage.GuildBoss => "IsGuildBossActive",
        ActivePage.GuildHall => "IsGuildHallActive",
        ActivePage.GuildAchievements => "IsGuildAchievementsActive",
        ActivePage.GuildChat => "IsGuildChatActive",
        ActivePage.GuildWar => "IsGuildWarActive",
        ActivePage.Crafting => "IsCraftingActive",
        ActivePage.ForgeGame => "IsForgeGameActive",
        ActivePage.InventGame => "IsInventGameActive",
        ActivePage.Ascension => "IsAscensionActive",
        ActivePage.Prestige => "IsPrestigeActive",
        _ => null
    };

    /// <summary>
    /// Verwaltet den Navigation-Stack: Push bei Sub-Navigation, Clear bei Tab-Wechsel.
    /// Idempotent + cap-geschützt.
    ///
    /// Code-Review-Fix [Finding 5]: Cap-Handling jetzt in <see cref="CappedNavigationStack"/>
    /// O(1) statt O(n)-Rebuild pro Navigation.
    /// </summary>
    public static void ManageStack(CappedNavigationStack stack, ActivePage from, ActivePage to, bool isNavigatingBack)
    {
        if (isNavigatingBack || from == to) return;

        if (IsTabToTabTransition(from, to))
        {
            stack.Clear();
            return;
        }

        // Cap-Drop in O(1) — der Ringbuffer ueberschreibt den aeltesten Eintrag still.
        stack.Push(from);
    }
}
