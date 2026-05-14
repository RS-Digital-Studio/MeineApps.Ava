using BomberBlast.Core.Combat;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für ComboSystem (v2.0.54 — Phase 12).
/// Pure-Logic-Klasse — testbar ohne Engine-Mocks. Validiert Counter, Timer-Window-Verlängerung,
/// Score-Tabelle, ULTRA/MEGA/CHAIN-Schwellen, Reset-Verhalten.
/// </summary>
public class ComboSystemTests
{
    [Fact]
    public void Initial_KeineComboAktiv()
    {
        var combo = new ComboSystem();
        combo.Count.Should().Be(0);
        combo.Timer.Should().Be(0);
        combo.IsActive.Should().BeFalse();
        combo.IsChainKill.Should().BeFalse();
        combo.IsMega.Should().BeFalse();
        combo.IsUltra.Should().BeFalse();
    }

    [Fact]
    public void RegisterKill_ErsteKill_StartetCombo()
    {
        var combo = new ComboSystem();
        combo.RegisterKill();

        combo.Count.Should().Be(1);
        combo.Timer.Should().Be(ComboSystem.COMBO_WINDOW);
        combo.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RegisterKill_InnerhalbWindow_ErhoehtCounter()
    {
        var combo = new ComboSystem();
        combo.RegisterKill();
        combo.Update(0.5f);  // 1.5s remaining
        combo.RegisterKill();

        combo.Count.Should().Be(2);
        combo.Timer.Should().Be(ComboSystem.COMBO_WINDOW, "Timer wird bei jedem Kill resettet");
    }

    [Fact]
    public void RegisterKill_NachWindow_ResettetCounter()
    {
        var combo = new ComboSystem();
        combo.RegisterKill();
        combo.Update(2.5f);  // Window abgelaufen

        combo.Count.Should().Be(1, "Counter sollte 1 sein nach Update — Reset durch Engine, dann RegisterKill");
        // Eigentlich resettet Update den Counter NICHT — das macht die Engine separat.
        // Testen wir das direkte Verhalten: RegisterKill nach abgelaufenem Timer
        combo.RegisterKill();
        combo.Count.Should().Be(1, "RegisterKill mit abgelaufenem Timer fängt bei 1 an");
    }

    [Theory]
    [InlineData(2, 200)]
    [InlineData(3, 500)]
    [InlineData(4, 1000)]
    [InlineData(5, 2000)]
    [InlineData(6, 4000)]
    [InlineData(7, 8000)]
    [InlineData(8, 15000)]
    [InlineData(9, 20000)]
    [InlineData(10, 30000)]
    [InlineData(15, 30000)]
    public void GetScoreBonus_KorrekteWerteProCombo(int comboCount, int expectedBonus)
    {
        var combo = new ComboSystem();
        for (int i = 0; i < comboCount; i++) combo.RegisterKill();

        combo.GetScoreBonus().Should().Be(expectedBonus);
    }

    [Fact]
    public void GetScoreBonus_BeiCombo1_LiefertNull()
    {
        var combo = new ComboSystem();
        combo.RegisterKill();
        combo.GetScoreBonus().Should().Be(0, "Erster Kill ist kein Combo-Bonus wert");
    }

    [Fact]
    public void Window_VerlaengertSich_BeiCombo6Plus()
    {
        var combo = new ComboSystem();
        for (int i = 0; i < 6; i++) combo.RegisterKill();

        combo.Timer.Should().Be(ComboSystem.COMBO_WINDOW + ComboSystem.COMBO_WINDOW_EXTENSION);
    }

    [Fact]
    public void Window_StandardLänge_UnterCombo6()
    {
        var combo = new ComboSystem();
        for (int i = 0; i < 5; i++) combo.RegisterKill();

        combo.Timer.Should().Be(ComboSystem.COMBO_WINDOW);
    }

    [Theory]
    [InlineData(1, false, false, false)]
    [InlineData(2, false, false, false)]
    [InlineData(3, true, false, false)]
    [InlineData(4, true, false, false)]
    [InlineData(5, true, true, false)]
    [InlineData(9, true, true, false)]
    [InlineData(10, true, true, true)]
    [InlineData(15, true, true, true)]
    public void Schwellen_IsChainKill_IsMega_IsUltra(int count, bool chain, bool mega, bool ultra)
    {
        var combo = new ComboSystem();
        for (int i = 0; i < count; i++) combo.RegisterKill();

        combo.IsChainKill.Should().Be(chain);
        combo.IsMega.Should().Be(mega);
        combo.IsUltra.Should().Be(ultra);
    }

    [Fact]
    public void Reset_StelltAllesNullZurueck()
    {
        var combo = new ComboSystem();
        for (int i = 0; i < 5; i++) combo.RegisterKill();

        combo.Reset();

        combo.Count.Should().Be(0);
        combo.Timer.Should().Be(0);
        combo.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Update_VerringertTimer()
    {
        var combo = new ComboSystem();
        combo.RegisterKill();

        combo.Update(0.5f);

        combo.Timer.Should().BeApproximately(ComboSystem.COMBO_WINDOW - 0.5f, 0.001f);
    }

    [Fact]
    public void Update_TimerKannNichtNegativWerden()
    {
        var combo = new ComboSystem();
        combo.RegisterKill();

        combo.Update(5.0f);  // Sehr großer Time-Step

        combo.Timer.Should().Be(0);
    }

    [Theory]
    [InlineData(5, 0.8f, 0.8f)]
    [InlineData(9, 0.8f, 0.8f)]
    [InlineData(10, 0.8f, 1.2f)]   // ULTRA: 1.5×
    [InlineData(15, 1.0f, 1.5f)]
    public void GetSlowMotionDuration_UltraVerlaengert(int count, float baseDur, float expected)
    {
        var combo = new ComboSystem();
        for (int i = 0; i < count; i++) combo.RegisterKill();

        combo.GetSlowMotionDuration(baseDur).Should().BeApproximately(expected, 0.001f);
    }

    [Fact]
    public void DailyRaceMode_TrySubmit_FunktioniertIdempotent()
    {
        // Hinweis: Test ist hier weil DailyRaceMode auch ein Pure-Logic-Hook hat (TrySubmit)
        var mode = new BomberBlast.Core.Modes.DailyRaceMode();

        mode.TrySubmit(5000).Should().BeTrue();
        mode.Submitted.Should().BeTrue();

        mode.TrySubmit(7000).Should().BeFalse("idempotent: zweite Submission wird verweigert");
    }

    [Fact]
    public void DailyRaceMode_TrySubmit_LehntScoreNullAb()
    {
        var mode = new BomberBlast.Core.Modes.DailyRaceMode();

        mode.TrySubmit(0).Should().BeFalse();
        mode.TrySubmit(-100).Should().BeFalse();
        mode.Submitted.Should().BeFalse();
    }

    [Fact]
    public void BossRushMode_AccumulateScore_ReturntNextIndex()
    {
        var mode = new BomberBlast.Core.Modes.BossRushMode { BossIndex = 0, AccumulatedScore = 0 };

        var nextIndex = mode.AccumulateScoreAndGetNextBossIndex(levelScoreEarned: 5000, totalBossesInSequence: 5);

        nextIndex.Should().Be(1);
        mode.AccumulatedScore.Should().Be(5000);
    }

    [Fact]
    public void BossRushMode_AccumulateScore_LiefertNeg1WennAlleDurch()
    {
        var mode = new BomberBlast.Core.Modes.BossRushMode { BossIndex = 4, AccumulatedScore = 20000 };

        var nextIndex = mode.AccumulateScoreAndGetNextBossIndex(levelScoreEarned: 5000, totalBossesInSequence: 5);

        nextIndex.Should().Be(-1, "Letzter Boss → kein nächster");
        mode.AccumulatedScore.Should().Be(25000);
    }

    [Fact]
    public void BossRushMode_TryGetSubmitArgs_AtomareSubmittedSetzung()
    {
        var mode = new BomberBlast.Core.Modes.BossRushMode { AccumulatedScore = 50000, TotalTimeSeconds = 240f };

        var args = mode.TryGetSubmitArgs(completedAllBosses: true);

        args.Should().NotBeNull();
        args!.Value.Score.Should().Be(50000);
        args.Value.Time.Should().Be(240f);
        args.Value.CompletedAll.Should().BeTrue();
        mode.Submitted.Should().BeTrue();

        mode.TryGetSubmitArgs(completedAllBosses: true).Should().BeNull("idempotent");
    }
}
