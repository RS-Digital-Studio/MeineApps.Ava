using HandwerkerImperium.Models;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using NSubstitute;

namespace HandwerkerImperium.Tests.Services;

public sealed class EternalMasteryServiceTests
{
    private static IEternalMasteryService CreateService(int totalPrestigeCount)
    {
        var state = new GameState();
        state.Prestige.BronzeCount = totalPrestigeCount;

        var gameStateService = Substitute.For<IGameStateService>();
        gameStateService.State.Returns(state);

        return new EternalMasteryService(gameStateService);
    }

    [Fact]
    public void IncomeBonus_ZeroPrestiges_IsZero()
    {
        var svc = CreateService(0);
        Assert.Equal(0m, svc.IncomeBonus);
        Assert.False(svc.IsActive);
    }

    [Fact]
    public void IncomeBonus_OnePrestige_IsLinearOnly()
    {
        var svc = CreateService(1);
        // 1 * 0.5% = 0.5%
        Assert.Equal(0.005m, svc.IncomeBonus);
        Assert.True(svc.IsActive);
    }

    [Fact]
    public void IncomeBonus_FivePrestiges_IncludesTierBonus()
    {
        var svc = CreateService(5);
        // 5 * 0.5% + 1 * 2.5% = 2.5% + 2.5% = 5.0%
        Assert.Equal(0.05m, svc.IncomeBonus);
    }

    [Fact]
    public void IncomeBonus_TenPrestiges_IncludesMegaTier()
    {
        var svc = CreateService(10);
        // 10 * 0.5% + 2 * 2.5% + 1 * 5% = 5% + 5% + 5% = 15%
        Assert.Equal(0.15m, svc.IncomeBonus);
    }

    [Fact]
    public void IncomeBonus_HundredPrestiges_ScalesEternal()
    {
        var svc = CreateService(100);
        // v2.1.1 (Audit B-H02): Soft-Cap ab 50 Prestiges. Ueberschuss wird logarithmisch
        // gedaempft: effective = 50 + log10(50+1)*10 ≈ 67
        // 67 * 0.5% + 13 * 2.5% + 6 * 5% = 33.5% + 32.5% + 30% = 96%
        Assert.Equal(0.960m, svc.IncomeBonus);
    }

    [Fact]
    public void IncomeBonus_FiftyPrestiges_ReachesSoftCap()
    {
        // B-H02: Genau an der Soft-Cap-Schwelle — voller Bonus, noch keine Daempfung.
        // 50 * 0.5% + 10 * 2.5% + 5 * 5% = 25% + 25% + 25% = 75%
        var svc = CreateService(50);
        Assert.Equal(0.75m, svc.IncomeBonus);
    }

    [Fact]
    public void IncomeBonus_ThousandPrestiges_StaysBoundedBySoftCap()
    {
        // B-H02: Bei 1000 Prestiges greift der Soft-Cap stark — frueher waeren das +1500%
        // gewesen (game-breaking, multiplikativ mit Premium 5-10x). Mit Soft-Cap bleiben
        // wir unter 200% statt 1500%. Das ist die ganze Pointe des Caps.
        var svc = CreateService(1000);
        Assert.InRange(svc.IncomeBonus, 1.0m, 2.0m);
    }

    [Fact]
    public void PrestigesUntilNextTier_AfterFour_IsOne()
    {
        var svc = CreateService(4);
        Assert.Equal(1, svc.PrestigesUntilNextTier);
    }

    [Fact]
    public void PrestigesUntilNextTier_AfterFive_IsFive()
    {
        var svc = CreateService(5);
        // Nächste 5er-Stufe ist 10 → noch 5 Prestiges
        Assert.Equal(5, svc.PrestigesUntilNextTier);
    }

    [Fact]
    public void PrestigesUntilNextMegaTier_AfterNine_IsOne()
    {
        var svc = CreateService(9);
        Assert.Equal(1, svc.PrestigesUntilNextMegaTier);
    }

    [Fact]
    public void DisplayText_FormatsAsPercent()
    {
        var svc = CreateService(10);
        // 15.0%
        Assert.Equal("+15.0%", svc.DisplayText);
    }

    [Fact]
    public void CalculateBonus_NegativeInput_ReturnsZero()
    {
        var svc = CreateService(0);
        Assert.Equal(0m, svc.CalculateBonus(-5));
    }
}
