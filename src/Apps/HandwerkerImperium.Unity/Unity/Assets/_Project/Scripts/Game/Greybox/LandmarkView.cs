using UnityEngine;
using HandwerkerImperium.Domain.Restoration;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Physisches Wahrzeichen des Stadt-Wiederaufbaus (GDD §6.4): der Avatar steht auf der
    /// Sanieren-Zone und investiert per Hold-to-Pay Geld in Bauphasen; das Fortschritts-Schild
    /// zählt mit, und mit der letzten Phase tauscht die Ruine sichtbar gegen das sanierte Modell
    /// (der Vorher/Nachher-Moment des Genres). Visual-Sync läuft in LateUpdate — Update gehört
    /// der Hold-to-Pay-Basisklasse (private Unity-Message, würde sonst verdeckt).
    /// </summary>
    public sealed class LandmarkView : HoldToPayPad
    {
        [SerializeField] private string landmarkId = "";
        [SerializeField] private GameObject ruinedVisual;
        [SerializeField] private GameObject restoredVisual;
        [SerializeField] private TextMesh progressText;
        [SerializeField] private float investStep = 250f;

        protected override bool IsDone()
        {
            var lm = controller != null ? controller.GetLandmark(landmarkId) : null;
            return lm == null || RestorationFormulas.IsComplete(lm);
        }

        protected override void TryPayStep()
        {
            controller.InvestLandmark(landmarkId, (decimal)investStep);
        }

        private void LateUpdate()
        {
            var lm = controller != null ? controller.GetLandmark(landmarkId) : null;
            if (lm == null) return;
            bool done = RestorationFormulas.IsComplete(lm);
            if (ruinedVisual != null && ruinedVisual.activeSelf == done) ruinedVisual.SetActive(!done);
            if (restoredVisual != null && restoredVisual.activeSelf != done) restoredVisual.SetActive(done);
            if (progressText != null)
            {
                string s = lm.PhasesComplete + "/" + lm.TotalPhases;
                if (progressText.text != s) progressText.text = s;
            }
        }
    }
}
