namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Auto-Battle-Progression nach Spieler-Level (Designplan v4 Kap. 6).
    /// Auto-Battle wird schrittweise freigeschaltet — soll Casual-Spieler beim Farmen entlasten,
    /// neue Spieler aber zunaechst manuell durch das Spiel fuehren.
    /// </summary>
    public static class AutoBattleProgression
    {
        /// <summary>Spieler-Level ab dem Auto-Battle ueberhaupt verfuegbar ist.</summary>
        public const int AutoBattleUnlockLevel = 10;

        /// <summary>Spieler-Level ab dem 2x-Geschwindigkeit freigeschaltet wird.</summary>
        public const int Speed2xUnlockLevel = 20;

        /// <summary>Spieler-Level ab dem 3x-Geschwindigkeit freigeschaltet wird.</summary>
        public const int Speed3xUnlockLevel = 30;

        /// <summary>Spieler-Level ab dem 4x-Geschwindigkeit (MAX) freigeschaltet wird.</summary>
        public const int Speed4xUnlockLevel = 50;

        /// <summary>
        /// Liefert die maximal moegliche Auto-Battle-Geschwindigkeit fuer das angegebene Spieler-Level.
        /// 0 = Auto-Battle deaktiviert (LV 1–9).
        /// 1 = 1x (LV 10–19).
        /// 2 = 2x (LV 20–29).
        /// 3 = 3x (LV 30–49).
        /// 4 = 4x MAX (LV 50+).
        /// </summary>
        public static int GetMaxAutoBattleSpeed(int playerLevel)
        {
            if (playerLevel < AutoBattleUnlockLevel) return 0;
            if (playerLevel < Speed2xUnlockLevel)    return 1;
            if (playerLevel < Speed3xUnlockLevel)    return 2;
            if (playerLevel < Speed4xUnlockLevel)    return 3;
            return 4;
        }

        /// <summary>
        /// Liefert true, wenn Auto-Battle fuer diesen Boss-Kampf zulaessig ist
        /// (Designplan v4 Kap. 6 Sonderregel: erste Begegnung mit Boss = manuell).
        /// </summary>
        public static bool IsAutoBattleAllowedForBoss(int playerLevel, bool isBossNode, bool hasBeatenBossOnce)
        {
            if (playerLevel < AutoBattleUnlockLevel) return false;
            if (isBossNode && !hasBeatenBossOnce) return false;
            return true;
        }
    }
}
