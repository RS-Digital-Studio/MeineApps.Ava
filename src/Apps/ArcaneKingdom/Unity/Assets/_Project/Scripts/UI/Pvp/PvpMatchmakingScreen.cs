#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Pvp
{
    /// <summary>
    /// PvP-Matchmaking-Screen (Spielplan v5 Kap. 11 + Impl_KOMPLETT Kap. 9.2).
    /// Animierter Suchkreis, "Suche Gegner...", Gegner-Gefunden-Fanfare, 3-2-1-Countdown.
    /// </summary>
    public sealed class PvpMatchmakingScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ToastService _toast;

        private Label _status = null!;
        private VisualElement _searchAnim = null!;
        private Label _opponentInfo = null!;
        private Label _countdown = null!;
        private Button _cancelBtn = null!;

        public override string Id => ScreenId.PvpMatchmaking;
        protected override string UxmlPath => "UI/PvpMatchmakingScreen";

        public PvpMatchmakingScreen(ScreenManager screenManager, ToastService toast)
        {
            _screenManager = screenManager;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _status       = Q<Label>("pvp-status");
            _searchAnim   = Q<VisualElement>("pvp-search-anim");
            _opponentInfo = Q<Label>("pvp-opponent-info");
            _countdown    = Q<Label>("pvp-countdown");
            _cancelBtn    = Q<Button>("pvp-cancel");

            _cancelBtn.clicked += () =>
            {
                _toast.Show("Suche abgebrochen.", ToastKind.Info);
                _screenManager.PopAsync().Forget();
            };
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            _status.text = "Suche Gegner...";
            _opponentInfo.text = string.Empty;
            _countdown.text = string.Empty;
            _searchAnim.AddToClassList("ak-spinning");

            // Mock-Suche: 2-3 Sekunden
            await UniTask.Delay(2500, cancellationToken: ct).SuppressCancellationThrow();
            if (ct.IsCancellationRequested) return;

            _status.text = "Gegner gefunden!";
            _opponentInfo.text = "[NEXUS] Sturmreiterin\nLV 88 — Siegesrate 64%";
            _searchAnim.RemoveFromClassList("ak-spinning");

            // 3-2-1-Countdown
            for (var i = 3; i > 0; i--)
            {
                _countdown.text = i.ToString();
                await UniTask.Delay(1000, cancellationToken: ct).SuppressCancellationThrow();
                if (ct.IsCancellationRequested) return;
            }

            _countdown.text = "KAMPF!";
            await UniTask.Delay(500, cancellationToken: ct).SuppressCancellationThrow();
            if (ct.IsCancellationRequested) return;

            // Wechsel zum Battle-Screen
            if (_screenManager.IsRegistered(ScreenId.Battle))
                await _screenManager.ReplaceAsync(ScreenId.Battle, ct);
            else
            {
                _toast.Show("Kampf startet (Battle-Screen folgt mit Photon).", ToastKind.Success);
                await _screenManager.PopAsync(ct);
            }
        }
    }
}
