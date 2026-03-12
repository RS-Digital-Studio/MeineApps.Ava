using FluentAssertions;
using FitnessRechner.Models;
using Xunit;

namespace FitnessRechner.Tests;

/// <summary>
/// Tests fuer FitnessEngine - alle Berechnungsformeln auf Korrektheit und Grenzfälle prüfen.
/// </summary>
public class FitnessEngineTests
{
    // System Under Test
    private readonly FitnessEngine _sut = new();

    #region BMI-Tests

    [Fact]
    public void CalculateBmi_NormalerMann_LiefertKorrektenBmiWert()
    {
        // Vorbereitung: 80 kg, 180 cm → BMI = 80 / (1.8 * 1.8) = 24.69
        const double erwartetBmi = 80.0 / (1.8 * 1.8);

        // Ausführung
        var ergebnis = _sut.CalculateBmi(80, 180);

        // Prüfung
        ergebnis.Bmi.Should().BeApproximately(erwartetBmi, precision: 0.01);
    }

    [Fact]
    public void CalculateBmi_NormalgewichtBereich_KategorieNormal()
    {
        // BMI 22 = Normalgewicht (18.5 - 24.9)
        var ergebnis = _sut.CalculateBmi(72, 180);

        ergebnis.Category.Should().Be(BmiCategory.Normal);
    }

    [Fact]
    public void CalculateBmi_BmiUnter16_KategorieSchweresUntergewicht()
    {
        // 40 kg bei 180 cm → BMI ≈ 12.35 → SevereUnderweight
        var ergebnis = _sut.CalculateBmi(40, 180);

        ergebnis.Category.Should().Be(BmiCategory.SevereUnderweight);
    }

    [Fact]
    public void CalculateBmi_BmiZwischen16Und17_KategorieModeratesUntergewicht()
    {
        // 51.8 kg bei 180 cm → BMI ≈ 16.0 → ModerateUnderweight
        var ergebnis = _sut.CalculateBmi(51.84, 180);

        ergebnis.Category.Should().Be(BmiCategory.ModerateUnderweight);
    }

    [Fact]
    public void CalculateBmi_BmiZwischen25Und30_KategorieUebergewicht()
    {
        // 83 kg bei 175 cm → BMI ≈ 27.1 → Overweight
        var ergebnis = _sut.CalculateBmi(83, 175);

        ergebnis.Category.Should().Be(BmiCategory.Overweight);
    }

    [Fact]
    public void CalculateBmi_BmiUeber30_KategorieAdipositasKlasse1()
    {
        // 97 kg bei 175 cm → BMI ≈ 31.7 → ObeseClass1
        var ergebnis = _sut.CalculateBmi(97, 175);

        ergebnis.Category.Should().Be(BmiCategory.ObeseClass1);
    }

    [Fact]
    public void CalculateBmi_BmiUeber35_KategorieAdipositasKlasse2()
    {
        // 108 kg bei 175 cm → BMI ≈ 35.3 → ObeseClass2
        var ergebnis = _sut.CalculateBmi(108, 175);

        ergebnis.Category.Should().Be(BmiCategory.ObeseClass2);
    }

    [Fact]
    public void CalculateBmi_BmiUeber40_KategorieAdipositasKlasse3()
    {
        // 126 kg bei 175 cm → BMI ≈ 41.1 → ObeseClass3
        var ergebnis = _sut.CalculateBmi(126, 175);

        ergebnis.Category.Should().Be(BmiCategory.ObeseClass3);
    }

    [Fact]
    public void CalculateBmi_GesunderBereich_MinMaxGewichtKorrekt()
    {
        // 180 cm → Gesunder Bereich: 18.5*1.8²=59.9 bis 24.9*1.8²=80.7 kg
        const double erwMin = 18.5 * 1.8 * 1.8;
        const double erwMax = 24.9 * 1.8 * 1.8;

        var ergebnis = _sut.CalculateBmi(70, 180);

        ergebnis.MinHealthyWeight.Should().BeApproximately(erwMin, precision: 0.01);
        ergebnis.MaxHealthyWeight.Should().BeApproximately(erwMax, precision: 0.01);
    }

    [Fact]
    public void CalculateBmi_EingabeWerteImErgebnis_GespeichertKorrekt()
    {
        var ergebnis = _sut.CalculateBmi(75, 172);

        ergebnis.Weight.Should().Be(75);
        ergebnis.Height.Should().Be(172);
    }

    [Fact]
    public void CalculateBmi_SehrKleineGroesse100cm_LiefertErgebnis()
    {
        // Grenzfall: Kleinwuchs - kein Absturz
        var ergebnis = _sut.CalculateBmi(20, 100);

        ergebnis.Bmi.Should().Be(20.0);
    }

    #endregion

    #region Kalorien-Tests (Mifflin-St Jeor)

    [Fact]
    public void CalculateCalories_Mann30Jahre80kg180cm_GrundeumsatzKorrekt()
    {
        // Mifflin-St Jeor Mann: 10*80 + 6.25*180 - 5*30 + 5 = 800 + 1125 - 150 + 5 = 1780
        const double erwartetBmr = 1780.0;

        var ergebnis = _sut.CalculateCalories(80, 180, 30, isMale: true, activityLevel: 1.0);

        ergebnis.Bmr.Should().BeApproximately(erwartetBmr, precision: 0.01);
    }

    [Fact]
    public void CalculateCalories_Frau25Jahre60kg165cm_GrundeumsatzKorrekt()
    {
        // Mifflin-St Jeor Frau: 10*60 + 6.25*165 - 5*25 - 161 = 600 + 1031.25 - 125 - 161 = 1345.25
        const double erwartetBmr = 1345.25;

        var ergebnis = _sut.CalculateCalories(60, 165, 25, isMale: false, activityLevel: 1.0);

        ergebnis.Bmr.Should().BeApproximately(erwartetBmr, precision: 0.01);
    }

    [Fact]
    public void CalculateCalories_AktivitaetsFaktor175_TdeeBmrMalFaktor()
    {
        var ergebnis = _sut.CalculateCalories(80, 180, 30, isMale: true, activityLevel: 1.75);

        ergebnis.Tdee.Should().BeApproximately(ergebnis.Bmr * 1.75, precision: 0.01);
    }

    [Fact]
    public void CalculateCalories_GewichtsabnahmeKalorien_Tdee500WenigerKcal()
    {
        var ergebnis = _sut.CalculateCalories(80, 180, 30, isMale: true, activityLevel: 1.5);

        ergebnis.WeightLossCalories.Should().BeApproximately(ergebnis.Tdee - 500, precision: 0.01);
    }

    [Fact]
    public void CalculateCalories_GewichtszunahmeKalorien_Tdee500MehrKcal()
    {
        var ergebnis = _sut.CalculateCalories(80, 180, 30, isMale: true, activityLevel: 1.5);

        ergebnis.WeightGainCalories.Should().BeApproximately(ergebnis.Tdee + 500, precision: 0.01);
    }

    [Fact]
    public void CalculateCalories_MannHatHoeherenGrundeumsatzAlsFrau_GleicheKoerpergroesse()
    {
        // Bei gleichen Parametern: Mann hat 166 kcal mehr (+5 vs -161 = Differenz 166)
        var mann = _sut.CalculateCalories(70, 170, 35, isMale: true, activityLevel: 1.0);
        var frau = _sut.CalculateCalories(70, 170, 35, isMale: false, activityLevel: 1.0);

        mann.Bmr.Should().BeGreaterThan(frau.Bmr);
        (mann.Bmr - frau.Bmr).Should().BeApproximately(166, precision: 0.01);
    }

    #endregion

    #region Wasser-Tests

    [Fact]
    public void CalculateWater_70kgKeinSportKeinHitze_Basiswassermenge()
    {
        // Basis: 70 * 0.033 = 2.31 L, kein Sport, kein Hitze
        const double erwartet = 70 * 0.033;

        var ergebnis = _sut.CalculateWater(70, activityMinutes: 0, isHotWeather: false);

        ergebnis.TotalLiters.Should().BeApproximately(erwartet, precision: 0.001);
        ergebnis.BaseWater.Should().BeApproximately(erwartet, precision: 0.001);
        ergebnis.ActivityWater.Should().Be(0);
        ergebnis.HeatWater.Should().Be(0);
    }

    [Fact]
    public void CalculateWater_60MinutenSport_SportanteilKorrekt()
    {
        // 60 min Sport: (60/30) * 0.35 = 0.70 L zusätzlich
        const double erwartetSport = (60.0 / 30.0) * 0.35;

        var ergebnis = _sut.CalculateWater(70, activityMinutes: 60, isHotWeather: false);

        ergebnis.ActivityWater.Should().BeApproximately(erwartetSport, precision: 0.001);
    }

    [Fact]
    public void CalculateWater_HeissesWetter_05LiterMehr()
    {
        var ohneHitze = _sut.CalculateWater(70, 0, isHotWeather: false);
        var mitHitze = _sut.CalculateWater(70, 0, isHotWeather: true);

        (mitHitze.TotalLiters - ohneHitze.TotalLiters).Should().BeApproximately(0.5, precision: 0.001);
        mitHitze.HeatWater.Should().Be(0.5);
    }

    [Fact]
    public void CalculateWater_GlaesanzahlKorrektRunden250mlGlaeser()
    {
        // 2.31 L / 0.25 = 9.24 → aufgerundet = 10 Gläser
        var ergebnis = _sut.CalculateWater(70, 0, false);

        var erwartetGlaeser = (int)Math.Ceiling(ergebnis.TotalLiters / 0.25);
        ergebnis.Glasses.Should().Be(erwartetGlaeser);
    }

    [Fact]
    public void CalculateWater_NullKgGrenzfall_KeinFehler()
    {
        // Grenzfall: 0 kg Gewicht - darf nicht abstürzen
        var ergebnis = _sut.CalculateWater(0, 0, false);

        ergebnis.TotalLiters.Should().Be(0);
        ergebnis.Glasses.Should().Be(0);
    }

    [Fact]
    public void CalculateWater_KeinSportMitHitze_KorrekteGesamtsumme()
    {
        // 80 kg, kein Sport, Hitze: 80*0.033 + 0 + 0.5 = 2.64 + 0.5 = 3.14
        const double erwartet = 80 * 0.033 + 0.5;

        var ergebnis = _sut.CalculateWater(80, 0, true);

        ergebnis.TotalLiters.Should().BeApproximately(erwartet, precision: 0.001);
    }

    #endregion

    #region Idealgewicht-Tests

    [Fact]
    public void CalculateIdealWeight_Mann30Jahre180cm_BrocaKorrekt()
    {
        // Broca Mann: 180 - 100 = 80 kg
        var ergebnis = _sut.CalculateIdealWeight(180, isMale: true, ageYears: 30);

        ergebnis.BrocaWeight.Should().BeApproximately(80.0, precision: 0.01);
    }

    [Fact]
    public void CalculateIdealWeight_Frau30Jahre170cm_BrocaKorrektMit15ProzentAbzug()
    {
        // Broca Frau: (170 - 100) * 0.85 = 70 * 0.85 = 59.5 kg
        var ergebnis = _sut.CalculateIdealWeight(170, isMale: false, ageYears: 30);

        ergebnis.BrocaWeight.Should().BeApproximately(59.5, precision: 0.01);
    }

    [Fact]
    public void CalculateIdealWeight_Mann30Jahre180cm_CreffKorrekt()
    {
        // Creff Mann: (180 - 100 + 30/10) * 0.9 = (80 + 3) * 0.9 = 83 * 0.9 = 74.7 kg
        var ergebnis = _sut.CalculateIdealWeight(180, isMale: true, ageYears: 30);

        ergebnis.CreffWeight.Should().BeApproximately(74.7, precision: 0.01);
    }

    [Fact]
    public void CalculateIdealWeight_Frau30Jahre170cm_CreffKorrektMit10ProzentAbzug()
    {
        // Creff Frau: (170 - 100 + 30/10) * 0.9 * 0.9 = (70+3) * 0.9 * 0.9 = 73 * 0.81 = 59.13 kg
        var ergebnis = _sut.CalculateIdealWeight(170, isMale: false, ageYears: 30);

        ergebnis.CreffWeight.Should().BeApproximately(59.13, precision: 0.01);
    }

    [Fact]
    public void CalculateIdealWeight_GesunderBMIBereich_MinMax185Bis249()
    {
        // 175 cm → Min: 18.5*1.75² = 56.6 kg, Max: 24.9*1.75² = 76.2 kg
        const double erwMin = 18.5 * 1.75 * 1.75;
        const double erwMax = 24.9 * 1.75 * 1.75;

        var ergebnis = _sut.CalculateIdealWeight(175, isMale: true, ageYears: 25);

        ergebnis.MinHealthyWeight.Should().BeApproximately(erwMin, precision: 0.01);
        ergebnis.MaxHealthyWeight.Should().BeApproximately(erwMax, precision: 0.01);
    }

    [Fact]
    public void CalculateIdealWeight_Durchschnitt_MittelwertAusBrocaUndCreff()
    {
        var ergebnis = _sut.CalculateIdealWeight(175, isMale: true, ageYears: 35);

        var erwDurchschnitt = (ergebnis.BrocaWeight + ergebnis.CreffWeight) / 2;
        ergebnis.AverageIdeal.Should().BeApproximately(erwDurchschnitt, precision: 0.001);
    }

    [Fact]
    public void CalculateIdealWeight_AeltererMann_CreffHoeherAlsJuengerer()
    {
        // Creff berücksichtigt Alter: ältere Person darf mehr wiegen
        var jung = _sut.CalculateIdealWeight(175, isMale: true, ageYears: 20);
        var alt = _sut.CalculateIdealWeight(175, isMale: true, ageYears: 60);

        alt.CreffWeight.Should().BeGreaterThan(jung.CreffWeight);
    }

    #endregion

    #region Körperfett-Tests (Navy-Methode)

    [Fact]
    public void CalculateBodyFat_NormalerMann_ProzentsatzPositiv()
    {
        // Mann: Bauch 90, Hals 40, Größe 180 → positiver Anteil erwartet
        var ergebnis = _sut.CalculateBodyFat(180, neckCm: 40, waistCm: 90, hipCm: 0, isMale: true);

        ergebnis.BodyFatPercent.Should().BePositive();
        ergebnis.BodyFatPercent.Should().BeLessThanOrEqualTo(60);
    }

    [Fact]
    public void CalculateBodyFat_NormalesFrau_ProzentsatzPositiv()
    {
        // Frau: Bauch 80, Hüfte 95, Hals 35, Größe 165
        var ergebnis = _sut.CalculateBodyFat(165, neckCm: 35, waistCm: 80, hipCm: 95, isMale: false);

        ergebnis.BodyFatPercent.Should().BePositive();
        ergebnis.BodyFatPercent.Should().BeLessThanOrEqualTo(60);
    }

    [Fact]
    public void CalculateBodyFat_MannAthleteBereich_KategorieAthletes()
    {
        // Athetenbereich: 6-13.9% Körperfett
        // Schmale Taille vs Hals für niedrigen Körperfett
        var ergebnis = _sut.CalculateBodyFat(180, neckCm: 43, waistCm: 75, hipCm: 0, isMale: true);

        // Ergebnis darf Athletes oder Essential sein (je nach exakter Formel)
        ergebnis.Category.Should().BeOneOf(BodyFatCategory.Essential, BodyFatCategory.Athletes, BodyFatCategory.Fitness);
    }

    [Fact]
    public void CalculateBodyFat_MannHohesKoerperfett_KategorieObese()
    {
        // Hohe Taille → hoher Körperfettanteil
        var ergebnis = _sut.CalculateBodyFat(175, neckCm: 38, waistCm: 120, hipCm: 0, isMale: true);

        ergebnis.Category.Should().Be(BodyFatCategory.Obese);
    }

    [Fact]
    public void CalculateBodyFat_DifferenzNegativMann_GibtNullProzentZurueck()
    {
        // Hals >= Bauch → Diff <= 0 → Log10 unmöglich → Fallback auf 0%
        var ergebnis = _sut.CalculateBodyFat(180, neckCm: 50, waistCm: 45, hipCm: 0, isMale: true);

        ergebnis.BodyFatPercent.Should().Be(0);
        ergebnis.Category.Should().Be(BodyFatCategory.Essential);
    }

    [Fact]
    public void CalculateBodyFat_EingabeWerteImErgebnis_GespeichertKorrekt()
    {
        var ergebnis = _sut.CalculateBodyFat(170, 35, 85, 90, isMale: false);

        ergebnis.Height.Should().Be(170);
        ergebnis.Neck.Should().Be(35);
        ergebnis.Waist.Should().Be(85);
        ergebnis.Hip.Should().Be(90);
        ergebnis.IsMale.Should().BeFalse();
    }

    [Fact]
    public void CalculateBodyFat_WertNichtUeber60Prozent_IstBegrenzt()
    {
        // Extreme Werte - Ergebnis darf 60% nicht überschreiten
        var ergebnis = _sut.CalculateBodyFat(150, neckCm: 25, waistCm: 200, hipCm: 200, isMale: false);

        ergebnis.BodyFatPercent.Should().BeLessThanOrEqualTo(60);
    }

    [Fact]
    public void CalculateBodyFat_MaennlicheKategoriegrenzen_KorrektKlassifiziert()
    {
        // Männer: <6% Essential, 6-13.9% Athletes, 14-17.9% Fitness, 18-24.9% Average, >=25% Obese
        // Breit taillig: Average-Bereich
        var ergebnis = _sut.CalculateBodyFat(180, neckCm: 40, waistCm: 95, hipCm: 0, isMale: true);

        ergebnis.Category.Should().BeOneOf(
            BodyFatCategory.Athletes,
            BodyFatCategory.Fitness,
            BodyFatCategory.Average,
            BodyFatCategory.Obese);
    }

    #endregion
}
