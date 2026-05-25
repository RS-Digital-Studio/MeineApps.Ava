#nullable enable
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.BattleReport
{
    /// <summary>
    /// Schlachtbericht (Spielplan v5 Kap. 11.2 + Impl_KOMPLETT Kap. 9.4).
    /// WIN/LOST-Banner gross, Rang-Aenderung, Gegner-Info, Replay-/Nochmal-Buttons.
    /// </summary>
    public sealed class BattleReportScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ToastService _toast;

        private Label _resultBanner = null!;
        private Label _rankChange = null!;
        private Label _opponentName = null!;
        private Label _battleTime = null!;
        private Button _replayBtn = null!;
        private Button _againBtn = null!;
        private Button _profileBtn = null!;
        private Button _closeBtn = null!;

        public override string Id => ScreenId.BattleReport;
        protected override string UxmlPath => "UI/BattleReportScreen";

        public BattleReportScreen(ScreenManager screenManager, ToastService toast)
        {
            _screenManager = screenManager;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _resultBanner = Q<Label>("battle-report-banner");
            _rankChange   = Q<Label>("battle-report-rank-change");
            _opponentName = Q<Label>("battle-report-opponent");
            _battleTime   = Q<Label>("battle-report-time");
            _replayBtn    = Q<Button>("battle-report-replay");
            _againBtn     = Q<Button>("battle-report-again");
            _profileBtn   = Q<Button>("battle-report-profile");
            _closeBtn     = Q<Button>("battle-report-close");

            _closeBtn.clicked   += () => _screenManager.PopAsync().Forget();
            _replayBtn.clicked  += () => _toast.Show("Replay-System kommt in Stufe 9.", ToastKind.Info);
            _againBtn.clicked   += () => _toast.Show("Neue Suche gestartet.", ToastKind.Info);
            _profileBtn.clicked += () => _screenManager.PushAsync(ScreenId.PlayerProfile).Forget();

            // Demo-Werte — wuerden von Context/Service kommen
            _resultBanner.text = "WIN";
            _resultBanner.style.color = new StyleColor(new UnityEngine.Color(0.41f, 0.94f, 0.68f));
            _rankChange.text = "+12 Rangpunkte (Rang 89 → 88)";
            _opponentName.text = "[NEXUS] Sturmreiterin — LV 88";
            _battleTime.text = System.DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        }
    }
}
