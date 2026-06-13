using System;
using System.Collections.Generic;
using UnityEngine;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
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
        [Tooltip("Plot-Freischalt-Kosten (0 = globaler plotUnlockCost-Fallback).")]
        public float unlockCost;
    }

    /// <summary>
    /// P0-Spec §4: alle spass-relevanten Tuning-Knoepfe als EIN ScriptableObject (kein Hardcoding,
    /// CLAUDE.md-Verbot). Mappt 1:1 auf das Unity-freie <see cref="IdleBalancing"/> (Domain) via <see cref="ToDomain"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "BalancingConfig", menuName = "HandwerkerImperium/P0 Greybox Balancing")]
    public sealed class BalancingConfig : ScriptableObject
    {
        [Header("Avatar")]
        public float walkSpeed = 3.2f;
        public float collectRadius = 2.5f;
        public int carryCapacity = 5;

        [Header("Stationen: 10 Gewerke (GDD §6.1), Start nur Schreinerei")]
        public StationConfig[] stations = new StationConfig[]
        {
            new StationConfig { id = "schreiner", produceInterval = 2f, stackCap = 8, sellValue = 5f, unlockedAtStart = true },
            new StationConfig { id = "klempner", produceInterval = 2.5f, stackCap = 8, sellValue = 8f, unlockedAtStart = false, unlockCost = 500f },
            new StationConfig { id = "elektriker", produceInterval = 3f, stackCap = 8, sellValue = 12f, unlockedAtStart = false, unlockCost = 1500f },
            new StationConfig { id = "maler", produceInterval = 3f, stackCap = 10, sellValue = 16f, unlockedAtStart = false, unlockCost = 4000f },
            new StationConfig { id = "dachdecker", produceInterval = 3.5f, stackCap = 10, sellValue = 22f, unlockedAtStart = false, unlockCost = 9000f },
            new StationConfig { id = "bauunternehmer", produceInterval = 3.5f, stackCap = 10, sellValue = 30f, unlockedAtStart = false, unlockCost = 18000f },
            new StationConfig { id = "architekt", produceInterval = 4f, stackCap = 12, sellValue = 42f, unlockedAtStart = false, unlockCost = 32000f },
            new StationConfig { id = "generalunternehmer", produceInterval = 4f, stackCap = 12, sellValue = 60f, unlockedAtStart = false, unlockCost = 55000f },
            new StationConfig { id = "meisterschmied", produceInterval = 4.5f, stackCap = 12, sellValue = 85f, unlockedAtStart = false, unlockCost = 90000f },
            new StationConfig { id = "innovationslabor", produceInterval = 5f, stackCap = 15, sellValue = 120f, unlockedAtStart = false, unlockCost = 140000f },
        };

        [Header("Upgrades (geometrisch: base × growth^level)")]
        public float upgradeCostBase = 50f;
        public float upgradeCostGrowth = 1.6f;
        public float upgradeStep = 0.25f;

        [Header("Worker")]
        public float workerHireCost = 200f;
        public float workerCarrySpeed = 1f;
        [Tooltip("Worker-Tempo-Stufen (GDD §6.2): Basis-Kosten, Wachstum je Stufe, Effekt, Max-Stufen.")]
        public float workerUpgradeCostBase = 300f;
        public float workerUpgradeCostGrowth = 2.2f;
        public float workerUpgradeStep = 0.5f;
        public int workerMaxLevel = 4;
        [Tooltip("Werkstatt-Ausbaustufen (GDD §6.1): Kosten-Basis, Wachstum, Wert-Bonus/Stufe, Max.")]
        public float stationBuildCostBase = 400f;
        public float stationBuildCostGrowth = 2.6f;
        public float stationBuildStep = 0.5f;
        public int stationBuildMaxLevel = 3;

        [Header("Plot-Unlock-Fallback (greift nur bei unlockCost = 0)")]
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
                WorkerUpgradeCostBase = (decimal)workerUpgradeCostBase,
                WorkerUpgradeCostGrowth = workerUpgradeCostGrowth,
                WorkerUpgradeStep = workerUpgradeStep,
                WorkerMaxLevel = workerMaxLevel,
                StationBuildCostBase = (decimal)stationBuildCostBase,
                StationBuildCostGrowth = stationBuildCostGrowth,
                StationBuildStep = stationBuildStep,
                StationBuildMaxLevel = stationBuildMaxLevel,
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
                    b.Stations.Add(new StationBalance(s.id, s.produceInterval, s.stackCap, (decimal)s.sellValue, s.unlockedAtStart, (decimal)s.unlockCost));
                }
            }
            return b;
        }
    }
}
