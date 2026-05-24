#nullable enable
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Iap
{
    /// <summary>
    /// Abstraktion ueber Unity IAP. Frontend ruft <see cref="BuyAsync"/> auf, die Implementierung
    /// loest Google Play Billing aus und liefert nach Validation (Server-seitig Cloud Function)
    /// die Diamanten-/Pack-Belohnung als Result.
    /// </summary>
    public interface IIapService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        IReadOnlyList<IapProduct> AvailableProducts { get; }
        UniTask<Result<IapReceipt>> BuyAsync(string productId, CancellationToken ct = default);
        UniTask<Result> RestorePurchasesAsync(CancellationToken ct = default);
    }

    public sealed class IapProduct
    {
        public string ProductId { get; init; } = string.Empty;
        public string DisplayNameKey { get; init; } = string.Empty;
        public string PriceText { get; init; } = string.Empty;      // z.B. "0,99 EUR"
        public long DiamondAmount { get; init; }
        public long DiamondBonus { get; init; }
    }

    public sealed class IapReceipt
    {
        public string ProductId { get; init; } = string.Empty;
        public string TransactionId { get; init; } = string.Empty;
        public long DiamondsGranted { get; init; }
        public bool ServerValidated { get; init; }
    }
}
