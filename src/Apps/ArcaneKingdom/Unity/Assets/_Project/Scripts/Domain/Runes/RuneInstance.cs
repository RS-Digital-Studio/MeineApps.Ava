#nullable enable
using System;

namespace ArcaneKingdom.Domain.Runes
{
    /// <summary>
    /// Runtime-Instanz einer Rune. Veraenderlicher State: Level (1-10).
    /// </summary>
    [Serializable]
    public sealed class RuneInstance
    {
        public string InstanceId { get; }
        public string RuneDefinitionId { get; }
        public int Level { get; private set; }
        public DateTime ObtainedAtUtc { get; }

        public RuneInstance(string instanceId, string runeDefinitionId, int level, DateTime obtainedAtUtc)
        {
            InstanceId = instanceId;
            RuneDefinitionId = runeDefinitionId;
            Level = level;
            ObtainedAtUtc = obtainedAtUtc;
        }

        internal void ApplyLevelUp(int newLevel)
        {
            if (newLevel < Level) throw new InvalidOperationException("Level kann nicht sinken.");
            if (newLevel > 10) throw new ArgumentOutOfRangeException(nameof(newLevel), "Max-Rune-Level ist 10.");
            Level = newLevel;
        }
    }
}
