using FluentAssertions;
using MeineApps.CalcLib;
using Xunit;

namespace MeineApps.CalcLib.Tests;

/// <summary>
/// Tests für CalculatorEngine: Grundrechenarten, wissenschaftliche Funktionen,
/// Grenzfälle (Division durch 0, negative Wurzel, Overflow, NaN).
/// </summary>
public class CalculatorEngineTests
{
    private readonly CalculatorEngine _sut = new();

    #region Grundrechenarten

    [Fact]
    public void Add_ZweiPositiveZahlen_GibtKorrektesSumme()
    {
        var ergebnis = _sut.Add(3, 5);
        ergebnis.Should().Be(8);
    }

    [Fact]
    public void Add_NegativeUndPositiveZahl_GibtKorrektesSumme()
    {
        var ergebnis = _sut.Add(-3, 5);
        ergebnis.Should().Be(2);
    }

    [Fact]
    public void Add_BeideMal0_GibtNull()
    {
        var ergebnis = _sut.Add(0, 0);
        ergebnis.Should().Be(0);
    }

    [Fact]
    public void Subtract_GroessereVonKleinerer_GibtNegativesErgebnis()
    {
        var ergebnis = _sut.Subtract(3, 10);
        ergebnis.Should().Be(-7);
    }

    [Fact]
    public void Multiply_ZweiPositiveZahlen_GibtKorektesProdukt()
    {
        var ergebnis = _sut.Multiply(4, 5);
        ergebnis.Should().Be(20);
    }

    [Fact]
    public void Multiply_MitNull_GibtNull()
    {
        var ergebnis = _sut.Multiply(12345, 0);
        ergebnis.Should().Be(0);
    }

    [Fact]
    public void Multiply_NegativMalNegativ_GibtPositiv()
    {
        var ergebnis = _sut.Multiply(-3, -4);
        ergebnis.Should().Be(12);
    }

    [Fact]
    public void Divide_NormaleZahlen_GibtKorektesErgebnis()
    {
        var result = _sut.Divide(10, 4);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(2.5, 1e-10);
    }

    [Fact]
    public void Divide_DurchNull_GibtFehler()
    {
        var result = _sut.Divide(5, 0);
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("zero");
    }

    [Fact]
    public void Divide_DurchSehrKleineZahl_GibtFehler()
    {
        // Werte innerhalb EPSILON (1e-15) gelten als null
        var result = _sut.Divide(5, 1e-16);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Divide_NullDurchZahl_GibtNull()
    {
        var result = _sut.Divide(0, 5);
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(0);
    }

    [Fact]
    public void Negate_PositiveZahl_GibtNegativ()
    {
        _sut.Negate(5).Should().Be(-5);
    }

    [Fact]
    public void Negate_NegativeZahl_GibtPositiv()
    {
        _sut.Negate(-5).Should().Be(5);
    }

    [Fact]
    public void Negate_Null_GibtNull()
    {
        _sut.Negate(0).Should().Be(0);
    }

    #endregion

    #region Erweiterte Funktionen

    [Fact]
    public void Percentage_HundertProzentVonZahl_GibtZahl()
    {
        var ergebnis = _sut.Percentage(200, 100);
        ergebnis.Should().Be(200);
    }

    [Fact]
    public void Percentage_ZehnProzentVon200_GibtZwanzig()
    {
        var ergebnis = _sut.Percentage(200, 10);
        ergebnis.Should().Be(20);
    }

    [Fact]
    public void SquareRoot_Vier_GibtZwei()
    {
        var result = _sut.SquareRoot(4);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(2.0, 1e-10);
    }

    [Fact]
    public void SquareRoot_Null_GibtNull()
    {
        var result = _sut.SquareRoot(0);
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(0);
    }

    [Fact]
    public void SquareRoot_NegativeZahl_GibtFehler()
    {
        var result = _sut.SquareRoot(-1);
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("negative");
    }

    [Fact]
    public void Square_DreiMalDrei_GibtNeun()
    {
        _sut.Square(3).Should().Be(9);
    }

    [Fact]
    public void Square_NegativeZahl_GibtPositiv()
    {
        _sut.Square(-4).Should().Be(16);
    }

    [Fact]
    public void Reciprocal_Zwei_GibtEinHalb()
    {
        var result = _sut.Reciprocal(2);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(0.5, 1e-10);
    }

    [Fact]
    public void Reciprocal_Null_GibtFehler()
    {
        var result = _sut.Reciprocal(0);
        result.IsError.Should().BeTrue();
    }

    #endregion

    #region Trigonometrische Funktionen

    [Fact]
    public void Sin_Null_GibtNull()
    {
        _sut.Sin(0).Should().BeApproximately(0, 1e-10);
    }

    [Fact]
    public void Sin_HalberPi_GibtEins()
    {
        _sut.Sin(Math.PI / 2).Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void Cos_Null_GibtEins()
    {
        _sut.Cos(0).Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void Cos_Pi_GibtMinusEins()
    {
        _sut.Cos(Math.PI).Should().BeApproximately(-1.0, 1e-10);
    }

    [Fact]
    public void Tan_ViertelPi_GibtEins()
    {
        var result = _sut.Tan(Math.PI / 4);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void Tan_HalberPi_GibtFehler()
    {
        // tan(π/2) ist nicht definiert (Polstelle), cos(π/2) ≈ 0
        var result = _sut.Tan(Math.PI / 2);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Asin_Eins_GibtHalbenPi()
    {
        var result = _sut.Asin(1);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(Math.PI / 2, 1e-10);
    }

    [Fact]
    public void Asin_WertGroesserEins_GibtFehler()
    {
        var result = _sut.Asin(1.1);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Asin_WertKleinerMinusEins_GibtFehler()
    {
        var result = _sut.Asin(-1.5);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Acos_Eins_GibtNull()
    {
        var result = _sut.Acos(1);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(0, 1e-10);
    }

    [Fact]
    public void Acos_WertAusserhalbBereich_GibtFehler()
    {
        var result = _sut.Acos(2);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Atan_Eins_GibtViertelPi()
    {
        _sut.Atan(1).Should().BeApproximately(Math.PI / 4, 1e-10);
    }

    #endregion

    #region Logarithmen und Potenzen

    [Fact]
    public void Log_Hundert_GibtZwei()
    {
        var result = _sut.Log(100);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(2.0, 1e-10);
    }

    [Fact]
    public void Log_NullOderNegativ_GibtFehler()
    {
        _sut.Log(0).IsError.Should().BeTrue();
        _sut.Log(-1).IsError.Should().BeTrue();
    }

    [Fact]
    public void Ln_EulerZahl_GibtEins()
    {
        var result = _sut.Ln(Math.E);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void Ln_NullOderNegativ_GibtFehler()
    {
        _sut.Ln(0).IsError.Should().BeTrue();
        _sut.Ln(-5).IsError.Should().BeTrue();
    }

    [Fact]
    public void Power_ZweiHochDrei_GibtAcht()
    {
        var result = _sut.Power(2, 3);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(8.0, 1e-10);
    }

    [Fact]
    public void Power_NegativeGeradeWurzel_GibtFehler()
    {
        // (-1)^0.5 = NaN
        var result = _sut.Power(-1, 0.5);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Power_ZweiHochSehrGrosseZahl_GibtFehler()
    {
        // 2^1e308 = Infinity
        var result = _sut.Power(2, 1e308);
        result.IsError.Should().BeTrue();
    }

    #endregion

    #region Fakultät

    [Fact]
    public void Factorial_Null_GibtEins()
    {
        var result = _sut.Factorial(0);
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(1);
    }

    [Fact]
    public void Factorial_Eins_GibtEins()
    {
        var result = _sut.Factorial(1);
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(1);
    }

    [Fact]
    public void Factorial_Fuenf_GibtHundertzwanzig()
    {
        var result = _sut.Factorial(5);
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(120);
    }

    [Fact]
    public void Factorial_Zehn_GibtKorektesErgebnis()
    {
        var result = _sut.Factorial(10);
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(3628800);
    }

    [Fact]
    public void Factorial_NegativeZahl_GibtFehler()
    {
        var result = _sut.Factorial(-1);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Factorial_SehrGrosseZahl_GibtOverflowFehler()
    {
        // 200! übersteigt double-Bereich
        var result = _sut.Factorial(200);
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Overflow");
    }

    #endregion

    #region Hyperbolische Funktionen

    [Fact]
    public void Sinh_Null_GibtNull()
    {
        _sut.Sinh(0).Should().BeApproximately(0, 1e-10);
    }

    [Fact]
    public void Cosh_Null_GibtEins()
    {
        _sut.Cosh(0).Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void Tanh_Null_GibtNull()
    {
        var result = _sut.Tanh(0);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(0, 1e-10);
    }

    #endregion

    #region Exponentialfunktionen

    [Fact]
    public void Exp_Null_GibtEins()
    {
        var result = _sut.Exp(0);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void Exp_SehrGrosserWert_GibtFehler()
    {
        // e^1000 = Infinity
        var result = _sut.Exp(1000);
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Overflow");
    }

    [Fact]
    public void Exp10_Zwei_GibtHundert()
    {
        var result = _sut.Exp10(2);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(100.0, 1e-10);
    }

    #endregion

    #region Kubik und n-te Wurzel

    [Fact]
    public void Cube_Drei_GibtSiebenundzwanzig()
    {
        _sut.Cube(3).Should().Be(27);
    }

    [Fact]
    public void CubeRoot_SiebenundzwanzigPositiv_GibtDrei()
    {
        _sut.CubeRoot(27).Should().BeApproximately(3.0, 1e-10);
    }

    [Fact]
    public void CubeRoot_NegativeZahl_GibtNegativesErgebnis()
    {
        // Kubikwurzel von -8 = -2 (gültig, da ungerade Wurzel)
        _sut.CubeRoot(-8).Should().BeApproximately(-2.0, 1e-10);
    }

    [Fact]
    public void NthRoot_AchtMitExponent3_GibtZwei()
    {
        var result = _sut.NthRoot(8, 3);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(2.0, 1e-10);
    }

    [Fact]
    public void NthRoot_ExponentNull_GibtFehler()
    {
        var result = _sut.NthRoot(8, 0);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void NthRoot_NegativeZahlMitGeradenExponent_GibtFehler()
    {
        // Gerade Wurzel aus negativer Zahl ist nicht reell
        var result = _sut.NthRoot(-4, 2);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void NthRoot_NegativeZahlMitUngeradenExponent_GibtNegativesErgebnis()
    {
        // Kubikwurzel von -8 = -2
        var result = _sut.NthRoot(-8, 3);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(-2.0, 1e-10);
    }

    #endregion

    #region Absolutwert und Modulo

    [Fact]
    public void Abs_NegativeZahl_GibtPositiv()
    {
        _sut.Abs(-7.5).Should().Be(7.5);
    }

    [Fact]
    public void Abs_Null_GibtNull()
    {
        _sut.Abs(0).Should().Be(0);
    }

    [Fact]
    public void Mod_TenModDrei_GibtEins()
    {
        var result = _sut.Mod(10, 3);
        result.IsError.Should().BeFalse();
        result.Value.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void Mod_DurchNull_GibtFehler()
    {
        var result = _sut.Mod(10, 0);
        result.IsError.Should().BeTrue();
    }

    #endregion

    #region Winkelkonvertierung

    [Fact]
    public void DegreesToRadians_180Grad_GibtPi()
    {
        _sut.DegreesToRadians(180).Should().BeApproximately(Math.PI, 1e-10);
    }

    [Fact]
    public void RadiansToDegrees_PiRadiant_Gibt180Grad()
    {
        _sut.RadiansToDegrees(Math.PI).Should().BeApproximately(180.0, 1e-10);
    }

    [Fact]
    public void DegreesToRadians_NullGrad_GibtNull()
    {
        _sut.DegreesToRadians(0).Should().Be(0);
    }

    #endregion

    #region Konstanten

    [Fact]
    public void Pi_GibtMathPi()
    {
        _sut.Pi.Should().BeApproximately(Math.PI, 1e-10);
    }

    [Fact]
    public void E_GibtMathE()
    {
        _sut.E.Should().BeApproximately(Math.E, 1e-10);
    }

    #endregion

    #region CalculationResult-Struktur

    [Fact]
    public void CalculationResult_Success_IsErrorFalse()
    {
        var result = CalculationResult.Success(42);
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(42);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CalculationResult_Error_IsErrorTrue()
    {
        var result = CalculationResult.Error("Fehler");
        result.IsError.Should().BeTrue();
        result.Value.Should().Be(double.NaN);
        result.ErrorMessage.Should().Be("Fehler");
    }

    #endregion
}
