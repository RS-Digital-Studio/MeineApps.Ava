#nullable enable
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Runes;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Runes
{
    /// <summary>
    /// Runen-Verwaltungs-Screen (Spielplan v5 Kap. 7 + Impl_KOMPLETT Kap. 3).
    /// 4 Slots oben (gesperrt/offen je Spieler-Level), Grid mit Filter unten,
    /// Aktive-Runen-Stats-Zusammenfassung.
    /// </summary>
    public sealed class RuneScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ILocalizationService _loc;
        private readonly ToastService _toast;

        private VisualElement _slotsRow = null!;
        private VisualElement _runeGrid = null!;
        private Label _activeStats = null!;
        private Button _closeBtn = null!;
        private VisualElement _filterRow = null!;
        private RuneType? _activeFilter;
        private PlayerSave? _saveCache;

        public override string Id => ScreenId.Runes;
        protected override string UxmlPath => "UI/RuneScreen";

        private readonly UIAssetService _uiAssets;

        public RuneScreen(ScreenManager screenManager,
                           ISaveService<PlayerSave> save,
                           ILocalizationService loc,
                           ToastService toast,
                           UIAssetService uiAssets)
        {
            _screenManager = screenManager;
            _save = save;
            _loc = loc;
            _toast = toast;
            _uiAssets = uiAssets;
        }

        protected override void BindElements(VisualElement root)
        {
            _slotsRow    = Q<VisualElement>("rune-slots-row");
            _runeGrid    = Q<VisualElement>("rune-grid");
            _activeStats = Q<Label>("rune-active-stats");
            _closeBtn    = Q<Button>("rune-close");
            _filterRow   = Q<VisualElement>("rune-filter-row");

            _closeBtn.clicked += () => _screenManager.PopAsync().Forget();
            BuildFilterButtons();
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var result = await _save.LoadAsync(ct);
            if (!result.IsSuccess || result.Value == null) return;
            _saveCache = result.Value;
            Refresh();
        }

        private void BuildFilterButtons()
        {
            _filterRow.Clear();
            AddFilter(null, "Alle");
            foreach (RuneType t in System.Enum.GetValues(typeof(RuneType)))
                AddFilter(t, t.ToString());
        }

        private void AddFilter(RuneType? type, string label)
        {
            var btn = new Button(() => { _activeFilter = type; Refresh(); }) { text = label };
            btn.style.marginRight = 8;
            btn.style.height = 36;
            btn.style.paddingLeft = 12;
            btn.style.paddingRight = 12;
            _filterRow.Add(btn);
        }

        private void Refresh()
        {
            if (_saveCache == null) return;
            BuildSlots(_saveCache);
            BuildGrid(_saveCache);
            BuildActiveStats(_saveCache);
        }

        private void BuildSlots(PlayerSave save)
        {
            _slotsRow.Clear();
            var playerLevel = save.Profile.Level;
            var activeDeck = save.Decks.FirstOrDefault(d => d.SlotIndex == save.ActiveDeckSlot);

            for (var s = 1; s <= RuneSlotUnlock.MaxSlots; s++)
            {
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

                var icon = new Label(unlocked
                    ? $"Slot {s}"
                    : $"{_loc.Get("rune.slot_locked", "Gesperrt")}\nLV {RuneSlotUnlock.MinLevelForSlot(s)}");
                icon.style.color = unlocked
                    ? new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f))
                    : new StyleColor(new UnityEngine.Color(0.55f, 0.55f, 0.65f));
                icon.style.fontSize = 12;
                icon.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                icon.style.whiteSpace = WhiteSpace.Normal;
                slot.Add(icon);

                _slotsRow.Add(slot);
            }
        }

        private void BuildGrid(PlayerSave save)
        {
            _runeGrid.Clear();
            if (save.RuneInventory.Count == 0)
            {
                _runeGrid.Add(new Label("Noch keine Runen gesammelt — kaempfe Welt-Bosse fuer Runen-Drops."));
                return;
            }

            // Pro Rune-Instance: Sprite-Icon + Definition-ID anzeigen
            foreach (var (instId, runeInst) in save.RuneInventory)
            {
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

                // Rune-Sprite (64x64)
                var icon = new VisualElement();
                icon.style.width = 64; icon.style.height = 64;
                icon.style.marginBottom = 4;
                _uiAssets.ApplyBackground(icon, $"Runes/{runeInst.RuneDefinitionId}",
                                          UnityEngine.ScaleMode.ScaleToFit);
                tile.Add(icon);

                var name = new Label(runeInst.RuneDefinitionId);
                name.style.fontSize = 11;
                name.style.color = new StyleColor(UnityEngine.Color.white);
                name.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                name.style.whiteSpace = WhiteSpace.Normal;
                tile.Add(name);

                _runeGrid.Add(tile);
            }
        }

        private void BuildActiveStats(PlayerSave save)
        {
            var unlocked = RuneSlotUnlock.UnlockedSlotCount(save.Profile.Level);
            _activeStats.text = $"{unlocked}/{RuneSlotUnlock.MaxSlots} Slots offen — Spieler-LV {save.Profile.Level}";
        }
    }
}
