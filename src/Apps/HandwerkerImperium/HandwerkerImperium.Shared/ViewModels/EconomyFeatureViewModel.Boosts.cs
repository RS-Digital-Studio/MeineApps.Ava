using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// EconomyFeatureViewModel — Boosts: Feierabend-Rush, Lieferant, Boost-Indikator.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
internal sealed partial class EconomyFeatureViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // FEIERABEND-RUSH
    // ═══════════════════════════════════════════════════════════════════════

    private const int RushCostScrews = 10;
    private const int RushDurationHours = 2;
    private const int RushAdDurationHours = 1;

    internal async Task ActivateRushAsync()
    {
        var state = _gameStateService.State;
        if (state.IsRushBoostActive) return;

        if (state.IsFreeRushAvailable)
        {
            // Täglicher Gratis-Rush (2h)
            state.RushBoostEndTime = DateTime.UtcNow.AddHours(RushDurationHours);
            state.LastFreeRushUsed = DateTime.UtcNow;
            _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
            FloatingTextRequested?.Invoke($"Rush 2x ({RushDurationHours}h)!", "Rush");
            CelebrationRequested?.Invoke();
        }
        else if (_rewardedAdService.IsAvailable && !_purchaseService.IsPremium)
        {
            // BAL-AD-5: Video-Rush (1h) als Alternative zu 10 GS (2h)
            var watchVideo = await _dialogService.ShowConfirmDialog(
                _localizationService.GetString("ActivateRush") ?? "Activate Rush",
                string.Format(
                    _localizationService.GetString("RushChoiceDesc") ?? "Video = {0}h Rush\n{1} Schrauben = {2}h Rush",
                    RushAdDurationHours, RushCostScrews, RushDurationHours),
                string.Format(_localizationService.GetString("WatchVideoRush") ?? "Video ({0}h)", RushAdDurationHours),
                string.Format(_localizationService.GetString("PayScrewsRush") ?? "{0} GS ({1}h)", RushCostScrews, RushDurationHours));

            if (watchVideo)
            {
                var success = await _rewardedAdService.ShowAdAsync("rush_boost");
                if (success)
                {
                    state.RushBoostEndTime = DateTime.UtcNow.AddHours(RushAdDurationHours);
                    _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
                    FloatingTextRequested?.Invoke($"Rush 2x ({RushAdDurationHours}h)!", "Rush");
                }
            }
            else if (_gameStateService.TrySpendGoldenScrews(RushCostScrews))
            {
                state.RushBoostEndTime = DateTime.UtcNow.AddHours(RushDurationHours);
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
        }
        else if (_gameStateService.TrySpendGoldenScrews(RushCostScrews))
        {
            // Bezahlter Rush — Premium oder keine Ad verfügbar
            state.RushBoostEndTime = DateTime.UtcNow.AddHours(RushDurationHours);
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

    internal void UpdateRushDisplay()
    {
        var state = _gameStateService.State;
        _host.IsRushActive = state.IsRushBoostActive;

        if (_host.IsRushActive)
        {
            var remaining = state.RushBoostEndTime - DateTime.UtcNow;
            _host.RushTimeRemaining = remaining.TotalMinutes >= 60
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
                : $"{remaining.Minutes}m {remaining.Seconds:D2}s";
            _host.CanActivateRush = false;
            _host.RushButtonText = _host.RushTimeRemaining;
        }
        else
        {
            _host.RushTimeRemaining = "";
            _host.CanActivateRush = true;
            _host.RushButtonText = state.IsFreeRushAvailable
                ? _localizationService.GetString("RushFreeActivation")
                : $"Rush ({RushCostScrews} GS)";
        }

        // Boost-Indikator mit-aktualisieren (Rush-Status hat sich geändert)
        UpdateBoostIndicator();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIEFERANT
    // ═══════════════════════════════════════════════════════════════════════

    internal void ClaimDelivery()
    {
        var state = _gameStateService.State;
        var delivery = state.PendingDelivery;
        if (delivery == null || delivery.IsExpired)
        {
            _host.HasPendingDelivery = false;
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
                FloatingTextRequested?.Invoke($"+{screwAmount} ⚙", "screw");
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

            case Models.Enums.DeliveryType.Material:
                // V7 (): Material direkt ins Lager (mit Stack-Limit).
                if (!string.IsNullOrEmpty(delivery.MaterialProductId))
                {
                    int requested = (int)Math.Round(delivery.Amount);
                    // Research-Bonus: SupplierMaterialBonus erhoeht die Menge (Plan logi_08).
                    int bonusMult = 1 + (int)Math.Round(_researchService?.GetTotalEffects().SupplierMaterialBonus * 1m ?? 0);
                    int total = requested + Math.Max(0, (int)Math.Round(requested * (_researchService?.GetTotalEffects().SupplierMaterialBonus ?? 0m)));
                    int added = _warehouseService?.AddToInventory(delivery.MaterialProductId, total) ?? 0;
                    if (added > 0)
                    {
                        var allProducts = CraftingProduct.GetAllProducts();
                        string name = allProducts.TryGetValue(delivery.MaterialProductId, out var p)
                            ? _localizationService.GetString(p.NameKey) ?? p.NameKey
                            : delivery.MaterialProductId;
                        FloatingTextRequested?.Invoke($"+{added}x {name}", "material");
                    }
                    _ = bonusMult; // documentation only — Bonus ist in `total` enthalten
                }
                break;
        }

        _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
        state.Statistics.TotalDeliveriesClaimed++;
        state.PendingDelivery = null;
        _host.HasPendingDelivery = false;
    }

    internal void UpdateDeliveryDisplay()
    {
        var delivery = _gameStateService.State.PendingDelivery;
        if (delivery == null || delivery.IsExpired)
        {
            if (_host.HasPendingDelivery)
            {
                _host.HasPendingDelivery = false;
                _gameStateService.State.PendingDelivery = null;
            }
            return;
        }

        _host.HasPendingDelivery = true;
        _host.DeliveryIcon = delivery.Icon;
        _host.DeliveryDescription = _localizationService.GetString(delivery.DescriptionKey);

        _host.DeliveryAmountText = delivery.Type switch
        {
            Models.Enums.DeliveryType.Money => MoneyFormatter.FormatCompact(delivery.Amount),
            Models.Enums.DeliveryType.GoldenScrews => $"{(int)delivery.Amount} ⚙",
            Models.Enums.DeliveryType.Experience => $"{(int)delivery.Amount} XP",
            Models.Enums.DeliveryType.MoodBoost => $"+{(int)delivery.Amount} Mood",
            Models.Enums.DeliveryType.SpeedBoost => $"{(int)delivery.Amount}min 2x",
            _ => ""
        };

        var remaining = delivery.TimeRemaining;
        _host.DeliveryTimeRemaining = $"{remaining.Minutes}:{remaining.Seconds:D2}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOOST-INDIKATOR
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert den Boost-Indikator im Dashboard-Header.
    /// Zeigt den aktiven Multiplikator wenn Rush und/oder SpeedBoost aktiv sind.
    /// </summary>
    internal void UpdateBoostIndicator()
    {
        var state = _gameStateService.State;
        bool rushActive = state.IsRushBoostActive;
        bool speedActive = state.IsSpeedBoostActive;

        if (!rushActive && !speedActive)
        {
            _host.ShowBoostIndicator = false;
            return;
        }

        _host.ShowBoostIndicator = true;

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

        _host.BoostIndicatorText = $"{multiplier:0.#}x";
    }
}
