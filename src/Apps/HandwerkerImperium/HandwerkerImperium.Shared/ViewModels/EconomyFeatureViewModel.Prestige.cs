using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// EconomyFeatureViewModel — Prestige-Banner, Challenges, Tier-Badge.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
internal sealed partial class EconomyFeatureViewModel
{
    /// <summary>
    /// Aktualisiert Prestige-Banner-Anzeige (Task #14).
    /// Dirty-Flag: Nur neu berechnen wenn sich Level oder Prestige-Count geaendert hat.
    /// </summary>
    internal void RefreshPrestigeBanner(GameState state)
    {
        int currentLevel = state.PlayerLevel;
        int currentPrestigeCount = state.Prestige.TotalPrestigeCount;

        // Early-Exit: Nichts hat sich geaendert seit letztem Aufruf
        if (currentLevel == _lastPrestigeBannerLevel && currentPrestigeCount == _lastPrestigeBannerPrestigeCount)
            return;
        _lastPrestigeBannerLevel = currentLevel;
        _lastPrestigeBannerPrestigeCount = currentPrestigeCount;

        var highestTier = state.Prestige.GetHighestAvailableTier(currentLevel);
        _host.PrestigeBannerVM.IsPrestigeAvailable = highestTier != PrestigeTier.None;

        // F-18: Kumulierten Eternal-Mastery-Bonus anzeigen (skaliert mit Prestige-Count).
        decimal emBonus = GameBalanceConstants.EternalMasteryBonusPerPrestige * currentPrestigeCount
                        + GameBalanceConstants.EternalMasteryBonusPer5Prestiges * (currentPrestigeCount / 5)
                        + GameBalanceConstants.EternalMasteryBonusPer10Prestiges * (currentPrestigeCount / 10);
        _host.PrestigeBannerVM.HasEternalMasteryBonus = emBonus > 0;
        _host.PrestigeBannerVM.EternalMasteryBonusDisplay = emBonus > 0
            ? $"+{(emBonus * 100):0.#}% {_localizationService.GetString("EternalMasteryShort") ?? "Eternal Mastery"}"
            : "";

        if (_host.PrestigeBannerVM.IsPrestigeAvailable)
        {
            var potentialPoints = _prestigeService.GetPrestigePoints(state.CurrentRunMoney);
            int tierPoints = (int)(potentialPoints * highestTier.GetPointMultiplier());
            var pointsLabel = _localizationService.GetString("PrestigePoints") ?? "Prestige Points";
            _host.PrestigeBannerVM.PrestigePointsPreview = $"+{tierPoints} {pointsLabel}";

            _host.PrestigeBannerVM.PrestigePreviewTierName = _localizationService.GetString(highestTier.GetLocalizationKey()) ?? highestTier.ToString();

            // Gewinne (wiederverwendbare Liste statt new List<string>)
            decimal permanentBonus = highestTier.GetPermanentMultiplierBonus() * 100;
            _prestigeGains.Clear();
            _prestigeGains.Add($"+{tierPoints} {pointsLabel} (x{highestTier.GetPointMultiplier()})");
            _prestigeGains.Add($"+{permanentBonus:0}% {_localizationService.GetString("PermanentIncomeBonus") ?? "permanenter Einkommens-Bonus"}");
            if (highestTier.KeepsResearch())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsResearch") ?? "Research preserved!");
            if (highestTier.KeepsShopItems())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsShop") ?? "Prestige shop preserved!");
            if (highestTier.KeepsMasterTools())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsTools") ?? "Master tools preserved!");
            if (highestTier.KeepsBuildings())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsBuildings") ?? "Buildings preserved (Lv.1)!");
            if (highestTier.KeepsManagers())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsManagers") ?? "Managers preserved (Lv.1)!");
            if (highestTier.KeepsBestWorkers())
                _prestigeGains.Add(_localizationService.GetString("PrestigeKeepsWorkers") ?? "Best workers preserved!");
            _host.PrestigeBannerVM.PrestigePreviewGains = string.Join("\n", _prestigeGains);

            // Verluste (wiederverwendbare Liste statt new List<string>)
            _prestigeLosses.Clear();
            _prestigeLosses.Add(_localizationService.GetString("PrestigeLosesLevel") ?? "Player level → 1");
            _prestigeLosses.Add(_localizationService.GetString("PrestigeLosesMoney") ?? "Money → 0");
            _prestigeLosses.Add(_localizationService.GetString("PrestigeLosesWorkers") ?? "Workers → dismissed");
            if (!highestTier.KeepsResearch())
                _prestigeLosses.Add(_localizationService.GetString("PrestigeLosesResearch") ?? "Research → reset");
            _host.PrestigeBannerVM.PrestigePreviewLosses = string.Join("\n", _prestigeLosses);

            // Geschätzter Speed-Up
            decimal currentMult = state.Prestige.PermanentMultiplier;
            decimal newMult = currentMult + highestTier.GetPermanentMultiplierBonus();
            int speedUpPercent = currentMult > 0 ? (int)((newMult / currentMult - 1m) * 100) : 100;
            _host.PrestigeBannerVM.PrestigePreviewSpeedUp = $"~{speedUpPercent}% {_localizationService.GetString("Faster") ?? "schneller"}";
        }
        else
        {
            _host.PrestigeBannerVM.PrestigePointsPreview = "";
            _host.PrestigeBannerVM.PrestigePreviewGains = "";
            _host.PrestigeBannerVM.PrestigePreviewLosses = "";
            _host.PrestigeBannerVM.PrestigePreviewSpeedUp = "";
            _host.PrestigeBannerVM.PrestigePreviewTierName = "";
        }

        // Fortschritt zum nächsten Tier (auch anzeigen wenn aktuell kein Prestige verfügbar)
        var nextTier = highestTier.GetNextTier();
        if (nextTier != PrestigeTier.None)
        {
            _host.PrestigeBannerVM.HasNextPrestigeTier = true;
            var reqLevel = nextTier.GetRequiredLevel();
            var currentTierLevel = highestTier != PrestigeTier.None ? highestTier.GetRequiredLevel() : 0;
            var range = reqLevel - currentTierLevel;
            var progress = range > 0
                ? Math.Clamp((double)(currentLevel - currentTierLevel) / range, 0.0, 1.0)
                : 0.0;
            _host.PrestigeBannerVM.NextPrestigeTierProgress = progress;
            var tierName = _localizationService.GetString(nextTier.GetLocalizationKey()) ?? nextTier.ToString();

            // PP-Prognose: "Bei Gold: +400 PP"
            var potentialPP = _prestigeService.GetPrestigePoints(state.CurrentRunMoney);
            int nextTierPoints = (int)(potentialPP * nextTier.GetPointMultiplier());
            _host.PrestigeBannerVM.NextPrestigeTierHint = nextTierPoints > 0
                ? $"Lv. {currentLevel}/{reqLevel} → {tierName} (+{nextTierPoints} PP)"
                : $"Lv. {currentLevel}/{reqLevel} → {tierName}";
        }
        else
        {
            _host.PrestigeBannerVM.HasNextPrestigeTier = false;
            _host.PrestigeBannerVM.NextPrestigeTierHint = "";
            _host.PrestigeBannerVM.NextPrestigeTierProgress = 0;
        }

        // Tier-Auswahl wird dynamisch beim Öffnen des Prestige-Dialogs in DialogVM gesetzt

        // Challenge-Anzeige aktualisieren
        RefreshChallengeDisplay();

        // Speedrun-Timer aktualisieren
        var runDuration = _prestigeService.GetCurrentRunDuration();
        _host.PrestigeBannerVM.CurrentRunDuration = runDuration.HasValue
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

        // PP-2: Challenge-Chip aktiv/inaktiv State
        var set = challenges != null ? new HashSet<PrestigeChallengeType>(challenges) : [];
        _host.PrestigeBannerVM.IsChallengeSpartanerActive = set.Contains(PrestigeChallengeType.Spartaner);
        _host.PrestigeBannerVM.IsChallengeOhneForschungActive = set.Contains(PrestigeChallengeType.OhneForschung);
        _host.PrestigeBannerVM.IsChallengeInflationszeitActive = set.Contains(PrestigeChallengeType.Inflationszeit);
        _host.PrestigeBannerVM.IsChallengeSoloMeisterActive = set.Contains(PrestigeChallengeType.SoloMeister);
        _host.PrestigeBannerVM.IsChallengeSprintActive = set.Contains(PrestigeChallengeType.Sprint);
        _host.PrestigeBannerVM.IsChallengeKeinNetzActive = set.Contains(PrestigeChallengeType.KeinNetz);

        if (challenges == null || challenges.Count == 0)
        {
            _host.PrestigeBannerVM.ActiveChallengeCount = 0;
            _host.PrestigeBannerVM.ActiveChallengesText = "";
            return;
        }

        _host.PrestigeBannerVM.ActiveChallengeCount = challenges.Count;
        var parts = new List<string>(challenges.Count);
        for (int i = 0; i < challenges.Count; i++)
        {
            var c = challenges[i];
            var name = _localizationService.GetString(c.GetNameKey()) ?? c.ToString();
            parts.Add($"{name} +{c.GetPpBonus() * 100:0}%");
        }
        _host.PrestigeBannerVM.ActiveChallengesText = string.Join(", ", parts);
    }

    /// <summary>
    /// Challenge aktivieren/deaktivieren (Toggle). Aufgerufen aus UI (ImperiumView).
    /// </summary>
    internal void ToggleChallenge(string challengeName)
    {
        if (!Enum.TryParse<PrestigeChallengeType>(challengeName, out var challenge))
            return;

        bool success = _challengeConstraints?.ToggleChallenge(challenge) ?? false;
        if (!success)
        {
            // SoloMeister + QuickStart oder Max erreicht
            var msg = _localizationService.GetString("ChallengesMaxReached") ?? "Maximum 3 challenges";
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
    internal async Task AbandonChallengeRun()
    {
        if (!_prestigeService.HasActiveChallenges) return;

        var title = _localizationService.GetString("AbandonChallengeTitle") ?? "Abandon Challenge?";
        var msg = _localizationService.GetString("AbandonChallengeMessage")
                  ?? "You will receive 50% of the base prestige points (without challenge bonus). All challenges will be deactivated.";

        var acceptText = _localizationService.GetString("AbandonChallengeButton") ?? "Abandon";
        var cancelText = _localizationService.GetString("Cancel") ?? "Cancel";
        bool confirmed = await _host.DialogVM.ShowConfirmDialog(title, msg, acceptText, cancelText);
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
            _host.ShowPrestigeBadge = false;
            return;
        }

        // Höchsten abgeschlossenen Tier ermitteln (CurrentTier zeigt den aktuell aktiven)
        var tier = prestigeData.CurrentTier;
        if (tier == PrestigeTier.None)
        {
            // Mindestens 1 Prestige aber CurrentTier ist None → muss Bronze gewesen sein
            tier = PrestigeTier.Bronze;
        }

        _host.ShowPrestigeBadge = true;
        _host.PrestigeTierBadgeColor = tier.GetColorKey();

        // Kurztext: Erster Buchstabe des Tier-Namens (lokalisiert falls verfügbar)
        _host.PrestigeTierBadgeText = tier switch
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
}
