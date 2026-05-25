#nullable enable
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Festes Fusions-Rezept (Designplan v4 Kap. 5.1 Typ B + Kap. 5.2 Goetter-Crafting).
    /// Im Gegensatz zum kategorie-basierten Crafting (zufaelliges Ergebnis derselben Rasse)
    /// liefern feste Rezepte eine spezifische Karte als Ergebnis.
    /// </summary>
    public sealed class FusionRecipe
    {
        /// <summary>Stabile Rezept-ID (z.B. "recipe_solaris_4star").</summary>
        public string Id { get; }

        /// <summary>Karten-ID die durch dieses Rezept erstellt wird.</summary>
        public string ResultCardId { get; }

        /// <summary>Liste der benoetigten Input-Karten (genau eine Kopie wird pro Eintrag verbraucht).</summary>
        public IReadOnlyList<string> RequiredCardIds { get; }

        /// <summary>Benoetigte Crafting-Materialien (Liste von Material-IDs).</summary>
        public IReadOnlyList<string> RequiredMaterialIds { get; }

        /// <summary>Gold-Kosten fuer das Rezept.</summary>
        public long GoldCost { get; }

        /// <summary>Tooltip: Hint-Key fuer Lokalisierung. Wird ueber NPC-Dialog oder Story-Drop freigeschaltet.</summary>
        public string HintLocalizationKey { get; }

        /// <summary>Versteckt = NICHT in der Schmiede sichtbar bevor entdeckt (Story/NPC-Trigger).</summary>
        public bool IsHidden { get; }

        public FusionRecipe(
            string id,
            string resultCardId,
            IReadOnlyList<string> requiredCardIds,
            IReadOnlyList<string> requiredMaterialIds,
            long goldCost,
            string hintLocalizationKey,
            bool isHidden = false)
        {
            Id = id;
            ResultCardId = resultCardId;
            RequiredCardIds = requiredCardIds;
            RequiredMaterialIds = requiredMaterialIds;
            GoldCost = goldCost;
            HintLocalizationKey = hintLocalizationKey;
            IsHidden = isHidden;
        }
    }
}
