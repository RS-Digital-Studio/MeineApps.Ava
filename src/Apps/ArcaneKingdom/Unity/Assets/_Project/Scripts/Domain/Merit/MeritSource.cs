#nullable enable
namespace ArcaneKingdom.Domain.Merit
{
    /// <summary>
    /// Quellen fuer Merit-Punkte (Spielplan v5 Kap. 15.2).
    /// Jede Quelle hat eigene Vergabe-Logik und ein eigenes Tagesmaximum.
    /// </summary>
    public enum MeritSource
    {
        /// <summary>Taegliche Quest abgeschlossen (10-50 Merit pro Quest).</summary>
        DailyQuest = 0,
        /// <summary>Arena-Kampf bestritten (Sieg: 25, Niederlage: 5).</summary>
        ArenaBattle = 1,
        /// <summary>Dieb angreifen + Schaden machen (1 Merit pro 1000 Schaden, max 200/Dieb).</summary>
        ThiefAttack = 2,
        /// <summary>Gilden-Beitrag (Tech-Spende = 5 Merit, Klan-Match-Sieg = 50).</summary>
        GuildContribution = 3,
        /// <summary>Event absolviert (Event-spezifisch, 50-500 Merit).</summary>
        EventCompleted = 4,
        /// <summary>Welt-Boss besiegt (Boss-LV5: 100, Boss-LV10: 250).</summary>
        WorldBossDefeated = 5,
        /// <summary>Saison-Pass-Stufe erreicht (10 Merit pro Stufe).</summary>
        SaisonPassTier = 6,
        /// <summary>Achievement freigeschaltet (variabel).</summary>
        Achievement = 7
    }

    /// <summary>
    /// Standard-Werte pro Quelle. Server kann diese ueberschreiben.
    /// </summary>
    public static class MeritRewardTable
    {
        public const long DailyQuestMin = 10;
        public const long DailyQuestMax = 50;
        public const long ArenaWin = 25;
        public const long ArenaLoss = 5;
        public const long ThiefPerThousandDmg = 1;
        public const long ThiefPerEncounterCap = 200;
        public const long GuildTechDonation = 5;
        public const long GuildClanMatchWin = 50;
        public const long WorldBoss5 = 100;
        public const long WorldBoss10 = 250;
        public const long SaisonPassTierStep = 10;

        /// <summary>Maximum-Wert eines Merit-Kontos (Spielplan v5 zeigt 199.999 als Cap).</summary>
        public const long MeritAccountCap = 199_999;
    }
}
