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
        /// <summary>True wenn die Station zu Spielbeginn offen ist (GDD §5.1: nur die Schreinerei).</summary>
        public bool UnlockedAtStart;
        /// <summary>Plot-Freischalt-Kosten dieser Station (0 = Fallback auf <see cref="IdleBalancing.PlotUnlockCost"/>).</summary>
        public decimal UnlockCost;

        public StationBalance() { }

        public StationBalance(string id, double produceInterval, int stackCap, decimal sellValue, bool unlockedAtStart, decimal unlockCost = 0m)
        {
            Id = id;
            ProduceInterval = produceInterval;
            StackCap = stackCap;
            SellValue = sellValue;
            UnlockedAtStart = unlockedAtStart;
            UnlockCost = unlockCost;
        }

        public StationBalance Clone() => new StationBalance(Id, ProduceInterval, StackCap, SellValue, UnlockedAtStart, UnlockCost);
    }

    /// <summary>
    /// Alle spaß-relevanten Tuning-Knoepfe des Greybox-Loops an EINER Stelle (P0-Spec §4),
    /// damit der Loop in Minuten iterierbar ist. Unity-frei (das <c>BalancingConfig</c>-
    /// ScriptableObject im Game-Layer mappt 1:1 hierauf). Default-Werte = Start-Tuning.
    /// </summary>
    public sealed class IdleBalancing
    {
        // ── Avatar ─────────────────────────────────────────────────────────
        /// <summary>Lauftempo in m/s. 3,2 = flottes Gehen einer 1,8-m-Figur (5 wäre Sprint —
        /// die Schritt-Animation kann dann nicht mehr glaubwürdig folgen).</summary>
        public double WalkSpeed = 3.2;
        public double CollectRadius = 2.5;
        public int CarryCapacity = 5;

        // ── Stationen: alle 10 Gewerke (GDD §6.1), Start nur Schreinerei (GDD §5.1) ──
        // Plot-Kosten-Progression auf den Akt-1-Bogen getunt (Prestige ab ~100k+, PP=floor(sqrt(money/100k))):
        // fruehe Plots in Minuten, spaete Plots tragen den Akt bis zur 5★-/Prestige-Reife.
        public List<StationBalance> Stations = new List<StationBalance>
        {
            new StationBalance("schreiner", 2.0, 8, 5m, true),
            new StationBalance("klempner", 2.5, 8, 8m, false, 500m),
            new StationBalance("elektriker", 3.0, 8, 12m, false, 1500m),
            new StationBalance("maler", 3.0, 10, 16m, false, 4000m),
            new StationBalance("dachdecker", 3.5, 10, 22m, false, 9000m),
            new StationBalance("bauunternehmer", 3.5, 10, 30m, false, 18000m),
            new StationBalance("architekt", 4.0, 12, 42m, false, 32000m),
            new StationBalance("generalunternehmer", 4.0, 12, 60m, false, 55000m),
            new StationBalance("meisterschmied", 4.5, 12, 85m, false, 90000m),
            new StationBalance("innovationslabor", 5.0, 15, 120m, false, 140000m),
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
        /// <summary>Waren/Sekunde, die ein angestellter Worker Station->Tresen bewegt (Stufe 0).</summary>
        public double WorkerCarrySpeed = 1.0;
        /// <summary>Worker-Tempo-Stufen (GDD §6.2): Basis-Kosten der ersten Stufe.</summary>
        public decimal WorkerUpgradeCostBase = 300m;
        /// <summary>Geometrischer Wachstumsfaktor je Worker-Stufe.</summary>
        public double WorkerUpgradeCostGrowth = 2.2;
        /// <summary>Tempo-Effekt je Worker-Stufe (0.5 = +50 % Tragegeschwindigkeit).</summary>
        public double WorkerUpgradeStep = 0.5;
        /// <summary>Maximal kaufbare Worker-Stufen (4 = insgesamt 5 Leistungsstufen inkl. Basis).</summary>
        public int WorkerMaxLevel = 4;

        // ── Plot-Unlock-Fallback (greift nur, wenn StationBalance.UnlockCost = 0) ──
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
