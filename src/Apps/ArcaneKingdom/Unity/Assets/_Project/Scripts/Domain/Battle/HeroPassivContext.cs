#nullable enable
using ArcaneKingdom.Domain.Hero;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Vorberechnete Helden-Passiv-Parameter pro Kampfseite (Designplan v4 Kap. 2.1).
    /// Wird beim Battle-Setup gefuellt und vom BattleEngine bei jedem PlayCard / DamageDealt / EndTurn ausgewertet.
    /// </summary>
    public sealed class HeroPassivContext
    {
        public HeroFaehigkeitsTyp PassivType { get; }
        public int Magnitude { get; }

        /// <summary>
        /// Rudelbund: Anzahl Tiergeister im Deck, pre-computed. Multipliziert mit Magnitude (3%) = ATK-Bonus.
        /// Wird in BattleEngine.Setup() gesetzt nachdem das Deck bekannt ist.
        /// </summary>
        public int BeastSpiritCountInDeck { get; set; }

        /// <summary>
        /// Waldlaeufer: Zaehler ob die erste Karte dieser Runde schon gespielt wurde.
        /// Wird in EndTurn() zurueckgesetzt, in PlayCard() gepruft.
        /// </summary>
        public bool FirstCardThisTurnPlayed { get; set; }

        /// <summary>
        /// Goettlicher Segen: ist der Rettungs-Einsatz schon gebraucht? Default 1x pro Kampf.
        /// </summary>
        public int DivineBlessingsRemaining { get; set; }

        public HeroPassivContext(HeroFaehigkeitsTyp passivType, int magnitude, int beastSpiritCountInDeck = 0)
        {
            PassivType = passivType;
            Magnitude = magnitude;
            BeastSpiritCountInDeck = beastSpiritCountInDeck;
            FirstCardThisTurnPlayed = false;
            DivineBlessingsRemaining = passivType == HeroFaehigkeitsTyp.GoettlicherSegen ? magnitude : 0;
        }
    }
}
