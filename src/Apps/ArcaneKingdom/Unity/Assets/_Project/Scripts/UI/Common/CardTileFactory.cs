#nullable enable
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Game.Artwork;
using Cysharp.Threading.Tasks;
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
        /// Globaler Artwork-Service der von der UI-Foundation gesetzt wird (im UIInstaller).
        /// Wenn null: Karten zeigen nur den leeren Artwork-Platzhalter.
        /// </summary>
        public static CardArtworkService? ArtworkService { get; set; }

        /// <summary>
        /// Globaler Localization-Service. Wenn gesetzt: lokalisierte Display-Namen
        /// (z.B. "Waldläufer" mit Umlaut aus strings.csv). Wenn null: Key-Suffix-Fallback.
        /// </summary>
        public static ILocalizationService? LocalizationService { get; set; }
        /// <summary>
        /// Erstellt ein neues Karten-Tile. <paramref name="onClick"/> wird bei Klick aufgerufen.
        /// <paramref name="locked"/> grayed-out die Karte (z.B. nicht-besessene Codex-Einträge).
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

            // Artwork — Sprite-Load via ArtworkService (async, mit Procedural-Fallback)
            var art = new VisualElement { name = "art" };
            art.AddToClassList("ak-card__art");
            root.Add(art);

            if (ArtworkService != null)
                LoadArtworkAsync(art, card).Forget();

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

        private static async UniTaskVoid LoadArtworkAsync(VisualElement art, CardDefinition card)
        {
            if (ArtworkService == null) return;
            var sprite = await ArtworkService.GetSpriteAsync(card);
            if (sprite == null) return;
            // VisualElement existiert vielleicht nicht mehr (Tile wurde recyclet)
            if (art.panel == null) return;
            art.style.backgroundImage = new UnityEngine.UIElements.StyleBackground(sprite);
        }

        /// <summary>
        /// Liefert den lokalisierten Display-Namen einer Karte. Bevorzugt
        /// LocalizationService (volle deutsche Namen mit Umlauten), fällt auf
        /// "card.Id" capitalized zurück wenn Localization noch nicht geladen ist.
        /// </summary>
        private static string DisplayNameOf(CardDefinition card)
        {
            // 1. Localization-Service nutzen wenn verfügbar
            if (!string.IsNullOrEmpty(card.DisplayNameKey) && LocalizationService != null)
            {
                var translated = LocalizationService.Get(card.DisplayNameKey, fallback: null);
                if (!string.IsNullOrEmpty(translated) && translated != card.DisplayNameKey)
                    return translated;
            }

            // 2. Fallback: card.Id capitalized (z.B. "waldlaeufer" -> "Waldlaeufer")
            if (string.IsNullOrEmpty(card.Id)) return string.Empty;
            var name = card.Id.Replace('_', ' ');
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}
