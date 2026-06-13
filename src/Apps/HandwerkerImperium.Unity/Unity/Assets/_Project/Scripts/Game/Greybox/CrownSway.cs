using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Sanfter Wind fuer Vegetation: schwingt das Objekt minimal um zwei Achsen (verschiedene
    /// Frequenzen gegen sichtbare Wiederholung). Die Phase wird aus der Welt-Position gehasht —
    /// jede Pflanze wiegt sich individuell, ohne Zufalls-State (deterministisch je Platzierung).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrownSway : MonoBehaviour
    {
        [SerializeField] private float degrees = 1.6f;
        [SerializeField] private float frequency = 0.35f;

        private Quaternion _baseRotation;
        private float _phase;

        private void Start()
        {
            _baseRotation = transform.localRotation;
            Vector3 p = transform.position;
            _phase = Mathf.Repeat(p.x * 7.31f + p.z * 3.17f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float t = Time.time * frequency * Mathf.PI * 2f + _phase;
            transform.localRotation = _baseRotation *
                Quaternion.Euler(Mathf.Sin(t) * degrees, 0f, Mathf.Cos(t * 0.83f) * degrees * 0.7f);
        }
    }
}
