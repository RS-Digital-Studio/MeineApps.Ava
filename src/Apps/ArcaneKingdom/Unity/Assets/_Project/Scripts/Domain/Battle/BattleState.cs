#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Battle
{
    public enum BattlePhase
    {
        Setup = 0,
        PlayerTurn = 1,
        EnemyTurn = 2,
        TurnEnd = 3,
        Settlement = 4
    }

    public enum BattleResult
    {
        Undecided = 0,
        PlayerWins = 1,
        EnemyWins = 2,
        Draw = 3
    }

    /// <summary>
    /// Deterministischer Battle-State. Wird vom BattleEngine fortgeschritten.
    /// </summary>
    [Serializable]
    public sealed class BattleState
    {
        public int Seed { get; }
        public int CurrentTurn { get; set; }
        public int PlayerHeroHp { get; set; }
        public int EnemyHeroHp { get; set; }
        /// <summary>Maximales HP des Spieler-Helden (fuer 50%-Schwellen-Berechnung).</summary>
        public int PlayerHeroMaxHp { get; set; }
        /// <summary>Maximales HP des Gegner-Helden (fuer Boss-Phasen-Trigger).</summary>
        public int EnemyHeroMaxHp { get; set; }
        public int PlayerMana { get; set; }
        public int EnemyMana { get; set; }
        public int PlayerMaxMana { get; set; }
        public int EnemyMaxMana { get; set; }

        /// <summary>
        /// Markiert den Kampf als Boss-Encounter (Mini-Boss = Node-Index 5, World-Boss = Node-Index 10).
        /// Aktiviert die Boss-Phase-2-Mechanik (Spielplan v5 Kap. 9.4).
        /// </summary>
        public bool IsBossEncounter { get; set; }

        /// <summary>
        /// Karten-Definitionen die als Verstaerkung in Boss-Phase-2 ins Feld kommen
        /// (2-3 starke Karten laut Plan). Wird beim Setup gefuellt.
        /// </summary>
        public List<string> BossPhase2ReinforcementCardIds { get; } = new();

        /// <summary>
        /// Passive Faehigkeit, die in Boss-Phase-2 aktiviert wird
        /// (z.B. "Alle Karten +200 ATK"). Lokalisierungs-Key oder Beschreibung.
        /// </summary>
        public string? BossPhase2PassiveKey { get; set; }

        /// <summary>True wenn Boss-Phase-2 bereits ausgeloest wurde (verhindert doppelten Trigger).</summary>
        public bool BossPhase2Active { get; set; }
        public List<CardFieldSlot> PlayerField { get; }      // max. 5
        public List<CardFieldSlot> EnemyField { get; }       // max. 5
        public List<string> PlayerHand { get; }              // InstanceIds
        public List<string> EnemyHand { get; }
        public Queue<string> PlayerDeckQueue { get; }
        public Queue<string> EnemyDeckQueue { get; }
        public BattlePhase Phase { get; set; }
        public BattleResult Result { get; set; }

        /// <summary>
        /// Helden-Passiv-Kontext pro Seite (Designplan v4 Kap. 2.1). Wird beim Setup gefüllt.
        /// </summary>
        public HeroPassivContext? PlayerHeroPassiv { get; set; }
        public HeroPassivContext? EnemyHeroPassiv { get; set; }

        /// <summary>
        /// Karten-Persönlichkeit-Events (Designplan v4 Kap. 8) für UI/Animation/Replay.
        /// </summary>
        public List<BattleEvent> Events { get; }

        public BattleState(int seed, int playerHeroHp, int enemyHeroHp)
        {
            Seed = seed;
            PlayerHeroHp = playerHeroHp;
            EnemyHeroHp = enemyHeroHp;
            PlayerHeroMaxHp = playerHeroHp;
            EnemyHeroMaxHp = enemyHeroHp;
            PlayerMana = 3;
            EnemyMana = 3;
            PlayerMaxMana = 3;
            EnemyMaxMana = 3;
            PlayerField = new List<CardFieldSlot>(5);
            EnemyField = new List<CardFieldSlot>(5);
            PlayerHand = new List<string>(8);
            EnemyHand = new List<string>(8);
            PlayerDeckQueue = new Queue<string>();
            EnemyDeckQueue = new Queue<string>();
            Phase = BattlePhase.Setup;
            Result = BattleResult.Undecided;
            CurrentTurn = 1;
            Events = new List<BattleEvent>(64);
        }
    }

    /// <summary>
    /// Karten-Feld-Slot mit aktuellen Werten (ATK/HP können Buffs/Debuffs erlitten haben).
    /// </summary>
    [Serializable]
    public sealed class CardFieldSlot
    {
        public string CardInstanceId { get; }
        public int CurrentAttack { get; set; }
        public int CurrentHealth { get; set; }
        public int MaxHealth { get; set; }              // fuer Vorgaben wie "+5% HP" beim KoeniglicheAura
        public int TurnsUntilSpecial { get; set; }

        /// <summary>
        /// Aktive Status-Effekte auf dieser Karte (Designplan v4 Kap. 3.4):
        /// Schlaf, Stille, Einfrierung, Betaeubung, Vergiftung, Verbrennung, Verlangsamung, Verwurzelung.
        /// </summary>
        public List<StatusEffect> StatusEffects { get; } = new();

        public CardFieldSlot(string cardInstanceId, int currentAttack, int currentHealth, int turnsUntilSpecial)
        {
            CardInstanceId = cardInstanceId;
            CurrentAttack = currentAttack;
            CurrentHealth = currentHealth;
            MaxHealth = currentHealth;
            TurnsUntilSpecial = turnsUntilSpecial;
        }
    }
}
