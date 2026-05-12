using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// AAA-Audit P1 Long-Term-Engagement post-Lv1000 (12.05.2026).
/// Implementierung von <see cref="IEternalMasteryService"/>. Liest TotalPrestigeCount aus
/// dem GameState und liefert den akkumulierten Bonus. Konstanten leben in
/// <see cref="GameBalanceConstants"/> fuer zentrales Balancing.
/// </summary>
public sealed class EternalMasteryService : IEternalMasteryService
{
    private readonly IGameStateService _gameStateService;

    public EternalMasteryService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
    }

    public int CompletedPrestiges => _gameStateService.State.Prestige.TotalPrestigeCount;

    public decimal IncomeBonus => CalculateBonus(CompletedPrestiges);

    public bool IsActive => CompletedPrestiges > 0;

    public int PrestigesUntilNextTier
    {
        get
        {
            if (CompletedPrestiges == 0) return 5;
            int next5 = ((CompletedPrestiges / 5) + 1) * 5;
            return next5 - CompletedPrestiges;
        }
    }

    public int PrestigesUntilNextMegaTier
    {
        get
        {
            if (CompletedPrestiges == 0) return 10;
            int next10 = ((CompletedPrestiges / 10) + 1) * 10;
            return next10 - CompletedPrestiges;
        }
    }

    public string DisplayText
    {
        get
        {
            var pct = IncomeBonus * 100m;
            // InvariantCulture damit Tests + Logs reproduzierbar sind — die UI nutzt
            // ggf. eigenes Format via Localization-Service.
            return $"+{pct.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%";
        }
    }

    public decimal CalculateBonus(int completedPrestiges)
    {
        if (completedPrestiges <= 0) return 0m;

        // Linear: 0.5% pro Prestige
        decimal linear = completedPrestiges * GameBalanceConstants.EternalMasteryBonusPerPrestige;

        // 5er-Stufen-Bonus: alle 5 Prestiges +2.5% zusaetzlich
        int tiers5 = completedPrestiges / 5;
        decimal tier5Bonus = tiers5 * GameBalanceConstants.EternalMasteryBonusPer5Prestiges;

        // 10er-Mega-Stufen-Bonus: alle 10 Prestiges +5% zusaetzlich
        int tiers10 = completedPrestiges / 10;
        decimal tier10Bonus = tiers10 * GameBalanceConstants.EternalMasteryBonusPer10Prestiges;

        return linear + tier5Bonus + tier10Bonus;
    }
}
