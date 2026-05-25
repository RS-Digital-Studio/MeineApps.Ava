#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ArcaneKingdom.Game.Services
{
    /// <summary>
    /// Auth-Service mit lokaler Identity-Persistierung. Bereitet sich auf Firebase-Auth-
    /// Anbindung vor (siehe SETUP.md "Firebase-Integration").
    ///
    /// Was funktioniert:
    ///   - Anonymous-Login mit STABILER User-ID (PlayerPrefs-persistiert)
    ///   - DisplayName aus PlayerPrefs (default "Gast")
    ///   - SignOut loescht die Prefs nicht (User-Identity bleibt fuer Re-Login)
    ///
    /// Was als Firebase-Switch zu ergaenzen ist:
    ///   - SignInAnonymouslyAsync: ersetzen durch
    ///       Firebase.Auth.FirebaseAuth.DefaultInstance.SignInAnonymouslyAsync()
    ///   - SignInWithGooglePlayAsync: ersetzen durch
    ///       PlayGamesPlatform-Activate + GoogleAuthProvider.GetCredential + SignInWithCredentialAsync
    ///   - LinkWithEmailAsync: EmailAuthProvider + CurrentUser.LinkWithCredentialAsync
    /// </summary>
    public sealed class FirebaseAuthService : IAuthService
    {
        private const string UserIdPrefsKey = "ak.user_id";
        private const string DisplayNamePrefsKey = "ak.display_name";

        public bool IsAuthenticated { get; private set; }
        public string? CurrentUserId { get; private set; }
        public string? CurrentUserDisplayName { get; private set; }

        public async UniTask<Result> SignInAnonymouslyAsync(CancellationToken ct = default)
        {
            await UniTask.Yield(ct);

            // TODO Firebase: ersetzen durch
            //   var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            //   var result = await auth.SignInAnonymouslyAsync();
            //   CurrentUserId = result.User.UserId;

            // Lokaler Fallback: stabile UserId in PlayerPrefs persistieren
            CurrentUserId = PlayerPrefs.GetString(UserIdPrefsKey, "");
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                CurrentUserId = $"local-{Guid.NewGuid():N}";
                PlayerPrefs.SetString(UserIdPrefsKey, CurrentUserId);
                PlayerPrefs.Save();
                GameLogger.Info("Auth", $"Neue lokale UserId generiert: {CurrentUserId}");
            }
            else
            {
                GameLogger.Info("Auth", $"Lokale UserId wiederhergestellt: {CurrentUserId}");
            }

            CurrentUserDisplayName = PlayerPrefs.GetString(DisplayNamePrefsKey, "Gast");
            IsAuthenticated = true;
            return Result.Success();
        }

        public UniTask<Result> SignInWithGooglePlayAsync(CancellationToken ct = default)
        {
            GameLogger.Warning("Auth", "SignInWithGooglePlayAsync — erfordert Firebase + Play Games SDK.");
            return UniTask.FromResult(Result.Failure("Firebase Unity SDK + Play Games noch nicht installiert."));
        }

        public UniTask<Result> LinkWithEmailAsync(string email, string password, CancellationToken ct = default)
        {
            GameLogger.Warning("Auth", "LinkWithEmailAsync — erfordert Firebase Auth.");
            return UniTask.FromResult(Result.Failure("Firebase Unity SDK noch nicht installiert."));
        }

        public UniTask SignOutAsync()
        {
            IsAuthenticated = false;
            CurrentUserId = null;
            CurrentUserDisplayName = null;
            // Bewusst: PlayerPrefs bleiben, damit Re-Login dieselbe Identity hat.
            return UniTask.CompletedTask;
        }

        /// <summary>Setzt den DisplayName (z.B. nach erstmaligem Namens-Setup).</summary>
        public void UpdateDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            CurrentUserDisplayName = name.Trim();
            PlayerPrefs.SetString(DisplayNamePrefsKey, CurrentUserDisplayName);
            PlayerPrefs.Save();
        }
    }
}
