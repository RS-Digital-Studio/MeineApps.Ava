#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Iap
{
    /// <summary>
    /// Unity-IAP-Implementierung (Skelett). Wird in der MVP-Phase mit <c>com.unity.purchasing</c>
    /// verdrahtet — aktuell rein lokale Stub-Logik, die direkt Diamanten gutschreibt.
    /// </summary>
    public sealed class UnityIapService : IIapService
    {
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly List<IapProduct> _products = new();

        public UnityIapService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
            SeedProductCatalog();
        }

        public IReadOnlyList<IapProduct> AvailableProducts => _products;

        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            GameLogger.Warning("IAP", "InitializeAsync — STUB (Unity IAP nicht installiert).");
            await UniTask.Yield(ct);
        }

        public async UniTask<Result<IapReceipt>> BuyAsync(string productId, CancellationToken ct = default)
        {
            IapProduct? product = null;
            foreach (var p in _products) if (p.ProductId == productId) { product = p; break; }
            if (product == null) return Result<IapReceipt>.Failure($"Produkt '{productId}' unbekannt.");

            // TODO: Unity IAP Buy + Server-side Validation via Cloud Function
            await UniTask.Delay(200, cancellationToken: ct);

            var totalDiamonds = product.DiamondAmount + product.DiamondBonus;
            await _save.MutateAsync(save => { save.Currencies.AddDiamond(totalDiamonds); return save; }, ct);

            var receipt = new IapReceipt
            {
                ProductId = product.ProductId,
                TransactionId = Guid.NewGuid().ToString("N"),
                DiamondsGranted = totalDiamonds,
                ServerValidated = false
            };
            _analytics.Track("iap_purchase", new Dictionary<string, object>
            {
                ["product_id"] = product.ProductId,
                ["diamonds"] = totalDiamonds,
                ["validated"] = false
            });
            GameLogger.Info("IAP", $"Kauf {product.ProductId} → +{totalDiamonds} Diamanten (STUB, nicht server-validiert).");
            return Result<IapReceipt>.Success(receipt);
        }

        public async UniTask<Result> RestorePurchasesAsync(CancellationToken ct = default)
        {
            GameLogger.Warning("IAP", "RestorePurchasesAsync — STUB.");
            await UniTask.Yield(ct);
            return Result.Success();
        }

        /// <summary>
        /// Pilot-Produkt-Katalog (DESIGN.md Kap. 17.1).
        /// </summary>
        private void SeedProductCatalog()
        {
            _products.Add(new IapProduct { ProductId = "diamonds_starter", DisplayNameKey = "iap.starter.name", PriceText = "0,99 EUR", DiamondAmount = 60, DiamondBonus = 0 });
            _products.Add(new IapProduct { ProductId = "diamonds_small",   DisplayNameKey = "iap.small.name",   PriceText = "4,99 EUR", DiamondAmount = 300, DiamondBonus = 30 });
            _products.Add(new IapProduct { ProductId = "diamonds_medium",  DisplayNameKey = "iap.medium.name",  PriceText = "14,99 EUR", DiamondAmount = 980, DiamondBonus = 150 });
            _products.Add(new IapProduct { ProductId = "diamonds_large",   DisplayNameKey = "iap.large.name",   PriceText = "29,99 EUR", DiamondAmount = 1980, DiamondBonus = 400 });
            _products.Add(new IapProduct { ProductId = "diamonds_huge",    DisplayNameKey = "iap.huge.name",    PriceText = "49,99 EUR", DiamondAmount = 3280, DiamondBonus = 800 });
            _products.Add(new IapProduct { ProductId = "diamonds_mega",    DisplayNameKey = "iap.mega.name",    PriceText = "99,99 EUR", DiamondAmount = 6480, DiamondBonus = 2000 });
        }
    }
}
