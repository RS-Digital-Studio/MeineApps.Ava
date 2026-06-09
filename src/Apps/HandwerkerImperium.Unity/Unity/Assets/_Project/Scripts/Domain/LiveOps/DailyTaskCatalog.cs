#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Pool der Tagesaufgaben (P2 §3): pro UTC-Tag werden 3 daraus gezogen. Reine Daten — Ziel-Metrik + Gem-Belohnung.
    /// </summary>
    public static class DailyTaskCatalog
    {
        public static List<DailyTaskDefinition> Pool()
        {
            return new List<DailyTaskDefinition>
            {
                new DailyTaskDefinition("dt_serve_10",   DailyTaskMetric.ServeCustomers,           10, 15),
                new DailyTaskDefinition("dt_serve_50",   DailyTaskMetric.ServeCustomers,           50, 40),
                new DailyTaskDefinition("dt_upgrades_3", DailyTaskMetric.BuyUpgrades,               3, 20),
                new DailyTaskDefinition("dt_worker_1",   DailyTaskMetric.HireWorker,                1, 15),
                new DailyTaskDefinition("dt_restore_1",  DailyTaskMetric.CompleteRestorationPhase,  1, 25),
                new DailyTaskDefinition("dt_cash_10000", DailyTaskMetric.CollectCash,           10000, 20),
            };
        }
    }
}
