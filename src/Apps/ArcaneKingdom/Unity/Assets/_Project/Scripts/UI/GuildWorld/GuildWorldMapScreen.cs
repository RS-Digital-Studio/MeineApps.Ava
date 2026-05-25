#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Guild;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.GuildWorld
{
    /// <summary>
    /// Gilden-Weltkarte (Spielplan v5 Kap. 13 + Impl_KOMPLETT Kap. 12).
    /// Overworld mit 10 Gebieten, Farbe nach Besitzer, Gebots-Popup, Bevorstehende Matches.
    /// </summary>
    public sealed class GuildWorldMapScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly TerritoryService _territoryService;
        private readonly ToastService _toast;

        private VisualElement _territoryGrid = null!;
        private VisualElement _matchesList = null!;
        private Button _closeBtn = null!;

        public override string Id => ScreenId.GuildWorldMap;
        protected override string UxmlPath => "UI/GuildWorldMapScreen";

        public GuildWorldMapScreen(ScreenManager screenManager, TerritoryService territoryService, ToastService toast)
        {
            _screenManager = screenManager;
            _territoryService = territoryService;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _territoryGrid = Q<VisualElement>("territory-grid");
            _matchesList   = Q<VisualElement>("matches-list");
            _closeBtn      = Q<Button>("guild-world-close");
            _closeBtn.clicked += () => _screenManager.PopAsync().Forget();
            BuildTerritories();
            BuildMatches();
        }

        private void BuildTerritories()
        {
            _territoryGrid.Clear();
            string[] names = { "Abendtundra", "Schwarzbinge", "Meeresreich", "Himmelsreich", "Uebungswald",
                                "Westwindinsel", "Verbrannte Ebene", "Schildkroeteninsel", "Endzeitkamm", "Jadewald" };
            TerritoryRarity[] rarities = {
                TerritoryRarity.Common, TerritoryRarity.Rare, TerritoryRarity.Common, TerritoryRarity.Epic, TerritoryRarity.Common,
                TerritoryRarity.Rare, TerritoryRarity.Epic, TerritoryRarity.Rare, TerritoryRarity.Legendaer, TerritoryRarity.Epic
            };
            string?[] owners = { "KINGZ", null, "ELITE", null, null, "KINGZ", "VOID", null, null, "NEXUS" };

            for (var i = 0; i < names.Length; i++)
            {
                var tile = new VisualElement();
                tile.style.width = 140; tile.style.height = 90;
                tile.style.marginRight = 8; tile.style.marginBottom = 8;
                tile.style.borderTopLeftRadius = 10; tile.style.borderTopRightRadius = 10;
                tile.style.borderBottomLeftRadius = 10; tile.style.borderBottomRightRadius = 10;
                tile.style.paddingLeft = 10; tile.style.paddingRight = 10; tile.style.paddingTop = 8; tile.style.paddingBottom = 8;

                var bg = owners[i] switch
                {
                    "KINGZ"  => new UnityEngine.Color(0.0f, 0.5f, 0.0f, 0.5f),  // gruen
                    null     => new UnityEngine.Color(0.25f, 0.25f, 0.30f),     // neutral
                    _        => new UnityEngine.Color(0.55f, 0.10f, 0.10f, 0.6f) // andere Gilde
                };
                tile.style.backgroundColor = new StyleColor(bg);

                tile.Add(new Label(names[i]) { style = { fontSize = 14, unityFontStyleAndWeight = UnityEngine.FontStyle.Bold, color = new StyleColor(UnityEngine.Color.white) } });
                tile.Add(new Label(rarities[i].ToString()) { style = { fontSize = 11, color = RarityColor(rarities[i]) } });
                tile.Add(new Label(owners[i] ?? "Neutral") { style = { fontSize = 11, color = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f)), marginTop = 4 } });

                var idx = i;
                tile.RegisterCallback<ClickEvent>(_ => OnTerritoryTapped(names[idx], rarities[idx]));
                _territoryGrid.Add(tile);
            }
        }

        private static StyleColor RarityColor(TerritoryRarity rarity) => rarity switch
        {
            TerritoryRarity.Common    => new StyleColor(new UnityEngine.Color(0.7f, 0.7f, 0.7f)),
            TerritoryRarity.Rare      => new StyleColor(new UnityEngine.Color(0.25f, 0.65f, 1.0f)),
            TerritoryRarity.Epic      => new StyleColor(new UnityEngine.Color(0.78f, 0.55f, 1.0f)),
            TerritoryRarity.Legendaer => new StyleColor(new UnityEngine.Color(1.0f, 0.84f, 0.0f)),
            _ => new StyleColor(UnityEngine.Color.white)
        };

        private void BuildMatches()
        {
            _matchesList.Clear();
            string[] matches = { "Abendtundra — heute 19:50", "Schwarzbinge — morgen 12:00" };
            foreach (var m in matches)
            {
                var row = new Label(m);
                row.style.fontSize = 12; row.style.color = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f));
                row.style.marginBottom = 4;
                _matchesList.Add(row);
            }
        }

        private void OnTerritoryTapped(string name, TerritoryRarity rarity)
        {
            _toast.Show($"{name} ({rarity}) — Gebots-Popup folgt mit Server-Integration.", ToastKind.Info);
        }
    }
}
