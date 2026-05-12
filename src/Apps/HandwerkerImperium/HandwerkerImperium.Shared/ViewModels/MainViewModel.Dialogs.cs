using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
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
    /// Public-Variante (INavigationHost) macht die Methode aus Services aufrufbar
    /// (z.B. NavigationService nach QuickJob-Completion in v2.0.36).
    /// </summary>
    void INavigationHost.CheckForNewStoryChapter()
        => DialogVM.CheckForNewStoryChapter(IsAnyDialogVisible, IsHoldingUpgrade);

    private void CheckForNewStoryChapter()
        => DialogVM.CheckForNewStoryChapter(IsAnyDialogVisible, IsHoldingUpgrade);

    /// <summary>
    /// Tap-Handler fuer Notification-Center-Items (v2.0.36).
    /// Wird vom <see cref="NotificationCenterViewModel.ItemActivated"/>-Event ausgeloest.
    /// Routet je nach <see cref="NotificationKind"/> in die passende Aktion.
    /// </summary>
    private void OnNotificationItemActivated(NotificationItem item)
    {
        switch (item.Kind)
        {
            case NotificationKind.DailyReward:
                if (_dailyRewardService.IsRewardAvailable)
                {
                    HasDailyReward = true;
                    CheckDailyReward();
                }
                _notificationCenterService.Dismiss(item.Id);
                NotificationCenterVM.ClosePopupCommand.Execute(null);
                break;

            case NotificationKind.WelcomeBackOffer:
                // Re-Spawn des Welcome-Offer-Dialogs, falls Offer noch aktiv (nicht expired).
                MissionsVM.OnWelcomeOfferGenerated();
                _notificationCenterService.Dismiss(item.Id);
                NotificationCenterVM.ClosePopupCommand.Execute(null);
                break;

            case NotificationKind.NewStoryChapter:
                CheckForNewStoryChapter();
                _notificationCenterService.Dismiss(item.Id);
                NotificationCenterVM.ClosePopupCommand.Execute(null);
                break;

            case NotificationKind.LiveOrderAvailable:
                SelectDashboardTab();
                _notificationCenterService.Dismiss(item.Id);
                NotificationCenterVM.ClosePopupCommand.Execute(null);
                break;

            case NotificationKind.AchievementUnlocked:
                SelectAchievementsTab();
                _notificationCenterService.Dismiss(item.Id);
                NotificationCenterVM.ClosePopupCommand.Execute(null);
                break;

            case NotificationKind.StreakSaved:
                // Reine Info — Tap dismissed nur.
                _notificationCenterService.Dismiss(item.Id);
                break;

            case NotificationKind.OfflineEarnings:
                // Sollte gar nicht in Bell landen — Fallback: Dismiss.
                _notificationCenterService.Dismiss(item.Id);
                break;
        }
    }

    /// <summary>
    /// Zeigt den Prestige-Bestätigungsdialog und führt bei Bestätigung Prestige durch.
    /// Wird sowohl vom Dashboard-Banner als auch vom Statistik-Tab aufgerufen.
    /// v2.1.0: Statt Modal-Bottom-Sheet wird jetzt die PrestigeView als ActivePage
    /// geoeffnet — Spieler sieht Tier-Auswahl + Vorschau + grossen Confirm-CTA in voller
    /// Bildschirm-Hoehe. ConfirmDialogAcceptCommand/CancelCommand setzen das TCS.
    /// </summary>
    private async Task ShowPrestigeConfirmationAsync()
    {
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // v2.1.0: Page-Modus — Properties vorbereiten + ActivePage wechseln.
        var prepareTask = DialogVM.PrestigeConfirmation.PreparePageAsync();
        ActivePage = ActivePage.Prestige;

        var (confirmed, selectedTier) = await prepareTask;

        // Page schliessen (Spieler hat akzeptiert/abgelehnt — egal wohin als naechstes).
        if (ActivePage == ActivePage.Prestige)
            ActivePage = ActivePage.Buildings;

        if (!confirmed || selectedTier == PrestigeTier.None) return;

        var state = _gameStateService.State;
        var tierName = _localizationService.GetString(selectedTier.GetLocalizationKey()) ?? selectedTier.ToString();
        int tierPoints = DialogVM.PrestigeConfirmation.CalculateEffectivePoints(state, selectedTier);

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
                    _localizationService.GetString("PrestigeTierUp") ?? "New rank: {0}!",
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
