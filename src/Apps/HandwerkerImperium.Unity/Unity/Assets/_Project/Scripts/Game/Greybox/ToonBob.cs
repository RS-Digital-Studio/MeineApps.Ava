using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Prozedurale Toon-Animation für (noch) ungeriggte Figuren-Meshes (Game-Juice-Brücke, bis die
    /// Auto-Rig-Stufe der Asset-Pipeline echte Walk-Cycles liefert): beim Laufen hüpft das Visual
    /// genre-typisch (Bob + leichtes Roll-Wackeln), im Stand atmet es sanft (Y-Scale-Puls).
    /// Misst die eigene Root-Bewegung — funktioniert dadurch für Avatar, Worker-NPC und Kunden
    /// gleichermaßen, ganz ohne Kopplung an Controller-Logik. Sitzt auf dem Visual-Kind.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToonBob : MonoBehaviour
    {
        [Header("Laufen")]
        [SerializeField] private float bobAmplitude = 0.09f;
        [SerializeField] private float bobFrequency = 7f;
        [SerializeField] private float rollDegrees = 3.5f;
        [SerializeField] private float speedThreshold = 0.15f;

        [Header("Stand (Atmen)")]
        [SerializeField] private float breatheAmplitude = 0.015f;
        [SerializeField] private float breatheFrequency = 1.6f;

        private Transform _root;
        private Vector3 _lastRootPos;
        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;
        private Vector3 _baseLocalScale;
        private float _phase;

        private void Awake()
        {
            _root = transform.parent != null ? transform.parent : transform;
            _lastRootPos = _root.position;
            _baseLocalPos = transform.localPosition;
            _baseLocalRot = transform.localRotation;
            _baseLocalScale = transform.localScale;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 delta = _root.position - _lastRootPos;
            _lastRootPos = _root.position;
            delta.y = 0f;
            float speed = delta.magnitude / dt;

            if (speed > speedThreshold)
            {
                // Lauf-Bob: Hüpfen + leichtes Seiten-Wackeln, Tempo skaliert mit der Laufgeschwindigkeit.
                _phase += dt * bobFrequency * Mathf.Clamp(speed / 3f, 0.6f, 1.6f);
                float bob = Mathf.Abs(Mathf.Sin(_phase)) * bobAmplitude;
                float roll = Mathf.Sin(_phase) * rollDegrees;
                transform.localPosition = _baseLocalPos + new Vector3(0f, bob, 0f);
                transform.localRotation = _baseLocalRot * Quaternion.Euler(0f, 0f, roll);
                transform.localScale = _baseLocalScale;
            }
            else
            {
                // Idle-Atmen: sanfter Y-Scale-Puls, Position/Rotation kehren weich zur Basis zurück.
                float breathe = 1f + Mathf.Sin(Time.time * breatheFrequency * Mathf.PI * 2f) * breatheAmplitude;
                transform.localPosition = Vector3.Lerp(transform.localPosition, _baseLocalPos, 12f * dt);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, _baseLocalRot, 12f * dt);
                transform.localScale = new Vector3(_baseLocalScale.x, _baseLocalScale.y * breathe, _baseLocalScale.z);
            }
        }
    }
}
