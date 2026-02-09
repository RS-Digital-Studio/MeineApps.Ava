using HandwerkerImperium.Models;
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
    private readonly Random _random = new();
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

        // Lokale Zeit fuer Tagesgrenze (nicht UTC)
        if (DateTime.Now.Date > state.LastResetDate.Date)
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
        state.LastResetDate = DateTime.Now;

        // 3 zufaellige Typen (keine Duplikate)
        var availableTypes = new List<DailyChallengeType>(AllChallengeTypes);
        for (int i = 0; i < 3 && availableTypes.Count > 0; i++)
        {
            var idx = _random.Next(availableTypes.Count);
            var type = availableTypes[idx];
            availableTypes.RemoveAt(idx);

            state.Challenges.Add(CreateChallenge(type, level));
        }

        _gameStateService.MarkDirty();
    }

    private DailyChallenge CreateChallenge(DailyChallengeType type, int level)
    {
        // Level-Stufe bestimmen
        int tier = level <= 10 ? 0 : level <= 20 ? 1 : 2;

        var challenge = new DailyChallenge { Type = type };

        switch (type)
        {
            case DailyChallengeType.CompleteOrders:
                challenge.TargetValue = tier switch { 0 => 2, 1 => 3, _ => 5 };
                challenge.MoneyReward = 100m + tier * 100m;
                challenge.XpReward = 20 + tier * 15;
                break;

            case DailyChallengeType.EarnMoney:
                challenge.TargetValue = tier switch { 0 => 200, 1 => 500, _ => 1000 };
                challenge.MoneyReward = 80m + tier * 85m;
                challenge.XpReward = 15 + tier * 13;
                break;

            case DailyChallengeType.UpgradeWorkshop:
                challenge.TargetValue = tier >= 2 ? 2 : 1;
                challenge.MoneyReward = 150m + tier * 125m;
                challenge.XpReward = 25 + tier * 18;
                break;

            case DailyChallengeType.HireWorker:
                challenge.TargetValue = 1;
                challenge.MoneyReward = 100m + tier * 50m;
                challenge.XpReward = 20 + tier * 5;
                break;

            case DailyChallengeType.CompleteQuickJob:
                challenge.TargetValue = tier switch { 0 => 1, 1 => 2, _ => 3 };
                challenge.MoneyReward = 80m + tier * 60m;
                challenge.XpReward = 15 + tier * 10;
                break;

            case DailyChallengeType.PlayMiniGames:
                challenge.TargetValue = tier switch { 0 => 3, 1 => 5, _ => 7 };
                challenge.MoneyReward = 100m + tier * 75m;
                challenge.XpReward = 20 + tier * 13;
                break;

            case DailyChallengeType.AchieveMinigameScore:
                challenge.TargetValue = tier switch { 0 => 70, 1 => 80, _ => 90 };
                challenge.MoneyReward = 120m + tier * 115m;
                challenge.XpReward = 25 + tier * 15;
                break;
        }

        // Goldschrauben-Belohnung: 1-2 je nach Level-Stufe (gedeckelt auf 2)
        challenge.GoldenScrewReward = Math.Min(1 + tier, 2);

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
            var earned = (int)(e.NewAmount - e.OldAmount);
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

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
