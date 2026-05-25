#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Persistierter Story-State (Schema v3): Spieler-Rasse + Erinnerungs-Fragmente + Karten-Persoenlichkeit.
    /// </summary>
    [Serializable]
    public sealed class StorySaveSlice
    {
        /// <summary>
        /// Die gewaehlte Spieler-Rasse (= Held-Passiv). Designplan v4 Kap. 2.1.
        /// Goetter sind hier nicht waehlbar (nur Crafting/Endgame).
        /// </summary>
        public Race ChosenRace { get; set; } = Race.Ritter;

        /// <summary>
        /// Bisher freigeschaltete Erinnerungs-Fragmente (Designplan v4 Story Kap. 9).
        /// Fragment-IDs aus story_fragments.json (fragment_1 ... fragment_10).
        /// </summary>
        public HashSet<string> UnlockedMemoryFragments { get; } = new();

        /// <summary>
        /// Welche Fragmente wurden dem Spieler schon angezeigt (vs. nur freigeschaltet).
        /// </summary>
        public HashSet<string> ViewedMemoryFragments { get; } = new();

        /// <summary>
        /// Karten-Persoenlichkeit-Tracking: welche Dialog-Lines hat der Spieler bereits gesehen?
        /// Map Karten-ID -> (Play/Victory/Death-Suffix gesehen?). Fuer Skip-Logik in UI.
        /// </summary>
        public Dictionary<string, HashSet<string>> SeenPersonalityLinesByCardId { get; } = new();

        /// <summary>
        /// Wurde der Spieler-Twist in Welt 8 (Abysstiefe) bereits angezeigt?
        /// </summary>
        public bool TwistRevealed { get; set; }

        /// <summary>
        /// Endkampf-Entscheidung: Nythragor zerstoeren oder erloesen (null bis Welt 10 abgeschlossen).
        /// </summary>
        public NythragorEndingChoice? EndingChoice { get; set; }
    }

    public enum NythragorEndingChoice
    {
        Destroyed = 0,    // Endpunkt A: Nythragor zerstoert
        Redeemed = 1      // Endpunkt B: Nythragor erloest
    }
}
