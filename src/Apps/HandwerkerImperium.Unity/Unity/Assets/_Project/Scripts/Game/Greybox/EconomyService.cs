using System;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: Geldstand, Verkauf am Tresen (Geld-Quelle) und der Auto-Collect-Radius.
    /// Cash-Wuerfel im Game-Layer sind reine Optik — das Geld ist hier sim-autoritativ.
    /// </summary>
    public sealed class EconomyService
    {
        private readonly GreyboxSession _session;

        public EconomyService(GreyboxSession session) { _session = session; }

        /// <summary>Feuert mit dem neuen Geldstand bei jeder Aenderung.</summary>
        public event Action<decimal> MoneyChanged;

        public decimal Money => _session.State.Money;

        /// <summary>Effektiver Auto-Pickup-Sammelradius nach Upgrades.</summary>
        public double CollectRadius => GreyboxSimulation.EffectiveCollectRadius(_session.State, _session.Balancing);

        /// <summary>Effektive Trag-Kapazitaet des Avatars nach Upgrades.</summary>
        public int CarryCapacity => GreyboxSimulation.EffectiveCarryCapacity(_session.State, _session.Balancing);

        /// <summary>Verkauf getragener Waren am Tresen -> Geld. Liefert den Erloes (fuer den Cash-Spawn-Effekt).</summary>
        public decimal Sell(int stationIndex, int count)
        {
            decimal earned = GreyboxSimulation.SellCarried(_session.State, _session.Balancing, stationIndex, count);
            if (earned > 0) MoneyChanged?.Invoke(_session.State.Money);
            return earned;
        }

        /// <summary>Intern: Geld-Event nach Mutationen durch andere Services (Worker/Upgrade/Hire/Unlock/Offline).</summary>
        internal void RaiseMoneyChanged() => MoneyChanged?.Invoke(_session.State.Money);
    }
}
