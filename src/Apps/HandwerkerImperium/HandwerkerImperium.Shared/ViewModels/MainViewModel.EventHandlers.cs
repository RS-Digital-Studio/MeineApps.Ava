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
/// Service-Event-Handler die im MainViewModel verbleiben: Money/Order/Lieferant/Event-System,
/// Cinematic, Reputation-Tier, State-Loaded, Premium, Sprachwechsel.
/// Progression-Feedback (Level/Prestige/Workshop/Worker/MasterTool/Achievement) liegt im
/// ProgressionFeedbackCoordinator.
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

    /// <summary>
    /// Coordinator hat Daten lokalisiert + Audio-Track
    /// gesetzt — wir leiten nur noch das View-Trigger-Event weiter.
    /// </summary>
    private void OnCinematicReadyFromCoordinator(HandwerkerImperium.Models.PrestigeCinematicData resolved)
    {
        PrestigeCinematicRequested?.Invoke(resolved);
    }

    /// <summary>
    /// Fallback-Pfad fuer Tests ohne Coordinator. Production-Code laeuft
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

        // Review-Milestone prüfen (CheckReviewPrompt liegt im ProgressionFeedbackCoordinator)
        _reviewService?.OnMilestone("orders", _gameStateService.Statistics.TotalOrdersCompleted);
        _progressionFeedbackCoordinator.CheckReviewPrompt();

        // Ziel-Cache invalidieren (Auftragsabschluss könnte Ziel erfüllen)
        _goalService.Invalidate();
    }

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

    private void OnDeliveryArrived(object? sender, SupplierDelivery delivery)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateDeliveryDisplay();
            _uiEffectBus.RaiseFloatingText(
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
            _uiEffectBus.RaiseFloatingText(msg, "warning");
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
            _uiEffectBus.RaiseFloatingText(
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
            _uiEffectBus.RaiseFloatingText(
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
    /// v2.0.37: Reputation-Tier-Wechsel — bei Aufstieg Confetti + FloatingText, bei Abstieg
    /// stille Aktualisierung der Header-Properties (Spieler soll nicht zusaetzlich frustriert
    /// werden, wenn Reputation faellt).
    ///
    /// Zusaetzlich Achievement-Dialog mit Tier-Effekten (Stammkunden-Bonus
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

            // Effekt-Logik in IReputationTierEffects extrahiert.
            _reputationTierEffects?.HandleTierChanged(
                e,
                floatingTextRaiser: (text, kind) => _uiEffectBus.RaiseFloatingText(text, kind),
                celebrationRaiser: () => _uiEffectBus.RaiseCelebration(),
                achievementDialog: (name, desc) =>
                {
                    DialogVM.AchievementName = name;
                    DialogVM.AchievementDescription = desc;
                    DialogVM.IsAchievementDialogVisible = true;
                });
        });
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
        // Markt + Lager wurden bei Laufzeit-Sprachwechsel uebergangen — ihre via {Binding Title/
        // EmptyMessage/Subtitle} gebundenen Texte blieben in der alten Sprache (gemischtsprachige UI).
        MarketVM.UpdateLocalizedTexts();
        WarehouseVM.UpdateLocalizedTexts();
    }

}
