namespace FinanzRechner.Models;

/// <summary>
/// Berechnungsengine für alle Finanzrechner.
/// </summary>
public class FinanceEngine
{
    /// <summary>
    /// Prüft ob ein Berechnungsergebnis gültig ist (kein Infinity/NaN).
    /// Wirft OverflowException bei ungültigen Werten.
    /// </summary>
    private static void ValidateResult(double value, string description)
    {
        if (double.IsInfinity(value) || double.IsNaN(value))
            throw new OverflowException($"Berechnungsergebnis ungültig ({description}): Die Eingabewerte führen zu einem Überlauf.");
    }

    #region Compound Interest

    public CompoundInterestResult CalculateCompoundInterest(
        double principal, double annualRate, int years, int compoundingsPerYear = 1)
    {
        if (years <= 0)
            throw new ArgumentException("Jahre müssen größer als Null sein.", nameof(years));
        if (compoundingsPerYear <= 0)
            throw new ArgumentException("Zinsperioden pro Jahr müssen größer als Null sein.", nameof(compoundingsPerYear));

        var rate = annualRate / 100;
        var n = compoundingsPerYear;
        var t = years;

        var finalAmount = principal * Math.Pow(1 + rate / n, n * t);
        ValidateResult(finalAmount, "FinalAmount");
        var interestEarned = finalAmount - principal;

        return new CompoundInterestResult
        {
            Principal = principal, AnnualRate = annualRate, Years = years,
            CompoundingsPerYear = compoundingsPerYear,
            FinalAmount = finalAmount, InterestEarned = interestEarned
        };
    }

    #endregion

    #region Savings Plan

    public SavingsPlanResult CalculateSavingsPlan(
        double monthlyDeposit, double annualRate, int years, double initialDeposit = 0)
    {
        if (annualRate < 0)
            throw new ArgumentException("Zinssatz darf nicht negativ sein.", nameof(annualRate));
        if (years <= 0)
            throw new ArgumentException("Jahre müssen größer als Null sein.", nameof(years));

        var monthlyRate = (annualRate / 100) / 12;
        var months = years * 12;

        var initialGrowth = initialDeposit * Math.Pow(1 + monthlyRate, months);

        double savingsGrowth;
        if (monthlyRate > 0)
            savingsGrowth = monthlyDeposit * ((Math.Pow(1 + monthlyRate, months) - 1) / monthlyRate);
        else
            savingsGrowth = monthlyDeposit * months;

        var finalAmount = initialGrowth + savingsGrowth;
        ValidateResult(finalAmount, "FinalAmount");
        var totalDeposits = initialDeposit + (monthlyDeposit * months);
        var interestEarned = finalAmount - totalDeposits;

        return new SavingsPlanResult
        {
            MonthlyDeposit = monthlyDeposit, InitialDeposit = initialDeposit,
            AnnualRate = annualRate, Years = years,
            TotalDeposits = totalDeposits, FinalAmount = finalAmount,
            InterestEarned = interestEarned
        };
    }

    #endregion

    #region Loan

    public LoanResult CalculateLoan(double loanAmount, double annualRate, int years)
    {
        if (loanAmount <= 0)
            throw new ArgumentException("Kreditbetrag muss größer als Null sein.", nameof(loanAmount));
        if (years <= 0)
            throw new ArgumentException("Jahre müssen größer als Null sein.", nameof(years));

        var monthlyRate = (annualRate / 100) / 12;
        var months = years * 12;

        double monthlyPayment;
        if (monthlyRate > 0)
        {
            monthlyPayment = loanAmount *
                (monthlyRate * Math.Pow(1 + monthlyRate, months)) /
                (Math.Pow(1 + monthlyRate, months) - 1);
        }
        else
        {
            monthlyPayment = loanAmount / months;
        }
        ValidateResult(monthlyPayment, "MonthlyPayment");

        var totalPayment = monthlyPayment * months;
        var totalInterest = totalPayment - loanAmount;

        return new LoanResult
        {
            LoanAmount = loanAmount, AnnualRate = annualRate, Years = years,
            MonthlyPayment = monthlyPayment, TotalPayment = totalPayment,
            TotalInterest = totalInterest
        };
    }

    #endregion

    #region Amortization Schedule

    public AmortizationResult CalculateAmortization(double loanAmount, double annualRate, int years)
    {
        var loan = CalculateLoan(loanAmount, annualRate, years);
        var monthlyRate = (annualRate / 100) / 12;
        var months = years * 12;

        var schedule = new List<AmortizationEntry>();
        var balance = loanAmount;

        for (int i = 1; i <= months; i++)
        {
            var interestPayment = balance * monthlyRate;
            var principalPayment = loan.MonthlyPayment - interestPayment;
            balance -= principalPayment;

            if (i == months)
            {
                principalPayment += balance;
                balance = 0;
            }

            schedule.Add(new AmortizationEntry
            {
                Month = i, Payment = loan.MonthlyPayment,
                Principal = principalPayment, Interest = interestPayment,
                RemainingBalance = Math.Max(0, balance)
            });
        }

        return new AmortizationResult
        {
            LoanAmount = loanAmount, AnnualRate = annualRate, Years = years,
            MonthlyPayment = loan.MonthlyPayment, TotalInterest = loan.TotalInterest,
            Schedule = schedule
        };
    }

    #endregion

    #region Yield

    public YieldResult CalculateEffectiveYield(double initialInvestment, double finalValue, int years)
    {
        if (initialInvestment <= 0)
            throw new ArgumentException("Anfangsinvestition muss größer als Null sein.", nameof(initialInvestment));
        if (years <= 0)
            throw new ArgumentException("Jahre müssen größer als Null sein.", nameof(years));

        var effectiveAnnualRate = (Math.Pow(finalValue / initialInvestment, 1.0 / years) - 1) * 100;
        ValidateResult(effectiveAnnualRate, "EffectiveAnnualRate");
        var totalReturn = finalValue - initialInvestment;
        var totalReturnPercent = (totalReturn / initialInvestment) * 100;

        return new YieldResult
        {
            InitialInvestment = initialInvestment, FinalValue = finalValue, Years = years,
            TotalReturn = totalReturn, TotalReturnPercent = totalReturnPercent,
            EffectiveAnnualRate = effectiveAnnualRate
        };
    }

    #endregion

    #region Inflation

    /// <summary>
    /// Berechnet die Auswirkung der Inflation auf einen Geldbetrag über einen bestimmten Zeitraum.
    /// </summary>
    public InflationResult CalculateInflation(double currentAmount, double annualInflationRate, int years)
    {
        if (currentAmount <= 0)
            throw new ArgumentException("Betrag muss größer als Null sein.", nameof(currentAmount));
        if (years <= 0)
            throw new ArgumentException("Jahre müssen größer als Null sein.", nameof(years));

        var rate = annualInflationRate / 100;
        var futureValue = currentAmount * Math.Pow(1 + rate, years);
        ValidateResult(futureValue, "FutureValue");
        var purchasingPower = currentAmount / Math.Pow(1 + rate, years);
        ValidateResult(purchasingPower, "PurchasingPower");
        var purchasingPowerLoss = currentAmount - purchasingPower;
        var lossPercent = (purchasingPowerLoss / currentAmount) * 100;

        return new InflationResult
        {
            CurrentAmount = currentAmount,
            AnnualInflationRate = annualInflationRate,
            Years = years,
            FutureValue = futureValue,
            PurchasingPower = purchasingPower,
            PurchasingPowerLoss = purchasingPowerLoss,
            LossPercent = lossPercent
        };
    }

    #endregion
}

#region Result Types

public record CompoundInterestResult
{
    public double Principal { get; init; }
    public double AnnualRate { get; init; }
    public int Years { get; init; }
    public int CompoundingsPerYear { get; init; }
    public double FinalAmount { get; init; }
    public double InterestEarned { get; init; }
}

public record SavingsPlanResult
{
    public double MonthlyDeposit { get; init; }
    public double InitialDeposit { get; init; }
    public double AnnualRate { get; init; }
    public int Years { get; init; }
    public double TotalDeposits { get; init; }
    public double FinalAmount { get; init; }
    public double InterestEarned { get; init; }
}

public record LoanResult
{
    public double LoanAmount { get; init; }
    public double AnnualRate { get; init; }
    public int Years { get; init; }
    public double MonthlyPayment { get; init; }
    public double TotalPayment { get; init; }
    public double TotalInterest { get; init; }
}

public record AmortizationEntry
{
    public int Month { get; init; }
    public double Payment { get; init; }
    public double Principal { get; init; }
    public double Interest { get; init; }
    public double RemainingBalance { get; init; }
}

public record AmortizationResult
{
    public double LoanAmount { get; init; }
    public double AnnualRate { get; init; }
    public int Years { get; init; }
    public double MonthlyPayment { get; init; }
    public double TotalInterest { get; init; }
    public List<AmortizationEntry> Schedule { get; init; } = new();
}

public record YieldResult
{
    public double InitialInvestment { get; init; }
    public double FinalValue { get; init; }
    public int Years { get; init; }
    public double TotalReturn { get; init; }
    public double TotalReturnPercent { get; init; }
    public double EffectiveAnnualRate { get; init; }
}

public record InflationResult
{
    public double CurrentAmount { get; init; }
    public double AnnualInflationRate { get; init; }
    public int Years { get; init; }
    public double FutureValue { get; init; }
    public double PurchasingPower { get; init; }
    public double PurchasingPowerLoss { get; init; }
    public double LossPercent { get; init; }
}

#endregion
