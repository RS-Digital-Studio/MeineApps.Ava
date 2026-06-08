using UnityEngine;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: Hold-to-Pay-Upgrade-Pad fuer eine der drei Achsen (Stations-Tempo / Sammelradius /
    /// Trag-Kapazitaet). Kauft je Zahl-Schritt eine Stufe (geometrische Kosten via UpgradePadService).
    /// </summary>
    public sealed class UpgradePadView : HoldToPayPad
    {
        [SerializeField] private UpgradeTrack track = UpgradeTrack.StationSpeed;

        protected override bool IsDone() => false; // immer weiter upgradebar

        protected override void TryPayStep()
        {
            controller.Upgrades.Buy(track);
        }
    }
}
