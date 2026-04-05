using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet das Glücksrad: Täglicher Gratis-Spin, kostenpflichtige Spins (5 Goldschrauben),
/// gewichtete Zufallsgewinne basierend auf aktuellem Einkommen.
/// </summary>
public sealed class LuckySpinService : ILuckySpinService
{
    private readonly IGameStateService _gameStateService;

    /// <summary>
    /// Gewichtungstabelle für Glücksrad-Gewinne.
    /// Höherer Weight = häufigerer Drop.
    /// </summary>
    private static readonly (LuckySpinPrizeType type, int weight)[] PrizeWeights =
    [
        (LuckySpinPrizeType.MoneySmall, 30),
        (LuckySpinPrizeType.MoneyMedium, 20),
        (LuckySpinPrizeType.MoneyLarge, 10),
        (LuckySpinPrizeType.XpBoost, 15),
        (LuckySpinPrizeType.GoldenScrews5, 12),
        (LuckySpinPrizeType.SpeedBoost, 8),
        (LuckySpinPrizeType.ToolUpgrade, 4),
        (LuckySpinPrizeType.Jackpot50, 1),
    ];

    private static readonly int TotalWeight = PrizeWeights.Sum(p => p.weight);

    public LuckySpinService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
    }

    public bool HasFreeSpin => _gameStateService.State.LuckySpin.HasFreeSpin;
    public bool HasAdSpin => _gameStateService.State.LuckySpin.HasAdSpin;

    private const int FlatSpinCost = 5;
    public int SpinCost => FlatSpinCost;

    public LuckySpinPrizeType Spin()
    {
        var spinState = _gameStateService.State.LuckySpin;
        spinState.ResetDailyIfNeeded();

        if (HasFreeSpin)
        {
            spinState.LastFreeSpinDate = DateTime.UtcNow;
        }
        else
        {
            if (!_gameStateService.TrySpendGoldenScrews(SpinCost))
                return LuckySpinPrizeType.MoneySmall;

            spinState.PaidSpinsToday++;
            spinState.LastPaidSpinDate = DateTime.UtcNow;
        }

        spinState.TotalSpins++;
        return GetRandomPrize();
    }

    /// <summary>BAL-AD-6: Gewinn für Ad-Spin bestimmen (ohne Kosten). MarkAdSpinUsed() danach.</summary>
    public LuckySpinPrizeType SpinForAd()
    {
        _gameStateService.State.LuckySpin.TotalSpins++;
        return GetRandomPrize();
    }

    /// <summary>Markiert den Ad-Spin als heute verbraucht.</summary>
    public void MarkAdSpinUsed()
    {
        _gameStateService.State.LuckySpin.LastAdSpinDate = DateTime.UtcNow;
    }

    public void ApplyPrize(LuckySpinPrizeType prizeType)
    {
        var state = _gameStateService.State;
        var incomePerSecond = Math.Max(1m, state.NetIncomePerSecond);
        var (money, screws, xp, _) = LuckySpinPrize.CalculateReward(prizeType, incomePerSecond);

        if (money > 0)
            _gameStateService.AddMoney(money);

        if (screws > 0)
            _gameStateService.AddGoldenScrews(screws);

        if (xp > 0)
            _gameStateService.AddXp(xp);

        // SpeedBoost: 30min 2x-Geschwindigkeit
        if (prizeType == LuckySpinPrizeType.SpeedBoost)
        {
            var now = DateTime.UtcNow;
            var currentEnd = state.SpeedBoostEndTime > now ? state.SpeedBoostEndTime : now;
            state.SpeedBoostEndTime = currentEnd.AddMinutes(30);
        }

    }

    /// <summary>
    /// Bestimmt einen gewichteten Zufallsgewinn.
    /// </summary>
    private static LuckySpinPrizeType GetRandomPrize()
    {
        int roll = Random.Shared.Next(TotalWeight);
        int cumulative = 0;

        foreach (var (type, weight) in PrizeWeights)
        {
            cumulative += weight;
            if (roll < cumulative)
                return type;
        }

        // Fallback (sollte nie erreicht werden)
        return LuckySpinPrizeType.MoneySmall;
    }
}
