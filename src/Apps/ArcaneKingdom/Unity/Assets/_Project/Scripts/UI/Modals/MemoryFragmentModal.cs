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
    /// Persistiert in StorySaveSlice.ViewedMemoryFragments damit nicht 2x angezeigt wird.
    /// </summary>
    public sealed class MemoryFragmentModal : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ILocalizationService _loc;

        private Label _fragmentTitle = null!;
        private Label _fragmentContent = null!;
        private Label _twistReveal = null!;
        private VisualElement _twistBanner = null!;
        private Button _continueButton = null!;

        /// <summary>Welche Fragment-ID anzeigen? Wird vor OnEnter gesetzt.</summary>
        public string? FragmentId { get; set; }
        public string? TitleKey { get; set; }
        public string? ContentKey { get; set; }
        public string? TwistRevealKey { get; set; }
        public bool IsMajorTwist { get; set; }

        public override string Id => ScreenId.MemoryFragmentOverlay;
        public override bool IsOverlay => true;
        protected override string UxmlPath => "UI/Modals/MemoryFragmentModal";

        public MemoryFragmentModal(
            ScreenManager screenManager,
            ISaveService<PlayerSave> save,
            ILocalizationService loc)
        {
            _screenManager = screenManager;
            _save = save;
            _loc = loc;
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
            _fragmentTitle.text = TitleKey != null ? (_loc.Get(TitleKey) ?? TitleKey) : "Erinnerung";
            _fragmentContent.text = ContentKey != null ? (_loc.Get(ContentKey) ?? ContentKey) : string.Empty;
            _twistReveal.text = TwistRevealKey != null ? (_loc.Get(TwistRevealKey) ?? string.Empty) : string.Empty;

            // Twist-Banner bei Welt 8 (Fragment 8) gross hervorheben
            if (IsMajorTwist)
            {
                _twistBanner.style.display = DisplayStyle.Flex;
                _twistBanner.AddToClassList("ak-memory__twist-banner--major");
            }
            else
            {
                _twistBanner.style.display = DisplayStyle.None;
            }

            // Im Save als "gesehen" markieren
            if (!string.IsNullOrEmpty(FragmentId))
            {
                await _save.MutateAsync(s =>
                {
                    s.Story.UnlockedMemoryFragments.Add(FragmentId!);
                    s.Story.ViewedMemoryFragments.Add(FragmentId!);
                    if (IsMajorTwist) s.Story.TwistRevealed = true;
                    return s;
                }, ct);
            }
        }

        private void OnContinueClicked() => _screenManager.PopAsync().Forget();
    }
}
