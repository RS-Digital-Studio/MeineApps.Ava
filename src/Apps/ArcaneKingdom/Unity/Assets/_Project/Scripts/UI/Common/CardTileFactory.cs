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
    /// Das PNG-Artwork enthält bereits den kompletten Frame mit Rarity-Border,
    /// Eck-Ornamenten, Gold-Innenrahmen, Element-Wappen, Name-Banner und Sternen.
    /// Diese Factory rendert NUR die dynamischen Werte als Overlay über dem PNG:
    ///   - LV-Box (oben links)
    ///   - Cost-Bubble (oben rechts)
    ///   - Sanduhr (Mitte links, nur größere Tiles + Detail-View)
    ///   - ATK / HP (unten)
    ///
    /// Layout-CSS: <c>UI/Theme/Components.uss</c> (.ak-card und Modifier-Klassen).
    /// </summary>
    public static class CardTileFactory
    {
        /// <summary>Tile-Größen für unterschiedliche Kontexte.</summary>
        public enum TileSize { Small, Medium, Large }

        /// <summary>
        /// Globaler Artwork-Service der von der UI-Foundation gesetzt wird (im UIInstaller).
        /// Wenn null: Karten zeigen nur den leeren Artwork-Platzhalter.
        /// </summary>
        public static CardArtworkService? ArtworkService { get; set; }

        /// <summary>
        /// Globaler Localization-Service. Aktuell nur für Tooltip/Detail-View benötigt
        /// — der Karten-Name ist bereits im PNG-Banner gerendert.
        /// </summary>
        public static ILocalizationService? LocalizationService { get; set; }

        /// <summary>
        /// Erstellt ein neues Karten-Tile mit dem PNG-Artwork als Hintergrund und
        /// dynamischen Werten (LV, Cost, ATK, HP, optional Sanduhr) als Overlay.
        /// </summary>
        /// <param name="card">Karten-Definition.</param>
        /// <param name="onClick">Klick-Handler (z.B. Detail-Modal öffnen).</param>
        /// <param name="locked">Grayed-out für nicht-besessene Codex-Einträge.</param>
        /// <param name="size">Tile-Größe (Small/Medium/Large).</param>
        /// <param name="currentLevel">Aktuelles Karten-Level (0-15). 0 = Startzustand.</param>
        /// <param name="showHourglass">Sanduhr (Rundenwarten) anzeigen — nur Medium+Large.</param>
        public static VisualElement Build(CardDefinition card,
                                          System.Action<CardDefinition>? onClick = null,
                                          bool locked = false,
                                          TileSize size = TileSize.Medium,
                                          int currentLevel = 0,
                                          bool showHourglass = false)
        {
            var root = new VisualElement { name = $"card-{card.Id}" };
            root.AddToClassList("ak-card");
            root.AddToClassList(SizeClass(size));
            if (locked) root.AddToClassList("ak-card--locked");
            if (showHourglass) root.AddToClassList("ak-card--with-hourglass");

            // Card-Background = PNG-Artwork (cover-Mode)
            if (ArtworkService != null)
                LoadArtworkAsync(root, card).Forget();

            // LV-Badge (oben links)
            var lvBadge = new VisualElement { name = "lv-badge" };
            lvBadge.AddToClassList("ak-card__lv-badge");
            if (currentLevel >= 15) lvBadge.AddToClassList("ak-card__lv-badge--max");
            var lvValue = new Label($"LV.{currentLevel}");
            lvValue.AddToClassList("ak-card__lv-value");
            lvBadge.Add(lvValue);
            root.Add(lvBadge);

            // Cost-Bubble (oben rechts)
            var costBadge = new VisualElement { name = "cost-badge" };
            costBadge.AddToClassList("ak-card__cost-badge");
            var costValue = new Label(card.Cost.ToString());
            costValue.AddToClassList("ak-card__cost-value");
            costBadge.Add(costValue);
            root.Add(costBadge);

            // Sanduhr (Mitte links) — Rundenwarten
            var hourglass = new VisualElement { name = "hourglass" };
            hourglass.AddToClassList("ak-card__hourglass");
            var hgValue = new Label(card.TurnsToSpecial.ToString());
            hgValue.AddToClassList("ak-card__hourglass-value");
            hourglass.Add(hgValue);
            root.Add(hourglass);

            // Stats (ATK / HP)
            var stats = new VisualElement { name = "stats" };
            stats.AddToClassList("ak-card__stats");
            stats.Add(BuildStat("ATK", card.BaseAttack, "ak-card__stat--atk"));
            stats.Add(BuildStat("HP", card.BaseHealth, "ak-card__stat--hp"));
            root.Add(stats);

            if (onClick != null)
            {
                root.AddManipulator(new Clickable(() => onClick(card)));
            }

            return root;
        }

        private static VisualElement BuildStat(string label, int value, string modifierClass)
        {
            var stat = new VisualElement();
            stat.AddToClassList("ak-card__stat");
            stat.AddToClassList(modifierClass);
            var lbl = new Label(label);
            lbl.AddToClassList("ak-card__stat-label");
            var val = new Label(value.ToString("N0"));
            val.AddToClassList("ak-card__stat-value");
            stat.Add(lbl);
            stat.Add(val);
            return stat;
        }

        // --- Klassen-Mapping ---

        private static string SizeClass(TileSize size) => size switch
        {
            TileSize.Small  => "ak-card--size-small",
            TileSize.Large  => "ak-card--size-large",
            _               => "ak-card--size-medium"
        };

        private static async UniTaskVoid LoadArtworkAsync(VisualElement root, CardDefinition card)
        {
            if (ArtworkService == null) return;
            var sprite = await ArtworkService.GetSpriteAsync(card);
            if (sprite == null) return;
            // backgroundImage AUCH setzen, wenn das Tile noch nicht im Panel haengt — Build() ruft
            // diese Methode auf, BEVOR der Aufrufer das Tile ins Grid einfuegt. Der fruehere
            // panel==null-Check verwarf dadurch das Artwork beim Erst-Laden (Tiles blieben leer).
            root.style.backgroundImage = new StyleBackground(sprite);
            // Karten-Artwork formatfuellend skalieren (nicht stauchen): wird vom .ak-card-USS bestimmt,
            // hier als Sicherheitsnetz der Scale-Mode.
            root.style.unityBackgroundScaleMode = UnityEngine.ScaleMode.ScaleAndCrop;
        }
    }
}
