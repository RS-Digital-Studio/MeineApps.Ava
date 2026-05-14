using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementierung des Reputation-Shops (v2.1.0).
/// Items werden mit dem ReputationScore bezahlt — keine GS, kein Geld. Effekte werden
/// auf den GameState appliziert (z.B. Mood-Boost auf alle Worker, Skin-Flag im Cosmetics-Sub).
/// </summary>
public sealed class ReputationShopService : IReputationShopService
{
    private readonly IGameStateService _gameStateService;
    // Eigener Lock entfernt — Mutationen via IGameStateService.ExecuteWithLock.

    public event Action<ReputationShopItem>? ItemPurchased;

    public ReputationShopService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
    }

    public IReadOnlyList<ReputationShopItem> AvailableItems { get; } = new List<ReputationShopItem>
    {
        new()
        {
            Id = "rep_regular_customer_guarantee",
            Effect = ReputationShopEffect.RegularCustomerGuarantee,
            ReputationCost = 30,
            NameKey = "RepShopRegularCustomerName",
            DescriptionKey = "RepShopRegularCustomerDesc",
            NameFallback = "Stammkunden-Garantie",
            DescriptionFallback = "Naechste 5 Auftraege werden Stammkunden — bis zu 1.5x Reward.",
            IconKind = "AccountStar"
        },
        new()
        {
            Id = "rep_faster_delivery",
            Effect = ReputationShopEffect.FasterDelivery,
            ReputationCost = 20,
            NameKey = "RepShopFasterDeliveryName",
            DescriptionKey = "RepShopFasterDeliveryDesc",
            NameFallback = "Schnelle Lieferung",
            DescriptionFallback = "Naechster Lieferant kommt 50% schneller fuer 1 Stunde.",
            IconKind = "Truck"
        },
        new()
        {
            Id = "rep_worker_mood_boost",
            Effect = ReputationShopEffect.WorkerMoodBoost,
            ReputationCost = 25,
            NameKey = "RepShopWorkerMoodBoostName",
            DescriptionKey = "RepShopWorkerMoodBoostDesc",
            NameFallback = "Team-Stimmungs-Boost",
            DescriptionFallback = "Alle Worker erhalten +30 Mood (sofort).",
            IconKind = "AccountGroup"
        },
        new()
        {
            Id = "rep_workshop_skin_wood_premium",
            Effect = ReputationShopEffect.WorkshopSkinWoodPremium,
            ReputationCost = 100,
            NameKey = "RepShopSkinWoodName",
            DescriptionKey = "RepShopSkinWoodDesc",
            NameFallback = "Workshop-Skin „Holz-Premium\"",
            DescriptionFallback = "Permanenter kosmetischer Skin fuer alle Werkstaetten.",
            IconKind = "Palette"
        },
        new()
        {
            Id = "rep_insurance",
            Effect = ReputationShopEffect.ReputationInsurance,
            ReputationCost = 40,
            NameKey = "RepShopInsuranceName",
            DescriptionKey = "RepShopInsuranceDesc",
            NameFallback = "Reputation-Insurance",
            DescriptionFallback = "Naechster Risk-Miss kostet keine Reputation.",
            IconKind = "ShieldCheck"
        }
    };

    public bool IsUnlocked
        => _gameStateService.State.Reputation.ReputationScore >= ((IReputationShopService)this).MinReputationToUnlock;

    public bool TryBuy(string itemId)
    {
        ReputationShopItem? item = null;
        for (int i = 0; i < AvailableItems.Count; i++)
        {
            if (AvailableItems[i].Id == itemId) { item = AvailableItems[i]; break; }
        }
        if (item == null) return false;

        var purchased = _gameStateService.ExecuteWithLock(() =>
        {
            var state = _gameStateService.State;
            if (state.Reputation.ReputationScore < item.ReputationCost) return false;
            state.Reputation.ReputationScore -= item.ReputationCost;
            ApplyEffect(item.Effect, state);
            return true;
        });
        if (purchased) ItemPurchased?.Invoke(item);
        return purchased;
    }

    private static void ApplyEffect(ReputationShopEffect effect, GameState state)
    {
        switch (effect)
        {
            case ReputationShopEffect.RegularCustomerGuarantee:
                state.RepShopRegularCustomerCharges = 5;
                break;
            case ReputationShopEffect.FasterDelivery:
                state.RepShopFasterDeliveryUntil = DateTime.UtcNow.AddHours(1);
                break;
            case ReputationShopEffect.WorkerMoodBoost:
                for (int i = 0; i < state.Workshops.Count; i++)
                {
                    var workers = state.Workshops[i].Workers;
                    for (int j = 0; j < workers.Count; j++)
                        workers[j].Mood = Math.Min(100m, workers[j].Mood + 30m);
                }
                break;
            case ReputationShopEffect.WorkshopSkinWoodPremium:
                state.RepShopWoodPremiumSkinUnlocked = true;
                break;
            case ReputationShopEffect.ReputationInsurance:
                state.RepShopInsuranceCharges = 1;
                break;
        }
    }
}
