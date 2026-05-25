#nullable enable
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Shop
{
    /// <summary>
    /// Shop-Screen mit Karten-Paketen (Spielplan v5 + Impl_KOMPLETT Kap. 8).
    /// 6 Pack-Typen mit Garantien (Basis/Standard/Premium/Legendaer/10er/Element).
    /// Tabs: Pakete | Sonderangebote | Diamanten | Gold | Runen | Event-Shop.
    /// </summary>
    public sealed class ShopScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ILocalizationService _loc;
        private readonly ToastService _toast;

        private VisualElement _tabBar = null!;
        private VisualElement _content = null!;
        private Button _closeBtn = null!;
        private string _activeTab = "packs";

        public override string Id => ScreenId.Shop;
        protected override string UxmlPath => "UI/ShopScreen";

        public ShopScreen(ScreenManager screenManager,
                          ISaveService<PlayerSave> save,
                          ILocalizationService loc,
                          ToastService toast)
        {
            _screenManager = screenManager;
            _save = save;
            _loc = loc;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _tabBar   = Q<VisualElement>("shop-tabs");
            _content  = Q<VisualElement>("shop-content");
            _closeBtn = Q<Button>("shop-close");

            _closeBtn.clicked += () => _screenManager.PopAsync().Forget();
            BuildTabs();
            RenderContent();
        }

        private void BuildTabs()
        {
            _tabBar.Clear();
            string[] tabs = { "packs", "offers", "diamonds", "gold", "runes", "event" };
            string[] labels = { "Pakete", "Angebote", "Diamanten", "Gold", "Runen", "Event" };
            for (var i = 0; i < tabs.Length; i++)
            {
                var tabId = tabs[i];
                var btn = new Button(() => { _activeTab = tabId; RenderContent(); BuildTabs(); }) { text = labels[i] };
                btn.style.flexGrow = 1;
                btn.style.height = 40;
                btn.style.marginRight = 4;
                if (tabId == _activeTab)
                {
                    btn.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f));
                    btn.style.color = new StyleColor(new UnityEngine.Color(0.07f, 0.07f, 0.13f));
                }
                _tabBar.Add(btn);
            }
        }

        private void RenderContent()
        {
            _content.Clear();
            switch (_activeTab)
            {
                case "packs": RenderPacks(); break;
                case "offers": RenderOffers(); break;
                case "diamonds": RenderDiamonds(); break;
                case "gold": RenderGold(); break;
                case "runes": RenderRunes(); break;
                case "event": RenderEvent(); break;
            }
        }

        private void RenderPacks()
        {
            // 6 Pack-Typen aus Impl_KOMPLETT Kap. 8.2
            var packs = new[]
            {
                ("Basis-Pack", "3 Karten — mind. 1 Ungewoehnlich", "500 Gold", false),
                ("Standard-Pack", "5 Karten — mind. 1 Selten", "1.500 Gold / 50 Diamanten", false),
                ("Premium-Pack", "10 Karten — mind. 1 Epic", "100 Diamanten", true),
                ("Legendaer-Pack", "5 Karten — mind. 1 Legendaer", "300 Diamanten", true),
                ("10er Pack", "10 Karten — garantiert 1 Epic + 1 Legendaer", "900 Diamanten", true),
                ("Element-Pack", "8 Karten — 1 Element, 3 Selten garantiert", "150 Diamanten", true)
            };
            foreach (var (name, desc, price, premium) in packs)
                _content.Add(BuildPackCard(name, desc, price, premium));
        }

        private VisualElement BuildPackCard(string name, string desc, string price, bool premium)
        {
            var card = new VisualElement();
            card.style.backgroundColor = premium
                ? new StyleColor(new UnityEngine.Color(0.29f, 0.11f, 0.60f))
                : new StyleColor(new UnityEngine.Color(0.10f, 0.10f, 0.18f));
            card.style.paddingLeft = 16; card.style.paddingRight = 16;
            card.style.paddingTop = 12; card.style.paddingBottom = 12;
            card.style.marginBottom = 8;
            card.style.borderTopLeftRadius = 12; card.style.borderTopRightRadius = 12;
            card.style.borderBottomLeftRadius = 12; card.style.borderBottomRightRadius = 12;

            var nameLbl = new Label(name);
            nameLbl.style.fontSize = 16; nameLbl.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            nameLbl.style.color = premium
                ? new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f))
                : new StyleColor(UnityEngine.Color.white);
            card.Add(nameLbl);

            var descLbl = new Label(desc);
            descLbl.style.fontSize = 12; descLbl.style.color = new StyleColor(new UnityEngine.Color(0.67f, 0.67f, 0.75f));
            descLbl.style.marginTop = 4; descLbl.style.whiteSpace = WhiteSpace.Normal;
            card.Add(descLbl);

            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.marginTop = 8;
            var priceLbl = new Label(price);
            priceLbl.style.fontSize = 14; priceLbl.style.color = new StyleColor(new UnityEngine.Color(0.41f, 0.94f, 0.68f));
            priceLbl.style.flexGrow = 1;
            row.Add(priceLbl);

            var buyBtn = new Button(() => _toast.Show($"Kauf-Flow fuer '{name}' kommt mit IAP-Integration.", ToastKind.Info))
                { text = "Kaufen" };
            buyBtn.style.width = 100; buyBtn.style.height = 36;
            buyBtn.style.backgroundColor = new StyleColor(new UnityEngine.Color(1.0f, 0.48f, 0.0f));
            buyBtn.style.color = new StyleColor(UnityEngine.Color.white);
            row.Add(buyBtn);
            card.Add(row);
            return card;
        }

        private void RenderOffers() => _content.Add(new Label("Sonderangebote rotieren taeglich — kommen mit Server-Integration."));
        private void RenderDiamonds() => _content.Add(new Label("Diamanten-Pakete: 100, 500, 2000, 5000, 10000."));
        private void RenderGold() => _content.Add(new Label("Gold-Pakete oder Diamanten -> Gold Wechsel."));
        private void RenderRunes() => _content.Add(new Label("Runen-Direktkauf — Drachen/Heiler/Schutz/Feuer/Wasser/Schatten."));
        private void RenderEvent() => _content.Add(new Label("Event-Shop ist nur waehrend laufender Events verfuegbar."));
    }
}
