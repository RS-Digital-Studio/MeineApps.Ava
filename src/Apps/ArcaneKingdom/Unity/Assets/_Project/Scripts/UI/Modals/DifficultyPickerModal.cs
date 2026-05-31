#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.World;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Modals
{
    /// <summary>
    /// Schwierigkeits-Auswahl-Popup auf der WorldMap (Spielplan v5 Kap. 8.3 + Impl_KOMPLETT Kap. 4.3).
    /// Nach Tap auf einen Node erscheint dieses Modal mit 4 Buttons:
    /// Classic (1*, 1 Energie) / Amateur (2*, 1) / Profi (3*, 2) / Gott (4*, 3 Energie).
    /// </summary>
    public sealed class DifficultyPickerModal : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly DifficultyPickerContext _ctx;
        private readonly ILocalizationService _loc;
        private readonly ToastService _toast;

        private Label _nodeName = null!;
        private Label _bestStars = null!;
        private Button _classicBtn = null!;
        private Button _amateurBtn = null!;
        private Button _profiBtn = null!;
        private Button _gottBtn = null!;
        private Button _closeBtn = null!;

        public override string Id => ScreenId.DifficultyPickerOverlay;
        public override bool IsOverlay => true;
        protected override string UxmlPath => "UI/DifficultyPickerModal";

        public DifficultyPickerModal(ScreenManager screenManager,
                                       DifficultyPickerContext ctx,
                                       ILocalizationService loc,
                                       ToastService toast)
        {
            _screenManager = screenManager;
            _ctx = ctx;
            _loc = loc;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _nodeName   = Q<Label>("difficulty-node-name");
            _bestStars  = Q<Label>("difficulty-best-stars");
            _classicBtn = Q<Button>("difficulty-classic");
            _amateurBtn = Q<Button>("difficulty-amateur");
            _profiBtn   = Q<Button>("difficulty-profi");
            _gottBtn    = Q<Button>("difficulty-gott");
            _closeBtn   = Q<Button>("difficulty-close");

            _classicBtn.clicked += () => Pick(NodeDifficulty.Classic);
            _amateurBtn.clicked += () => Pick(NodeDifficulty.Amateur);
            _profiBtn.clicked   += () => Pick(NodeDifficulty.Profi);
            _gottBtn.clicked    += () => Pick(NodeDifficulty.Gott);
            _closeBtn.clicked   += Close;

            // Backdrop-Klick schliesst
            var backdrop = QOptional<VisualElement>("difficulty-backdrop");
            if (backdrop != null)
            {
                backdrop.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == backdrop) Close();
                });
            }
        }

        public override UniTask OnEnterAsync(CancellationToken ct)
        {
            var node = _ctx.Node;
            if (node == null)
            {
                _toast.Show("Kein Node uebergeben.", ToastKind.Danger);
                Close();
                return UniTask.CompletedTask;
            }

            _nodeName.text = _loc.Get(node.DisplayNameKey, node.Id);
            _bestStars.text = _ctx.BestStarsSoFar > 0
                ? $"Bestwert: {_ctx.BestStarsSoFar}/4 Sterne"
                : "Noch nicht gespielt";

            ConfigureButton(_classicBtn, NodeDifficulty.Classic, node);
            ConfigureButton(_amateurBtn, NodeDifficulty.Amateur, node);
            ConfigureButton(_profiBtn, NodeDifficulty.Profi, node);
            ConfigureButton(_gottBtn, NodeDifficulty.Gott, node);

            return UniTask.CompletedTask;
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            _ctx.Reset();
            return UniTask.CompletedTask;
        }

        private void ConfigureButton(Button btn, NodeDifficulty difficulty, NodeDefinition node)
        {
            var starCount = (int)difficulty;
            var energy = difficulty.EnergyCost();
            var gold = node.GoldReward(difficulty);
            var label = difficulty switch
            {
                NodeDifficulty.Classic => "Classic",
                NodeDifficulty.Amateur => "Amateur",
                NodeDifficulty.Profi   => "Profi",
                NodeDifficulty.Gott    => "Gott",
                _ => difficulty.ToString()
            };
            btn.text = $"{label}\n{starCount}/4 Sterne\n{energy} Energie - {gold:N0} Gold";

            var canAfford = _ctx.AvailableEnergy >= energy;
            btn.SetEnabled(canAfford);
            btn.tooltip = canAfford ? string.Empty : "Nicht genug Energie";
        }

        private void Pick(NodeDifficulty difficulty)
        {
            var callback = _ctx.OnDifficultySelected;
            _screenManager.PopAsync().Forget();
            callback?.Invoke(difficulty);
        }

        private void Close() => _screenManager.PopAsync().Forget();
    }
}
