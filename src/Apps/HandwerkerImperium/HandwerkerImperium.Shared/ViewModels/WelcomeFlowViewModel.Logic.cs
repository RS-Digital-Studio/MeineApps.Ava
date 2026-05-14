using System;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Welcome-Flow-Logik: Offline-Earnings, Daily-Reward, Starter-Offer, kombinierter
/// Welcome-Dialog und die verzögerte Dialog-Kaskade. Aus MainViewModel.Init.cs extrahiert —
/// MainViewModel haelt nur noch eine schmale <see cref="IWelcomeFlowHost"/>-Bruecke.
/// </summary>
public sealed partial class WelcomeFlowViewModel
{
    private readonly IOfflineProgressService _offlineProgressService;
    private readonly IWelcomeBackService _welcomeBackService;
    private readonly IDailyRewardService _dailyRewardService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private readonly IAudioService _audioService;
    private readonly IGoalService _goalService;
    private readonly IContextualHintService _contextualHintService;
    private readonly INotificationCenterService _notificationCenterService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IUiEffectBus _uiEffectBus;
    private readonly MissionsFeatureViewModel _missionsVm;
    private readonly DialogViewModel _dialogVm;
    private IWelcomeFlowHost? _host;

    /// <summary>Berechnete, noch nicht eingesammelte Offline-Einnahmen.</summary>
    private decimal _pendingOfflineEarnings;

    // Verzögerte Dialoge (Dialog-Kaskaden-Begrenzung beim Start).
    private bool _hasDeferredDailyReward;
    private bool _hasDeferredStory;
    private bool _hasDeferredWelcomeHint;

    // Streak-Meilensteine die eine besondere Celebration auslösen.
    private static readonly int[] s_streakMilestones = [30, 60, 100];

    public WelcomeFlowViewModel(
        IOfflineProgressService offlineProgressService,
        IWelcomeBackService welcomeBackService,
        IDailyRewardService dailyRewardService,
        IGameStateService gameStateService,
        ILocalizationService localizationService,
        IAudioService audioService,
        IGoalService goalService,
        IContextualHintService contextualHintService,
        INotificationCenterService notificationCenterService,
        IRewardedAdService rewardedAdService,
        IUiEffectBus uiEffectBus,
        MissionsFeatureViewModel missionsVm,
        DialogViewModel dialogVm)
    {
        _offlineProgressService = offlineProgressService;
        _welcomeBackService = welcomeBackService;
        _dailyRewardService = dailyRewardService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;
        _audioService = audioService;
        _goalService = goalService;
        _contextualHintService = contextualHintService;
        _notificationCenterService = notificationCenterService;
        _rewardedAdService = rewardedAdService;
        _uiEffectBus = uiEffectBus;
        _missionsVm = missionsVm;
        _dialogVm = dialogVm;
    }

    /// <summary>Verbindet die schmale Host-Bruecke (MainViewModel) — einmalig im MainViewModel-Ctor.</summary>
    public void AttachHost(IWelcomeFlowHost host) => _host = host;

    /// <summary>
    /// True wenn irgendein Welcome-Flow-Overlay oder ein DialogVM-Dialog sichtbar ist.
    /// Kombiniert die eigenen Dialoge mit MissionsVM.IsWelcomeOfferVisible und DialogVM.IsAnyDialogVisible.
    /// </summary>
    public bool IsAnyDialogVisible =>
        IsOfflineEarningsDialogVisible || IsCombinedWelcomeDialogVisible ||
        _missionsVm.IsWelcomeOfferVisible || IsDailyRewardDialogVisible ||
        IsStarterOfferVisible || _dialogVm.IsAnyDialogVisible;

    // ═══════════════════════════════════════════════════════════════════════
    // STARTUP-DIALOG-KASKADE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dialog-Kaskaden-Begrenzung beim Spielstart: Max. 2 Dialoge.
    /// Reihenfolge: Offline/Welcome → Daily Reward → (Rest verzögert).
    /// Spieler sollen nicht von 5+ Dialogen erschlagen werden.
    /// Wird vom GameStartupCoordinator nach dem State-Load aufgerufen.
    /// </summary>
    public void RunStartupDialogSequence()
    {
        int dialogsShown = 0;

        // Offline-Earnings berechnen (noch nicht anzeigen)
        CheckOfflineProgress();

        // Welcome-Back-Offer prüfen und ggf. mit Offline-Earnings kombinieren
        _welcomeBackService.CheckAndGenerateOffer();
        CheckCombinedWelcomeDialog();

        // Zählen ob ein Dialog gezeigt wurde
        if (IsOfflineEarningsDialogVisible || IsCombinedWelcomeDialogVisible || _missionsVm.IsWelcomeOfferVisible)
            dialogsShown++;

        // Daily Reward: Bei brandneuem Spiel still einsammeln (kein Dialog am allerersten Start,
        // der Spieler kennt das Spiel noch nicht und wird sonst von Dialogen erschlagen)
        var isFirstEverStart = _gameStateService.State.LastDailyRewardClaim == DateTime.MinValue
                            && _gameStateService.Statistics.TotalOrdersCompleted == 0;
        if (isFirstEverStart && _dailyRewardService.IsRewardAvailable)
        {
            _dailyRewardService.ClaimReward();
            HasDailyReward = false;
        }
        else if (dialogsShown < 1)
        {
            // v2.0.36: Daily Reward bekommt nur dann ein Modal, wenn noch KEIN anderer Dialog
            // geschlagen wurde. Zweiter Modal in der Kaskade landet stattdessen in der Bell.
            CheckDailyReward();
            if (IsDailyRewardDialogVisible)
                dialogsShown++;
        }
        else if (_dailyRewardService.IsRewardAvailable)
        {
            // v2.0.36: Statt verzoegertem Modal — direkt in Notification-Center pushen.
            // HasDailyReward bleibt true, sodass das Header-Badge sichtbar wird.
            HasDailyReward = true;
            _notificationCenterService.Add(new NotificationItem
            {
                Id = "daily_reward_today",
                Kind = NotificationKind.DailyReward,
                TitleKey = "NotificationDailyRewardTitle",
                BodyKey = "NotificationDailyRewardBody",
                CreatedAt = DateTime.UtcNow,
                IconKind = "Gift"
            });
            _hasDeferredDailyReward = false;
        }

        // Story + Welcome-Hint NUR verzögert (nie beim Start direkt)
        // Werden nach Schließen des letzten Startup-Dialogs geprüft
        _hasDeferredStory = true;
        _hasDeferredWelcomeHint = !_contextualHintService.HasSeenHint(ContextualHints.Welcome.Id);

        // Starter-Offer prüfen (ab Level 10, einmalig, Nicht-Premium)
        CheckStarterOffer();
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
        _missionsVm.PendingOfflineEarnings = earnings;
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

        // Effizienz-Hinweis: Durchschnittliche Offline-Rate basiert auf gestaffeltem System
        var avgOfflinePercent = effectiveDuration.TotalHours switch
        {
            <= 2 => 80,
            <= 4 => 55,
            <= 8 => 35,
            _ => 20
        };
        OfflineEfficiencyHint = string.Format(
            _localizationService.GetString("OfflineEfficiencyHint") ?? "~{0}% deiner Online-Einnahmen",
            avgOfflinePercent);

        // Neuer Rekord pruefen
        IsOfflineNewRecord = earnings > _gameStateService.State.MaxOfflineEarnings;
        if (IsOfflineNewRecord)
            _gameStateService.State.MaxOfflineEarnings = earnings;

        // Wiedereinsteiger-Tipp: Nächstes Ziel anzeigen wenn >48h offline
        if (offlineDuration.TotalHours > 48)
        {
            var goal = _goalService.GetCurrentGoal();
            OfflineGoalText = goal?.Description ?? "";
        }
        else
        {
            OfflineGoalText = "";
        }

        // Dialog wird NICHT sofort angezeigt - CheckCombinedWelcomeDialog() entscheidet
        // ob ein einzelner Offline-Dialog oder ein kombinierter Dialog gezeigt wird
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
            // Nur Welcome-Back-Dialog (wird durch MissionsVM.OnWelcomeOfferGenerated angezeigt)
            _missionsVm.OnWelcomeOfferGenerated();
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
            _uiEffectBus.RaiseFloatingText($"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}", "money");
            _pendingOfflineEarnings = 0;
        }

        // Welcome-Back-Offer einlösen
        _welcomeBackService.ClaimOffer();

        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        _uiEffectBus.RaiseCelebration();

        IsCombinedWelcomeDialogVisible = false;
        CheckDeferredDialogs();
    }

    /// <summary>
    /// Schließt den kombinierten Dialog und sammelt nur die Offline-Earnings ein.
    /// Public, weil die Back-Press-Kaskade (INavigationHost) sie aufruft.
    /// </summary>
    [RelayCommand]
    public void DismissCombinedDialog()
    {
        // Offline-Earnings trotzdem einsammeln (die hat der Spieler verdient)
        if (_pendingOfflineEarnings > 0)
        {
            _gameStateService.AddMoney(_pendingOfflineEarnings);
            _uiEffectBus.RaiseFloatingText($"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}", "money");
            _pendingOfflineEarnings = 0;
        }

        // Welcome-Back-Offer ablehnen
        _welcomeBackService.DismissOffer();

        IsCombinedWelcomeDialogVisible = false;
        CheckDeferredDialogs();
    }

    private void CollectOfflineEarnings(bool withAdBonus)
    {
        var amount = _pendingOfflineEarnings;
        if (withAdBonus)
            amount *= 2;

        _gameStateService.AddMoney(amount);
        _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();

        // Muenz-Partikel Burst im Dashboard ausloesen
        if (amount > 0)
            _uiEffectBus.RaiseFloatingText($"+{MoneyFormatter.FormatCompact(amount)}", "money");

        _pendingOfflineEarnings = 0;
    }

    /// <summary>
    /// Sammelt die Offline-Earnings ohne Ad-Bonus ein und schliesst den Dialog.
    /// Public, weil die Back-Press-Kaskade (INavigationHost) sie aufruft.
    /// </summary>
    [RelayCommand]
    public void CollectOfflineEarningsNormal()
    {
        CollectOfflineEarnings(false);
        IsOfflineEarningsDialogVisible = false;
        CheckDeferredDialogs();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task CollectOfflineEarningsWithAdAsync()
    {
        var success = await _rewardedAdService.ShowAdAsync("offline_double");
        CollectOfflineEarnings(success);
        IsOfflineEarningsDialogVisible = false;
        CheckDeferredDialogs();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY REWARD
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft die Daily-Reward-Verfügbarkeit und befüllt den Daily-Reward-Dialog.
    /// Public, weil das Notification-Center (MainViewModel) den Dialog re-triggern kann.
    /// </summary>
    public void CheckDailyReward()
    {
        HasDailyReward = _dailyRewardService.IsRewardAvailable;

        // Streak-Rettung prüfen: War der Streak unterbrochen und kann gerettet werden?
        var state = _gameStateService.State;
        _missionsVm.CanRescueStreak = state.StreakBeforeBreak > 1
                          && state.DailyRewardStreak <= 1
                          && !state.StreakRescueUsed
                          && state.GoldenScrews >= 3;  // BAL-7: Von 5 auf 3 reduziert
        if (_missionsVm.CanRescueStreak)
        {
            var costText = _localizationService.GetString("StreakRescueCost") ?? "{0} Golden Screws";
            _missionsVm.StreakRescueText = string.Format(costText, 3);
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
            // Streak-Dots datengebunden — siehe DailyRewardDialog.axaml.
            UpdateStreakDays(currentDay, currentStreak);
            IsDailyRewardDialogVisible = true;
        }
    }

    [RelayCommand]
    private void ClaimDailyReward()
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
                    _uiEffectBus.RaiseCelebration();
                    _uiEffectBus.RaiseCeremony(CeremonyType.Achievement, streakText,
                        $"{currentStreak} {_localizationService.GetString("Days") ?? "Days"}");
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
    public void CheckDeferredDialogs()
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
            _dialogVm.CheckForNewStoryChapter(IsAnyDialogVisible, _host?.IsHoldingUpgrade ?? false);
            if (_dialogVm.IsStoryDialogVisible) return;
        }

        // 3. Verzögerter Welcome-Hint (nur beim allerersten Start)
        if (_hasDeferredWelcomeHint)
        {
            _hasDeferredWelcomeHint = false;

            // Story Kapitel 1 ("Willkommen, Lehrling!") deckt die Begrüßung bereits ab
            // → Welcome-Hint überspringen, direkt FirstWorkshop-Hint zeigen
            if (_gameStateService.State.ViewedStoryIds.Contains("tutorial_welcome"))
            {
                _gameStateService.Tutorial.SeenHints.Add(ContextualHints.Welcome.Id);
                _contextualHintService.TryShowHint(ContextualHints.FirstWorkshop);
            }
            else
            {
                _contextualHintService.TryShowHint(ContextualHints.Welcome);
            }
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

        // Level-Gate: Mindestens Level 10 (UX-1 20.04.2026: von 15 auf 10 gesenkt, seit Plumber-Unlock
        // von Lv8 auf Lv5 vorverlegt wurde. Sweet-Spot: Spieler hat Plumber etabliert, versteht Economy,
        // kommt aber noch VOR dem Electrician-Schock bei Lv15 (250k EUR).
        if (state.PlayerLevel < 10) return;

        // Timestamp setzen falls noch nicht gesetzt (erste Auslösung)
        if (state.StarterOfferTimestamp == null)
        {
            state.StarterOfferTimestamp = DateTime.UtcNow;
        }

        // 24h abgelaufen -> Angebot verpasst, als "gezeigt" markieren
        var elapsed = DateTime.UtcNow - state.StarterOfferTimestamp.Value;
        if (elapsed.TotalHours >= 24)
        {
            state.StarterOfferShown = true;
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
        IsStarterOfferVisible = false;

        // Zum Shop navigieren damit der Spieler den Premium-Kauf durchführen kann
        _host?.NavigateToShop();
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
