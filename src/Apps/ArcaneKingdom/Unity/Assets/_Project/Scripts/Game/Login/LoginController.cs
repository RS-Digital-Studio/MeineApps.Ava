#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Login;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Login
{
    /// <summary>
    /// Login-Flow: Auth -&gt; SaveLoad -&gt; Validate. Wird vom <c>LoginScreen</c> aufgerufen,
    /// nachdem das UI-Foundation steht. Vorher: war IAsyncStartable und lief auto —
    /// jetzt UI-gesteuert, weil der LoginScreen Status-Updates braucht.
    /// </summary>
    public sealed class LoginController
    {
        private readonly IAuthService _auth;
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;

        public LoginController(IAuthService auth,
                               ISaveService<PlayerSave> save,
                               IAnalyticsService analytics)
        {
            _auth = auth;
            _save = save;
            _analytics = analytics;
        }

        /// <summary>Letzter Save-Stand nach erfolgreichem Login (null wenn noch kein Erfolg).</summary>
        public PlayerSave? LoadedSave { get; private set; }

        /// <summary>
        /// Fuehrt den kompletten Login-Flow aus und meldet Stufen über <paramref name="progress"/>.
        /// </summary>
        /// <returns>Success bei abgeschlossenem Login, Failure mit Fehlermeldung bei Abbruch.</returns>
        public async UniTask<Result> RunLoginAsync(IProgress<LoginStage>? progress,
                                                   CancellationToken ct = default)
        {
            _analytics.Track("login_started");
            GameLogger.Info("Login", "Auto-SignIn (anonym)...");

            try
            {
                progress?.Report(LoginStage.Authenticating);
                var auth = await _auth.SignInAnonymouslyAsync(ct);
                if (!auth.IsSuccess)
                {
                    GameLogger.Error("Login", $"Auth fehlgeschlagen: {auth.ErrorMessage}");
                    _analytics.Track("login_failed", new Dictionary<string, object>
                    {
                        ["reason"] = auth.ErrorMessage ?? "unknown"
                    });
                    progress?.Report(LoginStage.Failed);
                    return Result.Failure(auth.ErrorMessage ?? "Auth failed");
                }
                _analytics.SetUserId(_auth.CurrentUserId ?? "anonymous");

                progress?.Report(LoginStage.LoadingSave);
                var saveResult = await _save.LoadAsync(ct);
                if (!saveResult.IsSuccess)
                {
                    GameLogger.Error("Login", $"Save-Load fehlgeschlagen: {saveResult.ErrorMessage}");
                    progress?.Report(LoginStage.Failed);
                    return Result.Failure(saveResult.ErrorMessage ?? "Save load failed");
                }

                progress?.Report(LoginStage.Validating);
                LoadedSave = saveResult.Value!;

                _analytics.Track("login_success", new Dictionary<string, object>
                {
                    ["player_level"] = LoadedSave.Profile.Level
                });

                progress?.Report(LoginStage.Ready);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                progress?.Report(LoginStage.Failed);
                return Result.Failure("Login abgebrochen");
            }
            catch (Exception ex)
            {
                GameLogger.Error("Login", $"Unerwarteter Fehler: {ex.Message}");
                _analytics.Track("login_crash", new Dictionary<string, object>
                {
                    ["exception"] = ex.GetType().Name,
                    ["message"] = ex.Message
                });
                progress?.Report(LoginStage.Failed);
                return Result.Failure(ex.Message);
            }
        }
    }
}
