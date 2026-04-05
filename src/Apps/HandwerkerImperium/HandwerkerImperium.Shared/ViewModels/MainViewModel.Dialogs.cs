using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Weiterleitungsmethoden an DialogVM (nach Extraktion der Dialog-Logik in DialogViewModel)
public sealed partial class MainViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // WEITERLEITUNGSMETHODEN (für Aufrufe aus anderen Partial Classes)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Weiterleitung an DialogVM.ShowAlertDialog(). Kurzform für Aufrufe aus Economy/Missions/Init.
    /// </summary>
    private void ShowAlertDialog(string title, string message, string buttonText)
        => DialogVM.ShowAlertDialog(title, message, buttonText);

    /// <summary>
    /// Weiterleitung an DialogVM.ShowConfirmDialog(). Kurzform für Aufrufe aus Economy/Init.
    /// </summary>
    private Task<bool> ShowConfirmDialog(string title, string message, string acceptText, string cancelText)
        => DialogVM.ShowConfirmDialog(title, message, acceptText, cancelText);

    /// <summary>
    /// Weiterleitung an DialogVM.ShowLockedTabHint(). Aufgerufen von GameTabBarRenderer via MainViewModel.
    /// </summary>
    public void ShowLockedTabHint(int requiredLevel)
        => DialogVM.ShowLockedTabHint(requiredLevel);

    /// <summary>
    /// Prüft ob ein neues Story-Kapitel freigeschaltet wurde.
    /// Weiterleitung an DialogVM mit aktuellem Dialog- und Upgrade-Status.
    /// </summary>
    private void CheckForNewStoryChapter()
        => DialogVM.CheckForNewStoryChapter(IsAnyDialogVisible, IsHoldingUpgrade);

    /// <summary>
    /// Zeigt den Prestige-Bestätigungsdialog und führt bei Bestätigung Prestige durch.
    /// Wird sowohl vom Dashboard-Banner als auch vom Statistik-Tab aufgerufen.
    /// </summary>
    private async Task ShowPrestigeConfirmationAsync()
    {
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        var (confirmed, selectedTier) = await DialogVM.ShowPrestigeConfirmationDialogAsync();

        if (!confirmed || selectedTier == PrestigeTier.None) return;

        var state = _gameStateService.State;
        var tierName = _localizationService.GetString(selectedTier.GetLocalizationKey()) ?? selectedTier.ToString();
        int tierPoints = DialogVM.CalculateEffectivePrestigePoints(state, selectedTier);

        // Alten höchsten Tier merken für Tier-Aufstieg-Celebration
        var oldHighestTier = state.Prestige.CurrentTier;

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

            // Tier-Aufstieg-Celebration: Wenn ein neuer höchster Tier erreicht wurde
            var newHighestTier = _gameStateService.Prestige.CurrentTier;
            if (newHighestTier > oldHighestTier)
            {
                var newTierName = _localizationService.GetString(newHighestTier.GetLocalizationKey())
                                  ?? newHighestTier.ToString();
                var prestigeTierUpText = string.Format(
                    _localizationService.GetString("PrestigeTierUp") ?? "Neuer Rang: {0}!",
                    newTierName);
                CeremonyRequested?.Invoke(CeremonyType.Achievement, prestigeTierUpText, newHighestTier.GetIcon());
                _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();
            }

            // Ziel-Cache invalidieren (Prestige ändert den gesamten Spielzustand)
            _goalService.Invalidate();

            // Post-Prestige Zusammenfassung anzeigen
            DialogVM.ShowPrestigeSummary(selectedTier, tierPoints);
        }
    }
}
