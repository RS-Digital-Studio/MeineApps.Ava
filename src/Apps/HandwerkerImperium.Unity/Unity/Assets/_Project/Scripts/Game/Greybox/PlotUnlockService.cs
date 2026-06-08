using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: Bauzaun-Plot -> Freischaltung der gesperrten 4. Station bei Bezahlung (Hold-to-Pay).
    /// Die Hold-Logik liegt im PlotFenceView; dieser Service kapselt Kosten + Freischaltung.
    /// </summary>
    public sealed class PlotUnlockService
    {
        private readonly GreyboxSession _session;
        private readonly EconomyService _economy;

        public PlotUnlockService(GreyboxSession session, EconomyService economy)
        {
            _session = session;
            _economy = economy;
        }

        public decimal UnlockCost => _session.Balancing.PlotUnlockCost;

        public bool IsUnlocked(int stationIndex) =>
            stationIndex >= 0 && stationIndex < _session.State.Stations.Count && _session.State.Stations[stationIndex].Unlocked;

        /// <summary>Schaltet die gesperrte Station frei, wenn genug Geld da ist. Liefert true bei Erfolg.</summary>
        public bool Unlock(int stationIndex)
        {
            bool ok = GreyboxSimulation.UnlockPlot(_session.State, _session.Balancing, stationIndex);
            if (ok) _economy.RaiseMoneyChanged();
            return ok;
        }
    }
}
