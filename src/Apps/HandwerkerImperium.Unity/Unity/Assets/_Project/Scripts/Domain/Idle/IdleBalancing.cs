using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Idle
{
    /// <summary>
    /// Per-Station-Tuning fuer den Greybox-Loop (P0). Reine Daten, Unity-frei.
    /// </summary>
    public sealed class StationBalance
    {
        public string Id = "";
        /// <summary>Sekunden je produzierter Ware (vor Tempo-Upgrade).</summary>
        public double ProduceInterval;
        /// <summary>Maximaler Waren-Stapel an der Station.</summary>
        public int StackCap;
        /// <summary>Geld je abgegebener Ware am Tresen.</summary>
        public decimal SellValue;
        /// <summary>True wenn die Station zu Spielbeginn offen ist (Station 4 = false, Plot-Unlock).</summary>
        public bool UnlockedAtStart;

        public StationBalance() { }

        public StationBalance(string id, double produceInterval, int stackCap, decimal sellValue, bool unlockedAtStart)
        {
            Id = id;
            ProduceInterval = produceInterval;
            StackCap = stackCap;
            SellValue = sellValue;
            UnlockedAtStart = unlockedAtStart;
        }

        public StationBalance Clone() => new StationBalance(Id, ProduceInterval, StackCap, SellValue, UnlockedAtStart);
    }

    /// <summary>
    /// Alle spaß-relevanten Tuning-Knoepfe des Greybox-Loops an EINER Stelle (P0-Spec §4),
    /// damit der Loop in Minuten iterierbar ist. Unity-frei (das <c>BalancingConfig</c>-
    /// ScriptableObject im Game-Layer mappt 1:1 hierauf). Default-Werte = Start-Tuning.
    /// </summary>
    public sealed class IdleBalancing
    {
        // ── Avatar ─────────────────────────────────────────────────────────
        public double WalkSpeed = 5.0;
        public double CollectRadius = 2.5;
        public int CarryCapacity = 5;

        // ── Stationen (3 offen + 1 ueber Plot-Unlock) ──────────────────────
        public List<StationBalance> Stations = new List<StationBalance>
        {
            new StationBalance("schreiner", 2.0, 8, 5m, true),
            new StationBalance("klempner", 2.5, 8, 8m, true),
            new StationBalance("elektriker", 3.0, 8, 12m, true),
            new StationBalance("dachdecker", 3.5, 8, 20m, false),
        };

        // ── Upgrades (geometrische Kostenkurve, Effekt je Stufe) ───────────
        /// <summary>Basis-Kosten der ersten Upgrade-Stufe (Stufe 0 -> 1).</summary>
        public decimal UpgradeCostBase = 50m;
        /// <summary>Geometrischer Wachstumsfaktor je Stufe (cost = base * growth^level).</summary>
        public double UpgradeCostGrowth = 1.6;
        /// <summary>Effekt je Stufe (0.25 = +25 % Tempo/Radius/Kapazitaet je Stufe).</summary>
        public double UpgradeStep = 0.25;

        // ── Worker (NPC-Automatisierung) ───────────────────────────────────
        public decimal WorkerHireCost = 200m;
        /// <summary>Waren/Sekunde, die ein angestellter Worker Station->Tresen bewegt.</summary>
        public double WorkerCarrySpeed = 1.0;

        // ── Plot-Unlock (4. Station) ───────────────────────────────────────
        public decimal PlotUnlockCost = 500m;

        // ── Offline ────────────────────────────────────────────────────────
        /// <summary>Deckel der angerechneten Offline-Zeit in Sekunden (P0: 2 h).</summary>
        public double OfflineCapSeconds = 7200;
        /// <summary>
        /// Optionaler flacher Offline-Bonus je automatisierter Station (P0-Spec §4 „offlineRatePerWorker").
        /// Default 0 = nur die aus der Stations-Oekonomie abgeleitete Rate. &gt; 0 = additiver Bonus/Sekunde je Worker.
        /// </summary>
        public decimal OfflineRatePerWorker = 0m;
    }
}
