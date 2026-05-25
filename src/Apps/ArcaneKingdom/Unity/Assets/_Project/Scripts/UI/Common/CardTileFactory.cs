#nullable enable
using ArcaneKingdom.Domain.Cards;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Common
{
    /// <summary>
    /// Helper zum Bauen eines Karten-Visual-Items (ak-card) aus einer CardDefinition.
    /// Wird vom Cards-Tab + DeckBuilder + Pack-Opening verwendet.
    ///
    /// Layout-CSS: <c>UI/Theme/Components.uss</c> (.ak-card und Modifier-Klassen).
    /// </summary>
    public static class CardTileFactory
    {
        /// <summary>
        /// Erstellt ein neues Karten-Tile. <paramref name="onClick"/> wird bei Klick aufgerufen.
        /// <paramref name="locked"/> grayed-out die Karte (z.B. nicht-besessene Codex-Eintraege).
        /// </summary>
        public static VisualElement Build(CardDefinition card,
                                          System.Action<CardDefinition>? onClick = null,
                                          bool locked = false)
        {
            var root = new VisualElement { name = $"card-{card.Id}" };
            root.AddToClassList("ak-card");
            root.AddToClassList(RarityClass(card.Rarity));
            if (locked) root.AddToClassList("ak-card--locked");

            // Cost-Badge (oben links)
            var costBadge = new VisualElement { name = "cost-badge" };
            costBadge.AddToClassList("ak-card__cost-badge");
            var costValue = new Label(card.Cost.ToString());
            costValue.AddToClassList("ak-card__cost-value");
            costBadge.Add(costValue);
            root.Add(costBadge);

            // Element-Badge (oben rechts)
            var elementBadge = new VisualElement { name = "element-badge" };
            elementBadge.AddToClassList("ak-card__element-badge");
            elementBadge.AddToClassList(ElementClass(card.Element));
            root.Add(elementBadge);

            // Artwork (Platzhalter — spaeter Sprite-Load via Addressables)
            var art = new VisualElement { name = "art" };
            art.AddToClassList("ak-card__art");
            root.Add(art);

            // Name
            var nameLabel = new Label(DisplayNameOf(card));
            nameLabel.AddToClassList("ak-card__name");
            root.Add(nameLabel);

            // Stats (ATK / HP)
            var stats = new VisualElement { name = "stats" };
            stats.AddToClassList("ak-card__stats");
            var atk = new Label(card.BaseAttack.ToString("N0"));
            atk.AddToClassList("ak-card__stat");
            atk.AddToClassList("ak-card__stat--atk");
            var hp = new Label(card.BaseHealth.ToString("N0"));
            hp.AddToClassList("ak-card__stat");
            hp.AddToClassList("ak-card__stat--hp");
            stats.Add(atk);
            stats.Add(hp);
            root.Add(stats);

            if (onClick != null)
            {
                root.AddManipulator(new Clickable(() => onClick(card)));
                root.RegisterCallback<MouseEnterEvent>(_ => root.style.opacity = 1f);
            }

            return root;
        }

        // --- Klassen-Mapping ---

        private static string RarityClass(Rarity r) => r switch
        {
            Rarity.Ungewoehnlich => "ak-card--rarity-uncommon",
            Rarity.Selten        => "ak-card--rarity-rare",
            Rarity.Epic          => "ak-card--rarity-epic",
            Rarity.Legendaer     => "ak-card--rarity-legendary",
            _                    => "ak-card--rarity-common"
        };

        private static string ElementClass(Element e) => e switch
        {
            Element.Feuer  => "ak-card__element-badge--feuer",
            Element.Wasser => "ak-card__element-badge--wasser",
            Element.Licht  => "ak-card__element-badge--licht",
            Element.Dunkel => "ak-card__element-badge--dunkel",
            _              => "ak-card__element-badge--natur"
        };

        /// <summary>Fallback: wenn Localization noch nicht da ist, nutzen wir ID-Suffix als Name.</summary>
        private static string DisplayNameOf(CardDefinition card)
        {
            if (!string.IsNullOrEmpty(card.DisplayNameKey))
            {
                // Spaeter: ILocalizationService.Get(card.DisplayNameKey)
                // Vorerst: Key-basiertes Fallback ("card.drachenherrscher" -> "Drachenherrscher")
                var key = card.DisplayNameKey;
                var dot = key.LastIndexOf('.');
                if (dot >= 0 && dot < key.Length - 1)
                {
                    var name = key.Substring(dot + 1).Replace('_', ' ');
                    if (name.Length > 0)
                        return char.ToUpper(name[0]) + name.Substring(1);
                }
            }
            return card.Id;
        }
    }
}
