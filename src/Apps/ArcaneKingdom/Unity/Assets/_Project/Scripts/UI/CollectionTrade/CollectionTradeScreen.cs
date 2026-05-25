#nullable enable
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Collection;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Collection;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.CollectionTrade
{
    /// <summary>
    /// Sammlung-Tausch-Screen (Spielplan v5 Kap. 5.6 + Impl_KOMPLETT Kap. 7.3).
    /// Spieler sammelt 4 Material-Karten (NUM 1/1), bekommt die Belohnungs-Karte.
    /// Kategorien links (White Heart 3/4, Dark Heart 3/6, etc.), Materialien rechts.
    /// </summary>
    public sealed class CollectionTradeScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly CollectionService _collectionService;
        private readonly ToastService _toast;
        private readonly ILocalizationService _loc;

        private VisualElement _setsList = null!;
        private VisualElement _selectedDetails = null!;
        private Button _closeBtn = null!;

        public override string Id => "collection-trade";
        protected override string UxmlPath => "UI/CollectionTradeScreen";

        public CollectionTradeScreen(ScreenManager screenManager,
                                      ISaveService<PlayerSave> save,
                                      CollectionService collectionService,
                                      ToastService toast,
                                      ILocalizationService loc)
        {
            _screenManager = screenManager;
            _save = save;
            _collectionService = collectionService;
            _toast = toast;
            _loc = loc;
        }

        protected override void BindElements(VisualElement root)
        {
            _setsList         = Q<VisualElement>("collection-sets");
            _selectedDetails  = Q<VisualElement>("collection-details");
            _closeBtn         = Q<Button>("collection-close");
            _closeBtn.clicked += () => _screenManager.PopAsync().Forget();
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            await RefreshAsync(ct);
        }

        private async UniTask RefreshAsync(CancellationToken ct)
        {
            _setsList.Clear();
            _selectedDetails.Clear();

            var sets = _collectionService.AllSets;
            if (sets == null || sets.Count == 0)
            {
                _setsList.Add(new Label("Keine Sammlungen verfuegbar."));
                return;
            }

            var progressList = await _collectionService.EvaluateAllAsync(ct);
            foreach (var progress in progressList)
            {
                _setsList.Add(BuildSetRow(progress));
            }
        }

        private VisualElement BuildSetRow(CollectionProgress progress)
        {
            var set = progress.Set;
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 12; row.style.paddingRight = 12;
            row.style.paddingTop = 10; row.style.paddingBottom = 10;
            row.style.marginBottom = 6;
            row.style.backgroundColor = progress.IsComplete
                ? new StyleColor(new UnityEngine.Color(0.0f, 0.4f, 0.2f, 0.4f))
                : new StyleColor(new UnityEngine.Color(0.10f, 0.10f, 0.18f));
            row.style.borderTopLeftRadius = 8; row.style.borderTopRightRadius = 8;
            row.style.borderBottomLeftRadius = 8; row.style.borderBottomRightRadius = 8;

            row.Add(new Label(_loc.Get(set.DisplayNameKey, set.Id)) { style = { flexGrow = 1, fontSize = 14, color = new StyleColor(UnityEngine.Color.white), unityFontStyleAndWeight = UnityEngine.FontStyle.Bold } });
            row.Add(new Label($"{progress.OwnedCount}/{progress.TotalCount}") { style = { width = 60, color = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f)), unityTextAlign = UnityEngine.TextAnchor.MiddleRight } });

            if (progress.IsComplete)
            {
                var exchangeBtn = new Button(() => ExchangeAsync(set.Id).Forget()) { text = "Tauschen" };
                exchangeBtn.style.width = 100; exchangeBtn.style.height = 36; exchangeBtn.style.marginLeft = 12;
                exchangeBtn.style.backgroundColor = new StyleColor(new UnityEngine.Color(1.0f, 0.48f, 0.0f));
                exchangeBtn.style.color = new StyleColor(UnityEngine.Color.white);
                row.Add(exchangeBtn);
            }
            return row;
        }

        private async UniTask ExchangeAsync(string setId)
        {
            var result = await _collectionService.ExchangeAsync(setId);
            if (!result.IsSuccess)
            {
                _toast.Show(result.ErrorMessage ?? "Tausch fehlgeschlagen", ToastKind.Danger);
                return;
            }
            _toast.Show($"Belohnung erhalten: {result.Value}!", ToastKind.Success, 4f);
            await RefreshAsync(default);
        }
    }
}
