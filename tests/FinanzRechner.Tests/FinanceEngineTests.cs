using FluentAssertions;
using Xunit;
using FinanzRechner.Models;

namespace FinanzRechner.Tests;

/// <summary>
/// Tests für die FinanceEngine - alle 6 Rechner-Funktionen.
/// Prüft Korrektheit, Grenzfälle und Fehlerbehandlung.
/// </summary>
public class FinanceEngineTests
{
    // Testinstanz wird pro Test neu erstellt (keine shared State)
    private readonly FinanceEngine _sut = new();

    #region Zinseszins (CompoundInterest)

    [Fact]
    public void CalculateCompoundInterest_StandardWerte_GibtKorrektesErgebnis()
    {
        // Vorbereitung: 1000€ bei 5% p.a. über 10 Jahre, jährliche Verzinsung
        // Erwartung: 1000 * (1 + 0.05)^10 = 1628.894...
        var erwartetFinalAmount = 1000 * Math.Pow(1.05, 10);

        // Ausführung
        var ergebnis = _sut.CalculateCompoundInterest(1000, 5, 10, 1);

        // Prüfung
        ergebnis.FinalAmount.Should().BeApproximately(erwartetFinalAmount, 0.001);
        ergebnis.InterestEarned.Should().BeApproximately(erwartetFinalAmount - 1000, 0.001);
        ergebnis.Principal.Should().Be(1000);
        ergebnis.AnnualRate.Should().Be(5);
        ergebnis.Years.Should().Be(10);
    }

    [Fact]
    public void CalculateCompoundInterest_MonatlicheVerzinsung_GibtHoehereRendite()
    {
        // Vorbereitung: Monatliche Verzinsung (12x pro Jahr) muss mehr ergeben als jährliche
        var ergebnisJaehrlich = _sut.CalculateCompoundInterest(1000, 5, 10, 1);
        var ergebnisMonatlich = _sut.CalculateCompoundInterest(1000, 5, 10, 12);

        // Prüfung: Häufigere Verzinsung → höherer Endbetrag
        ergebnisMonatlich.FinalAmount.Should().BeGreaterThan(ergebnisJaehrlich.FinalAmount);
    }

    [Fact]
    public void CalculateCompoundInterest_NullProzentZins_GibtNurKapitalZurueck()
    {
        // Vorbereitung: 0% Zinsen → Kapital bleibt unverändert
        var ergebnis = _sut.CalculateCompoundInterest(5000, 0, 20, 1);

        // Prüfung
        ergebnis.FinalAmount.Should().BeApproximately(5000, 0.001);
        ergebnis.InterestEarned.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void CalculateCompoundInterest_NullJahre_WirftArgumentException()
    {
        // Ausführung & Prüfung
        var aktion = () => _sut.CalculateCompoundInterest(1000, 5, 0, 1);
        aktion.Should().Throw<ArgumentException>().WithMessage("*Jahre*");
    }

    [Fact]
    public void CalculateCompoundInterest_NegativeJahre_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateCompoundInterest(1000, 5, -1, 1);
        aktion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateCompoundInterest_NullZinsperiodenProJahr_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateCompoundInterest(1000, 5, 10, 0);
        aktion.Should().Throw<ArgumentException>().WithMessage("*Zinsperioden*");
    }

    [Fact]
    public void CalculateCompoundInterest_NegativesKapital_GibtNegativenEndbetrag()
    {
        // Vorbereitung: Negatives Startkapital (z.B. Schulden)
        var ergebnis = _sut.CalculateCompoundInterest(-1000, 5, 10, 1);

        // Prüfung: Endbetrag ist negativ (konsistente Mathematik)
        ergebnis.FinalAmount.Should().BeLessThan(0);
    }

    [Fact]
    public void CalculateCompoundInterest_EinJahrEinmaligVerzinst_ExaktesProzentergebnis()
    {
        // Vorbereitung: 1 Jahr, 10% → exakt 1100
        var ergebnis = _sut.CalculateCompoundInterest(1000, 10, 1, 1);

        ergebnis.FinalAmount.Should().BeApproximately(1100, 0.001);
        ergebnis.InterestEarned.Should().BeApproximately(100, 0.001);
    }

    #endregion

    #region Sparplan (SavingsPlan)

    [Fact]
    public void CalculateSavingsPlan_MonatlicherBeitrag_BerechnetKorrektenEndbetrag()
    {
        // Vorbereitung: 100€/Monat, 5% p.a., 10 Jahre, kein Anfangsguthaben
        // Formel: 100 * ((1.004167^120 - 1) / 0.004167) ≈ 15.528
        var ergebnis = _sut.CalculateSavingsPlan(100, 5, 10);

        // Prüfung
        ergebnis.FinalAmount.Should().BeGreaterThan(12000); // mehr als reine Einzahlungen (12000)
        ergebnis.TotalDeposits.Should().BeApproximately(12000, 0.001);
        ergebnis.InterestEarned.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateSavingsPlan_NullProzentZins_FinalAmountGleichEinzahlungen()
    {
        // Vorbereitung: Kein Zins → Endbetrag = Summe aller Einzahlungen
        var ergebnis = _sut.CalculateSavingsPlan(200, 0, 5);

        // Prüfung: 200 * 60 Monate = 12000
        ergebnis.FinalAmount.Should().BeApproximately(12000, 0.001);
        ergebnis.InterestEarned.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void CalculateSavingsPlan_MitAnfangsguthaben_ErhoehtEndwert()
    {
        // Vorbereitung: Gleiche Parameter, aber mit Anfangsguthaben
        var ohneAnfang = _sut.CalculateSavingsPlan(100, 5, 10, 0);
        var mitAnfang = _sut.CalculateSavingsPlan(100, 5, 10, 5000);

        // Prüfung: Mit Anfangsguthaben muss Endbetrag höher sein
        mitAnfang.FinalAmount.Should().BeGreaterThan(ohneAnfang.FinalAmount);
    }

    [Fact]
    public void CalculateSavingsPlan_NegativerZinssatz_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateSavingsPlan(100, -1, 10);
        aktion.Should().Throw<ArgumentException>().WithMessage("*Zinssatz*");
    }

    [Fact]
    public void CalculateSavingsPlan_NullJahre_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateSavingsPlan(100, 5, 0);
        aktion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateSavingsPlan_TotalDepositsIstKonsistent()
    {
        // Prüfung: TotalDeposits = Anfangsguthaben + (Monatsbeitrag * Monate)
        var ergebnis = _sut.CalculateSavingsPlan(150, 3, 8, 1000);
        var erwartetGesamt = 1000 + (150 * 8 * 12);

        ergebnis.TotalDeposits.Should().BeApproximately(erwartetGesamt, 0.001);
    }

    #endregion

    #region Kredit (Loan)

    [Fact]
    public void CalculateLoan_StandardKredit_BerechnetMonatlicheRate()
    {
        // Vorbereitung: 10.000€ bei 5% über 5 Jahre
        // Bekannte Annuität (online verifizierbar): ca. 188,71€/Monat
        var ergebnis = _sut.CalculateLoan(10000, 5, 5);

        // Prüfung
        ergebnis.MonthlyPayment.Should().BeApproximately(188.71, 0.1);
        ergebnis.TotalPayment.Should().BeApproximately(ergebnis.MonthlyPayment * 60, 0.01);
        ergebnis.TotalInterest.Should().BeApproximately(ergebnis.TotalPayment - 10000, 0.01);
    }

    [Fact]
    public void CalculateLoan_NullProzentZins_RateIstKapitalDurchLaufzeit()
    {
        // Vorbereitung: 0% Zinsen → Rate = Kredit / Monate
        var ergebnis = _sut.CalculateLoan(12000, 0, 1);

        // Prüfung: 12000 / 12 = 1000€/Monat
        ergebnis.MonthlyPayment.Should().BeApproximately(1000, 0.001);
        ergebnis.TotalInterest.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void CalculateLoan_HoehererZins_ErhoehtGesamtkosten()
    {
        var ergebnisNiedrig = _sut.CalculateLoan(10000, 2, 10);
        var ergebnisHoch = _sut.CalculateLoan(10000, 8, 10);

        ergebnisHoch.TotalInterest.Should().BeGreaterThan(ergebnisNiedrig.TotalInterest);
        ergebnisHoch.MonthlyPayment.Should().BeGreaterThan(ergebnisNiedrig.MonthlyPayment);
    }

    [Fact]
    public void CalculateLoan_NullBetrag_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateLoan(0, 5, 10);
        aktion.Should().Throw<ArgumentException>().WithMessage("*Kreditbetrag*");
    }

    [Fact]
    public void CalculateLoan_NegativerBetrag_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateLoan(-1000, 5, 10);
        aktion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateLoan_NullJahre_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateLoan(10000, 5, 0);
        aktion.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Tilgungsplan (Amortization)

    [Fact]
    public void CalculateAmortization_ErsteRateZinslastig_LetztePrincipalLastig()
    {
        // Vorbereitung: Langer Kredit mit hohem Zinssatz → am Anfang überwiegt Zins
        // 100.000€ bei 8% über 30 Jahre: erste Rate hat mehr Zins als Tilgung
        var ergebnis = _sut.CalculateAmortization(100000, 8, 30);
        var ersteRate = ergebnis.Schedule.First();
        var letzteRate = ergebnis.Schedule.Last();

        // Prüfung: Erste Rate zinslastig, letzte tilgungslastig
        ersteRate.Interest.Should().BeGreaterThan(ersteRate.Principal);
        letzteRate.Principal.Should().BeGreaterThan(letzteRate.Interest);
    }

    [Fact]
    public void CalculateAmortization_LetzteRateBringtSaldoAufNull()
    {
        // Vorbereitung: Nach letzter Rate muss Restschuld 0 sein
        var ergebnis = _sut.CalculateAmortization(10000, 5, 10);
        var letzteRate = ergebnis.Schedule.Last();

        // Prüfung
        letzteRate.RemainingBalance.Should().BeApproximately(0, 0.01);
    }

    [Fact]
    public void CalculateAmortization_AnzahlRatenKorrekt()
    {
        // Vorbereitung: 5 Jahre = 60 Monate
        var ergebnis = _sut.CalculateAmortization(10000, 5, 5);

        ergebnis.Schedule.Should().HaveCount(60);
    }

    [Fact]
    public void CalculateAmortization_ZinsenFallenMonatlichAb()
    {
        // Vorbereitung: Restschuld sinkt → Zinszahlungen müssen auch sinken
        var ergebnis = _sut.CalculateAmortization(20000, 6, 10);

        // Prüfung: Zinsen der ersten Rate > Zinsen der letzten Rate
        ergebnis.Schedule.First().Interest.Should().BeGreaterThan(ergebnis.Schedule.Last().Interest);
    }

    [Fact]
    public void CalculateAmortization_MonatsRateKonsistentMitLoanBerechnung()
    {
        // Vorbereitung: AmortizationResult.MonthlyPayment muss mit CalculateLoan übereinstimmen
        var kredit = _sut.CalculateLoan(15000, 4, 7);
        var tilgung = _sut.CalculateAmortization(15000, 4, 7);

        tilgung.MonthlyPayment.Should().BeApproximately(kredit.MonthlyPayment, 0.001);
    }

    #endregion

    #region Rendite (Yield)

    [Fact]
    public void CalculateEffectiveYield_VerdoppelungIn10Jahren_EtwaZwei()
    {
        // Vorbereitung: 1000→2000 in 10 Jahren → ca. 7,177% p.a.
        var ergebnis = _sut.CalculateEffectiveYield(1000, 2000, 10);

        // Prüfung
        ergebnis.EffectiveAnnualRate.Should().BeApproximately(7.177, 0.01);
        ergebnis.TotalReturn.Should().BeApproximately(1000, 0.001);
        ergebnis.TotalReturnPercent.Should().BeApproximately(100, 0.001);
    }

    [Fact]
    public void CalculateEffectiveYield_FinalValueGleichInitial_NullProzentRendite()
    {
        // Vorbereitung: Kein Gewinn, kein Verlust → 0% Rendite
        var ergebnis = _sut.CalculateEffectiveYield(5000, 5000, 5);

        ergebnis.EffectiveAnnualRate.Should().BeApproximately(0, 0.001);
        ergebnis.TotalReturn.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void CalculateEffectiveYield_Verlust_NegativeRendite()
    {
        // Vorbereitung: 1000 → 800 in 3 Jahren → negative Rendite
        var ergebnis = _sut.CalculateEffectiveYield(1000, 800, 3);

        ergebnis.EffectiveAnnualRate.Should().BeLessThan(0);
        ergebnis.TotalReturn.Should().BeLessThan(0);
    }

    [Fact]
    public void CalculateEffectiveYield_NullInvestition_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateEffectiveYield(0, 5000, 5);
        aktion.Should().Throw<ArgumentException>().WithMessage("*Anfangsinvestition*");
    }

    [Fact]
    public void CalculateEffectiveYield_NegativeInvestition_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateEffectiveYield(-1000, 5000, 5);
        aktion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateEffectiveYield_NullJahre_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateEffectiveYield(1000, 5000, 0);
        aktion.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Inflation

    [Fact]
    public void CalculateInflation_ZweiprozentUeber10Jahre_KorrektesErgebnis()
    {
        // Vorbereitung: 1000€ bei 2% Inflation über 10 Jahre
        // FutureValue = 1000 * (1.02)^10 ≈ 1218.99
        // PurchasingPower = 1000 / (1.02)^10 ≈ 820.35
        var ergebnis = _sut.CalculateInflation(1000, 2, 10);

        ergebnis.FutureValue.Should().BeApproximately(1218.994, 0.01);
        ergebnis.PurchasingPower.Should().BeApproximately(820.348, 0.01);
        ergebnis.PurchasingPowerLoss.Should().BeApproximately(1000 - ergebnis.PurchasingPower, 0.001);
    }

    [Fact]
    public void CalculateInflation_NullInflation_KeinKaufkraftverlust()
    {
        // Vorbereitung: 0% Inflation → keine Kaufkraftänderung
        var ergebnis = _sut.CalculateInflation(1000, 0, 10);

        ergebnis.FutureValue.Should().BeApproximately(1000, 0.001);
        ergebnis.PurchasingPower.Should().BeApproximately(1000, 0.001);
        ergebnis.PurchasingPowerLoss.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void CalculateInflation_LossPercentIstKonsistentMitPurchasingPowerLoss()
    {
        // Prüfung: LossPercent = PurchasingPowerLoss / CurrentAmount * 100
        var ergebnis = _sut.CalculateInflation(2000, 3, 15);
        var erwartetLossPercent = (ergebnis.PurchasingPowerLoss / 2000) * 100;

        ergebnis.LossPercent.Should().BeApproximately(erwartetLossPercent, 0.001);
    }

    [Fact]
    public void CalculateInflation_NullBetrag_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateInflation(0, 2, 10);
        aktion.Should().Throw<ArgumentException>().WithMessage("*Betrag*");
    }

    [Fact]
    public void CalculateInflation_NegativerBetrag_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateInflation(-500, 2, 10);
        aktion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateInflation_NullJahre_WirftArgumentException()
    {
        var aktion = () => _sut.CalculateInflation(1000, 2, 0);
        aktion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateInflation_HoheInflation_StarkerKaufkraftverlust()
    {
        // Vorbereitung: 10% Inflation über 20 Jahre = drastischer Kaufkraftverlust
        var ergebnis = _sut.CalculateInflation(1000, 10, 20);

        // Prüfung: Kaufkraft sinkt auf unter 15% des Ursprungswerts
        ergebnis.PurchasingPower.Should().BeLessThan(150);
        ergebnis.LossPercent.Should().BeGreaterThan(85);
    }

    #endregion

    #region Invarianten über alle Rechner

    [Fact]
    public void CalculateLoan_TotalPaymentIstMonatlicheRateTimesMonths()
    {
        // Invariante: TotalPayment = MonthlyPayment * Jahre * 12
        var ergebnis = _sut.CalculateLoan(20000, 6, 8);

        ergebnis.TotalPayment.Should().BeApproximately(ergebnis.MonthlyPayment * 8 * 12, 0.01);
    }

    [Fact]
    public void CalculateCompoundInterest_InterestEarnedIstFinalAmountMinusPrincipal()
    {
        // Invariante: InterestEarned = FinalAmount - Principal
        var ergebnis = _sut.CalculateCompoundInterest(3000, 7, 15, 4);

        ergebnis.InterestEarned.Should().BeApproximately(ergebnis.FinalAmount - ergebnis.Principal, 0.001);
    }

    [Fact]
    public void CalculateSavingsPlan_InterestEarnedIstFinalMinusTotalDeposits()
    {
        // Invariante: InterestEarned = FinalAmount - TotalDeposits
        var ergebnis = _sut.CalculateSavingsPlan(300, 4, 12, 2000);

        ergebnis.InterestEarned.Should().BeApproximately(ergebnis.FinalAmount - ergebnis.TotalDeposits, 0.001);
    }

    #endregion
}
