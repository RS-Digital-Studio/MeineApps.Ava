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
using ArcaneKingdom.Game.Crafting;
using ArcaneKingdom.UI.Common;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Schmiede
{
    /// <summary>
    /// Karten-Schmiede (Designplan v4 Kap. 5 + Kap. 6 Tausch).
    /// Drei Modi:
    ///   1. Kategorie-Crafting: 3-4 gleiche Rasse+Rarity → zufaellige hoehere
    ///   2. Festes Rezept: spezifische Karten-Kombination → exakte Karte
    ///   3. Inventar-Bereinigung: ueberschuessige Karten in Scraps konvertieren (Phase 2)
    ///
    /// Workflow:
    ///   - Spieler waehlt 2-4 Karten aus seinem Inventar
    ///   - SchmiedeScreen ruft FusionAppService.FindMatchingRecipe + PreviewCategoryFusion
    ///   - Zeigt: Pool moeglicher Ergebnisse + Gold-Kosten + Letzte-Kopie-Warnung
    ///   - "Schmieden"-Button mit Confirmation-Dialog
    /// </summary>
    public sealed class SchmiedeScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly CardCatalogService _catalog;
        private readonly FusionAppService _fusion;
        private readonly ILocalizationService _loc;
        private readonly ToastService _toast;

        // UI-Elemente
        private VisualElement _inputSlots = null!;
        private VisualElement _inventoryList = null!;
        private VisualElement _previewPanel = null!;
        private Label _previewTitle = null!;
        private Label _previewGoldCost = null!;
        private Label _previewWarning = null!;
        private Button _fuseButton = null!;
        private Button _clearButton = null!;
        private Button _backButton = null!;

        // Selection-State
        private readonly List<string> _selectedInstanceIds = new(4);
        private PlayerSave? _cachedSave;

        public override string Id => ScreenId.Schmiede;
        protected override string UxmlPath => "UI/SchmiedeScreen";

        private readonly UIAssetService _uiAssets;

        public SchmiedeScreen(
            ScreenManager screenManager,
            ISaveService<PlayerSave> save,
            CardCatalogService catalog,
            FusionAppService fusion,
            ILocalizationService loc,
            ToastService toast,
            UIAssetService uiAssets)
        {
            _screenManager = screenManager;
            _save = save;
            _catalog = catalog;
            _fusion = fusion;
            _loc = loc;
            _toast = toast;
            _uiAssets = uiAssets;
        }

        protected override void BindElements(VisualElement root)
        {
            _uiAssets.ApplyUIBackground(root, "zauberschmiede");
            _inputSlots = Q<VisualElement>("input-slots");
            _inventoryList = Q<VisualElement>("inventory-list");
            _previewPanel = Q<VisualElement>("preview-panel");
            _previewTitle = Q<Label>("preview-title");
            _previewGoldCost = Q<Label>("preview-gold-cost");
            _previewWarning = Q<Label>("preview-warning");
            _fuseButton = Q<Button>("fuse-button");
            _clearButton = Q<Button>("clear-button");
            _backButton = Q<Button>("back-button");

            _fuseButton.clicked += OnFuseClicked;
            _clearButton.clicked += ClearSelection;
            _backButton.clicked += OnBackClicked;
            _fuseButton.SetEnabled(false);
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var saveResult = await _save.LoadAsync(ct);
            if (!saveResult.IsSuccess || saveResult.Value == null)
            {
                _toast.Show(_loc.Get("schmiede.no_save") ?? "Save nicht geladen", ToastKind.Danger);
                return;
            }
            _cachedSave = saveResult.Value;
            RebuildInventoryList();
            UpdatePreview();
        }

        // ==========================================================================
        // Inventar-Anzeige
        // ==========================================================================

        private void RebuildInventoryList()
        {
            _inventoryList.Clear();
            if (_cachedSave == null) return;

            // Karten nach Rasse + Rarity sortieren, gleiche Definition gruppieren
            var grouped = _cachedSave.CardInventory.Values
                .GroupBy(inst => inst.CardDefinitionId)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var group in grouped)
            {
                if (!_catalog.TryFind(group.Key, out var def)) continue;
                if (!def.CanBeUsedInFusion) continue;   // Premium-Karten ausblenden

                var count = group.Count();
                var firstInstance = group.First();
                var tile = CardTileFactory.Build(
                    def,
                    onClick: _ => ToggleSelection(firstInstance.InstanceId),
                    locked: _selectedInstanceIds.Contains(firstInstance.InstanceId),
                    size: CardTileFactory.TileSize.Small,
                    currentLevel: firstInstance.Level);

                var stackLabel = new Label($"x{count}");
                stackLabel.AddToClassList("ak-schmiede__stack-count");
                tile.Add(stackLabel);

                _inventoryList.Add(tile);
            }
        }

        // ==========================================================================
        // Selection-Logik
        // ==========================================================================

        private void ToggleSelection(string instanceId)
        {
            if (_selectedInstanceIds.Contains(instanceId))
            {
                _selectedInstanceIds.Remove(instanceId);
            }
            else
            {
                if (_selectedInstanceIds.Count >= 4)
                {
                    _toast.Show(_loc.Get("schmiede.max_selection") ?? "Max. 4 Karten waehlbar", ToastKind.Warning);
                    return;
                }
                _selectedInstanceIds.Add(instanceId);
            }
            RefreshInputSlots();
            RebuildInventoryList();
            UpdatePreview();
        }

        private void RefreshInputSlots()
        {
            _inputSlots.Clear();
            if (_cachedSave == null) return;

            for (var i = 0; i < 4; i++)
            {
                var slot = new VisualElement { name = $"input-slot-{i}" };
                slot.AddToClassList("ak-schmiede__input-slot");
                if (i < _selectedInstanceIds.Count)
                {
                    if (_cachedSave.CardInventory.TryGetValue(_selectedInstanceIds[i], out var inst) &&
                        _catalog.TryFind(inst.CardDefinitionId, out var def))
                    {
                        slot.Add(CardTileFactory.Build(def, size: CardTileFactory.TileSize.Small,
                                                       currentLevel: inst.Level));
                    }
                }
                else
                {
                    slot.AddToClassList("ak-schmiede__input-slot--empty");
                }
                _inputSlots.Add(slot);
            }
        }

        private void ClearSelection()
        {
            _selectedInstanceIds.Clear();
            RefreshInputSlots();
            RebuildInventoryList();
            UpdatePreview();
        }

        // ==========================================================================
        // Vorschau (Festes Rezept ODER Kategorie-Fusion)
        // ==========================================================================

        private void UpdatePreview()
        {
            if (_cachedSave == null || _selectedInstanceIds.Count == 0)
            {
                _previewTitle.text = _loc.Get("schmiede.select_cards") ?? "Karten auswaehlen";
                _previewGoldCost.text = string.Empty;
                _previewWarning.text = string.Empty;
                _fuseButton.SetEnabled(false);
                return;
            }

            // 1. Festes Rezept pruefen
            var recipe = _fusion.FindMatchingRecipe(_selectedInstanceIds, _cachedSave);
            if (recipe != null)
            {
                if (_catalog.TryFind(recipe.ResultCardId, out var resultDef))
                {
                    _previewTitle.text = $"{_loc.Get("schmiede.fixed_recipe") ?? "Rezept"}: {_loc.Get(resultDef.DisplayNameKey)}";
                }
                else
                {
                    _previewTitle.text = recipe.ResultCardId;
                }
                _previewGoldCost.text = $"{recipe.GoldCost:N0} Gold";
                _previewWarning.text = string.Empty;
                _fuseButton.SetEnabled(_cachedSave.Currencies.Gold >= recipe.GoldCost);
                return;
            }

            // 2. Kategorie-Fusion-Preview
            var preview = _fusion.PreviewCategoryFusion(_selectedInstanceIds, _cachedSave);
            if (!preview.IsSuccess)
            {
                _previewTitle.text = preview.ErrorMessage ?? "Ungueltige Kombination";
                _previewGoldCost.text = string.Empty;
                _previewWarning.text = string.Empty;
                _fuseButton.SetEnabled(false);
                return;
            }

            _previewTitle.text = $"{_loc.Get("schmiede.random_result") ?? "Zufaelliges Ergebnis"}: {preview.ResultPool!.Count} Karten moeglich";
            _previewGoldCost.text = $"{preview.GoldCost:N0} Gold";

            // Letzte-Kopie-Warnung
            var lastCopyWarnings = new List<string>();
            foreach (var instId in _selectedInstanceIds)
            {
                if (_cachedSave.CardInventory.TryGetValue(instId, out var inst) &&
                    _fusion.IsLastCopy(inst.CardDefinitionId, _cachedSave))
                {
                    if (_catalog.TryFind(inst.CardDefinitionId, out var def))
                        lastCopyWarnings.Add(_loc.Get(def.DisplayNameKey));
                }
            }
            _previewWarning.text = lastCopyWarnings.Count > 0
                ? $"⚠ {_loc.Get("schmiede.last_copy_warning") ?? "Letzte Kopie"}: {string.Join(", ", lastCopyWarnings)}"
                : string.Empty;

            _fuseButton.SetEnabled(_cachedSave.Currencies.Gold >= preview.GoldCost);
        }

        // ==========================================================================
        // Fuse-Aktion
        // ==========================================================================

        private void OnFuseClicked() => RunFuseAsync().Forget();

        private async UniTaskVoid RunFuseAsync()
        {
            if (_cachedSave == null) return;
            _fuseButton.SetEnabled(false);

            // Festes Rezept ODER Kategorie?
            var recipe = _fusion.FindMatchingRecipe(_selectedInstanceIds, _cachedSave);
            var resultIds = _selectedInstanceIds.ToList();   // copy

            Result<string> result;
            if (recipe != null)
                result = await _fusion.ApplyFixedRecipeAsync(recipe, resultIds);
            else
                result = await _fusion.ApplyCategoryFusionAsync(resultIds);

            if (!result.IsSuccess)
            {
                _toast.Show(result.ErrorMessage ?? "Fusion fehlgeschlagen", ToastKind.Danger);
                _fuseButton.SetEnabled(true);
                return;
            }

            var resultCardId = result.Value!;
            if (_catalog.TryFind(resultCardId, out var resultDef))
                _toast.Show($"{_loc.Get("schmiede.fuse_success") ?? "Geschmiedet"}: {_loc.Get(resultDef.DisplayNameKey)}", ToastKind.Success);
            else
                _toast.Show($"{_loc.Get("schmiede.fuse_success") ?? "Geschmiedet"}: {resultCardId}", ToastKind.Success);

            // Selection leeren + Save neu laden
            _selectedInstanceIds.Clear();
            var saveR = await _save.LoadAsync();
            if (saveR.IsSuccess && saveR.Value != null) _cachedSave = saveR.Value;
            RefreshInputSlots();
            RebuildInventoryList();
            UpdatePreview();
        }

        private void OnBackClicked() => _screenManager.PopAsync().Forget();
    }
}
