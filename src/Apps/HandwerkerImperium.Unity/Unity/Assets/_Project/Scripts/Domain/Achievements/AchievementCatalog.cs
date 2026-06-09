#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Achievements
{
    /// <summary>
    /// Default-Achievement-Katalog (repräsentativer Auszug; voller 95+-Satz als Content im Game-Layer/Addressables).
    /// Reine Daten — messbare Ziele über die Kern-Mechaniken + Gem-Belohnungen.
    /// </summary>
    public static class AchievementCatalog
    {
        public static List<AchievementDefinition> Default()
        {
            return new List<AchievementDefinition>
            {
                new AchievementDefinition("orders_10",    AchievementMetric.OrdersServed,      10,  10),
                new AchievementDefinition("orders_100",   AchievementMetric.OrdersServed,     100,  30),
                new AchievementDefinition("orders_1000",  AchievementMetric.OrdersServed,    1000, 100),
                new AchievementDefinition("workshops_4",  AchievementMetric.StationsUnlocked,   4,  20),
                new AchievementDefinition("worker_1",     AchievementMetric.WorkersHired,       1,  10),
                new AchievementDefinition("restore_5",    AchievementMetric.RestorationPhases,  5,  30),
                new AchievementDefinition("prestige_1",   AchievementMetric.PrestigeCount,      1,  50),
                new AchievementDefinition("prestige_3",   AchievementMetric.PrestigeCount,      3, 200),
                new AchievementDefinition("mastery_10",   AchievementMetric.MasteryLevel,      10,  50),
            };
        }
    }
}
