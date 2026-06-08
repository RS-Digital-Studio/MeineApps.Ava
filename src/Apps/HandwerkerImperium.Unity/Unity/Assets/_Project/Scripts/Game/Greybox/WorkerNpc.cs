using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0: rein optische Automatisierungs-Rueckmeldung — laeuft Station&lt;-&gt;Tresen hin und her.
    /// Das eigentliche Geld macht der <see cref="WorkerAutomationService"/> (sim-autoritativ); dieser
    /// NPC liefert nur das sichtbare „der Arbeiter uebernimmt"-Gefuehl (der P0-Aha-Moment).
    /// </summary>
    public sealed class WorkerNpc : MonoBehaviour
    {
        [SerializeField] private Transform stationPoint;
        [SerializeField] private Transform counterPoint;
        [SerializeField] private float speed = 3f;
        [SerializeField] private float pauseSeconds = 0.4f;

        private bool _toCounter = true;
        private float _pause;

        public void Setup(Transform station, Transform counter)
        {
            stationPoint = station;
            counterPoint = counter;
        }

        private void Update()
        {
            if (stationPoint == null || counterPoint == null) return;
            if (_pause > 0f) { _pause -= Time.deltaTime; return; }

            Transform goal = _toCounter ? counterPoint : stationPoint;
            Vector3 target = new Vector3(goal.position.x, transform.position.y, goal.position.z);
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            Vector3 dir = target - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);

            if ((transform.position - target).sqrMagnitude < 0.04f)
            {
                _toCounter = !_toCounter;
                _pause = pauseSeconds;
            }
        }
    }
}
