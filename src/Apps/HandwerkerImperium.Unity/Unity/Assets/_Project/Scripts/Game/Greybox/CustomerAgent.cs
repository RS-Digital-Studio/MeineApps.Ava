using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Einzelner Kunde der physischen Tresen-Schlange: läuft vom Spawn (Stadttor) zu seinem
    /// Queue-Platz, wartet dort mit Blick zum Tresen und geht nach der Bedienung wieder ab
    /// (Despawn am Exit). Die Bein-Animation übernimmt der <see cref="ProceduralBoneWalker"/>
    /// auf dem Visual-Kind (misst die Root-Bewegung) — hier lebt nur die Wegfindung.
    /// Verwaltet von <see cref="CustomerQueueView"/>.
    /// </summary>
    public sealed class CustomerAgent : MonoBehaviour
    {
        private enum Phase { ToQueue, Waiting, Leaving }

        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float rotateSpeedDeg = 540f;

        private Phase _phase = Phase.ToQueue;
        private Vector3 _slot;
        private Vector3 _exit;
        private Vector3 _lookAtWhileWaiting;

        /// <summary>True sobald der Kunde seinen Queue-Platz erreicht hat und wartet.</summary>
        public bool IsWaiting => _phase == Phase.Waiting;

        public void Init(Vector3 slot, Vector3 exit, Vector3 lookAtWhileWaiting)
        {
            _slot = slot;
            _exit = exit;
            _lookAtWhileWaiting = lookAtWhileWaiting;
        }

        /// <summary>Queue rückt auf: neuer Platz (weiter vorn) — Kunde läuft nach.</summary>
        public void MoveToSlot(Vector3 slot)
        {
            _slot = slot;
            if (_phase == Phase.Waiting) _phase = Phase.ToQueue;
        }

        /// <summary>Kunde wurde bedient (oder Sichtfenster verkleinert) — abdrehen und gehen.</summary>
        public void Leave()
        {
            if (_phase != Phase.Leaving) _phase = Phase.Leaving;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (_phase)
            {
                case Phase.ToQueue:
                    if (StepTowards(_slot, dt)) _phase = Phase.Waiting;
                    break;
                case Phase.Waiting:
                    FaceTowards(_lookAtWhileWaiting, dt);
                    break;
                case Phase.Leaving:
                    if (StepTowards(_exit, dt)) Destroy(gameObject);
                    break;
            }
        }

        /// <summary>Schritt Richtung Ziel (XZ); liefert true, wenn angekommen.</summary>
        private bool StepTowards(Vector3 target, float dt)
        {
            Vector3 pos = transform.position;
            target.y = pos.y;
            Vector3 to = target - pos;
            if (to.magnitude < 0.08f) return true;
            FaceTowards(target, dt);
            transform.position = Vector3.MoveTowards(pos, target, walkSpeed * dt);
            return false;
        }

        private void FaceTowards(Vector3 point, float dt)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0004f) return;
            var look = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, rotateSpeedDeg * dt);
        }
    }
}
