using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: Hold-to-Pay-Upgrades (Stations-Tempo / Sammelradius / Trag-Kapazitaet) mit
    /// geometrischer Kostenkurve. Die rampende Ausgaberate liegt im UpgradePadView (MonoBehaviour);
    /// dieser Service kapselt Kosten + Kauf ueber dem Idle-Kern.
    /// </summary>
    public sealed class UpgradePadService
    {
        private readonly GreyboxSession _session;
        private readonly EconomyService _economy;

        public UpgradePadService(GreyboxSession session, EconomyService economy)
        {
            _session = session;
            _economy = economy;
        }

        public decimal CostFor(UpgradeTrack track) =>
            GreyboxSimulation.UpgradeCostFor(_session.State, _session.Balancing, track);

        public int LevelOf(UpgradeTrack track) => _session.State.GetLevel(track);

        /// <summary>Kauft eine Upgrade-Stufe, wenn genug Geld da ist. Liefert true bei Erfolg.</summary>
        public bool Buy(UpgradeTrack track)
        {
            bool ok = GreyboxSimulation.BuyUpgrade(_session.State, _session.Balancing, track);
            if (ok) _economy.RaiseMoneyChanged();
            return ok;
        }
    }
}
