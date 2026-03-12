using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für Worker: CreateRandom(), Tier-Effizienz-Multiplikatoren,
/// Stimmungs-/Ausdauer-Faktoren, EffectiveEfficiency-Berechnung.
/// </summary>
public class WorkerTests
{
    // ═══════════════════════════════════════════════════════════════════
    // CreateRandom / CreateForTier
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateRandom_GibtTierEArbeiter()
    {
        // Ausführung
        var worker = Worker.CreateRandom();

        // Prüfung: CreateRandom() erstellt immer Tier E (laut Code-Kommentar)
        worker.Tier.Should().Be(WorkerTier.E);
    }

    [Fact]
    public void CreateRandom_HatGueltigeId()
    {
        // Ausführung
        var worker = Worker.CreateRandom();

        // Prüfung
        worker.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreateRandom_StartStimmungIstAchzig()
    {
        // Ausführung
        var worker = Worker.CreateRandom();

        // Prüfung: Startstimmung ist immer 80
        worker.Mood.Should().Be(80m);
    }

    [Fact]
    public void CreateRandom_StartMüdigkeit_IstNull()
    {
        // Ausführung
        var worker = Worker.CreateRandom();

        // Prüfung: Kein Startwert für Müdigkeit
        worker.Fatigue.Should().Be(0m);
    }

    [Fact]
    public void CreateRandom_StartErfahrungsLevel_IstEins()
    {
        // Ausführung
        var worker = Worker.CreateRandom();

        // Prüfung
        worker.ExperienceLevel.Should().Be(1);
    }

    [Fact]
    public void CreateForTier_TierA_HatHoeherenMindestlohn()
    {
        // Vorbereitung
        var tierE = Worker.CreateForTier(WorkerTier.E);
        var tierA = Worker.CreateForTier(WorkerTier.A);

        // Prüfung: Laut Tabelle E=9€/h, A=90€/h
        tierA.WagePerHour.Should().BeGreaterThan(tierE.WagePerHour);
    }

    [Fact]
    public void CreateForTier_TierF_EffizienzInnerhalb_F_Bereich()
    {
        // Vorbereitung: Tier F min=0.30, max=0.50
        var worker = Worker.CreateForTier(WorkerTier.F);

        // Prüfung
        worker.Efficiency.Should().BeInRange(0.30m, 0.50m);
    }

    [Fact]
    public void CreateForTier_TierLegendary_EffizienzInnerhalb_Legendary_Bereich()
    {
        // Vorbereitung: Tier Legendary min=13.00, max=22.00
        var worker = Worker.CreateForTier(WorkerTier.Legendary);

        // Prüfung
        worker.Efficiency.Should().BeInRange(13.00m, 22.00m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Tier-Effizienz-Grenzen
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TierGetMinEfficiency_AlleGueltigeWerte()
    {
        // Prüfung: Alle Tiers haben positive Mindesteffizienz
        foreach (var tier in Enum.GetValues<WorkerTier>())
        {
            tier.GetMinEfficiency().Should().BePositive(
                because: $"Tier {tier} muss eine positive Mindesteffizienz haben");
        }
    }

    [Fact]
    public void TierGetMaxEfficiency_ImmerGroesserAlsMin()
    {
        // Prüfung: Max muss immer größer als Min sein
        foreach (var tier in Enum.GetValues<WorkerTier>())
        {
            tier.GetMaxEfficiency().Should().BeGreaterThan(
                tier.GetMinEfficiency(),
                because: $"Max-Effizienz für Tier {tier} muss größer als Min-Effizienz sein");
        }
    }

    [Fact]
    public void TierGetMaxEfficiency_SteigendJeHoeherDerTier()
    {
        // Prüfung: Höhere Tiers haben höhere Max-Effizienz
        var tierE = WorkerTier.E.GetMaxEfficiency();
        var tierC = WorkerTier.C.GetMaxEfficiency();
        var tierA = WorkerTier.A.GetMaxEfficiency();
        var tierS = WorkerTier.S.GetMaxEfficiency();

        tierE.Should().BeLessThan(tierC);
        tierC.Should().BeLessThan(tierA);
        tierA.Should().BeLessThan(tierS);
    }

    // ═══════════════════════════════════════════════════════════════════
    // EffectiveEfficiency - Zustandsberechnung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void EffectiveEfficiency_BeiRuhe_IstNull()
    {
        // Vorbereitung: Worker in Ruhe
        var worker = Worker.CreateRandom();
        worker.IsResting = true;

        // Prüfung: Ruhende Worker leisten nichts
        worker.EffectiveEfficiency.Should().Be(0m);
    }

    [Fact]
    public void EffectiveEfficiency_BeimTraining_IstNull()
    {
        // Vorbereitung: Worker beim Training
        var worker = Worker.CreateRandom();
        worker.IsTraining = true;

        // Prüfung: Trainierende Worker leisten nichts
        worker.EffectiveEfficiency.Should().Be(0m);
    }

    [Fact]
    public void EffectiveEfficiency_GuteLauneNullMüdigkeit_NaheAnBasiseffizienz()
    {
        // Vorbereitung: Worker mit 80 Stimmung und 0 Müdigkeit
        var worker = Worker.CreateRandom();
        worker.Mood = 80m;
        worker.Fatigue = 0m;
        worker.Efficiency = 1.0m;

        // Prüfung: Effektiveffizienz sollte nahe der Basiseffizienz sein (1.0)
        worker.EffectiveEfficiency.Should().BeApproximately(1.0m, 0.15m);
    }

    [Fact]
    public void EffectiveEfficiency_NiedereMood_VerringerteEffizienz()
    {
        // Vorbereitung: Zwei Worker, einer happy, einer unglücklich
        var happyWorker = Worker.CreateRandom();
        happyWorker.Mood = 100m;
        happyWorker.Fatigue = 0m;
        happyWorker.Efficiency = 1.0m;

        var sadWorker = Worker.CreateRandom();
        sadWorker.Mood = 10m;
        sadWorker.Fatigue = 0m;
        sadWorker.Efficiency = 1.0m;

        // Prüfung: Unglücklicher Worker ist weniger effizient
        sadWorker.EffectiveEfficiency.Should().BeLessThan(happyWorker.EffectiveEfficiency);
    }

    [Fact]
    public void EffectiveEfficiency_HoheErmuedung_VerringerteEffizienz()
    {
        // Vorbereitung: Müder vs. ausgeruhter Worker
        var ausgeruht = Worker.CreateRandom();
        ausgeruht.Fatigue = 0m;
        ausgeruht.Mood = 80m;
        ausgeruht.Efficiency = 1.0m;

        var müde = Worker.CreateRandom();
        müde.Fatigue = 100m;
        müde.Mood = 80m;
        müde.Efficiency = 1.0m;

        // Prüfung: Laut Formel: 0 Müdigkeit = 1.0x, 100 Müdigkeit = 0.5x
        müde.EffectiveEfficiency.Should().BeLessThan(ausgeruht.EffectiveEfficiency);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Zustandsflags
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsTired_BeiMüdigkeitHundert_IstTrue()
    {
        // Vorbereitung
        var worker = Worker.CreateRandom();
        worker.Fatigue = 100m;

        // Prüfung
        worker.IsTired.Should().BeTrue();
    }

    [Fact]
    public void IsTired_BeiMüdigkeitNeunzig_IstFalse()
    {
        // Vorbereitung
        var worker = Worker.CreateRandom();
        worker.Fatigue = 90m;

        // Prüfung
        worker.IsTired.Should().BeFalse();
    }

    [Fact]
    public void IsUnhappy_BeiMoodUnterFuenfzig_IstTrue()
    {
        // Vorbereitung
        var worker = Worker.CreateRandom();
        worker.Mood = 49m;

        // Prüfung
        worker.IsUnhappy.Should().BeTrue();
    }

    [Fact]
    public void WillQuit_BeiMoodUnterZwanzig_IstTrue()
    {
        // Vorbereitung
        var worker = Worker.CreateRandom();
        worker.Mood = 19m;

        // Prüfung
        worker.WillQuit.Should().BeTrue();
    }

    [Fact]
    public void WillQuit_BeiMoodZwanzig_IstFalse()
    {
        // Prüfung: Exakt 20 sollte noch nicht kündigen (WillQuit: Mood < 20)
        var worker = Worker.CreateRandom();
        worker.Mood = 20m;

        worker.WillQuit.Should().BeFalse();
    }

    [Fact]
    public void IsWorking_NichtRuhendNichtTrainenMitZuweisung_IstTrue()
    {
        // Vorbereitung
        var worker = Worker.CreateRandom();
        worker.IsResting = false;
        worker.IsTraining = false;
        worker.AssignedWorkshop = WorkshopType.Carpenter;

        // Prüfung
        worker.IsWorking.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Lohn-Berechnung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TrainingCostPerHour_IstZweifacherStundenlohn()
    {
        // Vorbereitung
        var worker = Worker.CreateRandom();
        worker.WagePerHour = 20m;

        // Prüfung: Training kostet 2x Stundenlohn
        worker.TrainingCostPerHour.Should().Be(40m);
    }

    [Fact]
    public void XpForNextLevel_Level1_IstZweihundert()
    {
        // Vorbereitung
        var worker = Worker.CreateRandom();
        worker.ExperienceLevel = 1;

        // Prüfung: 1 * 200 = 200
        worker.XpForNextLevel.Should().Be(200);
    }

    [Fact]
    public void GetAvailableTiers_Level1_NurFUndE()
    {
        // Ausführung
        var tiers = Worker.GetAvailableTiers(playerLevel: 1, prestigeLevel: 0);

        // Prüfung: Bei Level 1 sind nur niedrige Tiers verfügbar
        tiers.Should().Contain(WorkerTier.E);
        tiers.Should().NotContain(WorkerTier.S);
        tiers.Should().NotContain(WorkerTier.Legendary);
    }
}
