using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// EconomyFeatureViewModel — Werkstätten: Auswahl, Kauf, Upgrade, Hire, BulkBuy + Display-Aufbau.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
internal sealed partial class EconomyFeatureViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP SELECTION + KAUF
    // ═══════════════════════════════════════════════════════════════════════

    internal async Task SelectWorkshopAsync(WorkshopDisplayModel workshop)
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
        _host.WorkshopViewModel.SetWorkshopType(workshop.Type);
        _host.ActivePage = ActivePage.WorkshopDetail;
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

            // Telemetrie: Workshop-Unlocks sind ein zentraler Fortschritts-Marker.
            // Wichtig fuer Funnel-Analyse (wie viele Spieler erreichen Workshop X?).
            _analyticsService?.TrackEvent(Models.AnalyticsEvents.WorkshopUnlocked, new Dictionary<string, object?>
            {
                ["type"] = type.ToString(),
                ["player_level"] = _gameStateService.State.PlayerLevel,
                ["total_earned"] = (double)_gameStateService.State.TotalMoneyEarned
            });
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

    internal void CycleBulkBuy()
    {
        _host.BulkBuyAmount = _host.BulkBuyAmount switch
        {
            1 => 10,
            10 => 100,
            100 => 0, // Max
            _ => 1
        };
        _host.BulkBuyLabel = _host.BulkBuyAmount switch
        {
            0 => "Max",
            _ => $"x{_host.BulkBuyAmount}"
        };
        RefreshWorkshops();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP UPGRADE + HIRE
    // ═══════════════════════════════════════════════════════════════════════

    internal async Task UpgradeWorkshopAsync(WorkshopDisplayModel workshop)
    {
        if (!workshop.IsUnlocked || !workshop.CanUpgrade)
            return;

        if (_host.BulkBuyAmount == 1)
        {
            if (_gameStateService.TryUpgradeWorkshop(workshop.Type))
            {
                await _audioService.PlaySoundAsync(GameSound.Upgrade);
                FloatingTextRequested?.Invoke("+1 Level!", "level");
            }
        }
        else
        {
            int upgraded = _gameStateService.TryUpgradeWorkshopBulk(workshop.Type, _host.BulkBuyAmount);
            if (upgraded > 0)
            {
                await _audioService.PlaySoundAsync(GameSound.Upgrade);
                FloatingTextRequested?.Invoke($"+{upgraded} Level!", "level");
            }
        }
    }

    internal async Task HireWorkerAsync(WorkshopDisplayModel workshop)
    {
        if (!workshop.IsUnlocked || !workshop.CanHireWorker)
            return;

        // Zum Arbeitermarkt navigieren statt direkt zu hiren
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
        _host.SelectWorkerMarketTab();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP DISPLAY (State → UI)
    // ═══════════════════════════════════════════════════════════════════════

    internal void RefreshWorkshops()
    {
        var state = _gameStateService.State;

        // Erste Initialisierung: Items erstellen
        if (_host.Workshops.Count == 0)
        {
            foreach (var type in MainViewModel._workshopTypes)
            {
                _host.Workshops.Add(CreateWorkshopDisplay(state, type));
            }
        }
        else
        {
            // Update: Bestehende Items aktualisieren (kein Clear/Add → weniger UI-Churn)
            for (int i = 0; i < MainViewModel._workshopTypes.Length && i < _host.Workshops.Count; i++)
            {
                UpdateWorkshopDisplay(_host.Workshops[i], state, MainViewModel._workshopTypes[i]);
            }
        }

        // Workshop-Canvas-Höhe aktualisieren (dynamisch basierend auf Anzahl)
        _host.OnPropertyChanged(nameof(MainViewModel.WorkshopCanvasHeight));

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
    internal void RefreshSingleWorkshop(WorkshopType type)
    {
        var state = _gameStateService.State;
        var index = Array.IndexOf(MainViewModel._workshopTypes, type);
        if (index >= 0 && index < _host.Workshops.Count)
        {
            UpdateWorkshopDisplay(_host.Workshops[index], state, type);
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
            IconKind = MainViewModel.GetWorkshopIconKind(type, workshop?.Level ?? 1),
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
    /// Setzt BulkUpgradeCost und BulkUpgradeLabel basierend auf aktuellem _host.BulkBuyAmount.
    /// </summary>
    internal void SetBulkUpgradeCost(WorkshopDisplayModel model, Workshop? workshop, decimal money)
    {
        if (workshop == null || !workshop.CanUpgrade)
        {
            model.BulkUpgradeCost = 0;
            model.BulkUpgradeLabel = "";
            return;
        }

        if (_host.BulkBuyAmount == 0) // Max
        {
            var (count, cost) = workshop.GetMaxAffordableUpgrades(money);
            model.BulkUpgradeCost = cost;
            model.BulkUpgradeLabel = count > 0 ? $"Max ({count})" : "Max";
            model.CanAffordUpgrade = count > 0;
        }
        else if (_host.BulkBuyAmount == 1)
        {
            model.BulkUpgradeCost = workshop.UpgradeCost;
            model.BulkUpgradeLabel = "";
            model.CanAffordUpgrade = money >= workshop.UpgradeCost;
        }
        else
        {
            model.BulkUpgradeCost = workshop.GetBulkUpgradeCost(_host.BulkBuyAmount);
            model.BulkUpgradeLabel = $"x{_host.BulkBuyAmount}";
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
            int upgradeCount = _host.BulkBuyAmount == 0 ? 10 : _host.BulkBuyAmount; // Max → zeige Preview für ~10 Level
            int targetLevel = Math.Min(workshop.Level + upgradeCount, Workshop.MaxLevel);
            // Einkommen bei Ziel-Level ueber die kanonische Formel (inkl. Meilenstein-Multiplikator);
            // keine duplizierte Magic-Number 1.02 mehr — sonst driftet die Preview an Meilenstein-Leveln.
            decimal currentBase = WorkshopFormulas.CalculateBaseIncomePerWorker(workshop.Level, workshop.Type);
            decimal targetBase = WorkshopFormulas.CalculateBaseIncomePerWorker(targetLevel, workshop.Type);
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

    private void UpdateWorkshopDisplay(WorkshopDisplayModel model, GameState state, WorkshopType type)
    {
        Workshop? workshop = null;
        for (int i = 0; i < state.Workshops.Count; i++)
            if (state.Workshops[i].Type == type) { workshop = state.Workshops[i]; break; }
        bool isUnlocked = state.IsWorkshopUnlocked(type);

        model.Name = _localizationService.GetString(type.GetLocalizationKey());
        model.Level = workshop?.Level ?? 1;
        model.IconKind = MainViewModel.GetWorkshopIconKind(type, model.Level);
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
}
