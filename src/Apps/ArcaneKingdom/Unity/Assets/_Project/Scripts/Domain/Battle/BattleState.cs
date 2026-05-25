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
        public int PlayerMana { get; set; }
        public int EnemyMana { get; set; }
        public int PlayerMaxMana { get; set; }
        public int EnemyMaxMana { get; set; }
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
