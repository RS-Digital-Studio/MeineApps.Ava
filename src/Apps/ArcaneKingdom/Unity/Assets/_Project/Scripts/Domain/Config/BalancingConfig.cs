#nullable enable
using ArcaneKingdom.Domain.Cards;
using UnityEngine;

namespace ArcaneKingdom.Domain.Config
{
    /// <summary>
    /// Globale Balancing-Konstanten als ScriptableObject. Wird über Firebase Remote Config
    /// für Live-Anpassung gespiegelt — Werte hier sind die Pilot-Defaults aus DESIGN.md (v6).
    /// </summary>
    [CreateAssetMenu(menuName = "ArcaneKingdom/Config/Balancing", fileName = "BalancingConfig")]
    public sealed class BalancingConfig : ScriptableObject
    {
        [Header("Energie")]
        [SerializeField, Min(1)] private int energyCapDefault = 60;
        [SerializeField, Min(1)] private int energyRegenSeconds = 360;       // 6 Minuten / 1 Energie
        [SerializeField, Min(0)] private int energyCostNormalNode = 1;
        [SerializeField, Min(0)] private int energyCostMiniBoss = 2;
        [SerializeField, Min(0)] private int energyCostWorldBoss = 3;
        [SerializeField, Min(0)] private int energyCostArena = 5;
        [SerializeField, Min(0)] private int energyCostThiefAttack = 5;

        [Header("Spieler-Level-Cap")]
        [SerializeField, Min(1)] private int playerLevelSoftCap = 150;

        [Header("Karten-System")]
        [SerializeField, Min(1)] private int maxCardsPerDeck = 10;
        [SerializeField, Min(1)] private int defaultDeckSlots = 3;
        [SerializeField, Min(1)] private int maxDeckSlots = 6;

        [Header("Karten-Besitz-Limits pro Rarity (Designplan v4 Oeko Kap. 1.1)")]
        [Tooltip("1★ Gewoehnlich: unbegrenzt (0 = unlimited)")]
        [SerializeField, Min(0)] private int maxCopies1Star = 0;
        [Tooltip("2★ Ungewoehnlich: unbegrenzt (0 = unlimited)")]
        [SerializeField, Min(0)] private int maxCopies2Star = 0;
        [Tooltip("3★ Selten: max. 5 (ab dann werden Drops in Scraps konvertiert)")]
        [SerializeField, Min(1)] private int maxCopies3Star = 5;
        [Tooltip("4★ Epic: max. 3")]
        [SerializeField, Min(1)] private int maxCopies4Star = 3;
        [Tooltip("5★ Legendaer: max. 2")]
        [SerializeField, Min(1)] private int maxCopies5Star = 2;
        [Tooltip("6★ Mythisch: max. 1 (einzigartig, keine Duplikate)")]
        [SerializeField, Min(1)] private int maxCopies6Star = 1;

        [Header("Kampf")]
        [SerializeField, Min(10)] private int maxBattleTurns = 50;
        [SerializeField, Min(1)] private int startManaPerSide = 3;
        [SerializeField, Min(1)] private int maxMana = 10;
        [SerializeField, Min(1)] private int maxFieldSlots = 5;
        [SerializeField, Min(1)] private int maxHandSize = 5;
        [SerializeField, Min(1)] private int startHandSize = 4;

        [Header("Arena")]
        [SerializeField, Min(10)] private int matchmakingRatingWindow = 150;
        [SerializeField, Min(1)] private int arenaMatchCooldownSeconds = 30;
        [SerializeField, Min(1)] private int arenaSeasonDays = 30;

        [Header("Gilde")]
        [SerializeField, Min(1)] private int guildMinPlayerLevel = 25;
        [SerializeField, Min(1)] private long guildFoundationCost = 50_000L;
        [SerializeField, Min(1)] private int guildInactivityKickDays = 7;

        [Header("Dieb")]
        [SerializeField, Min(1)] private int thiefSpawnIntervalHoursMin = 4;
        [SerializeField, Min(1)] private int thiefSpawnIntervalHoursMax = 6;
        [SerializeField, Min(1)] private int thiefActiveHours = 2;
        [SerializeField, Min(1)] private int thiefMaxAttacksPerPlayer = 10;

        public int EnergyCapDefault => energyCapDefault;
        public int EnergyRegenSeconds => energyRegenSeconds;
        public int EnergyCostNormalNode => energyCostNormalNode;
        public int EnergyCostMiniBoss => energyCostMiniBoss;
        public int EnergyCostWorldBoss => energyCostWorldBoss;
        public int EnergyCostArena => energyCostArena;
        public int EnergyCostThiefAttack => energyCostThiefAttack;
        public int PlayerLevelSoftCap => playerLevelSoftCap;
        public int MaxCardsPerDeck => maxCardsPerDeck;
        public int DefaultDeckSlots => defaultDeckSlots;
        public int MaxDeckSlots => maxDeckSlots;
        public int MaxBattleTurns => maxBattleTurns;
        public int StartManaPerSide => startManaPerSide;
        public int MaxMana => maxMana;
        public int MaxFieldSlots => maxFieldSlots;
        public int MaxHandSize => maxHandSize;
        public int StartHandSize => startHandSize;
        public int MatchmakingRatingWindow => matchmakingRatingWindow;
        public int ArenaMatchCooldownSeconds => arenaMatchCooldownSeconds;
        public int ArenaSeasonDays => arenaSeasonDays;
        public int GuildMinPlayerLevel => guildMinPlayerLevel;
        public long GuildFoundationCost => guildFoundationCost;
        public int GuildInactivityKickDays => guildInactivityKickDays;
        public int ThiefSpawnIntervalHoursMin => thiefSpawnIntervalHoursMin;
        public int ThiefSpawnIntervalHoursMax => thiefSpawnIntervalHoursMax;
        public int ThiefActiveHours => thiefActiveHours;
        public int ThiefMaxAttacksPerPlayer => thiefMaxAttacksPerPlayer;

        /// <summary>
        /// Maximale Karten-Kopien pro Seltenheit (Designplan v4 Oeko Kap. 1.1).
        /// Rückgabewert 0 = unlimited (für 1★ und 2★).
        /// </summary>
        public int GetMaxCopies(Rarity rarity) => rarity switch
        {
            Rarity.Gewoehnlich   => maxCopies1Star,
            Rarity.Ungewoehnlich => maxCopies2Star,
            Rarity.Selten        => maxCopies3Star,
            Rarity.Epic          => maxCopies4Star,
            Rarity.Legendaer     => maxCopies5Star,
            Rarity.Mythisch      => maxCopies6Star,
            _                    => 0
        };

        /// <summary>Liefert true, wenn die Seltenheit unbegrenzte Kopien erlaubt.</summary>
        public bool IsCopyLimitUnlimited(Rarity rarity) => GetMaxCopies(rarity) == 0;
    }
}
