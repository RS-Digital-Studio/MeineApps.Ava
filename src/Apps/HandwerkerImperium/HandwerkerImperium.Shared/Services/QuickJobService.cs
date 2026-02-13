using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet Schnell-Auftraege die alle 15 Minuten rotieren.
/// </summary>
public class QuickJobService : IQuickJobService
{
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private static readonly TimeSpan RotationInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Maximale Anzahl Quick Jobs pro Tag (verhindert Reward-Farming).
    /// </summary>
    public const int MaxQuickJobsPerDay = 20;

    // Verfuegbare MiniGame-Typen fuer Quick Jobs (nur die 4 implementierten)
    private static readonly MiniGameType[] AvailableMiniGames =
    [
        MiniGameType.Sawing,
        MiniGameType.PipePuzzle,
        MiniGameType.WiringGame,
        MiniGameType.PaintingGame
    ];

    private static readonly string[] TitleKeys =
    [
        "QuickRepair", "QuickFix", "ExpressService", "SmallOrder",
        "QuickMeasure", "QuickInstall", "QuickPaint", "QuickCheck"
    ];

    public event EventHandler<QuickJob>? QuickJobCompleted;

    public QuickJobService(IGameStateService gameStateService, ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _localizationService = localizationService;
    }

    public TimeSpan TimeUntilNextRotation
    {
        get
        {
            var lastRotation = _gameStateService.State.LastQuickJobRotation;
            var nextRotation = lastRotation + RotationInterval;
            var remaining = nextRotation - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public List<QuickJob> GetAvailableJobs()
    {
        var jobs = _gameStateService.State.QuickJobs;
        foreach (var job in jobs)
        {
            PopulateDisplayFields(job);
        }
        return jobs;
    }

    public void GenerateJobs(int count = 5)
    {
        var state = _gameStateService.State;
        var level = state.PlayerLevel;

        // Freigeschaltete Workshop-Typen ermitteln
        var unlockedTypes = state.UnlockedWorkshopTypes;
        if (unlockedTypes.Count == 0)
            unlockedTypes = [WorkshopType.Carpenter];

        state.QuickJobs.Clear();
        for (int i = 0; i < count; i++)
        {
            var workshopType = unlockedTypes[Random.Shared.Next(unlockedTypes.Count)];
            var miniGameType = AvailableMiniGames[Random.Shared.Next(AvailableMiniGames.Length)];
            var titleKey = TitleKeys[Random.Shared.Next(TitleKeys.Length)];

            // Belohnung skaliert mit Level und aktuellem Einkommen
            var (reward, xpReward) = CalculateQuickJobRewards(level);

            state.QuickJobs.Add(new QuickJob
            {
                WorkshopType = workshopType,
                Difficulty = OrderDifficulty.Easy,
                MiniGameType = miniGameType,
                Reward = reward,
                XpReward = xpReward,
                TitleKey = titleKey
            });
        }

        state.LastQuickJobRotation = DateTime.UtcNow;
        _gameStateService.MarkDirty();
    }

    public bool NeedsRotation()
    {
        return DateTime.UtcNow - _gameStateService.State.LastQuickJobRotation > RotationInterval;
    }

    public void RotateIfNeeded()
    {
        // Tages-Counter zurücksetzen wenn neuer Tag
        ResetDailyCounterIfNewDay();

        if (!NeedsRotation()) return;

        var state = _gameStateService.State;

        // Erledigte Jobs entfernen
        state.QuickJobs.RemoveAll(j => j.IsCompleted);

        // Neue Jobs generieren bis 5 erreicht
        var missing = 5 - state.QuickJobs.Count;
        if (missing > 0)
        {
            var unlockedTypes = state.UnlockedWorkshopTypes;
            if (unlockedTypes.Count == 0) unlockedTypes = [WorkshopType.Carpenter];
            var level = state.PlayerLevel;

            for (int i = 0; i < missing; i++)
            {
                var workshopType = unlockedTypes[Random.Shared.Next(unlockedTypes.Count)];
                var miniGameType = AvailableMiniGames[Random.Shared.Next(AvailableMiniGames.Length)];
                var titleKey = TitleKeys[Random.Shared.Next(TitleKeys.Length)];
                var (reward, xpReward) = CalculateQuickJobRewards(level);

                state.QuickJobs.Add(new QuickJob
                {
                    WorkshopType = workshopType,
                    Difficulty = OrderDifficulty.Easy,
                    MiniGameType = miniGameType,
                    Reward = reward,
                    XpReward = xpReward,
                    TitleKey = titleKey
                });
            }
        }

        state.LastQuickJobRotation = DateTime.UtcNow;
        _gameStateService.MarkDirty();
    }

    /// <summary>
    /// Berechnet QuickJob-Belohnungen basierend auf Level und aktuellem Netto-Einkommen.
    /// Gibt ~5 Minuten Netto-Einkommen als Basis, mindestens Level-skaliert.
    /// </summary>
    private (decimal reward, int xpReward) CalculateQuickJobRewards(int level)
    {
        // Basis: ~5 Min Netto-Einkommen (Mindestens Level * 50)
        var fiveMinIncome = Math.Max(0m, _gameStateService.State.NetIncomePerSecond) * 300m;
        var reward = Math.Max(20m + level * 50m, fiveMinIncome);

        // XP skaliert mit Level (kein starres Cap)
        var xpReward = 5 + level * 3;

        return (Math.Round(reward, 0), xpReward);
    }

    /// <summary>
    /// Fuellt die Display-Properties eines QuickJobs mit lokalisierten Texten.
    /// </summary>
    private void PopulateDisplayFields(QuickJob job)
    {
        var title = _localizationService.GetString(job.TitleKey);
        job.DisplayTitle = string.IsNullOrEmpty(title) ? job.TitleKey : title;
        job.DisplayWorkshopName = _localizationService.GetString(job.WorkshopType.GetLocalizationKey());
        job.RewardDisplay = $"{job.Reward:N0} € + {job.XpReward} XP";
    }

    /// <summary>
    /// Prüft ob das tägliche Quick-Job-Limit erreicht ist.
    /// </summary>
    public bool IsDailyLimitReached
    {
        get
        {
            ResetDailyCounterIfNewDay();
            return _gameStateService.State.QuickJobsCompletedToday >= MaxQuickJobsPerDay;
        }
    }

    /// <summary>
    /// Verbleibende Quick Jobs heute.
    /// </summary>
    public int RemainingJobsToday
    {
        get
        {
            ResetDailyCounterIfNewDay();
            return Math.Max(0, MaxQuickJobsPerDay - _gameStateService.State.QuickJobsCompletedToday);
        }
    }

    /// <summary>
    /// Wird vom MainViewModel aufgerufen wenn ein QuickJob abgeschlossen wird.
    /// Erhöht Tages-Counter und feuert Event.
    /// </summary>
    public void NotifyJobCompleted(QuickJob job)
    {
        ResetDailyCounterIfNewDay();
        _gameStateService.State.QuickJobsCompletedToday++;
        QuickJobCompleted?.Invoke(this, job);
    }

    /// <summary>
    /// Setzt den Tages-Counter zurück wenn ein neuer Tag (UTC) begonnen hat.
    /// </summary>
    private void ResetDailyCounterIfNewDay()
    {
        var state = _gameStateService.State;
        if (DateTime.UtcNow.Date > state.LastQuickJobDailyReset.Date)
        {
            state.QuickJobsCompletedToday = 0;
            state.LastQuickJobDailyReset = DateTime.UtcNow;
        }
    }
}
