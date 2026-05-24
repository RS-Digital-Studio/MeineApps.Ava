namespace ArcaneKingdom.Domain.Hero
{
    /// <summary>
    /// Typen der Helden-Faehigkeit (DESIGN.md Kap. 9.6). Magnitude/Dauer kommen
    /// aus der HeroDefinition, die Wirkung wird vom BattleEngine ausgeloest.
    /// </summary>
    public enum HeroFaehigkeitsTyp
    {
        /// <summary>Alle Allies bekommen +X % HP (Heilung).</summary>
        AllyHeal = 0,
        /// <summary>Gegner verliert X Mana sofort.</summary>
        EnemyManaBurn = 1,
        /// <summary>AoE-Schaden auf alle Gegner-Karten.</summary>
        AoeDamage = 2,
        /// <summary>Spielt eine zufaellige Karte aus dem Deck kostenlos.</summary>
        FreeCardCast = 3,
        /// <summary>Allies bekommen +X Mana sofort.</summary>
        AllyManaBoost = 4,
        /// <summary>Eigener Held zieht Karte + max Mana erhoeht sich temporaer.</summary>
        DrawAndManaBoost = 5
    }
}
