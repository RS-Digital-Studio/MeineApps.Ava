using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: Produktion je Station (Intervall -> Waren-Stapel bis StackCap) + Spieler-Aufnahme.
    /// Reine Logik ueber dem verifizierten Idle-Kern; die StationView (MonoBehaviour) rendert nur.
    /// </summary>
    public sealed class StationService
    {
        private readonly GreyboxSession _session;

        public StationService(GreyboxSession session) { _session = session; }

        public int StationCount => _session.State.Stations.Count;

        public StationState Get(int index) =>
            (index >= 0 && index < _session.State.Stations.Count) ? _session.State.Stations[index] : null;

        public int Stock(int index) { var s = Get(index); return s != null ? s.Stock : 0; }

        public bool IsUnlocked(int index) { var s = Get(index); return s != null && s.Unlocked; }

        /// <summary>Schreibt die Produktion aller Stationen fort (Game-Loop, pro Frame).</summary>
        public void Tick(double dtSeconds) =>
            GreyboxSimulation.TickProduction(_session.State, _session.Balancing, dtSeconds);

        /// <summary>Avatar nimmt bis zu <paramref name="requested"/> Waren an einer Station auf. Liefert die aufgenommene Menge.</summary>
        public int Pickup(int stationIndex, int requested) =>
            GreyboxSimulation.PlayerPickup(_session.State, _session.Balancing, stationIndex, requested);
    }
}
