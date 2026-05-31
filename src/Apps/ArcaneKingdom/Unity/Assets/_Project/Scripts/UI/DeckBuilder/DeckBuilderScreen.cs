#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.UI.Common;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.DeckBuilder
{
    /// <summary>
    /// Deck-Builder mit Sammlung (links) + aktuellem Deck (rechts). Klick auf Sammlungs-Karte
    /// fügt sie hinzu, Klick auf Deck-Eintrag entfernt sie. Live-Validierung zeigt Status.
    /// </summary>
    public sealed class DeckBuilderScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly CardCatalogService _cardCatalog;
        private readonly ToastService _toast;

        // Header
        private Button _backBtn = null!;
        private DropdownField _slotSelector = null!;
        private TextField _nameInput = null!;
        private Button _suggestBtn = null!;
        private Button _saveBtn = null!;

        // Sammlung
        private TextField _cardsSearch = null!;
        private DropdownField _cardsRarity = null!;
        private VisualElement _collectionGrid = null!;
        private Label _collectionEmpty = null!;

        // Deck-Panel
        private Label _statsCount = null!;
        private Label _statsCost = null!;
        private Label _validationStatus = null!;
        private VisualElement _cardsList = null!;
        private Button _clearBtn = null!;

        private PlayerSave? _saveCached;
        private Deck? _editingDeck;
        private bool _dirty;

        public override string Id => ScreenId.DeckBuilder;
        protected override string UxmlPath => "UI/DeckBuilderScreen";

        private readonly UIAssetService _uiAssets;
        private VisualElement _heroPortrait = null!;

        public DeckBuilderScreen(ScreenManager screenManager,
                                 ISaveService<PlayerSave> save,
                                 CardCatalogService cardCatalog,
                                 ToastService toast,
                                 UIAssetService uiAssets)
        {
            _screenManager = screenManager;
            _save = save;
            _cardCatalog = cardCatalog;
            _toast = toast;
            _uiAssets = uiAssets;
        }

        protected override void BindElements(VisualElement root)
        {
            _backBtn         = Q<Button>("deck-back-button");
            _heroPortrait    = Q<VisualElement>("deck-hero-portrait");
            _slotSelector    = Q<DropdownField>("deck-slot-selector");
            _nameInput       = Q<TextField>("deck-name-input");
            _suggestBtn      = Q<Button>("deck-suggest-button");
            _saveBtn         = Q<Button>("deck-save-button");

            _cardsSearch     = Q<TextField>("deck-cards-search");
            _cardsRarity     = Q<DropdownField>("deck-cards-rarity");
            _collectionGrid  = Q<VisualElement>("deck-collection-grid");
            _collectionEmpty = Q<Label>("deck-collection-empty");

            _statsCount       = Q<Label>("deck-stats-count");
            _statsCost        = Q<Label>("deck-stats-cost");
            _validationStatus = Q<Label>("deck-validation-status");
            _cardsList        = Q<VisualElement>("deck-cards-list");
            _clearBtn         = Q<Button>("deck-clear-button");

            _cardsRarity.choices = new List<string> { "Alle Raritaeten",
                "Gewoehnlich", "Ungewoehnlich", "Selten", "Epic", "Legendaer" };
            _cardsRarity.index = 0;

            _backBtn.clicked += OnBack;
            _saveBtn.clicked += () => SaveAsync().Forget();
            _suggestBtn.clicked += OnSuggest;
            _clearBtn.clicked += OnClearDeck;
            _nameInput.RegisterValueChangedCallback(evt =>
            {
                if (_editingDeck != null && _editingDeck.Name != evt.newValue)
                {
                    _editingDeck.Name = evt.newValue;
                    MarkDirty();
                }
            });
            _slotSelector.RegisterValueChangedCallback(_ => OnSlotChanged());
            _cardsSearch.RegisterValueChangedCallback(_ => RefreshCollection());
            _cardsRarity.RegisterValueChangedCallback(_ => RefreshCollection());
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var saveResult = await _save.LoadAsync(ct);
            if (!saveResult.IsSuccess)
            {
                _toast.Show("Save laden fehlgeschlagen.", ToastKind.Danger);
                return;
            }
            _saveCached = saveResult.Value;
            // Hero-Portrait pro gewaehlter Rasse aus Save
            var race = _saveCached?.Story?.ChosenRace ?? ArcaneKingdom.Domain.Cards.Race.Ritter;
            _uiAssets.ApplyHeroPortrait(_heroPortrait, race);
            InitializeSlotSelector();
            LoadActiveDeck();
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            if (_dirty)
                _toast.Show("Hinweis: Aenderungen wurden NICHT gespeichert.", ToastKind.Warning);
            return UniTask.CompletedTask;
        }

        // ============================================================
        // Slot-Selector
        // ============================================================

        private void InitializeSlotSelector()
        {
            if (_saveCached == null) return;
            _slotSelector.choices = _saveCached.Decks
                .Select(d => $"Slot {d.SlotIndex + 1}: {d.Name}")
                .ToList();
            // Falls noch keine Slots: 1 anlegen
            if (_saveCached.Decks.Count == 0)
            {
                _saveCached.Decks.Add(new Deck(0, "Deck 1"));
                _slotSelector.choices = new List<string> { "Slot 1: Deck 1" };
            }
            _slotSelector.index = System.Math.Min(_saveCached.ActiveDeckSlot, _saveCached.Decks.Count - 1);
        }

        private void OnSlotChanged()
        {
            if (_saveCached == null) return;
            if (_dirty)
                _toast.Show("Ungespeicherte Aenderungen wurden verworfen.", ToastKind.Warning);
            _saveCached.ActiveDeckSlot = _slotSelector.index;
            LoadActiveDeck();
        }

        private void LoadActiveDeck()
        {
            if (_saveCached == null || _saveCached.Decks.Count == 0) return;
            _editingDeck = _saveCached.Decks[_saveCached.ActiveDeckSlot];
            _nameInput.SetValueWithoutNotify(_editingDeck.Name);
            _dirty = false;
            RefreshCollection();
            RefreshDeckPanel();
        }

        // ============================================================
        // Sammlung
        // ============================================================

        private void RefreshCollection()
        {
            _collectionGrid.Clear();
            if (_saveCached == null) { return; }

            var rarity = ParseRarity(_cardsRarity.value);
            var search = (_cardsSearch.value ?? string.Empty).Trim().ToLowerInvariant();

            // Gruppiere Sammlung nach CardDefinitionId — zeige nur Karten die NICHT
            // bereits maximal im Deck sind.
            var inDeckPerDef = CountPerDefinitionInDeck();
            var ownedPerDef = new Dictionary<string, List<KeyValuePair<string, CardInstance>>>();
            foreach (var kv in _saveCached.CardInventory)
            {
                if (!ownedPerDef.TryGetValue(kv.Value.CardDefinitionId, out var list))
                {
                    list = new List<KeyValuePair<string, CardInstance>>();
                    ownedPerDef[kv.Value.CardDefinitionId] = list;
                }
                list.Add(kv);
            }

            var anyShown = false;
            foreach (var grp in ownedPerDef)
            {
                var def = _cardCatalog.Find(grp.Key);
                if (def == null) continue;
                if (rarity != null && def.Rarity != rarity) continue;
                if (!string.IsNullOrEmpty(search) && !def.Id.ToLowerInvariant().Contains(search)) continue;

                var owned = grp.Value.Count;
                var inDeck = inDeckPerDef.TryGetValue(def.Id, out var c) ? c : 0;
                var available = owned - inDeck;
                if (available <= 0) continue;

                var tile = CardTileFactory.Build(def, onClick: _ => AddToDeck(grp.Value, inDeck), locked: false);
                var qty = new Label($"x{available}");
                qty.AddToClassList("ak-caption");
                qty.style.position = Position.Absolute;
                qty.style.bottom = 4;
                qty.style.right = 6;
                tile.Add(qty);
                _collectionGrid.Add(tile);
                anyShown = true;
            }

            if (anyShown)
            {
                _collectionEmpty.AddToClassList("ak-hidden");
            }
            else
            {
                _collectionEmpty.RemoveFromClassList("ak-hidden");
            }
        }

        private void AddToDeck(List<KeyValuePair<string, CardInstance>> available, int alreadyInDeck)
        {
            if (_editingDeck == null) return;
            if (_editingDeck.CardInstanceIds.Count >= Deck.MaxCards)
            {
                _toast.Show($"Maximal {Deck.MaxCards} Karten erlaubt.", ToastKind.Warning);
                return;
            }
            // Erste nicht-im-Deck Instanz hinzufuegen
            foreach (var kv in available)
            {
                if (_editingDeck.CardInstanceIds.Contains(kv.Key)) continue;
                _editingDeck.CardInstanceIds.Add(kv.Key);
                MarkDirty();
                RefreshCollection();
                RefreshDeckPanel();
                return;
            }
        }

        // ============================================================
        // Deck-Panel
        // ============================================================

        private void RefreshDeckPanel()
        {
            _cardsList.Clear();
            if (_editingDeck == null || _saveCached == null) return;

            foreach (var instanceId in _editingDeck.CardInstanceIds.ToList())
            {
                if (!_saveCached.CardInventory.TryGetValue(instanceId, out var inst)) continue;
                var def = _cardCatalog.Find(inst.CardDefinitionId);
                if (def == null) continue;

                var row = new VisualElement();
                row.AddToClassList("ak-surface");
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4;

                var cost = new Label(def.Cost.ToString());
                cost.AddToClassList("ak-h4");
                cost.AddToClassList("ak-text--accent");
                cost.style.width = 28;
                cost.style.unityTextAlign = TextAnchor.MiddleCenter;
                row.Add(cost);

                var name = new Label(def.Id);
                name.AddToClassList("ak-body");
                name.style.flexGrow = 1;
                name.style.marginLeft = 8;
                row.Add(name);

                var remove = new Button(() => RemoveFromDeck(instanceId)) { text = "X" };
                remove.AddToClassList("ak-btn");
                remove.AddToClassList("ak-btn--sm");
                remove.AddToClassList("ak-btn--ghost");
                remove.AddToClassList("ak-btn--icon");
                row.Add(remove);

                _cardsList.Add(row);
            }

            UpdateDeckStats();
        }

        private void RemoveFromDeck(string instanceId)
        {
            if (_editingDeck == null) return;
            _editingDeck.CardInstanceIds.Remove(instanceId);
            MarkDirty();
            RefreshCollection();
            RefreshDeckPanel();
        }

        private void UpdateDeckStats()
        {
            if (_editingDeck == null || _saveCached == null) return;

            var defs = _cardCatalog.AllCards.ToDictionary(c => c.Id, c => c);
            _statsCount.text = $"{_editingDeck.CardInstanceIds.Count}/{Deck.MaxCards}";
            _statsCost.text = _editingDeck.TotalCost(defs, _saveCached.CardInventory).ToString();

            var validation = DeckValidator.Validate(_editingDeck, _saveCached.CardInventory, defs);
            if (validation.IsValid)
            {
                _validationStatus.text = "Bereit";
                _validationStatus.RemoveFromClassList("ak-text--danger");
                _validationStatus.RemoveFromClassList("ak-text--warning");
                _validationStatus.AddToClassList("ak-text--success");
            }
            else
            {
                _validationStatus.text = validation.Code switch
                {
                    DeckValidator.ValidationCode.EmptyDeck => "Deck ist leer",
                    DeckValidator.ValidationCode.TooManyCards => "Zu viele Karten",
                    DeckValidator.ValidationCode.CardLimitExceeded => $"Limit ueberschritten: {validation.OffendingCardId}",
                    DeckValidator.ValidationCode.UniqueCardDuplicated => $"Unique-Karte doppelt: {validation.OffendingCardId}",
                    DeckValidator.ValidationCode.CopyLimitExceeded => $"Mehr als 3 Kopien: {validation.OffendingCardId}",
                    _ => $"Fehler: {validation.Code}"
                };
                _validationStatus.RemoveFromClassList("ak-text--success");
                _validationStatus.AddToClassList("ak-text--danger");
            }
        }

        private void OnClearDeck()
        {
            if (_editingDeck == null) return;
            _editingDeck.CardInstanceIds.Clear();
            MarkDirty();
            RefreshCollection();
            RefreshDeckPanel();
        }

        // ============================================================
        // Suggest + Save
        // ============================================================

        private void OnSuggest()
        {
            // DeckBuilderService nutzen — hier vereinfacht: maximal kostentragend
            _toast.Show("Auto-Vorschlag: Service ist DeckBuilderService, Integration folgt.", ToastKind.Info);
        }

        private async UniTask SaveAsync()
        {
            if (_editingDeck == null || _saveCached == null) return;

            var defs = _cardCatalog.AllCards.ToDictionary(c => c.Id, c => c);
            var validation = DeckValidator.Validate(_editingDeck, _saveCached.CardInventory, defs);
            if (!validation.IsValid && validation.Code != DeckValidator.ValidationCode.EmptyDeck)
            {
                _toast.Show($"Deck ungueltig: {_validationStatus.text}", ToastKind.Danger);
                return;
            }

            _editingDeck.LastModifiedUtc = System.DateTime.UtcNow;
            await _save.SaveAsync(_saveCached, CancellationToken.None);
            _dirty = false;
            _toast.Show("Deck gespeichert.", ToastKind.Success);
            GameLogger.Info("Deck", $"Slot {_editingDeck.SlotIndex} gespeichert ({_editingDeck.CardInstanceIds.Count} Karten).");
        }

        private void OnBack() => _screenManager.PopAsync().Forget();

        // ============================================================
        // Helpers
        // ============================================================

        private void MarkDirty()
        {
            _dirty = true;
            UpdateDeckStats();
        }

        private Dictionary<string, int> CountPerDefinitionInDeck()
        {
            var result = new Dictionary<string, int>();
            if (_editingDeck == null || _saveCached == null) return result;
            foreach (var instId in _editingDeck.CardInstanceIds)
            {
                if (!_saveCached.CardInventory.TryGetValue(instId, out var inst)) continue;
                result[inst.CardDefinitionId] = result.TryGetValue(inst.CardDefinitionId, out var c) ? c + 1 : 1;
            }
            return result;
        }

        private static Rarity? ParseRarity(string value) => value switch
        {
            "Gewoehnlich"   => Rarity.Gewoehnlich,
            "Ungewoehnlich" => Rarity.Ungewoehnlich,
            "Selten"        => Rarity.Selten,
            "Epic"          => Rarity.Epic,
            "Legendaer"     => Rarity.Legendaer,
            "Mythisch"      => Rarity.Mythisch,
            _               => null
        };
    }
}
