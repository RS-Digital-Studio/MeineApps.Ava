#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.UI.Common;
using ArcaneKingdom.UI.Foundation;
using ArcaneKingdom.UI.Modals;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Codex
{
    /// <summary>
    /// Karten-Lexikon. Zeigt alle Karten aus dem CardCatalog, markiert besessene farblich,
    /// nicht-besessene als locked. Klick auf Karte oeffnet das CardDetailModal.
    /// </summary>
    public sealed class CodexScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly CardCatalogService _cardCatalog;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ModalContext _modalContext;

        private Button _backBtn = null!;
        private Label _completion = null!;
        private TextField _search = null!;
        private DropdownField _elementFilter = null!;
        private Toggle _ownedOnly = null!;
        private VisualElement _grid = null!;

        private HashSet<string> _ownedDefIds = new();

        public override string Id => ScreenId.Codex;
        protected override string UxmlPath => "UI/CodexScreen";

        public CodexScreen(ScreenManager screenManager,
                           CardCatalogService cardCatalog,
                           ISaveService<PlayerSave> save,
                           ModalContext modalContext)
        {
            _screenManager = screenManager;
            _cardCatalog = cardCatalog;
            _save = save;
            _modalContext = modalContext;
        }

        protected override void BindElements(VisualElement root)
        {
            _backBtn       = Q<Button>("codex-back-button");
            _completion    = Q<Label>("codex-completion");
            _search        = Q<TextField>("codex-search");
            _elementFilter = Q<DropdownField>("codex-element-filter");
            _ownedOnly     = Q<Toggle>("codex-owned-only");
            _grid          = Q<VisualElement>("codex-grid");

            _elementFilter.choices = new List<string> { "Alle", "Natur", "Feuer", "Wasser", "Erde", "Licht", "Dunkel" };
            _elementFilter.index = 0;

            _backBtn.clicked += () => _screenManager.PopAsync().Forget();
            _search.RegisterValueChangedCallback(_ => RefreshGrid());
            _elementFilter.RegisterValueChangedCallback(_ => RefreshGrid());
            _ownedOnly.RegisterValueChangedCallback(_ => RefreshGrid());
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var result = await _save.LoadAsync(ct);
            _ownedDefIds = result.IsSuccess && result.Value != null
                ? new HashSet<string>(result.Value.CardInventory.Values
                    .Select(i => i.CardDefinitionId))
                : new HashSet<string>();

            var allCount = _cardCatalog.AllCards.Count;
            var ownedCount = _ownedDefIds.Count(id => _cardCatalog.Find(id) != null);
            _completion.text = $"{ownedCount} / {allCount} entdeckt";

            RefreshGrid();
        }

        private void RefreshGrid()
        {
            _grid.Clear();

            var element = ParseElement(_elementFilter.value);
            var search = (_search.value ?? string.Empty).Trim().ToLowerInvariant();
            var ownedOnly = _ownedOnly.value;

            var cards = _cardCatalog.AllCards
                .Where(c => element == null || c.Element == element)
                .Where(c => string.IsNullOrEmpty(search) || c.Id.ToLowerInvariant().Contains(search))
                .Where(c => !ownedOnly || _ownedDefIds.Contains(c.Id))
                .OrderBy(c => c.Rarity).ThenBy(c => c.Element).ThenBy(c => c.Id);

            foreach (var card in cards)
            {
                var owned = _ownedDefIds.Contains(card.Id);
                _grid.Add(CardTileFactory.Build(card,
                    onClick: c => OnCardClicked(c),
                    locked: !owned));
            }
        }

        private void OnCardClicked(CardDefinition card)
        {
            _modalContext.Set(CardDetailModal.ContextKey, card);
            _screenManager.PushAsync(ScreenId.CardDetailOverlay).Forget();
        }

        private static Element? ParseElement(string value) => value switch
        {
            "Natur"  => Element.Natur,
            "Feuer"  => Element.Feuer,
            "Wasser" => Element.Wasser,
            "Erde"   => Element.Erde,
            "Licht"  => Element.Licht,
            "Dunkel" => Element.Dunkel,
            _        => null
        };
    }
}
