using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models.Dungeon;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für den Dungeon-Run Roguelike-Modus.
/// 3 Zustände: PreRun (Start-Screen), BuffSelection (nach Floor-Abschluss), PostRun (Zusammenfassung).
/// </summary>
public partial class DungeonViewModel : ObservableObject, INavigable, IGameJuiceEmitter
{
    private readonly IDungeonService _dungeonService;
    private readonly IDungeonUpgradeService _dungeonUpgradeService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ILocalizationService _localizationService;
    private readonly IRewardedAdService _rewardedAdService;

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    /// <summary>Event für Ad-basierte Runs (ViewModel weiß nicht ob Ad erfolgreich)</summary>
    public event Action? AdRunRequested;

    /// <summary>Event für Dungeon-Floor-Start (floor, seed)</summary>
    public event Action<int, int>? StartDungeonFloorRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty] private string _title = "Dungeon Run";
    [ObservableProperty] private string _currentFloorText = "Floor 1";
    [ObservableProperty] private string _livesText = "1";
    [ObservableProperty] private string _totalRunsText = "0";
    [ObservableProperty] private string _bestFloorText = "0";
    [ObservableProperty] private string _startButtonText = "Starten (Gratis)";
    [ObservableProperty] private string _coinEntryText = "500 Coins";
    [ObservableProperty] private string _gemEntryText = "10 Gems";
    [ObservableProperty] private string _adEntryText = "Werbung";
    [ObservableProperty] private bool _canStartFree;
    [ObservableProperty] private bool _canStartPaid = true;
    [ObservableProperty] private bool _canStartAd = true;
    [ObservableProperty] private bool _isPreRun = true;
    [ObservableProperty] private bool _isBuffSelection;
    [ObservableProperty] private bool _isPostRun;
    [ObservableProperty] private List<DungeonBuffDisplayItem> _buffChoices = [];
    [ObservableProperty] private string _summaryFloorsText = "";
    [ObservableProperty] private string _summaryCoinsText = "";
    [ObservableProperty] private string _summaryGemsText = "";
    [ObservableProperty] private string _summaryCardsText = "";
    [ObservableProperty] private string _summaryNewBestText = "";
    [ObservableProperty] private string _summaryDungeonCoinsText = "";

    /// <summary>Ob ein Extra-Buff per Werbung verfügbar ist (nach Buff-Auswahl)</summary>
    [ObservableProperty] private bool _canWatchAdForExtraBuff;

    /// <summary>Ob Wiederbelebung für Gems möglich ist (Run aktiv + Lives == 0)</summary>
    [ObservableProperty] private bool _canReviveForGems;

    /// <summary>Lokalisierter Text für den Revive-Button</summary>
    [ObservableProperty] private string _reviveForGemsText = "";

    // === Permanente Upgrades (B1) ===
    [ObservableProperty] private string _dungeonCoinBalanceText = "0";
    [ObservableProperty] private ObservableCollection<DungeonUpgradeDisplayItem> _upgradeItems = [];

    // === Raum-Typ + Modifikator (B3+B4) ===
    [ObservableProperty] private string _currentRoomTypeText = "";
    [ObservableProperty] private string _currentRoomTypeColor = "#FFFFFF";
    [ObservableProperty] private string _currentModifierText = "";
    [ObservableProperty] private string _currentModifierColor = "#FFFFFF";

    // === Node-Map (B6) ===
    [ObservableProperty] private bool _isMapSelection;
    [ObservableProperty] private DungeonMapData? _mapData;
    [ObservableProperty] private int _mapCurrentFloor;
    [ObservableProperty] private float _mapAnimationTime;

    // === Reroll + Synergy (B5) ===
    [ObservableProperty] private bool _canReroll;
    [ObservableProperty] private string _rerollCostText = "";
    [ObservableProperty] private List<DungeonSynergyDisplayItem> _activeSynergies = [];

    // === Ascension (B7) ===
    [ObservableProperty] private string _ascensionLevelText = "";
    [ObservableProperty] private string _ascensionBadgeText = "";
    [ObservableProperty] private string _ascensionColor = "#FF9800";
    [ObservableProperty] private bool _hasAscension;
    [ObservableProperty] private string _summaryAscensionText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public DungeonViewModel(
        IDungeonService dungeonService,
        IDungeonUpgradeService dungeonUpgradeService,
        ICoinService coinService,
        IGemService gemService,
        ILocalizationService localizationService,
        IRewardedAdService rewardedAdService)
    {
        _dungeonService = dungeonService;
        _dungeonUpgradeService = dungeonUpgradeService;
        _coinService = coinService;
        _gemService = gemService;
        _localizationService = localizationService;
        _rewardedAdService = rewardedAdService;

        _dungeonUpgradeService.BalanceChanged += () => RefreshUpgrades();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        RefreshState();
        RefreshUpgrades();
        UpdateLocalizedTexts();
    }

    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("DungeonTitle") ?? "Dungeon Run";
        StartButtonText = _localizationService.GetString("DungeonStartFree") ?? "Start (Free)";
        CoinEntryText = string.Format(
            _localizationService.GetString("DungeonStartCoins") ?? "{0} Coins",
            _dungeonService.PaidRunCoinCost);
        GemEntryText = string.Format(
            _localizationService.GetString("DungeonStartGems") ?? "{0} Gems",
            _dungeonService.PaidRunGemCost);
        AdEntryText = _localizationService.GetString("DungeonStartAd") ?? "Watch Ad";
        ReviveForGemsText = _localizationService.GetString("DungeonReviveGems") ?? "Revive (15 Gems)";

        // Stats-Texte aktualisieren
        var stats = _dungeonService.Stats;
        TotalRunsText = string.Format(
            _localizationService.GetString("DungeonTotalRuns") ?? "Runs: {0}", stats.TotalRuns);
        BestFloorText = string.Format(
            _localizationService.GetString("DungeonBestFloor") ?? "Best Floor: {0}", stats.BestFloor);

        // Aktiver Run-Zustand
        var runState = _dungeonService.RunState;
        if (runState is { IsActive: true })
        {
            CurrentFloorText = string.Format(
                _localizationService.GetString("DungeonFloor") ?? "Floor {0}", runState.CurrentFloor);
            LivesText = string.Format(
                _localizationService.GetString("DungeonLives") ?? "Lives: {0}", runState.Lives);

            // Raum-Typ + Modifikator
            UpdateRoomTypeDisplay(runState.CurrentRoomType);
            UpdateModifierDisplay(runState.CurrentModifier);
        }
        else
        {
            CurrentFloorText = string.Format(
                _localizationService.GetString("DungeonFloor") ?? "Floor {0}", 1);
            LivesText = string.Format(
                _localizationService.GetString("DungeonLives") ?? "Lives: {0}", 1);
            CurrentRoomTypeText = "";
            CurrentModifierText = "";
        }
    }

    public void RefreshState()
    {
        // CanStart-Flags aktualisieren
        CanStartFree = _dungeonService.CanStartFreeRun;
        CanStartPaid = _coinService.CanAfford(_dungeonService.PaidRunCoinCost)
                       || _gemService.CanAfford(_dungeonService.PaidRunGemCost);
        CanStartAd = _dungeonService.CanStartAdRun && RewardedAdCooldownTracker.CanShowAd;

        // Stats
        var stats = _dungeonService.Stats;
        TotalRunsText = string.Format(
            _localizationService.GetString("DungeonTotalRuns") ?? "Runs: {0}", stats.TotalRuns);
        BestFloorText = string.Format(
            _localizationService.GetString("DungeonBestFloor") ?? "Best Floor: {0}", stats.BestFloor);

        // Ascension
        UpdateAscensionDisplay();

        // Zustand zurücksetzen auf PreRun wenn kein aktiver Run
        if (!_dungeonService.IsRunActive)
        {
            IsPreRun = true;
            IsBuffSelection = false;
            IsPostRun = false;
            IsMapSelection = false;
        }
    }

    private void UpdateRoomTypeDisplay(DungeonRoomType roomType)
    {
        (CurrentRoomTypeText, CurrentRoomTypeColor) = roomType switch
        {
            DungeonRoomType.Elite => (_localizationService.GetString("DungeonRoomElite") ?? "Elite", "#F44336"),
            DungeonRoomType.Treasure => (_localizationService.GetString("DungeonRoomTreasure") ?? "Treasure", "#FFD700"),
            DungeonRoomType.Challenge => (_localizationService.GetString("DungeonRoomChallenge") ?? "Challenge", "#FF9800"),
            DungeonRoomType.Rest => (_localizationService.GetString("DungeonRoomRest") ?? "Rest", "#4CAF50"),
            _ => ("", "#FFFFFF")
        };
    }

    private void UpdateModifierDisplay(DungeonFloorModifier modifier)
    {
        (CurrentModifierText, CurrentModifierColor) = modifier switch
        {
            DungeonFloorModifier.LavaBorders => (_localizationService.GetString("DungeonModLava") ?? "Lava Borders", "#FF5722"),
            DungeonFloorModifier.Darkness => (_localizationService.GetString("DungeonModDarkness") ?? "Darkness", "#7878A0"),
            DungeonFloorModifier.DoubleSpawns => (_localizationService.GetString("DungeonModDoubleSpawns") ?? "Double Spawns", "#F44336"),
            DungeonFloorModifier.FastBombs => (_localizationService.GetString("DungeonModFastBombs") ?? "Fast Bombs", "#FFC107"),
            DungeonFloorModifier.BigExplosions => (_localizationService.GetString("DungeonModBigExplosions") ?? "Big Explosions", "#FF9800"),
            DungeonFloorModifier.Regeneration => (_localizationService.GetString("DungeonModRegeneration") ?? "Regeneration", "#4CAF50"),
            DungeonFloorModifier.Wealthy => (_localizationService.GetString("DungeonModWealthy") ?? "Wealthy", "#FFD700"),
            _ => ("", "#FFFFFF")
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void StartFreeRun()
    {
        if (!_dungeonService.StartRun(DungeonEntryType.Free)) return;
        ShowMapAfterRunStart();
    }

    [RelayCommand]
    private void StartCoinRun()
    {
        if (!_dungeonService.StartRun(DungeonEntryType.Coins)) return;
        ShowMapAfterRunStart();
    }

    [RelayCommand]
    private void StartGemRun()
    {
        if (!_dungeonService.StartRun(DungeonEntryType.Gems)) return;
        ShowMapAfterRunStart();
    }

    [RelayCommand]
    private void StartAdRun()
    {
        // ViewModel weiß nicht ob Ad erfolgreich - Aufruf delegieren
        AdRunRequested?.Invoke();
    }

    /// <summary>
    /// Wird extern aufgerufen wenn Ad erfolgreich war (z.B. aus MainViewModel)
    /// </summary>
    public void OnAdRunRewarded()
    {
        if (!_dungeonService.StartRun(DungeonEntryType.Ad)) return;
        ShowMapAfterRunStart();
    }

    /// <summary>
    /// Wählt einen Node auf der Map aus und startet den Floor.
    /// </summary>
    [RelayCommand]
    private void SelectMapNode(DungeonMapNode? node)
    {
        if (node == null || !node.IsReachable || node.IsCompleted) return;

        var runState = _dungeonService.RunState;
        if (runState is not { IsActive: true }) return;

        _dungeonService.SelectMapNode(node.Floor, node.Column);

        IsMapSelection = false;
        NavigateToFloor(runState.CurrentFloor, runState.RunSeed);
    }

    /// <summary>
    /// Zeigt die Map-Auswahl nach Run-Start oder Floor-Complete (wenn mehrere Nodes erreichbar).
    /// </summary>
    private void ShowMapAfterRunStart()
    {
        var runState = _dungeonService.RunState;
        if (runState?.MapData == null)
        {
            // Kein MapData (Fallback): Direkt zum Floor
            if (runState is { IsActive: true })
                NavigateToFloor(runState.CurrentFloor, runState.RunSeed);
            return;
        }

        // Prüfen ob der aktuelle Floor nur 1 erreichbaren Node hat
        if (runState.CurrentFloor <= runState.MapData.Rows.Count)
        {
            var row = runState.MapData.Rows[runState.CurrentFloor - 1];
            var reachable = row.FindAll(n => n.IsReachable);

            if (reachable.Count <= 1)
            {
                // Einziger Node: Direkt los (ist bereits in StartRun/CompleteFloor auto-selected)
                NavigateToFloor(runState.CurrentFloor, runState.RunSeed);
                return;
            }
        }

        // Mehrere Nodes: Map-Auswahl anzeigen
        ShowMapSelection();
    }

    /// <summary>
    /// Zeigt die Map-Ansicht zur Node-Auswahl.
    /// </summary>
    private void ShowMapSelection()
    {
        var runState = _dungeonService.RunState;
        if (runState?.MapData == null) return;

        IsPreRun = false;
        IsBuffSelection = false;
        IsPostRun = false;
        IsMapSelection = true;

        MapData = runState.MapData;
        MapCurrentFloor = runState.CurrentFloor;

        UpdateLocalizedTexts();
    }

    [RelayCommand]
    private void SelectBuff(DungeonBuffType type)
    {
        _dungeonService.ApplyBuff(type);

        // Buff-Name für Feedback-Text suchen
        var buffDef = DungeonBuffCatalog.Find(type);
        if (buffDef != null)
        {
            var buffName = _localizationService.GetString(buffDef.NameKey) ?? buffDef.NameKey;
            FloatingTextRequested?.Invoke($"+{buffName}", "success");
        }

        // Synergien aktualisieren nach Buff-Auswahl
        UpdateSynergies();

        // Nach Buff-Auswahl: Extra-Buff per Ad anbieten (1x pro Buff-Phase, Cooldown beachten)
        CanWatchAdForExtraBuff = _rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd;

        // Hinweis wenn Ad nicht verfügbar, trotzdem Buff-Phase anzeigen mit Skip-Button
        if (!CanWatchAdForExtraBuff)
        {
            FloatingTextRequested?.Invoke(
                _localizationService.GetString("AdUnavailable") ?? "Ad not available",
                "warning");
            ContinueDungeon();
        }
    }

    /// <summary>
    /// Extra-Buff per Rewarded Ad (nach normaler Buff-Auswahl).
    /// Wählt zufällig einen der nicht-gewählten Buffs aus.
    /// </summary>
    [RelayCommand]
    private async Task WatchAdForExtraBuff()
    {
        if (!CanWatchAdForExtraBuff) return;

        CanWatchAdForExtraBuff = false;

        var success = await _rewardedAdService.ShowAdAsync("dungeon_extra_buff");
        if (success)
        {
            RewardedAdCooldownTracker.RecordAdShown();
            // Zufälligen Extra-Buff aus dem Pool wählen
            var extraChoices = _dungeonService.GenerateBuffChoices();
            if (extraChoices.Count > 0)
            {
                var random = new Random();
                var extraBuff = extraChoices[random.Next(extraChoices.Count)];
                _dungeonService.ApplyBuff(extraBuff.Type);

                var buffName = _localizationService.GetString(extraBuff.NameKey) ?? extraBuff.NameKey;
                FloatingTextRequested?.Invoke($"+{buffName}", "success");
            }
        }

        // Weiter zum nächsten Floor (unabhängig vom Ad-Ergebnis)
        ContinueDungeon();
    }

    /// <summary>Buff-Phase überspringen und direkt zum nächsten Floor</summary>
    [RelayCommand]
    private void SkipExtraBuff()
    {
        CanWatchAdForExtraBuff = false;
        ContinueDungeon();
    }

    [RelayCommand]
    private void ContinueDungeon()
    {
        var runState = _dungeonService.RunState;
        if (runState is not { IsActive: true }) return;

        IsBuffSelection = false;

        // Prüfen ob Map-Auswahl nötig (mehrere erreichbare Nodes)
        if (runState.MapData != null && runState.CurrentFloor <= runState.MapData.Rows.Count)
        {
            var row = runState.MapData.Rows[runState.CurrentFloor - 1];
            var reachable = row.FindAll(n => n.IsReachable);
            if (reachable.Count > 1)
            {
                ShowMapSelection();
                return;
            }
        }

        NavigateToFloor(runState.CurrentFloor, runState.RunSeed);
    }

    /// <summary>Wiederbelebung für 15 Gems (wenn Run aktiv und Lives == 0)</summary>
    [RelayCommand]
    private void ReviveForGems()
    {
        var runState = _dungeonService.RunState;
        if (runState is not { IsActive: true } || runState.Lives > 0) return;

        if (!_gemService.TrySpendGems(15))
        {
            FloatingTextRequested?.Invoke(
                _localizationService.GetString("InsufficientGems") ?? "Not enough Gems",
                "warning");
            return;
        }

        // Leben auf 1 setzen
        runState.Lives = 1;
        CanReviveForGems = false;

        FloatingTextRequested?.Invoke("-15 Gems", "cyan");
        FloatingTextRequested?.Invoke(
            _localizationService.GetString("DungeonRevived") ?? "Revived!",
            "success");

        // Texte aktualisieren
        LivesText = string.Format(
            _localizationService.GetString("DungeonLives") ?? "Lives: {0}", runState.Lives);
    }

    [RelayCommand]
    private void BuyUpgrade(string upgradeId)
    {
        if (!_dungeonUpgradeService.TryBuyUpgrade(upgradeId)) return;

        var def = DungeonUpgradeCatalog.Find(upgradeId);
        if (def != null)
        {
            var name = _localizationService.GetString(def.NameKey) ?? def.NameKey;
            FloatingTextRequested?.Invoke($"+{name}", "success");
        }

        RefreshUpgrades();
    }

    /// <summary>Buff-Auswahl rerollt (1x gratis pro Run, danach 5 Gems)</summary>
    [RelayCommand]
    private void RerollBuffs()
    {
        var runState = _dungeonService.RunState;
        if (runState is not { IsActive: true }) return;

        bool isFreeReroll = runState.FreeRerollsUsed < 1;

        if (!isFreeReroll)
        {
            const int rerollGemCost = 5;
            if (!_gemService.TrySpendGems(rerollGemCost))
            {
                FloatingTextRequested?.Invoke(
                    _localizationService.GetString("InsufficientGems") ?? "Not enough Gems",
                    "warning");
                return;
            }
            FloatingTextRequested?.Invoke($"-{rerollGemCost} Gems", "cyan");
        }

        runState.FreeRerollsUsed++;
        _dungeonService.PersistRunState();

        // Neue Buffs generieren
        var newChoices = _dungeonService.GenerateBuffChoices();
        ShowBuffSelection(newChoices);
        UpdateRerollState();
    }

    [RelayCommand]
    private void Back() => NavigationRequested?.Invoke(new GoBack());

    // ═══════════════════════════════════════════════════════════════════════
    // DUNGEON-FLOOR CALLBACKS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wird von MainViewModel aufgerufen wenn ein Dungeon-Floor abgeschlossen wurde.
    /// Prüft ob Buff-Auswahl nötig ist oder direkt weiter.
    /// </summary>
    public void OnDungeonFloorComplete()
    {
        // Floor-Belohnung berechnen und zum nächsten Floor vorrücken
        var reward = _dungeonService.CompleteFloor();

        // Belohnungs-Feedback
        if (reward.Coins > 0)
            FloatingTextRequested?.Invoke($"+{reward.Coins} Coins", "coin");
        if (reward.Gems > 0)
            FloatingTextRequested?.Invoke($"+{reward.Gems} Gems", "gem");
        if (reward.WasBossFloor)
            CelebrationRequested?.Invoke();

        // Prüfen ob Buff-Floor
        if (_dungeonService.IsBuffFloorNext)
        {
            var choices = _dungeonService.GenerateBuffChoices();
            ShowBuffSelection(choices);
        }
        else
        {
            // Direkt zum nächsten Floor weiter
            ContinueDungeon();
        }
    }

    /// <summary>
    /// Wird von MainViewModel aufgerufen wenn der Spieler im Dungeon stirbt/aufgibt.
    /// </summary>
    public void OnDungeonRunEnded()
    {
        CanReviveForGems = false;
        var summary = _dungeonService.EndRun();
        ShowPostRun(summary);
    }

    /// <summary>
    /// Wird aufgerufen wenn der Spieler im Dungeon alle Leben verloren hat (vor EndRun).
    /// Bietet Revive für Gems an.
    /// </summary>
    public void OnDungeonPlayerDied()
    {
        var runState = _dungeonService.RunState;
        if (runState is { IsActive: true, Lives: 0 })
        {
            CanReviveForGems = _gemService.CanAfford(15);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STATE-MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeigt die Buff-Auswahl nach einem Floor-Abschluss an
    /// </summary>
    public void ShowBuffSelection(List<DungeonBuffDefinition> choices)
    {
        IsPreRun = false;
        IsBuffSelection = true;
        IsPostRun = false;

        // DungeonBuffDefinition → DungeonBuffDisplayItem konvertieren
        var items = new List<DungeonBuffDisplayItem>(choices.Count);
        for (int i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            items.Add(new DungeonBuffDisplayItem
            {
                Type = choice.Type,
                Name = _localizationService.GetString(choice.NameKey) ?? choice.NameKey,
                Description = _localizationService.GetString(choice.DescKey) ?? choice.DescKey,
                IconName = choice.IconName,
                RarityText = GetLocalizedRarityText(choice.Rarity),
                RarityColor = GetRarityColor(choice.Rarity),
                CardIndex = i,
                RarityGlowOpacity = GetRarityGlowOpacity(choice.Rarity)
            });
        }

        BuffChoices = items;
        UpdateRerollState();
        UpdateSynergies();
    }

    /// <summary>
    /// Zeigt die Run-Zusammenfassung nach Tod oder Aufgabe
    /// </summary>
    public void ShowPostRun(DungeonRunSummary summary)
    {
        IsPreRun = false;
        IsBuffSelection = false;
        IsPostRun = true;

        SummaryFloorsText = string.Format(
            _localizationService.GetString("DungeonSummaryFloors") ?? "Floors: {0}", summary.FloorsCompleted);
        SummaryCoinsText = string.Format(
            _localizationService.GetString("DungeonSummaryCoins") ?? "Coins: {0}", summary.TotalCoins);
        SummaryGemsText = string.Format(
            _localizationService.GetString("DungeonSummaryGems") ?? "Gems: {0}", summary.TotalGems);
        SummaryCardsText = string.Format(
            _localizationService.GetString("DungeonSummaryCards") ?? "Cards: {0}", summary.TotalCards);
        SummaryDungeonCoinsText = summary.TotalDungeonCoins > 0
            ? string.Format(
                _localizationService.GetString("DungeonSummaryDC") ?? "Dungeon Coins: {0}", summary.TotalDungeonCoins)
            : "";
        SummaryNewBestText = summary.IsNewBestFloor
            ? _localizationService.GetString("DungeonNewBest") ?? "New Record!"
            : "";

        // Ascension Level-Up
        SummaryAscensionText = summary.AscensionLevelUp
            ? string.Format(
                _localizationService.GetString("DungeonAscensionUp") ?? "Ascension {0} Unlocked!",
                summary.NewAscensionLevel)
            : "";

        if (summary.IsNewBestFloor || summary.AscensionLevelUp)
            CelebrationRequested?.Invoke();

        // Stats aktualisieren
        RefreshState();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Aktualisiert die Ascension-Anzeige basierend auf dem aktuellen Level</summary>
    private void UpdateAscensionDisplay()
    {
        int ascension = _dungeonService.CurrentAscension;
        HasAscension = ascension > 0;

        if (!HasAscension)
        {
            AscensionLevelText = "";
            AscensionBadgeText = "";
            AscensionColor = "#FF9800";
            return;
        }

        AscensionLevelText = string.Format(
            _localizationService.GetString("DungeonAscension") ?? "Ascension {0}", ascension);

        // Sterne als Badge-Text (1-5 Flammen-Symbole per Ascension-Level)
        AscensionBadgeText = ascension switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            _ => ""
        };

        // Farbe eskaliert mit Level
        AscensionColor = ascension switch
        {
            1 => "#FF9800",  // Orange
            2 => "#F44336",  // Rot
            3 => "#E040FB",  // Magenta
            4 => "#9C27B0",  // Violett
            5 => "#FFD700",  // Gold (Nightmare)
            _ => "#FF9800"
        };
    }

    private void RefreshUpgrades()
    {
        DungeonCoinBalanceText = _dungeonUpgradeService.DungeonCoinBalance.ToString("N0");

        var allUpgrades = _dungeonUpgradeService.GetAllUpgrades();
        var items = new ObservableCollection<DungeonUpgradeDisplayItem>();

        foreach (var (def, currentLevel) in allUpgrades)
        {
            bool isMaxed = currentLevel >= def.MaxLevel;
            int nextCost = isMaxed ? 0 : def.CostsPerLevel[currentLevel];

            items.Add(new DungeonUpgradeDisplayItem
            {
                UpgradeId = def.Id,
                Name = _localizationService.GetString(def.NameKey) ?? def.NameKey,
                Description = _localizationService.GetString(def.DescKey) ?? def.DescKey,
                IconName = def.IconName,
                CurrentLevel = currentLevel,
                MaxLevel = def.MaxLevel,
                NextCost = nextCost,
                IsMaxed = isMaxed,
                CanBuy = _dungeonUpgradeService.CanBuyUpgrade(def.Id),
                LevelText = isMaxed
                    ? $"MAX ({currentLevel}/{def.MaxLevel})"
                    : $"{currentLevel}/{def.MaxLevel}",
                CostText = isMaxed ? "MAX" : $"{nextCost} DC"
            });
        }

        UpgradeItems = items;
    }

    private void UpdateRerollState()
    {
        var runState = _dungeonService.RunState;
        if (runState is not { IsActive: true } || !IsBuffSelection)
        {
            CanReroll = false;
            return;
        }

        bool isFree = runState.FreeRerollsUsed < 1;
        CanReroll = isFree || _gemService.CanAfford(5);
        RerollCostText = isFree
            ? _localizationService.GetString("DungeonRerollFree") ?? "Reroll (Free)"
            : string.Format(_localizationService.GetString("DungeonRerollGems") ?? "Reroll ({0} Gems)", 5);
    }

    /// <summary>Berechnet aktive Synergien basierend auf den Buffs des aktuellen Runs</summary>
    private void UpdateSynergies()
    {
        var runState = _dungeonService.RunState;
        if (runState is not { IsActive: true })
        {
            ActiveSynergies = [];
            return;
        }

        var buffs = runState.ActiveBuffs;
        var synergies = new List<DungeonSynergyDisplayItem>();

        // ExtraBomb + ExtraFire → Bombardier (+1 nochmal auf beides)
        if (buffs.Contains(DungeonBuffType.ExtraBomb) && buffs.Contains(DungeonBuffType.ExtraFire))
        {
            synergies.Add(new DungeonSynergyDisplayItem
            {
                Name = _localizationService.GetString("SynergyBombardier") ?? "Bombardier",
                Description = _localizationService.GetString("SynergyBombardierDesc") ?? "+1 Bomb & Fire",
                Color = "#FFD700"
            });
        }

        // SpeedBoost + BombTimer → Blitzkrieg (Bomben-Timer -0.5s extra)
        if (buffs.Contains(DungeonBuffType.SpeedBoost) && buffs.Contains(DungeonBuffType.BombTimer))
        {
            synergies.Add(new DungeonSynergyDisplayItem
            {
                Name = _localizationService.GetString("SynergyBlitzkrieg") ?? "Blitzkrieg",
                Description = _localizationService.GetString("SynergyBlitzkriegDesc") ?? "Faster bomb timer",
                Color = "#FF9800"
            });
        }

        // Shield + ExtraLife → Festung (Shield regeneriert nach 20s)
        if (buffs.Contains(DungeonBuffType.Shield) && buffs.Contains(DungeonBuffType.ExtraLife))
        {
            synergies.Add(new DungeonSynergyDisplayItem
            {
                Name = _localizationService.GetString("SynergyFortress") ?? "Fortress",
                Description = _localizationService.GetString("SynergyFortressDesc") ?? "Shield regenerates",
                Color = "#4CAF50"
            });
        }

        // CoinBonus + GoldRush → Midas (Gegner droppen Mini-Coins bei Tod)
        if (buffs.Contains(DungeonBuffType.CoinBonus) && buffs.Contains(DungeonBuffType.GoldRush))
        {
            synergies.Add(new DungeonSynergyDisplayItem
            {
                Name = _localizationService.GetString("SynergyMidas") ?? "Midas",
                Description = _localizationService.GetString("SynergyMidasDesc") ?? "Enemies drop coins",
                Color = "#FFC107"
            });
        }

        // EnemySlow + FireImmunity → Elementar (Lava verlangsamt Gegner statt Spieler zu schaden)
        if (buffs.Contains(DungeonBuffType.EnemySlow) && buffs.Contains(DungeonBuffType.FireImmunity))
        {
            synergies.Add(new DungeonSynergyDisplayItem
            {
                Name = _localizationService.GetString("SynergyElemental") ?? "Elemental",
                Description = _localizationService.GetString("SynergyElementalDesc") ?? "Lava slows enemies",
                Color = "#E91E63"
            });
        }

        ActiveSynergies = synergies;
    }

    private void NavigateToFloor(int floor, int seed)
    {
        StartDungeonFloorRequested?.Invoke(floor, seed);
        NavigationRequested?.Invoke(new GoGame(Mode: "dungeon", Floor: floor, Seed: seed));
    }

    private string GetLocalizedRarityText(DungeonBuffRarity rarity) => rarity switch
    {
        DungeonBuffRarity.Common => _localizationService.GetString("RarityCommon") ?? "Common",
        DungeonBuffRarity.Rare => _localizationService.GetString("RarityRare") ?? "Rare",
        DungeonBuffRarity.Epic => _localizationService.GetString("RarityEpic") ?? "Epic",
        DungeonBuffRarity.Legendary => _localizationService.GetString("RarityLegendary") ?? "Legendary",
        _ => ""
    };

    private static Avalonia.Media.Color GetRarityColor(DungeonBuffRarity rarity) => rarity switch
    {
        DungeonBuffRarity.Common => Avalonia.Media.Color.Parse("#FFFFFF"),
        DungeonBuffRarity.Rare => Avalonia.Media.Color.Parse("#2196F3"),
        DungeonBuffRarity.Epic => Avalonia.Media.Color.Parse("#9C27B0"),
        DungeonBuffRarity.Legendary => Avalonia.Media.Color.Parse("#FFD700"),
        _ => Avalonia.Media.Color.Parse("#FFFFFF")
    };

    private static double GetRarityGlowOpacity(DungeonBuffRarity rarity) => rarity switch
    {
        DungeonBuffRarity.Common => 0.3,
        DungeonBuffRarity.Rare => 0.5,
        DungeonBuffRarity.Epic => 0.7,
        DungeonBuffRarity.Legendary => 0.9,
        _ => 0.3
    };
}

// ═══════════════════════════════════════════════════════════════════════
// HILFSKLASSE
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Display-Item für ein permanentes Dungeon-Upgrade in der PreRun-Ansicht
/// </summary>
public class DungeonUpgradeDisplayItem
{
    public string UpgradeId { get; init; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconName { get; init; } = "";
    public int CurrentLevel { get; init; }
    public int MaxLevel { get; init; }
    public int NextCost { get; init; }
    public bool IsMaxed { get; init; }
    public bool CanBuy { get; init; }
    public string LevelText { get; set; } = "";
    public string CostText { get; set; } = "";
}

/// <summary>
/// Display-Item für einen Dungeon-Buff in der Auswahl-Ansicht
/// </summary>
public class DungeonBuffDisplayItem
{
    public DungeonBuffType Type { get; init; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconName { get; init; } = "";
    public string RarityText { get; set; } = "";
    public Avalonia.Media.Color RarityColor { get; init; }

    /// <summary>Index (0, 1, 2) für gestaffelte Einblend-Animation</summary>
    public int CardIndex { get; init; }

    /// <summary>Glow-Opacity basierend auf Rarität (Common=0.3, Rare=0.5, Epic=0.7)</summary>
    public double RarityGlowOpacity { get; init; }
}

/// <summary>
/// Display-Item für eine aktive Dungeon-Buff-Synergie
/// </summary>
public class DungeonSynergyDisplayItem
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Color { get; init; } = "#FFD700";
}
