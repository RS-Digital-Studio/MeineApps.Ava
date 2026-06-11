using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §3: Bauzaun-Plot — Hold-to-Pay schaltet die gesperrte 4. Station frei; der Zaun verschwindet,
    /// die StationView zeigt die Station ab dann (PlotUnlockService -&gt; Stock-Produktion startet).
    /// </summary>
    public sealed class PlotFenceView : HoldToPayPad
    {
        [SerializeField] private int stationIndex = 3;
        [SerializeField] private GameObject fenceVisual;

        protected override bool IsDone() => controller != null && controller.Plots.IsUnlocked(stationIndex);

        protected override void TryPayStep()
        {
            if (controller.Plots.Unlock(stationIndex))
            {
                if (fenceVisual != null) fenceVisual.SetActive(false);
                controller.Audio?.Play(GameSfx.PlotUnlock);
            }
        }
    }
}
