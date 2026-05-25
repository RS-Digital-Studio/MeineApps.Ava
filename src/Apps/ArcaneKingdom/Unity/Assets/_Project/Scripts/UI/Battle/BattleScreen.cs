#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.UI.Common;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Battle
{
    /// <summary>
    /// Battle-Screen mit Mock-Battle (kein echtes BattleEngine-Setup — das kommt in
    /// einer Folge-Stufe). Zeigt: Spieler-/Gegner-Hero (HP/Mana), Felder (5 Slots je),
    /// Spieler-Hand (Klick zum Spielen wenn genug Mana), Zug-Beenden-Button, Floating
    /// Damage-Numbers.
    /// </summary>
    public sealed class BattleScreen : ScreenBase
    {
        private const int HeroStartHp = 100;
        private const int MaxFieldSlots = 5;
        private const int StartingHandSize = 4;
        private const int StartingMana = 3;

        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly CardCatalogService _cardCatalog;
        private readonly ToastService _toast;

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

        // State (Mock)
        private BattleState _state = null!;
        private List<CardDefinition> _playerDeckPool = new();
        private List<CardDefinition> _enemyDeckPool = new();
        private bool _busy;

        public override string Id => ScreenId.Battle;
        protected override string UxmlPath => "UI/BattleScreen";

        public BattleScreen(ScreenManager screenManager,
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

            _fleeBtn.clicked += () =>
            {
                _toast.Show("Aufgegeben.", ToastKind.Warning);
                _screenManager.PopAsync().Forget();
            };
            _endTurnBtn.clicked += () => OnEndTurnAsync().Forget();
        }

        public override UniTask OnEnterAsync(CancellationToken ct)
        {
            SetupMockBattle();
            RefreshAll();
            return UniTask.CompletedTask;
        }

        // ============================================================
        // Mock-Battle-Setup
        // ============================================================

        private void SetupMockBattle()
        {
            // Mock-Decks aus dem Katalog: jeweils 10 zufaellige Karten
            var allCards = _cardCatalog.AllCards.ToList();
            if (allCards.Count == 0)
            {
                _toast.Show("Keine Karten im Catalog — Sync ausfuehren.", ToastKind.Danger);
                return;
            }
            var rng = new System.Random();
            _playerDeckPool = allCards.OrderBy(_ => rng.Next()).Take(10).ToList();
            _enemyDeckPool  = allCards.OrderBy(_ => rng.Next()).Take(10).ToList();

            _state = new BattleState(rng.Next(), HeroStartHp, HeroStartHp);
            // Hand initialisieren (4 Karten)
            for (var i = 0; i < StartingHandSize && i < _playerDeckPool.Count; i++)
                _state.PlayerHand.Add(_playerDeckPool[i].Id);
            for (var i = 0; i < StartingHandSize && i < _enemyDeckPool.Count; i++)
                _state.EnemyHand.Add(_enemyDeckPool[i].Id);

            _state.Phase = BattlePhase.PlayerTurn;
        }

        // ============================================================
        // Render
        // ============================================================

        private void RefreshAll()
        {
            RenderHud();
            RenderEnemyField();
            RenderPlayerField();
            RenderPlayerHand();
            RenderManaOrbs();
        }

        private void RenderHud()
        {
            _turnNumber.text = _state.CurrentTurn.ToString();
            _phaseLabel.text = _state.Phase switch
            {
                BattlePhase.PlayerTurn => "Dein Zug",
                BattlePhase.EnemyTurn  => "Gegner-Zug",
                BattlePhase.Settlement => "Ende",
                _                      => _state.Phase.ToString()
            };

            UpdateHpBar(_playerHpFill, _playerHpText, _state.PlayerHeroHp, HeroStartHp);
            UpdateHpBar(_enemyHpFill, _enemyHpText, _state.EnemyHeroHp, HeroStartHp);
            _enemyManaText.text = $"{_state.EnemyMana}/{_state.EnemyMaxMana}";
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
            foreach (var slot in _state.EnemyField)
                _enemyField.Add(BuildFieldSlotTile(slot, isEnemy: true));
        }

        private void RenderPlayerField()
        {
            _playerField.Clear();
            foreach (var slot in _state.PlayerField)
                _playerField.Add(BuildFieldSlotTile(slot, isEnemy: false));
        }

        private VisualElement BuildFieldSlotTile(CardFieldSlot slot, bool isEnemy)
        {
            var def = _cardCatalog.Find(slot.CardInstanceId);
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

            if (!isEnemy && _state.Phase == BattlePhase.PlayerTurn)
            {
                tile.AddManipulator(new Clickable(() => OnPlayerAttack(slot)));
            }
            return tile;
        }

        private void RenderPlayerHand()
        {
            _playerHand.Clear();
            foreach (var cardId in _state.PlayerHand)
            {
                var def = _cardCatalog.Find(cardId);
                if (def == null) continue;
                var tile = BuildHandCardTile(def);
                _playerHand.Add(tile);
            }
        }

        private VisualElement BuildHandCardTile(CardDefinition def)
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

            var canPlay = _state.Phase == BattlePhase.PlayerTurn
                          && _state.PlayerMana >= def.Cost
                          && _state.PlayerField.Count < MaxFieldSlots;
            if (!canPlay)
            {
                tile.style.opacity = 0.5f;
            }
            else
            {
                // Drag&Drop: Karte aus Hand auf Player-Field ziehen.
                // Click-Fallback bleibt fuer Touch-Geraete erhalten (Tap auf Karte = Spielen).
                var drag = new CardDragManipulator(
                    dropZone: _playerField,
                    onDrop: () => OnPlayCard(def),
                    floatingLayer: _floatingLayer,
                    canDrag: () => _state.Phase == BattlePhase.PlayerTurn
                                    && _state.PlayerMana >= def.Cost
                                    && _state.PlayerField.Count < MaxFieldSlots);
                tile.AddManipulator(drag);
                tile.AddManipulator(new Clickable(() => OnPlayCard(def)));
            }
            return tile;
        }

        private void RenderManaOrbs()
        {
            _playerManaOrbs.Clear();
            for (var i = 0; i < _state.PlayerMaxMana; i++)
            {
                var orb = new VisualElement();
                orb.style.width = 14;
                orb.style.height = 14;
                orb.style.borderTopLeftRadius = 7;
                orb.style.borderTopRightRadius = 7;
                orb.style.borderBottomLeftRadius = 7;
                orb.style.borderBottomRightRadius = 7;
                orb.style.marginRight = 4;
                orb.style.backgroundColor = i < _state.PlayerMana
                    ? new StyleColor(new Color(0.30f, 0.65f, 0.95f))
                    : new StyleColor(new Color(0.30f, 0.30f, 0.40f));
                _playerManaOrbs.Add(orb);
            }
        }

        // ============================================================
        // Player-Actions
        // ============================================================

        private void OnPlayCard(CardDefinition def)
        {
            if (_busy) return;
            if (_state.Phase != BattlePhase.PlayerTurn) return;
            if (_state.PlayerMana < def.Cost) { _toast.Show("Nicht genug Mana.", ToastKind.Warning); return; }
            if (_state.PlayerField.Count >= MaxFieldSlots) { _toast.Show("Feld voll.", ToastKind.Warning); return; }

            _state.PlayerMana -= def.Cost;
            _state.PlayerHand.Remove(def.Id);
            _state.PlayerField.Add(new CardFieldSlot(def.Id, def.BaseAttack, def.BaseHealth, def.TurnsToSpecial));
            SpawnFloatingText("Eingesetzt!", new Color(0.95f, 0.78f, 0.30f));
            RefreshAll();
        }

        private void OnPlayerAttack(CardFieldSlot attacker)
        {
            if (_busy) return;
            if (_state.Phase != BattlePhase.PlayerTurn) return;

            // Vereinfacht: greift Enemy-Hero an wenn Feld leer, sonst erstes Feld
            if (_state.EnemyField.Count == 0)
            {
                _state.EnemyHeroHp = System.Math.Max(0, _state.EnemyHeroHp - attacker.CurrentAttack);
                SpawnFloatingText($"-{attacker.CurrentAttack}", Color.red);
            }
            else
            {
                var target = _state.EnemyField[0];
                target.CurrentHealth -= attacker.CurrentAttack;
                attacker.CurrentHealth -= target.CurrentAttack;
                SpawnFloatingText($"-{attacker.CurrentAttack}", new Color(0.95f, 0.50f, 0.50f));
                if (target.CurrentHealth <= 0) _state.EnemyField.RemoveAt(0);
                if (attacker.CurrentHealth <= 0) _state.PlayerField.Remove(attacker);
            }

            CheckGameOver();
            RefreshAll();
        }

        private async UniTask OnEndTurnAsync()
        {
            if (_busy || _state.Phase != BattlePhase.PlayerTurn) return;
            _busy = true;
            _state.Phase = BattlePhase.EnemyTurn;
            RenderHud();

            await UniTask.Delay(600);
            await RunEnemyTurnAsync();

            _state.CurrentTurn++;
            _state.PlayerMaxMana = System.Math.Min(_state.PlayerMaxMana + 1, 10);
            _state.PlayerMana = _state.PlayerMaxMana;
            _state.EnemyMaxMana = System.Math.Min(_state.EnemyMaxMana + 1, 10);
            _state.EnemyMana = _state.EnemyMaxMana;

            // Karte ziehen
            if (_state.PlayerHand.Count < 8 && _playerDeckPool.Count > _state.CurrentTurn + StartingHandSize)
                _state.PlayerHand.Add(_playerDeckPool[_state.CurrentTurn + StartingHandSize - 1].Id);

            _state.Phase = BattlePhase.PlayerTurn;
            _busy = false;
            RefreshAll();
        }

        private async UniTask RunEnemyTurnAsync()
        {
            // Mock-AI: spielt eine Karte wenn genug Mana, attackiert Spieler-Hero
            foreach (var enemyCardId in _state.EnemyHand.ToList())
            {
                if (_state.EnemyField.Count >= MaxFieldSlots) break;
                var def = _cardCatalog.Find(enemyCardId);
                if (def == null || _state.EnemyMana < def.Cost) continue;
                _state.EnemyMana -= def.Cost;
                _state.EnemyHand.Remove(enemyCardId);
                _state.EnemyField.Add(new CardFieldSlot(def.Id, def.BaseAttack, def.BaseHealth, def.TurnsToSpecial));
            }

            await UniTask.Delay(400);

            // Attacke: jede Enemy-Karte attackiert
            foreach (var enemyCard in _state.EnemyField.ToList())
            {
                if (_state.PlayerField.Count == 0)
                {
                    _state.PlayerHeroHp = System.Math.Max(0, _state.PlayerHeroHp - enemyCard.CurrentAttack);
                    SpawnFloatingText($"-{enemyCard.CurrentAttack}", Color.red);
                }
                else
                {
                    var target = _state.PlayerField[0];
                    target.CurrentHealth -= enemyCard.CurrentAttack;
                    enemyCard.CurrentHealth -= target.CurrentAttack;
                    if (target.CurrentHealth <= 0) _state.PlayerField.RemoveAt(0);
                    if (enemyCard.CurrentHealth <= 0) _state.EnemyField.Remove(enemyCard);
                }
                await UniTask.Delay(200);
            }

            CheckGameOver();
        }

        private void CheckGameOver()
        {
            if (_state.PlayerHeroHp <= 0)
            {
                _state.Result = BattleResult.EnemyWins;
                _toast.Show("Niederlage. Versuch's nochmal!", ToastKind.Danger, 6f);
                CloseAfterDelay().Forget();
            }
            else if (_state.EnemyHeroHp <= 0)
            {
                _state.Result = BattleResult.PlayerWins;
                _toast.Show("Sieg! +50 Gold +25 EXP", ToastKind.Success, 6f);
                CloseAfterDelay().Forget();
            }
        }

        private async UniTask CloseAfterDelay()
        {
            await UniTask.Delay(2200);
            await _screenManager.PopAsync();
        }

        // ============================================================
        // Floating Damage-Numbers
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

        private static string RarityClass(Rarity r) => r switch
        {
            Rarity.Ungewoehnlich => "ak-card--rarity-uncommon",
            Rarity.Selten        => "ak-card--rarity-rare",
            Rarity.Epic          => "ak-card--rarity-epic",
            Rarity.Legendaer     => "ak-card--rarity-legendary",
            _                    => "ak-card--rarity-common"
        };
    }
}
