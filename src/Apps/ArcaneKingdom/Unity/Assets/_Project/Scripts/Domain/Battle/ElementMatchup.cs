#nullable enable
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Element-Effektivitaets-Matrix (DESIGN.md Kapitel 9.3).
    /// Stark = 1.5, Schwach = 0.75, sonst Neutral = 1.0.
    /// Natur/Feuer/Wasser = Dreieck, Licht/Dunkel = separater Achsen-Konflikt.
    /// </summary>
    public static class ElementMatchup
    {
        public const float NeutralMultiplier = 1.0f;
        public const float StrongMultiplier = 1.5f;
        public const float WeakMultiplier = 0.75f;

        public static float GetMultiplier(Element attacker, Element defender)
        {
            return (attacker, defender) switch
            {
                // Natur-Feuer-Wasser-Dreieck
                (Element.Natur, Element.Wasser)  => StrongMultiplier,
                (Element.Natur, Element.Feuer)   => WeakMultiplier,
                (Element.Feuer, Element.Natur)   => StrongMultiplier,
                (Element.Feuer, Element.Wasser)  => WeakMultiplier,
                (Element.Wasser, Element.Feuer)  => StrongMultiplier,
                (Element.Wasser, Element.Natur)  => WeakMultiplier,

                // Licht-Dunkel-Achse
                (Element.Licht, Element.Dunkel)  => StrongMultiplier,
                (Element.Dunkel, Element.Licht)  => WeakMultiplier,

                // Alles andere: Neutral
                _ => NeutralMultiplier
            };
        }
    }
}
