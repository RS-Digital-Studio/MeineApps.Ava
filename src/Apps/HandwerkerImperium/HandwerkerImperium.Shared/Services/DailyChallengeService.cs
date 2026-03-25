using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet taegliche Herausforderungen mit Belohnungen.
/// Subscribes auf GameState-Events fuer automatisches Tracking.
/// </summary>
public sealed class DailyChallengeService : IDailyChallengeService, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private readonly IVipService _vipService;
    private readonly IWorkerService _workerService;
    private bool _disposed;

    private static readonly DailyChallengeType[] AllChallengeTypes = Enum.GetValues<DailyChallengeType>();

    public event EventHandler? ChallengeProgressChanged;

    public decimal AllCompletedBonusAmount => 500m;

    /// <summary>
    /// Tier-basierter Alle-fertig-Bonus (GS). Tiers 0-4: 6, Tiers 5-8: 8/10/12/15.
    /// </summary>
    public int AllCompletedBonusScrews
    {
        get
        {
            int tier = GetTier(_gameStateService.State.PlayerLevel);
            return tier switch
            {
                <= 4 => 6,
                5 => 8,
                6 => 10,
                7 => 12,
                _ => 15
            };
        }
    }

    public DailyChallengeService(
        IGameStateService gameStateService,
        ILocalizationService localizationService,
        IVipService vipService,
        IWorkerService workerService)
    {
        _gameStateService = gameStateService;
        _localizationService = localizationService;
        _vipService = vipService;
        _workerService = workerService;

        // Event-Subscriptions fuer automatisches Tracking
        _gameStateService.OrderCompleted += OnOrderCompleted;
        _gameStateService.MoneyChanged += OnMoneyChanged;
        _gameStateService.WorkshopUpgraded += OnWorkshopUpgraded;
        _gameStateService.WorkerHired += OnWorkerHired;
        _gameStateService.MiniGameResultRecorded += OnMiniGameResultRecorded;

        // Neues Event fuer Worker-Training-Challenge
        _workerService.WorkerLevelUp += OnWorkerLevelUp;
    }

    public bool AreAllCompleted
    {
        get
        {
            var challenges = _gameStateService.State.DailyChallengeState.Challenges;
            if (challenges.Count == 0) return false;
            // For-Schleife statt LINQ .All()
            for (int i = 0; i < challenges.Count; i++)
                if (!challenges[i].IsCompleted) return false;
            return true;
        }
    }

    public bool HasUnclaimedRewards
    {
        get
        {
            var state = _gameStateService.State.DailyChallengeState;
            var challenges = state.Challenges;
            // For-Schleife statt LINQ .Any()
            for (int i = 0; i < challenges.Count; i++)
                if (challenges[i].IsCompleted && !challenges[i].IsClaimed) return true;
            return AreAllCompleted && !state.AllCompletedBonusClaimed;
        }
    }

    public DailyChallengeState GetState()
    {
        var state = _gameStateService.State.DailyChallengeState;
        foreach (var challenge in state.Challenges)
        {
            PopulateDisplayFields(challenge);
        }
        return state;
    }

    public void CheckAndResetIfNewDay()
    {
        var state = _gameStateService.State.DailyChallengeState;
        var today = DateTime.UtcNow.Date;

        // Zeitmanipulations-Schutz: Wenn LastResetDate in der Zukunft liegt, nicht resetten
        if (state.LastResetDate.Date > today)
            return;

        // UTC fuer konsistente Tagesgrenze
        if (today > state.LastResetDate.Date)
        {
            GenerateDailyChallenges();
        }
    }

    public bool ClaimReward(string challengeId)
    {
        var challenge = _gameStateService.State.DailyChallengeState.Challenges
            .FirstOrDefault(c => c.Id == challengeId);

        if (challenge == null || !challenge.IsCompleted || challenge.IsClaimed)
            return false;

        challenge.IsClaimed = true;
        _gameStateService.AddMoney(challenge.MoneyReward);
        _gameStateService.AddXp(challenge.XpReward);
        if (challenge.GoldenScrewReward > 0)
            _gameStateService.AddGoldenScrews(challenge.GoldenScrewReward);
        _gameStateService.MarkDirty();
        return true;
    }

    public bool RetryChallenge(string challengeId)
    {
        var challenge = _gameStateService.State.DailyChallengeState.Challenges
            .FirstOrDefault(c => c.Id == challengeId);

        if (challenge == null || challenge.IsCompleted || challenge.HasRetriedWithAd || challenge.CurrentValue == 0)
            return false;

        challenge.CurrentValue = 0;
        challenge.IsCompleted = false;
        challenge.HasRetriedWithAd = true;
        _gameStateService.MarkDirty();
        return true;
    }

    public bool ClaimAllCompletedBonus()
    {
        var state = _gameStateService.State.DailyChallengeState;
        if (!AreAllCompleted || state.AllCompletedBonusClaimed)
            return false;

        // Zuerst alle unclaimten Einzelbelohnungen einsammeln (For-Schleife statt LINQ)
        for (int i = 0; i < state.Challenges.Count; i++)
        {
            var challenge = state.Challenges[i];
            if (!challenge.IsCompleted || challenge.IsClaimed) continue;
            challenge.IsClaimed = true;
            _gameStateService.AddMoney(challenge.MoneyReward);
            _gameStateService.AddXp(challenge.XpReward);
            if (challenge.GoldenScrewReward > 0)
                _gameStateService.AddGoldenScrews(challenge.GoldenScrewReward);
        }

        // Bonus
        state.AllCompletedBonusClaimed = true;
        _gameStateService.AddMoney(AllCompletedBonusAmount);
        _gameStateService.AddGoldenScrews(AllCompletedBonusScrews);
        _gameStateService.MarkDirty();
        return true;
    }

    private void GenerateDailyChallenges()
    {
        var state = _gameStateService.State.DailyChallengeState;
        var level = _gameStateService.State.PlayerLevel;

        state.Challenges.Clear();
        state.AllCompletedBonusClaimed = false;
        state.LastResetDate = DateTime.UtcNow;

        // Tier bestimmen (wird auch fuer Typ-Filterung benoetigt)
        int tier = GetTier(level);

        // VIP-Extra: Silver+ bekommt +1 Challenge pro Tag
        int challengeCount = 3 + _vipService.ExtraDailyChallenges;

        // Verfuegbare Typen nach Tier filtern (neue Typen nur bei passenden Tiers)
        var availableTypes = GetAvailableTypesForTier(tier);
        for (int i = 0; i < challengeCount && availableTypes.Count > 0; i++)
        {
            var idx = Random.Shared.Next(availableTypes.Count);
            var type = availableTypes[idx];
            availableTypes.RemoveAt(idx);

            state.Challenges.Add(CreateChallenge(type, level));
        }

        _gameStateService.MarkDirty();
    }

    /// <summary>
    /// Gibt die fuer den Tier verfuegbaren Challenge-Typen zurueck.
    /// Neue Typen werden erst ab bestimmten Tiers freigeschaltet.
    /// </summary>
    private static List<DailyChallengeType> GetAvailableTypesForTier(int tier)
    {
        var types = new List<DailyChallengeType>
        {
            // Basis-Typen (immer verfuegbar)
            DailyChallengeType.CompleteOrders,
            DailyChallengeType.EarnMoney,
            DailyChallengeType.UpgradeWorkshop,
            DailyChallengeType.HireWorker,
            DailyChallengeType.CompleteQuickJob,
            DailyChallengeType.PlayMiniGames,
            DailyChallengeType.AchieveMinigameScore
        };

        // Tier 5+: Arbeiter trainieren, Crafting abschliessen
        if (tier >= 5)
        {
            types.Add(DailyChallengeType.TrainWorker);
            types.Add(DailyChallengeType.CompleteCrafting);
        }

        // Tier 6+: Perfekte Serie erreichen
        if (tier >= 6)
            types.Add(DailyChallengeType.AchievePerfectStreak);

        // Tier 7+: Werkstatt-Level erreichen
        if (tier >= 7)
            types.Add(DailyChallengeType.ReachWorkshopLevel);

        // Tier 5+: Auto-Produktion und Verkauf (benötigt Workshop Lv50)
        if (tier >= 5)
        {
            types.Add(DailyChallengeType.ProduceItems);
            types.Add(DailyChallengeType.SellItems);
        }

        // Tier 6+: Lieferaufträge
        if (tier >= 6)
            types.Add(DailyChallengeType.CompleteMaterialOrder);

        return types;
    }

    /// <summary>
    /// Berechnet den Tier basierend auf dem Spieler-Level.
    /// Tiers 0-4 sind bestehend, Tiers 5-8 sind neue Late-Game-Stufen.
    /// </summary>
    private static int GetTier(int level) => level switch
    {
        <= 5 => 0,
        <= 15 => 1,
        <= 30 => 2,
        <= 50 => 3,
        <= 100 => 4,
        <= 300 => 5,
        <= 500 => 6,
        <= 750 => 7,
        _ => 8
    };

    private DailyChallenge CreateChallenge(DailyChallengeType type, int level)
    {
        int tier = GetTier(level);

        // Basis-Multiplikator: Belohnung skaliert mit Level
        // ~10 Minuten Netto-Einkommen als Basis, mindestens Level * 30
        var netPerSecond = Math.Max(0m, _gameStateService.State.NetIncomePerSecond);
        var incomeBase = Math.Max(level * 30m, netPerSecond * 600m);

        var challenge = new DailyChallenge { Type = type };

        switch (type)
        {
            case DailyChallengeType.CompleteOrders:
                challenge.TargetValue = tier switch { 0 => 2, 1 => 3, 2 => 4, 3 => 5, 4 => 5, 5 => 6, 6 => 7, 7 => 8, _ => 10 };
                challenge.MoneyReward = Math.Round(incomeBase * 0.8m, 0);
                challenge.XpReward = 20 + level * 2;
                break;

            case DailyChallengeType.EarnMoney:
                challenge.TargetValue = (long)Math.Max(200, incomeBase * 0.5m);
                challenge.MoneyReward = Math.Round(incomeBase * 0.6m, 0);
                challenge.XpReward = 15 + level * 2;
                break;

            case DailyChallengeType.UpgradeWorkshop:
                challenge.TargetValue = tier switch { 0 => 1, 1 => 2, 2 => 2, 3 => 3, 4 => 3, 5 => 4, 6 => 5, 7 => 6, _ => 8 };
                challenge.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                challenge.XpReward = 25 + level * 2;
                break;

            case DailyChallengeType.HireWorker:
                challenge.TargetValue = tier >= 6 ? 2 : 1;
                challenge.MoneyReward = Math.Round(incomeBase * 0.7m, 0);
                challenge.XpReward = 20 + level * 2;
                break;

            case DailyChallengeType.CompleteQuickJob:
                challenge.TargetValue = tier switch { 0 => 1, 1 => 2, 2 => 3, 3 => 4, 4 => 4, 5 => 5, 6 => 6, 7 => 7, _ => 8 };
                challenge.MoneyReward = Math.Round(incomeBase * 0.5m, 0);
                challenge.XpReward = 15 + level * 2;
                break;

            case DailyChallengeType.PlayMiniGames:
                challenge.TargetValue = tier switch { 0 => 3, 1 => 4, 2 => 5, 3 => 7, 4 => 7, 5 => 8, 6 => 10, 7 => 12, _ => 15 };
                challenge.MoneyReward = Math.Round(incomeBase * 0.7m, 0);
                challenge.XpReward = 20 + level * 2;
                break;

            case DailyChallengeType.AchieveMinigameScore:
                challenge.TargetValue = tier switch { 0 => 70, 1 => 75, 2 => 80, 3 => 90, _ => 90 };
                challenge.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                challenge.XpReward = 25 + level * 2;
                break;

            // Neue Typen (Tier 5+)
            case DailyChallengeType.TrainWorker:
                challenge.TargetValue = tier switch { 5 => 2, 6 => 3, 7 => 4, _ => 5 };
                challenge.MoneyReward = Math.Round(incomeBase * 0.9m, 0);
                challenge.XpReward = 25 + level * 2;
                break;

            case DailyChallengeType.CompleteCrafting:
                challenge.TargetValue = tier switch { 5 => 1, 6 => 2, 7 => 3, _ => 4 };
                challenge.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                challenge.XpReward = 30 + level * 2;
                break;

            // Tier 6+
            case DailyChallengeType.AchievePerfectStreak:
                challenge.TargetValue = tier switch { 6 => 3, 7 => 5, _ => 7 };
                challenge.MoneyReward = Math.Round(incomeBase * 1.2m, 0);
                challenge.XpReward = 35 + level * 2;
                break;

            // Tier 7+: Zielwert abhaengig vom aktuellen hoechsten Workshop-Level
            case DailyChallengeType.ReachWorkshopLevel:
                int highestLevel = GetHighestWorkshopLevel();
                int increment = tier >= 8 ? 50 : tier >= 7 ? 10 : 10;
                challenge.TargetValue = highestLevel + increment;
                challenge.MoneyReward = Math.Round(incomeBase * 1.5m, 0);
                challenge.XpReward = 40 + level * 2;
                break;

            // Tier 5+: Items auto-produzieren
            case DailyChallengeType.ProduceItems:
                challenge.TargetValue = tier switch { 5 => 20, 6 => 50, 7 => 100, _ => 200 };
                challenge.MoneyReward = Math.Round(incomeBase * 0.8m, 0);
                challenge.XpReward = 25 + level * 2;
                break;

            // Tier 5+: Items verkaufen
            case DailyChallengeType.SellItems:
                challenge.TargetValue = tier switch { 5 => 10, 6 => 25, 7 => 50, _ => 100 };
                challenge.MoneyReward = Math.Round(incomeBase * 0.9m, 0);
                challenge.XpReward = 25 + level * 2;
                break;

            // Tier 6+: Lieferauftrag abschliessen
            case DailyChallengeType.CompleteMaterialOrder:
                challenge.TargetValue = tier switch { 6 => 1, 7 => 2, _ => 3 };
                challenge.MoneyReward = Math.Round(incomeBase * 1.3m, 0);
                challenge.XpReward = 35 + level * 2;
                break;
        }

        // Goldschrauben-Belohnung nach Tier (BAL-9: Tiers 0-4 bleiben 1-2)
        challenge.GoldenScrewReward = tier switch
        {
            <= 4 => Math.Min(1 + tier, 2),
            5 => 3,
            6 => 4,
            7 => 5,
            _ => 6
        };

        return challenge;
    }

    /// <summary>
    /// Ermittelt das hoechste Workshop-Level aller freigeschalteten Werkstaetten.
    /// </summary>
    private int GetHighestWorkshopLevel()
    {
        int max = 1;
        var workshops = _gameStateService.State.Workshops;
        for (int i = 0; i < workshops.Count; i++)
        {
            if (workshops[i].Level > max)
                max = workshops[i].Level;
        }
        return max;
    }

    private void PopulateDisplayFields(DailyChallenge challenge)
    {
        // Lokalisierte Beschreibung ohne englische Fallback-Strings
        // GetString gibt den Key zurueck wenn kein Eintrag gefunden wird
        challenge.DisplayDescription = challenge.Type switch
        {
            DailyChallengeType.CompleteOrders =>
                string.Format(_localizationService.GetString("ChallengeCompleteOrders"), challenge.TargetValue),
            DailyChallengeType.EarnMoney =>
                string.Format(_localizationService.GetString("ChallengeEarnMoney"), MoneyFormatter.FormatCompact(challenge.TargetValue)),
            DailyChallengeType.UpgradeWorkshop =>
                string.Format(_localizationService.GetString("ChallengeUpgradeWorkshop"), challenge.TargetValue),
            DailyChallengeType.HireWorker =>
                _localizationService.GetString("ChallengeHireWorker"),
            DailyChallengeType.CompleteQuickJob =>
                string.Format(_localizationService.GetString("ChallengeCompleteQuickJob"), challenge.TargetValue),
            DailyChallengeType.PlayMiniGames =>
                string.Format(_localizationService.GetString("ChallengePlayMiniGames"), challenge.TargetValue),
            DailyChallengeType.AchieveMinigameScore =>
                string.Format(_localizationService.GetString("ChallengeAchieveScore"), challenge.TargetValue),

            // Neue Typen
            DailyChallengeType.TrainWorker =>
                string.Format(
                    _localizationService.GetString("ChallengeTrainWorker") ?? "Bilde {0} Arbeiter aus",
                    challenge.TargetValue),
            DailyChallengeType.CompleteCrafting =>
                string.Format(
                    _localizationService.GetString("ChallengeCompleteCrafting") ?? "Stelle {0} Gegenstände her",
                    challenge.TargetValue),
            DailyChallengeType.AchievePerfectStreak =>
                string.Format(
                    _localizationService.GetString("ChallengePerfectStreak") ?? "Erreiche {0} perfekte Bewertungen in Folge",
                    challenge.TargetValue),
            DailyChallengeType.ReachWorkshopLevel =>
                string.Format(
                    _localizationService.GetString("ChallengeReachWorkshopLevel") ?? "Erreiche Werkstatt-Level {0}",
                    challenge.TargetValue),
            DailyChallengeType.ProduceItems =>
                string.Format(
                    _localizationService.GetString("ChallengeProduceItems") ?? "Produziere {0} Items automatisch",
                    challenge.TargetValue),
            DailyChallengeType.SellItems =>
                string.Format(
                    _localizationService.GetString("ChallengeSellItems") ?? "Verkaufe {0} Items",
                    challenge.TargetValue),
            DailyChallengeType.CompleteMaterialOrder =>
                string.Format(
                    _localizationService.GetString("ChallengeCompleteMaterialOrder") ?? "Schließe {0} Lieferaufträge ab",
                    challenge.TargetValue),

            _ => ""
        };

        challenge.RewardDisplay = challenge.GoldenScrewReward > 0
            ? $"{MoneyFormatter.FormatCompact(challenge.MoneyReward)} + {challenge.XpReward} XP + {challenge.GoldenScrewReward} GS"
            : $"{MoneyFormatter.FormatCompact(challenge.MoneyReward)} + {challenge.XpReward} XP";
    }

    /// <summary>
    /// Aktualisiert den Fortschritt einer bestimmten Challenge-Art.
    /// </summary>
    private void IncrementChallenge(DailyChallengeType type, long amount = 1)
    {
        var challenges = _gameStateService.State.DailyChallengeState.Challenges;
        // For-Schleife statt LINQ .Where() (wird bei Events haeufig aufgerufen)
        for (int i = 0; i < challenges.Count; i++)
        {
            var challenge = challenges[i];
            if (challenge.Type != type || challenge.IsCompleted) continue;
            challenge.CurrentValue += amount;
            if (challenge.CurrentValue >= challenge.TargetValue)
            {
                challenge.IsCompleted = true;
            }
        }
        _gameStateService.MarkDirty();
        // UI sofort ueber Fortschrittsaenderung benachrichtigen
        ChallengeProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Setzt den Fortschritt einer Challenge auf den Maximalwert (fuer Score-basierte).
    /// </summary>
    private void SetChallengeMax(DailyChallengeType type, long value)
    {
        var challenges = _gameStateService.State.DailyChallengeState.Challenges;
        // For-Schleife statt LINQ .Where()
        for (int i = 0; i < challenges.Count; i++)
        {
            var challenge = challenges[i];
            if (challenge.Type != type || challenge.IsCompleted) continue;
            if (value > challenge.CurrentValue)
                challenge.CurrentValue = value;
            if (challenge.CurrentValue >= challenge.TargetValue)
                challenge.IsCompleted = true;
        }
        _gameStateService.MarkDirty();
        // UI sofort ueber Fortschrittsaenderung benachrichtigen
        ChallengeProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════════════

    private void OnOrderCompleted(object? sender, OrderCompletedEventArgs e)
    {
        IncrementChallenge(DailyChallengeType.CompleteOrders);
    }

    private void OnMoneyChanged(object? sender, MoneyChangedEventArgs e)
    {
        // Nur bei Geldeinnahmen (nicht Ausgaben)
        if (e.NewAmount > e.OldAmount)
        {
            var earned = (long)Math.Min(Math.Round(e.NewAmount - e.OldAmount), long.MaxValue);
            IncrementChallenge(DailyChallengeType.EarnMoney, earned);
        }
    }

    private void OnWorkshopUpgraded(object? sender, WorkshopUpgradedEventArgs e)
    {
        IncrementChallenge(DailyChallengeType.UpgradeWorkshop);
        // Workshop-Level koennte ein Ziel fuer ReachWorkshopLevel-Challenge sein
        OnWorkshopLevelReached();
    }

    private void OnWorkerHired(object? sender, WorkerHiredEventArgs e)
    {
        IncrementChallenge(DailyChallengeType.HireWorker);
    }

    private void OnMiniGameResultRecorded(object? sender, MiniGameResultRecordedEventArgs e)
    {
        // Score-Prozent basierend auf Rating berechnen
        int scorePercent = e.Rating switch
        {
            MiniGameRating.Perfect => 100,
            MiniGameRating.Good => 75,
            MiniGameRating.Ok => 50,
            _ => 0
        };
        OnMiniGamePlayed(scorePercent);
    }

    private void OnWorkerLevelUp(object? sender, Worker worker)
    {
        // Worker-Training wird als "Worker ausgebildet" gezaehlt
        OnWorkerTrained();
    }


    /// <summary>
    /// Wird extern aufgerufen wenn ein QuickJob abgeschlossen wird.
    /// </summary>
    public void OnQuickJobCompleted()
    {
        IncrementChallenge(DailyChallengeType.CompleteQuickJob);
    }

    /// <summary>
    /// Wird extern aufgerufen wenn ein Minispiel gespielt wird.
    /// </summary>
    public void OnMiniGamePlayed(int scorePercent = 0)
    {
        IncrementChallenge(DailyChallengeType.PlayMiniGames);
        if (scorePercent > 0)
        {
            SetChallengeMax(DailyChallengeType.AchieveMinigameScore, scorePercent);
        }

        // PerfectStreak aus GameState lesen (wird von GameStateService.RecordMiniGameResult aktualisiert)
        SetChallengeMax(DailyChallengeType.AchievePerfectStreak, _gameStateService.State.PerfectStreak);
    }

    /// <summary>
    /// Wird extern aufgerufen wenn ein Arbeiter-Training abgeschlossen wird.
    /// </summary>
    public void OnWorkerTrained()
    {
        IncrementChallenge(DailyChallengeType.TrainWorker);
    }

    /// <summary>
    /// Wird extern aufgerufen wenn ein Crafting-Produkt eingesammelt wird.
    /// </summary>
    public void OnCraftingCompleted()
    {
        IncrementChallenge(DailyChallengeType.CompleteCrafting);
    }

    /// <summary>
    /// Wird extern aufgerufen wenn ein Workshop ein bestimmtes Level erreicht.
    /// Setzt den Fortschritt auf den hoechsten Workshop-Level.
    /// </summary>
    public void OnWorkshopLevelReached()
    {
        int maxLevel = GetHighestWorkshopLevel();
        SetChallengeMax(DailyChallengeType.ReachWorkshopLevel, maxLevel);
    }

    /// <summary>
    /// Wird vom GameLoopService aufgerufen wenn Items auto-produziert wurden.
    /// </summary>
    public void OnItemsAutoProduced(int count)
    {
        IncrementChallenge(DailyChallengeType.ProduceItems, count);
    }

    /// <summary>
    /// Wird vom CraftingViewModel aufgerufen wenn Items verkauft wurden.
    /// </summary>
    public void OnItemsSold(int count)
    {
        IncrementChallenge(DailyChallengeType.SellItems, count);
    }

    /// <summary>
    /// Wird vom MainViewModel aufgerufen wenn ein Lieferauftrag abgeschlossen wird.
    /// </summary>
    public void OnMaterialOrderCompleted()
    {
        IncrementChallenge(DailyChallengeType.CompleteMaterialOrder);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _gameStateService.OrderCompleted -= OnOrderCompleted;
        _gameStateService.MoneyChanged -= OnMoneyChanged;
        _gameStateService.WorkshopUpgraded -= OnWorkshopUpgraded;
        _gameStateService.WorkerHired -= OnWorkerHired;
        _gameStateService.MiniGameResultRecorded -= OnMiniGameResultRecorded;
        _workerService.WorkerLevelUp -= OnWorkerLevelUp;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
