#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.DailyShop;
using ArcaneKingdom.Domain.Economy;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.DailyShop
{
    /// <summary>
    /// Liefert die aktuelle Tages-Rotation + Kauf-Logik. Lokal optimistisch;
    /// Server-seitig wird per Cloud Function das Inventar abgerechnet.
    /// </summary>
    public sealed class DailyShopService
    {
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;

        public DailyShopService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
        }

        public IReadOnlyList<DailyShopSlot> TodaysSlots() => DailyShopRotation.RotationForDay(DateTime.UtcNow.Date);

        public async UniTask<Result> PurchaseSlotAsync(int slotIndex, CancellationToken ct = default)
        {
            var slots = TodaysSlots();
            if (slotIndex < 0 || slotIndex >= slots.Count) return Result.Failure("Ungueltiger Slot-Index.");
            var slot = slots[slotIndex];

            var success = false;
            string error = string.Empty;
            await _save.MutateAsync(save =>
            {
                if (slot.PriceCurrency == "Diamond" && !save.Currencies.SpendDiamond(slot.PriceAmount))
                {
                    error = "Nicht genug Diamanten."; return save;
                }
                if (slot.PriceCurrency == "Gold" && !save.Currencies.SpendGold(slot.PriceAmount))
                {
                    error = "Nicht genug Gold."; return save;
                }

                switch (slot.Kind)
                {
                    case DailyShopItemKind.Scrap when Enum.TryParse<ScrapType>(slot.SubType, out var st):
                        save.Currencies.AddScraps(st, slot.Quantity);
                        success = true;
                        break;
                    case DailyShopItemKind.Energy:
                        save.Currencies.AddEnergyBonus((int)slot.Quantity);
                        success = true;
                        break;
                    case DailyShopItemKind.Pack:
                        // TODO MVP: Pack als ungeoeffnetes Inventar-Item — fuers Erste sofort rollen
                        save.PendingClaims.Add(new Domain.Save.PendingClaim
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Kind = Domain.Save.PendingClaimKind.Pack,
                            SubType = slot.SubType,
                            Amount = slot.Quantity,
                            SourceKey = "daily_shop",
                            CreatedAtUtc = DateTime.UtcNow
                        });
                        success = true;
                        break;
                    case DailyShopItemKind.Rune:
                    case DailyShopItemKind.Card:
                        save.PendingClaims.Add(new Domain.Save.PendingClaim
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Kind = slot.Kind == DailyShopItemKind.Rune ? Domain.Save.PendingClaimKind.Card : Domain.Save.PendingClaimKind.Card,
                            SubType = slot.SubType,
                            Amount = slot.Quantity,
                            SourceKey = "daily_shop",
                            CreatedAtUtc = DateTime.UtcNow
                        });
                        success = true;
                        break;
                }
                return save;
            }, ct);

            if (!success) return Result.Failure(string.IsNullOrEmpty(error) ? "Kauf fehlgeschlagen." : error);
            _analytics.Track("daily_shop_purchase", new Dictionary<string, object>
            {
                ["slot_index"] = slotIndex, ["kind"] = slot.Kind.ToString(),
                ["currency"] = slot.PriceCurrency, ["amount"] = slot.PriceAmount,
                ["discounted"] = slot.DiscountedFromDaily
            });
            GameLogger.Info("DailyShop", $"Slot {slotIndex} ({slot.Kind}/{slot.SubType}) gekauft.");
            return Result.Success();
        }
    }
}
