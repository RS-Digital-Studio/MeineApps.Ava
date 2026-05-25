#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Thief
{
    /// <summary>
    /// Belohnungs-Payload fuer einen Dieb-Angreifer (Spielplan v5 Kap. 10.4).
    /// Wird vom Server (oder Mock) berechnet und an den Client zurueckgegeben.
    /// </summary>
    public sealed class ThiefReward
    {
        public ThiefRewardTier Tier { get; init; }
        public long Gold { get; init; }
        public int Diamonds { get; init; }
        public List<string> CardFragmentIds { get; init; } = new();
        public List<string> CardIds { get; init; } = new();
        public bool LastHitBonus { get; init; }
        public bool DiscoveryBonus { get; init; }
    }

    /// <summary>
    /// Service der die Dieb-Belohnungen berechnet — pure Logik, ohne Side-Effects.
    /// Server-Modus (Photon/Firebase): Logik bleibt identisch, nur State kommt vom Server.
    /// </summary>
    public sealed class ThiefService
    {
        /// <summary>
        /// Berechnet die Belohnung fuer einen Spieler nach einem Dieb-Kampf.
        /// </summary>
        public ThiefReward ComputeReward(ActiveThief thief, string playerId)
        {
            if (thief is null) throw new ArgumentNullException(nameof(thief));
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("playerId leer", nameof(playerId));

            var share = thief.ContributionShare(playerId);
            var isTopAttacker = IsTopAttacker(thief, playerId);
            var tier = ThiefRewardCalculator.TierForShare(share, isTopAttacker);

            var isLastHit = thief.Attacks.Count > 0
                            && thief.Attacks.Last().PlayerId == playerId
                            && thief.CurrentHealth == 0;
            var isDiscoverer = thief.DiscoveredByPlayerId == playerId;

            return BuildReward(thief.Type, tier, isLastHit, isDiscoverer);
        }

        /// <summary>
        /// Last-Hit-Erkennung: Spieler ist der letzte Angreifer UND der Dieb ist tot.
        /// </summary>
        public bool IsLastHit(ActiveThief thief, string playerId) =>
            thief.Attacks.Count > 0
            && thief.Attacks.Last().PlayerId == playerId
            && thief.CurrentHealth == 0;

        /// <summary>
        /// Top-Attacker: Hoechster Schadensbeitrag aller Spieler.
        /// </summary>
        public bool IsTopAttacker(ActiveThief thief, string playerId)
        {
            if (thief.Attacks.Count == 0) return false;
            var byPlayer = thief.Attacks
                .GroupBy(a => a.PlayerId)
                .Select(g => new { PlayerId = g.Key, Total = g.Sum(a => a.Damage) })
                .OrderByDescending(x => x.Total)
                .ToList();
            return byPlayer.Count > 0 && byPlayer[0].PlayerId == playerId;
        }

        /// <summary>
        /// Anzahl unterschiedlicher Angreifer (fuer UI-Anzeige "X Kaempfe wurden gefuehrt").
        /// </summary>
        public int AttackCount(ActiveThief thief) => thief.Attacks.Count;

        // ============================================================================
        // Belohnungs-Tabellen — Plan-Werte aus Spielplan v5 Kap. 10.3
        // ============================================================================

        private static ThiefReward BuildReward(ThiefType type, ThiefRewardTier tier, bool lastHit, bool discoverer)
        {
            var reward = new ThiefReward
            {
                Tier = tier,
                LastHitBonus = lastHit,
                DiscoveryBonus = discoverer
            };

            // Basis-Belohnung nach Dieb-Typ + Tier
            var (gold, diamonds, fragments) = (type, tier) switch
            {
                (ThiefType.Mysterious, ThiefRewardTier.Pity)        => (200L, 0, 0),
                (ThiefType.Mysterious, ThiefRewardTier.Basic)       => (500L, 0, 1),
                (ThiefType.Mysterious, ThiefRewardTier.Standard)    => (1_000L, 1, 2),
                (ThiefType.Mysterious, ThiefRewardTier.Increased)   => (2_500L, 2, 3),
                (ThiefType.Mysterious, ThiefRewardTier.Premium)     => (5_000L, 5, 5),
                (ThiefType.Mysterious, ThiefRewardTier.TopAttacker) => (8_000L, 10, 8),
                (ThiefType.Elite,      ThiefRewardTier.Pity)        => (500L, 1, 1),
                (ThiefType.Elite,      ThiefRewardTier.Basic)       => (1_500L, 2, 2),
                (ThiefType.Elite,      ThiefRewardTier.Standard)    => (3_000L, 5, 3),
                (ThiefType.Elite,      ThiefRewardTier.Increased)   => (7_000L, 10, 5),
                (ThiefType.Elite,      ThiefRewardTier.Premium)     => (15_000L, 20, 8),
                (ThiefType.Elite,      ThiefRewardTier.TopAttacker) => (25_000L, 35, 12),
                (ThiefType.Legendary,  ThiefRewardTier.Pity)        => (2_000L, 5, 2),
                (ThiefType.Legendary,  ThiefRewardTier.Basic)       => (5_000L, 10, 3),
                (ThiefType.Legendary,  ThiefRewardTier.Standard)    => (12_000L, 20, 5),
                (ThiefType.Legendary,  ThiefRewardTier.Increased)   => (30_000L, 40, 8),
                (ThiefType.Legendary,  ThiefRewardTier.Premium)     => (75_000L, 80, 12),
                (ThiefType.Legendary,  ThiefRewardTier.TopAttacker) => (150_000L, 150, 20),
                _ => (0L, 0, 0)
            };

            reward = new ThiefReward
            {
                Tier = tier,
                Gold = gold,
                Diamonds = diamonds,
                LastHitBonus = lastHit,
                DiscoveryBonus = discoverer
            };

            // Last-Hit-Bonus: +50% Gold + Bonus-Fragment
            if (lastHit)
            {
                reward = new ThiefReward
                {
                    Tier = reward.Tier,
                    Gold = (long)(reward.Gold * 1.5),
                    Diamonds = reward.Diamonds + 2,
                    LastHitBonus = true,
                    DiscoveryBonus = reward.DiscoveryBonus
                };
            }

            // Discovery-Bonus: +25% Gold
            if (discoverer)
            {
                reward = new ThiefReward
                {
                    Tier = reward.Tier,
                    Gold = (long)(reward.Gold * 1.25),
                    Diamonds = reward.Diamonds + 1,
                    LastHitBonus = reward.LastHitBonus,
                    DiscoveryBonus = true
                };
            }

            return reward;
        }

        /// <summary>
        /// Generiert einen Mock-Dieb fuer Tests/Offline-Modus.
        /// Real-Spawning passiert serverseitig via Photon-Room.
        /// </summary>
        public ActiveThief SpawnMockThief(ThiefType type, int level, string discoveredBy, TimeSpan timeUntilFlees)
        {
            var maxHp = type switch
            {
                ThiefType.Mysterious => 5_000L + level * 1_000L,
                ThiefType.Elite      => 30_000L + level * 5_000L,
                ThiefType.Legendary  => 100_000L + level * 20_000L,
                _ => 5_000L
            };
            return new ActiveThief(
                id: Guid.NewGuid().ToString("N"),
                type: type,
                level: level,
                maxHealth: maxHp,
                spawnedAtUtc: DateTime.UtcNow,
                fleesAtUtc: DateTime.UtcNow + timeUntilFlees,
                discoveredByPlayerId: discoveredBy);
        }
    }
}
