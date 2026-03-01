using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Workshop-Kauf/Upgrade, Orders, Rush, Delivery, Prestige-Banner, Bulk-Buy
public partial class MainViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP SELECTION + KAUF
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SelectWorkshopAsync(WorkshopDisplayModel workshop)
    {
        if (!workshop.IsUnlocked)
        {
            // Level-Anforderung prüfen
            if (!_gameStateService.CanPurchaseWorkshop(workshop.Type))
            {
                var reqLevel = workshop.Type.GetUnlockLevel();
                var reqPrestige = workshop.Type.GetRequiredPrestige();
                string reason = reqPrestige > 0
                    ? $"{_localizationService.GetString("Prestige")} {reqPrestige}"
                    : $"Level {reqLevel}";
                ShowAlertDialog(
                    _localizationService.GetString("WorkshopLocked"),
                    $"{_localizationService.GetString("RequiresLevel")}: {reason}",
                    _localizationService.GetString("OK"));
                await _audioService.PlaySoundAsync(GameSound.ButtonTap);
                return;
            }

            // Level erreicht → Kauf anbieten
            var unlockCost = workshop.Type.GetUnlockCost();
            var costDisplay = MoneyFormatter.FormatCompact(unlockCost);

            // Video-Rabatt: 50% Kosten (nur wenn Werbung aktiv)
            if (ShowAds)
            {
                var halfCost = unlockCost / 2m;
                var halfCostDisplay = MoneyFormatter.FormatCompact(halfCost);

                var watchAd = await ShowConfirmDialog(
                    _localizationService.GetString("UnlockWorkshop"),
                    $"{_localizationService.GetString("UnlockWorkshopCost")}: {costDisplay}\n{_localizationService.GetString("WatchAdForHalfPrice")}: {halfCostDisplay}",
                    _localizationService.GetString("WatchAdForDiscount"),
                    $"{_localizationService.GetString("BuyFull")} ({costDisplay})");

                if (watchAd)
                {
                    // Video schauen → 50% Rabatt
                    var success = await _rewardedAdService.ShowAdAsync("workshop_unlock");
                    if (success)
                    {
                        TryPurchaseWorkshopAndNotify(workshop.Type, halfCost);
                    }
                }
                else
                {
                    TryPurchaseWorkshopAndNotify(workshop.Type);
                }
            }
            else
            {
                TryPurchaseWorkshopAndNotify(workshop.Type);
            }
            return;
        }

        // Navigate to workshop detail page
        WorkshopViewModel.SetWorkshopType(workshop.Type);
        DeactivateAllTabs();
        IsWorkshopDetailActive = true;
        NotifyTabBarVisibility();
    }

    /// <summary>
    /// Versucht eine Werkstatt zu kaufen und zeigt Erfolgs- oder Fehler-Dialog.
    /// Wird aus allen 3 Kauf-Pfaden aufgerufen (Ad+Rabatt, Vollpreis, Ohne Werbung).
    /// </summary>
    private void TryPurchaseWorkshopAndNotify(WorkshopType type, decimal? customCost = null)
    {
        bool purchased = customCost.HasValue
            ? _gameStateService.TryPurchaseWorkshop(type, customCost.Value)
            : _gameStateService.TryPurchaseWorkshop(type);

        if (purchased)
        {
            RefreshWorkshops();
            ShowAlertDialog(
                _localizationService.GetString("WorkshopUnlocked"),
                _localizationService.GetString(type.GetLocalizationKey()),
                _localizationService.GetString("OK"));
            CelebrationRequested?.Invoke();
        }
        else
        {
            var displayCost = MoneyFormatter.FormatCompact(customCost ?? type.GetUnlockCost());
            ShowAlertDialog(
                _localizationService.GetString("NotEnoughMoney"),
                $"{_localizationService.GetString("Required")}: {displayCost}",
                _localizationService.GetString("OK"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BULK BUY
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void CycleBulkBuy()
    {
        BulkBuyAmount = BulkBuyAmount switch
        {
            1 => 10,
            10 => 100,
            100 => 0, // Max
            _ => 1
        };
        BulkBuyLabel = BulkBuyAmount switch
        {
            0 => "Max",
            _ => $"x{BulkBuyAmount}"
        };
        RefreshWorkshops();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP UPGRADE + HIRE
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task UpgradeWorkshopAsync(WorkshopDisplayModel workshop)
    {
        if (!workshop.IsUnlocked || !workshop.CanUpgrade)
            return;

        if (BulkBuyAmount == 1)
        {
            if (_gameStateService.TryUpgradeWorkshop(workshop.Type))
            {
                await _audioService.PlaySoundAsync(GameSound.Upgrade);
                FloatingTextRequested?.Invoke("+1 Level!", "level");
            }
        }
        else
        {
            int upgraded = _gameStateService.TryUpgradeWorkshopBulk(workshop.Type, BulkBuyAmount);
            if (upgraded > 0)
            {
                await _audioService.PlaySoundAsync(GameSound.Upgrade);
                FloatingTextRequested?.Invoke($"+{upgraded} Level!", "level");
            }
        }
    }

    /// <summary>
    /// Flag: Hold-to-Upgrade aktiv → aufpoppende Dialoge unterdrücken.
    /// </summary>
    public bool IsHoldingUpgrade { get; set; }

    /// <summary>
    /// Stilles Upgrade ohne Sound/FloatingText - für Hold-to-Upgrade.
    /// </summary>
    public bool UpgradeWorkshopSilent(WorkshopType type)
    {
        return _gameStateService.TryUpgradeWorkshop(type);
    }

    /// <summary>
    /// Spielt den Upgrade-Sound ab (für Hold-to-Upgrade Ende).
    /// </summary>
    public void PlayUpgradeSound()
    {
        _audioService.PlaySoundAsync(GameSound.Upgrade).FireAndForget();
    }

    /// <summary>
    /// Aktualisiert eine einzelne Workshop-Anzeige (öffentlicher Zugang für Code-Behind).
    /// </summary>
    public void RefreshSingleWorkshopPublic(WorkshopType type)
    {
        RefreshSingleWorkshop(type);
    }

    /// <summary>
    /// Gibt den aktuellen GameState für SkiaSharp-Rendering zurück (City-Skyline im Header).
    /// </summary>
    public GameState? GetGameStateForRendering()
    {
        return _gameStateService.State;
    }

    /// <summary>
    /// Navigation zum Workshop-Detail direkt aus der City-Szene (Tap auf Gebäude).
    /// Nur für freigeschaltete Workshops.
    /// </summary>
    public void NavigateToWorkshopFromCity(WorkshopType type)
    {
        WorkshopViewModel.SetWorkshopType(type);
        DeactivateAllTabs();
        IsWorkshopDetailActive = true;
        NotifyTabBarVisibility();
    }

    /// <summary>
    /// Gibt den lokalisierten Workshop-Namen zurück (für City-Tap-Label).
    /// </summary>
    public string GetLocalizedWorkshopName(WorkshopType type)
        => _localizationService.GetString(type.GetLocalizationKey()) ?? type.ToString();

    /// <summary>
    /// Gibt den lokalisierten Gebäude-Namen zurück (für City-Tap-Label).
    /// </summary>
    public string GetLocalizedBuildingName(BuildingType type)
        => _localizationService.GetString(type.GetLocalizationKey()) ?? type.ToString();

    /// <summary>
    /// Gibt die lokalisierten Tab-Labels für die SkiaSharp Tab-Bar zurück.
    /// </summary>
    public string[] GetTabLabels() =>
    [
        _localizationService.GetString("TabWerkstatt") ?? "Workshop",
        _localizationService.GetString("TabImperium") ?? "Empire",
        _localizationService.GetString("TabMissionen") ?? "Missions",
        _localizationService.GetString("TabGilde") ?? "Guild",
        _localizationService.GetString("TabShop") ?? "Shop"
    ];

    /// <summary>
    /// Gibt die lokalisierten Loading-Tipps für den Ladebildschirm zurück.
    /// </summary>
    public string[] GetLoadingTips() =>
    [
        _localizationService.GetString("LoadingTip1") ?? "Tip: Hold the upgrade button for rapid leveling!",
        _localizationService.GetString("LoadingTip2") ?? "Tip: Higher worker tiers earn significantly more!",
        _localizationService.GetString("LoadingTip3") ?? "Tip: Visit daily for login rewards!",
        _localizationService.GetString("LoadingTip4") ?? "Tip: Prestige unlocks new bonuses and workshops!",
        _localizationService.GetString("LoadingTip5") ?? "Tip: Reputation above 70 brings extra orders!",
        _localizationService.GetString("LoadingTip6") ?? "Tip: Master tools give permanent income bonuses!"
    ];

    [RelayCommand]
    private async Task HireWorkerAsync(WorkshopDisplayModel workshop)
    {
        if (!workshop.IsUnlocked || !workshop.CanHireWorker)
            return;

        // Zum Arbeitermarkt navigieren statt direkt zu hiren
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
        SelectWorkerMarketTab();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ORDERS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task StartOrderAsync(Order order)
    {
        if (HasActiveOrder) return;

        _gameStateService.StartOrder(order);
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Show order detail
        OrderViewModel.SetOrder(order);
        DeactivateAllTabs();
        IsOrderDetailActive = true;
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private async Task RefreshOrdersAsync()
    {
        _orderGeneratorService.RefreshOrders();
        RefreshOrders();
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FEIERABEND-RUSH
    // ═══════════════════════════════════════════════════════════════════════

    private const int RushCostScrews = 10;
    private const int RushDurationHours = 2;

    [RelayCommand]
    private void ActivateRush()
    {
        var state = _gameStateService.State;
        if (state.IsRushBoostActive) return;

        if (state.IsFreeRushAvailable)
        {
            // Täglicher Gratis-Rush
            state.RushBoostEndTime = DateTime.UtcNow.AddHours(RushDurationHours);
            state.LastFreeRushUsed = DateTime.UtcNow;
            _gameStateService.MarkDirty();
            _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
            FloatingTextRequested?.Invoke($"Rush 2x ({RushDurationHours}h)!", "Rush");
            CelebrationRequested?.Invoke();
        }
        else if (_gameStateService.TrySpendGoldenScrews(RushCostScrews))
        {
            // Bezahlter Rush (Goldschrauben)
            state.RushBoostEndTime = DateTime.UtcNow.AddHours(RushDurationHours);
            _gameStateService.MarkDirty();
            _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
            FloatingTextRequested?.Invoke($"Rush 2x ({RushDurationHours}h)!", "Rush");
        }
        else
        {
            ShowAlertDialog(
                _localizationService.GetString("NotEnoughScrews"),
                string.Format(_localizationService.GetString("RushCostScrews"), RushCostScrews),
                _localizationService.GetString("OK"));
        }

        UpdateRushDisplay();
    }

    private void UpdateRushDisplay()
    {
        var state = _gameStateService.State;
        IsRushActive = state.IsRushBoostActive;

        if (IsRushActive)
        {
            var remaining = state.RushBoostEndTime - DateTime.UtcNow;
            RushTimeRemaining = remaining.TotalMinutes >= 60
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
                : $"{remaining.Minutes}m {remaining.Seconds:D2}s";
            CanActivateRush = false;
            RushButtonText = RushTimeRemaining;
        }
        else
        {
            RushTimeRemaining = "";
            CanActivateRush = true;
            RushButtonText = state.IsFreeRushAvailable
                ? _localizationService.GetString("RushFreeActivation")
                : $"Rush ({RushCostScrews} GS)";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIEFERANT
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ClaimDelivery()
    {
        var state = _gameStateService.State;
        var delivery = state.PendingDelivery;
        if (delivery == null || delivery.IsExpired)
        {
            HasPendingDelivery = false;
            state.PendingDelivery = null;
            return;
        }

        // Belohnung anwenden
        switch (delivery.Type)
        {
            case Models.Enums.DeliveryType.Money:
                _gameStateService.AddMoney(delivery.Amount);
                FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(delivery.Amount)}", "money");
                break;

            case Models.Enums.DeliveryType.GoldenScrews:
                _gameStateService.AddGoldenScrews((int)delivery.Amount);
                FloatingTextRequested?.Invoke($"+{(int)delivery.Amount} GS", "screw");
                break;

            case Models.Enums.DeliveryType.Experience:
                _gameStateService.AddXp((int)delivery.Amount);
                FloatingTextRequested?.Invoke($"+{(int)delivery.Amount} XP", "xp");
                break;

            case Models.Enums.DeliveryType.MoodBoost:
                foreach (var ws in state.Workshops)
                foreach (var worker in ws.Workers)
                    worker.Mood = Math.Min(100m, worker.Mood + delivery.Amount);
                FloatingTextRequested?.Invoke($"+{(int)delivery.Amount} Mood", "mood");
                break;

            case Models.Enums.DeliveryType.SpeedBoost:
                state.SpeedBoostEndTime = DateTime.UtcNow.AddMinutes((double)delivery.Amount);
                FloatingTextRequested?.Invoke($"2x ({(int)delivery.Amount}min)", "speed");
                break;
        }

        _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
        state.TotalDeliveriesClaimed++;
        state.PendingDelivery = null;
        HasPendingDelivery = false;
        _gameStateService.MarkDirty();
    }

    private void UpdateDeliveryDisplay()
    {
        var delivery = _gameStateService.State.PendingDelivery;
        if (delivery == null || delivery.IsExpired)
        {
            if (HasPendingDelivery)
            {
                HasPendingDelivery = false;
                _gameStateService.State.PendingDelivery = null;
            }
            return;
        }

        HasPendingDelivery = true;
        DeliveryIcon = delivery.Icon;
        DeliveryDescription = _localizationService.GetString(delivery.DescriptionKey);

        DeliveryAmountText = delivery.Type switch
        {
            Models.Enums.DeliveryType.Money => MoneyFormatter.FormatCompact(delivery.Amount),
            Models.Enums.DeliveryType.GoldenScrews => $"{(int)delivery.Amount} GS",
            Models.Enums.DeliveryType.Experience => $"{(int)delivery.Amount} XP",
            Models.Enums.DeliveryType.MoodBoost => $"+{(int)delivery.Amount} Mood",
            Models.Enums.DeliveryType.SpeedBoost => $"{(int)delivery.Amount}min 2x",
            _ => ""
        };

        var remaining = delivery.TimeRemaining;
        DeliveryTimeRemaining = $"{remaining.Minutes}:{remaining.Seconds:D2}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // REFRESH-METHODEN (State → UI)
    // ═══════════════════════════════════════════════════════════════════════

    private void RefreshFromState()
    {
        var state = _gameStateService.State;

        // Update properties
        Money = state.Money;
        // Beim Start: sofort setzen, kein Ticken
        _displayedMoney = state.Money;
        _targetMoney = state.Money;
        MoneyDisplay = FormatMoney(state.Money);
        IncomePerSecond = state.NetIncomePerSecond;
        IncomeDisplay = $"{FormatMoney(state.NetIncomePerSecond)}/s";
        UpdateNetIncomeHeader(state);
        UpdateWorkerWarning(state);
        PlayerLevel = state.PlayerLevel;
        CurrentXp = state.CurrentXp;
        XpForNextLevel = state.XpForNextLevel;
        LevelProgress = state.LevelProgress;
        GoldenScrewsDisplay = state.GoldenScrews.ToString("N0");

        // Login-Streak aktualisieren
        OnPropertyChanged(nameof(LoginStreak));
        OnPropertyChanged(nameof(HasLoginStreak));

        // Automation-Unlock-Properties aktualisieren (Level-abhängig, wichtig nach Init + Prestige)
        OnPropertyChanged(nameof(IsAutoCollectUnlocked));
        OnPropertyChanged(nameof(IsAutoAcceptUnlocked));
        OnPropertyChanged(nameof(IsAutoAssignUnlocked));
        OnPropertyChanged(nameof(IsAutoClaimUnlocked));

        // Rush/Delivery/MasterTools
        UpdateRushDisplay();
        UpdateDeliveryDisplay();
        MasterToolsCollected = state.CollectedMasterTools.Count;
        MasterToolsTotal = MasterTool.GetAllDefinitions().Count;
        var totalMtBonus = MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools);
        MasterToolsBonusDisplay = totalMtBonus > 0 ? $"+{(int)(totalMtBonus * 100)}%" : "";

        // Prestige-Shop ab Level 500 (oder wenn bereits prestigiert → Shop bleibt zugänglich nach Reset)
        IsPrestigeShopUnlocked = state.PlayerLevel >= 500 || state.Prestige.TotalPrestigeCount > 0;

        // Refresh workshops
        RefreshWorkshops();

        // Tutorial-Hint: Pulsierender Rahmen wenn noch nie ein Upgrade gemacht wurde
        ShowTutorialHint = !state.HasSeenTutorialHint && state.PlayerLevel < 3;

        // Refresh orders
        RefreshOrders();

        // Check for active order
        HasActiveOrder = state.ActiveOrder != null;
        ActiveOrder = state.ActiveOrder;
    }

    private void RefreshWorkshops()
    {
        var state = _gameStateService.State;

        // Erste Initialisierung: Items erstellen
        if (Workshops.Count == 0)
        {
            foreach (var type in _workshopTypes)
            {
                Workshops.Add(CreateWorkshopDisplay(state, type));
            }
        }
        else
        {
            // Update: Bestehende Items aktualisieren (kein Clear/Add → weniger UI-Churn)
            for (int i = 0; i < _workshopTypes.Length && i < Workshops.Count; i++)
            {
                UpdateWorkshopDisplay(Workshops[i], state, _workshopTypes[i]);
            }
        }

        // Gebäude-Zusammenfassung aktualisieren (Task #5)
        RefreshBuildingsSummary(state);

        // Feature-Button Status-Texte aktualisieren
        RefreshFeatureStatusTexts(state);

        // Reputation aktualisieren (Task #6)
        RefreshReputation(state);

        // Prestige-Banner aktualisieren (Task #14)
        RefreshPrestigeBanner(state);
    }

    /// <summary>
    /// Aktualisiert nur einen einzelnen Workshop (statt alle) → weniger UI-Churn bei Upgrade/Hire.
    /// </summary>
    private void RefreshSingleWorkshop(WorkshopType type)
    {
        var state = _gameStateService.State;
        var index = Array.IndexOf(_workshopTypes, type);
        if (index >= 0 && index < Workshops.Count)
        {
            UpdateWorkshopDisplay(Workshops[index], state, type);
        }
    }

    private WorkshopDisplayModel CreateWorkshopDisplay(GameState state, WorkshopType type)
    {
        var workshop = state.Workshops.FirstOrDefault(w => w.Type == type);
        bool isUnlocked = state.IsWorkshopUnlocked(type);
        var model = new WorkshopDisplayModel
        {
            Type = type,
            Icon = type.GetIcon(),
            IconKind = GetWorkshopIconKind(type, workshop?.Level ?? 1),
            Name = _localizationService.GetString(type.GetLocalizationKey()),
            Level = workshop?.Level ?? 1,
            WorkerCount = workshop?.Workers.Count ?? 0,
            MaxWorkers = workshop?.MaxWorkers ?? 1,
            IncomePerSecond = workshop?.IncomePerSecond ?? 0,
            UpgradeCost = workshop?.UpgradeCost ?? 100,
            HireWorkerCost = workshop?.HireWorkerCost ?? 50,
            IsUnlocked = isUnlocked,
            UnlockLevel = type.GetUnlockLevel(),
            RequiredPrestige = type.GetRequiredPrestige(),
            UnlockCost = type.GetUnlockCost(),
            CanBuyUnlock = _gameStateService.CanPurchaseWorkshop(type),
            CanAffordUnlock = _gameStateService.CanPurchaseWorkshop(type) && state.Money >= type.GetUnlockCost(),
            UnlockDisplay = type.GetRequiredPrestige() > 0
                ? $"{_localizationService.GetString("Prestige")} {type.GetRequiredPrestige()}"
                : $"Lv. {type.GetUnlockLevel()}",
            CanUpgrade = workshop?.CanUpgrade ?? true,
            CanHireWorker = workshop?.CanHireWorker ?? false,
            CanAffordUpgrade = state.Money >= (workshop?.UpgradeCost ?? 100),
            CanAffordWorker = state.Money >= (workshop?.HireWorkerCost ?? 50)
        };
        // BulkBuy-Kosten berechnen
        SetBulkUpgradeCost(model, workshop, state.Money);

        // Upgrade-Preview + Net-Income berechnen (Task #10, #13)
        SetWorkshopFinancials(model, workshop);

        return model;
    }

    /// <summary>
    /// Setzt BulkUpgradeCost und BulkUpgradeLabel basierend auf aktuellem BulkBuyAmount.
    /// </summary>
    private void SetBulkUpgradeCost(WorkshopDisplayModel model, Workshop? workshop, decimal money)
    {
        if (workshop == null || !workshop.CanUpgrade)
        {
            model.BulkUpgradeCost = 0;
            model.BulkUpgradeLabel = "";
            return;
        }

        if (BulkBuyAmount == 0) // Max
        {
            var (count, cost) = workshop.GetMaxAffordableUpgrades(money);
            model.BulkUpgradeCost = cost;
            model.BulkUpgradeLabel = count > 0 ? $"Max ({count})" : "Max";
            model.CanAffordUpgrade = count > 0;
        }
        else if (BulkBuyAmount == 1)
        {
            model.BulkUpgradeCost = workshop.UpgradeCost;
            model.BulkUpgradeLabel = "";
            model.CanAffordUpgrade = money >= workshop.UpgradeCost;
        }
        else
        {
            model.BulkUpgradeCost = workshop.GetBulkUpgradeCost(BulkBuyAmount);
            model.BulkUpgradeLabel = $"x{BulkBuyAmount}";
            model.CanAffordUpgrade = money >= model.BulkUpgradeCost;
        }
    }

    /// <summary>
    /// Berechnet Upgrade-Income-Preview und Netto-Einkommen für eine Workshop-Anzeige (Task #10, #13).
    /// </summary>
    private void SetWorkshopFinancials(WorkshopDisplayModel model, Workshop? workshop)
    {
        if (workshop == null || !model.IsUnlocked)
        {
            model.UpgradeIncomePreview = "";
            model.NetIncomeDisplay = "";
            model.IsNetNegative = false;
            model.HasCosts = false;
            return;
        }

        // Netto-Einkommen (Brutto - Kosten)
        var netIncome = workshop.NetIncomePerSecond;
        model.NetIncomeDisplay = MoneyFormatter.FormatPerSecond(netIncome, 1);
        model.IsNetNegative = netIncome < 0;
        model.HasCosts = workshop.TotalCostsPerHour > 0;

        // Upgrade-Preview: Einkommensdifferenz nach Bulk-Upgrade berechnen
        if (workshop.CanUpgrade && workshop.Level < Workshop.MaxLevel)
        {
            int upgradeCount = BulkBuyAmount == 0 ? 10 : BulkBuyAmount; // Max → zeige Preview für ~10 Level
            int targetLevel = Math.Min(workshop.Level + upgradeCount, Workshop.MaxLevel);
            // Einkommen bei Ziel-Level basierend auf Base-Income-Formel berechnen
            decimal currentBase = (decimal)Math.Pow(1.025, workshop.Level - 1) * workshop.Type.GetBaseIncomeMultiplier();
            decimal targetBase = (decimal)Math.Pow(1.025, targetLevel - 1) * workshop.Type.GetBaseIncomeMultiplier();
            // Differenz berücksichtigt nur die Basis (Worker-Effekte skalieren proportional)
            decimal diff = (targetBase - currentBase) * Math.Max(1, workshop.Workers.Count);
            model.UpgradeIncomePreview = diff > 0 ? $"+{MoneyFormatter.FormatPerSecond(diff, 1)}" : "";
        }
        else
        {
            model.UpgradeIncomePreview = "";
        }
    }

    /// <summary>
    /// Aktualisiert die Gebäude-Zusammenfassung (Task #5).
    /// </summary>
    private void RefreshBuildingsSummary(GameState state)
    {
        int totalBuildings = Enum.GetValues<BuildingType>().Length;
        int builtCount = state.Buildings.Count(b => b.IsBuilt);
        var builtLabel = _localizationService.GetString("Built") ?? "gebaut";
        var buildingsLabel = _localizationService.GetString("Buildings") ?? "Gebäude";
        BuildingsSummary = $"{totalBuildings} {buildingsLabel}, {builtCount} {builtLabel}";
    }

    /// <summary>
    /// Aktualisiert die Feature-Button Status-Texte.
    /// </summary>
    private void RefreshFeatureStatusTexts(GameState state)
    {
        // Arbeiter
        var totalWorkers = state.Workshops.Sum(w => w.Workers.Count);
        WorkersStatusText = string.Format(
            _localizationService.GetString("WorkersStatus") ?? "{0} angestellt",
            totalWorkers);

        // Forschung
        var completedResearch = state.Researches.Count(r => r.IsResearched);
        if (!string.IsNullOrEmpty(state.ActiveResearchId))
        {
            var researchName = _localizationService.GetString($"Research_{state.ActiveResearchId}") ?? state.ActiveResearchId;
            ResearchStatusText = string.Format(
                _localizationService.GetString("ResearchActiveStatus") ?? "Erforscht: {0}",
                researchName);
        }
        else
        {
            ResearchStatusText = string.Format(
                _localizationService.GetString("ResearchStatus") ?? "{0}/45 erforscht",
                completedResearch);
        }

        // Vorarbeiter
        var activeManagers = state.Managers.Count(m => m.IsUnlocked);
        ManagerStatusText = string.Format(
            _localizationService.GetString("ManagerStatus") ?? "{0} aktiv",
            activeManagers);

        // Turnier
        if (state.CurrentTournament != null)
        {
            var remainingEntries = state.CurrentTournament.FreeEntriesRemaining;
            TournamentStatusText = string.Format(
                _localizationService.GetString("TournamentStatus") ?? "{0} Versuche",
                remainingEntries);
        }
        else
        {
            TournamentStatusText = "";
        }

        // Saison-Event
        if (state.CurrentSeasonalEvent != null)
        {
            var seasonKey = state.CurrentSeasonalEvent.Season.ToString();
            SeasonalEventStatusText = _localizationService.GetString(seasonKey) ?? seasonKey;
        }
        else
        {
            SeasonalEventStatusText = "";
        }

        // Saison-Pass
        BattlePassStatusText = string.Format(
            _localizationService.GetString("BattlePassStatus") ?? "Tier {0}/{1}",
            state.BattlePass.CurrentTier, 30);

        // Produktion
        var activeCrafts = state.ActiveCraftingJobs.Count;
        CraftingStatusText = string.Format(
            _localizationService.GetString("CraftingStatus") ?? "{0} in Produktion",
            activeCrafts);
    }

    /// <summary>
    /// Aktualisiert Reputation-Anzeige (Task #6).
    /// </summary>
    private void RefreshReputation(GameState state)
    {
        var score = state.Reputation.ReputationScore;
        ReputationScore = score;
        ReputationColor = score switch
        {
            < 30 => "#EF4444",  // Rot
            < 60 => "#F59E0B",  // Gelb
            < 80 => "#22C55E",  // Grün
            _ => "#FFD700"      // Gold
        };
    }

    /// <summary>
    /// Aktualisiert Prestige-Banner-Anzeige (Task #14).
    /// </summary>
    private void RefreshPrestigeBanner(GameState state)
    {
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);
        IsPrestigeAvailable = highestTier != PrestigeTier.None;

        if (IsPrestigeAvailable)
        {
            var potentialPoints = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
            int tierPoints = (int)(potentialPoints * highestTier.GetPointMultiplier());
            var pointsLabel = _localizationService.GetString("PrestigePoints") ?? "Prestige-Punkte";
            PrestigePointsPreview = $"+{tierPoints} {pointsLabel}";

            PrestigePreviewTierName = _localizationService.GetString(highestTier.GetLocalizationKey()) ?? highestTier.ToString();

            // Gewinne
            decimal permanentBonus = highestTier.GetPermanentMultiplierBonus() * 100;
            var gains = new List<string>();
            gains.Add($"+{tierPoints} {pointsLabel} (x{highestTier.GetPointMultiplier()})");
            gains.Add($"+{permanentBonus:0}% {_localizationService.GetString("PermanentIncomeBonus") ?? "permanenter Einkommens-Bonus"}");
            if (highestTier.KeepsResearch())
                gains.Add(_localizationService.GetString("PrestigeKeepsResearch") ?? "Forschung bleibt erhalten!");
            if (highestTier.KeepsShopItems())
                gains.Add(_localizationService.GetString("PrestigeKeepsShop") ?? "Prestige-Shop bleibt!");
            if (highestTier.KeepsMasterTools())
                gains.Add(_localizationService.GetString("PrestigeKeepsTools") ?? "Meisterwerkzeuge bleiben!");
            if (highestTier.KeepsBuildings())
                gains.Add(_localizationService.GetString("PrestigeKeepsBuildings") ?? "Gebäude bleiben (Lv.1)!");
            if (highestTier.KeepsManagers())
                gains.Add(_localizationService.GetString("PrestigeKeepsManagers") ?? "Manager bleiben (Lv.1)!");
            if (highestTier.KeepsBestWorkers())
                gains.Add(_localizationService.GetString("PrestigeKeepsWorkers") ?? "Beste Worker bleiben!");
            PrestigePreviewGains = string.Join("\n", gains);

            // Verluste
            var losses = new List<string>();
            losses.Add(_localizationService.GetString("PrestigeLosesLevel") ?? "Spieler-Level → 1");
            losses.Add(_localizationService.GetString("PrestigeLosesMoney") ?? "Geld → 0");
            losses.Add(_localizationService.GetString("PrestigeLosesWorkers") ?? "Worker → entlassen");
            if (!highestTier.KeepsResearch())
                losses.Add(_localizationService.GetString("PrestigeLosesResearch") ?? "Forschung → Reset");
            PrestigePreviewLosses = string.Join("\n", losses);

            // Geschätzter Speed-Up
            decimal currentMult = state.Prestige.PermanentMultiplier;
            decimal newMult = currentMult + highestTier.GetPermanentMultiplierBonus();
            int speedUpPercent = currentMult > 0 ? (int)((newMult / currentMult - 1m) * 100) : 100;
            PrestigePreviewSpeedUp = $"~{speedUpPercent}% {_localizationService.GetString("Faster") ?? "schneller"}";
        }
        else
        {
            PrestigePointsPreview = "";
            PrestigePreviewGains = "";
            PrestigePreviewLosses = "";
            PrestigePreviewSpeedUp = "";
            PrestigePreviewTierName = "";
        }
    }

    private void UpdateWorkshopDisplay(WorkshopDisplayModel model, GameState state, WorkshopType type)
    {
        var workshop = state.Workshops.FirstOrDefault(w => w.Type == type);
        bool isUnlocked = state.IsWorkshopUnlocked(type);

        model.Name = _localizationService.GetString(type.GetLocalizationKey());
        model.Level = workshop?.Level ?? 1;
        model.IconKind = GetWorkshopIconKind(type, model.Level);
        model.WorkerCount = workshop?.Workers.Count ?? 0;
        model.MaxWorkers = workshop?.MaxWorkers ?? 1;
        model.IncomePerSecond = workshop?.IncomePerSecond ?? 0;
        model.UpgradeCost = workshop?.UpgradeCost ?? 100;
        model.HireWorkerCost = workshop?.HireWorkerCost ?? 50;
        model.IsUnlocked = isUnlocked;
        model.UnlockCost = type.GetUnlockCost();
        model.CanBuyUnlock = _gameStateService.CanPurchaseWorkshop(type);
        model.CanAffordUnlock = model.CanBuyUnlock && state.Money >= type.GetUnlockCost();
        model.UnlockDisplay = type.GetRequiredPrestige() > 0
            ? $"{_localizationService.GetString("Prestige")} {type.GetRequiredPrestige()}"
            : $"Lv. {type.GetUnlockLevel()}";
        model.CanUpgrade = workshop?.CanUpgrade ?? true;
        model.CanHireWorker = workshop?.CanHireWorker ?? false;
        model.CanAffordUpgrade = state.Money >= (workshop?.UpgradeCost ?? 100);
        model.CanAffordWorker = state.Money >= (workshop?.HireWorkerCost ?? 50);

        // BulkBuy-Kosten aktualisieren
        SetBulkUpgradeCost(model, workshop, state.Money);

        // Upgrade-Preview + Net-Income berechnen (Task #10, #13)
        SetWorkshopFinancials(model, workshop);

        model.NotifyAllChanged();
    }

    private void RefreshOrders()
    {
        var state = _gameStateService.State;
        AvailableOrders.Clear();

        foreach (var order in state.AvailableOrders)
        {
            // Lokalisierte Display-Felder befüllen
            var localizedTitle = _localizationService.GetString(order.TitleKey);
            order.DisplayTitle = string.IsNullOrEmpty(localizedTitle) ? order.TitleFallback : localizedTitle;
            order.DisplayWorkshopName = _localizationService.GetString(order.WorkshopType.GetLocalizationKey());

            // Auftragstyp Display-Properties (Task #3)
            order.DisplayOrderType = _localizationService.GetString(order.OrderType.GetLocalizationKey())
                                     ?? order.OrderType.ToString();
            order.OrderTypeIcon = order.OrderType.GetIcon();
            order.OrderTypeBadgeColor = order.OrderType switch
            {
                OrderType.Large => "#EA580C",
                OrderType.Weekly => "#FFD700",
                OrderType.Cooperation => "#0E7490",
                _ => ""
            };
            order.ShowOrderTypeBadge = order.OrderType != OrderType.Standard && order.OrderType != OrderType.Quick;

            AvailableOrders.Add(order);
        }

        // Empty State (Task #8)
        HasNoOrders = AvailableOrders.Count == 0;
    }
}
