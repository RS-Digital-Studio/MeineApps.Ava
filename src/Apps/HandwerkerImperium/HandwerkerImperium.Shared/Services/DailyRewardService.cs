using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Manages daily login rewards with a 7-day cycle and streak tracking.
/// </summary>
public class DailyRewardService : IDailyRewardService
{
    private readonly IGameStateService _gameStateService;

    public DailyRewardService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
    }

    public int CurrentDay
    {
        get
        {
            var streak = CurrentStreak;
            if (streak == 0) return 1;
            // Cycle through days 1-7
            return ((streak - 1) % 7) + 1;
        }
    }

    public int CurrentStreak => _gameStateService.State.DailyRewardStreak;

    public bool IsRewardAvailable
    {
        get
        {
            var lastClaim = _gameStateService.State.LastDailyRewardClaim;
            if (lastClaim == DateTime.MinValue)
                return true; // Never claimed before

            var today = DateTime.Now.Date;
            var lastClaimDate = lastClaim.ToLocalTime().Date;

            return today > lastClaimDate;
        }
    }

    public DailyReward? TodaysReward
    {
        get
        {
            if (!IsRewardAvailable)
                return null;

            var rewards = DailyReward.GetRewardSchedule();
            return rewards[CurrentDay - 1];
        }
    }

    public List<DailyReward> GetRewardCycle()
    {
        var rewards = DailyReward.GetRewardSchedule();
        var currentDay = CurrentDay;
        var isAvailable = IsRewardAvailable;

        foreach (var reward in rewards)
        {
            reward.IsToday = reward.Day == currentDay && isAvailable;
            reward.IsClaimed = reward.Day < currentDay ||
                              (reward.Day == currentDay && !isAvailable);
        }

        return rewards;
    }

    public DailyReward? ClaimReward()
    {
        if (!IsRewardAvailable)
            return null;

        var reward = TodaysReward;
        if (reward == null)
            return null;

        var state = _gameStateService.State;

        // Check if streak was broken (missed more than 1 day)
        if (WasStreakBroken())
        {
            state.DailyRewardStreak = 1;
        }
        else
        {
            state.DailyRewardStreak++;
        }

        // Apply rewards
        _gameStateService.AddMoney(reward.Money);

        if (reward.Xp > 0)
        {
            _gameStateService.AddXp(reward.Xp);
        }

        if (reward.GoldenScrews > 0)
        {
            _gameStateService.AddGoldenScrews(reward.GoldenScrews);
        }

        // Apply bonus if any
        if (reward.BonusType == DailyBonusType.SpeedBoost)
        {
            state.SpeedBoostEndTime = DateTime.UtcNow.AddHours(1);
        }
        else if (reward.BonusType == DailyBonusType.XpBoost)
        {
            state.XpBoostEndTime = DateTime.UtcNow.AddHours(1);
        }

        // Update last claim time
        state.LastDailyRewardClaim = DateTime.UtcNow;

        return reward;
    }

    public bool WasStreakBroken()
    {
        var lastClaim = _gameStateService.State.LastDailyRewardClaim;
        if (lastClaim == DateTime.MinValue)
            return false; // First time claiming

        var today = DateTime.Now.Date;
        var lastClaimDate = lastClaim.ToLocalTime().Date;
        var daysSinceLastClaim = (today - lastClaimDate).Days;

        // Streak is broken if more than 1 day has passed
        return daysSinceLastClaim > 1;
    }

    public TimeSpan TimeUntilNextReward
    {
        get
        {
            if (IsRewardAvailable)
                return TimeSpan.Zero;

            var lastClaim = _gameStateService.State.LastDailyRewardClaim;
            var nextRewardTime = lastClaim.ToLocalTime().Date.AddDays(1);
            var timeUntil = nextRewardTime - DateTime.Now;

            return timeUntil > TimeSpan.Zero ? timeUntil : TimeSpan.Zero;
        }
    }
}
