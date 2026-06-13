using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// „Die Stadt heilt" (Innovations-Kern der Welt): Der Sanierungs-Fortschritt der Wahrzeichen
    /// (abgeschlossene Bauphasen / alle Phasen) steuert sichtbar die Stimmung der Welt —
    /// die Farbsättigung wächst mit jeder Phase, und bei 1/3, 2/3 und 100 % erblühen
    /// zusätzliche Deko-Stufen (Blumen/Büsche), die der Builder vorab platziert.
    /// Der Spieler SIEHT seinen Fortschritt in der Landschaft, nicht nur in Zahlen.
    /// </summary>
    public sealed class WorldMoodController : MonoBehaviour
    {
        [SerializeField] private RuntimeGameController runtime;
        [SerializeField] private Volume postVolume;
        [Tooltip("Deko-Stufen: erscheinen bei >=1/3, >=2/3 und 100 % Stadt-Heilung.")]
        [SerializeField] private GameObject bloomStage1;
        [SerializeField] private GameObject bloomStage2;
        [SerializeField] private GameObject bloomStage3;
        [SerializeField] private float minSaturation = 2f;
        [SerializeField] private float maxSaturation = 16f;
        [SerializeField] private float checkInterval = 0.5f;

        private ColorAdjustments _colors;
        private float _timer;

        private void Start()
        {
            if (postVolume != null && postVolume.sharedProfile != null)
                postVolume.sharedProfile.TryGet(out _colors);
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;
            if (runtime == null || runtime.Model == null) return;

            float heal = Heal01();
            if (_colors != null)
                _colors.saturation.Override(Mathf.Lerp(minSaturation, maxSaturation, heal));
            SetStage(bloomStage1, heal >= 1f / 3f);
            SetStage(bloomStage2, heal >= 2f / 3f);
            SetStage(bloomStage3, heal >= 0.999f);
        }

        /// <summary>Stadt-Heilung 0..1 = abgeschlossene Wahrzeichen-Phasen / alle Phasen.</summary>
        private float Heal01()
        {
            var landmarks = runtime.Model.Landmarks;
            if (landmarks == null || landmarks.Count == 0) return 0f;
            int done = 0, total = 0;
            for (int i = 0; i < landmarks.Count; i++)
            {
                var lm = landmarks[i];
                if (lm == null) continue;
                done += lm.PhasesComplete;
                total += lm.TotalPhases;
            }
            return total > 0 ? Mathf.Clamp01(done / (float)total) : 0f;
        }

        private static void SetStage(GameObject stage, bool active)
        {
            if (stage != null && stage.activeSelf != active) stage.SetActive(active);
        }
    }
}
