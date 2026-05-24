#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Core.Services
{
    /// <summary>
    /// Abstraktion ueber den Auth-Provider (Firebase, Google Play Games).
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
    }
}
