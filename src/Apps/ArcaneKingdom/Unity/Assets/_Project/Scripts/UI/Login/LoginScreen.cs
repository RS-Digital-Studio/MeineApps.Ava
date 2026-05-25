#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Login;
using ArcaneKingdom.Game.Login;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Login
{
    /// <summary>
    /// Erste sichtbare Seite nach App-Start. Zeigt Logo + Spieltitel + Login-Progress.
    /// Bei Erfolg: <c>ScreenManager.ReplaceAsync(Hub)</c>. Bei Fehler: Retry-Button.
    /// </summary>
    public sealed class LoginScreen : ScreenBase
    {
        private readonly LoginController _login;
        private readonly ScreenManager _screenManager;
        private readonly ToastService _toast;

        private VisualElement _progressFill = null!;
        private Label _statusText = null!;
        private Button _retryButton = null!;
        private Label _versionLabel = null!;

        public override string Id => ScreenId.Login;
        protected override string UxmlPath => "UI/LoginScreen";

        public LoginScreen(LoginController login, ScreenManager screenManager, ToastService toast)
        {
            _login = login;
            _screenManager = screenManager;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _progressFill = Q<VisualElement>("login-progress-fill");
            _statusText   = Q<Label>("login-status-text");
            _retryButton  = Q<Button>("login-retry-button");
            _versionLabel = Q<Label>("login-version");

            _retryButton.clicked += OnRetryClicked;
            _versionLabel.text = $"v{UnityEngine.Application.version}";
        }

        public override UniTask OnEnterAsync(CancellationToken ct)
        {
            // WICHTIG: detached starten (.Forget) statt awaiten — RunLoginAsync ruft am
            // Ende ScreenManager.ReplaceAsync(Hub) auf, was waehrend laufender Push-
            // Transaktion zum busy-Deadlock fuehren wuerde. So kann OnEnterAsync sofort
            // returnen, ScreenManager.busy wird freigegeben, Hub-Replace klappt.
            RunLoginAsync(ct).Forget();
            return UniTask.CompletedTask;
        }

        private async UniTask RunLoginAsync(CancellationToken ct)
        {
            _retryButton.AddToClassList("ak-hidden");
            ResetProgress();

            var progress = new SyncProgress<LoginStage>(OnStageChanged);
            var result = await _login.RunLoginAsync(progress, ct);

            if (result.IsSuccess)
            {
                // Kleine Pause damit der User "Bereit"-Status sieht
                await UniTask.Delay(System.TimeSpan.FromMilliseconds(400), cancellationToken: ct);

                if (_screenManager.IsRegistered(ScreenId.Hub))
                {
                    await _screenManager.ReplaceAsync(ScreenId.Hub, ct);
                }
                else
                {
                    // Stufe 3 noch nicht implementiert — Login zeigt nur Erfolgs-Toast.
                    _statusText.text = "Login erfolgreich (Hub-Screen folgt in Stufe 3)";
                    _toast.Show("Login erfolgreich!", ToastKind.Success, 4f);
                }
            }
            else
            {
                _statusText.text = $"Fehler: {result.ErrorMessage}";
                _toast.Show(result.ErrorMessage ?? "Login fehlgeschlagen", ToastKind.Danger, 5f);
                _retryButton.RemoveFromClassList("ak-hidden");
            }
        }

        private void OnStageChanged(LoginStage stage)
        {
            var (text, percent) = stage switch
            {
                LoginStage.Idle           => ("Bereit", 0f),
                LoginStage.Authenticating => ("Verbinde mit Server…", 20f),
                LoginStage.LoadingSave    => ("Lade Spielerdaten…", 55f),
                LoginStage.Validating     => ("Validiere…", 80f),
                LoginStage.Ready          => ("Bereit!", 100f),
                LoginStage.Failed         => ("Verbindung fehlgeschlagen", 0f),
                _ => (stage.ToString(), 0f)
            };

            _statusText.text = text;
            _progressFill.style.width = new Length(percent, LengthUnit.Percent);

            GameLogger.Verbose("Login", $"Stage -> {stage} ({percent}%)");
        }

        private void ResetProgress()
        {
            _progressFill.style.width = new Length(0, LengthUnit.Percent);
            _statusText.text = "Verbinde mit Server…";
        }

        private void OnRetryClicked()
        {
            // Neuer Versuch mit frischem CancellationToken
            RunLoginAsync(default).Forget();
        }

        /// <summary>
        /// Hilfsklasse — IProgress&lt;T&gt;-Wrapper der Reports synchron auf dem
        /// aufrufenden Thread durchreicht (LoginController laeuft via UniTask
        /// im PlayerLoopUpdate, das ist der UI-Thread).
        /// </summary>
        private sealed class SyncProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;
            public SyncProgress(Action<T> handler) => _handler = handler;
            public void Report(T value) => _handler(value);
        }
    }
}
