using System;
using System.Collections.Generic;
using UnityEngine;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game.Greybox
{
    /// <summary>Pro-Station-Tuning im Inspector (Unity serialisiert kein decimal -> float, Cast in ToDomain).</summary>
    [Serializable]
    public sealed class StationConfig
    {
        public string id = "";
        public float produceInterval = 2f;
        public int stackCap = 8;
        public float sellValue = 5f;
        public bool unlockedAtStart = true;
    }

    /// <summary>
    /// P0-Spec §4: alle spass-relevanten Tuning-Knoepfe als EIN ScriptableObject (kein Hardcoding,
    /// CLAUDE.md-Verbot). Mappt 1:1 auf das Unity-freie <see cref="IdleBalancing"/> (Domain) via <see cref="ToDomain"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "BalancingConfig", menuName = "HandwerkerImperium/P0 Greybox Balancing")]
    public sealed class BalancingConfig : ScriptableObject
    {
        [Header("Avatar")]
        public float walkSpeed = 5f;
        public float collectRadius = 2.5f;
        public int carryCapacity = 5;

        [Header("Stationen (3 offen + 1 ueber Plot-Unlock)")]
        public StationConfig[] stations = new StationConfig[]
        {
            new StationConfig { id = "schreiner", produceInterval = 2f, stackCap = 8, sellValue = 5f, unlockedAtStart = true },
            new StationConfig { id = "klempner", produceInterval = 2.5f, stackCap = 8, sellValue = 8f, unlockedAtStart = true },
            new StationConfig { id = "elektriker", produceInterval = 3f, stackCap = 8, sellValue = 12f, unlockedAtStart = true },
            new StationConfig { id = "dachdecker", produceInterval = 3.5f, stackCap = 8, sellValue = 20f, unlockedAtStart = false },
        };

        [Header("Upgrades (geometrisch: base × growth^level)")]
        public float upgradeCostBase = 50f;
        public float upgradeCostGrowth = 1.6f;
        public float upgradeStep = 0.25f;

        [Header("Worker")]
        public float workerHireCost = 200f;
        public float workerCarrySpeed = 1f;

        [Header("Plot-Unlock (4. Station)")]
        public float plotUnlockCost = 500f;

        [Header("Offline")]
        public float offlineCapSeconds = 7200f;
        public float offlineRatePerWorker = 0f;

        /// <summary>Erzeugt das Unity-freie Domain-Balancing (decimal-Casts der Float-Inspector-Werte).</summary>
        public IdleBalancing ToDomain()
        {
            var b = new IdleBalancing
            {
                WalkSpeed = walkSpeed,
                CollectRadius = collectRadius,
                CarryCapacity = carryCapacity,
                UpgradeCostBase = (decimal)upgradeCostBase,
                UpgradeCostGrowth = upgradeCostGrowth,
                UpgradeStep = upgradeStep,
                WorkerHireCost = (decimal)workerHireCost,
                WorkerCarrySpeed = workerCarrySpeed,
                PlotUnlockCost = (decimal)plotUnlockCost,
                OfflineCapSeconds = offlineCapSeconds,
                OfflineRatePerWorker = (decimal)offlineRatePerWorker,
                Stations = new List<StationBalance>()
            };
            if (stations != null)
            {
                for (int i = 0; i < stations.Length; i++)
                {
                    var s = stations[i];
                    b.Stations.Add(new StationBalance(s.id, s.produceInterval, s.stackCap, (decimal)s.sellValue, s.unlockedAtStart));
                }
            }
            return b;
        }
    }
}
