#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Services
{
    /// <summary>
    /// Firebase-Auth-Implementierung. Aktuell Stub — Firebase Unity SDK ist noch nicht
    /// im manifest.json. Wird in der MVP-Phase ergaenzt durch <c>com.google.firebase.auth</c>.
    /// </summary>
    public sealed class FirebaseAuthService : IAuthService
    {
        public bool IsAuthenticated { get; private set; }
        public string? CurrentUserId { get; private set; }
        public string? CurrentUserDisplayName { get; private set; }

        public async UniTask<Result> SignInAnonymouslyAsync(CancellationToken ct = default)
        {
            GameLogger.Warning("FirebaseAuth", "SignInAnonymouslyAsync — STUB. Firebase Unity SDK fehlt.");
            await UniTask.Yield(ct);
            CurrentUserId = $"stub-{Guid.NewGuid():N}";
            CurrentUserDisplayName = "Gast";
            IsAuthenticated = true;
            return Result.Success();
        }

        public UniTask<Result> SignInWithGooglePlayAsync(CancellationToken ct = default)
        {
            GameLogger.Warning("FirebaseAuth", "SignInWithGooglePlayAsync — STUB.");
            return UniTask.FromResult(Result.Failure("Firebase Unity SDK noch nicht installiert."));
        }

        public UniTask<Result> LinkWithEmailAsync(string email, string password, CancellationToken ct = default)
        {
            GameLogger.Warning("FirebaseAuth", "LinkWithEmailAsync — STUB.");
            return UniTask.FromResult(Result.Failure("Firebase Unity SDK noch nicht installiert."));
        }

        public UniTask SignOutAsync()
        {
            IsAuthenticated = false;
            CurrentUserId = null;
            CurrentUserDisplayName = null;
            return UniTask.CompletedTask;
        }
    }
}
