#nullable enable
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Thief;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Thief
{
    /// <summary>
    /// Dieb-Event-Steuerung. Subscribed im Hintergrund auf Photon-Spawn-Events,
    /// orchestriert eigenen Angriff und verteilt Belohnungen nach Kampf.
    /// </summary>
    public sealed class ThiefController
    {
        public sealed class AttackResult
        {
            public bool Success { get; init; }
            public long DamageDealt { get; init; }
            public bool ThiefKilled { get; init; }
            public string? Error { get; init; }
        }

        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly Domain.Config.BalancingConfig _config;
        private readonly Dictionary<string, int> _attacksByThiefId = new();

        public ThiefController(ISaveService<PlayerSave> save, IAnalyticsService analytics, Domain.Config.BalancingConfig config)
        {
            _save = save;
            _analytics = analytics;
            _config = config;
        }

        public async UniTask<AttackResult> AttackAsync(ActiveThief thief, long damage, CancellationToken ct = default)
        {
            if (!thief.IsAlive) return new AttackResult { Success = false, Error = "Dieb bereits geflohen / besiegt." };

            var attackCount = _attacksByThiefId.TryGetValue(thief.Id, out var c) ? c : 0;
            if (attackCount >= _config.ThiefMaxAttacksPerPlayer)
                return new AttackResult { Success = false, Error = $"Max {_config.ThiefMaxAttacksPerPlayer} Angriffe pro Dieb erreicht." };

            var energyOk = false;
            await _save.MutateAsync(s => { energyOk = s.Currencies.SpendEnergy(_config.EnergyCostThiefAttack); return s; }, ct);
            if (!energyOk) return new AttackResult { Success = false, Error = "Nicht genug Energie." };

            // TODO: Photon RPC zum Dieb-Room — Server-validated Damage.
            thief.ApplyDamage(_authPlaceholderUserId(), damage);
            _attacksByThiefId[thief.Id] = attackCount + 1;

            _analytics.Track("thief_attack", new Dictionary<string, object>
            {
                ["thief_type"] = thief.Type.ToString(),
                ["damage"] = damage,
                ["thief_hp_pct"] = thief.HealthPercent
            });

            var killed = thief.CurrentHealth == 0;
            if (killed)
            {
                await AwardKillRewardsAsync(thief, ct);
                _analytics.Track("thief_killed", new Dictionary<string, object> { ["thief_id"] = thief.Id, ["type"] = thief.Type.ToString() });
            }

            return new AttackResult { Success = true, DamageDealt = damage, ThiefKilled = killed };
        }

        private async UniTask AwardKillRewardsAsync(ActiveThief thief, CancellationToken ct)
        {
            var userId = _authPlaceholderUserId();
            var share = thief.ContributionShare(userId);
            var topAttacker = IsTopAttacker(thief, userId);
            var tier = ThiefRewardCalculator.TierForShare(share, topAttacker);

            await _save.MutateAsync(save =>
            {
                // Pilot-Belohnungen — werden vom Backend in Production ausgegeben.
                var (gold, scraps) = tier switch
                {
                    ThiefRewardTier.Pity         => (50L, 1L),
                    ThiefRewardTier.Basic        => (150L, 3L),
                    ThiefRewardTier.Standard     => (500L, 5L),
                    ThiefRewardTier.Increased    => (1500L, 10L),
                    ThiefRewardTier.Premium      => (5000L, 25L),
                    ThiefRewardTier.TopAttacker  => (15000L, 50L),
                    _ => (0L, 0L)
                };
                save.Currencies.AddGold(gold);
                save.Currencies.AddScraps(Domain.Economy.ScrapType.Common, scraps);
                save.Currencies.AddMeritPoints(thief.Type switch
                {
                    ThiefType.Mysterious => 20,
                    ThiefType.Elite => 100,
                    ThiefType.Legendary => 500,
                    _ => 0
                });
                return save;
            }, ct);

            GameLogger.Info("Thief", $"Reward: Tier={tier} Share={share:P1} TopAttacker={topAttacker}");
        }

        private static bool IsTopAttacker(ActiveThief thief, string playerId)
        {
            var topId = string.Empty;
            long topDamage = 0;
            var sums = new Dictionary<string, long>();
            foreach (var a in thief.Attacks)
            {
                if (!sums.ContainsKey(a.PlayerId)) sums[a.PlayerId] = 0;
                sums[a.PlayerId] += a.Damage;
                if (sums[a.PlayerId] > topDamage) { topDamage = sums[a.PlayerId]; topId = a.PlayerId; }
            }
            return topId == playerId;
        }

        // Wird durch echten Auth-Service-Zugriff ersetzt sobald die Controller integriert sind.
        private static string _authPlaceholderUserId() => "current-user";
    }
}
