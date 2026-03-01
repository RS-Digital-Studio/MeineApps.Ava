using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Alert/Confirm-Dialoge, Story-Dialog, Tutorial-Overlay, Prestige-Bestätigung
public partial class MainViewModel
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
    private void ConfirmDialogAccept()
    {
        IsConfirmDialogVisible = false;
        _confirmDialogTcs?.TrySetResult(true);
    }

    [RelayCommand]
    private void ConfirmDialogCancel()
    {
        IsConfirmDialogVisible = false;
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

    private Task<bool> ShowConfirmDialog(string title, string message, string acceptText, string cancelText)
    {
        ConfirmDialogTitle = title;
        ConfirmDialogMessage = message;
        ConfirmDialogAcceptText = acceptText;
        ConfirmDialogCancelText = cancelText;
        _confirmDialogTcs = new TaskCompletionSource<bool>();
        IsConfirmDialogVisible = true;

        // Ad-Banner ausblenden damit es nicht den Dialog verdeckt
        _adService.HideBanner();

        return _confirmDialogTcs.Task;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE-BESTÄTIGUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeigt den Prestige-Bestätigungsdialog und führt bei Bestätigung Prestige durch.
    /// Wird sowohl vom Dashboard-Banner als auch vom Statistik-Tab aufgerufen.
    /// </summary>
    private async Task ShowPrestigeConfirmationAsync()
    {
        var state = _gameStateService.State;
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);

        if (highestTier == PrestigeTier.None)
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

        // Prestige-Info zusammenstellen
        var tierName = _localizationService.GetString(highestTier.GetLocalizationKey()) ?? highestTier.ToString();
        var potentialPoints = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
        int tierPoints = (int)(potentialPoints * highestTier.GetPointMultiplier());

        var keepInfo = "";
        if (highestTier.KeepsResearch())
            keepInfo += $"\n\u2713 {_localizationService.GetString("Research") ?? "Forschung"}";
        if (highestTier.KeepsMasterTools())
            keepInfo += $"\n\u2713 {_localizationService.GetString("MasterTools") ?? "Meisterwerkzeuge"}";
        if (highestTier.KeepsBuildings())
            keepInfo += $"\n\u2713 {_localizationService.GetString("Buildings") ?? "Gebäude"}";
        if (highestTier.KeepsManagers())
            keepInfo += $"\n\u2713 {_localizationService.GetString("Managers") ?? "Vorarbeiter"}";

        var message = $"{highestTier.GetIcon()} {tierName}\n"
                    + $"+{tierPoints} PP | +{highestTier.GetPermanentMultiplierBonus():P0} {_localizationService.GetString("IncomeBonus") ?? "Einkommen"}\n\n"
                    + (_localizationService.GetString("PrestigeWarning") ?? "Dein Fortschritt wird zurückgesetzt!")
                    + (keepInfo.Length > 0 ? $"\n\n{_localizationService.GetString("PrestigeKeeps") ?? "Wird behalten:"}{keepInfo}" : "");

        var confirmed = await ShowConfirmDialog(
            _localizationService.GetString("Prestige") ?? "Prestige",
            message,
            _localizationService.GetString("PrestigeConfirm") ?? "Prestige durchführen",
            _localizationService.GetString("Cancel") ?? "Abbrechen");

        if (!confirmed) return;

        var success = await _prestigeService.DoPrestige(highestTier);
        if (success)
        {
            // Prestige-Effekt-Cache invalidieren (Shop-Items zurückgesetzt)
            _gameLoopService.InvalidatePrestigeEffects();

            await _audioService.PlaySoundAsync(GameSound.LevelUp);

            // UI komplett neu laden
            SelectDashboardTab();
            OnStateLoaded(this, EventArgs.Empty);

            // Celebration
            FloatingTextRequested?.Invoke($"{highestTier.GetIcon()} {tierName}!", "level");

            // Ziel-Cache invalidieren (Prestige ändert den gesamten Spielzustand)
            _goalService.Invalidate();
        }
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
    // TUTORIAL COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void TutorialNext()
    {
        _tutorialService?.NextStep();
    }

    [RelayCommand]
    private void TutorialSkip()
    {
        _tutorialService?.SkipTutorial();
    }

    private void OnTutorialStep(object? sender, TutorialStep step)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TutorialTitle = _localizationService.GetString(step.TitleKey) ?? step.TitleKey;
            TutorialDescription = _localizationService.GetString(step.DescriptionKey) ?? step.DescriptionKey;
            TutorialIcon = step.Icon;
            TutorialStepDisplay = $"{(_tutorialService?.CurrentStepIndex ?? 0) + 1}/{_tutorialService?.TotalSteps ?? 0}";
            IsTutorialVisible = true;
        });
    }

    private void OnTutorialDone(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsTutorialVisible = false;
        });
    }
}
