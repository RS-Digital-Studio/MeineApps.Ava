using UnityEngine;
using UnityEngine.InputSystem;
using HandwerkerImperium.Domain.MiniGames;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Perfekt-Aktions-Pad (GDD §6.7): Steht der Avatar auf dem Pad, pulsiert ein Timing-Ring
    /// (linear schrumpfend/wachsend). Ein Tap (Space/Gamepad-Süd/Touch rechte Bildschirmhälfte)
    /// wird über <see cref="MiniGameBoostFormulas"/> bewertet — Perfekt/Gut/Ok geben der Station
    /// einen temporären Produktions-Buff (Domain: <c>GreyboxSimulation.ApplyBoost</c>), Miss nicht.
    /// Während ein Buff läuft, ist das Pad sichtbar abgekühlt (Ring aus). Rein optional —
    /// kein Pflicht-Mini-Game (GDD-Entscheidung gegen die 10 Pflicht-Spiele des Originals).
    /// </summary>
    public sealed class MiniGamePadView : MonoBehaviour
    {
        [SerializeField] private RuntimeGameController runtime;
        [SerializeField] private GreyboxGameController controller;
        [SerializeField] private int stationIndex;
        [Tooltip("Pulsierender Timing-Ring (skaliert zwischen min/max).")]
        [SerializeField] private Transform pulseRing;
        [Tooltip("Fixer Ziel-Ring — Tap, wenn der Puls-Ring ihn deckt.")]
        [SerializeField] private Transform targetRing;
        [SerializeField] private TextMesh feedbackText;
        [SerializeField] private float minScale = 0.7f;
        [SerializeField] private float maxScale = 2.3f;
        [SerializeField] private float targetScale = 1.2f;

        private bool _avatarInside;
        private float _pulseTime;
        private float _feedbackUntil;

        private void Update()
        {
            if (runtime == null || runtime.Model == null || controller == null) return;

            bool unlocked = controller.Stations != null && controller.Stations.IsUnlocked(stationIndex);
            bool buffActive = StationBoostRemaining() > 0;
            bool active = _avatarInside && unlocked && !buffActive;

            if (pulseRing != null) pulseRing.gameObject.SetActive(active);
            if (targetRing != null) targetRing.gameObject.SetActive(active && !buffActive);
            if (feedbackText != null && Time.time > _feedbackUntil && !buffActive) feedbackText.text = "";

            if (!active) return;

            // Linearer Puls (PingPong) — konstante Geschwindigkeit macht den Timing-Fehler fair messbar
            float halfPeriod = Mathf.Max(0.2f, (float)runtime.Balancing.MiniGames.PulsePeriodSeconds * 0.5f);
            _pulseTime += Time.deltaTime;
            float current = Mathf.Lerp(maxScale, minScale, Mathf.PingPong(_pulseTime / halfPeriod, 1f));
            if (pulseRing != null) pulseRing.localScale = new Vector3(current, pulseRing.localScale.y, current);

            if (TapPressed())
            {
                float speed = (maxScale - minScale) / halfPeriod; // Skalen-Einheiten je Sekunde
                double errorSeconds = Mathf.Abs(current - targetScale) / Mathf.Max(0.001f, speed);
                var mg = runtime.Balancing.MiniGames;
                var rating = MiniGameBoostFormulas.Rate(errorSeconds, mg.PerfectWindowSeconds, mg.GoodWindowSeconds, mg.OkWindowSeconds);
                ResolveTap(rating, mg.BoostBaseDurationSeconds);
            }
        }

        private void ResolveTap(TapRating rating, double baseDuration)
        {
            decimal multiplier = MiniGameBoostFormulas.BoostMultiplier(rating);
            double duration = MiniGameBoostFormulas.BoostDurationSeconds(rating, baseDuration);
            if (rating != TapRating.Miss)
            {
                runtime.ApplyStationBoost(stationIndex, multiplier, duration);
                controller.Audio?.Play(rating == TapRating.Perfect ? GameSfx.LandmarkComplete : GameSfx.UpgradePaid);
            }
            else
            {
                controller.Audio?.Play(GameSfx.ButtonTap);
            }

            if (feedbackText != null)
            {
                switch (rating)
                {
                    case TapRating.Perfect: feedbackText.text = $"Perfekt! ×{multiplier:0.0}"; feedbackText.color = new Color(1f, 0.85f, 0.25f); break;
                    case TapRating.Good: feedbackText.text = $"Gut! ×{multiplier:0.0}"; feedbackText.color = new Color(0.5f, 0.9f, 0.4f); break;
                    case TapRating.Ok: feedbackText.text = $"Ok ×{multiplier:0.0}"; feedbackText.color = new Color(0.8f, 0.8f, 0.75f); break;
                    default: feedbackText.text = "Daneben"; feedbackText.color = new Color(0.85f, 0.45f, 0.4f); break;
                }
                _feedbackUntil = Time.time + 1.4f;
            }
        }

        private double StationBoostRemaining()
        {
            var stations = runtime.Model.Idle.Stations;
            return stationIndex >= 0 && stationIndex < stations.Count ? stations[stationIndex].BoostRemainingSeconds : 0;
        }

        /// <summary>Tap: Space/Enter, Gamepad-Süd oder Touch-Beginn auf der rechten Bildschirmhälfte (links liegt der Joystick).</summary>
        private static bool TapPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.buttonSouth.wasPressedThisFrame) return true;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame &&
                touch.primaryTouch.position.ReadValue().x > Screen.width * 0.5f) return true;
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<AvatarController>() != null)
            {
                _avatarInside = true;
                _pulseTime = 0f;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<AvatarController>() != null) _avatarInside = false;
        }
    }
}
