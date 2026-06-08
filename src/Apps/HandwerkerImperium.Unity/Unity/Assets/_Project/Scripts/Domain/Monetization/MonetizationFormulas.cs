#nullable enable

namespace HandwerkerImperium.Domain.Monetization
{
    /// <summary>
    /// Monetarisierungs-Mathematik (P1 §3, GDD §9): Rewarded-Ad-Belohnungen (Free-Cash-Pad, Offline×2,
    /// Tempo-Boost) + Premium-„Imperium-Pass"-Effekte (+50 % Einkommen, Auto-Collect, höherer Offline-Cap/-Multiplikator).
    /// Reine, Unity-freie Mathematik; das Anzeigen von Ads / die IAP-Kaufabwicklung sind plattform-gated (Game-Layer).
    /// </summary>
    public static class MonetizationFormulas
    {
        /// <summary>Free-Cash-Pad-Multiplikator (zentrale Geld-Quelle: 2× Einkommen / Zeitblock).</summary>
        public const decimal FreeCashAdMultiplier = 2m;
        /// <summary>Premium-Einkommens-Bonus (+50 %).</summary>
        public const decimal PremiumIncomeBonus = 0.5m;
        /// <summary>Tempo-Boost-Multiplikator (alle Stationen kurz 2×).</summary>
        public const decimal RushTempoMultiplier = 2m;

        /// <summary>Free-Cash-Belohnung: <c>Einkommen/s × Zeitblock × Ad-Multiplikator</c>.</summary>
        public static decimal FreeCashReward(decimal incomePerSecond, double blockSeconds, decimal adMultiplier)
        {
            if (incomePerSecond <= 0m || blockSeconds <= 0) return 0m;
            if (adMultiplier < 1m) adMultiplier = 1m;
            return incomePerSecond * (decimal)blockSeconds * adMultiplier;
        }

        /// <summary>Verdopplung einer Belohnung (Offline×2 / Auftrags-×2 per Ad).</summary>
        public static decimal DoubledReward(decimal baseReward) => baseReward <= 0m ? 0m : baseReward * 2m;

        /// <summary>Einkommens-Multiplikator durch Premium (<c>1 + Bonus</c>, sonst 1).</summary>
        public static decimal PremiumIncomeMultiplier(bool isPremium, decimal bonus) =>
            isPremium ? 1m + (bonus < 0m ? 0m : bonus) : 1m;

        /// <summary>
        /// Effektiver Offline-Cap in Stunden: Basis + (Premium-Extra wenn Premium) + Perkboard-Extra.
        /// </summary>
        public static double OfflineCapHours(double baseCapHours, bool isPremium, double premiumExtraHours, double perkboardExtraHours)
        {
            double cap = baseCapHours;
            if (isPremium && premiumExtraHours > 0) cap += premiumExtraHours;
            if (perkboardExtraHours > 0) cap += perkboardExtraHours;
            return cap < 0 ? 0 : cap;
        }

        /// <summary>Offline-Multiplikator durch Premium (≥1), sonst 1.</summary>
        public static decimal PremiumOfflineMultiplier(bool isPremium, decimal multiplier) =>
            isPremium ? (multiplier < 1m ? 1m : multiplier) : 1m;

        /// <summary>Auto-Collect ist der Premium-Kern-QoL-Vorteil (riesig im Walk-around-Idle).</summary>
        public static bool AutoCollectEnabled(bool isPremium) => isPremium;
    }
}
