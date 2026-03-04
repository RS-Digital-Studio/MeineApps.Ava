using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet wöchentliche Missionen (5 pro Woche) mit höheren Belohnungen als Daily Challenges.
/// Subscribes auf GameState-Events für automatisches Tracking.
/// </summary>
public sealed class WeeklyMissionService : IWeeklyMissionService, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private bool _disposed;
    private bool _initialized;

    private static readonly WeeklyMissionType[] AllMissionTypes = Enum.GetValues<WeeklyMissionType>();

    /// <summary>
    /// Bonus-Goldschrauben wenn alle 5 Missionen abgeschlossen sind.
    /// </summary>
    private const int AllCompletedBonusScrews = 50;

    public event Action? MissionProgressChanged;

    public WeeklyMissionService(IGameStateService gameStateService, ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _localizationService = localizationService;
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // Event-Subscriptions für automatisches Tracking
        _gameStateService.OrderCompleted += OnOrderCompleted;
        _gameStateService.MoneyChanged += OnMoneyChanged;
        _gameStateService.WorkshopUpgraded += OnWorkshopUpgraded;
        _gameStateService.WorkerHired += OnWorkerHired;
        _gameStateService.MiniGameResultRecorded += OnMiniGameResultRecorded;
    }

    public void CheckAndResetIfNewWeek()
    {
        var state = _gameStateService.State.WeeklyMissionState;
        var now = DateTime.UtcNow;

        // Zeitmanipulations-Schutz: Wenn LastWeeklyReset in der Zukunft liegt, nicht resetten
        if (state.LastWeeklyReset > now)
            return;

        // Nächsten Montag 00:00 UTC nach dem letzten Reset berechnen
        var nextMonday = GetNextMonday(state.LastWeeklyReset);

        if (now >= nextMonday)
        {
            GenerateMissions();
        }
    }

    public void ClaimMission(string missionId)
    {
        var state = _gameStateService.State.WeeklyMissionState;
        var mission = state.Missions.FirstOrDefault(m => m.Id == missionId);

        if (mission == null || !mission.IsCompleted || mission.IsClaimed)
            return;

        mission.IsClaimed = true;

        // Belohnungen gutschreiben
        _gameStateService.AddMoney(mission.MoneyReward);
        _gameStateService.AddXp(mission.XpReward);
        if (mission.GoldenScrewReward > 0)
            _gameStateService.AddGoldenScrews(mission.GoldenScrewReward);

        _gameStateService.MarkDirty();
        MissionProgressChanged?.Invoke();
    }

    public void ClaimAllCompletedBonus()
    {
        var state = _gameStateService.State.WeeklyMissionState;

        // Alle 5 müssen abgeschlossen sein
        if (state.Missions.Count == 0 || !state.Missions.All(m => m.IsCompleted))
            return;

        if (state.AllCompletedBonusClaimed)
            return;

        // Zuerst alle unclaimten Einzelbelohnungen einsammeln
        foreach (var mission in state.Missions.Where(m => m.IsCompleted && !m.IsClaimed))
        {
            mission.IsClaimed = true;
            _gameStateService.AddMoney(mission.MoneyReward);
            _gameStateService.AddXp(mission.XpReward);
            if (mission.GoldenScrewReward > 0)
                _gameStateService.AddGoldenScrews(mission.GoldenScrewReward);
        }

        // Bonus
        state.AllCompletedBonusClaimed = true;
        _gameStateService.AddGoldenScrews(AllCompletedBonusScrews);

        _gameStateService.MarkDirty();
        MissionProgressChanged?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GENERIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    private void GenerateMissions()
    {
        var state = _gameStateService.State.WeeklyMissionState;
        var level = _gameStateService.State.PlayerLevel;

        state.Missions.Clear();
        state.AllCompletedBonusClaimed = false;
        state.LastWeeklyReset = DateTime.UtcNow;

        // 5 zufällige Typen (keine Duplikate)
        var availableTypes = new List<WeeklyMissionType>(AllMissionTypes);
        for (int i = 0; i < 5 && availableTypes.Count > 0; i++)
        {
            var idx = Random.Shared.Next(availableTypes.Count);
            var type = availableTypes[idx];
            availableTypes.RemoveAt(idx);

            state.Missions.Add(CreateMission(type, level));
        }

        _gameStateService.MarkDirty();
        MissionProgressChanged?.Invoke();
    }

    private WeeklyMission CreateMission(WeeklyMissionType type, int level)
    {
        // Level-Stufe (0-4)
        int tier = level switch
        {
            <= 5 => 0,
            <= 15 => 1,
            <= 30 => 2,
            <= 50 => 3,
            _ => 4
        };

        // Einkommens-Basis: ~50 Minuten Netto-Einkommen (5x Daily), mindestens Level * 150
        var netPerSecond = Math.Max(0m, _gameStateService.State.NetIncomePerSecond);
        var incomeBase = Math.Max(level * 150m, netPerSecond * 3000m);

        var mission = new WeeklyMission
        {
            Id = Guid.NewGuid().ToString(),
            Type = type
        };

        // Zielwerte sind 3-5x der täglichen Äquivalente
        switch (type)
        {
            case WeeklyMissionType.CompleteOrders:
                mission.TargetValue = tier switch { 0 => 10, 1 => 15, 2 => 20, 3 => 25, _ => 30 };
                mission.MoneyReward = Math.Round(incomeBase * 0.8m, 0);
                mission.XpReward = 100 + level * 5;
                break;

            case WeeklyMissionType.EarnMoney:
                mission.TargetValue = (long)Math.Max(1000, incomeBase * 2.5m);
                mission.MoneyReward = Math.Round(incomeBase * 0.6m, 0);
                mission.XpReward = 75 + level * 5;
                break;

            case WeeklyMissionType.UpgradeWorkshops:
                mission.TargetValue = tier switch { 0 => 5, 1 => 8, 2 => 12, 3 => 15, _ => 20 };
                mission.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                mission.XpReward = 125 + level * 5;
                break;

            case WeeklyMissionType.HireWorkers:
                mission.TargetValue = tier switch { 0 => 2, 1 => 3, 2 => 4, 3 => 5, _ => 7 };
                mission.MoneyReward = Math.Round(incomeBase * 0.7m, 0);
                mission.XpReward = 100 + level * 5;
                break;

            case WeeklyMissionType.PlayMiniGames:
                mission.TargetValue = tier switch { 0 => 15, 1 => 20, 2 => 25, 3 => 30, _ => 40 };
                mission.MoneyReward = Math.Round(incomeBase * 0.7m, 0);
                mission.XpReward = 100 + level * 5;
                break;

            case WeeklyMissionType.CompleteDailyChallenges:
                mission.TargetValue = tier switch { 0 => 5, 1 => 7, 2 => 10, 3 => 12, _ => 15 };
                mission.MoneyReward = Math.Round(incomeBase * 0.9m, 0);
                mission.XpReward = 110 + level * 5;
                break;

            case WeeklyMissionType.AchievePerfectRatings:
                mission.TargetValue = tier switch { 0 => 5, 1 => 8, 2 => 12, 3 => 15, _ => 20 };
                mission.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                mission.XpReward = 125 + level * 5;
                break;
        }

        // Goldschrauben-Belohnung: 5x Daily (5-15 je nach Stufe)
        mission.GoldenScrewReward = Math.Min(5 + tier * 2, 15);

        return mission;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FORTSCHRITTS-TRACKING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erhöht den Fortschritt aller passenden Missionen.
    /// </summary>
    private void IncrementMission(WeeklyMissionType type, long amount = 1)
    {
        var missions = _gameStateService.State.WeeklyMissionState.Missions;
        bool changed = false;

        foreach (var mission in missions.Where(m => m.Type == type && !m.IsCompleted))
        {
            mission.CurrentValue += amount;
            if (mission.CurrentValue >= mission.TargetValue)
            {
                // IsCompleted ist eine berechnete Property
            }
            changed = true;
        }

        if (changed)
        {
            _gameStateService.MarkDirty();
            MissionProgressChanged?.Invoke();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════════════

    private void OnOrderCompleted(object? sender, OrderCompletedEventArgs e)
    {
        IncrementMission(WeeklyMissionType.CompleteOrders);

        // Perfect-Rating tracken
        if (e.AverageRating == MiniGameRating.Perfect)
            IncrementMission(WeeklyMissionType.AchievePerfectRatings);
    }

    private void OnMoneyChanged(object? sender, MoneyChangedEventArgs e)
    {
        // Nur bei Geldeinnahmen (nicht Ausgaben)
        if (e.NewAmount > e.OldAmount)
        {
            var earned = (long)Math.Min(Math.Round(e.NewAmount - e.OldAmount), long.MaxValue);
            IncrementMission(WeeklyMissionType.EarnMoney, earned);
        }
    }

    private void OnWorkshopUpgraded(object? sender, WorkshopUpgradedEventArgs e)
    {
        IncrementMission(WeeklyMissionType.UpgradeWorkshops);
    }

    private void OnWorkerHired(object? sender, WorkerHiredEventArgs e)
    {
        IncrementMission(WeeklyMissionType.HireWorkers);
    }

    private void OnMiniGameResultRecorded(object? sender, MiniGameResultRecordedEventArgs e)
    {
        IncrementMission(WeeklyMissionType.PlayMiniGames);
    }

    /// <summary>
    /// Extern aufgerufen wenn eine Daily Challenge abgeschlossen wird.
    /// </summary>
    public void OnDailyChallengeCompleted()
    {
        IncrementMission(WeeklyMissionType.CompleteDailyChallenges);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet den nächsten Montag 00:00 UTC nach dem gegebenen Datum.
    /// </summary>
    private static DateTime GetNextMonday(DateTime from)
    {
        var date = from.Date;
        int daysUntilMonday = ((int)DayOfWeek.Monday - (int)date.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
            daysUntilMonday = 7;
        return date.AddDays(daysUntilMonday);
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
