using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Sichtbarer Werkstatt-Ausbau (GDD §6.1, Open-Shop): spiegelt die Domain-Ausbaustufe
    /// (<c>StationState.BuildLevel</c>) optisch — das Gebäude wächst je Stufe leicht und bekommt
    /// Wimpel auf dem Dach. So SIEHT der Spieler den Ausbau, nicht nur im Panel. Pollt den
    /// Runtime (gedrosselt) und reagiert auch nach Save-Load.
    /// </summary>
    public sealed class StationBuildView : MonoBehaviour
    {
        [SerializeField] private RuntimeGameController runtime;
        [SerializeField] private int stationIndex;
        [Tooltip("Gebäude-Visual (wird je Ausbaustufe leicht skaliert).")]
        [SerializeField] private Transform modelRoot;
        [Tooltip("Anker über dem Dach für die Ausbau-Wimpel.")]
        [SerializeField] private Transform pennantAnchor;
        [SerializeField] private GameObject pennantPrefab;
        [SerializeField] private float scalePerLevel = 0.05f;
        [SerializeField] private float checkInterval = 0.5f;

        private int _shownLevel = -1;
        private float _timer;
        private Vector3 _baseScale = Vector3.one;

        private void Start()
        {
            if (modelRoot != null) _baseScale = modelRoot.localScale;
            Apply();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;
            Apply();
        }

        private void Apply()
        {
            if (runtime == null) return;
            int level = runtime.StationBuildLevel(stationIndex);
            if (level == _shownLevel) return;
            _shownLevel = level;

            if (modelRoot != null)
                modelRoot.localScale = _baseScale * (1f + level * scalePerLevel);

            if (pennantAnchor != null && pennantPrefab != null)
            {
                for (int c = pennantAnchor.childCount - 1; c >= 0; c--)
                    Destroy(pennantAnchor.GetChild(c).gameObject);
                for (int p = 0; p < level; p++)
                {
                    var flag = Instantiate(pennantPrefab, pennantAnchor);
                    flag.transform.localPosition = new Vector3((p - (level - 1) * 0.5f) * 0.5f, 0f, 0f);
                }
            }
        }
    }
}
