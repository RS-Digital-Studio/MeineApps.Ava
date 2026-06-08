#nullable enable
using System;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>Ergebnis der Tagesbelohnungs-Auswertung (UTC-tagbasiert).</summary>
    public struct DailyClaim
    {
        /// <summary>True, wenn heute eine Belohnung abgeholt werden kann.</summary>
        public bool CanClaim;
        /// <summary>Leiter-Tag (1..ladderLength), der bei Abholung gilt.</summary>
        public int Day;
        /// <summary>True, wenn der Streak gerissen war und auf Tag 1 zurückgesetzt wurde.</summary>
        public bool StreakReset;
    }

    /// <summary>
    /// Tägliche Belohnung (P2 §4, Original-Logik geborgen): 30-Tage-Leiter mit Streak (UTC-tagbasiert) und
    /// einkommens-skalierter Auszahlung <c>max(BasisGeld, min(sqrt(Tag)·net·60, net·900))</c>, damit späte
    /// Spieler nicht von winzigen Fixbeträgen abgespeist werden. Reine, Unity-freie Mathematik
    /// (Leiter-Werte kommen aus dem Katalog/BalancingConfig).
    /// </summary>
    public static class DailyRewardFormulas
    {
        /// <summary>
        /// Einkommens-skalierte Geld-Belohnung: mindestens der Leiter-Basiswert, sonst der Gegenwert von
        /// <c>sqrt(Tag)</c> Minuten Netto-Einkommen, gedeckelt bei 15 Minuten (net·900).
        /// </summary>
        public static decimal GetScaledMoney(decimal baseMoney, int day, decimal netIncomePerSecond)
        {
            if (netIncomePerSecond <= 0m) return baseMoney;
            if (day < 1) day = 1;
            decimal minutesWorth = (decimal)Math.Sqrt(day) * netIncomePerSecond * 60m;
            decimal maxReward = netIncomePerSecond * 900m;
            if (minutesWorth > maxReward) minutesWorth = maxReward;
            return baseMoney > minutesWorth ? baseMoney : minutesWorth;
        }

        /// <summary>
        /// Wertet aus, ob heute (UTC) eine Belohnung fällig ist und welcher Leiter-Tag gilt:
        /// gleicher Tag → keine Abholung; Folgetag → Streak +1 (Wrap am Leiter-Ende); Lücke → Reset auf Tag 1.
        /// </summary>
        public static DailyClaim Evaluate(int currentDay, long lastClaimUtcTicks, long nowUtcTicks, int ladderLength)
        {
            if (ladderLength < 1) ladderLength = 1;

            if (lastClaimUtcTicks <= 0)
                return new DailyClaim { CanClaim = true, Day = 1, StreakReset = false };

            DateTime last = new DateTime(lastClaimUtcTicks, DateTimeKind.Utc).Date;
            DateTime now = new DateTime(nowUtcTicks, DateTimeKind.Utc).Date;
            int dayDiff = (int)(now - last).TotalDays;

            if (dayDiff <= 0)
                return new DailyClaim { CanClaim = false, Day = currentDay, StreakReset = false };

            if (dayDiff == 1)
            {
                int next = currentDay % ladderLength + 1; // 1..ladderLength, Wrap
                return new DailyClaim { CanClaim = true, Day = next, StreakReset = false };
            }

            return new DailyClaim { CanClaim = true, Day = 1, StreakReset = true };
        }
    }
}
