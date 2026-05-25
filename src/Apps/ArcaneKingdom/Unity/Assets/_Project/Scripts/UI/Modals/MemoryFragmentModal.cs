#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Modals
{
    /// <summary>
    /// Cutscene-Modal fuer Erinnerungs-Fragmente nach einem Welt-Boss-Sieg
    /// (Designplan v4 Story Kap. 9).
    ///
    /// 10 Fragmente erzaehlen schrittweise die Wahrheit ueber den Spieler:
    ///   Fragment 1-7: zunehmende Andeutungen
    ///   Fragment 8 (Welt Abysstiefe): DER TWIST — Spieler war Nythragors Champion
    ///   Fragment 9-10: Erloesung + finaler Name
    ///
    /// Anzeige: schwarz-weisser Bildschirm + dramatischer Text + Twist-Reveal-Klang.
    /// Caller setzt Parameter via <see cref="MemoryFragmentContext"/> VOR dem Push.
    /// </summary>
    public sealed class MemoryFragmentModal : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ILocalizationService _loc;
        private readonly MemoryFragmentContext _ctx;

        private Label _fragmentTitle = null!;
        private Label _fragmentContent = null!;
        private Label _twistReveal = null!;
        private VisualElement _twistBanner = null!;
        private Button _continueButton = null!;

        public override string Id => ScreenId.MemoryFragmentOverlay;
        public override bool IsOverlay => true;
        protected override string UxmlPath => "UI/Modals/MemoryFragmentModal";

        public MemoryFragmentModal(
            ScreenManager screenManager,
            ISaveService<PlayerSave> save,
            ILocalizationService loc,
            MemoryFragmentContext ctx)
        {
            _screenManager = screenManager;
            _save = save;
            _loc = loc;
            _ctx = ctx;
        }

        protected override void BindElements(VisualElement root)
        {
            _fragmentTitle = Q<Label>("fragment-title");
            _fragmentContent = Q<Label>("fragment-content");
            _twistReveal = Q<Label>("twist-reveal");
            _twistBanner = Q<VisualElement>("twist-banner");
            _continueButton = Q<Button>("continue-button");

            _continueButton.clicked += OnContinueClicked;
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            _fragmentTitle.text = _ctx.TitleKey != null ? _loc.Get(_ctx.TitleKey, _ctx.TitleKey) : "Erinnerung";
            _fragmentContent.text = _ctx.ContentKey != null ? _loc.Get(_ctx.ContentKey, _ctx.ContentKey) : string.Empty;
            _twistReveal.text = _ctx.TwistRevealKey != null ? _loc.Get(_ctx.TwistRevealKey, string.Empty) : string.Empty;

            if (_ctx.IsMajorTwist)
            {
                _twistBanner.style.display = DisplayStyle.Flex;
                _twistBanner.AddToClassList("ak-memory__twist-banner--major");
            }
            else
            {
                _twistBanner.style.display = DisplayStyle.None;
            }

            if (!string.IsNullOrEmpty(_ctx.FragmentId))
            {
                var fragId = _ctx.FragmentId!;
                var isMajor = _ctx.IsMajorTwist;
                await _save.MutateAsync(s =>
                {
                    s.Story.UnlockedMemoryFragments.Add(fragId);
                    s.Story.ViewedMemoryFragments.Add(fragId);
                    if (isMajor) s.Story.TwistRevealed = true;
                    return s;
                }, ct);
            }
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            _ctx.Reset();
            return UniTask.CompletedTask;
        }

        private void OnContinueClicked() => _screenManager.PopAsync().Forget();
    }
}
