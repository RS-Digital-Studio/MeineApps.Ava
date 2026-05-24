#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Thief
{
    /// <summary>
    /// Dieb-Typen (DESIGN.md Kap. 10.2).
    /// </summary>
    public enum ThiefType
    {
        Mysterious = 0,
        Elite = 1,
        Legendary = 2
    }

    /// <summary>
    /// Aktiver Dieb auf einem Server. Wird per Photon-Room live synchronisiert (HP-Balken,
    /// Top-Attackers). Sieg-Bedingung: HP <= 0 oder Timer abgelaufen.
    /// </summary>
    [Serializable]
    public sealed class ActiveThief
    {
        public string Id { get; }
        public ThiefType Type { get; }
        public int Level { get; }
        public long MaxHealth { get; }
        public long CurrentHealth { get; private set; }
        public DateTime SpawnedAtUtc { get; }
        public DateTime FleesAtUtc { get; }
        public string DiscoveredByPlayerId { get; }
        public List<ThiefAttackRecord> Attacks { get; }

        public ActiveThief(string id, ThiefType type, int level, long maxHealth,
                           DateTime spawnedAtUtc, DateTime fleesAtUtc, string discoveredByPlayerId)
        {
            Id = id;
            Type = type;
            Level = level;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            SpawnedAtUtc = spawnedAtUtc;
            FleesAtUtc = fleesAtUtc;
            DiscoveredByPlayerId = discoveredByPlayerId;
            Attacks = new List<ThiefAttackRecord>();
        }

        public bool IsAlive => CurrentHealth > 0 && DateTime.UtcNow < FleesAtUtc;
        public float HealthPercent => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;

        public void ApplyDamage(string playerId, long damage)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            CurrentHealth = Math.Max(0, CurrentHealth - damage);
            Attacks.Add(new ThiefAttackRecord(playerId, damage, DateTime.UtcNow));
        }

        /// <summary>
        /// Berechnet den Schadensanteil eines Spielers — Basis fuer Belohnungs-Verteilung
        /// (DESIGN.md Kap. 10.4).
        /// </summary>
        public float ContributionShare(string playerId)
        {
            if (MaxHealth == 0) return 0f;
            long ownDamage = 0;
            long totalDamage = 0;
            foreach (var a in Attacks)
            {
                totalDamage += a.Damage;
                if (a.PlayerId == playerId) ownDamage += a.Damage;
            }
            return totalDamage == 0 ? 0f : (float)ownDamage / totalDamage;
        }
    }

    [Serializable]
    public readonly struct ThiefAttackRecord
    {
        public string PlayerId { get; }
        public long Damage { get; }
        public DateTime TimestampUtc { get; }

        public ThiefAttackRecord(string playerId, long damage, DateTime timestampUtc)
        {
            PlayerId = playerId;
            Damage = damage;
            TimestampUtc = timestampUtc;
        }
    }

    /// <summary>
    /// Belohnungs-Tier nach Schadensanteil (DESIGN.md Kap. 10.4).
    /// </summary>
    public enum ThiefRewardTier
    {
        Pity = 0,           // < 1 %
        Basic = 1,          // 1-5 %
        Standard = 2,       // 5-10 %
        Increased = 3,      // 10-25 %
        Premium = 4,        // 25-50 %
        TopAttacker = 5     // Top-1-Schaden
    }

    public static class ThiefRewardCalculator
    {
        public static ThiefRewardTier TierForShare(float share, bool isTopAttacker)
        {
            if (isTopAttacker) return ThiefRewardTier.TopAttacker;
            if (share < 0.01f) return ThiefRewardTier.Pity;
            if (share < 0.05f) return ThiefRewardTier.Basic;
            if (share < 0.10f) return ThiefRewardTier.Standard;
            if (share < 0.25f) return ThiefRewardTier.Increased;
            return ThiefRewardTier.Premium;
        }
    }
}
