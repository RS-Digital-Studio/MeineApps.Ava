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
    private readonly IVipService _vipService;
    private readonly IWorkerService _workerService;
    private bool _disposed;

    private static readonly WeeklyMissionType[] AllMissionTypes = Enum.GetValues<WeeklyMissionType>();

    public event Action? MissionProgressChanged;

    /// <summary>
    /// Tier-basierter Alle-fertig-Bonus (GS). Tiers 0-4: 50, Tiers 5-8: 60/75/90/120.
    /// </summary>
    public int AllCompletedBonusScrews
    {
        get
        {
            int tier = GetTier(_gameStateService.State.PlayerLevel);
            return tier switch
            {
                <= 4 => 50,
                5 => 60,
                6 => 75,
                7 => 90,
                _ => 120
            };
        }
    }

    public WeeklyMissionService(IGameStateService gameStateService, ILocalizationService localizationService, IVipService vipService, IWorkerService workerService)
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
        _workerService.WorkerLevelUp += OnWorkerLevelUp;
    }

    public void Initialize()
    {
        // Event-Subscriptions sind im Konstruktor verdrahtet.
        // Initialize() bleibt als Interface-Methode erhalten.
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

        int tier = GetTier(level);

        // VIP-Extra: Gold+ bekommt +1 Mission pro Woche
        int missionCount = 5 + _vipService.ExtraWeeklyMissions;

        // Verfuegbare Typen nach Tier filtern
        var availableTypes = GetAvailableTypesForTier(tier);
        for (int i = 0; i < missionCount && availableTypes.Count > 0; i++)
        {
            var idx = Random.Shared.Next(availableTypes.Count);
            var type = availableTypes[idx];
            availableTypes.RemoveAt(idx);

            state.Missions.Add(CreateMission(type, level));
        }

        _gameStateService.MarkDirty();
        MissionProgressChanged?.Invoke();
    }

    /// <summary>
    /// Gibt die fuer den Tier verfuegbaren Missions-Typen zurueck.
    /// Neue Typen werden erst ab bestimmten Tiers freigeschaltet.
    /// </summary>
    private static List<WeeklyMissionType> GetAvailableTypesForTier(int tier)
    {
        var types = new List<WeeklyMissionType>
        {
            // Basis-Typen (immer verfuegbar)
            WeeklyMissionType.CompleteOrders,
            WeeklyMissionType.EarnMoney,
            WeeklyMissionType.UpgradeWorkshops,
            WeeklyMissionType.HireWorkers,
            WeeklyMissionType.PlayMiniGames,
            WeeklyMissionType.CompleteDailyChallenges,
            WeeklyMissionType.AchievePerfectRatings
        };

        // Tier 5+: Arbeiter trainieren, Crafting abschliessen
        if (tier >= 5)
        {
            types.Add(WeeklyMissionType.TrainWorkers);
            types.Add(WeeklyMissionType.CompleteCraftings);
        }

        // Tier 6+: Perfekte Serie erreichen
        if (tier >= 6)
            types.Add(WeeklyMissionType.AchievePerfectStreak);

        // Tier 7+: Werkstatt-Level erreichen
        if (tier >= 7)
            types.Add(WeeklyMissionType.ReachWorkshopLevels);

        return types;
    }

    /// <summary>
    /// Berechnet den Tier basierend auf dem Spieler-Level.
    /// Identisch mit DailyChallengeService.GetTier().
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

    private WeeklyMission CreateMission(WeeklyMissionType type, int level)
    {
        int tier = GetTier(level);

        // Einkommens-Basis: ~50 Minuten Netto-Einkommen (5x Daily), mindestens Level * 150
        var netPerSecond = Math.Max(0m, _gameStateService.State.NetIncomePerSecond);
        var incomeBase = Math.Max(level * 150m, netPerSecond * 3000m);

        var mission = new WeeklyMission
        {
            Id = Guid.NewGuid().ToString(),
            Type = type
        };

        // Zielwerte sind 3-5x der taeglichen Aequivalente
        switch (type)
        {
            case WeeklyMissionType.CompleteOrders:
                mission.TargetValue = tier switch { 0 => 10, 1 => 15, 2 => 20, 3 => 25, 4 => 30, 5 => 35, 6 => 40, 7 => 50, _ => 60 };
                mission.MoneyReward = Math.Round(incomeBase * 0.8m, 0);
                mission.XpReward = 100 + level * 5;
                break;

            case WeeklyMissionType.EarnMoney:
                mission.TargetValue = (long)Math.Max(1000, incomeBase * 2.5m);
                mission.MoneyReward = Math.Round(incomeBase * 0.6m, 0);
                mission.XpReward = 75 + level * 5;
                break;

            case WeeklyMissionType.UpgradeWorkshops:
                mission.TargetValue = tier switch { 0 => 5, 1 => 8, 2 => 12, 3 => 15, 4 => 20, 5 => 25, 6 => 30, 7 => 40, _ => 50 };
                mission.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                mission.XpReward = 125 + level * 5;
                break;

            case WeeklyMissionType.HireWorkers:
                mission.TargetValue = tier switch { 0 => 2, 1 => 3, 2 => 4, 3 => 5, 4 => 7, 5 => 8, 6 => 10, 7 => 12, _ => 15 };
                mission.MoneyReward = Math.Round(incomeBase * 0.7m, 0);
                mission.XpReward = 100 + level * 5;
                break;

            case WeeklyMissionType.PlayMiniGames:
                mission.TargetValue = tier switch { 0 => 15, 1 => 20, 2 => 25, 3 => 30, 4 => 40, 5 => 45, 6 => 50, 7 => 60, _ => 75 };
                mission.MoneyReward = Math.Round(incomeBase * 0.7m, 0);
                mission.XpReward = 100 + level * 5;
                break;

            case WeeklyMissionType.CompleteDailyChallenges:
                mission.TargetValue = tier switch { 0 => 5, 1 => 7, 2 => 10, 3 => 12, 4 => 15, 5 => 18, 6 => 20, 7 => 21, _ => 21 };
                mission.MoneyReward = Math.Round(incomeBase * 0.9m, 0);
                mission.XpReward = 110 + level * 5;
                break;

            case WeeklyMissionType.AchievePerfectRatings:
                mission.TargetValue = tier switch { 0 => 5, 1 => 8, 2 => 12, 3 => 15, 4 => 20, 5 => 25, 6 => 30, 7 => 35, _ => 40 };
                mission.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                mission.XpReward = 125 + level * 5;
                break;

            // Neue Typen (Tier 5+): Zielwerte 3-5x der Daily-Aequivalente
            case WeeklyMissionType.TrainWorkers:
                mission.TargetValue = tier switch { 5 => 8, 6 => 12, 7 => 16, _ => 20 };
                mission.MoneyReward = Math.Round(incomeBase * 0.9m, 0);
                mission.XpReward = 125 + level * 5;
                break;

            case WeeklyMissionType.CompleteCraftings:
                mission.TargetValue = tier switch { 5 => 4, 6 => 7, 7 => 10, _ => 15 };
                mission.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                mission.XpReward = 150 + level * 5;
                break;

            // Tier 6+
            case WeeklyMissionType.AchievePerfectStreak:
                mission.TargetValue = tier switch { 6 => 10, 7 => 15, _ => 20 };
                mission.MoneyReward = Math.Round(incomeBase * 1.2m, 0);
                mission.XpReward = 175 + level * 5;
                break;

            // Tier 7+: Zielwert abhaengig vom aktuellen hoechsten Workshop-Level
            case WeeklyMissionType.ReachWorkshopLevels:
                int highestLevel = GetHighestWorkshopLevel();
                int increment = tier >= 8 ? 150 : 50;
                mission.TargetValue = highestLevel + increment;
                mission.MoneyReward = Math.Round(incomeBase * 1.5m, 0);
                mission.XpReward = 200 + level * 5;
                break;

            // Tier 5+: Viele Items auto-produzieren (3-5x Daily)
            case WeeklyMissionType.ProduceItems:
                mission.TargetValue = tier switch { 5 => 100, 6 => 250, 7 => 500, _ => 1000 };
                mission.MoneyReward = Math.Round(incomeBase * 0.8m, 0);
                mission.XpReward = 125 + level * 5;
                break;
        }

        // Goldschrauben-Belohnung nach Tier
        mission.GoldenScrewReward = tier switch
        {
            0 => 5,
            1 => 7,
            2 => 9,
            3 => 11,
            4 => 15,
            5 => 18,
            6 => 22,
            7 => 28,
            _ => 35
        };

        return mission;
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

    // ═══════════════════════════════════════════════════════════════════════
    // FORTSCHRITTS-TRACKING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erhoeht den Fortschritt aller passenden Missionen.
    /// </summary>
    private void IncrementMission(WeeklyMissionType type, long amount = 1)
    {
        var missions = _gameStateService.State.WeeklyMissionState.Missions;
        bool changed = false;

        // For-Schleife statt LINQ .Where() (wird bei Events haeufig aufgerufen)
        for (int i = 0; i < missions.Count; i++)
        {
            var mission = missions[i];
            if (mission.Type != type || mission.IsCompleted) continue;
            mission.CurrentValue += amount;
            changed = true;
        }

        if (changed)
        {
            _gameStateService.MarkDirty();
            MissionProgressChanged?.Invoke();
        }
    }

    /// <summary>
    /// Setzt den Fortschritt einer Mission auf den Maximalwert (fuer Highscore-basierte wie PerfectStreak/WorkshopLevel).
    /// </summary>
    private void SetMissionMax(WeeklyMissionType type, long value)
    {
        var missions = _gameStateService.State.WeeklyMissionState.Missions;
        bool changed = false;

        for (int i = 0; i < missions.Count; i++)
        {
            var mission = missions[i];
            if (mission.Type != type || mission.IsCompleted) continue;
            if (value > mission.CurrentValue)
            {
                mission.CurrentValue = value;
                changed = true;
            }
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
        // Workshop-Level koennte ein Ziel fuer ReachWorkshopLevels-Mission sein
        OnWorkshopLevelReached();
    }

    private void OnWorkerHired(object? sender, WorkerHiredEventArgs e)
    {
        IncrementMission(WeeklyMissionType.HireWorkers);
    }

    private void OnMiniGameResultRecorded(object? sender, MiniGameResultRecordedEventArgs e)
    {
        IncrementMission(WeeklyMissionType.PlayMiniGames);

        // PerfectStreak aus GameState lesen (wird von GameStateService.RecordMiniGameResult aktualisiert)
        OnPerfectStreakUpdated(_gameStateService.State.PerfectStreak);
    }

    private void OnWorkerLevelUp(object? sender, Worker worker)
    {
        // Worker-Training wird als "Worker ausgebildet" gezaehlt
        OnWorkerTrained();
    }

    /// <summary>
    /// Extern aufgerufen wenn eine Daily Challenge abgeschlossen wird.
    /// </summary>
    public void OnDailyChallengeCompleted()
    {
        IncrementMission(WeeklyMissionType.CompleteDailyChallenges);
    }

    /// <summary>
    /// Extern aufgerufen wenn ein Arbeiter-Training abgeschlossen wird.
    /// </summary>
    public void OnWorkerTrained()
    {
        IncrementMission(WeeklyMissionType.TrainWorkers);
    }

    /// <summary>
    /// Extern aufgerufen wenn ein Crafting-Produkt eingesammelt wird.
    /// </summary>
    public void OnCraftingCompleted()
    {
        IncrementMission(WeeklyMissionType.CompleteCraftings);
    }

    /// <summary>
    /// Extern aufgerufen wenn ein Workshop ein neues Level erreicht.
    /// Setzt den Fortschritt auf den hoechsten Workshop-Level.
    /// </summary>
    public void OnWorkshopLevelReached()
    {
        int maxLevel = GetHighestWorkshopLevel();
        SetMissionMax(WeeklyMissionType.ReachWorkshopLevels, maxLevel);
    }

    /// <summary>
    /// Extern aufgerufen nach einem MiniGame mit PerfectStreak-Info.
    /// </summary>
    public void OnPerfectStreakUpdated(int currentStreak)
    {
        SetMissionMax(WeeklyMissionType.AchievePerfectStreak, currentStreak);
    }

    /// <summary>
    /// Wird vom GameLoopService aufgerufen wenn Items auto-produziert wurden.
    /// </summary>
    public void OnItemsAutoProduced(int count)
    {
        IncrementMission(WeeklyMissionType.ProduceItems, count);
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
        _workerService.WorkerLevelUp -= OnWorkerLevelUp;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
