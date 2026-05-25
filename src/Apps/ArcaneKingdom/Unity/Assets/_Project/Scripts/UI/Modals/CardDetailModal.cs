#nullable enable
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Modals
{
    /// <summary>
    /// Card-Detail-Overlay. Wird ueber dem aktuellen Screen (Hub/DeckBuilder/Codex) gezeigt
    /// und zeigt Stats, Abilities, Rune-Slots, Owned-Info und Aktions-Buttons.
    ///
    /// Aufruf:
    /// <code>
    ///   _modalContext.Set("card", cardDefinition);
    ///   await _screenManager.PushAsync(ScreenId.CardDetailOverlay);
    /// </code>
    /// </summary>
    public sealed class CardDetailModal : ScreenBase
    {
        public const string ContextKey = "card";

        private readonly ScreenManager _screenManager;
        private readonly ModalContext _context;
        private readonly ToastService _toast;
        private readonly ISaveService<PlayerSave> _save;
        private readonly CardArtworkService _artworkService;

        // Bindings
        private Label _name = null!;
        private Label _element = null!;
        private Label _rarity = null!;
        private Label _race = null!;
        private Label _cost = null!;
        private Label _atk = null!;
        private Label _hp = null!;
        private Label _turns = null!;
        private VisualElement _abilities = null!;
        private Label _ownedInfo = null!;
        private Button _upgradeBtn = null!;
        private Button _deckBtn = null!;
        private Button _closeBtn = null!;

        public override string Id => ScreenId.CardDetailOverlay;
        public override bool IsOverlay => true;
        protected override string UxmlPath => "UI/CardDetailModal";

        public CardDetailModal(ScreenManager screenManager,
                               ModalContext context,
                               ToastService toast,
                               ISaveService<PlayerSave> save,
                               CardArtworkService artworkService)
        {
            _screenManager = screenManager;
            _context = context;
            _toast = toast;
            _save = save;
            _artworkService = artworkService;
        }

        protected override void BindElements(VisualElement root)
        {
            _name       = Q<Label>("card-detail-name");
            _element    = Q<Label>("card-detail-element");
            _rarity     = Q<Label>("card-detail-rarity");
            _race       = Q<Label>("card-detail-race");
            _cost       = Q<Label>("card-detail-cost");
            _atk        = Q<Label>("card-detail-atk");
            _hp         = Q<Label>("card-detail-hp");
            _turns      = Q<Label>("card-detail-turns");
            _abilities  = Q<VisualElement>("card-detail-abilities");
            _ownedInfo  = Q<Label>("card-detail-owned-info");
            _upgradeBtn = Q<Button>("card-detail-upgrade");
            _deckBtn    = Q<Button>("card-detail-deck");
            _closeBtn   = Q<Button>("card-detail-close");

            // Backdrop-Klick schliesst
            var backdrop = Q<VisualElement>("card-detail-backdrop");
            backdrop.RegisterCallback<ClickEvent>(evt =>
            {
                // Nur schliessen wenn Klick AUF dem Backdrop (nicht innerhalb des Modals)
                if (evt.target == backdrop) Close();
            });

            _closeBtn.clicked += Close;
            _upgradeBtn.clicked += () =>
                _toast.Show("Upgrade-Logik kommt mit DeckBuilder (Stufe 6).", ToastKind.Info);
            _deckBtn.clicked += () =>
                _toast.Show("Karte zum Deck — DeckBuilder folgt in Stufe 6.", ToastKind.Info);
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var card = _context.Get<CardDefinition>(ContextKey);
            if (card == null)
            {
                _toast.Show("Keine Karte uebergeben.", ToastKind.Danger);
                Close();
                return;
            }

            PopulateCard(card);

            // Owned-Info aus Save
            var saveResult = await _save.LoadAsync(ct);
            if (saveResult.IsSuccess && saveResult.Value != null)
            {
                var owned = saveResult.Value.CardInventory.Values
                    .Count(i => i.CardDefinitionId == card.Id);
                _ownedInfo.text = owned > 0
                    ? $"Im Besitz: {owned}× — Saison-Limit: {card.GlobalCraftLimit}"
                    : $"Noch nicht im Besitz — Saison-Limit: {card.GlobalCraftLimit}";
                _upgradeBtn.SetEnabled(owned > 0);
                _deckBtn.SetEnabled(owned > 0);
            }
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            _context.Remove(ContextKey);
            return UniTask.CompletedTask;
        }

        private void PopulateCard(CardDefinition card)
        {
            _name.text    = NicifyId(card.Id);
            _element.text = $"Element: {card.Element}";
            _rarity.text  = $"Raritaet: {card.Rarity}";
            _race.text    = $"Rasse: {card.Race}";
            _cost.text    = $"Kosten: {card.Cost}";

            _atk.text   = card.BaseAttack.ToString("N0");
            _hp.text    = card.BaseHealth.ToString("N0");
            _turns.text = $"{card.TurnsToSpecial} Z";

            _abilities.Clear();
            AddAbility("LV 0",  card.BaseAbility?.Id);
            AddAbility("LV 5",  card.SecondAbility?.Id);
            AddAbility("LV 10", card.ThirdAbility?.Id);

            // Artwork in den Platzhalter laden (Procedural-Fallback wenn kein Sprite)
            var art = Root.Q<VisualElement>("card-detail-art");
            if (art != null) LoadArtAsync(art, card).Forget();
        }

        private async UniTaskVoid LoadArtAsync(VisualElement art, CardDefinition card)
        {
            var sprite = await _artworkService.GetSpriteAsync(card);
            if (sprite == null || art.panel == null) return;
            art.style.backgroundImage = new UnityEngine.UIElements.StyleBackground(sprite);
        }

        private void AddAbility(string levelLabel, string? abilityId)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var level = new Label(levelLabel);
            level.AddToClassList("ak-caption");
            level.style.width = 56;
            row.Add(level);

            var name = new Label(abilityId ?? "—");
            name.AddToClassList("ak-body");
            name.style.flexGrow = 1;
            if (abilityId == null) name.AddToClassList("ak-text--muted");
            row.Add(name);

            _abilities.Add(row);
        }

        private void Close() => _screenManager.PopAsync().Forget();

        /// <summary>"card_drachenherrscher" -> "Drachenherrscher".</summary>
        private static string NicifyId(string id)
        {
            var idx = id.IndexOf('_');
            if (idx < 0 || idx >= id.Length - 1) return id;
            var raw = id.Substring(idx + 1).Replace('_', ' ');
            return raw.Length == 0 ? id : char.ToUpper(raw[0]) + raw.Substring(1);
        }
    }
}
