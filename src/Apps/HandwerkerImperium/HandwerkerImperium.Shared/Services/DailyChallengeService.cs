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
public class DailyChallengeService : IDailyChallengeService, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private bool _disposed;

    private static readonly DailyChallengeType[] AllChallengeTypes = Enum.GetValues<DailyChallengeType>();

    public decimal AllCompletedBonusAmount => 500m;
    public int AllCompletedBonusScrews => 10;

    public DailyChallengeService(IGameStateService gameStateService, ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _localizationService = localizationService;

        // Event-Subscriptions fuer automatisches Tracking
        _gameStateService.OrderCompleted += OnOrderCompleted;
        _gameStateService.MoneyChanged += OnMoneyChanged;
        _gameStateService.WorkshopUpgraded += OnWorkshopUpgraded;
        _gameStateService.WorkerHired += OnWorkerHired;
        _gameStateService.MiniGameResultRecorded += OnMiniGameResultRecorded;
    }

    public bool AreAllCompleted
    {
        get
        {
            var challenges = _gameStateService.State.DailyChallengeState.Challenges;
            return challenges.Count > 0 && challenges.All(c => c.IsCompleted);
        }
    }

    public bool HasUnclaimedRewards
    {
        get
        {
            var state = _gameStateService.State.DailyChallengeState;
            return state.Challenges.Any(c => c.IsCompleted && !c.IsClaimed) ||
                   (AreAllCompleted && !state.AllCompletedBonusClaimed);
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

        // UTC fuer konsistente Tagesgrenze
        if (DateTime.UtcNow.Date > state.LastResetDate.Date)
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

        // Zuerst alle unclaimten Einzelbelohnungen einsammeln
        foreach (var challenge in state.Challenges.Where(c => c.IsCompleted && !c.IsClaimed))
        {
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

        // 3 zufaellige Typen (keine Duplikate)
        var availableTypes = new List<DailyChallengeType>(AllChallengeTypes);
        for (int i = 0; i < 3 && availableTypes.Count > 0; i++)
        {
            var idx = Random.Shared.Next(availableTypes.Count);
            var type = availableTypes[idx];
            availableTypes.RemoveAt(idx);

            state.Challenges.Add(CreateChallenge(type, level));
        }

        _gameStateService.MarkDirty();
    }

    private DailyChallenge CreateChallenge(DailyChallengeType type, int level)
    {
        // Level-Stufe bestimmen (0-4 statt nur 0-2)
        int tier = level switch
        {
            <= 5 => 0,
            <= 15 => 1,
            <= 30 => 2,
            <= 50 => 3,
            _ => 4
        };

        // Basis-Multiplikator: Belohnung skaliert mit Level
        // ~10 Minuten Netto-Einkommen als Basis, mindestens Level * 30
        var netPerSecond = Math.Max(0m, _gameStateService.State.NetIncomePerSecond);
        var incomeBase = Math.Max(level * 30m, netPerSecond * 600m);

        var challenge = new DailyChallenge { Type = type };

        switch (type)
        {
            case DailyChallengeType.CompleteOrders:
                challenge.TargetValue = tier switch { 0 => 2, 1 => 3, 2 => 4, _ => 5 };
                challenge.MoneyReward = Math.Round(incomeBase * 0.8m, 0);
                challenge.XpReward = 20 + level * 2;
                break;

            case DailyChallengeType.EarnMoney:
                challenge.TargetValue = (int)Math.Max(200, incomeBase * 0.5m);
                challenge.MoneyReward = Math.Round(incomeBase * 0.6m, 0);
                challenge.XpReward = 15 + level * 2;
                break;

            case DailyChallengeType.UpgradeWorkshop:
                challenge.TargetValue = tier >= 3 ? 3 : tier >= 1 ? 2 : 1;
                challenge.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                challenge.XpReward = 25 + level * 2;
                break;

            case DailyChallengeType.HireWorker:
                challenge.TargetValue = 1;
                challenge.MoneyReward = Math.Round(incomeBase * 0.7m, 0);
                challenge.XpReward = 20 + level * 2;
                break;

            case DailyChallengeType.CompleteQuickJob:
                challenge.TargetValue = tier switch { 0 => 1, 1 => 2, 2 => 3, _ => 4 };
                challenge.MoneyReward = Math.Round(incomeBase * 0.5m, 0);
                challenge.XpReward = 15 + level * 2;
                break;

            case DailyChallengeType.PlayMiniGames:
                challenge.TargetValue = tier switch { 0 => 3, 1 => 4, 2 => 5, _ => 7 };
                challenge.MoneyReward = Math.Round(incomeBase * 0.7m, 0);
                challenge.XpReward = 20 + level * 2;
                break;

            case DailyChallengeType.AchieveMinigameScore:
                challenge.TargetValue = tier switch { 0 => 70, 1 => 75, 2 => 80, _ => 90 };
                challenge.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                challenge.XpReward = 25 + level * 2;
                break;
        }

        // Goldschrauben-Belohnung: 1-3 je nach Level-Stufe
        challenge.GoldenScrewReward = Math.Min(1 + tier, 3);

        return challenge;
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
                string.Format(_localizationService.GetString("ChallengeEarnMoney"), challenge.TargetValue),
            DailyChallengeType.UpgradeWorkshop =>
                _localizationService.GetString("ChallengeUpgradeWorkshop"),
            DailyChallengeType.HireWorker =>
                _localizationService.GetString("ChallengeHireWorker"),
            DailyChallengeType.CompleteQuickJob =>
                string.Format(_localizationService.GetString("ChallengeCompleteQuickJob"), challenge.TargetValue),
            DailyChallengeType.PlayMiniGames =>
                string.Format(_localizationService.GetString("ChallengePlayMiniGames"), challenge.TargetValue),
            DailyChallengeType.AchieveMinigameScore =>
                string.Format(_localizationService.GetString("ChallengeAchieveScore"), challenge.TargetValue),
            _ => ""
        };

        challenge.RewardDisplay = challenge.GoldenScrewReward > 0
            ? $"{challenge.MoneyReward:N0} € + {challenge.XpReward} XP + {challenge.GoldenScrewReward} 🔩"
            : $"{challenge.MoneyReward:N0} € + {challenge.XpReward} XP";
    }

    /// <summary>
    /// Aktualisiert den Fortschritt einer bestimmten Challenge-Art.
    /// </summary>
    private void IncrementChallenge(DailyChallengeType type, int amount = 1)
    {
        var challenges = _gameStateService.State.DailyChallengeState.Challenges;
        foreach (var challenge in challenges.Where(c => c.Type == type && !c.IsCompleted))
        {
            challenge.CurrentValue += amount;
            if (challenge.CurrentValue >= challenge.TargetValue)
            {
                challenge.IsCompleted = true;
            }
        }
        _gameStateService.MarkDirty();
    }

    /// <summary>
    /// Setzt den Fortschritt einer Challenge auf den Maximalwert (fuer Score-basierte).
    /// </summary>
    private void SetChallengeMax(DailyChallengeType type, int value)
    {
        var challenges = _gameStateService.State.DailyChallengeState.Challenges;
        foreach (var challenge in challenges.Where(c => c.Type == type && !c.IsCompleted))
        {
            if (value > challenge.CurrentValue)
                challenge.CurrentValue = value;
            if (challenge.CurrentValue >= challenge.TargetValue)
                challenge.IsCompleted = true;
        }
        _gameStateService.MarkDirty();
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
            var earned = (int)Math.Round(e.NewAmount - e.OldAmount);
            IncrementChallenge(DailyChallengeType.EarnMoney, earned);
        }
    }

    private void OnWorkshopUpgraded(object? sender, WorkshopUpgradedEventArgs e)
    {
        IncrementChallenge(DailyChallengeType.UpgradeWorkshop);
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
    }

    public void Dispose()
    {
        if (_disposed) return;

        _gameStateService.OrderCompleted -= OnOrderCompleted;
        _gameStateService.MoneyChanged -= OnMoneyChanged;
        _gameStateService.WorkshopUpgraded -= OnWorkshopUpgraded;
        _gameStateService.WorkerHired -= OnWorkerHired;
        _gameStateService.MiniGameResultRecorded -= OnMiniGameResultRecorded;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
