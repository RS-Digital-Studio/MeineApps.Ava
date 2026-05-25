#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Splash
{
    /// <summary>
    /// Splash-Screen (Spielplan v5 Kap. 2 + Login-Hub-Plan Kap. 3).
    /// Erster sichtbarer Screen nach App-Start. Logo + Ladebalken + Versions-Label.
    /// Pruefte parallel im Hintergrund den Auto-Login-Status und entscheidet
    /// danach zwischen Login, Registration oder Hub.
    ///
    /// Minimum-Splash-Zeit 3 Sekunden auch wenn Laden schneller fertig ist
    /// (gutes Markenerlebnis, Login-Hub-Plan Kap. 3.4).
    /// </summary>
    public sealed class SplashScreen : ScreenBase
    {
        private const int MinSplashMillis = 3000;

        private readonly ScreenManager _screenManager;
        private readonly ILocalizationService _loc;

        private VisualElement _logo = null!;
        private VisualElement _progressFill = null!;
        private Label _statusText = null!;
        private Label _versionLabel = null!;

        public override string Id => ScreenId.Splash;
        protected override string UxmlPath => "UI/SplashScreen";

        public SplashScreen(ScreenManager screenManager, ILocalizationService loc)
        {
            _screenManager = screenManager;
            _loc = loc;
        }

        protected override void BindElements(VisualElement root)
        {
            _logo         = Q<VisualElement>("splash-logo");
            _progressFill = Q<VisualElement>("splash-progress-fill");
            _statusText   = Q<Label>("splash-status-text");
            _versionLabel = Q<Label>("splash-version");

            _versionLabel.text = $"v{Application.version}";
            SetProgress(0f, _loc.Get("splash.status.preparing", "Vorbereitung..."));
        }

        public override UniTask OnEnterAsync(CancellationToken ct)
        {
            PulseLogoAsync(ct).Forget();
            RunSplashFlowAsync(ct).Forget();
            return UniTask.CompletedTask;
        }

        private async UniTask PulseLogoAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _logo?.panel != null)
            {
                _logo.AddToClassList("ak-logo--pulse");
                await UniTask.Delay(1000, cancellationToken: ct).SuppressCancellationThrow();
                if (ct.IsCancellationRequested || _logo?.panel == null) return;
                _logo.RemoveFromClassList("ak-logo--pulse");
                await UniTask.Delay(1000, cancellationToken: ct).SuppressCancellationThrow();
            }
        }

        private async UniTask RunSplashFlowAsync(CancellationToken ct)
        {
            var startedAtUtc = DateTime.UtcNow;

            // Phase 1: Logo-Animation + Asset-Preload (Pseudo, ~25%)
            SetProgress(0.10f, _loc.Get("splash.status.loadingAssets", "Lade Karten-Daten..."));
            await UniTask.Delay(400, cancellationToken: ct).SuppressCancellationThrow();
            SetProgress(0.25f, _loc.Get("splash.status.loadingAssets", "Lade Karten-Daten..."));

            // Phase 2: Lokalen Account pruefen (direkt PlayerPrefs — kein eigener Service noetig)
            SetProgress(0.40f, _loc.Get("splash.status.checkingAccount", "Pruefe Account..."));
            var hasEmail = !string.IsNullOrEmpty(PlayerPrefs.GetString("last_user_email", string.Empty));
            var hasToken = !string.IsNullOrEmpty(PlayerPrefs.GetString("auth_token", string.Empty));
            await UniTask.Delay(300, cancellationToken: ct).SuppressCancellationThrow();

            // Phase 3: Token-Pruefung
            SetProgress(0.65f, _loc.Get("splash.status.validatingToken", "Verbinde mit Server..."));
            await UniTask.Delay(400, cancellationToken: ct).SuppressCancellationThrow();
            SetProgress(0.85f, _loc.Get("splash.status.validatingToken", "Verbinde mit Server..."));

            // Phase 4: Minimum-Splash-Zeit garantieren
            var elapsed = (int)(DateTime.UtcNow - startedAtUtc).TotalMilliseconds;
            var remaining = MinSplashMillis - elapsed;
            if (remaining > 0)
                await UniTask.Delay(remaining, cancellationToken: ct).SuppressCancellationThrow();

            SetProgress(1.0f, _loc.Get("splash.status.ready", "Bereit"));
            await UniTask.Delay(300, cancellationToken: ct).SuppressCancellationThrow();

            if (ct.IsCancellationRequested) return;

            // Phase 5: Naechsten Screen entscheiden
            if (!hasEmail || !hasToken)
            {
                // Erster Start oder Logout: Registration anzeigen
                if (_screenManager.IsRegistered(ScreenId.Registration))
                    await _screenManager.ReplaceAsync(ScreenId.Registration, ct);
                else
                    await _screenManager.ReplaceAsync(ScreenId.Login, ct);
            }
            else
            {
                // Bekannter Nutzer mit gespeichertem Token: Login-Screen probiert Auto-Login
                await _screenManager.ReplaceAsync(ScreenId.Login, ct);
            }
        }

        private void SetProgress(float percent, string text)
        {
            _progressFill.style.width = new Length(percent * 100f, LengthUnit.Percent);
            _statusText.text = text;
            GameLogger.Verbose("Splash", $"{(int)(percent * 100)}% {text}");
        }
    }
}
