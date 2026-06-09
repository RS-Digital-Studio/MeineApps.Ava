#nullable enable
using System;
using System.Collections.Generic;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Achievements;
using HandwerkerImperium.Domain.Restoration;
using HandwerkerImperium.Domain.Story;

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
