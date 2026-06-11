using System.Collections.Generic;
using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Prozeduraler Walk-Cycle auf den UniRig-Skeletten (generische Bone-Namen, Generic-Rig aus glTFast):
    /// klassifiziert die Gliedmaßen <b>topologie-basiert</b> — UniRig-Humanoide haben eine Hüfte
    /// (Wurzel-Bone) mit Bein-Ketten, die bis zum Boden laufen, und eine Wirbelsäulen-Kette, an deren
    /// Brust-Knoten Kopf + zwei Arm-Ketten abzweigen. (Höhenband-Heuristiken scheitern an gedrungenen
    /// Toon-Proportionen: herabhängende Arme reichen bis Hüfthöhe.) Beim Laufen schwingen Oberschenkel
    /// gegenphasig mit Knie-Nachbeugung, die Oberarme gegenläufig dazu; im Stand kehren die Glieder
    /// weich in die Bind-Pose zurück (+ Atmen). Ohne erkennbare Gliedmaßen: Fallback auf Lauf-Bob.
    /// Sitzt wie <see cref="ToonBob"/> auf dem Visual-Kind; misst die eigene Root-Bewegung.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralBoneWalker : MonoBehaviour
    {
        [Header("Laufen")]
        [SerializeField] private float legSwingDegrees = 30f;
        [SerializeField] private float kneeBendDegrees = 24f;
        [SerializeField] private float armSwingDegrees = 16f;
        [Tooltip("Schrittzyklen pro Sekunde bei normalem Gehtempo (~1,5-2 ist natürlich).")]
        [SerializeField] private float stepFrequency = 1.7f;
        [SerializeField] private float speedThreshold = 0.15f;
        [SerializeField] private float bobAmplitude = 0.03f;

        [Header("Stand (Atmen)")]
        [SerializeField] private float breatheAmplitude = 0.012f;
        [SerializeField] private float breatheFrequency = 1.5f;

        private Transform _root;        // bewegtes Objekt (Avatar-/NPC-Root)
        private Transform _hips;        // Wurzel-Bone des Skeletts
        private Transform _thighL, _thighR, _kneeL, _kneeR, _armL, _armR;
        private Quaternion _thighLBase, _thighRBase, _kneeLBase, _kneeRBase, _armLBase, _armRBase; // relativ zur Root
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
            var boneSet = new HashSet<Transform>();
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var b in smr.bones)
            {
                if (b == null) continue;
                boneSet.Add(b);
                float y = _root.InverseTransformPoint(b.position).y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
            float h = Mathf.Max(0.01f, maxY - minY);

            // Hüfte = Wurzel-Bone des Skeletts
            _hips = smr.rootBone != null && boneSet.Contains(smr.rootBone) ? smr.rootBone : null;
            if (_hips == null)
                foreach (var b in boneSet) { if (b.parent == null || !boneSet.Contains(b.parent)) { _hips = b; break; } }
            if (_hips == null) return;
            _hipsBasePos = _hips.localPosition;

            // WICHTIG: UniRig-Skelette sind seitlich versetzt (nicht um x=0 zentriert) — alle
            // Links/Rechts-Messungen laufen RELATIV zur Körper-Achse (x der Hüfte bzw. Brust),
            // sonst wird der Hals als "seitlichstes Kind" fehlklassifiziert (Kopf schwingt mit).
            float hipsX = _root.InverseTransformPoint(_hips.position).x;

            // Direkte Hüft-Kinder: Ketten, deren Ende nahe dem Boden liegt = Beine; die übrige Kette = Wirbelsäule.
            Transform spineStart = null;
            var legs = new List<Transform>();
            foreach (Transform child in _hips)
            {
                if (!boneSet.Contains(child)) continue;
                var leaf = FollowToLeaf(child, boneSet);
                float leafNy = (_root.InverseTransformPoint(leaf.position).y - minY) / h;
                if (leafNy < 0.2f) legs.Add(child);
                else spineStart = child;
            }
            if (legs.Count >= 2)
            {
                legs.Sort((a, b2) => (_root.InverseTransformPoint(a.position).x - hipsX)
                    .CompareTo(_root.InverseTransformPoint(b2.position).x - hipsX));
                _thighL = legs[0];                 // kleinste dx = links
                _thighR = legs[legs.Count - 1];    // größte dx = rechts
            }
            else if (legs.Count == 1)
            {
                _thighL = legs[0];
            }
            if (_thighL != null) _kneeL = FirstChildIn(_thighL, boneSet);
            if (_thighR != null) _kneeR = FirstChildIn(_thighR, boneSet);

            // Wirbelsäule hochlaufen bis zum Brust-Knoten (>= 3 Kinder: Hals + 2 Arme).
            // Arme = die Kinder mit größtem Seitenversatz RELATIV zur Brust-Achse, je eines pro Seite.
            var spine = spineStart;
            while (spine != null)
            {
                var children = new List<Transform>();
                foreach (Transform c in spine) if (boneSet.Contains(c)) children.Add(c);
                if (children.Count >= 3)
                {
                    float chestX = _root.InverseTransformPoint(spine.position).x;
                    Transform shoulderL = null, shoulderR = null;
                    float bestL = 0f, bestR = 0f;
                    foreach (var c in children)
                    {
                        float dx = _root.InverseTransformPoint(c.position).x - chestX;
                        if (dx < 0f && -dx > bestL) { bestL = -dx; shoulderL = c; }
                        if (dx > 0f && dx > bestR) { bestR = dx; shoulderR = c; }
                    }
                    // Schwingen am Oberarm (Kind der Schulter) wirkt natürlicher als an der Schulter selbst
                    if (shoulderL != null) _armL = FirstChildIn(shoulderL, boneSet) ?? shoulderL;
                    if (shoulderR != null) _armR = FirstChildIn(shoulderR, boneSet) ?? shoulderR;
                    break;
                }
                spine = children.Count > 0 ? children[0] : null;
            }

            if (_thighL != null && _thighR != null)
            {
                Quaternion inv = Quaternion.Inverse(_root.rotation);
                _thighLBase = inv * _thighL.rotation;
                _thighRBase = inv * _thighR.rotation;
                if (_kneeL != null) _kneeLBase = inv * _kneeL.rotation;
                if (_kneeR != null) _kneeRBase = inv * _kneeR.rotation;
                if (_armL != null) _armLBase = inv * _armL.rotation;
                if (_armR != null) _armRBase = inv * _armR.rotation;
                _ready = true;
            }
        }

        private static Transform FollowToLeaf(Transform start, HashSet<Transform> boneSet)
        {
            var current = start;
            while (true)
            {
                Transform next = null;
                foreach (Transform c in current) { if (boneSet.Contains(c)) { next = c; break; } }
                if (next == null) return current;
                current = next;
            }
        }

        private static Transform FirstChildIn(Transform bone, HashSet<Transform> boneSet)
        {
            foreach (Transform c in bone) if (boneSet.Contains(c)) return c;
            return null;
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
                // Knie beugt nur beim Zurückschwingen des jeweiligen Beins (nie überstrecken)
                if (_kneeL != null) ApplySwing(_kneeL, _kneeLBase, Mathf.Max(0f, -swing) * kneeBendDegrees);
                if (_kneeR != null) ApplySwing(_kneeR, _kneeRBase, Mathf.Max(0f, swing) * kneeBendDegrees);
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
                ReturnToBase(_kneeL, _kneeLBase, k);
                ReturnToBase(_kneeR, _kneeRBase, k);
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
