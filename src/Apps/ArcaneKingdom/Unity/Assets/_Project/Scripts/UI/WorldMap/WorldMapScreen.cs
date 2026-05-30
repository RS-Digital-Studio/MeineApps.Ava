#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.World;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.WorldMap
{
    /// <summary>
    /// Welt-Karte mit 9 Welten als horizontale Tabs und 10 Nodes pro Welt als Grid.
    /// Klick auf Node zeigt Detail-Panel rechts mit Energie-Kosten, Belohnungen
    /// und Start-Button.
    /// </summary>
    public sealed class WorldMapScreen : ScreenBase
    {
        public const string NodeContextKey = "battle_node";
        public const string DifficultyContextKey = "battle_difficulty";

        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly WorldCatalogService _worldCatalog;
        private readonly ToastService _toast;
        private readonly ModalContext _modalContext;

        // Header
        private Button _backBtn = null!;
        private Label _totalStarsLabel = null!;

        // Tabs
        private VisualElement _tabsContainer = null!;

        // Body
        private Label _currentWorldName = null!;
        private Label _currentWorldMeta = null!;
        private VisualElement _nodesGrid = null!;

        // Detail
        private Label _detailEmpty = null!;
        private VisualElement _detailContent = null!;
        private Label _detailName = null!;
        private Label _detailType = null!;
        private Label _detailEnergy = null!;
        private Label _detailStars = null!;
        private VisualElement _detailRewards = null!;
        private Button _detailStartBtn = null!;

        private PlayerSave? _saveCached;
        private WorldDefinition? _activeWorld;
        private NodeDefinition? _selectedNode;

        public override string Id => ScreenId.WorldMap;
        protected override string UxmlPath => "UI/WorldMapScreen";

        // v6 (Designplan v4): Prestige-Upgrade-Button pro Welt
        private readonly ArcaneKingdom.Domain.World.PrestigeService _prestige;
        private readonly ArcaneKingdom.UI.Modals.PrestigeUpgradeContext _prestigeCtx;
        private readonly ArcaneKingdom.UI.Modals.DifficultyPickerContext _difficultyCtx;

        private readonly UIAssetService _uiAssets;

        public WorldMapScreen(ScreenManager screenManager,
                              ISaveService<PlayerSave> save,
                              WorldCatalogService worldCatalog,
                              ToastService toast,
                              ModalContext modalContext,
                              ArcaneKingdom.Domain.World.PrestigeService prestige,
                              ArcaneKingdom.UI.Modals.PrestigeUpgradeContext prestigeCtx,
                              ArcaneKingdom.UI.Modals.DifficultyPickerContext difficultyCtx,
                              UIAssetService uiAssets)
        {
            _screenManager = screenManager;
            _save = save;
            _worldCatalog = worldCatalog;
            _toast = toast;
            _modalContext = modalContext;
            _prestige = prestige;
            _prestigeCtx = prestigeCtx;
            _difficultyCtx = difficultyCtx;
            _uiAssets = uiAssets;
        }

        protected override void BindElements(VisualElement root)
        {
            _backBtn = Q<Button>("world-back-button");
            _totalStarsLabel = Q<Label>("world-total-stars");
            _tabsContainer = Q<VisualElement>("world-tabs-container");
            _currentWorldName = Q<Label>("current-world-name");
            _currentWorldMeta = Q<Label>("current-world-meta");
            _nodesGrid = Q<VisualElement>("world-nodes-grid");

            _detailEmpty = Q<Label>("node-detail-empty");
            _detailContent = Q<VisualElement>("node-detail-content");
            _detailName = Q<Label>("node-detail-name");
            _detailType = Q<Label>("node-detail-type");
            _detailEnergy = Q<Label>("node-detail-energy");
            _detailStars = Q<Label>("node-detail-stars");
            _detailRewards = Q<VisualElement>("node-detail-rewards");
            _detailStartBtn = Q<Button>("node-detail-start");

            _backBtn.clicked += () => _screenManager.PopAsync().Forget();
            _detailStartBtn.clicked += OnStartBattle;
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var saveResult = await _save.LoadAsync(ct);
            if (!saveResult.IsSuccess) { _toast.Show("Save-Load fehlgeschlagen.", ToastKind.Danger); return; }
            _saveCached = saveResult.Value;

            BuildWorldTabs();

            // Initial: erste Welt (oder gespeicherte letzte)
            if (_activeWorld == null && _worldCatalog.AllWorlds.Count > 0)
                SetActiveWorld(_worldCatalog.AllWorlds[0]);

            RefreshTotalStars();
        }

        // ============================================================
        // Welt-Tabs
        // ============================================================

        private void BuildWorldTabs()
        {
            _tabsContainer.Clear();
            foreach (var world in _worldCatalog.AllWorlds)
            {
                var tab = BuildWorldTab(world);
                _tabsContainer.Add(tab);
            }
        }

        private VisualElement BuildWorldTab(WorldDefinition world)
        {
            var tab = new VisualElement { name = $"world-tab-{world.Id}" };
            tab.style.width = 100;
            tab.style.height = 60;
            tab.style.marginRight = 8;
            tab.style.alignItems = Align.Center;
            tab.style.justifyContent = Justify.Center;
            tab.style.borderTopLeftRadius = 8;
            tab.style.borderTopRightRadius = 8;
            tab.style.borderBottomLeftRadius = 8;
            tab.style.borderBottomRightRadius = 8;
            tab.style.backgroundColor = ElementBg(world.ThemeElement);

            var idx = new Label($"W{world.Index}");
            idx.style.fontSize = 22;
            idx.style.unityFontStyleAndWeight = FontStyle.Bold;
            idx.style.color = new StyleColor(new Color(0.95f, 0.95f, 1f));
            tab.Add(idx);

            var lvl = new Label($"Lv {world.RecommendedPlayerLevel}+");
            lvl.style.fontSize = 11;
            lvl.style.color = new StyleColor(new Color(0.9f, 0.9f, 1f, 0.7f));
            tab.Add(lvl);

            tab.AddManipulator(new Clickable(() => SetActiveWorld(world)));
            return tab;
        }

        private void SetActiveWorld(WorldDefinition world)
        {
            _activeWorld = world;
            _selectedNode = null;
            _currentWorldName.text = NicifyId(world.Id);

            // Welt-Background pro aktiver Welt (z.B. Worlds/elderwald.png, Worlds/vulkanhort.png)
            _uiAssets.ApplyWorldBackground(Root, world.Id);

            // Aktuelle Prestige-Stufe der Welt
            var stufe = _saveCached?.Prestige?.Get(world.Id) ?? PrestigeStufe.Normal;
            var stufeText = stufe == PrestigeStufe.Normal ? string.Empty : $"  ★ Prestige {stufe}";
            _currentWorldMeta.text =
                $"Element: {world.ThemeElement}  •  Empfohlene Stufe: {world.RecommendedPlayerLevel}{stufeText}";

            BuildNodesGrid();
            HideDetail();
            UpdateActiveTabHighlight();
            UpdatePrestigeUpgradeButton(world, stufe);
        }

        /// <summary>
        /// v6 (Designplan v4 Oeko Kap. 6): Zeigt einen "Prestige aufwerten"-Button wenn alle Nodes 3 Sterne haben.
        /// Button wird dynamisch unter dem Welt-Meta-Label angefuegt.
        /// </summary>
        private void UpdatePrestigeUpgradeButton(WorldDefinition world, PrestigeStufe currentStufe)
        {
            // Vorhandenen Button entfernen wenn vorher aktiviert
            var existing = _currentWorldMeta.parent?.Q<Button>("prestige-upgrade-button");
            existing?.RemoveFromHierarchy();

            if (_saveCached == null || currentStufe == PrestigeStufe.IV) return;
            if (!_saveCached.WorldProgress.TryGetValue(world.Id, out var progress)) return;
            if (progress.StarsByNodeId.Count == 0) return;

            // Alle Nodes 3 Sterne?
            var allThreeStars = progress.StarsByNodeId.Count >= world.Nodes.Count
                                && progress.StarsByNodeId.Values.All(s => s >= 3);
            if (!allThreeStars) return;

            var cost = PrestigeStufeBalancing.GetUpgradeGoldCost(currentStufe);
            var nextStufe = currentStufe switch
            {
                PrestigeStufe.Normal => PrestigeStufe.I,
                PrestigeStufe.I      => PrestigeStufe.II,
                PrestigeStufe.II     => PrestigeStufe.III,
                _                    => PrestigeStufe.IV
            };

            var btn = new Button(() => OpenPrestigeUpgrade(world.Id))
            {
                name = "prestige-upgrade-button",
                text = $"Aufwerten zu Prestige {nextStufe} ({cost:N0} Gold)"
            };
            btn.AddToClassList("ak-btn");
            btn.AddToClassList("ak-btn--accent");
            btn.style.marginTop = 8;
            _currentWorldMeta.parent?.Add(btn);
        }

        private void OpenPrestigeUpgrade(string worldId)
        {
            _prestigeCtx.TargetWorldId = worldId;
            _screenManager.PushAsync(ScreenId.PrestigeUpgradeOverlay).Forget();
        }

        private void UpdateActiveTabHighlight()
        {
            foreach (var tab in _tabsContainer.Children())
            {
                var active = tab.name == $"world-tab-{_activeWorld?.Id}";
                tab.style.borderTopWidth = active ? 3 : 0;
                tab.style.borderBottomWidth = active ? 3 : 0;
                tab.style.borderLeftWidth = active ? 3 : 0;
                tab.style.borderRightWidth = active ? 3 : 0;
                tab.style.borderTopColor = active
                    ? new StyleColor(new Color(0.95f, 0.78f, 0.30f))
                    : new StyleColor(Color.clear);
                tab.style.borderBottomColor = tab.style.borderTopColor;
                tab.style.borderLeftColor = tab.style.borderTopColor;
                tab.style.borderRightColor = tab.style.borderTopColor;
            }
        }

        // ============================================================
        // Nodes-Grid
        // ============================================================

        private void BuildNodesGrid()
        {
            _nodesGrid.Clear();
            if (_activeWorld == null || _saveCached == null) return;

            var progress = GetOrCreateProgress(_activeWorld.Id);

            foreach (var node in _activeWorld.Nodes)
            {
                var stars = progress.StarsByNodeId.TryGetValue(node.Id, out var s) ? s : 0;
                var prevUnlocked = node.NodeIndex == 1
                    || IsNodeUnlocked(_activeWorld, node.NodeIndex - 1, progress);

                _nodesGrid.Add(BuildNodeTile(node, stars, prevUnlocked));
            }
        }

        private bool IsNodeUnlocked(WorldDefinition world, int previousIndex, WorldProgress progress)
        {
            var prev = world.Nodes.FirstOrDefault(n => n.NodeIndex == previousIndex);
            if (prev == null) return true;
            return progress.StarsByNodeId.TryGetValue(prev.Id, out var s) && s >= 1;
        }

        private VisualElement BuildNodeTile(NodeDefinition node, int stars, bool unlocked)
        {
            var tile = new VisualElement { name = $"node-{node.Id}" };
            tile.style.width = 96;
            tile.style.height = 110;
            tile.style.marginTop = 8;
            tile.style.marginBottom = 8;
            tile.style.marginLeft = 8;
            tile.style.marginRight = 8;
            tile.style.borderTopLeftRadius = 12;
            tile.style.borderTopRightRadius = 12;
            tile.style.borderBottomLeftRadius = 12;
            tile.style.borderBottomRightRadius = 12;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;
            tile.style.backgroundColor = NodeBg(node.Type);
            if (!unlocked) tile.style.opacity = 0.35f;

            // Node-Marker-Sprite oben drueber (normal/miniboss/worldboss)
            var marker = new VisualElement();
            marker.style.width = 56;
            marker.style.height = 56;
            marker.style.marginBottom = 4;
            var nodeTypeStr = node.Type switch
            {
                NodeType.MiniBoss => "miniboss",
                NodeType.WorldBoss => "worldboss",
                _ => "normal"
            };
            _uiAssets.ApplyNodeMarker(marker, nodeTypeStr);
            tile.Add(marker);

            var indexLbl = new Label(node.NodeIndex.ToString());
            indexLbl.style.fontSize = 22;
            indexLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            indexLbl.style.color = new StyleColor(Color.white);
            tile.Add(indexLbl);

            var starsRow = new Label(BuildStarsString(stars));
            starsRow.style.fontSize = 14;
            starsRow.style.color = new StyleColor(new Color(0.95f, 0.78f, 0.30f));
            tile.Add(starsRow);

            if (unlocked)
                tile.AddManipulator(new Clickable(() => OnNodeClicked(node)));

            return tile;
        }

        private static string BuildStarsString(int stars)
        {
            const int max = 4;
            stars = System.Math.Clamp(stars, 0, max);
            return new string('★', stars) + new string('☆', max - stars);
        }

        // ============================================================
        // Detail-Panel
        // ============================================================

        private void OnNodeClicked(NodeDefinition node)
        {
            _selectedNode = node;
            ShowDetail();
        }

        private void ShowDetail()
        {
            if (_selectedNode == null || _saveCached == null) return;

            _detailEmpty.AddToClassList("ak-hidden");
            _detailContent.RemoveFromClassList("ak-hidden");

            _detailName.text = NicifyId(_selectedNode.Id);
            _detailType.text = _selectedNode.Type switch
            {
                NodeType.MiniBoss  => "Mini-Boss",
                NodeType.WorldBoss => "Welt-Boss",
                _                  => "Normal-Kampf"
            };
            _detailEnergy.text = _selectedNode.EnergyCost.ToString();

            var progress = GetOrCreateProgress(_activeWorld!.Id);
            var stars = progress.StarsByNodeId.TryGetValue(_selectedNode.Id, out var s) ? s : 0;
            _detailStars.text = BuildStarsString(stars);

            BuildRewardsPanel(_selectedNode);

            // Energie-Check für Start-Button
            var enoughEnergy = _saveCached.Currencies.TotalEnergy >= _selectedNode.EnergyCost;
            _detailStartBtn.SetEnabled(enoughEnergy);
            _detailStartBtn.text = enoughEnergy ? "Kampf starten" : "Nicht genug Energie";
        }

        private void BuildRewardsPanel(NodeDefinition node)
        {
            _detailRewards.Clear();
            for (var stars = 1; stars <= 4; stars++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.marginBottom = 2;

                var starsLbl = new Label(BuildStarsString(stars));
                starsLbl.style.color = new StyleColor(new Color(0.95f, 0.78f, 0.30f));
                row.Add(starsLbl);

                var rewards = new Label($"{node.GoldReward(stars)} Gold • {node.ExpReward(stars)} EXP");
                rewards.AddToClassList("ak-caption");
                row.Add(rewards);

                _detailRewards.Add(row);
            }
        }

        private void HideDetail()
        {
            _detailEmpty.RemoveFromClassList("ak-hidden");
            _detailContent.AddToClassList("ak-hidden");
        }

        private void OnStartBattle()
        {
            if (_selectedNode == null) return;
            if (!_screenManager.IsRegistered(ScreenId.Battle))
            {
                _toast.Show("Battle-Screen nicht verfuegbar.", ToastKind.Warning);
                return;
            }

            // Spielplan v5 Kap. 8.3: Vor dem Kampf Difficulty-Auswahl (Classic/Amateur/Profi/Gott).
            // Wenn das Difficulty-Picker-Overlay registriert ist, gehen wir den vollen Flow,
            // sonst direkt mit Classic-Difficulty starten (Fallback fuer alte Builds).
            if (_screenManager.IsRegistered(ScreenId.DifficultyPickerOverlay))
            {
                var progress = GetOrCreateProgress(_activeWorld!.Id);
                var bestStars = progress.StarsByNodeId.TryGetValue(_selectedNode.Id, out var s) ? s : 0;

                _difficultyCtx.Node = _selectedNode;
                _difficultyCtx.WorldId = _activeWorld!.Id;
                _difficultyCtx.AvailableEnergy = _saveCached?.Currencies.TotalEnergy ?? 0;
                _difficultyCtx.BestStarsSoFar = bestStars;
                _difficultyCtx.OnDifficultySelected = difficulty => StartBattleWithDifficulty(_selectedNode, difficulty);

                _screenManager.PushAsync(ScreenId.DifficultyPickerOverlay).Forget();
            }
            else
            {
                StartBattleWithDifficulty(_selectedNode, ArcaneKingdom.Domain.World.NodeDifficulty.Classic);
            }
        }

        private void StartBattleWithDifficulty(NodeDefinition node, ArcaneKingdom.Domain.World.NodeDifficulty difficulty)
        {
            _modalContext.Set(NodeContextKey, node);
            _modalContext.Set(DifficultyContextKey, difficulty);
            _screenManager.PushAsync(ScreenId.Battle).Forget();
        }

        // ============================================================
        // Helpers
        // ============================================================

        private WorldProgress GetOrCreateProgress(string worldId)
        {
            if (_saveCached!.WorldProgress.TryGetValue(worldId, out var p)) return p;
            var fresh = new WorldProgress(worldId);
            _saveCached.WorldProgress[worldId] = fresh;
            return fresh;
        }

        private void RefreshTotalStars()
        {
            if (_saveCached == null) return;
            var totalStars = _saveCached.WorldProgress.Values.Sum(p => p.TotalStars);
            var maxStars = _worldCatalog.AllWorlds.Sum(w => w.Nodes.Count * 4);
            _totalStarsLabel.text = $"{totalStars} / {maxStars} ★";
        }

        private static StyleColor ElementBg(ArcaneKingdom.Domain.Cards.Element e) => e switch
        {
            ArcaneKingdom.Domain.Cards.Element.Feuer  => new StyleColor(new Color(0.55f, 0.20f, 0.10f)),
            ArcaneKingdom.Domain.Cards.Element.Wasser => new StyleColor(new Color(0.15f, 0.30f, 0.55f)),
            ArcaneKingdom.Domain.Cards.Element.Licht  => new StyleColor(new Color(0.55f, 0.45f, 0.20f)),
            ArcaneKingdom.Domain.Cards.Element.Dunkel => new StyleColor(new Color(0.32f, 0.15f, 0.40f)),
            _ => new StyleColor(new Color(0.20f, 0.42f, 0.20f))
        };

        private static StyleColor NodeBg(NodeType t) => t switch
        {
            NodeType.MiniBoss  => new StyleColor(new Color(0.55f, 0.30f, 0.10f)),
            NodeType.WorldBoss => new StyleColor(new Color(0.70f, 0.15f, 0.20f)),
            _                  => new StyleColor(new Color(0.30f, 0.30f, 0.55f))
        };

        private static string NicifyId(string id)
        {
            var idx = id.IndexOf('_');
            if (idx < 0 || idx >= id.Length - 1) return id;
            var raw = id.Substring(idx + 1).Replace('_', ' ');
            return raw.Length == 0 ? id : char.ToUpper(raw[0]) + raw.Substring(1);
        }
    }
}
