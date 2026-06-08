using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: NPC-Anstellung (Hire-Pad) + Automatisierung — der Worker uebernimmt das Tragen
    /// Station->Tresen und ersetzt die Spielerlauferei. Logik ueber dem Idle-Kern; WorkerNpc rendert.
    /// </summary>
    public sealed class WorkerAutomationService
    {
        private readonly GreyboxSession _session;
        private readonly EconomyService _economy;

        public WorkerAutomationService(GreyboxSession session, EconomyService economy)
        {
            _session = session;
            _economy = economy;
        }

        public decimal HireCost => _session.Balancing.WorkerHireCost;

        public bool HasWorker(int stationIndex) =>
            stationIndex >= 0 && stationIndex < _session.State.Stations.Count && _session.State.Stations[stationIndex].HasWorker;

        /// <summary>Automatisierte Stationen fortschreiben (Stock->Geld). Game-Loop, pro Frame. Liefert das Geld-Delta.</summary>
        public decimal Tick(double dtSeconds)
        {
            decimal earned = GreyboxSimulation.TickWorkers(_session.State, _session.Balancing, dtSeconds);
            if (earned > 0) _economy.RaiseMoneyChanged();
            return earned;
        }

        /// <summary>Stellt einen Worker an einer Station an (einmalig). Liefert true bei Erfolg.</summary>
        public bool Hire(int stationIndex)
        {
            bool ok = GreyboxSimulation.HireWorker(_session.State, _session.Balancing, stationIndex);
            if (ok) _economy.RaiseMoneyChanged();
            return ok;
        }
    }
}
