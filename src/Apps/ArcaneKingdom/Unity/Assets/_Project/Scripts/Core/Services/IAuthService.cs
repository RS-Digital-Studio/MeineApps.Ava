#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Core.Services
{
    /// <summary>
    /// Abstraktion über den Auth-Provider (Firebase, Google Play Games).
    /// </summary>
    public interface IAuthService
    {
        bool IsAuthenticated { get; }
        string? CurrentUserId { get; }
        string? CurrentUserDisplayName { get; }

        UniTask<Result> SignInAnonymouslyAsync(CancellationToken ct = default);
        UniTask<Result> SignInWithGooglePlayAsync(CancellationToken ct = default);
        UniTask<Result> LinkWithEmailAsync(string email, string password, CancellationToken ct = default);
        UniTask SignOutAsync();

        /// <summary>
        /// Registriert einen neuen Account mit E-Mail/Passwort (Spielplan v5 Kap. 2).
        /// Bei Erfolg: Token im Result.Value zurueckgeben fuer lokale Speicherung.
        /// </summary>
        UniTask<Result<string>> RegisterAsync(string email, string password, string displayName, CancellationToken ct = default);

        /// <summary>
        /// Klassischer Login mit E-Mail/Passwort (im Gegensatz zum SignInAnonymously).
        /// </summary>
        UniTask<Result<string>> SignInWithEmailAsync(string email, string password, CancellationToken ct = default);

        /// <summary>Setzt den lokalen DisplayName (z.B. nach Namens-Setup im Onboarding).</summary>
        void UpdateDisplayName(string name);
    }
}
