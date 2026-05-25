#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.UI.Common;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Hub
{
    /// <summary>
    /// Zentrale Hub-Ansicht — Header (Profil + Currencies), TabBar (Cards/Quests/Shop/Arena/Mehr),
    /// Content-Container der das aktive Tab-Panel zeigt.
    ///
    /// In Stufe 3 ist nur der Cards-Tab voll implementiert. Quests/Shop/Arena/Mehr
    /// kommen in Stufe 4.
    /// </summary>
    public sealed class HubScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly CardCatalogService _cardCatalog;
        private readonly ToastService _toast;

        // Header
        private Label _displayName = null!;
        private Label _levelLabel = null!;
        private Label _energyValue = null!;
        private Label _goldValue = null!;
        private Label _diamondValue = null!;

        // Tab-Panels
        private VisualElement _tabCards = null!;
        private VisualElement _tabQuests = null!;
        private VisualElement _tabShop = null!;
        private VisualElement _tabArena = null!;
        private VisualElement _tabMore = null!;

        // Tab-Buttons
        private VisualElement _btnCards = null!;
        private VisualElement _btnQuests = null!;
        private VisualElement _btnShop = null!;
        private VisualElement _btnArena = null!;
        private VisualElement _btnMore = null!;

        // Cards-Tab
        private TextField _cardsSearch = null!;
        private DropdownField _cardsRarityFilter = null!;
        private DropdownField _cardsElementFilter = null!;
        private VisualElement _cardsGrid = null!;
        private VisualElement _cardsEmpty = null!;

        private PlayerSave? _saveCached;
        private CancellationTokenSource? _refreshCts;

        public override string Id => ScreenId.Hub;
        protected override string UxmlPath => "UI/HubScreen";

        public HubScreen(ScreenManager screenManager,
                         ISaveService<PlayerSave> save,
                         CardCatalogService cardCatalog,
                         ToastService toast)
        {
            _screenManager = screenManager;
            _save = save;
            _cardCatalog = cardCatalog;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            // Header
            _displayName  = Q<Label>("header-display-name");
            _levelLabel   = Q<Label>("header-level");
            _energyValue  = Q<Label>("header-energy-value");
            _goldValue    = Q<Label>("header-gold-value");
            _diamondValue = Q<Label>("header-diamond-value");

            // Tab-Panels
            _tabCards  = Q<VisualElement>("tab-cards");
            _tabQuests = Q<VisualElement>("tab-quests");
            _tabShop   = Q<VisualElement>("tab-shop");
            _tabArena  = Q<VisualElement>("tab-arena");
            _tabMore   = Q<VisualElement>("tab-more");

            // Tab-Buttons
            _btnCards  = Q<VisualElement>("tab-button-cards");
            _btnQuests = Q<VisualElement>("tab-button-quests");
            _btnShop   = Q<VisualElement>("tab-button-shop");
            _btnArena  = Q<VisualElement>("tab-button-arena");
            _btnMore   = Q<VisualElement>("tab-button-more");

            _btnCards.AddManipulator(new Clickable(() => SwitchTab("cards")));
            _btnQuests.AddManipulator(new Clickable(() => SwitchTab("quests")));
            _btnShop.AddManipulator(new Clickable(() => SwitchTab("shop")));
            _btnArena.AddManipulator(new Clickable(() => SwitchTab("arena")));
            _btnMore.AddManipulator(new Clickable(() => SwitchTab("more")));

            // Cards-Tab
            _cardsSearch        = Q<TextField>("cards-search");
            _cardsRarityFilter  = Q<DropdownField>("cards-rarity-filter");
            _cardsElementFilter = Q<DropdownField>("cards-element-filter");
            _cardsGrid          = Q<VisualElement>("cards-grid");
            _cardsEmpty         = Q<VisualElement>("cards-empty");

            // Filter-Dropdowns befuellen
            _cardsRarityFilter.choices = new List<string> { "Alle Raritaeten",
                "Gewoehnlich", "Ungewoehnlich", "Selten", "Epic", "Legendaer" };
            _cardsRarityFilter.index = 0;

            _cardsElementFilter.choices = new List<string> { "Alle Elemente",
                "Natur", "Feuer", "Wasser", "Licht", "Dunkel" };
            _cardsElementFilter.index = 0;

            _cardsSearch.RegisterValueChangedCallback(_ => RefreshCardsGrid());
            _cardsRarityFilter.RegisterValueChangedCallback(_ => RefreshCardsGrid());
            _cardsElementFilter.RegisterValueChangedCallback(_ => RefreshCardsGrid());

            SwitchTab("cards");
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            _refreshCts?.Cancel();
            _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var result = await _save.LoadAsync(_refreshCts.Token);
            if (!result.IsSuccess)
            {
                _toast.Show($"Save laden fehlgeschlagen: {result.ErrorMessage}", ToastKind.Danger);
                return;
            }
            _saveCached = result.Value;
            RefreshHeader();
            RefreshCardsGrid();
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            _refreshCts?.Cancel();
            _refreshCts = null;
            return UniTask.CompletedTask;
        }

        // ============================================================
        // Header
        // ============================================================

        private void RefreshHeader()
        {
            if (_saveCached == null) return;
            var p = _saveCached.Profile;
            var c = _saveCached.Currencies;

            _displayName.text  = string.IsNullOrEmpty(p.DisplayName) ? "Spieler" : p.DisplayName;
            _levelLabel.text   = $"Stufe {p.Level}";
            _energyValue.text  = c.EnergyBonus > 0
                ? $"{c.TotalEnergy}/{PlayerCurrencies.EnergyDefaultCap} (+{c.EnergyBonus})"
                : $"{c.Energy}/{PlayerCurrencies.EnergyDefaultCap}";
            _goldValue.text    = FormatNumber(c.Gold);
            _diamondValue.text = FormatNumber(c.Diamond);
        }

        // ============================================================
        // Tabs
        // ============================================================

        private void SwitchTab(string id)
        {
            SetActive(_tabCards,  id == "cards");
            SetActive(_tabQuests, id == "quests");
            SetActive(_tabShop,   id == "shop");
            SetActive(_tabArena,  id == "arena");
            SetActive(_tabMore,   id == "more");

            SetTabButtonActive(_btnCards,  id == "cards");
            SetTabButtonActive(_btnQuests, id == "quests");
            SetTabButtonActive(_btnShop,   id == "shop");
            SetTabButtonActive(_btnArena,  id == "arena");
            SetTabButtonActive(_btnMore,   id == "more");

            GameLogger.Verbose("Hub", $"Tab -> {id}");
        }

        private static void SetActive(VisualElement panel, bool active)
        {
            if (active) panel.RemoveFromClassList("ak-hidden");
            else panel.AddToClassList("ak-hidden");
        }

        private static void SetTabButtonActive(VisualElement button, bool active)
        {
            if (active) button.AddToClassList("ak-tab--active");
            else button.RemoveFromClassList("ak-tab--active");
        }

        // ============================================================
        // Cards-Tab
        // ============================================================

        private void RefreshCardsGrid()
        {
            _cardsGrid.Clear();
            if (_saveCached == null) return;

            // Welche Karten besitzt der Spieler? (Card-Inventory enthaelt CardInstance,
            // wir zaehlen pro CardDefinitionId)
            var ownedCounts = new Dictionary<string, int>();
            foreach (var instance in _saveCached.CardInventory.Values)
            {
                var defId = instance.CardDefinitionId;
                ownedCounts[defId] = ownedCounts.TryGetValue(defId, out var c) ? c + 1 : 1;
            }

            var rarityFilter = ParseRarityFilter(_cardsRarityFilter.value);
            var elementFilter = ParseElementFilter(_cardsElementFilter.value);
            var search = (_cardsSearch.value ?? string.Empty).Trim().ToLowerInvariant();

            // Wir zeigen ALLE Cards aus dem Catalog. Nicht besessene werden "locked" dargestellt.
            // So sieht der Spieler die Vollstaendigkeit der Collection.
            var cards = _cardCatalog.AllCards
                .Where(c => rarityFilter == null || c.Rarity == rarityFilter)
                .Where(c => elementFilter == null || c.Element == elementFilter)
                .Where(c => string.IsNullOrEmpty(search)
                            || c.Id.ToLowerInvariant().Contains(search))
                .ToList();

            foreach (var card in cards)
            {
                var owned = ownedCounts.ContainsKey(card.Id);
                var tile = CardTileFactory.Build(card,
                    onClick: OnCardClicked,
                    locked: !owned);
                _cardsGrid.Add(tile);
            }

            var empty = cards.Count == 0;
            SetActive(_cardsEmpty, empty);
            SetActive(_cardsGrid, !empty);
        }

        private void OnCardClicked(CardDefinition card)
        {
            // Stufe 5 bringt das Card-Detail-Modal. Bis dahin nur Toast.
            _toast.Show($"{card.Id} ({card.Rarity}, {card.Element})", ToastKind.Info);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static Rarity? ParseRarityFilter(string value) => value switch
        {
            "Gewoehnlich"   => Rarity.Gewoehnlich,
            "Ungewoehnlich" => Rarity.Ungewoehnlich,
            "Selten"        => Rarity.Selten,
            "Epic"          => Rarity.Epic,
            "Legendaer"     => Rarity.Legendaer,
            _               => null
        };

        private static Element? ParseElementFilter(string value) => value switch
        {
            "Natur"  => Element.Natur,
            "Feuer"  => Element.Feuer,
            "Wasser" => Element.Wasser,
            "Licht"  => Element.Licht,
            "Dunkel" => Element.Dunkel,
            _        => null
        };

        private static string FormatNumber(long n)
        {
            if (n >= 1_000_000) return $"{n / 1_000_000.0:0.#}M";
            if (n >= 10_000)    return $"{n / 1_000.0:0.#}K";
            return n.ToString("N0");
        }
    }
}
