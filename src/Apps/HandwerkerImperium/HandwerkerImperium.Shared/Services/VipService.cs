using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// VIP-System basierend auf Gesamtausgaben (In-App-Purchases).
/// Nutzt VipTierExtensions fuer Schwellen und Boni.
/// </summary>
public sealed class VipService : IVipService
{
    private readonly IGameStateService _gameStateService;

    public VipService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;

        // Bei State-Load (z.B. nach Import/Prestige) VIP-Level neu berechnen
        _gameStateService.StateLoaded += (_, _) => RefreshVipLevel();
    }

    /// <inheritdoc />
    public VipTier CurrentTier { get; private set; } = VipTier.None;

    /// <inheritdoc />
    public decimal IncomeBonus => CurrentTier.GetIncomeBonus();

    /// <inheritdoc />
    public decimal XpBonus => CurrentTier.GetXpBonus();

    /// <inheritdoc />
    public decimal CostReduction => CurrentTier.GetCostReduction();

    /// <inheritdoc />
    public int ExtraDailyChallenges => CurrentTier >= VipTier.Silver ? 1 : 0;

    /// <inheritdoc />
    public int ExtraWeeklyMissions => CurrentTier >= VipTier.Gold ? 1 : 0;

    /// <inheritdoc />
    public event Action? VipLevelChanged;

    /// <inheritdoc />
    public void RefreshVipLevel()
    {
        var state = _gameStateService.State;
        var newTier = VipTierExtensions.DetermineVipTier(state.TotalPurchaseAmount);

        if (newTier != CurrentTier)
        {
            CurrentTier = newTier;
            state.VipLevel = newTier;
            VipLevelChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public void RecordPurchase(decimal amountEur)
    {
        var state = _gameStateService.State;
        state.TotalPurchaseAmount += amountEur;
        RefreshVipLevel();
    }
}
