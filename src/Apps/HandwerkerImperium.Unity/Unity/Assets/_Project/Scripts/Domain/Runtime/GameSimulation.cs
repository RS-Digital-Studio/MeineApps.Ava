#nullable enable
using System.Collections.Generic;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Orders;
using HandwerkerImperium.Domain.LiveOps;
using HandwerkerImperium.Domain.Restoration;
using HandwerkerImperium.Domain.StarRating;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Franchise;
using HandwerkerImperium.Domain.Monetization;
using HandwerkerImperium.Domain.Config;

namespace HandwerkerImperium.Domain.Runtime
{
    /// <summary>
    /// Der Spiel-Orchestrator: komponiert ALLE verifizierten Formel-Sätze über dem <see cref="GameModel"/> zu
    /// einem kohärenten Spiel — Tick (Produktion/Worker/Orders), effektives Einkommen (Aggregat aller permanenten
    /// Quellen + Log2-Soft-Cap), Stern-Bewertung (skaliert je Stadt), Prestige (Reset Idle-Loop + Persist Meta) und
    /// Offline-Verdienst. Reine, Unity-freie Logik; der Game-Layer ruft sie pro Frame / auf Aktionen.
    /// </summary>
    public static class GameSimulation
    {
        /// <summary>Pro-Frame-Fortschritt: Produktion + Worker-Automatisierung + Kunden-Zustrom + Ablauf von Rush/Eil-Auftrag.
        /// Liefert das durch Worker erwirtschaftete Geld-Delta.</summary>
        public static decimal Tick(GameModel m, IdleBalancing idleBal, GameBalancing bal, double dtSeconds, long nowUtcTicks)
        {
            GreyboxSimulation.TickProduction(m.Idle, idleBal, dtSeconds);
            decimal earned = GreyboxSimulation.TickWorkers(m.Idle, idleBal, dtSeconds);
            OrderQueueFormulas.Tick(m.Orders, dtSeconds, bal.Orders.SpawnIntervalSeconds, bal.Orders.MaxQueue);
            OrderQueueFormulas.ExpireRushIfDue(m.Orders, nowUtcTicks);
            RushEventFormulas.ExpireIfDue(m.Rush, nowUtcTicks);

            // Meisterschafts-XP fließt aus dem laufenden Verdienst (PROGRESSION §4: kontoweit, nie reset).
            if (earned > 0m && bal.Mastery.XpPerMoney > 0)
                MetaProgression.GainMasteryXp(m.Meta, (double)earned * bal.Mastery.XpPerMoney, bal.Mastery.BaseXp, bal.Mastery.Growth);

            // Endgame: in der Endstadt akkumuliert Renommee aus dem Verdienst (Soft-Infinite-Meistergrad-Loop, PROGRESSION §5).
            if (earned > 0m && PrestigeFormulas.IsFinalCity(m.Meta.PrestigeCount, bal.Prestige.MaxPrestige))
                m.Meta.Renommee += earned * bal.Meistergrad.AccrualRatePerIncome;

            return earned;
        }

        /// <summary>Soft-Cap-Schwelle, skaliert je Stadt (Akte werden länger).</summary>
        public static decimal SoftCapThreshold(GameModel m, GameBalancing bal)
        {
            double scale = WorldTierFormulas.StarThresholdScale(m.Meta.CityIndex, bal.WorldTier.StarThresholdPerCityScale);
            return bal.SoftCap.ThresholdBase * (decimal)scale;
        }

        /// <summary>Aggregat-Multiplikator aus allen permanenten Quellen (Prestige × Mastery × Perkboard × MasterTools × Premium × Meistergrad).</summary>
        public static decimal AggregateMultiplier(GameModel m, GameBalancing bal, List<MasterToolDefinition> masterToolCatalog)
        {
            decimal masteryBonus = MasteryFormulas.GlobalIncomeBonus(m.Meta.MasteryLevel, bal.Mastery.BonusPerLevel);
            decimal perkBonus = PerkboardIncomeBonus(m, bal);
            decimal mtBonus = MasterToolFormulas.TotalIncomeBonus(m.CollectedMasterTools, masterToolCatalog);
            decimal premiumBonus = MonetizationFormulas.PremiumIncomeMultiplier(m.IsPremium, bal.Monetization.PremiumIncomeBonus) - 1m;
            decimal meistergradBonus = MeistergradFormulas.GlobalBonus(m.Meta.MeistergradGrade, bal.Meistergrad.BonusPerGrade);
            return IncomeAggregation.AggregateMultiplier(m.Meta.PrestigeMultiplier, masteryBonus, perkBonus, mtBonus, premiumBonus, meistergradBonus);
        }

        /// <summary>Effektives automatisiertes Einkommen/s: Idle-Basis × Aggregat, Log2-Soft-Cap-gedämpft.</summary>
        public static decimal EffectiveIncomePerSecond(GameModel m, IdleBalancing idleBal, GameBalancing bal, List<MasterToolDefinition> masterToolCatalog)
        {
            decimal baseIncome = GreyboxSimulation.TotalAutomatedIncomePerSecond(m.Idle, idleBal);
            decimal agg = AggregateMultiplier(m, bal, masterToolCatalog);
            return IncomeAggregation.EffectiveIncomePerSecond(baseIncome, agg, SoftCapThreshold(m, bal));
        }

        /// <summary>Bewertet den Stadt-Stern neu (Score aus Werkstätten/Sanierung/Volumen, Schwellen je Stadt skaliert, Hysterese) und schreibt ihn in den State.</summary>
        public static int EvaluateStar(GameModel m, GameBalancing bal)
        {
            int workshops = UnlockedStationCount(m.Idle);
            int phases = RestorationFormulas.TotalPhasesComplete(m.Landmarks);
            m.Meta.RestorationPhases = phases;
            m.Meta.OrdersServed = m.Orders.TotalServed;
            double score = StarRatingFormulas.Score(workshops, phases, m.Orders.TotalServed,
                bal.Star.WorkshopWeight, bal.Star.RestorationWeight, bal.Star.VolumeWeight);
            int star = StarRatingFormulas.EvaluateStars(score, m.Meta.CurrentStar, ScaledStarThresholds(m, bal), bal.Star.HysteresisBuffer);
            m.Meta.CurrentStar = star;
            return star;
        }

        /// <summary>True, wenn ein Prestige erlaubt ist (5★ + Limit nicht erreicht).</summary>
        public static bool CanPrestige(GameModel m, GameBalancing bal) =>
            PrestigeFormulas.CanPrestige(m.Meta.CurrentStar, m.Meta.PrestigeCount, bal.Prestige.MaxPrestige);

        /// <summary>
        /// Führt ein Prestige aus: Meta-Progression (Multiplikator/Marken/PP/Stadt) + <b>Reset des aktiven
        /// Idle-Loops</b> und der akt-internen Runtime-States (Orders/Wahrzeichen/Rush). Permanent bleibt erhalten.
        /// </summary>
        public static bool TryPrestige(GameModel m, IdleBalancing idleBal, GameBalancing bal)
        {
            if (!MetaProgression.TryPrestige(m.Meta, m.Idle.Money, bal.Prestige.StageMultipliers, bal.Prestige.MarksPerPrestige, bal.Prestige.MaxPrestige))
                return false;

            long lastSeen = m.Idle.LastSeenUtcTicks;
            m.Idle = GreyboxSimState.CreateNew(idleBal);
            m.Idle.LastSeenUtcTicks = lastSeen;
            m.Orders = new OrderQueueState();
            m.Landmarks.Clear();
            m.Rush = new RushEventState();
            return true;
        }

        /// <summary>Offline-Verdienst: effektives Einkommen/s über die (gestaffelte, gedeckelte) Abwesenheit, mit Premium-Multiplikator.</summary>
        public static decimal ComputeOffline(GameModel m, IdleBalancing idleBal, GameBalancing bal, List<MasterToolDefinition> masterToolCatalog, double elapsedSeconds)
        {
            decimal effIncome = EffectiveIncomePerSecond(m, idleBal, bal, masterToolCatalog);
            double capHours = MonetizationFormulas.OfflineCapHours(bal.Offline.BaseCapHours, m.IsPremium, bal.Offline.PremiumExtraHours, 0);
            decimal staggered = IdleEconomyFormulas.OfflineEarnings(effIncome, elapsedSeconds, capHours * 3600.0);
            return staggered * MonetizationFormulas.PremiumOfflineMultiplier(m.IsPremium, bal.Offline.PremiumMultiplier);
        }

        private static decimal PerkboardIncomeBonus(GameModel m, GameBalancing bal)
        {
            int idx = (int)PerkKind.GlobalTempo;
            if (m.PerkLevels == null || idx >= m.PerkLevels.Count) return 0m;
            return PerkboardFormulas.BonusAtLevel(m.PerkLevels[idx], bal.Perkboard.DefaultMaxLevel, bal.Perkboard.BonusPerLevel);
        }

        private static List<double> ScaledStarThresholds(GameModel m, GameBalancing bal)
        {
            double scale = WorldTierFormulas.StarThresholdScale(m.Meta.CityIndex, bal.WorldTier.StarThresholdPerCityScale);
            var result = new List<double>(bal.Star.Thresholds.Count);
            foreach (var t in bal.Star.Thresholds) result.Add(t * scale);
            return result;
        }

        private static int UnlockedStationCount(GreyboxSimState idle)
        {
            int n = 0;
            foreach (var st in idle.Stations) if (st.Unlocked) n++;
            return n;
        }
    }
}
