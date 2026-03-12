using FluentAssertions;
using MeineApps.CalcLib;
using Xunit;

namespace MeineApps.CalcLib.Tests;

/// <summary>
/// Tests für ExpressionParser: Operator-Präzedenz, Klammern, Kettenausdrücke,
/// unäres Minus, implizite Multiplikation, Fehlerbehandlung.
/// </summary>
public class ExpressionParserTests
{
    private readonly CalculatorEngine _engine = new();
    private ExpressionParser CreateSut() => new(_engine);

    #region Leere und triviale Eingaben

    [Fact]
    public void Evaluate_LeerenString_GibtNull()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("");
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(0);
    }

    [Fact]
    public void Evaluate_NurLeerzeichen_GibtNull()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("   ");
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(0);
    }

    [Fact]
    public void Evaluate_EineZahl_GibtDieZahl()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("42");
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    #endregion

    #region Operator-Präzedenz

    [Fact]
    public void Evaluate_AdditionUndMultiplikation_MultiplikationVorrangig()
    {
        // 2 + 3 × 4 = 14, NICHT 20
        var sut = CreateSut();
        var result = sut.Evaluate("2+3×4");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(14, 1e-10);
    }

    [Fact]
    public void Evaluate_SubtraktionUndDivision_DivisionVorrangig()
    {
        // 10 - 6 ÷ 2 = 7, NICHT 2
        var sut = CreateSut();
        var result = sut.Evaluate("10-6÷2");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(7, 1e-10);
    }

    [Fact]
    public void Evaluate_PotenzVorMultiplikation_PotenzVorrangig()
    {
        // 2 + 3^2 = 11, NICHT 25
        var sut = CreateSut();
        var result = sut.Evaluate("2+3^2");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(11, 1e-10);
    }

    [Fact]
    public void Evaluate_PotenzRechtsassoziativ_KorrekteReihenfolge()
    {
        // 2^3^2 = 2^(3^2) = 2^9 = 512 (rechtsassoziativ)
        var sut = CreateSut();
        var result = sut.Evaluate("2^3^2");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(512, 1e-10);
    }

    [Fact]
    public void Evaluate_GleicheOperatoren_LinksassoziativAuswertung()
    {
        // 10 - 3 - 2 = 5 (linksassoziativ, also (10-3)-2=5)
        var sut = CreateSut();
        var result = sut.Evaluate("10-3-2");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(5, 1e-10);
    }

    #endregion

    #region Klammern

    [Fact]
    public void Evaluate_KlammernAendernPraezedenz_KorrekteReihenfolge()
    {
        // (2 + 3) × 4 = 20, nicht 14
        var sut = CreateSut();
        var result = sut.Evaluate("(2+3)×4");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(20, 1e-10);
    }

    [Fact]
    public void Evaluate_GeschachtelteKlammern_KorektesErgebnis()
    {
        // ((2+3) × (1+1)) = 10
        var sut = CreateSut();
        var result = sut.Evaluate("((2+3)×(1+1))");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(10, 1e-10);
    }

    [Fact]
    public void Evaluate_FehlendeSchliessungsKlammer_GibtFehler()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("(2+3");
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ZuVieleSchliessungsKlammern_GibtFehler()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("2+3)");
        result.IsError.Should().BeTrue();
    }

    #endregion

    #region Unäres Minus

    [Fact]
    public void Evaluate_UnaeresMinusAmAnfang_NegativeZahl()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("-5");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(-5, 1e-10);
    }

    [Fact]
    public void Evaluate_DoppeltesMinusAmAnfang_PositiveZahl()
    {
        // --5 = 5
        var sut = CreateSut();
        var result = sut.Evaluate("--5");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(5, 1e-10);
    }

    [Fact]
    public void Evaluate_DreifachesMinusAmAnfang_NegativeZahl()
    {
        // ---5 = -5
        var sut = CreateSut();
        var result = sut.Evaluate("---5");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(-5, 1e-10);
    }

    [Fact]
    public void Evaluate_NegativeZahlInKlammern_KorektesErgebnis()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("(-5)+3");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(-2, 1e-10);
    }

    #endregion

    #region Implizite Multiplikation

    [Fact]
    public void Evaluate_ZahlNachKlammer_ImpliziteMultiplikation()
    {
        // (5+3)2 = 16
        var sut = CreateSut();
        var result = sut.Evaluate("(5+3)×2");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(16, 1e-10);
    }

    [Fact]
    public void Evaluate_ZweiKlammernHintereinander_ImpliziteMultiplikation()
    {
        // (5+3)(2+1) = 24
        var sut = CreateSut();
        var result = sut.Evaluate("(5+3)×(2+1)");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(24, 1e-10);
    }

    #endregion

    #region Operatoren (Alternativ-Notation)

    [Fact]
    public void Evaluate_AltMinusZeichen_WirdAlsSubtraktionErkannt()
    {
        // Minuszeichen "−" (Unicode) als Alternative zu "-"
        var sut = CreateSut();
        var result = sut.Evaluate("5−3");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(2, 1e-10);
    }

    [Fact]
    public void Evaluate_AsteriskAlsMultiplikation_KorektesErgebnis()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("3*4");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(12, 1e-10);
    }

    [Fact]
    public void Evaluate_SlashAlsDivision_KorektesErgebnis()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("10/4");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(2.5, 1e-10);
    }

    [Fact]
    public void Evaluate_ModOperator_GibtRest()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("10 mod 3");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(1, 1e-10);
    }

    #endregion

    #region Dezimalzahlen

    [Fact]
    public void Evaluate_KommaAlsDezimatrenner_WirdAkzeptiert()
    {
        // Komma wird zu Punkt normalisiert
        var sut = CreateSut();
        var result = sut.Evaluate("1,5+1,5");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(3.0, 1e-10);
    }

    [Fact]
    public void Evaluate_ScientificNotation_WirdAkzeptiert()
    {
        // 1E2 = 100
        var sut = CreateSut();
        var result = sut.Evaluate("1E2+0");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(100, 1e-10);
    }

    #endregion

    #region Kettenausdrücke

    [Fact]
    public void Evaluate_LangerKettenausdruck_KorektesErgebnis()
    {
        // 1 + 2 + 3 + 4 + 5 = 15
        var sut = CreateSut();
        var result = sut.Evaluate("1+2+3+4+5");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(15, 1e-10);
    }

    [Fact]
    public void Evaluate_GemischterAusdruck_KorektesErgebnis()
    {
        // 2 * (3 + 4) - 5 / 5 = 13
        var sut = CreateSut();
        var result = sut.Evaluate("2*(3+4)-5/5");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(13, 1e-10);
    }

    #endregion

    #region Fehlerbehandlung

    [Fact]
    public void Evaluate_DivisionDurchNull_GibtFehler()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("5÷0");
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_UngueltigesZeichen_GibtFehler()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("5$3");
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ZweiOperatorenHintereinander_GibtFehler()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("5++3");
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_EndetMitOperator_GibtFehler()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("5+");
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_StartetMitBinaerOperator_GibtFehler()
    {
        // Plus am Anfang (nicht unär) ist ungültig
        var sut = CreateSut();
        var result = sut.Evaluate("+5");
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ZuGrosseZahl_GibtFehler()
    {
        // 1e309 = Infinity → Fehler
        var sut = CreateSut();
        var result = sut.Evaluate("1e309");
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LeeresErkennungsToken_GibtFehler()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("abc");
        result.IsError.Should().BeTrue();
    }

    #endregion

    #region Leerzeichen

    [Fact]
    public void Evaluate_AusdruckMitLeerzeichen_WirdKorrektAusgwertet()
    {
        var sut = CreateSut();
        var result = sut.Evaluate("2 + 3 × 4");
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(14, 1e-10);
    }

    #endregion
}
