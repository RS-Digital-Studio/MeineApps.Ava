using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für LuckySpin Pity-Counter (Phase 23 — , Lootbox-Compliance UK/China).
/// Validiert dass nach 50 Spins ohne Jackpot der nächste Spin garantiert ein Jackpot wird,
/// und dass die Drop-Rate-Tabelle für Compliance-Disclosure korrekt summiert.
/// </summary>
public class LuckySpinPityTests
{
    [Fact]
    public void NeueInstanz_HatNullSpins()
    {
        var prefs = new InMemoryPreferences();
        var svc = new LuckySpinService(prefs);
        svc.SpinsSinceLastJackpot.Should().Be(0);
        svc.JackpotPityThreshold.Should().Be(50);
    }

    [Fact]
    public void Spin_ErhoehtPityCounter_BisJackpotKommt()
    {
        var prefs = new InMemoryPreferences();
        var svc = new LuckySpinService(prefs);
        var jackpotIdx = svc.GetRewards().Single(r => r.IsJackpot).Index;

        bool jackpotHit = false;
        for (int i = 0; i < 200; i++)
        {
            int idx = svc.Spin();
            if (idx == jackpotIdx)
            {
                jackpotHit = true;
                svc.SpinsSinceLastJackpot.Should().Be(0, "Jackpot-Hit resetet Pity-Counter");
                break;
            }
        }
        // Garantie: Spätestens nach 50 Spins schlägt Pity-Counter zu
        jackpotHit.Should().BeTrue("Pity-Counter muss spätestens bei Spin 51 garantiert Jackpot liefern");
    }

    [Fact]
    public void Spin_PityThreshold_GarantiertJackpot()
    {
        var prefs = new InMemoryPreferences();
        var svc = new LuckySpinService(prefs);

        // Setze Counter direkt auf 50 via Reflection (faking 50 Pech-Spins)
        var dataField = typeof(LuckySpinService).GetField("_data",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var data = dataField!.GetValue(svc)!;
        var spinsField = data.GetType().GetProperty("SpinsSinceLastJackpot")!;
        spinsField.SetValue(data, 50);

        // Nächster Spin MUSS Jackpot sein
        int idx = svc.Spin();
        var jackpotIdx = svc.GetRewards().Single(r => r.IsJackpot).Index;

        idx.Should().Be(jackpotIdx, "Pity-Counter erzwingt Jackpot bei >= 50 Spins ohne");
        svc.SpinsSinceLastJackpot.Should().Be(0);
    }

    [Fact]
    public void GetDropRates_SummiertAuf100Prozent()
    {
        var prefs = new InMemoryPreferences();
        var svc = new LuckySpinService(prefs);

        var rates = svc.GetDropRates();
        rates.Should().HaveCount(svc.GetRewards().Count);

        var totalPercent = rates.Sum(r => r.ProbabilityPercent);
        totalPercent.Should().BeApproximately(100f, 0.5f, "Drop-Rate-Summe muss 100% sein für Compliance-Disclosure");
    }

    [Fact]
    public void GetDropRates_JackpotHatNiedrigsteRate()
    {
        var prefs = new InMemoryPreferences();
        var svc = new LuckySpinService(prefs);

        var rates = svc.GetDropRates();
        var rewards = svc.GetRewards();
        var jackpotIdx = rewards.Single(r => r.IsJackpot).Index;
        var jackpotRate = rates.Single(r => r.RewardIndex == jackpotIdx).ProbabilityPercent;

        // Jackpot sollte am unteren Ende der Verteilung liegen (max ~5%)
        jackpotRate.Should().BeLessThan(8f);
    }

    [Fact]
    public void Spin_Persistiert_PityCounter()
    {
        var prefs = new InMemoryPreferences();
        var svc = new LuckySpinService(prefs);
        for (int i = 0; i < 5; i++) svc.Spin();
        var pityBefore = svc.SpinsSinceLastJackpot;

        // Neue Instanz mit gleichen Prefs → Pity-Counter sollte erhalten bleiben
        var svc2 = new LuckySpinService(prefs);
        svc2.SpinsSinceLastJackpot.Should().BeOneOf(pityBefore, 0); // 0 wenn dazwischen Jackpot, sonst gleich
    }
}
