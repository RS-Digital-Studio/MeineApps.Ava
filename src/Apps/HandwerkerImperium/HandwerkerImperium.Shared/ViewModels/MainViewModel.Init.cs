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

        // FpsProfile an die vom Spieler gewaehlte Grafikqualitaet binden. Aktualisiert laufende
        // Render-Timer (WorkerAvatar via Event, andere Views lesen neu bei Tab-Wechsel).
        Graphics.FpsProfile.SetCurrent(_gameStateService.Settings.GraphicsQuality);

        // Sprache synchronisieren: gespeicherte Sprache laden oder Gerätesprache übernehmen
        var savedLang = _gameStateService.Settings.Language;
        if (!string.IsNullOrEmpty(savedLang))
        {
            _localizationService.SetLanguage(savedLang);
        }
        else
        {
            // Neues Spiel: Gerätesprache in GameState übernehmen
            _gameStateService.Settings.Language = _localizationService.CurrentLanguage;
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
        MissionsVM.RefreshQuickJobs();

        // Daily Challenges initialisieren
        _dailyChallengeService.CheckAndResetIfNewDay();
        MissionsVM.MarkChallengesDirty();
        MissionsVM.RefreshChallenges();

        // Weekly Missions initialisieren
        _weeklyMissionService.CheckAndResetIfNewWeek();
        MissionsVM.RefreshWeeklyMissions();

        // Lucky Spin Status
        MissionsVM.HasFreeSpin = _luckySpinService.HasFreeSpin;

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
        if (WelcomeFlowVM.IsOfflineEarningsDialogVisible || WelcomeFlowVM.IsCombinedWelcomeDialogVisible || MissionsVM.IsWelcomeOfferVisible)
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
            if (WelcomeFlowVM.IsDailyRewardDialogVisible)
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

        // Start the game loop for idle earnings
        _gameLoopService.Start();

        // WhatsNew-Dialog fuer Bestandsspieler nach App-Update.
        // Wird verzoegert ausgespielt, damit Offline-Earnings/Daily-Reward/Story zuerst durchgehen.
        // Fire-and-forget — Spielstart darf darauf nicht warten.
        if (_whatsNewService != null)
            ShowWhatsNewDeferredAsync().SafeFireAndForget();

        // Telemetrie: Analytics + Session-Start (nur wenn Consent gegeben oder noch nie gefragt).
        // ShowAnalyticsConsentIfNeededAsync laeuft nicht-blockierend — der Spieler kann schon spielen.
        if (_analyticsService != null)
        {
            if (_gameStateService.Settings.AnalyticsConsentShown && _gameStateService.Settings.AnalyticsEnabled)
            {
                await _analyticsService.InitializeAsync();
            }
            else if (!_gameStateService.Settings.AnalyticsConsentShown)
            {
                ShowAnalyticsConsentIfNeededAsync().SafeFireAndForget();
            }
        }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] InitializeAsync fehlgeschlagen: {ex}");
            IsLoading = false;

            // Fehlerdialog anzeigen (DialogVM ist per DI injiziert, immer verfügbar)
            try
            {
                DialogVM.ShowAlertDialog(
                    _localizationService?.GetString("Error") ?? "Error",
                    _localizationService?.GetString("InitError") ?? "An error occurred while loading. Please restart the app.",
                    "OK");
            }
            catch
            {
                // Wenn selbst der Dialog fehlschlägt, still ignorieren
            }
        }
    }

    /// <summary>
    /// Vergleicht Cloud-Spielstand mit lokalem und fragt Benutzer bei neuerem Cloud-Save.
    /// Nutzt <see cref="ICloudSaveService"/> (Firebase-REST), ersetzt den nicht-funktionalen Play-Games-Stub.
    /// </summary>
    private async Task CheckCloudSaveAsync()
    {
        if (_cloudSaveService?.IsAvailable != true || !_gameStateService.Settings.CloudSaveEnabled)
            return;

        try
        {
            var metadata = await _cloudSaveService.GetMetadataAsync();
            if (metadata == null) return;

            // App-Outdated-Schutz. Wenn der Cloud-Save mit einer
            // neueren App-Version geschrieben wurde (z.B. Spieler hat 2 Geraete, aktuelles
            // Geraet ist alte App-Version), KEIN Download — sonst wuerde Migration auf
            // bereits-aktuelle Daten den State korrumpieren. Nutzer sieht stattdessen
            // den Hinweis dass er die App aktualisieren muss.
            if (metadata.StateVersion > GameState.CurrentStateVersion)
            {
                var outdatedTitle = _localizationService.GetString("CloudSaveTooNewTitle")
                    ?? "App update required";
                var outdatedBody = _localizationService.GetString("CloudSaveTooNewBody")
                    ?? "Your cloud save was created with a newer app version. Please update the app in the Play Store.";
                DialogVM.ShowAlertDialog(outdatedTitle, outdatedBody, _localizationService.GetString("Confirm") ?? "OK");
                return;
            }

            // Cloud neuer als lokal? Toleranz 5s gegen Clock-Skew.
            // H-H09: Wenn der lokale Save beschaedigt war (LastLoadFailedCorrupt → CreateNew lief),
            // die SavedAt-Heuristik ueberspringen — der Cloud-Stand ist IMMER besser als der
            // frische Leer-State, auch wenn sein Zeitstempel aelter aussieht.
            var localSavedAt = _gameStateService.State.LastSavedAt;
            var cloudSavedAt = metadata.SavedAtUtc;
            bool localWasCorrupt = _saveGameService.LastLoadFailedCorrupt;
            if (!localWasCorrupt && cloudSavedAt <= localSavedAt.AddSeconds(5))
                return;

            // Konflikt-Dialog: zeigt Level + Money beider Stände
            var title = _localizationService.GetString("CloudSaveNewer") ?? "A newer cloud save was found (Level {0}). Use cloud save?";
            var localLbl = string.Format(
                _localizationService.GetString("CloudSaveLocalSummary") ?? "Local: Level {0} ({1})",
                _gameStateService.State.PlayerLevel,
                Helpers.MoneyFormatter.FormatCompact(_gameStateService.State.Money));
            var cloudLbl = string.Format(
                _localizationService.GetString("CloudSaveCloudSummary") ?? "Cloud: Level {0} ({1})",
                metadata.PlayerLevel,
                Helpers.MoneyFormatter.FormatCompact(metadata.Money));
            var message = $"{localLbl}\n{cloudLbl}";

            var useCloud = _localizationService.GetString("UseCloudSave") ?? "Use Cloud";
            var useLocal = _localizationService.GetString("UseLocalSave") ?? "Keep Local";

            var confirmed = await ShowConfirmDialog(title, message, useCloud, useLocal);
            if (!confirmed) return;

            var cloudState = await _cloudSaveService.DownloadAsync();
            if (cloudState == null) return;

            // Cloud-State via ImportSaveAsync einspielen (ruft SanitizeState + SaveInternalAsync)
            var cloudJson = System.Text.Json.JsonSerializer.Serialize(cloudState);
            await _saveGameService.ImportSaveAsync(cloudJson);
            RefreshFromState();

            _analyticsService?.TrackEvent(AnalyticsEvents.CloudSaveDownloaded, new Dictionary<string, object?>
            {
                ["level"] = cloudState.PlayerLevel,
                ["money"] = (double)cloudState.Money
            });
        }
        catch (Exception ex)
        {
            // Cloud-Sync-Fehler still ignorieren (lokaler Save funktioniert)
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] CheckCloudSaveAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// Wartet kurz und zeigt dann den WhatsNew-Dialog wenn er
    /// gebraucht wird. Wartet zusaetzlich falls beim Start andere Dialoge offen sind
    /// (Offline/DailyReward/Story/Welcome/Starter-Offer) — Bestandsspieler haben nach
    /// einem Update meist mehrere Dialog-Kandidaten.
    /// </summary>
    private async Task ShowWhatsNewDeferredAsync()
    {
        if (_whatsNewService == null) return;

        // Erste Verzoegerung: andere Startup-Dialoge zuerst durchlassen.
        await Task.Delay(2500);

        // Maximal 4 Sekunden zusaetzlich warten falls Dialoge offen sind.
        for (int i = 0; i < 8 && IsAnyDialogVisible; i++)
            await Task.Delay(500);

        if (IsAnyDialogVisible) return; // ergibt sich beim naechsten Start nochmal

        await _whatsNewService.ShowWhatsNewIfNeededAsync();
    }

    /// <summary>
    /// Zeigt den DSGVO-Consent-Dialog fuer Analytics, wenn er noch nie gezeigt wurde.
    /// Wird nicht-blockierend aufgerufen (fire-and-forget) damit der Spielstart nicht wartet.
    /// </summary>
    private async Task ShowAnalyticsConsentIfNeededAsync()
    {
        if (_analyticsService == null) return;
        if (_gameStateService.Settings.AnalyticsConsentShown) return;

        // Kleines Delay, damit der Consent-Dialog nicht mit Offline-Earnings/Welcome-Dialog kollidiert
        await Task.Delay(1500);
        if (WelcomeFlowVM.IsOfflineEarningsDialogVisible ||
            WelcomeFlowVM.IsCombinedWelcomeDialogVisible ||
            WelcomeFlowVM.IsDailyRewardDialogVisible)
        {
            // Warten bis der erste Dialog geschlossen ist
            await Task.Delay(2500);
        }

        var title = _localizationService.GetString("AnalyticsConsentTitle") ?? "Help us improve?";
        var message = _localizationService.GetString("AnalyticsConsentMessage")
                      ?? "Anonymous usage data helps us improve the game. No personal data, no third-party tracking. You can change this in settings at any time.";
        var accept = _localizationService.GetString("AnalyticsConsentAccept") ?? "Yes, help";
        var decline = _localizationService.GetString("AnalyticsConsentDecline") ?? "No, thanks";

        var consent = await ShowConfirmDialog(title, message, accept, decline);

        _gameStateService.Settings.AnalyticsConsentShown = true;
        _analyticsService.IsEnabled = consent;

        if (consent)
        {
            await _analyticsService.InitializeAsync();
        }

        // Settings speichern damit der Dialog nicht nochmal auftaucht
        await _saveGameService.SaveAsync();
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
        MissionsVM.PendingOfflineEarnings = earnings;
        var maxDuration = _offlineProgressService.GetMaxOfflineDuration();
        bool wasCapped = offlineDuration > maxDuration;
        var effectiveDuration = wasCapped ? maxDuration : offlineDuration;

        WelcomeFlowVM.OfflineEarningsAmountText = MoneyFormatter.FormatCompact(earnings);
        var durationText = effectiveDuration.TotalHours >= 1
            ? $"{(int)effectiveDuration.TotalHours}h {effectiveDuration.Minutes}min"
            : $"{(int)effectiveDuration.TotalMinutes}min";
        // Hinweis wenn Offline-Dauer gekappt wurde
        if (wasCapped)
            durationText += $" (Max. {(int)maxDuration.TotalHours}h)";
        WelcomeFlowVM.OfflineEarningsDurationText = durationText;

        // Effizienz-Hinweis: Durchschnittliche Offline-Rate basiert auf gestaffeltem System
        var avgOfflinePercent = effectiveDuration.TotalHours switch
        {
            <= 2 => 80,
            <= 4 => 55,
            <= 8 => 35,
            _ => 20
        };
        WelcomeFlowVM.OfflineEfficiencyHint = string.Format(
            _localizationService.GetString("OfflineEfficiencyHint") ?? "~{0}% deiner Online-Einnahmen",
            avgOfflinePercent);

        // Neuer Rekord pruefen
        WelcomeFlowVM.IsOfflineNewRecord = earnings > _gameStateService.State.MaxOfflineEarnings;
        if (WelcomeFlowVM.IsOfflineNewRecord)
            _gameStateService.State.MaxOfflineEarnings = earnings;

        // Wiedereinsteiger-Tipp: Nächstes Ziel anzeigen wenn >48h offline
        if (offlineDuration.TotalHours > 48)
        {
            var goal = _goalService.GetCurrentGoal();
            WelcomeFlowVM.OfflineGoalText = goal?.Description ?? "";
        }
        else
        {
            WelcomeFlowVM.OfflineGoalText = "";
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
            WelcomeFlowVM.CombinedOfflineEarnings = WelcomeFlowVM.OfflineEarningsAmountText;
            WelcomeFlowVM.CombinedOfflineDuration = WelcomeFlowVM.OfflineEarningsDurationText;
            WelcomeFlowVM.CombinedOfferMoney = MoneyFormatter.FormatCompact(offer!.MoneyReward);
            WelcomeFlowVM.CombinedOfferScrews = offer.GoldenScrewReward > 0 ? $"+{offer.GoldenScrewReward}" : "";

            var remaining = offer.TimeRemaining;
            WelcomeFlowVM.CombinedOfferTimer = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
                : $"{remaining.Minutes}m";

            WelcomeFlowVM.IsCombinedWelcomeDialogVisible = true;
        }
        else if (hasOffline)
        {
            // Nur Offline-Dialog
            WelcomeFlowVM.IsOfflineEarningsDialogVisible = true;
        }
        else if (hasWelcome)
        {
            // Nur Welcome-Back-Dialog (wird durch MissionsVM.OnWelcomeOfferGenerated angezeigt)
            MissionsVM.OnWelcomeOfferGenerated();
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

        WelcomeFlowVM.IsCombinedWelcomeDialogVisible = false;
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
            _uiEffectBus.RaiseFloatingText($"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}", "money");
            _pendingOfflineEarnings = 0;
        }

        // Welcome-Back-Offer ablehnen
        _welcomeBackService.DismissOffer();

        WelcomeFlowVM.IsCombinedWelcomeDialogVisible = false;
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
            _uiEffectBus.RaiseFloatingText($"+{MoneyFormatter.FormatCompact(amount)}", "money");

        _pendingOfflineEarnings = 0;
    }

    [RelayCommand]
    private void CollectOfflineEarningsNormal()
    {
        CollectOfflineEarnings(false);
        WelcomeFlowVM.IsOfflineEarningsDialogVisible = false;
        CheckDeferredDialogs();
    }

    [RelayCommand]
    private async Task CollectOfflineEarningsWithAdAsync()
    {
        var success = await _rewardedAdService.ShowAdAsync("offline_double");
        CollectOfflineEarnings(success);
        WelcomeFlowVM.IsOfflineEarningsDialogVisible = false;
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
        MissionsVM.CanRescueStreak = state.StreakBeforeBreak > 1
                          && state.DailyRewardStreak <= 1
                          && !state.StreakRescueUsed
                          && state.GoldenScrews >= 3;  // BAL-7: Von 5 auf 3 reduziert
        if (MissionsVM.CanRescueStreak)
        {
            var costText = _localizationService.GetString("StreakRescueCost") ?? "{0} Golden Screws";
            MissionsVM.StreakRescueText = string.Format(costText, 3);
        }

        if (HasDailyReward)
        {
            var rewards = _dailyRewardService.GetRewardCycle();
            var currentDay = _dailyRewardService.CurrentDay;
            var currentStreak = _dailyRewardService.CurrentStreak;

            var todaysReward = _dailyRewardService.TodaysReward;
            WelcomeFlowVM.DailyRewardDayText = string.Format(_localizationService.GetString("DayReward"), currentDay);
            WelcomeFlowVM.DailyRewardStreakText = string.Format(_localizationService.GetString("DailyStreak"), currentStreak);
            WelcomeFlowVM.DailyRewardAmountText = todaysReward != null
                ? MoneyFormatter.FormatCompact(todaysReward.Money)
                : "";
            // Streak-Dots datengebunden — siehe DailyRewardDialog.axaml.
            WelcomeFlowVM.UpdateStreakDays(currentDay, currentStreak);
            WelcomeFlowVM.IsDailyRewardDialogVisible = true;
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
                    _uiEffectBus.RaiseCelebration();
                    _uiEffectBus.RaiseCeremony(CeremonyType.Achievement, streakText,
                        $"{currentStreak} {_localizationService.GetString("Days") ?? "Days"}");
                    _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();
                    break;
                }
            }

            HasDailyReward = false;
            WelcomeFlowVM.IsDailyRewardDialogVisible = false;
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
            if (WelcomeFlowVM.IsDailyRewardDialogVisible) return;
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
        WelcomeFlowVM.StarterOfferCountdown = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
            : $"{remaining.Minutes}m {remaining.Seconds:D2}s";

        WelcomeFlowVM.IsStarterOfferVisible = true;
    }

    /// <summary>
    /// Spieler nimmt das Starter-Offer an -> zum Premium-Kauf navigieren.
    /// </summary>
    [RelayCommand]
    private void AcceptStarterOffer()
    {
        var state = _gameStateService.State;
        state.StarterOfferShown = true;
        WelcomeFlowVM.IsStarterOfferVisible = false;

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
        WelcomeFlowVM.IsStarterOfferVisible = false;
        CheckDeferredDialogs();
    }
}
