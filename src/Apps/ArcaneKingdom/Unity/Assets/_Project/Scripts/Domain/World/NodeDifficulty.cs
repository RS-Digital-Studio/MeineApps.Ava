#nullable enable
namespace ArcaneKingdom.Domain.World
{
    /// <summary>
    /// Schwierigkeitsstufe eines Welt-Nodes (Spielplan v5 Kap. 8.3).
    /// Der Spieler waehlt vor dem Kampf eine Stufe; jede Stufe hat
    /// eigene Energie-Kosten, Belohnungs-Stufe und Gegner-Skalierung.
    /// </summary>
    public enum NodeDifficulty
    {
        /// <summary>1 Stern — Basis-Deck des Gegners, 1 Energie.</summary>
        Classic = 1,
        /// <summary>2 Sterne — Mehr HP/staerkere Karten, 1 Energie.</summary>
        Amateur = 2,
        /// <summary>3 Sterne — Spezialfaehigkeiten aktiv, 2 Energie.</summary>
        Profi = 3,
        /// <summary>4 Sterne — Elite-Deck/Phasen-Boss, 3 Energie. Boss-LV5 = Epic-Belohnung, Boss-LV10 = Legendaer.</summary>
        Gott = 4
    }

    /// <summary>
    /// Hilfs-Methoden fuer NodeDifficulty (Energie-Kosten, Stats-Multiplier).
    /// </summary>
    public static class NodeDifficultyHelpers
    {
        /// <summary>
        /// Energie-Kosten pro Schwierigkeitsstufe (Spielplan v5).
        /// Classic/Amateur = 1, Profi = 2, Gott = 3.
        /// </summary>
        public static int EnergyCost(this NodeDifficulty difficulty) => difficulty switch
        {
            NodeDifficulty.Classic => 1,
            NodeDifficulty.Amateur => 1,
            NodeDifficulty.Profi   => 2,
            NodeDifficulty.Gott    => 3,
            _ => 1
        };

        /// <summary>
        /// Gegner-Stat-Multiplier pro Stufe.
        /// Classic 1.0x, Amateur 1.25x, Profi 1.6x, Gott 2.2x.
        /// Wird auf ATK + HP des gesamten Gegner-Decks angewendet.
        /// </summary>
        public static float EnemyStatMultiplier(this NodeDifficulty difficulty) => difficulty switch
        {
            NodeDifficulty.Classic => 1.0f,
            NodeDifficulty.Amateur => 1.25f,
            NodeDifficulty.Profi   => 1.6f,
            NodeDifficulty.Gott    => 2.2f,
            _ => 1.0f
        };

        /// <summary>
        /// Anzahl Sterne, die der Spieler bei Sieg auf dieser Stufe erhaelt
        /// (entspricht direkt dem Difficulty-Wert).
        /// </summary>
        public static int StarsOnVictory(this NodeDifficulty difficulty) => (int)difficulty;

        /// <summary>
        /// Liefert true, wenn die Stufe Boss-Spezialitaeten aktiviert:
        /// Profi (3) und Gott (4) — Gegner nutzen ihre Skill-2 / Skill-3.
        /// </summary>
        public static bool ActivatesEnemySpecialSkills(this NodeDifficulty difficulty) =>
            difficulty >= NodeDifficulty.Profi;

        /// <summary>
        /// Liefert true, wenn der Node Phasen-Boss-Mechanik aktiviert. Spielplan v5 Kap. 9.4:
        /// Phase 2 (ab 50% HP) gilt fuer JEDEN Boss-Kampf (Node 5 = MiniBoss, Node 10 = WorldBoss),
        /// unabhaengig von der Schwierigkeitsstufe (frueher faelschlich nur auf Gott-Stufe).
        /// Die zusaetzlichen Prestige-Phasen (3/4 ab Prestige III/IV) sind eine separate Eskalation.
        /// </summary>
        public static bool ActivatesBossPhases(this NodeDifficulty difficulty, NodeType nodeType) =>
            nodeType == NodeType.MiniBoss || nodeType == NodeType.WorldBoss;
    }
}
