using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// P0-Spec §4/§5: Hold-to-Pay-Pad mit RAMPENDER Ausgaberate (Genre-Signatur: erst langsam, dann
    /// schneller). Je laenger der Avatar auf dem Pad steht, desto kuerzer das Zahl-Intervall. Konkrete
    /// Pads (Upgrade/Hire/Plot) ueberschreiben <see cref="TryPayStep"/> und <see cref="IsDone"/>.
    /// Braucht einen Trigger-Collider; der Avatar (CharacterController) loest OnTriggerEnter/Exit aus.
    /// </summary>
    public abstract class HoldToPayPad : MonoBehaviour
    {
        [SerializeField] protected GreyboxGameController controller;
        [SerializeField] private float baseInterval = 0.5f;
        [SerializeField] private float minInterval = 0.08f;
        [SerializeField] private float rampDurationSeconds = 2.5f;

        private bool _avatarPresent;
        private float _hold;
        private float _payTimer;

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<AvatarController>() != null) _avatarPresent = true;
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<AvatarController>() != null)
            {
                _avatarPresent = false;
                _hold = 0f;
                _payTimer = 0f;
            }
        }

        private void Update()
        {
            if (!_avatarPresent || controller == null || IsDone()) return;

            _hold += Time.deltaTime;
            float t = rampDurationSeconds > 0f ? Mathf.Clamp01(_hold / rampDurationSeconds) : 1f;
            float interval = Mathf.Lerp(baseInterval, minInterval, t);

            _payTimer += Time.deltaTime;
            if (_payTimer >= interval)
            {
                _payTimer = 0f;
                TryPayStep();
            }
        }

        /// <summary>Ein „Zahl-Schritt" (z. B. ein Upgrade kaufen / Worker anstellen / Plot freischalten).</summary>
        protected abstract void TryPayStep();

        /// <summary>True, wenn nichts mehr zu zahlen ist (z. B. Worker bereits angestellt / Plot frei).</summary>
        protected abstract bool IsDone();
    }
}
