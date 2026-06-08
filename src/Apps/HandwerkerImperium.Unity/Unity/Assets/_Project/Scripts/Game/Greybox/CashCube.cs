using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0: rein optischer Cash-Wuerfel. Das Geld ist sim-autoritativ (beim Verkauf gutgeschrieben);
    /// dieser Wuerfel ist nur „Game Juice" und wird vom <see cref="InteractionTriggerSystem"/> eingesammelt.
    /// </summary>
    public sealed class CashCube : MonoBehaviour
    {
        [SerializeField] private float spinDegPerSec = 120f;
        [SerializeField] private float lifeSeconds = 30f;
        [SerializeField] private float flyToSpeed = 14f;

        private bool _collected;
        private float _age;
        private Transform _collector;

        private void Update()
        {
            if (_collected && _collector != null)
            {
                transform.position = Vector3.MoveTowards(transform.position, _collector.position + Vector3.up, flyToSpeed * Time.deltaTime);
                if ((transform.position - (_collector.position + Vector3.up)).sqrMagnitude < 0.09f)
                    Destroy(gameObject);
                return;
            }

            transform.Rotate(Vector3.up, spinDegPerSec * Time.deltaTime, Space.World);
            _age += Time.deltaTime;
            if (_age >= lifeSeconds) Destroy(gameObject);
        }

        /// <summary>Optik-Einsammeln (kein Geld) — der Wuerfel fliegt zum Sammler und despawnt.</summary>
        public void Collect(Transform collector)
        {
            if (_collected) return;
            _collected = true;
            _collector = collector;
        }
    }
}
