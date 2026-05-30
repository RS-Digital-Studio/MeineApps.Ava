#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.World;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.Game.World;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Modals
{
    /// <summary>
    /// Modal zur Bestaetigung eines Welt-Prestige-Upgrades (Designplan v4 Oeko Kap. 6).
    /// Zeigt:
    ///   - Aktuelle Stufe + Naechste Stufe
    ///   - Gold-Kosten + Sterne-Reset-Warnung
    ///   - Neue Stat-Multiplier (Gegner/Drops/Daily-Income)
    ///   - Exklusive Prestige-IV-Karte bei Stufe IV
    /// </summary>
    public sealed class PrestigeUpgradeModal : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly PrestigeService _domain;
        private readonly PrestigeAppService _app;
        private readonly WorldCatalogService _worldCatalog;
        private readonly CardCatalogService _cardCatalog;
        private readonly ILocalizationService _loc;
        private readonly ToastService _toast;

        private Label _worldName = null!;
        private Label _currentStufe = null!;
        private Label _nextStufe = null!;
        private Label _goldCost = null!;
        private Label _enemyMultiplier = null!;
        private Label _dropMultiplier = null!;
        private Label _dailyIncomeMultiplier = null!;
        private Label _bossPhases = null!;
        private Label _exclusiveCard = null!;
        private Label _warning = null!;
        private Button _confirmButton = null!;
        private Button _cancelButton = null!;

        private readonly PrestigeUpgradeContext _ctx;

        public override string Id => ScreenId.PrestigeUpgradeOverlay;
        public override bool IsOverlay => true;
        protected override string UxmlPath => "UI/Modals/PrestigeUpgradeModal";

        public PrestigeUpgradeModal(
            ScreenManager screenManager,
            ISaveService<PlayerSave> save,
            PrestigeService domain,
            PrestigeAppService app,
            WorldCatalogService worldCatalog,
            CardCatalogService cardCatalog,
            ILocalizationService loc,
            ToastService toast,
            PrestigeUpgradeContext ctx)
        {
            _screenManager = screenManager;
            _save = save;
            _domain = domain;
            _app = app;
            _worldCatalog = worldCatalog;
            _cardCatalog = cardCatalog;
            _loc = loc;
            _toast = toast;
            _ctx = ctx;
        }

        protected override void BindElements(VisualElement root)
        {
            _worldName = Q<Label>("world-name");
            _currentStufe = Q<Label>("current-stufe");
            _nextStufe = Q<Label>("next-stufe");
            _goldCost = Q<Label>("gold-cost");
            _enemyMultiplier = Q<Label>("enemy-multiplier");
            _dropMultiplier = Q<Label>("drop-multiplier");
            _dailyIncomeMultiplier = Q<Label>("daily-income-multiplier");
            _bossPhases = Q<Label>("boss-phases");
            _exclusiveCard = Q<Label>("exclusive-card");
            _warning = Q<Label>("warning");
            _confirmButton = Q<Button>("confirm-button");
            _cancelButton = Q<Button>("cancel-button");

            _confirmButton.clicked += OnConfirmClicked;
            _cancelButton.clicked += () => _screenManager.PopAsync().Forget();
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_ctx.TargetWorldId))
            {
                _toast.Show("Keine Welt gewaehlt", ToastKind.Danger);
                _screenManager.PopAsync().Forget();
                return;
            }

            var saveR = await _save.LoadAsync(ct);
            if (!saveR.IsSuccess || saveR.Value == null)
            {
                _toast.Show(saveR.ErrorMessage ?? "Save fehlt", ToastKind.Danger);
                return;
            }
            var save = saveR.Value;
            var world = _worldCatalog.Find(_ctx.TargetWorldId!);
            if (world == null)
            {
                _toast.Show($"Welt '{_ctx.TargetWorldId}' unbekannt", ToastKind.Danger);
                return;
            }

            var currentStufe = save.Prestige.Get(_ctx.TargetWorldId!);
            var nextStufe = _domain.NextStufe(currentStufe);
            var cost = PrestigeStufeBalancing.GetUpgradeGoldCost(currentStufe);

            _worldName.text = _loc.Get(world.DisplayNameKey);
            _currentStufe.text = currentStufe.ToString();
            _nextStufe.text = nextStufe.ToString();
            _goldCost.text = cost > 0 ? $"{cost:N0} Gold" : "MAX";
            _enemyMultiplier.text = $"x{PrestigeStufeBalancing.GetEnemyStatMultiplier(nextStufe):0.00}";
            _dropMultiplier.text = $"x{PrestigeStufeBalancing.GetGoldDropMultiplier(nextStufe):0.00}";
            _dailyIncomeMultiplier.text = $"x{PrestigeStufeBalancing.GetDailyRevenueMultiplier(nextStufe):0.00}";
            _bossPhases.text = $"{_domain.GetBossPhaseCount(nextStufe)} Phasen";

            // Prestige-IV-Karte?
            _exclusiveCard.text = string.Empty;
            if (_domain.UnlocksExclusiveCard(nextStufe) && !string.IsNullOrEmpty(world.Prestige4CardId))
            {
                if (_cardCatalog.TryFind(world.Prestige4CardId, out var def))
                    _exclusiveCard.text = $"{_loc.Get("prestige.unlocks_card") ?? "Schaltet frei"}: {_loc.Get(def.DisplayNameKey)}";
            }

            // Voraussetzungs-Check
            var canUpgrade = _app.CanUpgrade(_ctx.TargetWorldId!, save);
            if (!canUpgrade.IsSuccess)
            {
                _warning.text = canUpgrade.ErrorMessage ?? string.Empty;
                _confirmButton.SetEnabled(false);
            }
            else
            {
                _warning.text = _loc.Get("prestige.stars_reset_warning")
                    ?? "Alle Sterne dieser Welt werden zurueckgesetzt — Belohnungen koennen erneut gefarmt werden.";
                _confirmButton.SetEnabled(true);
            }
        }

        private void OnConfirmClicked() => RunUpgradeAsync().Forget();

        private async UniTaskVoid RunUpgradeAsync()
        {
            if (string.IsNullOrEmpty(_ctx.TargetWorldId)) return;
            _confirmButton.SetEnabled(false);

            var result = await _app.ApplyUpgradeAsync(_ctx.TargetWorldId!);
            if (!result.IsSuccess)
            {
                _toast.Show(result.ErrorMessage ?? "Upgrade fehlgeschlagen", ToastKind.Danger);
                _confirmButton.SetEnabled(true);
                return;
            }

            var outcome = result.Value!;
            var msg = $"{_loc.Get("prestige.upgrade_success") ?? "Aufgewertet"}: {outcome.OldStufe} -> {outcome.NewStufe}";
            if (!string.IsNullOrEmpty(outcome.UnlockedCardId))
            {
                if (_cardCatalog.TryFind(outcome.UnlockedCardId, out var def))
                    msg += $" — {_loc.Get(def.DisplayNameKey)}!";
            }
            _toast.Show(msg, ToastKind.Success);
            await _screenManager.PopAsync();
        }
    }
}
