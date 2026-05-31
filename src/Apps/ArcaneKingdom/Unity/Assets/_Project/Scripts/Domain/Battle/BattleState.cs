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
        /// <summary>Verfuegbare Mana-Orbs des Spielers in der laufenden Runde (Designplan v3 Kap. 7.3: 3/Runde, 1 pro Karte).</summary>
        public int PlayerMana { get; set; }
        public int EnemyMana { get; set; }
        /// <summary>Mana-Orbs pro Runde (Reset-Wert). Spec: konstant 3, kein Anstieg ueber Runden.</summary>
        public int PlayerMaxMana { get; set; }
        public int EnemyMaxMana { get; set; }

        /// <summary>
        /// Anzahl in der laufenden Runde bereits eingesetzter Karten je Seite.
        /// Designplan v3 Kap. 7.3: Karten mit COST &gt; 30 koennen nur eingesetzt werden,
        /// wenn in diesem Zug noch nichts anderes gespielt wurde.
        /// </summary>
        public int PlayerCardsPlayedThisTurn { get; set; }
        public int EnemyCardsPlayedThisTurn { get; set; }

        /// <summary>
        /// Stat-Multiplier fuer Gegner-Karten nach Schwierigkeit (Spielplan v5 Kap. 8.3:
        /// Classic 1.0 / Amateur 1.25 / Profi 1.6 / Gott 2.2). Wird beim Einsetzen jeder
        /// Gegner-Karte auf ATK/HP angewandt (nicht auf das Spieler-Feld).
        /// </summary>
        public float EnemyStatMultiplier { get; set; } = 1.0f;

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
        /// Aggregierte Deck-Runen-Boni (Spielplan v5 Kap. 7.2). Vom BattleBootstrap vorberechnet,
        /// in Setup/PlayCard/EndTurn angewandt. Null = keine Runen. NICHT im Replay-Snapshot
        /// (Serializer ist DTO-basiert) — Boni werden in HeroHp/Field-Werte eingebrannt.
        /// EnemyRuneLoadout ist Phase 1 immer null (symmetrisch fuer spaeteren PvP).
        /// </summary>
        public ArcaneKingdom.Domain.Runes.RuneLoadout? PlayerRuneLoadout { get; set; }
        public ArcaneKingdom.Domain.Runes.RuneLoadout? EnemyRuneLoadout { get; set; }

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
