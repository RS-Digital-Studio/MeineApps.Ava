#nullable enable
using System;
using System.Collections.Generic;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Achievements;
using HandwerkerImperium.Domain.Restoration;
using HandwerkerImperium.Domain.Story;
using HandwerkerImperium.Domain.LiveOps;
using HandwerkerImperium.Domain.Common;

namespace HandwerkerImperium.Domain.Runtime
{
    /// <summary>
    /// Wertet die permanenten Fortschritts-Systeme über dem <see cref="GameModel"/> aus (periodisch / auf Events
    /// aufgerufen): Auto-Sammlung erfüllter Meisterwerkzeuge (permanenter Income-Bonus) + Gutschrift neu erreichter
    /// Achievements (Gems). Reine, Unity-freie Logik; macht die einzeln getesteten Formel-Sätze im Spiel <b>live</b>.
    /// </summary>
    public static class GameProgress
    {
        private static readonly AchievementMetric[] AllMetrics =
        {
            AchievementMetric.OrdersServed, AchievementMetric.StationsUnlocked, AchievementMetric.WorkersHired,
            AchievementMetric.RestorationPhases, AchievementMetric.PrestigeCount, AchievementMetric.MasteryLevel,
            AchievementMetric.MoneyEarned
        };

        /// <summary>Sammelt alle jetzt erfüllten, noch nicht gesammelten Meisterwerkzeuge ein. Liefert die neuen Ids.</summary>
        public static List<string> CollectEligibleMasterTools(GameModel m, List<MasterToolDefinition> catalog)
        {
            var newly = new List<string>();
            if (m == null || catalog == null) return newly;
            var ctx = BuildMasterToolContext(m);
            foreach (var def in catalog)
            {
                if (def == null || Contains(m.CollectedMasterTools, def.Id)) continue;
                if (MasterToolFormulas.IsEligible(def, ctx))
                {
                    m.CollectedMasterTools.Add(def.Id);
                    newly.Add(def.Id);
                    ctx.CollectedTools = m.CollectedMasterTools.Count; // letzte Bedingung (master_crown) sieht den neuen Stand
                }
            }
            return newly;
        }

        /// <summary>
        /// Schreibt alle jetzt erreichten, noch nicht eingelösten Achievements gut (über alle Metriken) und bucht die
        /// Gem-Belohnung auf <see cref="GameModel.Gems"/>. Liefert die Summe der gutgeschriebenen Gems.
        /// </summary>
        public static int GrantNewAchievements(GameModel m, IReadOnlyList<AchievementDefinition> catalog)
        {
            if (m == null || catalog == null) return 0;
            int totalGems = 0;
            foreach (var metric in AllMetrics)
            {
                long value = MetricValue(m, metric);
                var newly = AchievementFormulas.NewlyCompleted(catalog, metric, value, m.ClaimedAchievements);
                if (newly.Count == 0) continue;
                totalGems += AchievementFormulas.TotalGemReward(catalog, newly);
                foreach (var id in newly)
                    m.ClaimedAchievements.Add(id);
            }
            if (totalGems > 0) m.Gems += totalGems;
            return totalGems;
        }

        /// <summary>
        /// Wertet die Meister-Hans-Story-Beats aus: prüft die Loop-Meilenstein-Bedingungen und spielt die jeweils
        /// noch nicht abgespielten Beats ab (in <see cref="GameModel.PlayedStoryBeats"/> vermerkt). Liefert die neuen Ids.
        /// </summary>
        public static List<string> EvaluateStory(GameModel m, IReadOnlyList<StoryBeatDefinition> catalog)
        {
            var played = new List<string>();
            if (m == null || catalog == null) return played;
            FireBeats(m, catalog, StoryTrigger.GameStart, true, played);
            FireBeats(m, catalog, StoryTrigger.FirstStationProduce, AnyStock(m.Idle), played);
            FireBeats(m, catalog, StoryTrigger.FirstWorkerHired, CountWorkers(m.Idle) > 0, played);
            FireBeats(m, catalog, StoryTrigger.FirstPlotUnlocked, LastStationUnlocked(m.Idle), played);
            FireBeats(m, catalog, StoryTrigger.FirstLandmarkRestored, RestorationFormulas.CompletedLandmarks(m.Landmarks) > 0, played);
            FireBeats(m, catalog, StoryTrigger.FirstPrestige, m.Meta.PrestigeCount > 0, played);
            return played;
        }

        /// <summary>
        /// Wertet die Tagesaufgaben aus: zieht bei neuem UTC-Tag (oder leerer Liste) 3 neue Aufgaben deterministisch
        /// aus dem Pool (Basiswerte = aktuelle Metriken), und schreibt jede jetzt erfüllte, noch nicht abgeholte Aufgabe
        /// gut (Gems auf <see cref="GameModel.Gems"/>). Liefert die in diesem Aufruf neu abgeholten Aufgaben-Ids.
        /// </summary>
        public static List<string> EvaluateDailyTasks(GameModel m, List<DailyTaskDefinition> pool, long nowUtcTicks)
        {
            var claimed = new List<string>();
            if (m == null || pool == null || pool.Count == 0) return claimed;

            if (DailyTaskFormulas.ShouldReset(m.DailyTaskRollDayUtc, nowUtcTicks) || m.DailyTasks.Count == 0)
                RollDailyTasks(m, pool, nowUtcTicks);

            int gems = 0;
            foreach (var t in m.DailyTasks)
            {
                if (t.Claimed) continue;
                long progress = DailyMetricValue(m, t.Metric) - t.Baseline;
                if (DailyTaskFormulas.IsComplete(progress, t.Target))
                {
                    t.Claimed = true;
                    gems += t.GemReward;
                    claimed.Add(t.Id);
                }
            }
            if (gems > 0) m.Gems += gems;
            return claimed;
        }

        /// <summary>Aktueller Fortschritt 0..1 einer Tagesaufgabe (für die UI).</summary>
        public static double DailyTaskProgress01(GameModel m, DailyTaskRuntime t)
        {
            if (m == null || t == null) return 0.0;
            return DailyTaskFormulas.Progress01(DailyMetricValue(m, t.Metric) - t.Baseline, t.Target);
        }

        private static void RollDailyTasks(GameModel m, List<DailyTaskDefinition> pool, long nowUtcTicks)
        {
            m.DailyTasks.Clear();
            m.DailyTaskRollDayUtc = nowUtcTicks;

            long dayIndex = nowUtcTicks / TimeSpan.TicksPerDay;
            int n = pool.Count;
            int want = n < 3 ? n : 3;
            var chosen = new List<int>();
            for (int i = 0; chosen.Count < want && i < 256; i++)
            {
                int idx = StableHash.Bucket("dt|" + dayIndex + "|" + i, n);
                if (!chosen.Contains(idx)) chosen.Add(idx);
            }
            foreach (var idx in chosen)
            {
                var def = pool[idx];
                m.DailyTasks.Add(new DailyTaskRuntime
                {
                    Id = def.Id, Metric = def.Metric, Target = def.Target, GemReward = def.GemReward,
                    Baseline = DailyMetricValue(m, def.Metric), Claimed = false
                });
            }
        }

        private static long DailyMetricValue(GameModel m, DailyTaskMetric metric)
        {
            switch (metric)
            {
                case DailyTaskMetric.ServeCustomers: return m.Orders.TotalServed;
                case DailyTaskMetric.CollectCash: return m.Idle.Money > long.MaxValue ? long.MaxValue : (long)m.Idle.Money;
                case DailyTaskMetric.BuyUpgrades: return m.Idle.StationSpeedLevel + m.Idle.CollectRadiusLevel + m.Idle.CarryCapacityLevel;
                case DailyTaskMetric.HireWorker: return CountWorkers(m.Idle);
                case DailyTaskMetric.CompleteRestorationPhase: return RestorationFormulas.TotalPhasesComplete(m.Landmarks);
                default: return 0;
            }
        }

        private static void FireBeats(GameModel m, IReadOnlyList<StoryBeatDefinition> catalog, StoryTrigger trigger, bool condition, List<string> played)
        {
            if (!condition) return;
            var beats = StoryBeatFormulas.BeatsForTrigger(catalog, trigger, m.PlayedStoryBeats);
            foreach (var id in beats)
            {
                m.PlayedStoryBeats.Add(id);
                played.Add(id);
            }
        }

        private static bool AnyStock(GreyboxSimState idle)
        {
            foreach (var st in idle.Stations) if (st.Stock > 0) return true;
            return false;
        }

        private static bool LastStationUnlocked(GreyboxSimState idle) =>
            idle.Stations.Count > 0 && idle.Stations[idle.Stations.Count - 1].Unlocked;

        private static MasterToolContext BuildMasterToolContext(GameModel m) => new MasterToolContext
        {
            MaxStationLevel = MaxUpgradeLevel(m.Idle),
            OrdersServed = m.Orders.TotalServed,
            RestorationPhases = RestorationFormulas.TotalPhasesComplete(m.Landmarks),
            PrestigeCount = m.Meta.PrestigeCount,
            CollectedTools = m.CollectedMasterTools.Count
        };

        private static long MetricValue(GameModel m, AchievementMetric metric)
        {
            switch (metric)
            {
                case AchievementMetric.OrdersServed: return m.Orders.TotalServed;
                case AchievementMetric.StationsUnlocked: return CountUnlocked(m.Idle);
                case AchievementMetric.WorkersHired: return CountWorkers(m.Idle);
                case AchievementMetric.RestorationPhases: return RestorationFormulas.TotalPhasesComplete(m.Landmarks);
                case AchievementMetric.PrestigeCount: return m.Meta.PrestigeCount;
                case AchievementMetric.MasteryLevel: return m.Meta.MasteryLevel;
                case AchievementMetric.MoneyEarned: return m.Idle.Money > long.MaxValue ? long.MaxValue : (long)m.Idle.Money;
                default: return 0;
            }
        }

        private static int MaxUpgradeLevel(GreyboxSimState idle) =>
            Math.Max(idle.StationSpeedLevel, Math.Max(idle.CollectRadiusLevel, idle.CarryCapacityLevel));

        private static int CountUnlocked(GreyboxSimState idle)
        {
            int n = 0;
            foreach (var st in idle.Stations) if (st.Unlocked) n++;
            return n;
        }

        private static int CountWorkers(GreyboxSimState idle)
        {
            int n = 0;
            foreach (var st in idle.Stations) if (st.HasWorker) n++;
            return n;
        }

        private static bool Contains(List<string> set, string id)
        {
            foreach (var x in set) if (x == id) return true;
            return false;
        }
    }
}
