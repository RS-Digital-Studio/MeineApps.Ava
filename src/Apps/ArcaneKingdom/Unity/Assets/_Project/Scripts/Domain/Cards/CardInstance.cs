#nullable enable
using System;

namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Runtime-Instanz einer Karte im Spieler-Besitz. Haelt veränderlichen State
    /// (Level, EXP, Erwerbsdatum). Statische Daten kommen aus <see cref="CardDefinition"/>.
    /// </summary>
    [Serializable]
    public sealed class CardInstance
    {
        public string InstanceId { get; }            // Eindeutige Spieler-eigene ID
        public string CardDefinitionId { get; }       // Referenz auf CardDefinition.id
        public int Level { get; private set; }        // 0-15
        public int ExpWithinLevel { get; private set; }
        public DateTime ObtainedAtUtc { get; }

        public CardInstance(string instanceId, string cardDefinitionId, int level, int expWithinLevel, DateTime obtainedAtUtc)
        {
            InstanceId = instanceId;
            CardDefinitionId = cardDefinitionId;
            Level = level;
            ExpWithinLevel = expWithinLevel;
            ObtainedAtUtc = obtainedAtUtc;
        }

        public bool HasSecondAbilityUnlocked => Level >= 5;
        public bool HasThirdAbilityUnlocked => Level >= 10;
        public bool IsMaxLevel => Level >= 15;

        /// <summary>
        /// Berechnet den prozentualen ATK/HP-Bonus auf Basis des Levels (DESIGN.md 5.3).
        /// </summary>
        public float StatBonusMultiplier => Level switch
        {
            0 => 1.00f,
            1 => 1.05f,
            2 => 1.10f,
            3 => 1.15f,
            4 => 1.20f,
            5 => 1.25f,
            6 => 1.30f,
            7 => 1.35f,
            8 => 1.40f,
            9 => 1.50f,
            10 => 1.55f,
            11 => 1.58f,
            12 => 1.63f,
            13 => 1.68f,
            14 => 1.75f,
            15 => 1.80f,
            _ => 1.00f
        };

        /// <summary>
        /// Wendet einen Level-Up an. Voraussetzungen (Kopien, Steine, Gold) werden hier NICHT geprüft —
        /// das ist Aufgabe des Upgrade-Services in der Game-Assembly.
        /// </summary>
        internal void ApplyLevelUp(int newLevel)
        {
            if (newLevel < Level) throw new InvalidOperationException("Level kann nicht sinken.");
            if (newLevel > 15) throw new ArgumentOutOfRangeException(nameof(newLevel), "Max-Level ist 15.");
            Level = newLevel;
            ExpWithinLevel = 0;
        }

        /// <summary>Setzt das Level neu (vom CardUpgradeService aufgerufen, mit Pruefung).</summary>
        public void SetLevel(int newLevel) => ApplyLevelUp(newLevel);

        /// <summary>Setzt EXP-within-Level neu (vom CardUpgradeService oder beim Kampf-EXP-Reward).</summary>
        public void SetExpWithinLevel(int newExp)
        {
            if (newExp < 0) throw new ArgumentOutOfRangeException(nameof(newExp));
            ExpWithinLevel = newExp;
        }

        internal void AddExp(int delta)
        {
            if (delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            ExpWithinLevel += delta;
        }
    }
}
