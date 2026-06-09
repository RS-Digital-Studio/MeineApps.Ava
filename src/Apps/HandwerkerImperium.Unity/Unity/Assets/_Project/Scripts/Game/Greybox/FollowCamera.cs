using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Kamera (Spec §5): fixer 3rd-Person-Follow (~50° Schraegaufsicht, fixer Zoom). Pinch-Zoom,
    /// Drag-Pan und Impulse-Shake kommen erst ab P1 (dann via Cinemachine, GDD §4).
    /// </summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 13f, -10f);
        [SerializeField] private float smooth = 8f;
        [SerializeField] private float lookHeight = 1f;
        [Tooltip("Blickpunkt-Versatz vor den Avatar (Welt +Z) — Avatar sitzt dadurch im unteren Bilddrittel (Genre-Framing).")]
        [SerializeField] private float lookAheadZ;

        public void SetTarget(Transform t) => target = t;

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            float k = 1f - Mathf.Exp(-smooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, k);
            transform.LookAt(LookPoint());
        }

        /// <summary>Aktueller Blickpunkt (auch für die Editor-Erst-Ausrichtung durch die Szene-Builder).</summary>
        public Vector3 LookPoint() =>
            (target != null ? target.position : Vector3.zero) + Vector3.up * lookHeight + Vector3.forward * lookAheadZ;
    }
}
