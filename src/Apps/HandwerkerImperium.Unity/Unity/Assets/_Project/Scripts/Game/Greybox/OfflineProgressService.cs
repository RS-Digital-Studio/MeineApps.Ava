using System;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: Rueckkehr-Verdienst. Nutzt <see cref="GreyboxSimulation.ComputeOfflineEarnings"/>,
    /// das die Staffelung (0.80/0.35/0.15/0.05) aus dem Domain-Port (<c>OfflineProgressFormulas</c>)
    /// wiederverwendet — die einzige laut P0-Spec ausdruecklich uebernommene Alt-Formel.
    /// </summary>
    public sealed class OfflineProgressService
    {
        private readonly GreyboxSession _session;
        private readonly EconomyService _economy;

        public OfflineProgressService(GreyboxSession session, EconomyService economy)
        {
            _session = session;
            _economy = economy;
        }

        /// <summary>Vorschau auf den Offline-Verdienst seit dem letzten Speichern (ohne Gutschrift).</summary>
        public decimal Preview(long nowUtcTicks) =>
            GreyboxSimulation.ComputeOfflineEarnings(_session.State, _session.Balancing, ElapsedSeconds(nowUtcTicks));

        /// <summary>Schreibt den Offline-Verdienst gut (Dialog bestaetigt) + aktualisiert den Zeitstempel. Liefert den Betrag.</summary>
        public decimal Claim(long nowUtcTicks)
        {
            decimal amount = Preview(nowUtcTicks);
            GreyboxSimulation.ApplyOfflineEarnings(_session.State, amount, nowUtcTicks);
            if (amount > 0) _economy.RaiseMoneyChanged();
            return amount;
        }

        /// <summary>Markiert den aktuellen Zeitpunkt als „zuletzt gesehen" (beim Speichern/Pausieren).</summary>
        public void MarkSeen(long nowUtcTicks) => _session.State.LastSeenUtcTicks = nowUtcTicks;

        private double ElapsedSeconds(long nowUtcTicks)
        {
            if (_session.State.LastSeenUtcTicks <= 0) return 0;
            long deltaTicks = nowUtcTicks - _session.State.LastSeenUtcTicks;
            if (deltaTicks <= 0) return 0;
            return new TimeSpan(deltaTicks).TotalSeconds;
        }
    }
}
