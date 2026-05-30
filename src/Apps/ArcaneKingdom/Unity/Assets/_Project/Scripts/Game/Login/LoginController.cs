#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Login;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Quest;
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
        private readonly QuestService _questService;

        public LoginController(IAuthService auth,
                               ISaveService<PlayerSave> save,
                               IAnalyticsService analytics,
                               QuestService questService)
        {
            _auth = auth;
            _save = save;
            _analytics = analytics;
            _questService = questService;
        }

        /// <summary>Letzter Save-Stand nach erfolgreichem Login (null wenn noch kein Erfolg).</summary>
        public PlayerSave? LoadedSave { get; private set; }

        /// <summary>
        /// Fuehrt den kompletten Login-Flow aus und meldet Stufen über <paramref name="progress"/>.
        /// Optionale email/password werden fuer SignInWithEmail verwendet (Spielplan v5 Kap. 2.3),
        /// sonst Auto-SignIn (anonym) als Fallback fuer Pre-MVP-Tests.
        /// </summary>
        /// <returns>Success bei abgeschlossenem Login, Failure mit Fehlermeldung bei Abbruch.</returns>
        public async UniTask<Result> RunLoginAsync(IProgress<LoginStage>? progress,
                                                   CancellationToken ct = default,
                                                   string? email = null,
                                                   string? password = null)
        {
            _analytics.Track("login_started");
            var useEmail = !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);
            GameLogger.Info("Login", useEmail ? $"SignIn via E-Mail ({email})..." : "Auto-SignIn (anonym)...");

            try
            {
                progress?.Report(LoginStage.Authenticating);

                bool authOk;
                string? authError = null;
                if (useEmail)
                {
                    var emailAuth = await _auth.SignInWithEmailAsync(email!, password!, ct);
                    authOk = emailAuth.IsSuccess;
                    authError = emailAuth.ErrorMessage;
                    if (authOk)
                    {
                        // Token persistieren fuer Auto-Login beim naechsten Start
                        UnityEngine.PlayerPrefs.SetString("last_user_email", email!);
                        if (!string.IsNullOrEmpty(emailAuth.Value))
                            UnityEngine.PlayerPrefs.SetString("auth_token", emailAuth.Value);
                        UnityEngine.PlayerPrefs.Save();
                    }
                }
                else
                {
                    var anonAuth = await _auth.SignInAnonymouslyAsync(ct);
                    authOk = anonAuth.IsSuccess;
                    authError = anonAuth.ErrorMessage;
                }

                if (!authOk)
                {
                    GameLogger.Error("Login", $"Auth fehlgeschlagen: {authError}");
                    _analytics.Track("login_failed", new Dictionary<string, object>
                    {
                        ["reason"] = authError ?? "unknown",
                        ["mode"]   = useEmail ? "email" : "anonymous"
                    });
                    progress?.Report(LoginStage.Failed);
                    return Result.Failure(authError ?? "Auth failed");
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

                // H12: Quest-Fortschritt direkt nach Save-Load wiederherstellen (idempotent).
                // Frueheste Stelle vor dem ersten Quest-Advance (Battle etc.).
                _questService.RestoreFromSave(LoadedSave);

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
