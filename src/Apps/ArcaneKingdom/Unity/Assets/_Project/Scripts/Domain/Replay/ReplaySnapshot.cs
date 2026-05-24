#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Battle;

namespace ArcaneKingdom.Domain.Replay
{
    /// <summary>
    /// Snapshot eines BattleStates pro Runde — Basis fuer deterministische Replays
    /// (DESIGN.md Kap. 13.1). Pro Snapshot wird der minimal noetige State persistiert,
    /// damit der BattleEngine ihn aus dem Seed reproduzieren kann.
    /// </summary>
    [Serializable]
    public sealed class ReplaySnapshot
    {
        public int Turn { get; init; }
        public int PlayerHeroHp { get; init; }
        public int EnemyHeroHp { get; init; }
        public int PlayerMana { get; init; }
        public int EnemyMana { get; init; }
        public BattlePhase Phase { get; init; }
        public List<ReplayFieldSnapshot> PlayerField { get; init; } = new();
        public List<ReplayFieldSnapshot> EnemyField { get; init; } = new();
        public DateTime CapturedAtUtc { get; init; }
    }

    [Serializable]
    public sealed class ReplayFieldSnapshot
    {
        public string CardInstanceId { get; init; } = string.Empty;
        public int CurrentAttack { get; init; }
        public int CurrentHealth { get; init; }
        public int TurnsUntilSpecial { get; init; }
    }

    /// <summary>
    /// Replay-Datei (eine pro abgeschlossenem Kampf — z.B. Klan-Match oder Arena).
    /// </summary>
    [Serializable]
    public sealed class ReplayFile
    {
        public string ReplayId { get; init; } = string.Empty;
        public int Seed { get; init; }
        public string MatchType { get; init; } = string.Empty;
        public string PlayerAId { get; init; } = string.Empty;
        public string PlayerBId { get; init; } = string.Empty;
        public DateTime StartedAtUtc { get; init; }
        public DateTime? EndedAtUtc { get; set; }
        public BattleResult Result { get; set; }
        public List<ReplaySnapshot> Snapshots { get; } = new();

        /// <summary>
        /// Aufbewahrung 30 Tage gem. DESIGN 13.1.
        /// </summary>
        public bool IsExpired => EndedAtUtc.HasValue && (DateTime.UtcNow - EndedAtUtc.Value).TotalDays > 30;
    }
}
