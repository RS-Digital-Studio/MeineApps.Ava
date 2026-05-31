#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Save;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Modals
{
    /// <summary>
    /// Finale Endkampf-Entscheidung (Story v4 Kap. 10): nach dem Sieg ueber den letzten
    /// Welt-Boss (Welt 10 Drachenfeste) waehlt der Spieler, ob er Nythragor ZERSTOERT
    /// oder ERLOEST. Die Wahl wird in <see cref="StorySaveSlice.EndingChoice"/> persistiert;
    /// danach wird die jeweilige Ending-Cutscene gezeigt.
    ///
    /// Erscheint beim ersten W10-Sieg (EndingChoice == null) mit der Choice-Phase; ist die
    /// Wahl bereits getroffen, zeigt das Modal direkt das gewaehlte Ende (Wiederbesuch).
    /// Markiert zugleich das Finale-Fragment (Welt-ID "drachenfeste") als gesehen.
    /// </summary>
    public sealed class EndingChoiceModal : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ILocalizationService _loc;

        private Label _intro = null!;
        private Label _prompt = null!;
        private VisualElement _choiceRow = null!;
        private Button _destroyButton = null!;
        private Button _redeemButton = null!;
        private Label _resultTitle = null!;
        private Label _resultText = null!;
        private Button _closeButton = null!;

        public override string Id => ScreenId.EndingChoiceOverlay;
        public override bool IsOverlay => true;
        protected override string UxmlPath => "UI/Modals/EndingChoiceModal";

        public EndingChoiceModal(
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
            _intro = Q<Label>("ending-intro");
            _prompt = Q<Label>("ending-prompt");
            _choiceRow = Q<VisualElement>("ending-choice-row");
            _destroyButton = Q<Button>("ending-destroy-button");
            _redeemButton = Q<Button>("ending-redeem-button");
            _resultTitle = Q<Label>("ending-result-title");
            _resultText = Q<Label>("ending-result-text");
            _closeButton = Q<Button>("ending-close-button");

            _destroyButton.clicked += () => ChooseAsync(NythragorEndingChoice.Destroyed).Forget();
            _redeemButton.clicked += () => ChooseAsync(NythragorEndingChoice.Redeemed).Forget();
            _closeButton.clicked += () => _screenManager.PopAsync().Forget();
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            _intro.text = _loc.Get("ending.intro", string.Empty);
            _prompt.text = _loc.Get("ending.choice.prompt", string.Empty);
            _destroyButton.text = _loc.Get("ending.choice.destroy", "Nythragor zerstoeren");
            _redeemButton.text = _loc.Get("ending.choice.redeem", "Nythragor erloesen");
            _closeButton.text = _loc.Get("ending.close", "Vollenden");

            var loaded = await _save.LoadAsync(ct);
            var existing = loaded.IsSuccess && loaded.Value != null
                ? loaded.Value.Story.EndingChoice
                : null;

            if (existing != null) ShowEnding(existing.Value);
            else ShowChoice();
        }

        private void ShowChoice()
        {
            _prompt.style.display = DisplayStyle.Flex;
            _choiceRow.style.display = DisplayStyle.Flex;
            _resultTitle.style.display = DisplayStyle.None;
            _resultText.style.display = DisplayStyle.None;
            _closeButton.style.display = DisplayStyle.None;
        }

        private void ShowEnding(NythragorEndingChoice choice)
        {
            _prompt.style.display = DisplayStyle.None;
            _choiceRow.style.display = DisplayStyle.None;

            var key = choice == NythragorEndingChoice.Destroyed ? "ending.destroyed" : "ending.redeemed";
            _resultTitle.text = _loc.Get(key + ".title", string.Empty);
            _resultText.text = _loc.Get(key + ".text", string.Empty);

            _resultTitle.style.display = DisplayStyle.Flex;
            _resultText.style.display = DisplayStyle.Flex;
            _closeButton.style.display = DisplayStyle.Flex;
        }

        private async UniTaskVoid ChooseAsync(NythragorEndingChoice choice)
        {
            // Doppelklick-Schutz: Buttons sofort sperren, sonst koennte die zweite Wahl
            // die erste ueberschreiben, bevor das Save geschrieben ist.
            _destroyButton.SetEnabled(false);
            _redeemButton.SetEnabled(false);

            await _save.MutateAsync(s =>
            {
                s.Story.EndingChoice = choice;
                // Finale-Fragment (Welt 10) als freigeschaltet + gesehen markieren — das Ending
                // ersetzt das normale Memory-Fragment-Modal fuer Drachenfeste.
                s.Story.UnlockedMemoryFragments.Add("drachenfeste");
                s.Story.ViewedMemoryFragments.Add("drachenfeste");
                s.Story.TwistRevealed = true;
                return s;
            }, CancellationToken.None);

            ShowEnding(choice);
        }
    }
}
