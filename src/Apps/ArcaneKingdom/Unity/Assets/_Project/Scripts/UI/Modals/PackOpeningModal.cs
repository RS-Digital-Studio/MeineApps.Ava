#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Modals
{
    /// <summary>
    /// Pack-Opening-Overlay. Zeigt die geoeffneten Karten als Flip-Animation
    /// (eine nach der anderen) und schließt sich beim "Weiter"-Klick.
    ///
    /// Aufruf:
    /// <code>
    ///   _modalContext.Set("pack_rarities", new List&lt;Rarity&gt; {...});
    ///   await _screenManager.PushAsync(ScreenId.PackOpeningOverlay);
    /// </code>
    /// </summary>
    public sealed class PackOpeningModal : ScreenBase
    {
        public const string ContextKey = "pack_rarities";

        private readonly ScreenManager _screenManager;
        private readonly ModalContext _context;

        private Label _title = null!;
        private Label _subtitle = null!;
        private VisualElement _cardsRow = null!;
        private Button _revealAllBtn = null!;
        private Button _continueBtn = null!;

        private readonly List<VisualElement> _cardBacks = new();
        private int _revealedCount;

        public override string Id => ScreenId.PackOpeningOverlay;
        public override bool IsOverlay => true;
        protected override string UxmlPath => "UI/PackOpeningModal";

        public PackOpeningModal(ScreenManager screenManager, ModalContext context)
        {
            _screenManager = screenManager;
            _context = context;
        }

        protected override void BindElements(VisualElement root)
        {
            _title        = Q<Label>("pack-opening-title");
            _subtitle     = Q<Label>("pack-opening-subtitle");
            _cardsRow     = Q<VisualElement>("pack-opening-cards-row");
            _revealAllBtn = Q<Button>("pack-opening-reveal-all");
            _continueBtn  = Q<Button>("pack-opening-continue");

            _revealAllBtn.clicked += RevealAll;
            _continueBtn.clicked += Close;

            // Backdrop-Klick: revealt alle wenn noch Karten zu fluffen sind, sonst close
            var backdrop = Q<VisualElement>("pack-opening-backdrop");
            backdrop.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target != backdrop) return;
                if (_revealedCount < _cardBacks.Count) RevealNext();
                else Close();
            });
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            _cardsRow.Clear();
            _cardBacks.Clear();
            _revealedCount = 0;

            var rarities = _context.Get<List<Rarity>>(ContextKey);
            if (rarities == null || rarities.Count == 0)
            {
                _subtitle.text = "Keine Karten erhalten.";
                _revealAllBtn.SetEnabled(false);
                return;
            }

            _subtitle.text = $"{rarities.Count} Karten — tippe um aufzudecken";

            // Karten-Slots anlegen (alle initial verdeckt)
            for (var i = 0; i < rarities.Count; i++)
            {
                var slot = BuildCardSlot(rarities[i]);
                _cardsRow.Add(slot);
                _cardBacks.Add(slot);
            }

            // Erste Karte automatisch aufdecken nach kurzer Pause (Dramatik)
            await UniTask.Delay(System.TimeSpan.FromMilliseconds(400), cancellationToken: ct);
            if (!ct.IsCancellationRequested) RevealNext();
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            _context.Remove(ContextKey);
            return UniTask.CompletedTask;
        }

        private VisualElement BuildCardSlot(Rarity rarity)
        {
            var slot = new VisualElement();
            slot.AddToClassList("ak-card");
            slot.userData = rarity;

            // Initialer State: scale 0.92, opacity 0.6 (verdeckt)
            slot.style.scale = new StyleScale(new Scale(new Vector2(0.92f, 0.92f)));
            slot.style.opacity = 0.6f;
            slot.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName>
                { "opacity", "scale" };
            slot.style.transitionDuration = new System.Collections.Generic.List<TimeValue>
                { new TimeValue(220, TimeUnit.Millisecond) };

            // Card-Back-Visual: dunkelviolett mit Gold "?"
            var back = new Label("?");
            back.style.fontSize = 64;
            back.style.unityFontStyleAndWeight = FontStyle.Bold;
            back.style.color = new StyleColor(new Color(0.95f, 0.78f, 0.30f));
            back.style.unityTextAlign = TextAnchor.MiddleCenter;
            back.style.flexGrow = 1;
            slot.Add(back);

            slot.RegisterCallback<ClickEvent>(_ => RevealCard(slot, back, rarity));
            return slot;
        }

        private void RevealNext()
        {
            for (var i = 0; i < _cardBacks.Count; i++)
            {
                var slot = _cardBacks[i];
                var back = (Label)slot.Children().First();
                if (back.text == "?")
                {
                    var rarity = (Rarity)slot.userData;
                    RevealCard(slot, back, rarity);
                    return;
                }
            }
        }

        private void RevealAll()
        {
            foreach (var slot in _cardBacks)
            {
                var back = (Label)slot.Children().First();
                if (back.text != "?") continue;
                var rarity = (Rarity)slot.userData;
                RevealCard(slot, back, rarity);
            }
        }

        private void RevealCard(VisualElement slot, Label back, Rarity rarity)
        {
            if (back.text != "?") return; // Schon revealed

            _revealedCount++;
            back.text = rarity.ToString();
            back.style.fontSize = 18;

            slot.AddToClassList(RarityClass(rarity));
            slot.style.scale = new StyleScale(new Scale(new Vector2(1.05f, 1.05f)));
            slot.style.opacity = 1f;

            // Pop-Back nach Animation
            DelayPopBackAsync(slot).Forget();
        }

        private static async UniTaskVoid DelayPopBackAsync(VisualElement slot)
        {
            await UniTask.Delay(180);
            slot.style.scale = new StyleScale(new Scale(new Vector2(1.0f, 1.0f)));
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

        private void Close() => _screenManager.PopAsync().Forget();
    }
}
