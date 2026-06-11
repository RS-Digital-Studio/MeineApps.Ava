using System.Collections.Generic;
using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Prozeduraler Walk-Cycle auf den UniRig-Skeletten (generische Bone-Namen, Generic-Rig aus glTFast):
    /// klassifiziert die Gliedmaßen positionsbasiert (Oberschenkel = hüfthohe seitliche Bones mit
    /// tiefer laufender Kette, Oberarme = die äußersten Bones der oberen Hälfte) und schwingt sie beim
    /// Laufen gegenphasig um die Quer-Achse der Figur — echte Gelenk-Animation ohne Animations-Clips.
    /// Im Stand kehren die Glieder weich in die Bind-Pose zurück (+ sanftes Atmen über den Wurzel-Bone).
    /// Findet die Klassifikation keine 4 Gliedmaßen, fällt die Figur auf reinen Lauf-Bob zurück.
    /// Sitzt wie <see cref="ToonBob"/> auf dem Visual-Kind; misst die eigene Root-Bewegung.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralBoneWalker : MonoBehaviour
    {
        [Header("Laufen")]
        [SerializeField] private float legSwingDegrees = 32f;
        [SerializeField] private float armSwingDegrees = 22f;
        [SerializeField] private float stepFrequency = 5.5f;
        [SerializeField] private float speedThreshold = 0.15f;
        [SerializeField] private float bobAmplitude = 0.035f;

        [Header("Stand (Atmen)")]
        [SerializeField] private float breatheAmplitude = 0.012f;
        [SerializeField] private float breatheFrequency = 1.5f;

        private Transform _root;        // bewegtes Objekt (Avatar-/NPC-Root)
        private Transform _hips;        // Wurzel-Bone des Skeletts
        private Transform _thighL, _thighR, _armL, _armR;
        private Quaternion _thighLBase, _thighRBase, _armLBase, _armRBase; // relativ zur Root
        private Vector3 _hipsBasePos;
        private Vector3 _lastRootPos;
        private float _phase;
        private bool _ready;

        private void Start()
        {
            _root = transform.parent != null ? transform.parent : transform;
            _lastRootPos = _root.position;
            ClassifyBones();
        }

        private void ClassifyBones()
        {
            var smr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.bones == null || smr.bones.Length < 6) return;
            var bones = smr.bones;

            // Bounds des Skeletts im Root-Raum
            float minY = float.MaxValue, maxY = float.MinValue, maxAbsX = 0f;
            var local = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                local[i] = _root.InverseTransformPoint(bones[i].position);
                minY = Mathf.Min(minY, local[i].y);
                maxY = Mathf.Max(maxY, local[i].y);
                maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(local[i].x));
            }
            float h = Mathf.Max(0.01f, maxY - minY);
            float sideEps = Mathf.Max(0.02f, maxAbsX * 0.25f);

            _hips = smr.rootBone != null ? smr.rootBone : bones[0];
            _hipsBasePos = _hips.localPosition;

            // Oberschenkel: seitliche Bones im Hüftband (35-60 % Höhe), deren Kette nach unten führt —
            // je Seite der höchste Kandidat.
            float bestL = float.MinValue, bestR = float.MinValue;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null) continue;
                float ny = (local[i].y - minY) / h;
                if (ny < 0.30f || ny > 0.65f) continue;
                if (Mathf.Abs(local[i].x) < sideEps) continue;
                if (!HasDescendantBelow(b, local[i].y)) continue;
                if (local[i].x < 0f && local[i].y > bestL) { bestL = local[i].y; _thighL = b; }
                if (local[i].x > 0f && local[i].y > bestR) { bestR = local[i].y; _thighR = b; }
            }

            // Oberarme: die am weitesten außen liegenden Bones der oberen Hälfte (Schulter-Band)
            float widestL = 0f, widestR = 0f;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null || b == _thighL || b == _thighR) continue;
                float ny = (local[i].y - minY) / h;
                if (ny < 0.55f) continue;
                if (local[i].x < -sideEps && -local[i].x > widestL) { widestL = -local[i].x; _armL = b; }
                if (local[i].x > sideEps && local[i].x > widestR) { widestR = local[i].x; _armR = b; }
            }

            if (_thighL != null && _thighR != null)
            {
                Quaternion inv = Quaternion.Inverse(_root.rotation);
                _thighLBase = inv * _thighL.rotation;
                _thighRBase = inv * _thighR.rotation;
                if (_armL != null) _armLBase = inv * _armL.rotation;
                if (_armR != null) _armRBase = inv * _armR.rotation;
                _ready = true;
            }
        }

        private static bool HasDescendantBelow(Transform bone, float worldRefY)
        {
            foreach (Transform child in bone)
            {
                if (child.position.y < bone.position.y - 0.01f) return true;
                if (HasDescendantBelow(child, worldRefY)) return true;
            }
            return false;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _root == null) return;

            Vector3 delta = _root.position - _lastRootPos;
            _lastRootPos = _root.position;
            delta.y = 0f;
            float speed = delta.magnitude / dt;
            bool walking = speed > speedThreshold;

            if (!_ready)
            {
                // Fallback: reiner Bob (wie ToonBob, ohne Gelenke)
                if (_hips == null) return;
                FallbackBob(walking, dt);
                return;
            }

            if (walking)
            {
                _phase += dt * stepFrequency * Mathf.Clamp(speed / 3f, 0.7f, 1.5f) * Mathf.PI * 2f;
                float swing = Mathf.Sin(_phase);
                ApplySwing(_thighL, _thighLBase, swing * legSwingDegrees);
                ApplySwing(_thighR, _thighRBase, -swing * legSwingDegrees);
                if (_armL != null) ApplySwing(_armL, _armLBase, -swing * armSwingDegrees);
                if (_armR != null) ApplySwing(_armR, _armRBase, swing * armSwingDegrees);
                _hips.localPosition = _hipsBasePos + new Vector3(0f, Mathf.Abs(Mathf.Cos(_phase)) * bobAmplitude, 0f);
            }
            else
            {
                // weich zur Bind-Pose zurück + Atmen
                float k = 10f * dt;
                ReturnToBase(_thighL, _thighLBase, k);
                ReturnToBase(_thighR, _thighRBase, k);
                ReturnToBase(_armL, _armLBase, k);
                ReturnToBase(_armR, _armRBase, k);
                float breathe = Mathf.Sin(Time.time * breatheFrequency * Mathf.PI * 2f) * breatheAmplitude;
                _hips.localPosition = Vector3.Lerp(_hips.localPosition, _hipsBasePos + new Vector3(0f, breathe, 0f), k);
            }
        }

        private void ApplySwing(Transform bone, Quaternion baseRelative, float degrees)
        {
            if (bone == null) return;
            bone.rotation = _root.rotation * Quaternion.AngleAxis(degrees, Vector3.right) * baseRelative;
        }

        private void ReturnToBase(Transform bone, Quaternion baseRelative, float k)
        {
            if (bone == null) return;
            bone.rotation = Quaternion.Slerp(bone.rotation, _root.rotation * baseRelative, k);
        }

        private void FallbackBob(bool walking, float dt)
        {
            if (walking)
            {
                _phase += dt * stepFrequency * Mathf.PI * 2f;
                _hips.localPosition = _hipsBasePos + new Vector3(0f, Mathf.Abs(Mathf.Sin(_phase)) * bobAmplitude * 2f, 0f);
            }
            else
            {
                _hips.localPosition = Vector3.Lerp(_hips.localPosition, _hipsBasePos, 10f * dt);
            }
        }
    }
}
