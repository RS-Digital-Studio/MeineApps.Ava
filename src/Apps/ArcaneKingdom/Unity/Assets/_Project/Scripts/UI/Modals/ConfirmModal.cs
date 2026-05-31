#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Modals
{
    /// <summary>
    /// Generischer Bestaetigungs-Dialog fuer teure/irreversible Aktionen (Projekt-Regel:
    /// "Confirmation vor destruktiven Operationen"). Wiederverwendbar — Parameter (lokalisierte
    /// Texte + Bestaetigungs-Aktion) kommen via <see cref="ConfirmContext"/> VOR dem Push.
    ///
    /// Bei Bestaetigung wird das Modal gepoppt und danach <see cref="ConfirmContext.OnConfirmed"/>
    /// ausgefuehrt; bei Abbruch nur gepoppt.
    /// </summary>
    public sealed class ConfirmModal : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ILocalizationService _loc;
        private readonly ConfirmContext _ctx;

        private Label _title = null!;
        private Label _message = null!;
        private Button _confirmButton = null!;
        private Button _cancelButton = null!;

        public override string Id => ScreenId.ConfirmOverlay;
        public override bool IsOverlay => true;
        protected override string UxmlPath => "UI/Modals/ConfirmModal";

        public ConfirmModal(ScreenManager screenManager, ILocalizationService loc, ConfirmContext ctx)
        {
            _screenManager = screenManager;
            _loc = loc;
            _ctx = ctx;
        }

        protected override void BindElements(VisualElement root)
        {
            _title = Q<Label>("confirm-title");
            _message = Q<Label>("confirm-message");
            _confirmButton = Q<Button>("confirm-ok-button");
            _cancelButton = Q<Button>("confirm-cancel-button");

            _confirmButton.clicked += OnConfirm;
            _cancelButton.clicked += () => _screenManager.PopAsync().Forget();
        }

        public override UniTask OnEnterAsync(CancellationToken ct)
        {
            _title.text = _ctx.Title;
            _message.text = _ctx.Message;
            _confirmButton.text = _ctx.ConfirmLabel ?? _loc.Get("confirm.ok", "Bestätigen");
            _cancelButton.text = _ctx.CancelLabel ?? _loc.Get("confirm.cancel", "Abbrechen");

            // Bei destruktiver Aktion den Bestaetigen-Button visuell als Gefahr markieren.
            _confirmButton.EnableInClassList("ak-text--danger", _ctx.Danger);
            return UniTask.CompletedTask;
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            _ctx.Reset();
            return UniTask.CompletedTask;
        }

        private void OnConfirm()
        {
            // Aktion VOR dem Pop sichern — OnLeaveAsync resettet den Context.
            var action = _ctx.OnConfirmed;
            _screenManager.PopAsync().Forget();
            action?.Invoke();
        }
    }
}
