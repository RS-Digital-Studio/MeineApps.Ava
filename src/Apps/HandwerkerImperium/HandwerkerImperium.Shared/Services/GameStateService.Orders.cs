using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Auftrags-Operationen, Mini-Game-Ergebnisse, Lieferaufträge,
/// Lock-Zugriff und Event-Raiser für externe Services.
/// </summary>
public sealed partial class GameStateService
{
    // ===================================================================
    // AUFTRAGS-OPERATIONEN
    // ===================================================================

    public void StartOrder(Order order)
    {
        lock (_stateLock)
        {
            _state.AvailableOrders.Remove(order);
            _state.ActiveOrder = order;
            // v2.0.35 Feature A: Multi-Order-System — Auftrag in ParallelOrdersByWorkshop spiegeln.
            // Ein Workshop hat gleichzeitig max. einen aktiven Auftrag.
            _state.ParallelOrdersByWorkshop[order.WorkshopType] = order;

            // v2.0.35 Feature D: Live-Auftraege verlieren ihre "Annahme-Deadline" beim Start.
            // ExpiresAt bedeutete "Zeit zum Akzeptieren" — sobald der Spieler den Auftrag
            // startet, laeuft er normal (nur Deadline/Weekly kann noch greifen).
            if (order.IsLive)
            {
                order.ExpiresAt = null;
            }
        }
    }

    /// <summary>
    /// V7 (Phase 2 Ressourcen-Plan): Akzeptiert das Material-Angebot eines Auftrags.
    /// Atomar im Lock: Pruefung + Reservierung. Bei Nicht-Verfuegbarkeit false.
    /// </summary>
    public bool TryAcceptMaterialOffer(Order order)
    {
        if (order == null || !order.HasMaterialOffer || order.MaterialOfferAccepted) return false;

        lock (_stateLock)
        {
            // Verfuegbarkeit pruefen: total - reserved >= required
            foreach (var (productId, required) in order.MaterialOffer!)
            {
                int total = _state.CraftingInventory.GetValueOrDefault(productId, 0);
                int reserved = _state.ReservedInventory.GetValueOrDefault(productId, 0);
                if (total - reserved < required) return false;
            }

            // Reservieren (alle Materialien atomar)
            foreach (var (productId, required) in order.MaterialOffer!)
            {
                int current = _state.ReservedInventory.GetValueOrDefault(productId, 0);
                _state.ReservedInventory[productId] = current + required;
            }

            order.MaterialOfferAccepted = true;
        }
        return true;
    }

    public Order? GetActiveOrder()
    {
        return _state.ActiveOrder;
    }

    /// <summary>
    /// Liefert den parallelen (laufenden) Auftrag fuer den gegebenen Workshop-Typ,
    /// unabhaengig davon ob er gerade im Vordergrund bearbeitet wird (v2.0.35).
    /// </summary>
    public Order? GetParallelOrder(WorkshopType workshopType)
    {
        lock (_stateLock)
        {
            return _state.ParallelOrdersByWorkshop.GetValueOrDefault(workshopType);
        }
    }

    /// <summary>
    /// Setzt einen parallelen Auftrag als aktiv (vordergruendig) — der Spieler
    /// waehlt einen pausierten/wartenden Auftrag aus und startet/fortsetzt dessen
    /// MiniGame-Flow.
    /// </summary>
    /// <remarks>
    /// Veraltet ab v2.0.36: Bitte <see cref="SwapToParallelOrder"/> verwenden, um
    /// Pause + Resume atomar in einem Lock auszufuehren (verhindert Doppel-Tap-Races
    /// und einen kurzen Beobachtungs-Slot mit ActiveOrder=null fuer den GameLoop-Tick).
    /// </remarks>
    [Obsolete("Bitte SwapToParallelOrder() verwenden — pausiert+wechselt atomar.")]
    public void ResumeParallelOrder(WorkshopType workshopType)
    {
        lock (_stateLock)
        {
            if (_state.ParallelOrdersByWorkshop.TryGetValue(workshopType, out var order))
            {
                _state.ActiveOrder = order;
            }
        }
    }

    /// <summary>
    /// Wechselt atomar zu einem parallelen Auftrag (v2.0.36): liest
    /// ParallelOrdersByWorkshop und setzt ActiveOrder unter EINEM Lock. Vermeidet
    /// einen Beobachtungs-Slot mit ActiveOrder=null zwischen PauseActiveOrder und
    /// ResumeParallelOrder, der bei Doppel-Tap oder GameLoop-Ticks zu inkonsistentem
    /// State fuehren konnte.
    /// </summary>
    /// <returns>Den neuen ActiveOrder oder null, wenn kein paralleler Auftrag existiert.</returns>
    public Order? SwapToParallelOrder(WorkshopType workshopType)
    {
        lock (_stateLock)
        {
            if (!_state.ParallelOrdersByWorkshop.TryGetValue(workshopType, out var order))
                return null;

            // Atomarer Wechsel: ActiveOrder wird auf den parallelen Auftrag gesetzt.
            // Ein bisheriger ActiveOrder bleibt in ParallelOrdersByWorkshop erhalten
            // (wurde dort bei StartOrder gespiegelt) — Pause + Resume in einer Operation.
            _state.ActiveOrder = order;
            return order;
        }
    }

    /// <summary>
    /// Prueft ob ein neuer paralleler Auftrag gestartet werden kann.
    /// True wenn unter <see cref="GameBalanceConstants.MaxParallelOrders"/> und der Workshop
    /// noch keinen laufenden Auftrag hat.
    /// </summary>
    public bool CanStartParallelOrder(WorkshopType workshopType)
    {
        lock (_stateLock)
        {
            if (_state.ParallelOrdersByWorkshop.ContainsKey(workshopType)) return false;
            return _state.ParallelOrdersByWorkshop.Count < GameBalanceConstants.MaxParallelOrders;
        }
    }

    /// <summary>
    /// Liefert die Anzahl paralleler Auftraege (Lock-konsistent fuer UI-Bindings).
    /// </summary>
    public int ParallelOrderCount
    {
        get
        {
            lock (_stateLock)
            {
                return _state.ParallelOrdersByWorkshop.Count;
            }
        }
    }

    public void RecordMiniGameResult(MiniGameRating rating)
    {
        lock (_stateLock)
        {
            // Auftrags-spezifisch: Task-Ergebnis nur bei aktivem Auftrag (nicht bei QuickJobs)
            var order = _state.ActiveOrder;
            if (order != null)
                order.RecordTaskResult(rating);

            // Statistiken IMMER aktualisieren (auch bei QuickJobs)
            _state.Statistics.TotalMiniGamesPlayed++;

            if (rating == MiniGameRating.Perfect)
            {
                _state.Statistics.PerfectRatings++;
                _state.Statistics.PerfectStreak++;
                if (_state.Statistics.PerfectStreak > _state.Statistics.BestPerfectStreak)
                {
                    _state.Statistics.BestPerfectStreak = _state.Statistics.PerfectStreak;
                }
            }
            else
            {
                _state.Statistics.PerfectStreak = 0;
            }
        }

        // Event IMMER feuern (DailyChallengeService, WeeklyMissions, QuickJob-Validierung)
        MiniGameResultRecorded?.Invoke(this, new MiniGameResultRecordedEventArgs(rating));
    }

    /// <summary>
    /// v2.0.36: Zusaetzliche Ueberladung mit MiniGameType — fuettert die Sliding-Window-Stats
    /// fuer die personalisierte Erfolgsquote. Delegiert die generische Logik an die
    /// Single-Argument-Variante, damit alle bestehenden Listener (DailyChallengeService etc.)
    /// gleich angesprochen werden.
    /// </summary>
    public void RecordMiniGameResult(MiniGameRating rating, MiniGameType miniGameType)
    {
        lock (_stateLock)
        {
            var perf = _state.Statistics.MiniGamePerformance;
            if (!perf.TryGetValue(miniGameType, out var stats))
            {
                stats = new MiniGameStats();
                perf[miniGameType] = stats;
            }

            stats.TotalPlays++;
            stats.LastPlayedAt = DateTime.UtcNow;

            // "Erfolg" im Risk/Reward-Sinn = Good oder Perfect (Hit-Zone getroffen).
            bool wasSuccess = rating == MiniGameRating.Perfect || rating == MiniGameRating.Good;
            if (rating == MiniGameRating.Perfect)
                stats.PerfectRatings++;
            if (rating == MiniGameRating.Miss)
                stats.Misses++;

            stats.RollingResults.Add(wasSuccess);
            // Fenster auf RollingWindowSize halten — alte Eintraege wegtrimmen.
            while (stats.RollingResults.Count > MiniGameStats.RollingWindowSize)
                stats.RollingResults.RemoveAt(0);
        }

        // Generische Buchhaltung + Event ueber den bestehenden Pfad (eine Quelle der Wahrheit).
        RecordMiniGameResult(rating);
    }

    /// <summary>
    /// v2.0.36: Erfolgsquote auf Basis der RollingResults. -1 wenn weniger als 5 Plays —
    /// die UI zeigt dann „~?%" statt konkreter Zahl.
    /// </summary>
    public double GetMiniGameSuccessRate(MiniGameType miniGameType)
    {
        lock (_stateLock)
        {
            if (!_state.Statistics.MiniGamePerformance.TryGetValue(miniGameType, out var stats))
                return -1d;
            if (stats.RollingResults.Count < 5)
                return -1d;
            int successes = 0;
            for (int i = 0; i < stats.RollingResults.Count; i++)
                if (stats.RollingResults[i]) successes++;
            return (double)successes / stats.RollingResults.Count;
        }
    }

    /// <summary>
    /// v2.0.37: Vergleicht den uebergebenen Tier-Snapshot mit dem aktuellen Reputation-Tier.
    /// Bei Aenderung wird <see cref="ReputationTierChanged"/> gefeuert. Aufruf NICHT innerhalb
    /// eines Locks — Subscriber laufen auf dem aufrufenden Thread und koennen lange Operationen
    /// haben.
    ///
    /// Audit-Fix L5: Nutzt jetzt RecomputeTier mit Hysterese (3-Punkte-Buffer) statt direkter
    /// Score-zu-Tier-Berechnung. Verhindert UI-Flackern an Tier-Boundaries (z.B. bei +1/-1
    /// Score-Schwankungen durch Stammkunden-Reputation und Decay).
    /// </summary>
    internal void RaiseReputationTierChangedIfNeeded(CustomerReputationTier oldTier)
    {
        // Hysterese-Berechnung — aktualisiert state.Reputation.CurrentTier intern.
        if (_state.Reputation.RecomputeTier(out _))
        {
            ReputationTierChanged?.Invoke(this, new ReputationTierChangedEventArgs(oldTier, _state.Reputation.CurrentTier));
        }
    }

    public void PauseAllLiveOrders()
    {
        var now = DateTime.UtcNow;
        lock (_stateLock)
        {
            for (int i = 0; i < _state.AvailableOrders.Count; i++)
            {
                var o = _state.AvailableOrders[i];
                if (o.IsLive && o.PausedAt == null)
                    o.PausedAt = now;
            }
        }
    }

    public void ResumeAllLiveOrders()
    {
        var now = DateTime.UtcNow;
        var maxPause = TimeSpan.FromMinutes(5);
        lock (_stateLock)
        {
            for (int i = 0; i < _state.AvailableOrders.Count; i++)
            {
                var o = _state.AvailableOrders[i];
                if (o.PausedAt is { } pausedAt)
                {
                    var pauseDuration = now - pausedAt;
                    if (pauseDuration < TimeSpan.Zero) pauseDuration = TimeSpan.Zero;
                    if (pauseDuration > maxPause) pauseDuration = maxPause;
                    o.AccumulatedPauseDuration += pauseDuration;
                    o.PausedAt = null;
                }
            }
        }
    }

    public decimal GetOrderRewardMultiplier(Order order)
    {
        lock (_stateLock)
        {
            return CalculateOrderRewardMultiplierUnlocked(order);
        }
    }

    /// <summary>
    /// Interne Berechnung ohne Lock - nur innerhalb bestehender lock(_stateLock)-Blöcke aufrufen.
    /// </summary>
    private decimal CalculateOrderRewardMultiplierUnlocked(Order order)
    {
        decimal multiplier = 1m;

        // Research-RewardMultiplier (For-Schleife statt LINQ Where+Sum)
        decimal researchRewardBonus = 0m;
        for (int i = 0; i < _state.Researches.Count; i++)
        {
            var r = _state.Researches[i];
            if (r.IsResearched && r.Effect.RewardMultiplier > 0)
                researchRewardBonus += r.Effect.RewardMultiplier;
        }
        if (researchRewardBonus > 0)
            multiplier *= (1m + researchRewardBonus);

        // VehicleFleet-Gebäude: Auftragsbelohnungs-Bonus
        var vehicleFleet = _state.GetBuilding(BuildingType.VehicleFleet);
        if (vehicleFleet != null && vehicleFleet.OrderRewardBonus > 0)
            multiplier *= (1m + vehicleFleet.OrderRewardBonus);

        // Reputation-Multiplikator: Höhere Reputation → bessere Belohnungen
        multiplier *= _state.Reputation.ReputationMultiplier;

        // Event-RewardMultiplier (HighDemand 1.5x, EconomicDownturn 0.7x)
        var activeEvent = _state.ActiveEvent;
        if (activeEvent?.IsActive == true && activeEvent.Effect.RewardMultiplier != 1.0m)
        {
            // AffectedWorkshop: Nur anwenden wenn Workshop-Typ passt oder kein spezifischer Typ gesetzt
            if (activeEvent.Effect.AffectedWorkshop == null ||
                activeEvent.Effect.AffectedWorkshop == order.WorkshopType)
            {
                multiplier *= activeEvent.Effect.RewardMultiplier;
            }
        }

        // Stammkunden-Bonus
        if (order.IsRegularCustomerOrder)
        {
            RegularCustomer? customer = null;
            for (int i = 0; i < _state.Reputation.RegularCustomers.Count; i++)
            {
                if (_state.Reputation.RegularCustomers[i].Id == order.CustomerId)
                {
                    customer = _state.Reputation.RegularCustomers[i];
                    break;
                }
            }
            if (customer != null)
                multiplier *= customer.BonusMultiplier;
        }

        // Prestige-Shop: Auftragsbelohnungs-Bonus (wiederholbar pp_order_reward_rep)
        decimal shopOrderBonus = GetPrestigeShopOrderRewardBonus();
        if (shopOrderBonus > 0)
            multiplier *= (1m + shopOrderBonus);

        // Soft-Cap: Diminishing Returns auf den Gesamt-Multiplikator
        // Verhindert Multiplikator-Explosion bei voll ausgebauten Spielern
        decimal cap = GameBalanceConstants.OrderRewardMultiplierSoftCap;
        if (multiplier > cap)
            multiplier = cap + (decimal)Math.Sqrt((double)(multiplier - cap));

        return multiplier;
    }

    /// <summary>
    /// Gibt den gecachten Auftragsbelohnungs-Bonus zurück (refresht bei Bedarf).
    /// Cap bei +100%.
    /// </summary>
    private decimal GetPrestigeShopOrderRewardBonus()
    {
        RefreshPrestigeBonusCacheIfNeeded();
        return Math.Min(_cachedOrderRewardBonus, 1.0m);
    }

    public void CompleteActiveOrder()
    {
        Order? order;
        decimal moneyReward;
        int xpReward;
        MiniGameRating avgRating;

        lock (_stateLock)
        {
            order = _state.ActiveOrder;
            if (order == null || !order.IsCompleted) return;

            // Prestige-Multiplikator ist bereits in BaseReward enthalten
            // (via NetIncomePerSecond in OrderGeneratorService), daher NICHT nochmal anwenden
            moneyReward = order.FinalReward * CalculateOrderRewardMultiplierUnlocked(order);
            xpReward = order.FinalXp;

            // V7 (Phase 2): Material-Offer-Bonus — gilt VOR Combo/Doppel-Boosts.
            if (order.MaterialOfferAccepted && order.MaterialOfferBonusMultiplier > 0)
            {
                decimal bonusFactor = 1m + (decimal)order.MaterialOfferBonusMultiplier;
                moneyReward *= bonusFactor;
                xpReward = (int)(xpReward * bonusFactor);

                // Reservierte Materialien konsumieren (atomar im selben Lock).
                ConsumeOrderMaterialReservation(order);
            }

            // Combo-Multiplikator (PaintingGame)
            if (order.ComboMultiplier > 1m)
            {
                moneyReward *= order.ComboMultiplier;
                xpReward = (int)(xpReward * order.ComboMultiplier);
            }

            // Rewarded-Ad-Verdopplung
            if (order.IsScoreDoubled)
            {
                moneyReward *= 2m;
                xpReward *= 2;
            }

            var workshop = GetWorkshop(order.WorkshopType);
            if (workshop != null)
            {
                workshop.TotalEarned += moneyReward;
                workshop.OrdersCompleted++;
            }

            _state.Statistics.TotalOrdersCompleted++;

            if (order.TaskResults.Count > 0)
            {
                int ratingSum = 0;
                for (int i = 0; i < order.TaskResults.Count; i++)
                    ratingSum += (int)order.TaskResults[i];
                avgRating = (MiniGameRating)(int)Math.Round((double)ratingSum / order.TaskResults.Count);
            }
            else
            {
                avgRating = MiniGameRating.Ok;
            }

            // Reputation-System: Bewertung basierend auf MiniGame-Leistung
            int stars = avgRating switch
            {
                MiniGameRating.Perfect => 5,
                MiniGameRating.Good => 4,
                MiniGameRating.Ok => 3,
                _ => 2
            };
            // Research ReputationBonus direkt aus State berechnen (GameStateService hat keinen IResearchService)
            decimal reputationBonus = 0m;
            for (int ri = 0; ri < _state.Researches.Count; ri++)
            {
                if (_state.Researches[ri].IsResearched && _state.Researches[ri].Effect?.ReputationBonus > 0)
                    reputationBonus += _state.Researches[ri].Effect!.ReputationBonus;
            }
            // v2.0.37: Tier-Snapshot vor Aenderung — Event triggert Confetti/FloatingText
            // im MainViewModel, wenn der Spieler in einen neuen Tier aufsteigt.
            var tierBeforeRating = _state.Reputation.CurrentTier;
            _state.Reputation.AddRating(stars, reputationBonus);
            RaiseReputationTierChangedIfNeeded(tierBeforeRating);

            // Stammkunden-Tracking bei Perfect Rating
            if (avgRating == MiniGameRating.Perfect && !string.IsNullOrEmpty(order.CustomerName))
            {
                RegularCustomer? existingCustomer = null;
                for (int i = 0; i < _state.Reputation.RegularCustomers.Count; i++)
                {
                    if (_state.Reputation.RegularCustomers[i].Name == order.CustomerName)
                    {
                        existingCustomer = _state.Reputation.RegularCustomers[i];
                        break;
                    }
                }
                if (existingCustomer != null)
                {
                    existingCustomer.PerfectOrderCount++;
                    existingCustomer.LastOrder = DateTime.UtcNow;
                    // BonusMultiplier: 1.1 Basis + 0.02 pro Perfect über 5 (Cap 1.5)
                    if (existingCustomer.PerfectOrderCount > 5)
                    {
                        existingCustomer.BonusMultiplier = Math.Min(1.5m,
                            1.1m + (existingCustomer.PerfectOrderCount - 5) * 0.02m);
                    }
                }
                else
                {
                    // Neuen Stammkunden anlegen
                    _state.Reputation.RegularCustomers.Add(new RegularCustomer
                    {
                        Name = order.CustomerName,
                        PreferredWorkshop = order.WorkshopType,
                        PerfectOrderCount = 1,
                        LastOrder = DateTime.UtcNow,
                        AvatarSeed = order.CustomerAvatarSeed
                    });
                    // Max 20 Stammkunden (älteste entfernen)
                    while (_state.Reputation.RegularCustomers.Count > 20)
                        _state.Reputation.RegularCustomers.RemoveAt(0);
                }
            }

            _state.ActiveOrder = null;
            // v2.0.35 Feature A: Auftrag aus Parallel-Slot entfernen — Workshop wieder frei.
            _state.ParallelOrdersByWorkshop.Remove(order.WorkshopType);
        }

        // Grant rewards (these have their own locks)
        AddMoney(moneyReward);
        AddXp(xpReward);

        OrderCompleted?.Invoke(this, new OrderCompletedEventArgs(
            order, moneyReward, xpReward, avgRating));
    }

    // ===================================================================
    // MINI-GAME AUTO-COMPLETE
    // ===================================================================

    public void RecordPerfectRating(MiniGameType type)
    {
        int newLifetimeCount;
        lock (_stateLock)
        {
            int key = (int)type;

            // Auto-Complete-Counter (wird bei Ascension resettet).
            if (_state.PerfectRatingCounts.TryGetValue(key, out int count))
                _state.PerfectRatingCounts[key] = count + 1;
            else
                _state.PerfectRatingCounts[key] = 1;

            // Mastery-Lifetime-Counter (v2.0.36, NICHT bei Ascension/Prestige resetten).
            // Defensive Init fuer alte SaveGames ohne dieses Dictionary.
            _state.LifetimePerfectRatingCounts ??= new Dictionary<int, int>();
            if (_state.LifetimePerfectRatingCounts.TryGetValue(key, out int lifeCount))
                _state.LifetimePerfectRatingCounts[key] = lifeCount + 1;
            else
                _state.LifetimePerfectRatingCounts[key] = 1;

            newLifetimeCount = _state.LifetimePerfectRatingCounts[key];
        }

        // Event AUSSERHALB des Locks feuern — MasteryService subscribed darauf und
        // ruft AddGoldenScrews() auf (nimmt seinen eigenen Lock, Reentrancy unproblematisch).
        PerfectRatingIncremented?.Invoke(this, new PerfectRatingIncrementedEventArgs(type, newLifetimeCount));
    }

    public bool CanAutoComplete(MiniGameType type, bool isPremium)
    {
        // Differenzierte Schwellen: Puzzle/Memory-Spiele sind schwerer → weniger Perfects nötig
        int baseThreshold = type switch
        {
            MiniGameType.PipePuzzle or MiniGameType.Blueprint or MiniGameType.InventGame
                or MiniGameType.DesignPuzzle or MiniGameType.Inspection => 20,
            _ => 30
        };
        int threshold = isPremium ? baseThreshold / 2 : baseThreshold;

        lock (_stateLock)
        {
            return _state.PerfectRatingCounts.TryGetValue((int)type, out int count) && count >= threshold;
        }
    }

    public void CancelActiveOrder()
    {
        lock (_stateLock)
        {
            if (_state.ActiveOrder == null) return;

            var order = _state.ActiveOrder;
            order.CurrentTaskIndex = 0;
            order.TaskResults.Clear();

            // V7 (Phase 2): Beim Abbruch reservierte Materialien freigeben — kein Verbrauch.
            // Material-Offer-Status wird zurueckgesetzt damit der Spieler beim erneuten Annehmen
            // bewusst entscheiden kann.
            if (order.MaterialOfferAccepted)
            {
                ReleaseOrderMaterialReservation(order);
                order.MaterialOfferAccepted = false;
            }

            _state.AvailableOrders.Add(order);
            _state.ActiveOrder = null;
            // v2.0.35 Feature A: Workshop auch aus Parallel-Slot entfernen.
            _state.ParallelOrdersByWorkshop.Remove(order.WorkshopType);
        }
    }

    /// <summary>
    /// V7 (Phase 2): Konsumiert reservierte Materialien eines Auftrags (MUSS unter _stateLock laufen).
    /// Subtrahiert die Mengen sowohl aus CraftingInventory als auch aus ReservedInventory.
    /// </summary>
    private void ConsumeOrderMaterialReservation(Order order)
    {
        if (order.MaterialOffer == null) return;
        foreach (var (productId, required) in order.MaterialOffer)
        {
            int current = _state.CraftingInventory.GetValueOrDefault(productId, 0);
            int reserved = _state.ReservedInventory.GetValueOrDefault(productId, 0);

            int consume = Math.Min(required, current);
            int releaseReserved = Math.Min(required, reserved);

            _state.CraftingInventory[productId] = current - consume;
            if (_state.CraftingInventory[productId] <= 0)
                _state.CraftingInventory.Remove(productId);

            _state.ReservedInventory[productId] = reserved - releaseReserved;
            if (_state.ReservedInventory[productId] <= 0)
                _state.ReservedInventory.Remove(productId);
        }
    }

    /// <summary>
    /// V7 (Phase 2): Gibt reservierte Materialien zurueck (MUSS unter _stateLock laufen).
    /// Verringert NUR ReservedInventory — Material bleibt im CraftingInventory verfuegbar.
    /// </summary>
    private void ReleaseOrderMaterialReservation(Order order)
    {
        if (order.MaterialOffer == null) return;
        foreach (var (productId, required) in order.MaterialOffer)
        {
            int reserved = _state.ReservedInventory.GetValueOrDefault(productId, 0);
            int releaseAmount = Math.Min(required, reserved);
            _state.ReservedInventory[productId] = reserved - releaseAmount;
            if (_state.ReservedInventory[productId] <= 0)
                _state.ReservedInventory.Remove(productId);
        }
    }

    /// <summary>
    /// Pausiert den aktuellen Auftrag (v2.0.35 Feature A): setzt ActiveOrder zurueck,
    /// aber der Auftrag bleibt als paralleler Auftrag gespeichert. Spieler kann spaeter
    /// via <see cref="ResumeParallelOrder"/> zurueckkehren.
    /// </summary>
    public void PauseActiveOrder()
    {
        lock (_stateLock)
        {
            if (_state.ActiveOrder == null) return;
            // ActiveOrder bleibt in ParallelOrdersByWorkshop — nur Vordergrund wird aufgehoben
            _state.ActiveOrder = null;
        }
    }

    // ===================================================================
    // LIEFERAUFTRÄGE (MaterialOrder)
    // ===================================================================

    public decimal CompleteMaterialOrder(Order order)
    {
        if (order.OrderType != OrderType.MaterialOrder || order.RequiredMaterials == null)
            return 0m;

        decimal reward;
        int xpReward;

        lock (_stateLock)
        {
            // Prüfen ob alle Items vorhanden
            foreach (var (productId, required) in order.RequiredMaterials)
            {
                int available = _state.CraftingInventory.GetValueOrDefault(productId, 0);
                if (available < required) return 0m;
            }

            // Items abziehen
            foreach (var (productId, required) in order.RequiredMaterials)
            {
                _state.CraftingInventory[productId] -= required;
                if (_state.CraftingInventory[productId] <= 0)
                    _state.CraftingInventory.Remove(productId);
            }

            // Belohnung berechnen (innerhalb Lock, da CalculateOrderRewardMultiplierUnlocked State liest)
            reward = order.EstimatedReward * CalculateOrderRewardMultiplierUnlocked(order);
            // MaterialOrders haben keine TaskResults → XP direkt aus BaseXp + Difficulty + OrderType
            xpReward = (int)(order.BaseXp * order.Difficulty.GetXpMultiplier() * OrderType.MaterialOrder.GetXpMultiplier());

            // Statistiken
            _state.Statistics.TotalOrdersCompleted++;
            _state.Statistics.MaterialOrdersCompletedToday++;
            _state.Statistics.TotalMaterialOrdersCompleted++;
            var workshop = GetWorkshop(order.WorkshopType);
            if (workshop != null)
            {
                workshop.TotalEarned += reward;
                workshop.OrdersCompleted++;
            }

            // Order aus AvailableOrders entfernen
            _state.AvailableOrders.Remove(order);
        }

        // Geld + XP gutschreiben + Events AUSSERHALB des Locks
        // (AddMoney/AddXp nehmen eigene Locks und feuern Events die Event-Handler aufrufen)
        AddMoney(reward);
        AddXp(xpReward);

        OrderCompleted?.Invoke(this, new OrderCompletedEventArgs(order, reward, xpReward, MiniGameRating.Good));

        return reward;
    }

    // ===================================================================
    // LOCK-ZUGRIFF (für OperationServices)
    // ===================================================================

    /// <summary>
    /// Führt eine Aktion unter dem State-Lock aus.
    /// </summary>
    public void ExecuteWithLock(Action action)
    {
        lock (_stateLock)
        {
            action();
        }
    }

    /// <summary>
    /// Führt eine Funktion unter dem State-Lock aus und gibt das Ergebnis zurück.
    /// </summary>
    public T ExecuteWithLock<T>(Func<T> func)
    {
        lock (_stateLock)
        {
            return func();
        }
    }

}
