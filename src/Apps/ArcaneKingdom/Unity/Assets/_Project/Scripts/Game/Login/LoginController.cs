#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace ArcaneKingdom.Game.Login
{
    /// <summary>
    /// Login-Flow: Auth -> SaveLoad -> Hub-Wechsel. Wird vom BootEntryPoint angesteuert,
    /// sobald die Boot-Scene den DI-Container gebaut hat.
    /// </summary>
    public sealed class LoginController : IAsyncStartable, IDisposable
    {
        private readonly IAuthService _auth;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ISceneLoaderService _sceneLoader;
        private readonly IAnalyticsService _analytics;
        private readonly CancellationTokenSource _cts = new();

        public LoginController(IAuthService auth, ISaveService<PlayerSave> save,
                               ISceneLoaderService sceneLoader, IAnalyticsService analytics)
        {
            _auth = auth;
            _save = save;
            _sceneLoader = sceneLoader;
            _analytics = analytics;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _cts.Token);
            var ct = linked.Token;

            _analytics.Track("login_started");
            GameLogger.Info("Login", "Auto-SignIn (anonym)...");

            var auth = await _auth.SignInAnonymouslyAsync(ct);
            if (!auth.IsSuccess)
            {
                GameLogger.Error("Login", $"Auth fehlgeschlagen: {auth.ErrorMessage}");
                _analytics.Track("login_failed", new System.Collections.Generic.Dictionary<string, object> { ["reason"] = auth.ErrorMessage ?? "unknown" });
                return;
            }
            _analytics.SetUserId(_auth.CurrentUserId ?? "anonymous");

            var saveResult = await _save.LoadAsync(ct);
            if (!saveResult.IsSuccess)
            {
                GameLogger.Error("Login", $"Save-Load fehlgeschlagen: {saveResult.ErrorMessage}");
                return;
            }

            _analytics.Track("login_success", new System.Collections.Generic.Dictionary<string, object>
            {
                ["player_level"] = saveResult.Value!.Profile.Level
            });

            GameLogger.Info("Login", "Lade Hub-Scene...");
            await _sceneLoader.LoadAdditiveAsync(SceneNames.Hub, ct);
        }

        public void Dispose() => _cts.Cancel();
    }
}
