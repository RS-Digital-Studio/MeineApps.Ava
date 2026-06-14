using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// EconomyFeatureViewModel — Gesamt-Refresh State→UI inkl. Gebäude, Feature-Status, Reputation.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
internal sealed partial class EconomyFeatureViewModel
{
    internal void RefreshFromState()
    {
        var state = _gameStateService.State;

        // Update properties
        _host.HeaderVM.Money = state.Money;
        // Beim Start: sofort setzen, kein Ticken
        _host._displayedMoney = state.Money;
        _host._targetMoney = state.Money;
        _host.HeaderVM.MoneyDisplay = MainViewModel.FormatMoney(state.Money);
        _host.HeaderVM.IncomePerSecond = state.NetIncomePerSecond;
        _host.HeaderVM.IncomeDisplay = $"{MainViewModel.FormatMoney(state.NetIncomePerSecond)}/s";
        _host.UpdateNetIncomeHeader(state);
        _host.UpdateWorkerWarning(state);
        _host.HeaderVM.PlayerLevel = state.PlayerLevel;
        _host.HeaderVM.CurrentXp = state.CurrentXp;
        _host.HeaderVM.XpForNextLevel = state.XpForNextLevel;
        _host.HeaderVM.LevelProgress = state.LevelProgress;
        _host.HeaderVM.GoldenScrewsDisplay = state.GoldenScrews.ToString("N0");

        // Login-Streak aktualisieren
        _host.OnPropertyChanged(nameof(MainViewModel.LoginStreak));
        _host.OnPropertyChanged(nameof(MainViewModel.HasLoginStreak));
        _host.OnPropertyChanged(nameof(MainViewModel.ShowStreakBadge));

        // Automation-Unlock-Properties aktualisieren (Level-abhängig, wichtig nach Init + Prestige)
        _host.OnPropertyChanged(nameof(MainViewModel.IsAutoCollectUnlocked));
        _host.OnPropertyChanged(nameof(MainViewModel.IsAutoAcceptUnlocked));
        _host.OnPropertyChanged(nameof(MainViewModel.IsAutoAssignUnlocked));
        _host.OnPropertyChanged(nameof(MainViewModel.IsAutoClaimUnlocked));

        // Rush/Delivery/MasterTools + Boost-Indikator
        UpdateRushDisplay();
        UpdateBoostIndicator();
        UpdateDeliveryDisplay();
        _host.MissionsVM.MasterToolsCollected = state.CollectedMasterTools.Count;
        _host.MissionsVM.MasterToolsTotal = MasterTool.GetAllDefinitions().Count;
        var totalMtBonus = MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools);
        _host.MissionsVM.MasterToolsBonusDisplay = totalMtBonus > 0 ? $"+{(int)(totalMtBonus * 100)}%" : "";

        // Prestige-Shop ab bestimmtem Level (oder wenn bereits prestigiert → Shop bleibt zugänglich nach Reset)
        _host.IsPrestigeShopUnlocked = state.PlayerLevel >= LevelThresholds.PrestigeShopUnlock || state.Prestige.TotalPrestigeCount > 0;

        // Statische Renderer-Strings initialisieren (Karten-Texte)
        WorkshopGameCardRenderer.UpdateLocalizedStrings(
            _localizationService.GetString("TapToUnlock") ?? "Tap to unlock",
            _localizationService.GetString("AtLevelShort") ?? "From Level {0}");

        // Refresh workshops
        RefreshWorkshops();

        // Tutorial-Hint: Pulsierender Rahmen solange FirstWorkshop-Hint noch nicht gesehen
        // Nach Prestige (Level zurück auf 1) nicht erneut anzeigen
        _host.ShowTutorialHint = !_contextualHintService.HasSeenHint(ContextualHints.FirstWorkshop.Id)
                           && state.PlayerLevel < LevelThresholds.TutorialHintMaxLevel
                           && state.Prestige.TotalPrestigeCount == 0;

        // Refresh orders
        RefreshOrders();

        // Check for active order
        _host.HasActiveOrder = state.ActiveOrder != null;
        _host.ActiveOrder = state.ActiveOrder;
    }

    // Statisch gecacht: BuildingType-Enum hat feste Groesse (aendert sich nicht zur Laufzeit)
    private static readonly int s_totalBuildingCount = Enum.GetValues<BuildingType>().Length;

    /// <summary>
    /// Aktualisiert die Gebaeude-Zusammenfassung (Task #5).
    /// </summary>
    private void RefreshBuildingsSummary(GameState state)
    {
        // For-Schleife statt LINQ .Count() (vermeidet Enumerator+Closure pro Sekunde)
        int builtCount = 0;
        for (int i = 0; i < state.Buildings.Count; i++)
            if (state.Buildings[i].IsBuilt) builtCount++;
        var builtLabel = _localizationService.GetString("Built") ?? "gebaut";
        var buildingsLabel = _localizationService.GetString("Buildings") ?? "Buildings";
        _host.BuildingsSummary = $"{s_totalBuildingCount} {buildingsLabel}, {builtCount} {builtLabel}";
    }

    /// <summary>
    /// Aktualisiert die Feature-Button Status-Texte.
    /// </summary>
    private void RefreshFeatureStatusTexts(GameState state)
    {
        // Arbeiter (For-Schleife statt LINQ Sum - weniger GC-Pressure)
        int totalWorkers = 0;
        for (int i = 0; i < state.Workshops.Count; i++)
            totalWorkers += state.Workshops[i].Workers.Count;
        _host.WorkersStatusText = string.Format(
            _localizationService.GetString("WorkersStatus") ?? "{0} angestellt",
            totalWorkers);

        // Forschung (For-Schleife statt LINQ Count)
        int completedResearch = 0;
        for (int i = 0; i < state.Researches.Count; i++)
            if (state.Researches[i].IsResearched) completedResearch++;
        if (!string.IsNullOrEmpty(state.ActiveResearchId))
        {
            var researchName = _localizationService.GetString($"Research_{state.ActiveResearchId}") ?? state.ActiveResearchId;
            _host.ResearchStatusText = string.Format(
                _localizationService.GetString("ResearchActiveStatus") ?? "Researching: {0}",
                researchName);
        }
        else
        {
            _host.ResearchStatusText = string.Format(
                _localizationService.GetString("ResearchStatus") ?? "{0}/45 erforscht",
                completedResearch);
        }

        // Vorarbeiter (For-Schleife statt LINQ Count)
        int activeManagers = 0;
        for (int i = 0; i < state.Managers.Count; i++)
            if (state.Managers[i].IsUnlocked) activeManagers++;
        _host.ManagerStatusText = string.Format(
            _localizationService.GetString("ManagerStatus") ?? "{0} aktiv",
            activeManagers);

        // Progressive Disclosure: Missionen-Sub-Features (Dead Zone Lv40-80 schließen)
        // Nach Prestige: Einmal freigeschaltete Features bleiben sichtbar (Prestige-Count > 0 = war schon mal dort)
        bool hasPrestiged = state.Prestige.TotalPrestigeCount > 0;
        _host.ShowTournamentSection = state.PlayerLevel >= LevelThresholds.TournamentSection || hasPrestiged;
        _host.ShowSeasonalEventSection = state.PlayerLevel >= LevelThresholds.SeasonalEventSection || hasPrestiged;
        _host.ShowBattlePassSection = state.PlayerLevel >= LevelThresholds.BattlePassSection || hasPrestiged;

        // Turnier
        if (state.CurrentTournament != null)
        {
            var remainingEntries = state.CurrentTournament.FreeEntriesRemaining;
            _host.TournamentStatusText = string.Format(
                _localizationService.GetString("TournamentStatus") ?? "{0} Versuche",
                remainingEntries);
        }
        else
        {
            _host.TournamentStatusText = "";
        }

        // Saison-Event
        if (state.CurrentSeasonalEvent != null)
        {
            var seasonKey = state.CurrentSeasonalEvent.Season.ToString();
            _host.SeasonalEventStatusText = _localizationService.GetString(seasonKey) ?? seasonKey;
        }
        else
        {
            _host.SeasonalEventStatusText = "";
        }

        // Saison-Pass
        _host.BattlePassStatusText = string.Format(
            _localizationService.GetString("BattlePassStatus") ?? "Tier {0}/{1}",
            state.BattlePass.CurrentTier, 30);

        // Produktion
        var activeCrafts = state.ActiveCraftingJobs.Count;
        _host.CraftingStatusText = string.Format(
            _localizationService.GetString("CraftingStatus") ?? "{0} in Produktion",
            activeCrafts);
    }

    /// <summary>
    /// Aktualisiert Reputation-Anzeige (Task #6).
    /// </summary>
    internal void RefreshReputation(GameState state)
    {
        var score = state.Reputation.ReputationScore;
        _host.ReputationScore = score;
        _host.ReputationColor = score switch
        {
            < 30 => "#EF4444",  // Rot
            < 60 => "#F59E0B",  // Gelb
            < 80 => "#22C55E",  // Grün
            _ => "#FFD700"      // Gold
        };
        _host.OnPropertyChanged(nameof(MainViewModel.ShowReputationBadge));

        // ONB-2: Reputation-Hint
        if (_host.ShowReputationBadge)
            _contextualHintService.TryShowHint(ContextualHints.ReputationHint);
    }
}
