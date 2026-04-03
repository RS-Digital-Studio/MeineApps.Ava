using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Workshop-Kauf/Upgrade, Orders, Rush, Delivery, Prestige-Banner, Bulk-Buy
public sealed partial class MainViewModel
{
    // Prestige-Banner Dirty-Flag: Nur neu berechnen wenn sich Level oder Prestige-Count aendert
    private int _lastPrestigeBannerLevel = -1;
    private int _lastPrestigeBannerPrestigeCount = -1;
    // Wiederverwendbare Listen fuer Prestige-Banner (vermeidet 2x new List<string> alle 5 Ticks)
    private readonly List<string> _prestigeGains = new();
    private readonly List<string> _prestigeLosses = new();

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

            // Video-Rabatt: 30% Kosten-Reduktion (Spieler zahlt 70%)
            if (ShowAds)
            {
                var discountedCost = unlockCost * 0.70m;
                var discountDisplay = MoneyFormatter.FormatCompact(discountedCost);

                var watchAd = await ShowConfirmDialog(
                    _localizationService.GetString("UnlockWorkshop"),
                    $"{_localizationService.GetString("UnlockWorkshopCost")}: {costDisplay}\n{_localizationService.GetString("WatchAdForHalfPrice")}: {discountDisplay}",
                    _localizationService.GetString("WatchAdForDiscount"),
                    $"{_localizationService.GetString("BuyFull")} ({costDisplay})");

                if (watchAd)
                {
                    // Video schauen → 30% Rabatt
                    var success = await _rewardedAdService.ShowAdAsync("workshop_unlock");
                    if (success)
                    {
                        TryPurchaseWorkshopAndNotify(workshop.Type, discountedCost);
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

        // Zur Workshop-Detailseite navigieren
        WorkshopViewModel.SetWorkshopType(workshop.Type);
        ActivePage = ActivePage.WorkshopDetail;
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

        // Lieferaufträge: Items direkt abgeben, kein MiniGame
        if (order.OrderType == OrderType.MaterialOrder)
        {
            await CompleteMaterialOrderAsync(order);
            return;
        }

        _gameStateService.StartOrder(order);
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Hint beim ersten Auftrag
        _contextualHintService.TryShowHint(ContextualHints.FirstOrder);

        // Auftragsdetail anzeigen
        OrderViewModel.SetOrder(order);
        ActivePage = ActivePage.OrderDetail;
    }

    /// <summary>
    /// Schließt einen Lieferauftrag ab: Items prüfen, abziehen, Belohnung gutschreiben.
    /// </summary>
    private async Task CompleteMaterialOrderAsync(Order order)
    {
        if (order.RequiredMaterials == null) return;

        // Prüfen ob alle Items vorhanden
        var state = _gameStateService.State;
        foreach (var (productId, required) in order.RequiredMaterials)
        {
            int available = state.CraftingInventory.GetValueOrDefault(productId, 0);
            if (available < required)
            {
                // Nicht genug Materialien
                var msg = _localizationService.GetString("InsufficientMaterials") ?? "Nicht genügend Materialien";
                DialogVM.ShowAlertDialog(
                    _localizationService.GetString("MaterialsRequired") ?? "Materialien benötigt",
                    msg,
                    "OK");
                return;
            }
        }

        // Auftrag abschließen
        var reward = _gameStateService.CompleteMaterialOrder(order);
        if (reward <= 0) return;

        // Tracking für Daily/Weekly Challenges
        _dailyChallengeService?.OnMaterialOrderCompleted();

        await _audioService.PlaySoundAsync(GameSound.Perfect);
        FloatingTextRequested?.Invoke($"+{MoneyFormatter.Format(reward, 0)}", "money");
        CelebrationRequested?.Invoke();

        // Orders aktualisieren
        _orderGeneratorService.RefreshOrders();
        RefreshOrders();
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

        // Boost-Indikator mit-aktualisieren (Rush-Status hat sich geändert)
        UpdateBoostIndicator();
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
                var screwAmount = (int)Math.Round(delivery.Amount);
                _gameStateService.AddGoldenScrews(screwAmount);
                FloatingTextRequested?.Invoke($"+{screwAmount} \u2699", "screw");
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
            Models.Enums.DeliveryType.GoldenScrews => $"{(int)delivery.Amount} \u2699",
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
        OnPropertyChanged(nameof(ShowStreakBadge));

        // Automation-Unlock-Properties aktualisieren (Level-abhängig, wichtig nach Init + Prestige)
        OnPropertyChanged(nameof(IsAutoCollectUnlocked));
        OnPropertyChanged(nameof(IsAutoAcceptUnlocked));
        OnPropertyChanged(nameof(IsAutoAssignUnlocked));
        OnPropertyChanged(nameof(IsAutoClaimUnlocked));

        // Rush/Delivery/MasterTools + Boost-Indikator
        UpdateRushDisplay();
        UpdateBoostIndicator();
        UpdateDeliveryDisplay();
        MissionsVM.MasterToolsCollected = state.CollectedMasterTools.Count;
        MissionsVM.MasterToolsTotal = MasterTool.GetAllDefinitions().Count;
        var totalMtBonus = MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools);
        MissionsVM.MasterToolsBonusDisplay = totalMtBonus > 0 ? $"+{(int)(totalMtBonus * 100)}%" : "";

        // Prestige-Shop ab bestimmtem Level (oder wenn bereits prestigiert → Shop bleibt zugänglich nach Reset)
        IsPrestigeShopUnlocked = state.PlayerLevel >= LevelThresholds.PrestigeShopUnlock || state.Prestige.TotalPrestigeCount > 0;

        // Statische Renderer-Strings initialisieren (Karten-Texte)
        WorkshopGameCardRenderer.UpdateLocalizedStrings(
            _localizationService.GetString("TapToUnlock") ?? "Tap to unlock",
            _localizationService.GetString("AtLevelShort") ?? "From Level {0}");

        // Refresh workshops
        RefreshWorkshops();

        // Tutorial-Hint: Pulsierender Rahmen solange FirstWorkshop-Hint noch nicht gesehen
        // Nach Prestige (Level zurück auf 1) nicht erneut anzeigen
        ShowTutorialHint = !_contextualHintService.HasSeenHint(ContextualHints.FirstWorkshop.Id)
                           && state.PlayerLevel < LevelThresholds.TutorialHintMaxLevel
                           && state.Prestige.TotalPrestigeCount == 0;

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

        // Workshop-Canvas-Höhe aktualisieren (dynamisch basierend auf Anzahl)
        OnPropertyChanged(nameof(WorkshopCanvasHeight));

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
        Workshop? workshop = null;
        for (int i = 0; i < state.Workshops.Count; i++)
            if (state.Workshops[i].Type == type) { workshop = state.Workshops[i]; break; }
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
            CanAffordWorker = state.Money >= (workshop?.HireWorkerCost ?? 50),
            RebirthStars = workshop?.RebirthStars ?? 0,
            SpecializationBadge = GetSpecBadge(workshop?.WorkshopSpecialization),
            SpecializationColor = workshop?.WorkshopSpecialization?.Color ?? ""
        };
        // BulkBuy-Kosten berechnen
        SetBulkUpgradeCost(model, workshop, state.Money);

        // Upgrade-Preview + Net-Income berechnen (Task #10, #13)
        SetWorkshopFinancials(model, workshop);

        return model;
    }

    /// <summary>Kürzel für Spezialisierungs-Badge auf Dashboard-Karte (lokalisiert).</summary>
    private string GetSpecBadge(WorkshopSpecialization? spec)
    {
        if (spec == null) return "";
        // Erste 3-4 Buchstaben des lokalisierten Spezialisierungsnamens + Punkt
        string fullName = _localizationService.GetString(spec.NameKey) ?? spec.Type.ToString();
        int len = Math.Min(fullName.Length, 4);
        return fullName[..len].TrimEnd() + ".";
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
            decimal currentBase = (decimal)Math.Pow(1.02, workshop.Level - 1) * workshop.Type.GetBaseIncomeMultiplier();
            decimal targetBase = (decimal)Math.Pow(1.02, targetLevel - 1) * workshop.Type.GetBaseIncomeMultiplier();
            // Differenz berücksichtigt nur die Basis (Worker-Effekte skalieren proportional)
            decimal diff = (targetBase - currentBase) * Math.Max(1, workshop.Workers.Count);
            model.UpgradeIncomePreview = diff > 0 ? $"+{MoneyFormatter.FormatPerSecond(diff, 1)}" : "";
        }
        else
        {
            model.UpgradeIncomePreview = "";
        }

        // Time-to-Upgrade: Geschätzte Wartezeit bis nächstes Upgrade leistbar
        var state = _gameStateService.State;
        decimal upgradeCost = model.BulkUpgradeCost > 0 ? model.BulkUpgradeCost : workshop.UpgradeCost;
        decimal deficit = upgradeCost - state.Money;
        if (deficit > 0 && state.NetIncomePerSecond > 0)
        {
            double seconds = (double)(deficit / state.NetIncomePerSecond);
            model.TimeToUpgrade = seconds switch
            {
                < 60 => $"~{(int)seconds}s",
                < 3600 => $"~{seconds / 60:0.#} Min",
                < 86400 => $"~{seconds / 3600:0.#} Std",
                _ => $"~{seconds / 86400:0.#} Tage"
            };
        }
        else
        {
            model.TimeToUpgrade = "";
        }
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
        var buildingsLabel = _localizationService.GetString("Buildings") ?? "Gebäude";
        BuildingsSummary = $"{s_totalBuildingCount} {buildingsLabel}, {builtCount} {builtLabel}";
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
        WorkersStatusText = string.Format(
            _localizationService.GetString("WorkersStatus") ?? "{0} angestellt",
            totalWorkers);

        // Forschung (For-Schleife statt LINQ Count)
        int completedResearch = 0;
        for (int i = 0; i < state.Researches.Count; i++)
            if (state.Researches[i].IsResearched) completedResearch++;
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

        // Vorarbeiter (For-Schleife statt LINQ Count)
        int activeManagers = 0;
        for (int i = 0; i < state.Managers.Count; i++)
            if (state.Managers[i].IsUnlocked) activeManagers++;
        ManagerStatusText = string.Format(
            _localizationService.GetString("ManagerStatus") ?? "{0} aktiv",
            activeManagers);

        // Progressive Disclosure: Missionen-Sub-Features (Dead Zone Lv40-80 schließen)
        // Nach Prestige: Einmal freigeschaltete Features bleiben sichtbar (Prestige-Count > 0 = war schon mal dort)
        bool hasPrestiged = state.Prestige.TotalPrestigeCount > 0;
        ShowTournamentSection = state.PlayerLevel >= LevelThresholds.TournamentSection || hasPrestiged;
        ShowSeasonalEventSection = state.PlayerLevel >= LevelThresholds.SeasonalEventSection || hasPrestiged;
        ShowBattlePassSection = state.PlayerLevel >= LevelThresholds.BattlePassSection || hasPrestiged;

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
        OnPropertyChanged(nameof(ShowReputationBadge));
    }

    /// <summary>
    /// Aktualisiert Prestige-Banner-Anzeige (Task #14).
    /// Dirty-Flag: Nur neu berechnen wenn sich Level oder Prestige-Count geaendert hat.
    /// </summary>
    private void RefreshPrestigeBanner(GameState state)
    {
        int currentLevel = state.PlayerLevel;
        int currentPrestigeCount = state.Prestige.TotalPrestigeCount;

        // Early-Exit: Nichts hat sich geaendert seit letztem Aufruf
        if (currentLevel == _lastPrestigeBannerLevel && currentPrestigeCount == _lastPrestigeBannerPrestigeCount)
            return;
        _lastPrestigeBannerLevel = currentLevel;
        _lastPrestigeBannerPrestigeCount = currentPrestigeCount;

        var highestTier = state.Prestige.GetHighestAvailableTier(currentLevel);
        IsPrestigeAvailable = highestTier != PrestigeTier.None;

        if (IsPrestigeAvailable)
        {
            var potentialPoints = _prestigeService.GetPrestigePoints(state.CurrentRunMoney);
            int tierPoints = (int)(potentialPoints * highestTier.GetPointMultiplier());
            var pointsLabel = _localizationService.GetString("PrestigePoints") ?? "Prestige-Punkte";
            PrestigePointsPreview = $"+{tierPoints} {pointsLabel}";

            PrestigePreviewTierName = _localizationService.GetString(highestTier.GetLocalizationKey()) ?? highestTier.ToString();

            // Gewinne (wiederverwendbare Liste statt new List<string>)
            decimal permanentBonus = highestTier.GetPermanentMultiplierBonus() * 100;
            _prestigeGains.Clear();
            _prestigeGains.Add($"+{tierPoints} {pointsLabel} (x{highestTier.GetPointMultiplier()})");
            _prestigeGains.Add($"+{permanentBonus:0}% {_localizationService.GetString("PermanentIncomeBonus") ?? "permanenter Einkommens-Bonus"}");
            if (highestTier.KeepsResearch())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsResearch") ?? "Forschung bleibt erhalten!");
            if (highestTier.KeepsShopItems())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsShop") ?? "Prestige-Shop bleibt!");
            if (highestTier.KeepsMasterTools())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsTools") ?? "Meisterwerkzeuge bleiben!");
            if (highestTier.KeepsBuildings())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsBuildings") ?? "Gebäude bleiben (Lv.1)!");
            if (highestTier.KeepsManagers())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsManagers") ?? "Manager bleiben (Lv.1)!");
            if (highestTier.KeepsBestWorkers())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsWorkers") ?? "Beste Worker bleiben!");
            PrestigePreviewGains = string.Join("\n", _prestigeGains);

            // Verluste (wiederverwendbare Liste statt new List<string>)
            _prestigeLosses.Clear();
            _prestigeLosses.Add(_localizationService.GetString("PrestigeLosesLevel") ?? "Spieler-Level → 1");
            _prestigeLosses.Add(_localizationService.GetString("PrestigeLosesMoney") ?? "Geld → 0");
            _prestigeLosses.Add(_localizationService.GetString("PrestigeLosesWorkers") ?? "Worker → entlassen");
            if (!highestTier.KeepsResearch())
                _prestigeLosses.Add(_localizationService.GetString("PrestigeLosesResearch") ?? "Forschung → Reset");
            PrestigePreviewLosses = string.Join("\n", _prestigeLosses);

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

        // Fortschritt zum nächsten Tier (auch anzeigen wenn aktuell kein Prestige verfügbar)
        var nextTier = highestTier.GetNextTier();
        if (nextTier != PrestigeTier.None)
        {
            HasNextPrestigeTier = true;
            var reqLevel = nextTier.GetRequiredLevel();
            var currentTierLevel = highestTier != PrestigeTier.None ? highestTier.GetRequiredLevel() : 0;
            var range = reqLevel - currentTierLevel;
            var progress = range > 0
                ? Math.Clamp((double)(currentLevel - currentTierLevel) / range, 0.0, 1.0)
                : 0.0;
            NextPrestigeTierProgress = progress;
            var tierName = _localizationService.GetString(nextTier.GetLocalizationKey()) ?? nextTier.ToString();

            // PP-Prognose: "Bei Gold: +400 PP"
            var potentialPP = _prestigeService.GetPrestigePoints(state.CurrentRunMoney);
            int nextTierPoints = (int)(potentialPP * nextTier.GetPointMultiplier());
            NextPrestigeTierHint = nextTierPoints > 0
                ? $"Lv. {currentLevel}/{reqLevel} \u2192 {tierName} (+{nextTierPoints} PP)"
                : $"Lv. {currentLevel}/{reqLevel} \u2192 {tierName}";
        }
        else
        {
            HasNextPrestigeTier = false;
            NextPrestigeTierHint = "";
            NextPrestigeTierProgress = 0;
        }

        // Tier-Auswahl wird dynamisch beim Öffnen des Prestige-Dialogs in DialogVM gesetzt

        // Challenge-Anzeige aktualisieren
        RefreshChallengeDisplay();

        // Speedrun-Timer aktualisieren
        var runDuration = _prestigeService.GetCurrentRunDuration();
        CurrentRunDuration = runDuration.HasValue
            ? $"{(int)runDuration.Value.TotalHours}h {runDuration.Value.Minutes:D2}m"
            : "";

        // Prestige-Tier-Badge im Dashboard-Header aktualisieren
        UpdatePrestigeTierBadge(state);
    }

    /// <summary>
    /// Aktualisiert die Challenge-Anzeige (Anzahl + Text).
    /// </summary>
    private void RefreshChallengeDisplay()
    {
        var challenges = _challengeConstraints?.GetActiveChallenges();
        if (challenges == null || challenges.Count == 0)
        {
            ActiveChallengeCount = 0;
            ActiveChallengesText = "";
            return;
        }

        ActiveChallengeCount = challenges.Count;
        var parts = new List<string>(challenges.Count);
        for (int i = 0; i < challenges.Count; i++)
        {
            var c = challenges[i];
            var name = _localizationService.GetString(c.GetNameKey()) ?? c.ToString();
            parts.Add($"{name} +{c.GetPpBonus() * 100:0}%");
        }
        ActiveChallengesText = string.Join(", ", parts);
    }

    /// <summary>
    /// Challenge aktivieren/deaktivieren (Toggle). Aufgerufen aus UI (ImperiumView).
    /// </summary>
    [RelayCommand]
    private void ToggleChallenge(string challengeName)
    {
        if (!Enum.TryParse<PrestigeChallengeType>(challengeName, out var challenge))
            return;

        bool success = _challengeConstraints?.ToggleChallenge(challenge) ?? false;
        if (!success)
        {
            // SoloMeister + QuickStart oder Max erreicht
            var msg = _localizationService.GetString("ChallengesMaxReached") ?? "Maximal 3 Herausforderungen";
            FloatingTextRequested?.Invoke(msg, "warning");
            return;
        }

        RefreshChallengeDisplay();
        // Dirty-Flag zurücksetzen damit Prestige-Banner PP-Vorschau aktualisiert wird
        _lastPrestigeBannerPrestigeCount = -1;
    }

    /// <summary>
    /// Bricht den aktuellen Challenge-Run ab. Spieler erhält 50% der Basis-PP
    /// und spielt ohne Modifikatoren weiter.
    /// </summary>
    [RelayCommand]
    private async Task AbandonChallengeRun()
    {
        if (!_prestigeService.HasActiveChallenges) return;

        var title = _localizationService.GetString("AbandonChallengeTitle") ?? "Herausforderung aufgeben?";
        var msg = _localizationService.GetString("AbandonChallengeMessage")
                  ?? "Du erhältst 50% der Basis-Prestige-Punkte (ohne Challenge-Bonus). Die Herausforderungen werden deaktiviert.";

        var acceptText = _localizationService.GetString("AbandonChallengeButton") ?? "Aufgeben";
        var cancelText = _localizationService.GetString("Cancel") ?? "Abbrechen";
        bool confirmed = await DialogVM.ShowConfirmDialog(title, msg, acceptText, cancelText);
        if (!confirmed) return;

        int awardedPp = _prestigeService.AbandonChallengeRun();

        RefreshChallengeDisplay();
        _lastPrestigeBannerPrestigeCount = -1;
        RefreshPrestigeBanner(_gameStateService.State);

        if (awardedPp > 0)
        {
            var text = $"+{awardedPp} PP";
            FloatingTextRequested?.Invoke(text, "info");
            _audioService?.PlaySoundAsync(GameSound.CoinCollect).FireAndForget();
        }
    }

    /// <summary>
    /// Aktualisiert das kompakte Prestige-Tier-Badge im Dashboard-Header.
    /// Zeigt den höchsten abgeschlossenen Tier als farbiges Badge.
    /// </summary>
    private void UpdatePrestigeTierBadge(GameState state)
    {
        var prestigeData = state.Prestige;
        if (prestigeData.TotalPrestigeCount <= 0)
        {
            ShowPrestigeBadge = false;
            return;
        }

        // Höchsten abgeschlossenen Tier ermitteln (CurrentTier zeigt den aktuell aktiven)
        var tier = prestigeData.CurrentTier;
        if (tier == PrestigeTier.None)
        {
            // Mindestens 1 Prestige aber CurrentTier ist None → muss Bronze gewesen sein
            tier = PrestigeTier.Bronze;
        }

        ShowPrestigeBadge = true;
        PrestigeTierBadgeColor = tier.GetColorKey();

        // Kurztext: Erster Buchstabe des Tier-Namens (lokalisiert falls verfügbar)
        PrestigeTierBadgeText = tier switch
        {
            PrestigeTier.Bronze => "B",
            PrestigeTier.Silver => "S",
            PrestigeTier.Gold => "G",
            PrestigeTier.Platin => "P",
            PrestigeTier.Diamant => "D",
            PrestigeTier.Meister => "M",
            PrestigeTier.Legende => "L",
            _ => ""
        };
    }

    /// <summary>
    /// Aktualisiert den Boost-Indikator im Dashboard-Header.
    /// Zeigt den aktiven Multiplikator wenn Rush und/oder SpeedBoost aktiv sind.
    /// </summary>
    private void UpdateBoostIndicator()
    {
        var state = _gameStateService.State;
        bool rushActive = state.IsRushBoostActive;
        bool speedActive = state.IsSpeedBoostActive;

        if (!rushActive && !speedActive)
        {
            ShowBoostIndicator = false;
            return;
        }

        ShowBoostIndicator = true;

        // Multiplikator berechnen (identisch mit GameLoopService)
        decimal multiplier = 1m;
        if (speedActive) multiplier *= 2m;
        if (rushActive)
        {
            decimal rushMult = 2m;
            // Prestige-Shop Rush-Verstärker berücksichtigen
            var purchased = state.Prestige.PurchasedShopItems;
            foreach (var item in PrestigeShop.GetAllItems())
            {
                if (purchased.Contains(item.Id) && item.Effect.RushMultiplierBonus > 0)
                    rushMult += item.Effect.RushMultiplierBonus;
            }
            multiplier *= rushMult;
        }

        BoostIndicatorText = $"{multiplier:0.#}x";
    }

    private void UpdateWorkshopDisplay(WorkshopDisplayModel model, GameState state, WorkshopType type)
    {
        Workshop? workshop = null;
        for (int i = 0; i < state.Workshops.Count; i++)
            if (state.Workshops[i].Type == type) { workshop = state.Workshops[i]; break; }
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
        model.RebirthStars = workshop?.RebirthStars ?? 0;
        model.SpecializationBadge = GetSpecBadge(workshop?.WorkshopSpecialization);
        model.SpecializationColor = workshop?.WorkshopSpecialization?.Color ?? "";

        // BulkBuy-Kosten aktualisieren
        SetBulkUpgradeCost(model, workshop, state.Money);

        // Upgrade-Preview + Net-Income berechnen (Task #10, #13)
        SetWorkshopFinancials(model, workshop);

        model.NotifyAllChanged();
    }

    private void RefreshOrders()
    {
        var state = _gameStateService.State;
        // Collection-Referenz ersetzen statt Clear()+Add() → 1 statt N+1 Change-Notifications
        var newOrders = new ObservableCollection<Order>();

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
                OrderType.MaterialOrder => "#10B981",
                _ => ""
            };
            order.ShowOrderTypeBadge = order.OrderType != OrderType.Standard && order.OrderType != OrderType.Quick;

            // Lieferaufträge: Materialien-Info im Titel anzeigen
            if (order.OrderType == OrderType.MaterialOrder && order.RequiredMaterials != null)
            {
                var allProducts = CraftingProduct.GetAllProducts();
                var parts = new List<string>();
                foreach (var (productId, count) in order.RequiredMaterials)
                {
                    string name = allProducts.TryGetValue(productId, out var p)
                        ? _localizationService.GetString(p.NameKey) ?? p.NameKey
                        : productId;
                    int have = state.CraftingInventory.GetValueOrDefault(productId, 0);
                    parts.Add($"{name} {have}/{count}");
                }
                order.DisplayDescription = string.Join(", ", parts);
            }

            newOrders.Add(order);
        }

        AvailableOrders = newOrders;
        // Empty State (Task #8)
        HasNoOrders = AvailableOrders.Count == 0;
    }
}
