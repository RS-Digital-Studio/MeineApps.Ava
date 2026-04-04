using HandwerkerImperium.Models;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für DailyRewardService: Verfügbarkeit, Streak-Logik, Tages-Zyklus,
/// Belohnungen, Zeitmanipulations-Schutz und WasStreakBroken.
/// </summary>
public class DailyRewardServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (DailyRewardService service, IGameStateService mockState, GameState state) ErstelleService()
    {
        var mockState = Substitute.For<IGameStateService>();
        var state = GameState.CreateNew();
        mockState.State.Returns(state);
        var service = new DailyRewardService(mockState);
        return (service, mockState, state);
    }

    // ═══════════════════════════════════════════════════════════════════
    // IsRewardAvailable
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsRewardAvailable_NochNieGeansprucht_IstTrue()
    {
        // Vorbereitung: Kein vorheriger Claim
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.MinValue;

        // Prüfung
        service.IsRewardAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsRewardAvailable_HeuteGeansprucht_IstFalse()
    {
        // Vorbereitung: Heute bereits geansprucht
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow;

        // Prüfung
        service.IsRewardAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsRewardAvailable_GesternGeansprucht_IstTrue()
    {
        // Vorbereitung: Gestern geansprucht
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow.AddDays(-1);

        // Prüfung
        service.IsRewardAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsRewardAvailable_DatumInZukunft_IstFalse()
    {
        // Vorbereitung: Zeitmanipulations-Schutz
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow.AddDays(1);

        // Prüfung: Zukunftsdatum blockiert
        service.IsRewardAvailable.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // CurrentDay - 30-Tage-Zyklus
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CurrentDay_StreakNull_IstTag1()
    {
        // Vorbereitung: Neuer Spieler
        var (service, _, state) = ErstelleService();
        state.DailyRewardStreak = 0;

        // Prüfung
        service.CurrentDay.Should().Be(1);
    }

    [Fact]
    public void CurrentDay_Streak1_IstTag1()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.DailyRewardStreak = 1;

        // Prüfung: Streak 1 → Tag 1 (((1-1) % 30) + 1 = 1)
        service.CurrentDay.Should().Be(1);
    }

    [Fact]
    public void CurrentDay_Streak30_IstTag30()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.DailyRewardStreak = 30;

        // Prüfung: Letzter Tag des Zyklus
        service.CurrentDay.Should().Be(30);
    }

    [Fact]
    public void CurrentDay_Streak31_WiederholtzZyklusAuf1()
    {
        // Vorbereitung: Tag 31 → Neustart des 30-Tage-Zyklus
        var (service, _, state) = ErstelleService();
        state.DailyRewardStreak = 31;

        // Prüfung: ((31-1) % 30) + 1 = 1
        service.CurrentDay.Should().Be(1);
    }

    [Fact]
    public void CurrentDay_Streak61_IstTag1()
    {
        // Vorbereitung: Dritter Zyklus
        var (service, _, state) = ErstelleService();
        state.DailyRewardStreak = 61;

        // Prüfung: ((61-1) % 30) + 1 = 1
        service.CurrentDay.Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // WasStreakBroken
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void WasStreakBroken_ErsterClaim_IstFalse()
    {
        // Vorbereitung: Noch nie geansprucht
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.MinValue;

        // Prüfung
        service.WasStreakBroken().Should().BeFalse();
    }

    [Fact]
    public void WasStreakBroken_GesternGeansprucht_IstFalse()
    {
        // Vorbereitung: Gestern → Streak intakt
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow.AddDays(-1);

        // Prüfung
        service.WasStreakBroken().Should().BeFalse();
    }

    [Fact]
    public void WasStreakBroken_VorZweiTagenGeansprucht_IstTrue()
    {
        // Vorbereitung: Letzter Claim vor 2 Tagen → Streak unterbrochen
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow.AddDays(-2);

        // Prüfung
        service.WasStreakBroken().Should().BeTrue();
    }

    [Fact]
    public void WasStreakBroken_DatumInZukunft_IstTrue()
    {
        // Vorbereitung: Zeitmanipulations-Schutz (negative Tage)
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow.AddDays(1);

        // Prüfung: Zukunftsdatum bricht Streak
        service.WasStreakBroken().Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ClaimReward
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ClaimReward_KeineVerfuegbar_GibtNullZurueck()
    {
        // Vorbereitung: Heute bereits geansprucht
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow;

        // Ausführung
        var ergebnis = service.ClaimReward();

        // Prüfung
        ergebnis.Should().BeNull();
    }

    [Fact]
    public void ClaimReward_Verfuegbar_GibtBelohnungZurueck()
    {
        // Vorbereitung: Noch nie geansprucht
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.MinValue;
        state.DailyRewardStreak = 0;

        // Ausführung
        var ergebnis = service.ClaimReward();

        // Prüfung
        ergebnis.Should().NotBeNull();
    }

    [Fact]
    public void ClaimReward_Verfuegbar_ErhoehrtStreak()
    {
        // Vorbereitung: Gestern geansprucht → Streak um 1 erhöhen
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow.AddDays(-1);
        state.DailyRewardStreak = 5;

        // Ausführung
        service.ClaimReward();

        // Prüfung
        state.DailyRewardStreak.Should().Be(6);
    }

    [Fact]
    public void ClaimReward_StreakUnterbrochen_SetztStreakAuf1()
    {
        // Vorbereitung: Streak vor 3 Tagen → Unterbrochen
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow.AddDays(-3);
        state.DailyRewardStreak = 10;

        // Ausführung
        service.ClaimReward();

        // Prüfung: Streak wird auf 1 zurückgesetzt
        state.DailyRewardStreak.Should().Be(1);
    }

    [Fact]
    public void ClaimReward_StreakUnterbrochen_SpeichertStreakBeforeBreak()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow.AddDays(-3);
        state.DailyRewardStreak = 15;

        // Ausführung
        service.ClaimReward();

        // Prüfung: Alter Streak gespeichert für mögliche Rettung
        state.StreakBeforeBreak.Should().Be(15);
    }

    [Fact]
    public void ClaimReward_Verfuegbar_AktualisiertLastDailyRewardClaim()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.MinValue;

        // Ausführung
        service.ClaimReward();

        // Prüfung: LastDailyRewardClaim aktualisiert
        state.LastDailyRewardClaim.Date.Should().Be(DateTime.UtcNow.Date);
    }

    [Fact]
    public void ClaimReward_GeldBelohnung_RuftAddMoneyAuf()
    {
        // Vorbereitung
        var (service, mockState, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.MinValue;
        state.DailyRewardStreak = 0;

        // Ausführung
        service.ClaimReward();

        // Prüfung: AddMoney wurde aufgerufen
        mockState.Received().AddMoney(Arg.Any<decimal>());
    }

    // ═══════════════════════════════════════════════════════════════════
    // TodaysReward
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TodaysReward_NichtVerfuegbar_IstNull()
    {
        // Vorbereitung: Heute bereits geansprucht
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow;

        // Prüfung
        service.TodaysReward.Should().BeNull();
    }

    [Fact]
    public void TodaysReward_Verfuegbar_GibtBelohnungZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.MinValue;

        // Prüfung
        service.TodaysReward.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetRewardCycle
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetRewardCycle_GibtDreissigBelohnungenZurueck()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung
        var zyklus = service.GetRewardCycle();

        // Prüfung: Vollständiger 30-Tage-Zyklus
        zyklus.Should().HaveCount(30);
    }

    [Fact]
    public void GetRewardCycle_HedutigerTag_IstAlsIsTodayMarkiert()
    {
        // Vorbereitung: Noch nie geansprucht → Tag 1 ist heute
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.MinValue;
        state.DailyRewardStreak = 0;

        // Ausführung
        var zyklus = service.GetRewardCycle();

        // Prüfung: Tag 1 hat IsToday = true
        zyklus.Should().Contain(r => r.Day == 1 && r.IsToday);
    }

    [Fact]
    public void GetRewardCycle_BereitsGeanspruchteTage_SindAlsIsClaimedMarkiert()
    {
        // Vorbereitung: Streak = 5 → Tage 1-4 sind geansprucht
        var (service, _, state) = ErstelleService();
        state.DailyRewardStreak = 5;
        state.LastDailyRewardClaim = DateTime.UtcNow; // Heute geansprucht

        // Ausführung
        var zyklus = service.GetRewardCycle();

        // Prüfung: Tag 1-4 sind IsClaimed
        zyklus.Where(r => r.Day < 5).Should().AllSatisfy(r => r.IsClaimed.Should().BeTrue());
    }

    // ═══════════════════════════════════════════════════════════════════
    // TimeUntilNextReward
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TimeUntilNextReward_Verfuegbar_IstZero()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.MinValue;

        // Prüfung
        service.TimeUntilNextReward.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TimeUntilNextReward_GeradeGeansprucht_IstGroesserNull()
    {
        // Vorbereitung: Soeben geansprucht
        var (service, _, state) = ErstelleService();
        state.LastDailyRewardClaim = DateTime.UtcNow;

        // Prüfung: Muss bis Mitternacht warten
        service.TimeUntilNextReward.Should().BeGreaterThan(TimeSpan.Zero);
    }
}
