#nullable enable

namespace HandwerkerImperium.Domain.Orders
{
    /// <summary>
    /// Kunden-Queue am Tresen + gelegentlicher Eil-Auftrag (P1 §3 / GDD §6.3). Bewusst schlank:
    /// keine 6 Order-Typen / Risk / Material des Originals — Kunden treffen über Zeit ein (bedienen = Geld),
    /// ein Eil-Auftrag läuft auf Timer mit Bonus-Multiplikator und ist per Ad verlängerbar. Reine,
    /// Unity-freie, NUnit-testbare Mathematik; der Game-Layer mappt Bedienung auf den Stations-Verkaufswert.
    /// </summary>
    public sealed class OrderQueueState
    {
        /// <summary>Akkumulierte Spawn-Zeit (Sekunden), die noch nicht zu einem Kunden wurde.</summary>
        public double SpawnAccumulatorSeconds;
        /// <summary>Wartende Kunden in der Queue.</summary>
        public int PendingCustomers;
        /// <summary>Insgesamt bediente Kunden (kontoweit relevant; speist u. a. Stern-Volumen).</summary>
        public long TotalServed;
        /// <summary>Aktiver Eil-Auftrag (optional).</summary>
        public RushOrderState Rush = new RushOrderState();
    }

    /// <summary>Eil-Auftrag: zeitlich begrenzt, Bonus-Multiplikator, per Ad verlängerbar.</summary>
    public sealed class RushOrderState
    {
        public bool Active;
        public decimal RewardMultiplier = 1m;
        public long ExpiresAtUtcTicks;
    }

    public static class OrderQueueFormulas
    {
        private const long TicksPerSecond = 10_000_000L;

        /// <summary>
        /// Schreibt den Kunden-Zustrom fort: füllt die Queue im <paramref name="spawnIntervalSeconds"/>-Takt
        /// bis maximal <paramref name="maxQueue"/>. Liefert die Anzahl neu eingetroffener Kunden.
        /// </summary>
        public static int Tick(OrderQueueState state, double dtSeconds, double spawnIntervalSeconds, int maxQueue)
        {
            if (state == null || dtSeconds <= 0 || spawnIntervalSeconds <= 0 || maxQueue <= 0) return 0;
            int spawned = 0;
            state.SpawnAccumulatorSeconds += dtSeconds;
            while (state.SpawnAccumulatorSeconds >= spawnIntervalSeconds && state.PendingCustomers < maxQueue)
            {
                state.PendingCustomers++;
                spawned++;
                state.SpawnAccumulatorSeconds -= spawnIntervalSeconds;
            }
            // Bei voller Queue den Akkumulator auf EIN Intervall deckeln: nach dem Bedienen rückt höchstens
            // ein Kunde sofort nach (kein angestauter Burst), statt unbegrenzt zu akkumulieren.
            if (state.PendingCustomers >= maxQueue && state.SpawnAccumulatorSeconds > spawnIntervalSeconds)
                state.SpawnAccumulatorSeconds = spawnIntervalSeconds;
            return spawned;
        }

        /// <summary>Bedient bis zu <paramref name="requested"/> Kunden. Liefert die tatsächlich bediente Anzahl.</summary>
        public static int Serve(OrderQueueState state, int requested)
        {
            if (state == null || requested <= 0 || state.PendingCustomers <= 0) return 0;
            int served = requested < state.PendingCustomers ? requested : state.PendingCustomers;
            state.PendingCustomers -= served;
            state.TotalServed += served;
            return served;
        }

        /// <summary>Startet einen Eil-Auftrag mit Bonus-Multiplikator und Laufzeit ab <paramref name="nowUtcTicks"/>.</summary>
        public static void StartRush(OrderQueueState state, decimal rewardMultiplier, double durationSeconds, long nowUtcTicks)
        {
            if (state == null || durationSeconds <= 0) return;
            state.Rush.Active = true;
            state.Rush.RewardMultiplier = rewardMultiplier < 1m ? 1m : rewardMultiplier;
            state.Rush.ExpiresAtUtcTicks = nowUtcTicks + (long)(durationSeconds * TicksPerSecond);
        }

        /// <summary>Verlängert den laufenden Eil-Auftrag (Ad-Belohnung). Ohne Effekt, wenn keiner aktiv ist.</summary>
        public static void ExtendRush(OrderQueueState state, double extraSeconds)
        {
            if (state == null || !state.Rush.Active || extraSeconds <= 0) return;
            state.Rush.ExpiresAtUtcTicks += (long)(extraSeconds * TicksPerSecond);
        }

        /// <summary>True, wenn ein Eil-Auftrag aktiv und noch nicht abgelaufen ist.</summary>
        public static bool IsRushActive(OrderQueueState state, long nowUtcTicks) =>
            state != null && state.Rush.Active && nowUtcTicks < state.Rush.ExpiresAtUtcTicks;

        /// <summary>Deaktiviert einen abgelaufenen Eil-Auftrag. Liefert true, wenn er gerade beendet wurde.</summary>
        public static bool ExpireRushIfDue(OrderQueueState state, long nowUtcTicks)
        {
            if (state == null || !state.Rush.Active || nowUtcTicks < state.Rush.ExpiresAtUtcTicks) return false;
            state.Rush.Active = false;
            state.Rush.RewardMultiplier = 1m;
            return true;
        }

        /// <summary>Aktueller Belohnungs-Multiplikator (Eil-Bonus, sonst 1).</summary>
        public static decimal CurrentRewardMultiplier(OrderQueueState state, long nowUtcTicks) =>
            IsRushActive(state, nowUtcTicks) ? state!.Rush.RewardMultiplier : 1m;
    }
}
