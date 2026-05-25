#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.World;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.Game.Battle;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.UI.Common;
using ArcaneKingdom.UI.Foundation;
using ArcaneKingdom.UI.WorldMap;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Battle
{
    /// <summary>
    /// Battle-Screen mit echter <see cref="BattleEngine"/>-Integration. Setup kommt
    /// aus PlayerSave (aktives Deck) + Node (Enemy-Deck).
    /// </summary>
    public sealed class BattleScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly CardCatalogService _cardCatalog;
        private readonly BattleBootstrap _battleBootstrap;
        private readonly ModalContext _modalContext;
        private readonly ToastService _toast;
        private readonly CardArtworkService _artworkService;

        // Top
        private Button _fleeBtn = null!;
        private Label _turnNumber = null!;
        private Label _phaseLabel = null!;

        // Enemy
        private VisualElement _enemyHpFill = null!;
        private Label _enemyHpText = null!;
        private Label _enemyManaText = null!;
        private VisualElement _enemyField = null!;

        // Player
        private VisualElement _playerHpFill = null!;
        private Label _playerHpText = null!;
        private VisualElement _playerManaOrbs = null!;
        private VisualElement _playerField = null!;
        private VisualElement _playerHand = null!;
        private Button _endTurnBtn = null!;

        private VisualElement _floatingLayer = null!;

        // State (echt)
        private BattleEngine? _engine;
        private BattleAI? _ai;
        private Dictionary<string, CardDefinition>? _defs;
        private Dictionary<string, CardInstance>? _instances;
        private bool _busy;
        private int _playerStartHp;
        private int _enemyStartHp;
        private NodeDefinition? _node;
        private NodeDifficulty _difficulty = NodeDifficulty.Classic;

        // v6: Anzahl bereits angezeigter Events (damit jeder Event nur 1x als Toast erscheint)
        private int _eventsShownCount;

        public override string Id => ScreenId.Battle;
        protected override string UxmlPath => "UI/BattleScreen";

        private readonly ILocalizationService _loc;
        private readonly WorldCatalogService _worldCatalog;
        private readonly ArcaneKingdom.UI.Modals.MemoryFragmentContext _memoryCtx;
        private readonly ArcaneKingdom.UI.BattleReport.BattleReportContext _reportCtx;

        private readonly UIAssetService _uiAssets;

        public BattleScreen(ScreenManager screenManager,
                            ISaveService<PlayerSave> save,
                            CardCatalogService cardCatalog,
                            BattleBootstrap battleBootstrap,
                            ModalContext modalContext,
                            ToastService toast,
                            CardArtworkService artworkService,
                            ILocalizationService loc,
                            WorldCatalogService worldCatalog,
                            ArcaneKingdom.UI.Modals.MemoryFragmentContext memoryCtx,
                            ArcaneKingdom.UI.BattleReport.BattleReportContext reportCtx,
                            UIAssetService uiAssets)
        {
            _screenManager = screenManager;
            _save = save;
            _cardCatalog = cardCatalog;
            _battleBootstrap = battleBootstrap;
            _modalContext = modalContext;
            _toast = toast;
            _artworkService = artworkService;
            _loc = loc;
            _worldCatalog = worldCatalog;
            _memoryCtx = memoryCtx;
            _reportCtx = reportCtx;
            _uiAssets = uiAssets;
        }

        protected override void BindElements(VisualElement root)
        {
            _fleeBtn       = Q<Button>("battle-flee-button");
            _turnNumber    = Q<Label>("battle-turn-number");
            _phaseLabel    = Q<Label>("battle-phase-label");
            _enemyHpFill   = Q<VisualElement>("enemy-hp-fill");
            _playerHpFill  = Q<VisualElement>("player-hp-fill");
            _enemyHpText   = Q<Label>("enemy-hp-text");
            _enemyManaText = Q<Label>("enemy-mana-text");
            _enemyField    = Q<VisualElement>("enemy-field");

            _playerHpText  = Q<Label>("player-hp-text");
            _playerManaOrbs = Q<VisualElement>("player-mana-orbs");
            _playerField   = Q<VisualElement>("player-field");
            _playerHand    = Q<VisualElement>("player-hand");
            _endTurnBtn    = Q<Button>("battle-end-turn-button");
            _floatingLayer = Q<VisualElement>("battle-floating-layer");

            _fleeBtn.clicked += OnFlee;
            _endTurnBtn.clicked += () => OnEndTurnAsync().Forget();
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            _node = _modalContext.Get<NodeDefinition>(WorldMapScreen.NodeContextKey);

            // Spielplan v5 Kap. 8.3: Difficulty aus dem Context (gewaehlt im DifficultyPicker).
            // Fallback: Classic (1 Energie, 1 Stern bei Sieg).
            _difficulty = _modalContext.Get<NodeDifficulty?>(WorldMapScreen.DifficultyContextKey)
                          ?? NodeDifficulty.Classic;

            // Battle-Background pro Welt (z.B. battle_bg_elderwald, battle_bg_vulkanhort)
            if (_node != null)
            {
                var world = _worldCatalog.AllWorlds.FirstOrDefault(w => w.Nodes.Any(n => n.Id == _node.Id));
                if (world != null)
                    _uiAssets.ApplyBattleBackground(Root, world.Id);
            }

            var saveResult = await _save.LoadAsync(ct);
            if (!saveResult.IsSuccess || saveResult.Value == null)
            {
                _toast.Show("Save-Load fehlgeschlagen.", ToastKind.Danger);
                _screenManager.PopAsync().Forget();
                return;
            }

            var save = saveResult.Value;

            // Energie abziehen — skaliert mit Difficulty (Classic/Amateur=1, Profi=2, Gott=3)
            var energyCost = _node != null ? _node.EnergyCostFor(_difficulty) : 0;
            if (_node != null && !save.Currencies.SpendEnergy(energyCost))
            {
                _toast.Show($"Nicht genug Energie ({energyCost} benoetigt).", ToastKind.Warning);
                _screenManager.PopAsync().Forget();
                return;
            }
            if (_node != null) await _save.SaveAsync(save, ct);

            // Engine + AI bauen — Difficulty wird an BattleBootstrap weitergegeben
            var setup = _battleBootstrap.Build(save, _node, seed: System.Environment.TickCount, difficulty: _difficulty);
            if (setup == null)
            {
                _toast.Show("Kein Deck fuer Battle — bitte zuerst im DeckBuilder befuellen.", ToastKind.Danger);
                _screenManager.PopAsync().Forget();
                return;
            }
            _engine = setup.Engine;
            _ai = setup.Ai;
            _defs = setup.Definitions;
            _instances = setup.Instances;
            _playerStartHp = _engine.State.PlayerHeroHp;
            _enemyStartHp = _engine.State.EnemyHeroHp;

            RefreshAll();
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            _modalContext.Remove(WorldMapScreen.NodeContextKey);
            return UniTask.CompletedTask;
        }

        // ============================================================
        // Render
        // ============================================================

        private void RefreshAll()
        {
            if (_engine == null) return;
            RenderHud();
            RenderEnemyField();
            RenderPlayerField();
            RenderPlayerHand();
            RenderManaOrbs();
        }

        private void RenderHud()
        {
            var s = _engine!.State;
            _turnNumber.text = s.CurrentTurn.ToString();
            _phaseLabel.text = s.Phase switch
            {
                BattlePhase.PlayerTurn => "Dein Zug",
                BattlePhase.EnemyTurn  => "Gegner-Zug",
                BattlePhase.Settlement => "Ende",
                _                      => s.Phase.ToString()
            };

            UpdateHpBar(_playerHpFill, _playerHpText, s.PlayerHeroHp, _playerStartHp);
            UpdateHpBar(_enemyHpFill,  _enemyHpText,  s.EnemyHeroHp,  _enemyStartHp);
            _enemyManaText.text = $"{s.EnemyMana}/{s.EnemyMaxMana}";
        }

        private static void UpdateHpBar(VisualElement fill, Label text, int hp, int maxHp)
        {
            var pct = maxHp > 0 ? (float)hp * 100f / maxHp : 0f;
            fill.style.width = new Length(System.Math.Clamp(pct, 0f, 100f), LengthUnit.Percent);
            text.text = $"{hp} / {maxHp} HP";
        }

        private void RenderEnemyField()
        {
            _enemyField.Clear();
            foreach (var slot in _engine!.State.EnemyField)
                _enemyField.Add(BuildFieldSlotTile(slot, isEnemy: true));
        }

        private void RenderPlayerField()
        {
            _playerField.Clear();
            foreach (var slot in _engine!.State.PlayerField)
                _playerField.Add(BuildFieldSlotTile(slot, isEnemy: false));
        }

        private VisualElement BuildFieldSlotTile(CardFieldSlot slot, bool isEnemy)
        {
            var def = ResolveDefinition(slot.CardInstanceId);
            var tile = new VisualElement();
            tile.style.width = 80;
            tile.style.height = 96;
            tile.style.marginLeft = 4;
            tile.style.marginRight = 4;
            tile.style.borderTopLeftRadius = 8;
            tile.style.borderTopRightRadius = 8;
            tile.style.borderBottomLeftRadius = 8;
            tile.style.borderBottomRightRadius = 8;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;
            tile.style.backgroundColor = isEnemy
                ? new StyleColor(new Color(0.45f, 0.10f, 0.10f))
                : new StyleColor(new Color(0.10f, 0.20f, 0.45f));
            tile.style.borderTopWidth = 2;
            tile.style.borderBottomWidth = 2;
            tile.style.borderLeftWidth = 2;
            tile.style.borderRightWidth = 2;
            tile.style.borderTopColor = new StyleColor(new Color(0.95f, 0.78f, 0.30f, 0.4f));
            tile.style.borderBottomColor = tile.style.borderTopColor;
            tile.style.borderLeftColor = tile.style.borderTopColor;
            tile.style.borderRightColor = tile.style.borderTopColor;

            var nameLbl = new Label(def?.Id ?? slot.CardInstanceId);
            nameLbl.style.fontSize = 11;
            nameLbl.style.color = new StyleColor(Color.white);
            nameLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            tile.Add(nameLbl);

            var stats = new Label($"{slot.CurrentAttack} / {slot.CurrentHealth}");
            stats.style.fontSize = 14;
            stats.style.unityFontStyleAndWeight = FontStyle.Bold;
            stats.style.color = new StyleColor(new Color(0.95f, 0.78f, 0.30f));
            tile.Add(stats);

            // Cooldown-Anzeige (Spezial-Timer)
            if (slot.TurnsUntilSpecial > 0)
            {
                var cd = new Label($"⚡{slot.TurnsUntilSpecial}");
                cd.style.fontSize = 10;
                cd.style.color = new StyleColor(new Color(0.55f, 0.80f, 0.95f));
                tile.Add(cd);
            }

            // v6 (Designplan v4 Kap. 3.4): Status-Effekt-Icons unten links
            if (slot.StatusEffects.Count > 0)
            {
                var effectsRow = new VisualElement();
                effectsRow.AddToClassList("ak-status-effect-row");
                foreach (var fx in slot.StatusEffects)
                {
                    var icon = new Label(GetStatusEffectGlyph(fx.Type));
                    icon.AddToClassList("ak-status-effect-icon");
                    icon.AddToClassList($"ak-status-effect-icon--{fx.Type.ToString().ToLower()}");
                    icon.tooltip = $"{fx.Type} ({fx.RemainingTurns} Runden)";
                    effectsRow.Add(icon);
                }
                tile.Add(effectsRow);
            }
            return tile;
        }

        /// <summary>
        /// Liefert einen kurzen Glyph fuer den Status-Effekt (Emoji/Unicode-Symbol).
        /// </summary>
        private static string GetStatusEffectGlyph(StatusEffectType type) => type switch
        {
            StatusEffectType.Sleep    => "💤",
            StatusEffectType.Silence  => "🤐",
            StatusEffectType.Frozen   => "❄",
            StatusEffectType.Stunned  => "💫",
            StatusEffectType.Poisoned => "☠",
            StatusEffectType.Burning  => "🔥",
            StatusEffectType.Slowed   => "⏳",
            StatusEffectType.Rooted   => "🌿",
            _                          => "•"
        };

        private void RenderPlayerHand()
        {
            _playerHand.Clear();
            foreach (var cardInstId in _engine!.State.PlayerHand)
            {
                var def = ResolveDefinition(cardInstId);
                if (def == null) continue;
                var tile = BuildHandCardTile(cardInstId, def);
                _playerHand.Add(tile);
            }
        }

        private VisualElement BuildHandCardTile(string cardInstId, CardDefinition def)
        {
            var tile = new VisualElement();
            tile.AddToClassList("ak-card");
            tile.AddToClassList(RarityClass(def.Rarity));
            tile.style.width = 130;
            tile.style.height = 180;

            var costBadge = new VisualElement();
            costBadge.AddToClassList("ak-card__cost-badge");
            var costLabel = new Label(def.Cost.ToString());
            costLabel.AddToClassList("ak-card__cost-value");
            costBadge.Add(costLabel);
            tile.Add(costBadge);

            var art = new VisualElement();
            art.AddToClassList("ak-card__art");
            tile.Add(art);
            LoadArtAsync(art, def).Forget();

            var name = new Label(def.Id);
            name.AddToClassList("ak-card__name");
            tile.Add(name);

            var stats = new VisualElement();
            stats.AddToClassList("ak-card__stats");
            var atk = new Label(def.BaseAttack.ToString("N0"));
            atk.AddToClassList("ak-card__stat");
            atk.AddToClassList("ak-card__stat--atk");
            var hp = new Label(def.BaseHealth.ToString("N0"));
            hp.AddToClassList("ak-card__stat");
            hp.AddToClassList("ak-card__stat--hp");
            stats.Add(atk);
            stats.Add(hp);
            tile.Add(stats);

            var s = _engine!.State;
            var canPlay = s.Phase == BattlePhase.PlayerTurn
                          && s.PlayerMana >= def.Cost
                          && s.PlayerField.Count < BattleEngine.MaxFieldSlots;
            if (!canPlay)
            {
                tile.style.opacity = 0.5f;
            }
            else
            {
                var drag = new CardDragManipulator(
                    dropZone: _playerField,
                    onDrop: () => OnPlayCard(cardInstId),
                    floatingLayer: _floatingLayer,
                    canDrag: () =>
                    {
                        var st = _engine!.State;
                        return st.Phase == BattlePhase.PlayerTurn
                               && st.PlayerMana >= def.Cost
                               && st.PlayerField.Count < BattleEngine.MaxFieldSlots;
                    });
                tile.AddManipulator(drag);
                tile.AddManipulator(new Clickable(() => OnPlayCard(cardInstId)));
            }
            return tile;
        }

        private void RenderManaOrbs()
        {
            _playerManaOrbs.Clear();
            var s = _engine!.State;
            for (var i = 0; i < s.PlayerMaxMana; i++)
            {
                var orb = new VisualElement();
                orb.style.width = 14;
                orb.style.height = 14;
                orb.style.borderTopLeftRadius = 7;
                orb.style.borderTopRightRadius = 7;
                orb.style.borderBottomLeftRadius = 7;
                orb.style.borderBottomRightRadius = 7;
                orb.style.marginRight = 4;
                orb.style.backgroundColor = i < s.PlayerMana
                    ? new StyleColor(new Color(0.30f, 0.65f, 0.95f))
                    : new StyleColor(new Color(0.30f, 0.30f, 0.40f));
                _playerManaOrbs.Add(orb);
            }
        }

        // ============================================================
        // Player-Actions
        // ============================================================

        private void OnPlayCard(string cardInstanceId)
        {
            if (_busy || _engine == null) return;
            if (_engine.State.Phase != BattlePhase.PlayerTurn) return;

            if (!_engine.PlayCard(forPlayer: true, cardInstanceId))
            {
                _toast.Show("Karte kann nicht gespielt werden.", ToastKind.Warning);
                return;
            }
            SpawnFloatingText("Eingesetzt!", new Color(0.95f, 0.78f, 0.30f));
            DrainPersonalityEvents().Forget();
            RefreshAll();
        }

        /// <summary>
        /// Zeigt alle noch nicht angezeigten Personality-Events als Toast.
        /// Designplan v4 Kap. 8 (Karten-Persoenlichkeit).
        /// </summary>
        private async UniTask DrainPersonalityEvents()
        {
            if (_engine == null) return;
            var events = _engine.State.Events;
            while (_eventsShownCount < events.Count)
            {
                var evt = events[_eventsShownCount];
                _eventsShownCount++;
                await ShowEventToast(evt);
            }
        }

        private async UniTask ShowEventToast(BattleEvent evt)
        {
            string message;
            var kind = ToastKind.Info;
            switch (evt.EventType)
            {
                case BattleEventType.CardPlayed:
                case BattleEventType.CardVictory:
                case BattleEventType.CardDied:
                    if (string.IsNullOrEmpty(evt.LocalizationKey)) return;
                    message = _loc.Get(evt.LocalizationKey!, evt.LocalizationKey);
                    kind = evt.EventType == BattleEventType.CardDied ? ToastKind.Warning : ToastKind.Info;
                    break;
                case BattleEventType.SynergyActivated:
                    message = $"✨ Synergie: +{evt.Magnitude}% Bonus";
                    kind = ToastKind.Success;
                    break;
                case BattleEventType.RivalryClashed:
                    message = "⚡ Rivalen-Konflikt!";
                    kind = ToastKind.Warning;
                    break;
                case BattleEventType.HeroPassivTriggered:
                    message = $"⭐ {_loc.Get(evt.LocalizationKey ?? "", "Helden-Passiv")}";
                    kind = ToastKind.Success;
                    break;
                case BattleEventType.BossPhaseChange:
                    // Spielplan v5 Kap. 9.4: Boss-Phase 2 — dramatischer Toast + UI-Flash
                    message = $"⚠️ BOSS-PHASE 2 — {_loc.Get(evt.LocalizationKey ?? "", "Boss erwacht!")} (+{evt.Magnitude} Karten)";
                    kind = ToastKind.Danger;
                    break;
                default: return;
            }
            _toast.Show(message, kind, 2.5f);
            await UniTask.Delay(180);   // kleine Verzoegerung zwischen Toasts
        }

        private async UniTask OnEndTurnAsync()
        {
            if (_busy || _engine == null) return;
            if (_engine.State.Phase != BattlePhase.PlayerTurn) return;
            _busy = true;

            // 1. Player-EndTurn (Engine wickelt Combat ab, Phase wechselt zu Enemy)
            _engine.EndTurn();
            await DrainPersonalityEvents();
            RefreshAll();
            if (_engine.State.Result != BattleResult.Undecided)
            {
                await HandleGameOverAsync();
                _busy = false;
                return;
            }

            // 2. Kurze Pause damit Spieler die Phase wahrnimmt
            await UniTask.Delay(500);

            // 3. Enemy spielt Karten (AI)
            if (_ai != null)
            {
                var enemyHand = _engine.State.EnemyHand.ToList();
                var picks = _ai.ChooseCardsToPlay(enemyHand, _engine.State.EnemyMana);
                foreach (var instId in picks)
                {
                    _engine.PlayCard(forPlayer: false, instId);
                    RefreshAll();
                    await UniTask.Delay(280);
                }
            }

            // 4. Enemy-EndTurn (Engine fuehrt Enemy-Combat, wechselt zu Player)
            _engine.EndTurn();
            await DrainPersonalityEvents();
            RefreshAll();
            if (_engine.State.Result != BattleResult.Undecided)
                await HandleGameOverAsync();

            _busy = false;
        }

        private async UniTask HandleGameOverAsync()
        {
            var stars = _difficulty.StarsOnVictory();
            switch (_engine!.State.Result)
            {
                case BattleResult.PlayerWins:
                    var reward = _node?.GoldReward(_difficulty) ?? 50;
                    var exp = _node?.ExpReward(_difficulty) ?? 25;
                    await ApplyRewardsAsync(reward, exp, stars);
                    _toast.Show($"Sieg! ★ {stars}/4 — +{reward} Gold, +{exp} EXP", ToastKind.Success, 5f);

                    // v6 (Designplan v4 Story Kap. 9): Welt-Boss-Sieg -> Erinnerungs-Fragment
                    if (_node != null && _node.Type == NodeType.WorldBoss)
                        await ShowMemoryFragmentIfNewAsync();
                    break;
                case BattleResult.EnemyWins:
                    _toast.Show("Niederlage — versuch's nochmal.", ToastKind.Danger, 4f);
                    break;
                case BattleResult.Draw:
                    _toast.Show("Unentschieden.", ToastKind.Warning, 4f);
                    break;
            }
            await UniTask.Delay(1500);

            // Spielplan v5 Kap. 11.2: Schlachtbericht-Screen statt einfach Pop.
            // Battle-Report-Context fuellen + replacen (Pop + Push wuerde Stack-Order zerstoeren).
            await ShowBattleReportAsync();
        }

        /// <summary>
        /// Bereitet den BattleReportScreen vor und ersetzt den BattleScreen damit.
        /// </summary>
        private async UniTask ShowBattleReportAsync()
        {
            // Report-Context mit echten Battle-Daten befuellen
            _reportCtx.IsVictory = _engine!.State.Result == BattleResult.PlayerWins;
            _reportCtx.IsDraw    = _engine.State.Result == BattleResult.Draw;
            _reportCtx.Stars     = _reportCtx.IsVictory ? _difficulty.StarsOnVictory() : 0;
            _reportCtx.GoldReward = _reportCtx.IsVictory && _node != null ? _node.GoldReward(_difficulty) : 0;
            _reportCtx.ExpReward  = _reportCtx.IsVictory && _node != null ? _node.ExpReward(_difficulty) : 0;
            _reportCtx.NodeId     = _node?.Id;

            // Fallback: wenn der Report-Screen nicht registriert ist, einfach poppen.
            if (!_screenManager.IsRegistered(ScreenId.BattleReport))
            {
                await _screenManager.PopAsync();
                return;
            }

            // BattleScreen poppen und Report pushen — Report kann beim Schliessen
            // wieder zum WorldMap zurueckkehren.
            await _screenManager.PopAsync();
            _screenManager.PushAsync(ScreenId.BattleReport).Forget();
        }

        /// <summary>
        /// Zeigt das Erinnerungs-Fragment der Welt wenn der Spieler diesen Welt-Boss
        /// zum ersten Mal besiegt hat (Designplan v4 Story Kap. 9).
        /// </summary>
        private async UniTask ShowMemoryFragmentIfNewAsync()
        {
            if (_node == null) return;
            var worldId = WorldIdForNode(_node);
            if (string.IsNullOrEmpty(worldId)) return;

            var world = _worldCatalog.Find(worldId);
            if (world == null || string.IsNullOrEmpty(world.MemoryFragmentKey)) return;

            // Pruefen ob das Fragment bereits angesehen wurde — sonst zeigen
            var saveR = await _save.LoadAsync();
            if (!saveR.IsSuccess || saveR.Value == null) return;
            var fragmentId = world.Id;   // z.B. "elderwald" -> fragment-id = welt-id
            if (saveR.Value.Story.ViewedMemoryFragments.Contains(fragmentId)) return;

            // Fragment-Index aus Welt-Index ableiten (welt 1 -> fragment 1, ...)
            var isMajorTwist = world.Index == 8;   // Abysstiefe = DER TWIST
            _memoryCtx.FragmentId = fragmentId;
            _memoryCtx.TitleKey   = $"fragment.{world.Index}.title";
            _memoryCtx.ContentKey = world.MemoryFragmentKey;
            _memoryCtx.TwistRevealKey = $"fragment.{world.Index}.reveal";
            _memoryCtx.IsMajorTwist = isMajorTwist;

            await _screenManager.PushAsync(ScreenId.MemoryFragmentOverlay);
        }

        private async UniTask ApplyRewardsAsync(int gold, int exp, int stars)
        {
            await _save.MutateAsync(save =>
            {
                save.Currencies.AddGold(gold);
                save.Profile.ExpTotal += exp;
                // Welt-Progress: Sterne entsprechen der gewaehlten Schwierigkeit
                // (Classic=1, Amateur=2, Profi=3, Gott=4). Nur ueberschreiben wenn besser.
                if (_node != null)
                {
                    var worldId = WorldIdForNode(_node);
                    if (!string.IsNullOrEmpty(worldId))
                    {
                        if (!save.WorldProgress.TryGetValue(worldId, out var wp))
                        {
                            wp = new WorldProgress(worldId);
                            save.WorldProgress[worldId] = wp;
                        }
                        var prevStars = wp.StarsByNodeId.TryGetValue(_node.Id, out var s) ? s : 0;
                        if (prevStars < stars) wp.StarsByNodeId[_node.Id] = stars;
                    }
                }
                return save;
            });
        }

        private static string WorldIdForNode(NodeDefinition node)
        {
            // Konvention: "world_1_node_3" -> "world_1"
            var idx = node.Id.IndexOf("_node_", System.StringComparison.Ordinal);
            return idx > 0 ? node.Id.Substring(0, idx) : string.Empty;
        }

        private void OnFlee()
        {
            _toast.Show("Aufgegeben.", ToastKind.Warning);
            _screenManager.PopAsync().Forget();
        }

        // ============================================================
        // Floating Damage-Numbers (UI-Effekt)
        // ============================================================

        private void SpawnFloatingText(string text, Color color)
        {
            var label = new Label(text);
            label.style.position = Position.Absolute;
            label.style.left = Random.Range(300, 600);
            label.style.top = Random.Range(200, 400);
            label.style.fontSize = 28;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new StyleColor(color);
            label.style.transitionProperty = new List<StylePropertyName> { "opacity", "translate" };
            label.style.transitionDuration = new List<TimeValue> { new TimeValue(900, TimeUnit.Millisecond) };
            _floatingLayer.Add(label);

            FadeAndRiseAsync(label).Forget();
        }

        private static async UniTaskVoid FadeAndRiseAsync(Label label)
        {
            await UniTask.Yield();
            label.style.translate = new StyleTranslate(new Translate(0, new Length(-40, LengthUnit.Pixel)));
            label.style.opacity = 0;
            await UniTask.Delay(950);
            label.RemoveFromHierarchy();
        }

        // ============================================================
        // Helpers
        // ============================================================

        private async UniTaskVoid LoadArtAsync(VisualElement art, CardDefinition def)
        {
            var sprite = await _artworkService.GetSpriteAsync(def);
            if (sprite == null || art.panel == null) return;
            art.style.backgroundImage = new StyleBackground(sprite);
        }

        private CardDefinition? ResolveDefinition(string cardInstanceId)
        {
            // 1. Direkt aus Catalog (Catalog-ID = CardDefinition.Id)
            var direct = _cardCatalog.Find(cardInstanceId);
            if (direct != null) return direct;
            // 2. Via Engine-Defs (catalog-IDs sind dort schon drin, aber theoretisch sicher)
            if (_defs != null && _defs.TryGetValue(cardInstanceId, out var d)) return d;
            // 3. PlayerInstance (GUID) -> CardInstance -> CardDefinitionId -> Catalog
            if (_instances != null && _instances.TryGetValue(cardInstanceId, out var inst))
                return _cardCatalog.Find(inst.CardDefinitionId);
            return null;
        }

        private static string RarityClass(Rarity r) => r switch
        {
            Rarity.Ungewoehnlich => "ak-card--rarity-uncommon",
            Rarity.Selten        => "ak-card--rarity-rare",
            Rarity.Epic          => "ak-card--rarity-epic",
            Rarity.Legendaer     => "ak-card--rarity-legendary",
            Rarity.Mythisch      => "ak-card--rarity-mythic",
            _                    => "ak-card--rarity-common"
        };
    }
}
