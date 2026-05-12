using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Event-Handler-Subscriptions (Money, Level, Order, Worker, Prestige, Cinematic, BattlePass,
/// Language, Premium, State-Loaded, Master-Tool, Event-System).
/// AAA-Audit P0 Aufspaltung: aus MainViewModel.cs extrahiert (12.05.2026).
/// </summary>
public sealed partial class MainViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Benannter Handler fuer BackPressHelper.ExitHintRequested (statt anonymem Lambda, damit Dispose abmelden kann).
    /// </summary>
    private void OnBackPressExitHint(string msg) => ExitHintRequested?.Invoke(msg);

    // Debounce für teure Max-Modus-Berechnung (GetMaxAffordableUpgrades iteriert durch hunderte Levels)
    private DateTime _lastMaxModeCalc = DateTime.MinValue;

    private void OnMoneyChanged(object? sender, MoneyChangedEventArgs e)
    {
        HeaderVM.Money = e.NewAmount;
        // Phase 9: Smooth animierter Geld-Counter
        AnimateMoneyTo(e.NewAmount);

        // Geld-abhängige Workshop-Flags aktualisieren (CanAfford, BulkCost bei Max-Modus)
        // Bei x1/x10/x100: BulkUpgradeCost hängt nur vom Level ab (ändert sich nicht pro Tick),
        // daher nur CanAffordUpgrade aktualisieren statt teure Math.Pow-Schleife
        bool isMaxMode = BulkBuyAmount == 0;

        if (isMaxMode)
        {
            // Max-Modus: Nur alle 2s neu berechnen wenn Dashboard nicht sichtbar,
            // oder sofort wenn Dashboard sichtbar (dort sieht der User die Werte)
            var now = DateTime.UtcNow;
            bool shouldRecalc = IsDashboardActive || (now - _lastMaxModeCalc).TotalSeconds >= 2.0;
            if (!shouldRecalc)
            {
                // Nur CanAfford-Flags billig aktualisieren (Vergleiche statt Math.Pow-Schleifen)
                foreach (var workshop in Workshops)
                {
                    workshop.CanAffordUpgrade = e.NewAmount >= workshop.BulkUpgradeCost;
                    workshop.CanAffordWorker = e.NewAmount >= workshop.HireWorkerCost;
                }
                return;
            }
            _lastMaxModeCalc = now;

            // Max-Modus: Anzahl leistbarer Upgrades hängt vom Geld ab → muss neu berechnet werden
            var stateWorkshops = _gameStateService.State.Workshops;
            _workshopLookupCache.Clear();
            for (int i = 0; i < stateWorkshops.Count; i++)
                _workshopLookupCache[stateWorkshops[i].Type] = stateWorkshops[i];

            foreach (var workshop in Workshops)
            {
                _workshopLookupCache.TryGetValue(workshop.Type, out var ws);
                SetBulkUpgradeCost(workshop, ws, e.NewAmount);
                workshop.CanAffordWorker = e.NewAmount >= workshop.HireWorkerCost;
            }
        }
        else
        {
            // x1/x10/x100: BulkUpgradeCost ist level-abhängig (invariant bei Geld-Tick),
            // nur CanAfford-Flags aktualisieren (reine Vergleiche, kein Math.Pow)
            foreach (var workshop in Workshops)
            {
                workshop.CanAffordUpgrade = e.NewAmount >= workshop.BulkUpgradeCost;
                workshop.CanAffordUnlock = workshop.CanBuyUnlock && e.NewAmount >= workshop.UnlockCost;
                workshop.CanAffordWorker = e.NewAmount >= workshop.HireWorkerCost;
            }
        }
    }

    private void OnGoldenScrewsChanged(object? sender, GoldenScrewsChangedEventArgs e)
    {
        HeaderVM.GoldenScrewsDisplay = e.NewAmount.ToString("N0");

        // Goldschrauben-Erklärung beim allerersten Erhalt
        if (e.OldAmount == 0 && e.NewAmount > 0)
            _contextualHintService.TryShowHint(ContextualHints.GoldenScrews);

        // PP-3: FloatingText bei Goldschrauben-Ausgaben
        int diff = e.NewAmount - e.OldAmount;
        if (diff < 0)
            FloatingTextRequested?.Invoke($"{diff} GS", "warning");
        else if (diff > 0)
            FloatingTextRequested?.Invoke($"+{diff} GS", "goldscrews");
    }

    // Milestone-Level mit Goldschrauben-Belohnung
    private static readonly (int level, int screws)[] _milestones =
    [
        (10, 3), (25, 5), (50, 10), (100, 20), (250, 50), (500, 100), (1000, 200)
    ];

    private void OnLevelUp(object? sender, LevelUpEventArgs e)
    {
        HeaderVM.PlayerLevel = e.NewLevel;
        HeaderVM.LevelProgress = _gameStateService.State.LevelProgress;

        RefreshWorkshops();

        // Automation-Unlock-Properties aktualisieren (Level-Gates können sich ändern)
        OnPropertyChanged(nameof(IsAutoCollectUnlocked));
        OnPropertyChanged(nameof(IsAutoAcceptUnlocked));
        OnPropertyChanged(nameof(IsAutoAssignUnlocked));

        // Progressive Disclosure: Wird automatisch via [NotifyPropertyChangedFor] auf _playerLevel ausgelöst

        // Pulse-Animation bei JEDEM Level-Up (dezent, kein Dialog)
        DialogVM.IsLevelUpPulsing = true;
        _levelPulseTimer?.Stop();
        _levelPulseTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _levelPulseTimer.Tick -= OnLevelPulseTimeout;
        _levelPulseTimer.Tick += OnLevelPulseTimeout;
        _levelPulseTimer.Start();

        // Sound + FloatingText bei jedem Level-Up
        _audioService.PlaySoundAsync(GameSound.ButtonTap).FireAndForget();
        FloatingTextRequested?.Invoke($"Level {e.NewLevel}!", "level");

        // Milestone-Bonus prüfen (10/25/50/100/250/500/1000)
        foreach (var (level, screws) in _milestones)
        {
            if (e.NewLevel == level)
            {
                _gameStateService.AddGoldenScrews(screws);

                // Sound + Celebration nur bei Milestones
                _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();
                _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
                CelebrationRequested?.Invoke();
                CeremonyRequested?.Invoke(CeremonyType.LevelMilestone,
                    $"Level {e.NewLevel}!", $"+{screws} Goldschrauben");

                // FloatingText mit Level + Goldschrauben-Bonus
                FloatingTextRequested?.Invoke(
                    $"Level {e.NewLevel}! +{screws} \u2699", "level");
                break;
            }
        }

        // Tab-Freischaltung: Hinweis wenn ein neuer Tab verfügbar wird
        CheckTabUnlockNotification(e.NewLevel);

        // Kontextuelle Hints bei Level-Meilensteinen (passend zu Progressive Disclosure)
        // Nicht anzeigen wenn ein anderer Dialog offen ist (z.B. Prestige-Summary)
        if (IsAnyDialogVisible) return;

        if (e.NewLevel == LevelThresholds.HintWorkerUnlock)
            _contextualHintService.TryShowHint(ContextualHints.WorkerUnlock);
        else if (e.NewLevel == LevelThresholds.HintQuickJobs)
            _contextualHintService.TryShowHint(ContextualHints.QuickJobs);
        else if (e.NewLevel == LevelThresholds.HintCrafting)
            _contextualHintService.TryShowHint(ContextualHints.CraftingHint);
        else if (e.NewLevel == LevelThresholds.HintManagerUnlock)
            _contextualHintService.TryShowHint(ContextualHints.ManagerUnlock);
        else if (e.NewLevel == LevelThresholds.HintAutomation)
            _contextualHintService.TryShowHint(ContextualHints.Automation);
        else if (e.NewLevel == LevelThresholds.HintMasterTools)
            _contextualHintService.TryShowHint(ContextualHints.MasterToolsUnlock);
        else if (e.NewLevel == LevelThresholds.HintPrestige)
            _contextualHintService.TryShowHint(ContextualHints.PrestigeHint);

        // Story-Kapitel prüfen
        CheckForNewStoryChapter();

        // Review-Milestone prüfen
        _reviewService?.OnMilestone("level", e.NewLevel);
        CheckReviewPrompt();

        // Leaderboard-Score aktualisieren (fire-and-forget)
        if (_playGamesService?.IsSignedIn == true)
            _playGamesService.SubmitScoreAsync("leaderboard_player_level", e.NewLevel).SafeFireAndForget();
    }

    private void OnLevelPulseTimeout(object? sender, EventArgs e)
    {
        DialogVM.IsLevelUpPulsing = false;
        _levelPulseTimer?.Stop();
    }

    private void OnPrestigeCompleted(object? sender, EventArgs e)
    {
        var prestigeCount = _gameStateService.Prestige.TotalPrestigeCount;

        // Eternal Mastery (AAA-Audit P1 Long-Term-Engagement): Header-Badge aktualisieren
        RefreshEternalMastery();

        // Zeremonie: Feuerwerk + Confetti + Sound
        CelebrationRequested?.Invoke();
        var tierName = _localizationService.GetString("PrestigeCompleted") ?? "Prestige!";
        CeremonyRequested?.Invoke(CeremonyType.Prestige, tierName, $"#{prestigeCount}");
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        FloatingTextRequested?.Invoke($"Prestige #{prestigeCount}!", "level");

        // Floating-Hint mit aktuellem Eternal-Mastery-Bonus
        if (prestigeCount >= 1)
        {
            var bonusPct = GameBalanceConstants.EternalMasteryBonusPerPrestige * prestigeCount
                         + GameBalanceConstants.EternalMasteryBonusPer5Prestiges * (prestigeCount / 5)
                         + GameBalanceConstants.EternalMasteryBonusPer10Prestiges * (prestigeCount / 10);
            FloatingTextRequested?.Invoke(
                $"Eternal Mastery: +{bonusPct * 100m:F1}%",
                "level");
        }

        // Ascension-Hint-Kaskade (AAA-Audit P0 — Reset-Hierarchie-Vereinfachung):
        //   1. Prestige        → AscensionPath-Hint (Foreshadowing: "So funktioniert Ascension")
        //   3x Legende-Prestige → AscensionAvailable-Hint (Action: "Du kannst jetzt aufsteigen!")
        // So sieht der Spieler den Ascension-Tab nicht erst nach 3x Legende erstmals — er kennt
        // ihn vorher schon konzeptuell und arbeitet darauf hin.
        if (_gameStateService.Prestige.LegendeCount >= 3)
            _contextualHintService.TryShowHint(ContextualHints.AscensionAvailable);
        else if (prestigeCount == 1)
            _contextualHintService.TryShowHint(ContextualHints.AscensionPath);

        _reviewService?.OnMilestone("prestige", prestigeCount);
        CheckReviewPrompt();

        // Story-Kapitel prüfen (Prestige-bezogene Kapitel sofort triggern)
        CheckForNewStoryChapter();
    }

    /// <summary>
    /// AAA-Audit P0 Zerlegungs-Sprint: Coordinator hat Daten lokalisiert + Audio-Track
    /// gesetzt — wir leiten nur noch das View-Trigger-Event weiter.
    /// </summary>
    private void OnCinematicReadyFromCoordinator(HandwerkerImperium.Models.PrestigeCinematicData resolved)
    {
        PrestigeCinematicRequested?.Invoke(resolved);
    }

    /// <summary>
    /// P0.3 AAA-Audit: Fallback-Pfad fuer Tests ohne Coordinator. Production-Code laeuft
    /// ueber <see cref="OnCinematicReadyFromCoordinator"/>.
    /// </summary>
    private void OnPrestigeCinematicReady(object? sender, HandwerkerImperium.Models.PrestigeCinematicData data)
    {
        var localizedTierName = _localizationService.GetString($"Prestige{data.Tier}") ?? data.Tier.ToString();
        var resolved = new HandwerkerImperium.Models.PrestigeCinematicData
        {
            MoneyAtPrestige = data.MoneyAtPrestige,
            Tier = data.Tier,
            BasePrestigePoints = data.BasePrestigePoints,
            BonusPrestigePoints = data.BonusPrestigePoints,
            TierMultiplierRaw = data.TierMultiplierRaw,
            DiminishingReturnsFactor = data.DiminishingReturnsFactor,
            TierMultiplierEffective = data.TierMultiplierEffective,
            TierCount = data.TierCount,
            RunDurationSeconds = data.RunDurationSeconds,
            ActiveChallengeCount = data.ActiveChallengeCount,
            TierDisplayName = localizedTierName,
        };
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try { _ = _audioService.PlayMusicAsync(MusicTrack.Celebration, crossfade: true); }
            catch { /* Audio-Fehler ignorieren */ }
            PrestigeCinematicRequested?.Invoke(resolved);
        });
    }

    private void OnPrestigeMilestoneReached(object? sender, PrestigeMilestoneEventArgs e)
    {
        var text = string.Format(
            _localizationService.GetString("PrestigeMilestoneReached") ?? "Prestige milestone! +{0} golden screws",
            e.GoldenScrewReward);
        FloatingTextRequested?.Invoke(text, "currency");
        CelebrationRequested?.Invoke();
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
    }

    private void OnRebirthCompleted(object? sender, WorkshopType type)
    {
        // Erster-Stern-Hint nach erstem Rebirth (erklärt Stern-Boni)
        _contextualHintService.TryShowHint(ContextualHints.FirstStar);
    }

    private void CheckReviewPrompt()
    {
        if (_reviewService?.ShouldPromptReview() == true)
        {
            _reviewService.MarkReviewPrompted();
            App.ReviewPromptRequested?.Invoke();
        }
    }

    /// <summary>
    /// Worker-Level-Up: FloatingText mit Name + neuem Level und Sound.
    /// </summary>
    private void OnWorkerLevelUp(object? sender, Worker worker)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var levelUpText = string.Format(
                _localizationService.GetString("WorkerLevelUp") ?? "{0} ist jetzt Level {1}!",
                worker.Name, worker.ExperienceLevel);
            FloatingTextRequested?.Invoke(levelUpText, "level");
            _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
        });
    }

    private void OnXpGained(object? sender, XpGainedEventArgs e)
    {
        HeaderVM.CurrentXp = e.CurrentXp;
        HeaderVM.XpForNextLevel = e.XpForNextLevel;
        // Korrekte Formel aus GameState verwenden (berücksichtigt XP-Basis des aktuellen Levels)
        HeaderVM.LevelProgress = _gameStateService.State.LevelProgress;
    }

    private void OnWorkshopUpgraded(object? sender, WorkshopUpgradedEventArgs e)
    {
        // Nur den betroffenen Workshop aktualisieren statt alle
        RefreshSingleWorkshop(e.WorkshopType);

        // Workshop-Detail-Hint nach erstem Upgrade zeigen
        if (!_contextualHintService.HasSeenHint(ContextualHints.WorkshopDetail.Id))
        {
            ShowTutorialHint = false;
            _contextualHintService.TryShowHint(ContextualHints.WorkshopDetail);
        }
        // v2.0.39 Audit-Fix U10: Long-Press-Hint nach 2. Upgrade — Spieler hat erstes Tap-Upgrade
        // erlebt, jetzt ist der Discoverability-Moment fuer "Halten = x10 / x100 Bulk".
        // Bei aktivem Hold-to-Upgrade zeigen wir den Hint NICHT (er kennt das Feature dann schon).
        else if (!IsHoldingUpgrade && !_contextualHintService.HasSeenHint(ContextualHints.LongPressBulk.Id))
        {
            _contextualHintService.TryShowHint(ContextualHints.LongPressBulk);
        }

        // Rebirth-Hint: Erster Workshop erreicht Level 1000
        if (e.NewLevel >= Workshop.MaxLevel)
            _contextualHintService.TryShowHint(ContextualHints.RebirthReady);

        // Multiplikator-Meilensteine (Bumpy Progression)
        if (!IsHoldingUpgrade && Workshop.IsMilestoneLevel(e.NewLevel))
        {
            decimal milestoneMultiplier = Workshop.GetMilestoneMultiplierForLevel(e.NewLevel);
            var workshopName = _localizationService.GetString(e.WorkshopType.GetLocalizationKey());
            string boostText = $"x{milestoneMultiplier:0.#} {_localizationService.GetString("IncomeBoost") ?? "Income Boost"}!";

            FloatingTextRequested?.Invoke(boostText, "golden_screws");
            _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

            // Größere Zeremonien bei höheren Meilensteinen
            if (e.NewLevel >= LevelThresholds.WorkshopCeremonyThreshold)
            {
                CeremonyRequested?.Invoke(CeremonyType.WorkshopMilestone,
                    $"{workshopName} Lv.{e.NewLevel}",
                    boostText);
            }
        }

        // Workshop-Level-Milestone prüfen (nicht während Hold-to-Upgrade)
        // Schwellen weiter auseinander damit nicht bei jedem frühen Level Benachrichtigungen kommen
        if (!IsHoldingUpgrade)
        {
            foreach (var (level, screws) in s_workshopMilestones)
            {
                if (e.NewLevel == level)
                {
                    _gameStateService.AddGoldenScrews(screws);
                    var workshopName = _localizationService.GetString(e.WorkshopType.GetLocalizationKey());
                    FloatingTextRequested?.Invoke(
                        $"{workshopName} Lv.{e.NewLevel}! +{screws} \u2699", "level");
                    CelebrationRequested?.Invoke();
                    CeremonyRequested?.Invoke(CeremonyType.WorkshopMilestone,
                        $"{workshopName} Lv.{e.NewLevel}!", $"+{screws} Goldschrauben");
                    _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();
                    break;
                }
            }

            // Story-Kapitel prüfen
            CheckForNewStoryChapter();
        }

        // Ziel-Cache invalidieren (Workshop-Level könnte Ziel erfüllen)
        _goalService.Invalidate();
    }

    private void OnWorkerHired(object? sender, WorkerHiredEventArgs e)
    {
        // Nur den betroffenen Workshop aktualisieren statt alle
        RefreshSingleWorkshop(e.WorkshopType);

        // Ziel-Cache invalidieren (Worker-Einstellung könnte Ziel erfüllen)
        _goalService.Invalidate();
    }

    private void OnOrderCompleted(object? sender, OrderCompletedEventArgs e)
    {
        HasActiveOrder = false;
        ActiveOrder = null;

        // Replenish orders if running low
        if (_gameStateService.State.AvailableOrders.Count < 2)
        {
            _orderGeneratorService.RefreshOrders();
        }

        RefreshOrders();

        // Hint beim ersten Auftragsabschluss
        if (_gameStateService.Statistics.TotalOrdersCompleted == 1)
            _contextualHintService.TryShowHint(ContextualHints.OrderCompleted);

        // Story-Kapitel prüfen
        CheckForNewStoryChapter();

        // Review-Milestone prüfen
        _reviewService?.OnMilestone("orders", _gameStateService.Statistics.TotalOrdersCompleted);
        CheckReviewPrompt();

        // Ziel-Cache invalidieren (Auftragsabschluss könnte Ziel erfüllen)
        _goalService.Invalidate();
    }

    // OnChallengeProgressChanged → extrahiert nach MissionsFeatureViewModel

    private async void OnShowPrestigeDialog(object? sender, EventArgs e)
        => await Helpers.AsyncExtensions.RunHandlerSafely(ShowPrestigeConfirmationAsync);

    private void OnMiniGameResultRecorded(object? sender, MiniGameResultRecordedEventArgs e)
    {
        // Flag setzen: MiniGame wurde tatsächlich gespielt (für QuickJob-Validierung)
        _quickJobMiniGamePlayed = true;

        // Turnier-Score aufzeichnen (Rating → Prozent-Score: Miss=50, Ok=75, Good=100, Perfect=150)
        if (_isTournamentRound)
        {
            int score = (int)(e.Rating.GetRewardPercentage() * 100);
            _tournamentService?.RecordScore(score);
        }
    }

    private void OnMasterToolUnlocked(object? sender, MasterToolDefinition tool)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var name = _localizationService.GetString(tool.NameKey);
            if (string.IsNullOrEmpty(name)) name = tool.Id;
            FloatingTextRequested?.Invoke($"{tool.Icon} {name}!", "MasterTool");
            CelebrationRequested?.Invoke();
            CeremonyRequested?.Invoke(CeremonyType.MasterTool, name, $"+{(int)(tool.IncomeBonus * 100)}% Einkommen");
            _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

            MissionsVM.MasterToolsCollected = _gameStateService.State.CollectedMasterTools.Count;
        });
    }

    private void OnDeliveryArrived(object? sender, SupplierDelivery delivery)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateDeliveryDisplay();
            FloatingTextRequested?.Invoke(
                $"{_localizationService.GetString("DeliveryArrived")}!", "Delivery");
        });
    }

    /// <summary>
    /// Handler: Aktiver Auftrag ist abgelaufen (Deadline überschritten).
    /// Setzt UI-State zurück, damit kein "Geister-Auftrag" angezeigt wird.
    /// </summary>
    private void OnOrderExpired(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveOrder = false;
            ActiveOrder = null;
            var msg = _localizationService.GetString("OrderExpiredNotification") ?? "Order expired!";
            FloatingTextRequested?.Invoke(msg, "warning");
            _audioService.PlaySoundAsync(GameSound.Miss).FireAndForget();
            RefreshOrders();
        });
    }

    /// <summary>
    /// Handler: Automation hat eine Lieferung automatisch eingesammelt.
    /// Aktualisiert die Lieferungs-Anzeige in der UI.
    /// </summary>
    private void OnAutoCollectedDelivery(object? sender, SupplierDelivery delivery)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasPendingDelivery = false;
            FloatingTextRequested?.Invoke(
                $"{_localizationService.GetString("DeliveryCollected") ?? "Delivery collected"}!", "Delivery");
        });
    }

    /// <summary>
    /// Handler: Automation hat einen Auftrag automatisch angenommen.
    /// Aktualisiert Auftrags-Anzeige und verfügbare Aufträge.
    /// </summary>
    private void OnAutoAcceptedOrder(object? sender, Order order)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveOrder = true;
            ActiveOrder = order;
            RefreshOrders();
        });
    }

    private void OnEventStarted(object? sender, GameEvent evt)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveEvent = true;
            ActiveEventIcon = evt.Icon;
            ActiveEventName = _localizationService.GetString(evt.NameKey);
            ActiveEventDescription = _localizationService.GetString(evt.DescriptionKey);
            UpdateEventTimer();

            // FloatingText-Benachrichtigung anzeigen
            FloatingTextRequested?.Invoke(
                $"{evt.Icon} {ActiveEventName}", "Event");
        });
    }

    private void OnEventEnded(object? sender, GameEvent evt)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveEvent = false;
            ActiveEventIcon = "";
            ActiveEventName = "";
            ActiveEventDescription = "";
            ActiveEventTimeRemaining = "";
        });
    }

    /// <summary>
    /// Aktualisiert Event-Timer und saisonalen Modifikator (wird im GameTick aufgerufen).
    /// </summary>
    private void UpdateEventDisplay()
    {
        var activeEvent = _eventService.ActiveEvent;
        if (activeEvent != null)
        {
            HasActiveEvent = true;
            ActiveEventIcon = activeEvent.Icon;

            // Event-Name nur neu laden wenn sich der Event-Key geaendert hat
            if (_cachedActiveEventKey != activeEvent.NameKey)
            {
                _cachedActiveEventKey = activeEvent.NameKey;
                _cachedActiveEventName = _localizationService.GetString(activeEvent.NameKey);
            }
            ActiveEventName = _cachedActiveEventName ?? string.Empty;
            UpdateEventTimer();
        }
        else if (HasActiveEvent)
        {
            HasActiveEvent = false;
            _cachedActiveEventKey = null;
        }

        // Saisonaler Modifikator (nur bei Monatswechsel neu berechnen)
        var month = DateTime.UtcNow.Month;
        if (month != _cachedSeasonMonth)
        {
            _cachedSeasonMonth = month;
            SeasonalModifierText = month switch
            {
                3 or 4 or 5 => _localizationService.GetString("SeasonSpring"),
                6 or 7 or 8 => _localizationService.GetString("SeasonSummer"),
                9 or 10 or 11 => _localizationService.GetString("SeasonAutumn"),
                _ => _localizationService.GetString("SeasonWinter")
            };
        }
    }

    private void UpdateEventTimer()
    {
        var activeEvent = _eventService.ActiveEvent;
        if (activeEvent == null) return;

        var remaining = activeEvent.RemainingTime;
        ActiveEventTimeRemaining = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
            : $"{remaining.Minutes}m {remaining.Seconds:D2}s";
    }

    /// <summary>
    /// Forwarded PropertyChanged-Events von HeaderVM an MainViewModel fuer computed Properties.
    /// Nur fuer NotifyPropertyChangedFor-Effekte (ShowCraftingResearch etc.) bei PlayerLevel-Aenderung.
    /// AXAML-Bindings zeigen direkt auf HeaderVM.X — kein Forward der einzelnen Properties noetig.
    /// </summary>
    private void OnHeaderVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HeaderViewModel.PlayerLevel)) return;
        OnPropertyChanged(nameof(ShowCraftingResearch));
        OnPropertyChanged(nameof(ShowManagerSection));
        OnPropertyChanged(nameof(ShowMasterToolsSection));
        OnPropertyChanged(nameof(IsQuickJobsUnlocked));
        OnPropertyChanged(nameof(ShowBannerStrip));
        OnPropertyChanged(nameof(QuickAccessColumns));
    }

    private void OnStateLoaded(object? sender, EventArgs e)
    {
        _achievementService.Reset();
        RefreshFromState();
        RefreshEternalMastery();
    }

    /// <summary>
    /// v2.1.0: Praktikant hat 24h aktiv trainiert — Spieler bekommt Promotion-Dialog.
    /// Bei Annahme wird er zu E-Tier promoviert (kostenpflichtig), bei Ablehnung verlaesst er.
    /// </summary>
    private void OnInternReadyForPromotion(object? sender, Worker intern)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var title = _localizationService.GetString("InternPromotionTitle") ?? "Praktikant bereit zur Promotion";
            var msgFormat = _localizationService.GetString("InternPromotionMessage")
                            ?? "{0} hat 24h Training abgeschlossen. Behalten (E-Tier, Lohn) oder gehen lassen?";
            var keep = _localizationService.GetString("InternPromotionKeep") ?? "Behalten";
            var let = _localizationService.GetString("InternPromotionLet") ?? "Gehen lassen";

            var confirmed = await DialogVM.ShowConfirmDialog(
                title, string.Format(msgFormat, intern.Name), keep, let);

            if (confirmed)
            {
                _workerService.PromoteIntern(intern.Id);
                FloatingTextRequested?.Invoke($"{intern.Name}: E-Tier!", "level");
            }
            else
            {
                _workerService.DeclineInternPromotion(intern.Id);
            }
        });
    }

    /// <summary>
    /// v2.0.37: Reputation-Tier-Wechsel — bei Aufstieg Confetti + FloatingText, bei Abstieg
    /// stille Aktualisierung der Header-Properties (Spieler soll nicht zusaetzlich frustriert
    /// werden, wenn Reputation faellt).
    ///
    /// v2.0.39 Audit-Fix U7: Zusaetzlich Achievement-Dialog mit Tier-Effekten (Stammkunden-Bonus
    /// + Live-Order-Spawn-Chance), damit der Spieler die Bedeutung des Aufstiegs erkennt — nicht
    /// nur einen Floating-Text. Dialog erscheint NUR bei Aufstieg, nicht bei Abstieg.
    /// </summary>
    private void OnReputationTierChanged(object? sender, ReputationTierChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Header-Bindings aktualisieren — alle drei Properties sind computed.
            OnPropertyChanged(nameof(CurrentReputationTier));
            OnPropertyChanged(nameof(ShowReputationTierBadge));
            OnPropertyChanged(nameof(ReputationTierName));
            OnPropertyChanged(nameof(ReputationTierColor));

            // AAA-Audit P0 Zerlegungs-Sprint: Effekt-Logik in IReputationTierEffects extrahiert.
            _reputationTierEffects?.HandleTierChanged(
                e,
                floatingTextRaiser: (text, kind) => FloatingTextRequested?.Invoke(text, kind),
                celebrationRaiser: () => CelebrationRequested?.Invoke(),
                achievementDialog: (name, desc) =>
                {
                    DialogVM.AchievementName = name;
                    DialogVM.AchievementDescription = desc;
                    DialogVM.IsAchievementDialogVisible = true;
                });
        });
    }

    private void OnAchievementUnlocked(object? sender, Achievement achievement)
    {
        // Während Hold-to-Upgrade keine Dialoge anzeigen
        if (IsHoldingUpgrade) return;

        _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

        var title = _localizationService.GetString(achievement.TitleKey);
        DialogVM.AchievementName = string.IsNullOrEmpty(title) ? achievement.TitleFallback : title;
        var desc = _localizationService.GetString(achievement.DescriptionKey);
        DialogVM.AchievementDescription = string.IsNullOrEmpty(desc) ? achievement.DescriptionFallback : desc;
        DialogVM.IsAchievementDialogVisible = true;
        CelebrationRequested?.Invoke();
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ShowAds));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Lokalisierungs-Caches aktualisieren
        _cachedNetIncomeLabel = _localizationService.GetString("NetIncome") ?? "Net Income";
        _cachedActiveEventKey = null; // Event-Name bei Sprachwechsel neu laden
        EconomyVM.InvalidatePrestigeBannerCache(); // Prestige-Banner mit neuen Texten neu berechnen

        // Statische Renderer-Strings aktualisieren
        WorkshopGameCardRenderer.UpdateLocalizedStrings(
            _localizationService.GetString("TapToUnlock") ?? "Tap to unlock",
            _localizationService.GetString("AtLevelShort") ?? "From Level {0}");

        // Alle lokalisierten Display-Texte aktualisieren
        MissionsVM.RefreshQuickJobs();
        MissionsVM.MarkChallengesDirty();
        MissionsVM.RefreshChallenges();
        RefreshWorkshops();

        // Child-VMs aktualisieren
        WorkerMarketViewModel.UpdateLocalizedTexts();
        WorkerProfileViewModel.UpdateLocalizedTexts();
        BuildingsViewModel.UpdateLocalizedTexts();
        ResearchViewModel.UpdateLocalizedTexts();
        ShopViewModel.LoadShopData();
        ShopViewModel.LoadTools();
        GuildViewModel.UpdateLocalizedTexts();
        ManagerViewModel.UpdateLocalizedTexts();
        CraftingViewModel.UpdateLocalizedTexts();
        LuckySpinViewModel.UpdateLocalizedTexts();
        BattlePassViewModel.UpdateLocalizedTexts();
        TournamentViewModel.UpdateLocalizedTexts();
        SeasonalEventViewModel.UpdateLocalizedTexts();
    }

}
