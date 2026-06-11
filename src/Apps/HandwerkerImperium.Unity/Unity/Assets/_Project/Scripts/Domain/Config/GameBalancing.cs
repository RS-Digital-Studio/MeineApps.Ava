#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Config
{
    /// <summary>
    /// Zentrale, Unity-freie Tuning-Konfiguration aller Domain-Formeln (".md-Mandat: kein Hardcoding,
    /// alles über BalancingConfig" — PROGRESSION §11, P4 §3). Pure Daten mit Default-Werten aus den Specs;
    /// das BalancingConfig-ScriptableObject im Game-Layer mappt 1:1 hierauf (wie P0 BalancingConfig→IdleBalancing)
    /// und kann per Remote-Config live getunt werden (P3/P4). Single-Source für die Formel-Parameter.
    /// </summary>
    public sealed class GameBalancing
    {
        public StarConfig Star = new StarConfig();
        public PrestigeConfig Prestige = new PrestigeConfig();
        public MasteryConfig Mastery = new MasteryConfig();
        public MeistergradConfig Meistergrad = new MeistergradConfig();
        public PerkboardConfig Perkboard = new PerkboardConfig();
        public SoftCapConfig SoftCap = new SoftCapConfig();
        public WorldTierConfig WorldTier = new WorldTierConfig();
        public OrderConfig Orders = new OrderConfig();
        public RushConfig Rush = new RushConfig();
        public RestorationConfig Restoration = new RestorationConfig();
        public DailyConfig Daily = new DailyConfig();
        public OfflineConfig Offline = new OfflineConfig();
        public MonetizationConfig Monetization = new MonetizationConfig();
        public ReferralConfig Referral = new ReferralConfig();
        public MiniGameConfig MiniGames = new MiniGameConfig();

        // ── Stern-Rating (P1 §3/§4) ────────────────────────────────────────
        public sealed class StarConfig
        {
            /// <summary>Score-Schwellen für 2★…5★ (Akt 1; steigen je Stadt über WorldTier).</summary>
            public List<double> Thresholds = new List<double> { 100, 300, 600, 1000 };
            public double HysteresisBuffer = 50;
            public double WorkshopWeight = 50;
            public double RestorationWeight = 40;
            public double VolumeWeight = 2;
        }

        // ── Prestige (P1 §4, PROGRESSION §7) ───────────────────────────────
        public sealed class PrestigeConfig
        {
            /// <summary>Je-Stufe-Multiplikatoren (kumulativ ×3/×12/×60).</summary>
            public List<decimal> StageMultipliers = new List<decimal> { 3m, 4m, 5m };
            public int MaxPrestige = 3;
            public int MarksPerPrestige = 5;
        }

        // ── Meisterschaft (PROGRESSION §4) ─────────────────────────────────
        public sealed class MasteryConfig
        {
            public double BaseXp = 100;
            public double Growth = 1.15;
            public decimal BonusPerLevel = 0.01m; // +1 %/Level (Soft-Cap gedämpft)
            /// <summary>Anteil des laufenden Verdiensts, der als Meisterschafts-XP zufließt (PROGRESSION §4).</summary>
            public double XpPerMoney = 0.1;
        }

        // ── Endgame-Meistergrade (PROGRESSION §5) ──────────────────────────
        public sealed class MeistergradConfig
        {
            public decimal RenommeeBaseCost = 1000m;
            public double Growth = 1.5;
            public decimal BonusPerGrade = 0.02m;
            public decimal AccrualRatePerIncome = 0.0001m;
        }

        // ── Imperium-Marken-Perkboard (PROGRESSION §6) ─────────────────────
        public sealed class PerkboardConfig
        {
            public int MarkCostBase = 10;
            public double MarkCostGrowth = 1.5;
            public int DefaultMaxLevel = 5;
            public decimal BonusPerLevel = 0.05m;
        }

        // ── Income-Soft-Cap (GDD §7) ───────────────────────────────────────
        public sealed class SoftCapConfig
        {
            /// <summary>Soft-Cap-Schwelle in Stadt 0; steigt je Stadt über WorldTier.StarThresholdPerCityScale.</summary>
            public decimal ThresholdBase = 4m;
        }

        // ── World-Tiers / 4 Städte (P2 §3, GDD §5) ─────────────────────────
        public sealed class WorldTierConfig
        {
            public double StarThresholdPerCityScale = 1.6;
            public decimal IncomeTargetPerCityScale = 8m;
        }

        // ── Kunden-Queue + Eil-Auftrag (P1 §3) ─────────────────────────────
        public sealed class OrderConfig
        {
            public double SpawnIntervalSeconds = 3.0;
            public int MaxQueue = 12;
            public decimal RushRewardMultiplier = 3m;
            public double RushDurationSeconds = 60;
        }

        // ── Rush-Event (P2 §3, GDD §9.1) ───────────────────────────────────
        public sealed class RushConfig
        {
            public decimal Multiplier = 2m;
            public double DurationSeconds = 30;
            public double CooldownSeconds = 86400; // 1×/Tag gratis
        }

        // ── Wahrzeichen-Sanierung (P1 §6.4) ────────────────────────────────
        public sealed class RestorationConfig
        {
            public decimal PhaseBaseCost = 5000m;
            public double PhaseGrowth = 1.8;
            public int TotalPhases = 5;
        }

        // ── Tagesbelohnung (P2 §4) ─────────────────────────────────────────
        public sealed class DailyConfig
        {
            public int LadderLength = 30;
        }

        // ── Offline (P1 §3, GDD §8) ────────────────────────────────────────
        public sealed class OfflineConfig
        {
            public double BaseCapHours = 2;        // P0: 2 h
            public double PremiumExtraHours = 14;  // -> 16 h mit Premium
            public decimal PremiumMultiplier = 1.5m;
        }

        // ── Monetarisierung (GDD §9) ───────────────────────────────────────
        public sealed class MonetizationConfig
        {
            public decimal FreeCashAdMultiplier = 2m;
            public double FreeCashBlockSeconds = 120;
            public decimal PremiumIncomeBonus = 0.5m; // +50 %
            public decimal RushTempoMultiplier = 2m;
            public int MigrationBonusGems = 100;
        }

        // ── Referral (P3 §4) ───────────────────────────────────────────────
        public sealed class ReferralConfig
        {
            public List<int> TierThresholds = new List<int> { 1, 5, 10 };
            public List<int> TierRewards = new List<int> { 50, 200, 500 };
        }

        // ── Perfekt-Aktionen / Tap-Timing (GDD §6.7) ───────────────────────
        public sealed class MiniGameConfig
        {
            /// <summary>Timing-Fenster in Sekunden (perfect ⊂ good ⊂ ok; darueber Miss).</summary>
            public double PerfectWindowSeconds = 0.07;
            public double GoodWindowSeconds = 0.16;
            public double OkWindowSeconds = 0.30;
            /// <summary>Basis-Buff-Dauer (Perfect = voll; Good/Ok skaliert, siehe MiniGameBoostFormulas).</summary>
            public double BoostBaseDurationSeconds = 20;
            /// <summary>Puls-Periode des Timing-Rings in Sekunden (eine Schrumpf-Wachs-Schwingung).</summary>
            public double PulsePeriodSeconds = 1.4;
        }
    }
}
