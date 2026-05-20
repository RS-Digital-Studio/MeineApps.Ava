using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models.Events;

/// <summary>
/// Event fired when money changes.
/// </summary>
public class MoneyChangedEventArgs : EventArgs
{
    public decimal OldAmount { get; }
    public decimal NewAmount { get; }
    public decimal Delta => NewAmount - OldAmount;

    public MoneyChangedEventArgs(decimal oldAmount, decimal newAmount)
    {
        OldAmount = oldAmount;
        NewAmount = newAmount;
    }
}

/// <summary>
/// Event fired when player levels up.
/// </summary>
public class LevelUpEventArgs : EventArgs
{
    public int OldLevel { get; }
    public int NewLevel { get; }
    public List<WorkshopType> NewlyUnlockedWorkshops { get; }

    public LevelUpEventArgs(int oldLevel, int newLevel, List<WorkshopType> unlockedWorkshops)
    {
        OldLevel = oldLevel;
        NewLevel = newLevel;
        NewlyUnlockedWorkshops = unlockedWorkshops;
    }
}

/// <summary>
/// Event fired when XP is gained.
/// </summary>
public class XpGainedEventArgs : EventArgs
{
    public int Amount { get; }
    public int TotalXp { get; }
    public int CurrentXp { get; }
    public int XpForNextLevel { get; }

    public XpGainedEventArgs(int amount, int totalXp, int currentXp, int xpForNextLevel)
    {
        Amount = amount;
        TotalXp = totalXp;
        CurrentXp = currentXp;
        XpForNextLevel = xpForNextLevel;
    }
}

/// <summary>
/// Event fired when a workshop is upgraded.
/// </summary>
public class WorkshopUpgradedEventArgs : EventArgs
{
    public WorkshopType WorkshopType { get; }
    public int OldLevel { get; }
    public int NewLevel { get; }
    public decimal Cost { get; }

    public WorkshopUpgradedEventArgs(WorkshopType type, int oldLevel, int newLevel, decimal cost)
    {
        WorkshopType = type;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        Cost = cost;
    }
}

/// <summary>
/// Event fired when a worker is hired.
/// </summary>
public class WorkerHiredEventArgs : EventArgs
{
    public WorkshopType WorkshopType { get; }
    public Worker Worker { get; }
    public decimal Cost { get; }
    public int TotalWorkers { get; }

    public WorkerHiredEventArgs(WorkshopType type, Worker worker, decimal cost, int totalWorkers)
    {
        WorkshopType = type;
        Worker = worker;
        Cost = cost;
        TotalWorkers = totalWorkers;
    }
}

/// <summary>
/// Event fired when an order is started (v2.1.2, FTUE-Verdrahtung F-03).
/// Wird vom FtueProgressTracker abonniert, um den FTUE-Step "AcceptFirstOrder" zu triggern.
/// </summary>
public class OrderStartedEventArgs(Order order) : EventArgs
{
    public Order Order { get; } = order;
}

/// <summary>
/// Event fired when an order is completed.
/// </summary>
public class OrderCompletedEventArgs : EventArgs
{
    public Order Order { get; }
    public decimal MoneyReward { get; }
    public int XpReward { get; }
    public MiniGameRating AverageRating { get; }

    public OrderCompletedEventArgs(Order order, decimal moneyReward, int xpReward, MiniGameRating averageRating)
    {
        Order = order;
        MoneyReward = moneyReward;
        XpReward = xpReward;
        AverageRating = averageRating;
    }
}

/// <summary>
/// Event fired when a mini-game is completed.
/// </summary>
public class MiniGameCompletedEventArgs : EventArgs
{
    public MiniGameType GameType { get; }
    public MiniGameRating Rating { get; }
    public double TimingAccuracy { get; }

    public MiniGameCompletedEventArgs(MiniGameType gameType, MiniGameRating rating, double timingAccuracy)
    {
        GameType = gameType;
        Rating = rating;
        TimingAccuracy = timingAccuracy;
    }
}

/// <summary>
/// Event fired when offline earnings are calculated.
/// </summary>
public class OfflineEarningsEventArgs : EventArgs
{
    public decimal Earnings { get; }
    public TimeSpan OfflineDuration { get; }
    public bool WasCapped { get; }

    public OfflineEarningsEventArgs(decimal earnings, TimeSpan duration, bool wasCapped)
    {
        Earnings = earnings;
        OfflineDuration = duration;
        WasCapped = wasCapped;
    }
}

/// <summary>
/// Event fired when golden screws change.
/// </summary>
public class GoldenScrewsChangedEventArgs(int oldAmount, int newAmount) : EventArgs
{
    public int OldAmount { get; } = oldAmount;
    public int NewAmount { get; } = newAmount;
}

/// <summary>
/// Event fired when a mini-game result is recorded.
/// </summary>
public class MiniGameResultRecordedEventArgs(MiniGameRating rating) : EventArgs
{
    public MiniGameRating Rating { get; } = rating;
}

/// <summary>
/// Event fired when a Perfect-Rating increment happens (v2.0.36 Mastery-System).
/// Trigger fuer den MasteryService — enthaelt den MiniGameType, der nicht in
/// <see cref="MiniGameResultRecordedEventArgs"/> mitgegeben wird.
/// </summary>
public class PerfectRatingIncrementedEventArgs(Models.Enums.MiniGameType type, int newLifetimeCount) : EventArgs
{
    public Models.Enums.MiniGameType MiniGameType { get; } = type;
    public int NewLifetimeCount { get; } = newLifetimeCount;
}

/// <summary>
/// Event fired wenn der Reputation-Tier wechselt (v2.0.37).
/// Liefert alten + neuen Tier — Aufrufer kann Up-Direction (newTier &gt; oldTier) detecten
/// und Confetti/FloatingText nur bei Aufstieg triggern.
/// </summary>
public class ReputationTierChangedEventArgs(
    Models.Enums.CustomerReputationTier oldTier,
    Models.Enums.CustomerReputationTier newTier) : EventArgs
{
    public Models.Enums.CustomerReputationTier OldTier { get; } = oldTier;
    public Models.Enums.CustomerReputationTier NewTier { get; } = newTier;
    public bool IsUp => NewTier > OldTier;
}

/// <summary>
/// Event fired bei neuer persoenlicher Speedrun-Bestzeit pro Tier (v2.0.36).
/// Belohnung wurde bereits dem GameState gutgeschrieben.
/// </summary>
public class SpeedrunRecordEventArgs(Models.Enums.PrestigeTier tier, TimeSpan runDuration, int goldenScrewReward) : EventArgs
{
    public Models.Enums.PrestigeTier Tier { get; } = tier;
    public TimeSpan RunDuration { get; } = runDuration;
    public int GoldenScrewReward { get; } = goldenScrewReward;
}

/// <summary>
/// Event fired on each game tick (once per second).
/// </summary>
public class GameTickEventArgs : EventArgs
{
    public decimal EarningsThisTick { get; }
    public decimal TotalMoney { get; }
    public TimeSpan SessionDuration { get; }

    public GameTickEventArgs(decimal earnings, decimal totalMoney, TimeSpan sessionDuration)
    {
        EarningsThisTick = earnings;
        TotalMoney = totalMoney;
        SessionDuration = sessionDuration;
    }
}
