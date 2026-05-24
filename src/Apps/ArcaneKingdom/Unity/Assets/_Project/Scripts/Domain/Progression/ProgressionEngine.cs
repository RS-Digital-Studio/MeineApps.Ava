#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Player;

namespace ArcaneKingdom.Domain.Progression
{
    /// <summary>
    /// Wendet EXP auf einen Spieler an und ermittelt alle dabei freigeschalteten Level-Ups
    /// inkl. zugehöriger Belohnungen. Pure C# — keine Side-Effects auf Save, nur Berechnung.
    /// Die Anwendung der Belohnungen erfolgt im ProgressionService (Game-Layer).
    /// </summary>
    public static class ProgressionEngine
    {
        public sealed class ProgressionResult
        {
            public int OldLevel { get; init; }
            public int NewLevel { get; init; }
            public long OldExpTotal { get; init; }
            public long NewExpTotal { get; init; }
            public IReadOnlyList<LevelUpReward> EarnedRewards { get; init; } = Array.Empty<LevelUpReward>();
            public bool LeveledUp => NewLevel > OldLevel;
        }

        /// <summary>
        /// Berechnet das neue Level + die Belohnungen für einen EXP-Zuwachs.
        /// </summary>
        public static ProgressionResult ApplyExp(PlayerProfile profile, long expDelta)
        {
            if (expDelta < 0) throw new ArgumentOutOfRangeException(nameof(expDelta), "EXP kann nicht negativ sein.");
            var oldLevel = profile.Level;
            var oldTotal = profile.ExpTotal;
            var newTotal = oldTotal + expDelta;
            var newLevel = PlayerLevelCurve.LevelForExp(newTotal);

            var earned = new List<LevelUpReward>();
            for (var lv = oldLevel + 1; lv <= newLevel; lv++)
            {
                if (LevelUpRewardTable.TryGet(lv, out var reward)) earned.Add(reward);
            }

            return new ProgressionResult
            {
                OldLevel = oldLevel,
                NewLevel = newLevel,
                OldExpTotal = oldTotal,
                NewExpTotal = newTotal,
                EarnedRewards = earned
            };
        }
    }
}
