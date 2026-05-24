#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Quest
{
    /// <summary>
    /// Taeglicher Login-Bonus (DESIGN.md Kap. 16.1, Sieben-Tage-Zyklus aus Tempel).
    /// </summary>
    public sealed class DailyRewardService
    {
        public sealed class DailyRewardDay
        {
            public int Day { get; init; }
            public long Gold { get; init; }
            public long Diamonds { get; init; }
            public int CommonScraps { get; init; }
        }

        /// <summary>Pilot-Werte fuer den 7-Tage-Zyklus.</summary>
        public static readonly IReadOnlyList<DailyRewardDay> Cycle = new[]
        {
            new DailyRewardDay { Day = 1, Gold = 500,  Diamonds = 10, CommonScraps = 1 },
            new DailyRewardDay { Day = 2, Gold = 750,  Diamonds = 10, CommonScraps = 2 },
            new DailyRewardDay { Day = 3, Gold = 1000, Diamonds = 15, CommonScraps = 3 },
            new DailyRewardDay { Day = 4, Gold = 1500, Diamonds = 20, CommonScraps = 5 },
            new DailyRewardDay { Day = 5, Gold = 2000, Diamonds = 30, CommonScraps = 7 },
            new DailyRewardDay { Day = 6, Gold = 3000, Diamonds = 50, CommonScraps = 10 },
            new DailyRewardDay { Day = 7, Gold = 5000, Diamonds = 100, CommonScraps = 20 }
        };

        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private DateTime _lastClaimedUtc;
        private int _currentDay = 1;

        public DailyRewardService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
        }

        public bool CanClaimToday => DateTime.UtcNow.Date > _lastClaimedUtc.Date;

        public async UniTask<Result<DailyRewardDay>> ClaimAsync(CancellationToken ct = default)
        {
            if (!CanClaimToday) return Result<DailyRewardDay>.Failure("Heute schon eingeloest.");

            var resetCycle = (DateTime.UtcNow.Date - _lastClaimedUtc.Date).TotalDays > 1.5;
            if (resetCycle) _currentDay = 1;

            var reward = Cycle[Math.Min(_currentDay, Cycle.Count) - 1];
            await _save.MutateAsync(save =>
            {
                save.Currencies.AddGold(reward.Gold);
                save.Currencies.AddDiamond(reward.Diamonds);
                save.Currencies.AddScraps(Domain.Economy.ScrapType.Common, reward.CommonScraps);
                return save;
            }, ct);

            _lastClaimedUtc = DateTime.UtcNow;
            _analytics.Track("daily_reward_claimed", new Dictionary<string, object> { ["day"] = reward.Day });
            _currentDay = _currentDay >= Cycle.Count ? 1 : _currentDay + 1;
            GameLogger.Info("DailyReward", $"Day {reward.Day} eingeloest.");
            return Result<DailyRewardDay>.Success(reward);
        }
    }
}
