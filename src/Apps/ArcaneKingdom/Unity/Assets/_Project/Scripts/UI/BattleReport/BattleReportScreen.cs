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
        private readonly BattleReportContext _ctx;
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

        public BattleReportScreen(ScreenManager screenManager, BattleReportContext ctx, ToastService toast)
        {
            _screenManager = screenManager;
            _ctx = ctx;
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
            _replayBtn.clicked  += () => _toast.Show("Replay-System kommt in einer spaeteren Stufe.", ToastKind.Info);
            _againBtn.clicked   += () => _screenManager.PopAsync().Forget();
            _profileBtn.clicked += () => _screenManager.PushAsync(ScreenId.PlayerProfile).Forget();

            Populate();
        }

        public override Cysharp.Threading.Tasks.UniTask OnEnterAsync(System.Threading.CancellationToken ct)
        {
            Populate();
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        }

        public override Cysharp.Threading.Tasks.UniTask OnLeaveAsync(System.Threading.CancellationToken ct)
        {
            _ctx.Reset();
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        }

        private void Populate()
        {
            if (_ctx.IsDraw)
            {
                _resultBanner.text = "UNENTSCHIEDEN";
                _resultBanner.style.color = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f));
            }
            else if (_ctx.IsVictory)
            {
                _resultBanner.text = "SIEG";
                _resultBanner.style.color = new StyleColor(new UnityEngine.Color(0.41f, 0.94f, 0.68f));
            }
            else
            {
                _resultBanner.text = "NIEDERLAGE";
                _resultBanner.style.color = new StyleColor(new UnityEngine.Color(0.78f, 0.16f, 0.16f));
            }

            // Belohnungen / Rang-Aenderung
            if (_ctx.RankDelta.HasValue && _ctx.NewRank.HasValue)
            {
                var arrow = _ctx.RankDelta.Value >= 0 ? "↑" : "↓";
                _rankChange.text = $"{_ctx.RankDelta:+0;-0} Rangpunkte {arrow} Rang {_ctx.NewRank}";
            }
            else if (_ctx.IsVictory && _ctx.Stars > 0)
            {
                var stars = new string('★', _ctx.Stars);
                _rankChange.text = $"{stars}  +{_ctx.GoldReward:N0} Gold  +{_ctx.ExpReward} EXP";
            }
            else
            {
                _rankChange.text = string.Empty;
            }

            // Gegner
            if (!string.IsNullOrEmpty(_ctx.OpponentName))
            {
                _opponentName.text = _ctx.OpponentLevel.HasValue
                    ? $"{_ctx.OpponentName} — LV {_ctx.OpponentLevel}"
                    : _ctx.OpponentName;
            }
            else if (!string.IsNullOrEmpty(_ctx.NodeId))
            {
                _opponentName.text = $"Node: {_ctx.NodeId}";
            }
            else
            {
                _opponentName.text = "—";
            }

            _battleTime.text = System.DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        }
    }
}
