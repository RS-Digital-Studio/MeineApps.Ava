#nullable enable
using System;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>Zieltyp einer Tagesaufgabe (auf die 3D-Idle-Mechanik gemappt).</summary>
    public enum DailyTaskMetric
    {
        ServeCustomers = 0,
        CollectCash = 1,
        BuyUpgrades = 2,
        HireWorker = 3,
        CompleteRestorationPhase = 4
    }

    /// <summary>Definition einer Tagesaufgabe: Ziel + Gem-Belohnung.</summary>
    public sealed class DailyTaskDefinition
    {
        public string Id;
        public DailyTaskMetric Metric;
        public long Target;
        public int GemReward;

        public DailyTaskDefinition(string id, DailyTaskMetric metric, long target, int gemReward)
        {
            Id = id;
            Metric = metric;
            Target = target;
            GemReward = gemReward;
        }
    }

    /// <summary>
    /// Tägliche Aufgaben (P2 §3, GDD §10): 3 kleine Tagesziele → Gems, Reset auf den UTC-Tag.
    /// Reine, Unity-freie Logik (Fortschritt/Abschluss/Reset). Die 3 Tagesaufgaben werden vom Game-Layer
    /// pro Tag aus einem Pool gezogen.
    /// </summary>
    public static class DailyTaskFormulas
    {
        /// <summary>True, wenn der aktuelle Wert das Tagesziel erreicht.</summary>
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
        /// True, wenn die Tagesaufgaben seit <paramref name="lastResetUtcTicks"/> auf einen neuen UTC-Tag
        /// zurückzusetzen sind (Mitternacht-UTC überschritten oder noch nie gesetzt).
        /// </summary>
        public static bool ShouldReset(long lastResetUtcTicks, long nowUtcTicks)
        {
            if (lastResetUtcTicks <= 0) return true;
            DateTime last = new DateTime(lastResetUtcTicks, DateTimeKind.Utc).Date;
            DateTime now = new DateTime(nowUtcTicks, DateTimeKind.Utc).Date;
            return now > last;
        }
    }
}
