#nullable enable
using System;
using ArcaneKingdom.UI.Foundation;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Hub
{
    /// <summary>
    /// Hub-Stadt-Renderer (Spielplan v5 Kap. 3 + Login-Hub-Plan Kap. 6).
    /// Erzeugt klickbare Gebaeude (Karten-Turm, Zauberschmiede, Arena-Tempel, Tempel,
    /// Hafen, Marktplatz, Bibliothek, Wand-der-Ehre) als 2D-Stadt-Layout auf einem Container.
    /// Wird vom HubScreen als zusaetzliche Ansicht eingehaengt.
    /// </summary>
    public static class HubCityRenderer
    {
        /// <summary>Baut die Hub-Stadt-Gebaeude in den uebergebenen Container.</summary>
        public static void Render(VisualElement container, Action<string> onBuildingTapped)
        {
            container.Clear();
            container.style.flexGrow = 1;
            container.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.04f, 0.04f, 0.10f));

            // 2x4-Grid mit Gebaeuden
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.SpaceAround;
            grid.style.paddingLeft = 16; grid.style.paddingRight = 16;
            grid.style.paddingTop = 16; grid.style.paddingBottom = 16;
            container.Add(grid);

            AddBuilding(grid, "🏰 Karten-Turm",       "Deck-Verwaltung",     "rgb(74, 27, 154)",  ScreenId.DeckBuilder, onBuildingTapped);
            AddBuilding(grid, "⚒️ Zauberschmiede",   "Karten craften",       "rgb(180, 80, 20)",  ScreenId.Schmiede, onBuildingTapped);
            AddBuilding(grid, "⚔️ Arena-Tempel",     "PvP-Kaempfe",          "rgb(180, 30, 30)",  ScreenId.Arena, onBuildingTapped);
            AddBuilding(grid, "🛕 Tempel",            "Quests + Login-Bonus", "rgb(120, 70, 20)",  ScreenId.QuestCenter, onBuildingTapped);
            AddBuilding(grid, "⚓ Hafen",             "Gilden-Weltkarte",     "rgb(20, 60, 120)",  ScreenId.GuildWorldMap, onBuildingTapped);
            AddBuilding(grid, "🛒 Marktplatz",        "Shop + Pakete",        "rgb(40, 120, 60)",  ScreenId.Shop, onBuildingTapped);
            AddBuilding(grid, "📚 Bibliothek",        "Codex + Story",        "rgb(80, 40, 100)",  ScreenId.Codex, onBuildingTapped);
            AddBuilding(grid, "🏆 Wand der Ehre",     "Rangliste",            "rgb(245, 200, 66)", ScreenId.MeritRanking, onBuildingTapped);
            AddBuilding(grid, "🔮 Schmiede (Sammlung)","Material-Tausch",     "rgb(140, 30, 100)", "collection-trade", onBuildingTapped);
        }

        private static void AddBuilding(VisualElement parent, string title, string subtitle, string colorRgb, string screenId, Action<string> onTap)
        {
            var bg = StringRgbToColor(colorRgb);
            var tile = new VisualElement();
            tile.style.width = new Length(46, LengthUnit.Percent);
            tile.style.height = 110;
            tile.style.marginBottom = 12;
            tile.style.backgroundColor = new StyleColor(bg);
            tile.style.borderTopLeftRadius = 12; tile.style.borderTopRightRadius = 12;
            tile.style.borderBottomLeftRadius = 12; tile.style.borderBottomRightRadius = 12;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;
            tile.style.borderLeftWidth = 2; tile.style.borderRightWidth = 2;
            tile.style.borderTopWidth = 2; tile.style.borderBottomWidth = 2;
            tile.style.borderLeftColor = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f, 0.3f));
            tile.style.borderRightColor = tile.style.borderLeftColor;
            tile.style.borderTopColor = tile.style.borderLeftColor;
            tile.style.borderBottomColor = tile.style.borderLeftColor;
            tile.style.paddingLeft = 12; tile.style.paddingRight = 12;
            tile.AddToClassList("ak-hub-building");

            var titleLbl = new Label(title);
            titleLbl.style.fontSize = 16;
            titleLbl.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            titleLbl.style.color = new StyleColor(UnityEngine.Color.white);
            titleLbl.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
            titleLbl.style.whiteSpace = WhiteSpace.Normal;
            tile.Add(titleLbl);

            var subtitleLbl = new Label(subtitle);
            subtitleLbl.style.fontSize = 11;
            subtitleLbl.style.color = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f));
            subtitleLbl.style.marginTop = 4;
            tile.Add(subtitleLbl);

            tile.RegisterCallback<ClickEvent>(_ => onTap?.Invoke(screenId));
            parent.Add(tile);
        }

        private static UnityEngine.Color StringRgbToColor(string rgb)
        {
            // "rgb(R, G, B)" → Color
            var trimmed = rgb.Replace("rgb(", "").Replace(")", "");
            var parts = trimmed.Split(',');
            if (parts.Length != 3) return new UnityEngine.Color(0.5f, 0.5f, 0.5f);
            if (!float.TryParse(parts[0].Trim(), out var r)) r = 128;
            if (!float.TryParse(parts[1].Trim(), out var g)) g = 128;
            if (!float.TryParse(parts[2].Trim(), out var b)) b = 128;
            return new UnityEngine.Color(r / 255f, g / 255f, b / 255f);
        }
    }
}
