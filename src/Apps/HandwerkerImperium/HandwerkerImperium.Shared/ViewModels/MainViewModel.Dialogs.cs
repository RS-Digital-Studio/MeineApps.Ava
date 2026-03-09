using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Alert/Confirm-Dialoge, Story-Dialog, Kontextuelle Hints, Prestige-Bestätigung
public sealed partial class MainViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // DISMISS COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void DismissLevelUpDialog()
    {
        IsLevelUpDialogVisible = false;
    }

    [RelayCommand]
    private void DismissAchievementDialog()
    {
        IsAchievementDialogVisible = false;
    }

    [RelayCommand]
    private void DismissAlertDialog()
    {
        IsAlertDialogVisible = false;
    }

    [RelayCommand]
    private void DismissPrestigeSummary()
    {
        IsPrestigeSummaryVisible = false;
    }

    [RelayCommand]
    private void PrestigeSummaryGoToShop()
    {
        IsPrestigeSummaryVisible = false;
        // Zum Statistik-Tab navigieren (Prestige-Shop ist dort)
        SelectStatisticsTab();
    }

    [RelayCommand]
    private void ConfirmDialogAccept()
    {
        IsConfirmDialogVisible = false;
        IsPrestigeTierSelectionVisible = false;
        _confirmDialogTcs?.TrySetResult(true);
    }

    [RelayCommand]
    private void ConfirmDialogCancel()
    {
        IsConfirmDialogVisible = false;
        IsPrestigeTierSelectionVisible = false;
        _confirmDialogTcs?.TrySetResult(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ALERT / CONFIRM DIALOG
    // ═══════════════════════════════════════════════════════════════════════

    private void ShowAlertDialog(string title, string message, string buttonText)
    {
        AlertDialogTitle = title;
        AlertDialogMessage = message;
        AlertDialogButtonText = buttonText;
        IsAlertDialogVisible = true;
    }

    /// <summary>
    /// Zeigt einen FloatingText-Hinweis wenn ein gesperrter Tab angetippt wird.
    /// </summary>
    public void ShowLockedTabHint(int requiredLevel)
    {
        var text = string.Format(
            _localizationService.GetString("TabLockedHint") ?? "Ab Level {0} verfügbar",
            requiredLevel);
        FloatingTextRequested?.Invoke(text, "info");
    }

    private Task<bool> ShowConfirmDialog(string title, string message, string acceptText, string cancelText)
    {
        ConfirmDialogTitle = title;
        ConfirmDialogMessage = message;
        ConfirmDialogAcceptText = acceptText;
        ConfirmDialogCancelText = cancelText;
        IsPrestigeTierSelectionVisible = false; // Tier-Chips ausblenden bei generischen Dialogen
        _confirmDialogTcs = new TaskCompletionSource<bool>();
        IsConfirmDialogVisible = true;

        // Ad-Banner ausblenden damit es nicht den Dialog verdeckt
        _adService.HideBanner();

        return _confirmDialogTcs.Task;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE-BESTÄTIGUNG
    // ═══════════════════════════════════════════════════════════════════════

    // Merkt sich den aktuell ausgewählten Tier im Prestige-Dialog
    private PrestigeTier _dialogSelectedTier = PrestigeTier.None;

    /// <summary>
    /// Tier-Auswahl im Prestige-Dialog: Aktualisiert die Vorschau beim Wechsel.
    /// Parameter ist der Tier-Name als String (z.B. "Bronze", "Silver").
    /// </summary>
    [RelayCommand]
    private void SelectPrestigeTier(string tierName)
    {
        if (!Enum.TryParse<PrestigeTier>(tierName, out var tier)) return;

        var idx = AvailablePrestigeTierOptions.FindIndex(o => o.Tier == tier);
        if (idx < 0) return;

        SelectedPrestigeTierIndex = idx;

        // IsSelected aktualisieren
        for (int i = 0; i < AvailablePrestigeTierOptions.Count; i++)
            AvailablePrestigeTierOptions[i].IsSelected = i == idx;
        OnPropertyChanged(nameof(AvailablePrestigeTierOptions));

        _dialogSelectedTier = tier;

        // Dialog-Inhalt dynamisch aktualisieren
        UpdatePrestigeDialogContent(tier);
    }

    /// <summary>
    /// Baut die Dialog-Texte für einen bestimmten Tier auf.
    /// </summary>
    private void UpdatePrestigeDialogContent(PrestigeTier tier)
    {
        var state = _gameStateService.State;
        var tierName = _localizationService.GetString(tier.GetLocalizationKey()) ?? tier.ToString();
        var potentialPoints = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
        int tierPoints = (int)(potentialPoints * tier.GetPointMultiplier());

        // 1. GEWINNE (prominent, oben)
        var gains = new List<string>();
        gains.Add($"\u2b06 +{tierPoints} PP ({tierName} x{tier.GetPointMultiplier()})");
        gains.Add($"\u2b06 +{tier.GetPermanentMultiplierBonus():P0} {_localizationService.GetString("PermanentIncomeBonus") ?? "permanenter Einkommens-Bonus"}");

        // Speed-Up Prognose
        decimal currentMult = state.Prestige.PermanentMultiplier;
        decimal newMult = currentMult + tier.GetPermanentMultiplierBonus();
        if (currentMult > 0)
        {
            int speedUp = (int)((newMult / currentMult - 1m) * 100);
            gains.Add($"\u26a1 ~{speedUp}% {_localizationService.GetString("Faster") ?? "schneller als vorher"}");
        }

        // 2. BEWAHRUNG (positiv formuliert)
        if (tier.KeepsResearch())
            gains.Add($"\u2713 {_localizationService.GetString("PrestigeKeepsResearch") ?? "Forschung bleibt erhalten"}");
        if (tier.KeepsMasterTools())
            gains.Add($"\u2713 {_localizationService.GetString("PrestigeKeepsTools") ?? "Meisterwerkzeuge bleiben"}");
        if (tier.KeepsBuildings())
            gains.Add($"\u2713 {_localizationService.GetString("PrestigeKeepsBuildings") ?? "Gebäude bleiben (Lv.1)"}");
        if (tier.KeepsManagers())
            gains.Add($"\u2713 {_localizationService.GetString("PrestigeKeepsManagers") ?? "Manager bleiben (Lv.1)"}");
        if (tier.KeepsBestWorkers())
            gains.Add($"\u2713 {_localizationService.GetString("PrestigeKeepsWorkers") ?? "Beste Worker pro Workshop bleiben"}");

        // 3. VERLUSTE (kompakt, unten, neutral formuliert)
        var resetNote = _localizationService.GetString("PrestigeResetNote")
                        ?? "Level, Geld und Workshops werden zurückgesetzt.";

        ConfirmDialogTitle = $"{_localizationService.GetString("Prestige") ?? "Prestige"} \u2192 {tierName}";
        ConfirmDialogMessage = string.Join("\n", gains) + $"\n\n{resetNote}";
    }

    /// <summary>
    /// Zeigt den Prestige-Bestätigungsdialog und führt bei Bestätigung Prestige durch.
    /// Wird sowohl vom Dashboard-Banner als auch vom Statistik-Tab aufgerufen.
    /// Bei mehreren verfügbaren Tiers zeigt der Dialog Auswahl-Chips.
    /// </summary>
    private async Task ShowPrestigeConfirmationAsync()
    {
        var state = _gameStateService.State;
        var availableTiers = state.Prestige.GetAllAvailableTiers(state.PlayerLevel);

        if (availableTiers.Count == 0)
        {
            var minLevel = PrestigeTier.Bronze.GetRequiredLevel();
            ShowAlertDialog(
                _localizationService.GetString("PrestigeNotAvailable") ?? "Prestige nicht verfügbar",
                string.Format(
                    _localizationService.GetString("PrestigeNotAvailableDesc") ?? "Du benötigst Level {0} (aktuell Level {1})",
                    minLevel, state.PlayerLevel),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Höchster Tier als Standard vorauswählen
        var highestTier = availableTiers[^1];
        _dialogSelectedTier = highestTier;

        // Tier-Optionen für Auswahl-Chips aufbauen
        var potentialPoints = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
        var options = new List<PrestigeTierOption>();
        for (int i = 0; i < availableTiers.Count; i++)
        {
            var t = availableTiers[i];
            var tName = _localizationService.GetString(t.GetLocalizationKey()) ?? t.ToString();
            int tPoints = (int)(potentialPoints * t.GetPointMultiplier());
            var preservations = new List<string>();
            if (t.KeepsResearch()) preservations.Add(_localizationService.GetString("Research") ?? "Forschung");
            if (t.KeepsMasterTools()) preservations.Add(_localizationService.GetString("MasterTools") ?? "Werkzeuge");
            if (t.KeepsBuildings()) preservations.Add(_localizationService.GetString("Buildings") ?? "Gebäude");
            if (t.KeepsManagers()) preservations.Add(_localizationService.GetString("Managers") ?? "Manager");
            if (t.KeepsBestWorkers()) preservations.Add(_localizationService.GetString("Workers") ?? "Worker");

            options.Add(new PrestigeTierOption
            {
                Tier = t,
                Name = tName,
                Icon = t.GetIcon(),
                Color = t.GetColorKey(),
                Points = tPoints,
                PointsText = $"+{tPoints} PP",
                BonusText = $"+{t.GetPermanentMultiplierBonus():P0}",
                PreservationText = preservations.Count > 0 ? string.Join(", ", preservations) : "",
                IsSelected = t == highestTier,
                IsRecommended = t == highestTier,
            });
        }
        AvailablePrestigeTierOptions = options;
        SelectedPrestigeTierIndex = availableTiers.Count - 1;
        HasMultiplePrestigeTiers = availableTiers.Count > 1;

        // Dialog-Inhalt für den höchsten Tier aufbauen
        UpdatePrestigeDialogContent(highestTier);

        var confirmed = await ShowPrestigeConfirmDialogInternal();

        if (!confirmed) return;

        // Den vom Spieler ausgewählten Tier verwenden
        var selectedTier = _dialogSelectedTier;
        var tierName = _localizationService.GetString(selectedTier.GetLocalizationKey()) ?? selectedTier.ToString();
        int tierPoints = (int)(potentialPoints * selectedTier.GetPointMultiplier());

        var success = await _prestigeService.DoPrestige(selectedTier);
        if (success)
        {
            // Prestige-Effekt-Cache invalidieren (Shop-Items zurückgesetzt)
            _gameLoopService.InvalidatePrestigeEffects();

            await _audioService.PlaySoundAsync(GameSound.LevelUp);

            // UI komplett neu laden
            SelectDashboardTab();
            OnStateLoaded(this, EventArgs.Empty);

            // Celebration
            FloatingTextRequested?.Invoke($"{selectedTier.GetIcon()} {tierName}!", "level");

            // Ziel-Cache invalidieren (Prestige ändert den gesamten Spielzustand)
            _goalService.Invalidate();

            // Post-Prestige Zusammenfassung anzeigen
            ShowPrestigeSummary(selectedTier, tierPoints);
        }

        // Tier-Optionen zurücksetzen
        HasMultiplePrestigeTiers = false;
        AvailablePrestigeTierOptions = [];
    }

    /// <summary>
    /// Interner Prestige-Confirm-Dialog der Tier-Auswahl unterstützt.
    /// Nutzt den generischen ConfirmDialog + IsPrestigeTierSelectionVisible Flag.
    /// </summary>
    private Task<bool> ShowPrestigeConfirmDialogInternal()
    {
        ConfirmDialogAcceptText = _localizationService.GetString("PrestigeConfirm") ?? "Prestige durchführen";
        ConfirmDialogCancelText = _localizationService.GetString("Cancel") ?? "Abbrechen";
        IsPrestigeTierSelectionVisible = HasMultiplePrestigeTiers;

        _confirmDialogTcs = new TaskCompletionSource<bool>();
        IsConfirmDialogVisible = true;

        // Ad-Banner ausblenden damit es nicht den Dialog verdeckt
        _adService.HideBanner();

        return _confirmDialogTcs.Task;
    }

    /// <summary>
    /// Zeigt die Post-Prestige Zusammenfassung mit PP, Multiplikator und Shop-Link.
    /// </summary>
    private void ShowPrestigeSummary(PrestigeTier tier, int pointsEarned)
    {
        var state = _gameStateService.State;
        var tierName = _localizationService.GetString(tier.GetLocalizationKey()) ?? tier.ToString();

        PrestigeSummaryTier = tierName;
        PrestigeSummaryTierIcon = tier.GetIcon();
        PrestigeSummaryTierColor = tier.GetColorKey();
        PrestigeSummaryPoints = $"+{pointsEarned} PP";
        PrestigeSummaryMultiplier = $"{state.Prestige.PermanentMultiplier:F1}x";

        // Tier-spezifischer Count
        var count = tier switch
        {
            PrestigeTier.Bronze => state.Prestige.BronzeCount,
            PrestigeTier.Silver => state.Prestige.SilverCount,
            PrestigeTier.Gold => state.Prestige.GoldCount,
            PrestigeTier.Platin => state.Prestige.PlatinCount,
            PrestigeTier.Diamant => state.Prestige.DiamantCount,
            PrestigeTier.Meister => state.Prestige.MeisterCount,
            PrestigeTier.Legende => state.Prestige.LegendeCount,
            _ => state.Prestige.TotalPrestigeCount
        };
        PrestigeSummaryCount = $"#{count}";

        IsPrestigeSummaryVisible = true;
    }

    /// <summary>
    /// Zeigt Reputations-Info-Dialog mit Level und Multiplikator.
    /// </summary>
    [RelayCommand]
    private void ShowReputationInfo()
    {
        var rep = _gameStateService.State.Reputation;
        var level = _localizationService.GetString(rep.ReputationLevelKey)
                    ?? rep.ReputationLevelKey;
        var multiplier = rep.ReputationMultiplier;
        AlertDialogTitle = _localizationService.GetString("Reputation") ?? "Reputation";
        AlertDialogMessage = $"{level} ({rep.ReputationScore}/100)\n\u00d7{multiplier:F1} {_localizationService.GetString("IncomeBonus") ?? "Einkommensbonus"}";
        AlertDialogButtonText = "OK";
        IsAlertDialogVisible = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STORY-DIALOG COMMANDS (Meister Hans NPC)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void DismissStoryDialog()
    {
        if (!string.IsNullOrEmpty(StoryChapterId))
        {
            // Belohnungen werden im StoryService.MarkChapterViewed() vergeben
            _storyService?.MarkChapterViewed(StoryChapterId);
            HasNewStory = false;
        }
        IsStoryDialogVisible = false;
        CheckDeferredDialogs();

        // FloatingText für Belohnungen
        if (!string.IsNullOrEmpty(StoryRewardText))
        {
            FloatingTextRequested?.Invoke(StoryRewardText, "golden_screws");
        }
    }

    /// <summary>
    /// Prüft ob ein neues Story-Kapitel freigeschaltet wurde.
    /// Wird nach Level-Up, Workshop-Upgrade und Auftragsabschluss aufgerufen.
    /// </summary>
    private void CheckForNewStoryChapter()
    {
        var chapter = _storyService?.CheckForNewChapter();
        if (chapter != null)
        {
            HasNewStory = true;
            // Dialog wird erst beim nächsten passenden Moment angezeigt
            // (nicht sofort, damit Level-Up/Achievement-Dialoge nicht kollidieren)
            Dispatcher.UIThread.Post(() =>
            {
                // Warte kurz damit andere Dialoge zuerst angezeigt werden
                if (!IsLevelUpDialogVisible && !IsAchievementDialogVisible && !IsDailyRewardDialogVisible && !IsHoldingUpgrade)
                {
                    ShowStoryDialog(chapter);
                }
            }, DispatcherPriority.Background);
        }
    }

    private void ShowStoryDialog(StoryChapter chapter)
    {
        var title = _localizationService.GetString(chapter.TitleKey);
        var text = _localizationService.GetString(chapter.TextKey);

        StoryTitle = string.IsNullOrEmpty(title) ? chapter.TitleFallback : title;
        StoryText = string.IsNullOrEmpty(text) ? chapter.TextFallback : text;
        StoryMood = chapter.Mood;
        StoryChapterId = chapter.Id;
        StoryChapterNumber = chapter.ChapterNumber;
        StoryTotalChapters = 25;
        IsStoryTutorial = chapter.IsTutorial;
        StoryChapterBadge = chapter.IsTutorial
            ? _localizationService.GetString("StoryTipFromHans") ?? "Tipp von Meister Hans"
            : $"Kap. {chapter.ChapterNumber}/25";

        // Belohnungs-Text zusammenstellen (skalierte Geldbelohnung anzeigen)
        var rewards = new List<string>();
        if (chapter.MoneyReward > 0)
        {
            var netIncome = _gameStateService.State.NetIncomePerSecond;
            var scaledReward = Math.Max(chapter.MoneyReward, netIncome * 600);
            rewards.Add($"+{MoneyFormatter.FormatCompact(scaledReward)}");
        }
        if (chapter.GoldenScrewReward > 0)
        {
            var screwsLabel = _localizationService.GetString("GoldenScrews") ?? "Goldschrauben";
            rewards.Add($"+{chapter.GoldenScrewReward} {screwsLabel}");
        }
        if (chapter.XpReward > 0)
            rewards.Add($"+{chapter.XpReward} XP");
        StoryRewardText = string.Join("  |  ", rewards);

        IsStoryDialogVisible = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KONTEXTUELLER HINT COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void DismissHint()
    {
        var currentHintId = _contextualHintService.ActiveHint?.Id;
        _contextualHintService.DismissHint();

        // Nach Welcome-Dialog → FirstWorkshop-Hint zeigen (Kette)
        if (currentHintId == ContextualHints.Welcome.Id)
        {
            _contextualHintService.TryShowHint(ContextualHints.FirstWorkshop);
        }

        CheckDeferredDialogs();
    }

    /// <summary>
    /// Reagiert auf HintChanged-Event vom ContextualHintService.
    /// Aktualisiert die UI-Properties für die Tooltip-Bubble / den Dialog.
    /// </summary>
    private void OnHintChanged(object? sender, ContextualHint? hint)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (hint == null)
            {
                // Hint dismissed
                IsHintVisible = false;
                IsHintDialog = false;
                IsHintTooltipAbove = false;
                IsHintTooltipBelow = false;
                return;
            }

            // Texte aus RESX laden
            ActiveHintTitle = _localizationService.GetString(hint.TitleKey) ?? hint.TitleKey;
            ActiveHintText = _localizationService.GetString(hint.TextKey) ?? hint.TextKey;
            HintDismissButtonText = _localizationService.GetString("HintDismissButton") ?? "Verstanden";

            // Positionierung: Dialog (zentriert) oder Tooltip (oben/unten)
            IsHintDialog = hint.IsDialog;
            IsHintTooltipAbove = !hint.IsDialog && hint.Position == HintPosition.Below;
            IsHintTooltipBelow = !hint.IsDialog && hint.Position == HintPosition.Above;

            IsHintVisible = true;
        });
    }

}
