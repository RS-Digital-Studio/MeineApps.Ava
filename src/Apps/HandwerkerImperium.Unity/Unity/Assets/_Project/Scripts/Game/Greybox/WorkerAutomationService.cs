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

        /// <summary>Gekaufte Worker-Tempo-Stufen einer Station (GDD §6.2; 0 = Basis).</summary>
        public int Level(int stationIndex) =>
            stationIndex >= 0 && stationIndex < _session.State.Stations.Count ? _session.State.Stations[stationIndex].WorkerLevel : 0;

        /// <summary>Maximal kaufbare Worker-Stufen.</summary>
        public int MaxLevel => _session.Balancing.WorkerMaxLevel;

        /// <summary>Kosten der naechsten Worker-Stufe.</summary>
        public decimal UpgradeCost(int stationIndex) =>
            GreyboxSimulation.WorkerUpgradeCostFor(_session.State, _session.Balancing, stationIndex);

        /// <summary>Kauft die naechste Worker-Tempo-Stufe. Liefert true bei Erfolg.</summary>
        public bool Upgrade(int stationIndex)
        {
            bool ok = GreyboxSimulation.UpgradeWorker(_session.State, _session.Balancing, stationIndex);
            if (ok) _economy.RaiseMoneyChanged();
            return ok;
        }
    }
}
