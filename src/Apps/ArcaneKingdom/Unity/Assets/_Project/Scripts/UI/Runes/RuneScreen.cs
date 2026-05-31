#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Runes;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.Game.Codex;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Runes
{
    /// <summary>
    /// Runen-Verwaltungs-Screen (Spielplan v5 Kap. 7 + Impl_KOMPLETT Kap. 3, Brocken K12 Teil B).
    /// 4 Slots oben (gesperrt/offen je Spieler-Level, antippbar), Grid mit Filter unten,
    /// Aktive-Runen-Boni-Zusammenfassung, Detail-Overlay + Slot-Leeren.
    ///
    /// Zuweisung: Slot antippen -> aktiviert Auswahlmodus -> Rune im Grid antippen setzt sie in den
    /// Slot. Belegten Slot antippen -> Detail-Overlay mit "Slot leeren". Auto-Save beim Verlassen.
    /// Deck-Slot kommt via ModalContext (vom DeckBuilder), Fallback = aktiver Deck-Slot.
    /// </summary>
    public sealed class RuneScreen : ScreenBase
    {
        /// <summary>ModalContext-Key fuer den zu bearbeitenden Deck-Slot (vom DeckBuilder gesetzt).</summary>
        public const string RuneEditDeckSlotKey = "rune_edit_deck_slot";

        private static readonly RuneType[] FilterTypes =
        {
            RuneType.Angriff, RuneType.Verteidigung, RuneType.Geschwindigkeit,
            RuneType.Element, RuneType.Hero, RuneType.Kombo
        };

        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ILocalizationService _loc;
        private readonly ToastService _toast;
        private readonly UIAssetService _uiAssets;
        private readonly CodexService _codex;
        private readonly ModalContext _modalContext;

        private VisualElement _slotsRow = null!;
        private VisualElement _runeGrid = null!;
        private Label _activeStats = null!;
        private Label _selectionHint = null!;
        private Button _closeBtn = null!;
        private VisualElement _filterRow = null!;
        private Label _title = null!;
        private Label _slotsHeader = null!;
        private Label _filterHeader = null!;
        private Label _gridHeader = null!;

        // Detail-Overlay
        private VisualElement _detailOverlay = null!;
        private Label _detailName = null!;
        private Label _detailType = null!;
        private Label _detailRarity = null!;
        private Label _detailBonus = null!;
        private Button _detailRemove = null!;
        private Button _detailClose = null!;

        private RuneType? _activeFilter;
        private PlayerSave? _saveCache;
        private Deck? _activeDeck;
        private int _selectedSlot = -1;     // -1 = kein Slot im Auswahlmodus
        private int _detailSlotIndex = -1;  // Slot des aktuell im Detail-Overlay gezeigten (für "Slot leeren")
        private bool _dirty;

        public override string Id => ScreenId.Runes;
        protected override string UxmlPath => "UI/RuneScreen";

        public RuneScreen(ScreenManager screenManager,
                          ISaveService<PlayerSave> save,
                          ILocalizationService loc,
                          ToastService toast,
                          UIAssetService uiAssets,
                          CodexService codex,
                          ModalContext modalContext)
        {
            _screenManager = screenManager;
            _save = save;
            _loc = loc;
            _toast = toast;
            _uiAssets = uiAssets;
            _codex = codex;
            _modalContext = modalContext;
        }

        protected override void BindElements(VisualElement root)
        {
            _slotsRow      = Q<VisualElement>("rune-slots-row");
            _runeGrid      = Q<VisualElement>("rune-grid");
            _activeStats   = Q<Label>("rune-active-stats");
            _selectionHint = Q<Label>("rune-selection-hint");
            _closeBtn      = Q<Button>("rune-close");
            _filterRow     = Q<VisualElement>("rune-filter-row");
            _title         = Q<Label>("rune-title");
            _slotsHeader   = Q<Label>("rune-slots-header");
            _filterHeader  = Q<Label>("rune-filter-header");
            _gridHeader    = Q<Label>("rune-grid-header");

            _detailOverlay = Q<VisualElement>("rune-detail-overlay");
            _detailName    = Q<Label>("rune-detail-name");
            _detailType    = Q<Label>("rune-detail-type");
            _detailRarity  = Q<Label>("rune-detail-rarity");
            _detailBonus   = Q<Label>("rune-detail-bonus");
            _detailRemove  = Q<Button>("rune-detail-remove");
            _detailClose   = Q<Button>("rune-detail-close");

            _closeBtn.clicked += () => _screenManager.PopAsync().Forget();
            _detailClose.clicked += HideDetail;
            _detailRemove.clicked += OnDetailRemoveClicked;

            // Header lokalisieren (UXML-Text bleibt DE-Fallback).
            _title.text       = _loc.Get("rune.title", "RUNEN");
            _slotsHeader.text = _loc.Get("rune.slots_header", "Runen-Slots");
            _filterHeader.text = _loc.Get("rune.filter_header", "Filter");
            _gridHeader.text  = _loc.Get("rune.grid_header", "Verfügbare Runen");

            BuildFilterButtons();
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var result = await _save.LoadAsync(ct);
            if (!result.IsSuccess || result.Value == null) return;
            _saveCache = result.Value;

            // Deck-Slot aus ModalContext (vom DeckBuilder) oder aktiver Slot als Fallback.
            // Screens sind gecachte Singletons -> IMMER hier neu lesen, nicht im Ctor.
            var requestedSlot = _modalContext.GetStruct<int>(RuneEditDeckSlotKey);
            var deckIndex = requestedSlot ?? _saveCache.ActiveDeckSlot;
            deckIndex = Math.Clamp(deckIndex, 0, Math.Max(0, _saveCache.Decks.Count - 1));
            _activeDeck = _saveCache.Decks.Count > 0 ? _saveCache.Decks[deckIndex] : null;
            _selectedSlot = -1;
            _dirty = false;
            HideDetail();
            Refresh();
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            _modalContext.Remove(RuneEditDeckSlotKey);
            if (_dirty) SaveAsync().Forget();
            return UniTask.CompletedTask;
        }

        private void BuildFilterButtons()
        {
            _filterRow.Clear();
            AddFilter(null);
            foreach (var t in FilterTypes) AddFilter(t);
        }

        private void AddFilter(RuneType? type)
        {
            var btn = new Button(() => { _activeFilter = type; Refresh(); }) { text = FilterLabel(type) };
            btn.AddToClassList("ak-btn");
            btn.AddToClassList("ak-btn--sm");
            btn.style.marginRight = 8;
            btn.style.marginBottom = 6;
            _filterRow.Add(btn);
        }

        private string FilterLabel(RuneType? t) => t switch
        {
            null => _loc.Get("rune.filter_all", "Alle"),
            RuneType.Angriff => _loc.Get("rune.filter_angriff", "Angriff"),
            RuneType.Verteidigung => _loc.Get("rune.filter_verteidigung", "Verteidigung"),
            RuneType.Geschwindigkeit => _loc.Get("rune.filter_geschwindigkeit", "Geschwindigkeit"),
            RuneType.Element => _loc.Get("rune.filter_element", "Element"),
            RuneType.Hero => _loc.Get("rune.filter_hero", "Held"),
            _ => _loc.Get("rune.filter_kombo", "Kombo")
        };

        private void Refresh()
        {
            if (_saveCache == null) return;
            BuildSlots(_saveCache);
            BuildGrid(_saveCache);
            BuildActiveStats(_saveCache);
            UpdateSelectionHint();
        }

        private void UpdateSelectionHint()
        {
            if (_selectedSlot >= 0)
            {
                _selectionHint.text = _loc.GetFormatted("rune.select_for_slot", _selectedSlot + 1);
                _selectionHint.style.display = DisplayStyle.Flex;
            }
            else
            {
                _selectionHint.style.display = DisplayStyle.None;
            }
        }

        private void BuildSlots(PlayerSave save)
        {
            _slotsRow.Clear();
            var playerLevel = save.Profile.Level;

            for (var s = 1; s <= RuneSlotUnlock.MaxSlots; s++)
            {
                var slotIndex = s - 1;
                var slot = new VisualElement();
                slot.style.width = 80;
                slot.style.height = 80;
                slot.style.marginRight = 12;
                slot.style.alignItems = Align.Center;
                slot.style.justifyContent = Justify.Center;
                slot.style.borderTopLeftRadius = 12;
                slot.style.borderTopRightRadius = 12;
                slot.style.borderBottomLeftRadius = 12;
                slot.style.borderBottomRightRadius = 12;

                var unlocked = RuneSlotUnlock.IsUnlocked(s, playerLevel);
                slot.style.backgroundColor = unlocked
                    ? new StyleColor(new UnityEngine.Color(0.20f, 0.20f, 0.30f))
                    : new StyleColor(new UnityEngine.Color(0.10f, 0.10f, 0.15f));

                // Auswahlmodus -> Gold-Highlight (Layout-Inline, kein var()).
                if (_selectedSlot == slotIndex)
                {
                    slot.style.borderTopWidth = 2; slot.style.borderBottomWidth = 2;
                    slot.style.borderLeftWidth = 2; slot.style.borderRightWidth = 2;
                    var gold = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f));
                    slot.style.borderTopColor = gold; slot.style.borderBottomColor = gold;
                    slot.style.borderLeftColor = gold; slot.style.borderRightColor = gold;
                }

                var assignedInstId = _activeDeck != null && slotIndex < _activeDeck.RuneInstanceIds.Count
                    ? _activeDeck.RuneInstanceIds[slotIndex] : null;

                if (!unlocked)
                {
                    var lockLabel = new Label($"{_loc.Get("rune.slot_locked", "Gesperrt")}\nLV {RuneSlotUnlock.MinLevelForSlot(s)}");
                    lockLabel.style.color = new StyleColor(new UnityEngine.Color(0.55f, 0.55f, 0.65f));
                    lockLabel.style.fontSize = 12;
                    lockLabel.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                    lockLabel.style.whiteSpace = WhiteSpace.Normal;
                    slot.Add(lockLabel);
                }
                else if (!string.IsNullOrEmpty(assignedInstId)
                         && save.RuneInventory.TryGetValue(assignedInstId!, out var inst)
                         && _codex.FindRune(inst.RuneDefinitionId) is { } def)
                {
                    var icon = new VisualElement();
                    icon.style.width = 48; icon.style.height = 48; icon.style.marginBottom = 2;
                    _uiAssets.ApplyBackground(icon, $"Runes/{inst.RuneDefinitionId}", UnityEngine.ScaleMode.ScaleToFit);
                    slot.Add(icon);
                    var name = new Label(_loc.Get(def.DisplayNameKey, def.Id));
                    name.style.color = new StyleColor(UnityEngine.Color.white);
                    name.style.fontSize = 10;
                    name.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                    name.style.whiteSpace = WhiteSpace.Normal;
                    slot.Add(name);
                    slot.RegisterCallback<ClickEvent>(_ => OnSlotClicked(slotIndex));
                }
                else
                {
                    var plus = new Label(_loc.Get("rune.empty_slot", "+ Rune"));
                    plus.style.color = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f));
                    plus.style.fontSize = 13;
                    plus.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                    slot.Add(plus);
                    slot.RegisterCallback<ClickEvent>(_ => OnSlotClicked(slotIndex));
                }

                _slotsRow.Add(slot);
            }
        }

        private void BuildGrid(PlayerSave save)
        {
            _runeGrid.Clear();
            if (save.RuneInventory.Count == 0)
            {
                _runeGrid.Add(new Label(_loc.Get("rune.no_runes",
                    "Noch keine Runen gesammelt — kämpfe Welt-Bosse für Runen-Drops.")));
                return;
            }

            foreach (var (instId, runeInst) in save.RuneInventory)
            {
                var def = _codex.FindRune(runeInst.RuneDefinitionId);
                if (def == null) continue;
                if (_activeFilter != null && def.Type != _activeFilter.Value) continue;

                var alreadyEquipped = _activeDeck != null && _activeDeck.RuneInstanceIds.Contains(instId);

                var tile = new VisualElement();
                tile.style.width = 110;
                tile.style.height = 130;
                tile.style.marginRight = 8;
                tile.style.marginBottom = 8;
                tile.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.20f, 0.16f, 0.32f));
                tile.style.borderTopLeftRadius = 10;
                tile.style.borderTopRightRadius = 10;
                tile.style.borderBottomLeftRadius = 10;
                tile.style.borderBottomRightRadius = 10;
                tile.style.alignItems = Align.Center;
                tile.style.justifyContent = Justify.Center;
                tile.style.paddingTop = 6;
                tile.style.paddingBottom = 6;
                if (alreadyEquipped) tile.style.opacity = 0.4f;

                var icon = new VisualElement();
                icon.style.width = 64; icon.style.height = 64; icon.style.marginBottom = 4;
                _uiAssets.ApplyBackground(icon, $"Runes/{runeInst.RuneDefinitionId}", UnityEngine.ScaleMode.ScaleToFit);
                tile.Add(icon);

                var name = new Label(_loc.Get(def.DisplayNameKey, def.Id));
                name.style.fontSize = 11;
                name.style.color = new StyleColor(UnityEngine.Color.white);
                name.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                name.style.whiteSpace = WhiteSpace.Normal;
                tile.Add(name);

                tile.RegisterCallback<ClickEvent>(_ => OnRuneTileClicked(instId, runeInst));
                _runeGrid.Add(tile);
            }
        }

        private void BuildActiveStats(PlayerSave save)
        {
            var unlocked = RuneSlotUnlock.UnlockedSlotCount(save.Profile.Level);
            var slotLine = _loc.GetFormatted("rune.slots_open", unlocked, RuneSlotUnlock.MaxSlots, save.Profile.Level);

            if (_activeDeck == null)
            {
                _activeStats.text = slotLine;
                return;
            }

            var lo = RuneLoadoutBuilder.Build(_activeDeck, save, id => _codex.FindRune(id), id => _codex.FindCard(id));
            if (lo == null)
            {
                _activeStats.text = $"{slotLine}\n{_loc.Get("rune.bonus_none", "Keine Runen aktiv")}";
                return;
            }

            var parts = new List<string>();
            if (lo.AttackPercent > 0) parts.Add($"+{lo.AttackPercent:0}% ATK");
            if (lo.HealthPercent > 0) parts.Add($"+{lo.HealthPercent:0}% HP");
            if (lo.HeroHpFlat > 0) parts.Add($"+{lo.HeroHpFlat} Hero-HP");
            if (lo.SpecialTurnReduction > 0) parts.Add($"-{lo.SpecialTurnReduction} Cooldown");
            if (lo.BonusStartMana > 0) parts.Add($"+{lo.BonusStartMana} Mana");
            foreach (var kv in lo.ElementDamagePercent) parts.Add($"+{kv.Value:0}% {kv.Key}");
            if (lo.ComboDaemonActive) parts.Add($"+{lo.ComboDaemonAtkPercent:0}% Dämonen-Kombo");
            if (lo.ComboDracheActive) parts.Add($"+{lo.ComboDracheAtkPercent:0}% Drachen-Kombo");

            _activeStats.text = $"{slotLine}\n{_loc.Get("rune.active_bonuses", "Aktive Boni")}: {string.Join(" · ", parts)}";
        }

        // ==========================================================================
        // Interaktion
        // ==========================================================================

        private void OnSlotClicked(int slotIndex)
        {
            var assigned = _activeDeck != null && slotIndex < _activeDeck.RuneInstanceIds.Count
                ? _activeDeck.RuneInstanceIds[slotIndex] : null;
            if (!string.IsNullOrEmpty(assigned)) { ShowRuneDetail(assigned!, slotIndex); return; }
            _selectedSlot = slotIndex;
            Refresh();
        }

        private void OnRuneTileClicked(string runeInstId, ArcaneKingdom.Domain.Runes.RuneInstance inst)
        {
            if (_activeDeck == null || _saveCache == null) return;

            // Kein Slot im Auswahlmodus -> nur Detail zeigen.
            if (_selectedSlot < 0) { ShowRuneDetail(runeInstId, -1); return; }

            if (!RuneSlotUnlock.IsUnlocked(_selectedSlot + 1, _saveCache.Profile.Level))
            {
                _toast.Show(_loc.Get("rune.slot_still_locked", "Dieser Slot ist noch gesperrt."), ToastKind.Warning);
                return;
            }
            if (_activeDeck.RuneInstanceIds.Contains(runeInstId))
            {
                _toast.Show(_loc.Get("rune.already_equipped", "Diese Rune ist bereits eingesetzt."), ToastKind.Warning);
                return;
            }
            _activeDeck.RuneInstanceIds[_selectedSlot] = runeInstId;
            _selectedSlot = -1;
            MarkDirty();
            Refresh();
        }

        private void ShowRuneDetail(string runeInstId, int slotIndex)
        {
            if (_saveCache == null) return;
            if (!_saveCache.RuneInventory.TryGetValue(runeInstId, out var inst)) return;
            var def = _codex.FindRune(inst.RuneDefinitionId);
            if (def == null) return;

            _detailName.text = _loc.Get(def.DisplayNameKey, def.Id);
            _detailType.text = $"{_loc.Get("rune.detail_type", "Typ")}: {FilterLabel(def.Type)}";
            _detailRarity.text = $"{_loc.Get("rune.detail_rarity", "Seltenheit")}: {def.Rarity}";
            _detailBonus.text = _loc.GetFormatted(def.DescriptionKey, def.CalculateMagnitudeAtLevel(inst.Level));

            _detailSlotIndex = slotIndex;
            if (slotIndex >= 0)
            {
                _detailRemove.style.display = DisplayStyle.Flex;
                _detailRemove.text = _loc.Get("rune.remove_from_slot", "Slot leeren");
            }
            else
            {
                _detailRemove.style.display = DisplayStyle.None;
            }
            _detailClose.text = _loc.Get("confirm.cancel", "Schließen");
            _detailOverlay.style.display = DisplayStyle.Flex;
        }

        private void OnDetailRemoveClicked()
        {
            if (_detailSlotIndex >= 0) RemoveRuneFromSlot(_detailSlotIndex);
            HideDetail();
        }

        private void HideDetail()
        {
            _detailSlotIndex = -1;
            _detailOverlay.style.display = DisplayStyle.None;
        }

        private void RemoveRuneFromSlot(int slotIndex)
        {
            if (_activeDeck == null || slotIndex >= _activeDeck.RuneInstanceIds.Count) return;
            _activeDeck.RuneInstanceIds[slotIndex] = null;
            MarkDirty();
            Refresh();
        }

        private void MarkDirty() => _dirty = true;

        private async UniTask SaveAsync()
        {
            if (_saveCache == null || _activeDeck == null) return;
            _activeDeck.LastModifiedUtc = DateTime.UtcNow;
            await _save.SaveAsync(_saveCache, CancellationToken.None);
            _dirty = false;
            _toast.Show(_loc.Get("rune.saved", "Runen gespeichert."), ToastKind.Success);
        }
    }
}
