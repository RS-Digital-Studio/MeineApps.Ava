using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Initialisierung, Offline-Earnings, Daily Reward, Cloud-Save
public sealed partial class MainViewModel
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
        // ShaderPreloader.PreloadAll() wird bereits in HandwerkerImperiumLoadingPipeline (Schritt 1) aufgerufen

        // Spielstand laden
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

        // ═══════════════════════════════════════════════════════════════════
        // DIALOG-KASKADEN-BEGRENZUNG: Max. 2 Dialoge beim Start
        // Reihenfolge: Offline/Welcome → Daily Reward → (Rest verzögert)
        // Spieler sollen nicht von 5+ Dialogen erschlagen werden.
        // ═══════════════════════════════════════════════════════════════════

        int dialogsShown = 0;

        // Offline-Earnings berechnen (noch nicht anzeigen)
        CheckOfflineProgress();

        // Welcome-Back-Offer prüfen und ggf. mit Offline-Earnings kombinieren
        _welcomeBackService.CheckAndGenerateOffer();
        CheckCombinedWelcomeDialog();

        // Zählen ob ein Dialog gezeigt wurde
        if (IsOfflineEarningsDialogVisible || IsCombinedWelcomeDialogVisible || IsWelcomeOfferVisible)
            dialogsShown++;

        // Daily Reward nur wenn noch Platz für Dialog
        if (dialogsShown < 2)
        {
            CheckDailyReward();
            if (IsDailyRewardDialogVisible)
                dialogsShown++;
        }
        else
        {
            // Daily Reward verzögert anzeigen (nach erstem Dialog geschlossen)
            _hasDeferredDailyReward = _dailyRewardService.IsRewardAvailable;
        }

        // Story + Welcome-Hint NUR verzögert (nie beim Start direkt)
        // Werden nach Schließen des letzten Startup-Dialogs geprüft
        _hasDeferredStory = true;
        _hasDeferredWelcomeHint = !_contextualHintService.HasSeenHint(ContextualHints.Welcome.Id);

        // Starter-Offer prüfen (ab Level 10, einmalig, Nicht-Premium)
        CheckStarterOffer();

        // Start the game loop for idle earnings
        _gameLoopService.Start();
        }
        catch
        {
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
        CheckDeferredDialogs();
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
        CheckDeferredDialogs();
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
        CheckDeferredDialogs();
    }

    [RelayCommand]
    private async Task CollectOfflineEarningsWithAdAsync()
    {
        var success = await _rewardedAdService.ShowAdAsync("offline_double");
        CollectOfflineEarnings(success);
        IsOfflineEarningsDialogVisible = false;
        CheckDeferredDialogs();
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
                          && state.GoldenScrews >= 3;  // BAL-7: Von 5 auf 3 reduziert
        if (CanRescueStreak)
        {
            var costText = _localizationService.GetString("StreakRescueCost") ?? "Rescue streak ({0})";
            StreakRescueText = string.Format(costText, 3);
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

    // Streak-Meilensteine die eine besondere Celebration auslösen
    private static readonly int[] s_streakMilestones = [30, 60, 100];

    [RelayCommand]
    public void ClaimDailyReward()
    {
        var reward = _dailyRewardService.ClaimReward();
        if (reward != null)
        {
            _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();

            // Streak-Meilensteine feiern (30, 60, 100 Tage)
            var currentStreak = _dailyRewardService.CurrentStreak;
            for (int i = 0; i < s_streakMilestones.Length; i++)
            {
                if (currentStreak == s_streakMilestones[i])
                {
                    var streakText = string.Format(
                        _localizationService.GetString("StreakMilestone") ?? "{0} Tage Streak!",
                        currentStreak);
                    CelebrationRequested?.Invoke();
                    CeremonyRequested?.Invoke(CeremonyType.Achievement, streakText,
                        $"{currentStreak} {_localizationService.GetString("Days") ?? "Tage"}");
                    _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();
                    break;
                }
            }

            HasDailyReward = false;
            IsDailyRewardDialogVisible = false;
            CheckDeferredDialogs();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VERZÖGERTE DIALOGE (Dialog-Kaskaden-Begrenzung)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob verzögerte Dialoge angezeigt werden sollen.
    /// Wird aufgerufen wenn ein Startup-Dialog geschlossen wird.
    /// Zeigt maximal einen weiteren Dialog pro Aufruf.
    /// </summary>
    private void CheckDeferredDialogs()
    {
        // Nicht prüfen wenn noch ein Dialog offen ist
        if (IsAnyDialogVisible)
            return;

        // 1. Verzögerte Daily Reward
        if (_hasDeferredDailyReward)
        {
            _hasDeferredDailyReward = false;
            CheckDailyReward();
            if (IsDailyRewardDialogVisible) return;
        }

        // 2. Verzögerte Story
        if (_hasDeferredStory)
        {
            _hasDeferredStory = false;
            CheckForNewStoryChapter();
            if (DialogVM.IsStoryDialogVisible) return;
        }

        // 3. Verzögerter Welcome-Hint (nur beim allerersten Start)
        if (_hasDeferredWelcomeHint)
        {
            _hasDeferredWelcomeHint = false;
            _contextualHintService.TryShowHint(ContextualHints.Welcome);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STARTER OFFER (einmaliges zeitlich begrenztes Premium-Angebot)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob ein Starter-Offer angezeigt werden soll.
    /// Bedingungen: Level >= 10, noch nicht angezeigt, kein Premium.
    /// Bei erstem Auslösen: 24h-Countdown starten. Bei Wiedereinstieg: Countdown prüfen.
    /// </summary>
    private void CheckStarterOffer()
    {
        var state = _gameStateService.State;

        // Bereits Premium oder schon angezeigt -> überspringen
        if (state.IsPremium || state.StarterOfferShown) return;

        // Level-Gate: Mindestens Level 15 (hat dann Plumber + Storage + Auto-Collect, versteht Economy)
        if (state.PlayerLevel < 15) return;

        // Timestamp setzen falls noch nicht gesetzt (erste Auslösung)
        if (state.StarterOfferTimestamp == null)
        {
            state.StarterOfferTimestamp = DateTime.UtcNow;
            _gameStateService.MarkDirty();
        }

        // 24h abgelaufen -> Angebot verpasst, als "gezeigt" markieren
        var elapsed = DateTime.UtcNow - state.StarterOfferTimestamp.Value;
        if (elapsed.TotalHours >= 24)
        {
            state.StarterOfferShown = true;
            _gameStateService.MarkDirty();
            return;
        }

        // Countdown berechnen und Dialog anzeigen
        var remaining = TimeSpan.FromHours(24) - elapsed;
        StarterOfferCountdown = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
            : $"{remaining.Minutes}m {remaining.Seconds:D2}s";

        IsStarterOfferVisible = true;
    }

    /// <summary>
    /// Spieler nimmt das Starter-Offer an -> zum Premium-Kauf navigieren.
    /// </summary>
    [RelayCommand]
    private void AcceptStarterOffer()
    {
        var state = _gameStateService.State;
        state.StarterOfferShown = true;
        _gameStateService.MarkDirty();
        IsStarterOfferVisible = false;

        // Zum Shop navigieren damit der Spieler den Premium-Kauf durchführen kann
        NavigateToShop();
    }

    /// <summary>
    /// Spieler lehnt das Starter-Offer ab -> Dialog schließen.
    /// Angebot bleibt aber aktiv bis 24h abgelaufen.
    /// </summary>
    [RelayCommand]
    private void DismissStarterOffer()
    {
        IsStarterOfferVisible = false;
        CheckDeferredDialogs();
    }
}
