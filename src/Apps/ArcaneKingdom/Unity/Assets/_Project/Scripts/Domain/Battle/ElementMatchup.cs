#nullable enable
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Element-Effektivitäts-Matrix für das Doppel-Dreieck-System (Designplan v4 Kap. 3).
    ///
    /// Physisches Dreieck:
    ///   Feuer  → stark gegen Natur
    ///   Natur  → stark gegen Wasser
    ///   Wasser → stark gegen Feuer
    ///
    /// Magisches Dreieck:
    ///   Dunkel → stark gegen Erde   (Verderbnis zersetzt die Erde)
    ///   Erde   → stark gegen Licht  (Verschüttung, Finsternis)
    ///   Licht  → stark gegen Dunkel (Lichtschwert vertreibt die Dunkelheit)
    ///
    /// Stark = +10% Schaden + Element-Spezialeffekt (Verbrennung, Einfrierung, Steinpanzer etc.).
    /// Designplan v4 Kap. 3.3 kennt NUR den +10%-Bonus für das überlegene Element — KEINEN
    /// Schwäche-Malus. Alle nicht-starken Paarungen (inkl. Rückrichtung) sind daher neutral (1.0x).
    /// </summary>
    public static class ElementMatchup
    {
        /// <summary>Neutraler Multiplikator (1.0x) — auch für die "schwache" Rückrichtung (v4 hat keinen Malus).</summary>
        public const float NeutralMultiplier = 1.0f;

        /// <summary>Stark gegen — +10% Schaden (Designplan v4 Kap. 3.3).</summary>
        public const float StrongMultiplier = 1.10f;

        public static float GetMultiplier(Element attacker, Element defender)
        {
            return (attacker, defender) switch
            {
                // Physisches Dreieck: Feuer → Natur → Wasser → Feuer (nur +10%, kein Rück-Malus)
                (Element.Feuer,  Element.Natur)  => StrongMultiplier,
                (Element.Natur,  Element.Wasser) => StrongMultiplier,
                (Element.Wasser, Element.Feuer)  => StrongMultiplier,

                // Magisches Dreieck: Licht → Dunkel → Erde → Licht
                (Element.Licht,  Element.Dunkel) => StrongMultiplier,
                (Element.Dunkel, Element.Erde)   => StrongMultiplier,
                (Element.Erde,   Element.Licht)  => StrongMultiplier,

                // Alle anderen Paarungen (Schwäche-Rückrichtung + Cross-Dreieck): neutral
                _ => NeutralMultiplier
            };
        }

        /// <summary>
        /// Liefert true, wenn das angreifende Element zum physischen Dreieck (Feuer/Wasser/Natur) gehört.
        /// </summary>
        public static bool IsPhysical(Element element) =>
            element is Element.Feuer or Element.Wasser or Element.Natur;

        /// <summary>
        /// Liefert true, wenn das Element zum magischen Dreieck (Licht/Dunkel/Erde) gehört.
        /// </summary>
        public static bool IsMagical(Element element) =>
            element is Element.Licht or Element.Dunkel or Element.Erde;
    }
}
