#nullable enable
using System;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Endgame-Meistergrade (PROGRESSION §5, P3 §3) — der Soft-Infinite-Schwanz NACH dem 3. Prestige (Metropole),
    /// KEIN weiteres Stadt-Prestige. Eine dedizierte Endgame-Ressource „Imperium-Renommee" akkumuliert langsam
    /// aus Spitzen-Einkommen; ein Meistergrad kostet <c>base × 1.5^R</c> (geometrisch langsamer → Monate), gibt
    /// einen kleinen permanenten Global-Bonus (per Income-Soft-Cap gedämpft). Reine, Unity-freie Mathematik.
    /// </summary>
    public static class MeistergradFormulas
    {
        /// <summary>Geometrischer Kostenfaktor je Grad (PROGRESSION §7).</summary>
        public const double DefaultGrowth = 1.5;

        /// <summary>Renommee-Kosten des nächsten Grades <paramref name="grade"/>: <c>base × 1.5^grade</c>.</summary>
        public static decimal RenommeeCost(int grade, decimal baseCost, double growth)
        {
            if (grade < 0) grade = 0;
            double raw = (double)baseCost * Math.Pow(growth, grade);
            if (double.IsNaN(raw) || double.IsInfinity(raw) || raw > (double)decimal.MaxValue) return decimal.MaxValue;
            decimal cost = Math.Round((decimal)raw);
            return cost < 1m ? 1m : cost;
        }

        /// <summary>True, wenn der nächste Meistergrad bezahlbar ist (genug Renommee).</summary>
        public static bool CanPurchase(decimal renommee, int currentGrade, decimal baseCost, double growth) =>
            renommee >= RenommeeCost(currentGrade, baseCost, growth);

        /// <summary>
        /// Langsame Renommee-Akkumulation aus dem Spitzen-Einkommen über die Zeit
        /// (<c>peakIncomePerSecond × dt × ratePerIncome</c>).
        /// </summary>
        public static decimal AccrueRenommee(decimal peakIncomePerSecond, double dtSeconds, decimal ratePerIncome)
        {
            if (peakIncomePerSecond <= 0m || dtSeconds <= 0 || ratePerIncome <= 0m) return 0m;
            return peakIncomePerSecond * (decimal)dtSeconds * ratePerIncome;
        }

        /// <summary>Permanenter Global-Bonus auf einem Meistergrad (<c>grade × bonusPerGrade</c>, Soft-Cap durch Aufrufer).</summary>
        public static decimal GlobalBonus(int grade, decimal bonusPerGrade) =>
            grade <= 0 ? 0m : grade * bonusPerGrade;
    }
}
