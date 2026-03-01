using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Initialisierung, Offline-Earnings, Daily Reward, Cloud-Save
public partial class MainViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Task-Referenz für InitializeAsync, damit Fire-and-Forget Race Conditions vermieden werden.
    /// </summary>
    public Task? InitTask { get; set; }

    public async Task InitializeAsync()
    {
        try
        {
        // GPU-Shader vorab kompilieren während Loading-Screen sichtbar ist
        // (12 SkSL-Shader, spart 600ms-2.4s Jank beim ersten Render auf Android)
        MeineApps.UI.SkiaSharp.Shaders.ShaderPreloader.PreloadAll();

        // Load saved game first
        if (!_gameStateService.IsInitialized)
        {
            await _saveGameService.LoadAsync();

            // If LoadAsync didn't initialize (no save file), create new state
            if (!_gameStateService.IsInitialized)
            {
                _gameStateService.Initialize();
            }
        }

        // Cloud-Save prüfen (wenn Play Games angemeldet)
        await CheckCloudSaveAsync();

        // Sprache synchronisieren: gespeicherte Sprache laden oder Gerätesprache übernehmen
        var savedLang = _gameStateService.State.Language;
        if (!string.IsNullOrEmpty(savedLang))
        {
            _localizationService.SetLanguage(savedLang);
        }
        else
        {
            // Neues Spiel: Gerätesprache in GameState übernehmen
            _gameStateService.State.Language = _localizationService.CurrentLanguage;
        }

        // Reload settings in SettingsVM now that game state is loaded
        SettingsViewModel.ReloadSettings();

        // Recover stuck active order from previous session
        // (mini-game state is not saved, so it cannot be resumed)
        if (_gameStateService.State.ActiveOrder != null)
        {
            _gameStateService.CancelActiveOrder();
        }

        RefreshFromState();

        // Generate orders if none or too few exist
        if (_gameStateService.State.AvailableOrders.Count < 3)
        {
            _orderGeneratorService.RefreshOrders();
            RefreshOrders();
        }

        // Quick Jobs initialisieren
        if (_gameStateService.State.QuickJobs.Count == 0)
            _quickJobService.GenerateJobs();
        RefreshQuickJobs();

        // Daily Challenges initialisieren
        _dailyChallengeService.CheckAndResetIfNewDay();
        RefreshChallenges();

        // Weekly Missions initialisieren
        _weeklyMissionService.CheckAndResetIfNewWeek();
        RefreshWeeklyMissions();

        // Lucky Spin Status
        HasFreeSpin = _luckySpinService.HasFreeSpin;

        IsLoading = false;

        // Offline-Earnings berechnen (noch nicht anzeigen)
        CheckOfflineProgress();

        // Check for daily reward
        CheckDailyReward();

        // Welcome-Back-Offer prüfen und ggf. mit Offline-Earnings kombinieren
        _welcomeBackService.CheckAndGenerateOffer();
        CheckCombinedWelcomeDialog();

        // Story-Kapitel prüfen (z.B. pending aus letzter Session oder Sofort-Freischaltung)
        CheckForNewStoryChapter();

        // Tutorial starten wenn noch nicht abgeschlossen
        if (_tutorialService != null && !_tutorialService.IsCompleted)
        {
            _tutorialService.StartTutorial();
        }

        // Start the game loop for idle earnings
        _gameLoopService.Start();
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Fehler in Initialize: {ex}");
#endif
            IsLoading = false;
        }
    }

    /// <summary>
    /// Vergleicht Cloud-Spielstand mit lokalem und fragt Benutzer bei neuerem Cloud-Save.
    /// </summary>
    private async Task CheckCloudSaveAsync()
    {
        if (_playGamesService?.IsSignedIn != true || !_gameStateService.State.CloudSaveEnabled)
            return;

        try
        {
            var cloudJson = await _playGamesService.LoadCloudSaveAsync();
            if (string.IsNullOrEmpty(cloudJson)) return;

            var cloudState = System.Text.Json.JsonSerializer.Deserialize<GameState>(cloudJson);
            if (cloudState == null) return;

            // Cloud neuer als lokal?
            if (cloudState.LastSavedAt > _gameStateService.State.LastSavedAt)
            {
                var title = _localizationService.GetString("CloudSaveNewer") ?? "Cloud Save Found";
                var message = string.Format(
                    "{0} (Level {1})",
                    title, cloudState.PlayerLevel);
                var useCloud = _localizationService.GetString("UseCloudSave") ?? "Use Cloud";
                var useLocal = _localizationService.GetString("UseLocalSave") ?? "Use Local";

                var confirmed = await ShowConfirmDialog(
                    title, message, useCloud, useLocal);

                if (confirmed)
                {
                    await _saveGameService.ImportSaveAsync(cloudJson);
                    RefreshFromState();
                }
            }
        }
        catch
        {
            // Cloud-Sync-Fehler still ignorieren (lokaler Save funktioniert)
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // OFFLINE-EARNINGS
    // ═══════════════════════════════════════════════════════════════════════

    private void CheckOfflineProgress()
    {
        var offlineDuration = _offlineProgressService.GetOfflineDuration();
        if (offlineDuration.TotalMinutes < 1)
            return;

        var earnings = _offlineProgressService.CalculateOfflineProgress();
        if (earnings <= 0)
            return;

        _pendingOfflineEarnings = earnings;
        var maxDuration = _offlineProgressService.GetMaxOfflineDuration();
        bool wasCapped = offlineDuration > maxDuration;
        var effectiveDuration = wasCapped ? maxDuration : offlineDuration;

        OfflineEarningsAmountText = MoneyFormatter.FormatCompact(earnings);
        var durationText = effectiveDuration.TotalHours >= 1
            ? $"{(int)effectiveDuration.TotalHours}h {effectiveDuration.Minutes}min"
            : $"{(int)effectiveDuration.TotalMinutes}min";
        // Hinweis wenn Offline-Dauer gekappt wurde
        if (wasCapped)
            durationText += $" (Max. {(int)maxDuration.TotalHours}h)";
        OfflineEarningsDurationText = durationText;

        // Neuer Rekord pruefen
        IsOfflineNewRecord = earnings > _gameStateService.State.MaxOfflineEarnings;
        if (IsOfflineNewRecord)
            _gameStateService.State.MaxOfflineEarnings = earnings;

        // Dialog wird NICHT sofort angezeigt - CheckCombinedWelcomeDialog() entscheidet
        // ob ein einzelner Offline-Dialog oder ein kombinierter Dialog gezeigt wird

        ShowOfflineEarnings?.Invoke(this, new OfflineEarningsEventArgs(
            earnings, effectiveDuration, wasCapped));
    }

    /// <summary>
    /// Prüft ob Offline-Earnings UND Welcome-Back-Offer gleichzeitig vorliegen.
    /// Wenn ja: Zeigt einen kombinierten Dialog statt zwei separate.
    /// </summary>
    private void CheckCombinedWelcomeDialog()
    {
        var hasOffline = _pendingOfflineEarnings > 0;
        var offer = _gameStateService.State.ActiveWelcomeBackOffer;
        var hasWelcome = offer != null && !offer.IsExpired;

        if (hasOffline && hasWelcome)
        {
            // Kombinierter Dialog: Offline-Earnings + Welcome-Back in einem
            CombinedOfflineEarnings = OfflineEarningsAmountText;
            CombinedOfflineDuration = OfflineEarningsDurationText;
            CombinedOfferMoney = MoneyFormatter.FormatCompact(offer!.MoneyReward);
            CombinedOfferScrews = offer.GoldenScrewReward > 0 ? $"+{offer.GoldenScrewReward}" : "";

            var remaining = offer.TimeRemaining;
            CombinedOfferTimer = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
                : $"{remaining.Minutes}m";

            IsCombinedWelcomeDialogVisible = true;
        }
        else if (hasOffline)
        {
            // Nur Offline-Dialog
            IsOfflineEarningsDialogVisible = true;
        }
        else if (hasWelcome)
        {
            // Nur Welcome-Back-Dialog (wird durch OnWelcomeOfferGenerated angezeigt)
            OnWelcomeOfferGenerated();
        }
    }

    /// <summary>
    /// Sammelt alle Belohnungen aus dem kombinierten Welcome-Dialog ein (Offline + Welcome-Back).
    /// </summary>
    [RelayCommand]
    private void CollectCombinedRewards()
    {
        // Offline-Earnings einsammeln
        if (_pendingOfflineEarnings > 0)
        {
            _gameStateService.AddMoney(_pendingOfflineEarnings);
            FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}", "money");
            _pendingOfflineEarnings = 0;
        }

        // Welcome-Back-Offer einlösen
        _welcomeBackService.ClaimOffer();

        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        CelebrationRequested?.Invoke();

        IsCombinedWelcomeDialogVisible = false;
    }

    /// <summary>
    /// Schließt den kombinierten Dialog und sammelt nur die Offline-Earnings ein.
    /// </summary>
    [RelayCommand]
    private void DismissCombinedDialog()
    {
        // Offline-Earnings trotzdem einsammeln (die hat der Spieler verdient)
        if (_pendingOfflineEarnings > 0)
        {
            _gameStateService.AddMoney(_pendingOfflineEarnings);
            FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}", "money");
            _pendingOfflineEarnings = 0;
        }

        // Welcome-Back-Offer ablehnen
        _welcomeBackService.DismissOffer();

        IsCombinedWelcomeDialogVisible = false;
    }

    public void CollectOfflineEarnings(bool withAdBonus)
    {
        var amount = _pendingOfflineEarnings;
        if (withAdBonus)
            amount *= 2;

        _gameStateService.AddMoney(amount);
        _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();

        // Muenz-Partikel Burst im Dashboard ausloesen
        if (amount > 0)
            FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(amount)}", "money");

        _pendingOfflineEarnings = 0;
    }

    [RelayCommand]
    private void CollectOfflineEarningsNormal()
    {
        CollectOfflineEarnings(false);
        IsOfflineEarningsDialogVisible = false;
    }

    [RelayCommand]
    private async Task CollectOfflineEarningsWithAdAsync()
    {
        var success = await _rewardedAdService.ShowAdAsync("offline_double");
        CollectOfflineEarnings(success);
        IsOfflineEarningsDialogVisible = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY REWARD
    // ═══════════════════════════════════════════════════════════════════════

    private void CheckDailyReward()
    {
        HasDailyReward = _dailyRewardService.IsRewardAvailable;

        // Streak-Rettung prüfen: War der Streak unterbrochen und kann gerettet werden?
        var state = _gameStateService.State;
        CanRescueStreak = state.StreakBeforeBreak > 1
                          && state.DailyRewardStreak <= 1
                          && !state.StreakRescueUsed
                          && state.GoldenScrews >= 5;
        if (CanRescueStreak)
        {
            var costText = _localizationService.GetString("StreakRescueCost") ?? "Rescue streak ({0})";
            StreakRescueText = string.Format(costText, 5);
        }

        if (HasDailyReward)
        {
            var rewards = _dailyRewardService.GetRewardCycle();
            var currentDay = _dailyRewardService.CurrentDay;
            var currentStreak = _dailyRewardService.CurrentStreak;

            var todaysReward = _dailyRewardService.TodaysReward;
            DailyRewardDayText = string.Format(_localizationService.GetString("DayReward"), currentDay);
            DailyRewardStreakText = string.Format(_localizationService.GetString("DailyStreak"), currentStreak);
            DailyRewardAmountText = todaysReward != null
                ? MoneyFormatter.FormatCompact(todaysReward.Money)
                : "";
            IsDailyRewardDialogVisible = true;

            ShowDailyReward?.Invoke(this, new DailyRewardEventArgs(rewards, currentDay, currentStreak));
        }
    }

    [RelayCommand]
    public void ClaimDailyReward()
    {
        var reward = _dailyRewardService.ClaimReward();
        if (reward != null)
        {
            _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
            HasDailyReward = false;
            IsDailyRewardDialogVisible = false;
        }
    }
}
