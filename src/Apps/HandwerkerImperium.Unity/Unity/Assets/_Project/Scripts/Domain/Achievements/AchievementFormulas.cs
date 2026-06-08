#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Achievements
{
    /// <summary>Bezugsgröße eines Achievement-Ziels (auf die 3D-Idle-Mechanik gemappt).</summary>
    public enum AchievementMetric
    {
        OrdersServed = 0,
        StationsUnlocked = 1,
        WorkersHired = 2,
        RestorationPhases = 3,
        PrestigeCount = 4,
        MasteryLevel = 5,
        MoneyEarned = 6
    }

    /// <summary>Achievement-Definition: messbares Ziel + Gem-Belohnung (datengetrieben, Katalog im Game-Layer).</summary>
    public sealed class AchievementDefinition
    {
        public string Id;
        public AchievementMetric Metric;
        public long Target;
        public int GemReward;

        public AchievementDefinition(string id, AchievementMetric metric, long target, int gemReward)
        {
            Id = id;
            Metric = metric;
            Target = target;
            GemReward = gemReward;
        }
    }

    /// <summary>
    /// Achievement-Fortschritt (P2 §3/§4, GDD §10): messbare Ziele über alle Mechaniken + Gem-Belohnung.
    /// Reine, Unity-freie Logik (Fortschritt/Abschluss/Belohnung); der 95+-Katalog lebt als Content im Game-Layer.
    /// </summary>
    public static class AchievementFormulas
    {
        /// <summary>True, wenn der aktuelle Wert das Ziel erreicht.</summary>
        public static bool IsComplete(long current, long target) => target > 0 && current >= target;

        /// <summary>Fortschritt 0..1 (geklemmt).</summary>
        public static double Progress01(long current, long target)
        {
            if (target <= 0) return 1.0;
            if (current <= 0) return 0.0;
            double p = (double)current / target;
            return p > 1.0 ? 1.0 : p;
        }

        /// <summary>
        /// Liefert die Ids neu abgeschlossener Achievements (im Ziel erreicht, noch nicht in
        /// <paramref name="alreadyClaimed"/>) für einen Metrik-Wert. Der Game-Layer bucht die Gems gut.
        /// </summary>
        public static List<string> NewlyCompleted(
            IReadOnlyList<AchievementDefinition>? catalog, AchievementMetric metric, long currentValue,
            IReadOnlyCollection<string>? alreadyClaimed)
        {
            var result = new List<string>();
            if (catalog == null) return result;
            foreach (var def in catalog)
            {
                if (def == null || def.Metric != metric) continue;
                if (!IsComplete(currentValue, def.Target)) continue;
                if (alreadyClaimed != null && Contains(alreadyClaimed, def.Id)) continue;
                result.Add(def.Id);
            }
            return result;
        }

        /// <summary>Summe der Gem-Belohnungen für eine Menge abgeschlossener Achievement-Ids.</summary>
        public static int TotalGemReward(IReadOnlyList<AchievementDefinition>? catalog, IReadOnlyCollection<string>? ids)
        {
            if (catalog == null || ids == null) return 0;
            int total = 0;
            foreach (var def in catalog)
                if (def != null && Contains(ids, def.Id))
                    total += def.GemReward;
            return total;
        }

        private static bool Contains(IReadOnlyCollection<string> set, string id)
        {
            foreach (var x in set)
                if (x == id) return true;
            return false;
        }
    }
}
