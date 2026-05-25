#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Thief;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Thief
{
    /// <summary>
    /// Dieb-Screen (Spielplan v5 Kap. 10 + Impl_KOMPLETT Kap. 10).
    /// Server-weiter Dieb-Event mit gemeinsamem HP-Balken, Timer, Angriff-Button.
    /// </summary>
    public sealed class ThiefScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ThiefService _thiefService;
        private readonly ToastService _toast;

        private Label _thiefName = null!;
        private Label _thiefLevel = null!;
        private Label _hpText = null!;
        private VisualElement _hpFill = null!;
        private Label _timerLabel = null!;
        private Label _discoveredBy = null!;
        private Label _lastAttacker = null!;
        private Label _attackCount = null!;
        private Button _attackBtn = null!;
        private Button _refreshBtn = null!;
        private Button _closeBtn = null!;

        private ActiveThief? _activeThief;

        public override string Id => ScreenId.ThiefScreen;
        protected override string UxmlPath => "UI/ThiefScreen";

        public ThiefScreen(ScreenManager screenManager, ThiefService thiefService, ToastService toast)
        {
            _screenManager = screenManager;
            _thiefService = thiefService;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _thiefName    = Q<Label>("thief-name");
            _thiefLevel   = Q<Label>("thief-level");
            _hpText       = Q<Label>("thief-hp-text");
            _hpFill       = Q<VisualElement>("thief-hp-fill");
            _timerLabel   = Q<Label>("thief-timer");
            _discoveredBy = Q<Label>("thief-discovered-by");
            _lastAttacker = Q<Label>("thief-last-attacker");
            _attackCount  = Q<Label>("thief-attack-count");
            _attackBtn    = Q<Button>("thief-attack");
            _refreshBtn   = Q<Button>("thief-refresh");
            _closeBtn     = Q<Button>("thief-close");

            _closeBtn.clicked   += () => _screenManager.PopAsync().Forget();
            _attackBtn.clicked  += OnAttackClicked;
            _refreshBtn.clicked += RefreshUi;
        }

        public override UniTask OnEnterAsync(CancellationToken ct)
        {
            // Mock-Dieb fuer Test-Modus — Server-Modus liefert echten Dieb via NetworkService
            _activeThief = _thiefService.SpawnMockThief(ThiefType.Elite, 58, "ServerEvent", TimeSpan.FromMinutes(120));
            RefreshUi();
            UpdateTimerLoopAsync(ct).Forget();
            return UniTask.CompletedTask;
        }

        private async UniTask UpdateTimerLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _activeThief != null && _activeThief.IsAlive)
            {
                var remaining = _activeThief.FleesAtUtc - DateTime.UtcNow;
                if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                _timerLabel.text = $"Flueсhtet in {remaining:hh\\:mm\\:ss}";
                await UniTask.Delay(1000, cancellationToken: ct).SuppressCancellationThrow();
            }
        }

        private void OnAttackClicked()
        {
            if (_activeThief == null) return;
            // Damage = vom Spieler-Deck abhaengig — Mock: 1000-3000
            var damage = UnityEngine.Random.Range(1000, 3000);
            _activeThief.ApplyDamage("local-player", damage);
            _toast.Show($"{damage:N0} Schaden!", ToastKind.Success, 1.5f);
            RefreshUi();

            if (!_activeThief.IsAlive)
            {
                var reward = _thiefService.ComputeReward(_activeThief, "local-player");
                _toast.Show($"Dieb besiegt! Belohnung: {reward.Gold:N0} Gold, {reward.Diamonds} Diamanten", ToastKind.Success, 5f);
            }
        }

        private void RefreshUi()
        {
            if (_activeThief == null) return;
            _thiefName.text = $"{_activeThief.Type}-Dieb";
            _thiefLevel.text = $"LV {_activeThief.Level}";
            _hpText.text = $"{_activeThief.CurrentHealth:N0} / {_activeThief.MaxHealth:N0} HP";
            _hpFill.style.width = new Length(_activeThief.HealthPercent * 100f, LengthUnit.Percent);
            _discoveredBy.text = $"Entdeckt von: {_activeThief.DiscoveredByPlayerId}";
            _lastAttacker.text = _activeThief.Attacks.Count > 0
                ? $"Letzte Attacke: {_activeThief.Attacks[_activeThief.Attacks.Count - 1].PlayerId}"
                : "Letzte Attacke: —";
            _attackCount.text = $"{_activeThief.Attacks.Count} Kaempfe wurden gefuehrt";
            _attackBtn.SetEnabled(_activeThief.IsAlive);
        }
    }
}
