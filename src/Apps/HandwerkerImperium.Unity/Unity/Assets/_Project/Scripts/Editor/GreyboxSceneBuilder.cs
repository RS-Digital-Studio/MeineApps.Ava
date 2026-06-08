using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HandwerkerImperium.Game;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Editor
{
    /// <summary>
    /// Editor-Tool (P0-Spec §5): baut die Greybox-Szene `P0_Greybox.unity` komplett aus Primitives,
    /// legt das `BalancingConfig`-Asset an, erzeugt Ware-/Cash-/Worker-Prefabs und verdrahtet alle
    /// View-/Interaktions-MonoBehaviours mit dem Controller. Reine Primitives — keine finalen Assets.
    /// Menue: HandwerkerImperium ▸ P0 ▸ Build Greybox Scene.
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        private const string ProjectDir = "Assets/_Project";
        private const string SceneDir = ProjectDir + "/Scenes";
        private const string PrefabDir = ProjectDir + "/Prefabs/Greybox";
        private const string ScenePath = SceneDir + "/P0_Greybox.unity";
        private const string ConfigPath = SceneDir + "/P0_BalancingConfig.asset";

        [MenuItem("HandwerkerImperium/P0/Build Greybox Scene")]
        public static void Build()
        {
            EnsureFolder(SceneDir);
            EnsureFolder(PrefabDir);

            var config = LoadOrCreateConfig();
            var warePrefab = MakeCubePrefab("P0_Ware", new Color(0.80f, 0.58f, 0.30f), new Vector3(0.4f, 0.4f, 0.4f), addCash: false, addWorker: false);
            var cashPrefab = MakeCubePrefab("P0_Cash", new Color(1.0f, 0.85f, 0.15f), new Vector3(0.35f, 0.35f, 0.35f), addCash: true, addWorker: false);
            var workerPrefab = MakeWorkerPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Licht
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Boden
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Paint(ground, new Color(0.55f, 0.57f, 0.60f));

            // Avatar (Capsule + CharacterController + Carry-Anchor)
            var avatarGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            avatarGo.name = "Avatar";
            Object.DestroyImmediate(avatarGo.GetComponent<Collider>());
            avatarGo.transform.position = new Vector3(0f, 1f, -2f);
            Paint(avatarGo, new Color(0.20f, 0.55f, 0.95f));
            var cc = avatarGo.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0f, 0f);
            cc.height = 2f; cc.radius = 0.5f;
            var carryAnchor = new GameObject("CarryAnchor").transform;
            carryAnchor.SetParent(avatarGo.transform);
            carryAnchor.localPosition = new Vector3(0f, 1.2f, 0f);
            var avatar = avatarGo.AddComponent<AvatarController>();
            avatarGo.AddComponent<InteractionTriggerSystem>();

            // Kamera (fixer Follow)
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.62f, 0.78f);
            var follow = camGo.AddComponent<FollowCamera>();

            // Controller + DI-Scope
            var controllerGo = new GameObject("GreyboxGameController");
            var controller = controllerGo.AddComponent<GreyboxGameController>();
            var scopeGo = new GameObject("GameLifetimeScope");
            var scope = scopeGo.AddComponent<GameLifetimeScope>();
            SetRef(scope, "balancingConfig", config);

            // Tresen
            var counterGo = MakeBox("Counter", new Vector3(0f, 0.5f, 0f), new Vector3(3f, 1f, 1.5f), new Color(0.35f, 0.30f, 0.25f), trigger: true);
            var counter = counterGo.AddComponent<CounterView>();
            var cashSpawn = new GameObject("CashSpawn").transform;
            cashSpawn.SetParent(counterGo.transform);
            cashSpawn.localPosition = new Vector3(0f, 0f, -1.5f);
            SetRef(counter, "controller", controller);
            SetRef(counter, "cashSpawnPoint", cashSpawn);
            SetRef(counter, "cashPrefab", cashPrefab);

            // Stationen (3 offen + 1 gesperrt)
            var stationColors = new[]
            {
                new Color(0.85f, 0.45f, 0.30f), new Color(0.30f, 0.65f, 0.55f),
                new Color(0.85f, 0.80f, 0.30f), new Color(0.60f, 0.40f, 0.75f)
            };
            var stationPos = new[]
            {
                new Vector3(-6f, 0.75f, 6f), new Vector3(0f, 0.75f, 8f),
                new Vector3(6f, 0.75f, 6f), new Vector3(0f, 0.75f, -8f)
            };
            var stationTransforms = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var stGo = MakeBox($"Station_{i}", stationPos[i], new Vector3(1.5f, 1.5f, 1.5f), stationColors[i], trigger: false);
                // Separater Trigger-Bereich vor der Station
                var trig = MakeTriggerZone($"Station_{i}_Pickup", stationPos[i] + new Vector3(0f, -0.25f, -1.6f), new Vector3(2.5f, 2f, 2.5f));
                var view = trig.AddComponent<StationView>();
                var stackAnchor = new GameObject("StackAnchor").transform;
                stackAnchor.SetParent(stGo.transform);
                stackAnchor.localPosition = new Vector3(0f, 1f, -0.9f);
                stackAnchor.localScale = Vector3.one;
                GameObject locked = null;
                if (i == 3)
                {
                    locked = MakeBox("Locked", stationPos[i] + new Vector3(0f, 0.5f, 0f), new Vector3(1.7f, 2.6f, 1.7f), new Color(0.3f, 0.3f, 0.3f), trigger: false);
                    locked.transform.SetParent(stGo.transform, true);
                }
                SetInt(view, "stationIndex", i);
                SetRef(view, "controller", controller);
                SetRef(view, "stackAnchor", stackAnchor);
                SetRef(view, "warePrefab", warePrefab);
                if (locked != null) SetRef(view, "lockedVisual", locked);
                stationTransforms[i] = stGo.transform;
            }

            // Upgrade-Pads (3 Achsen)
            MakeUpgradePad("Pad_Tempo", new Vector3(-9f, 0.1f, 0f), UpgradeTrack.StationSpeed, controller, new Color(0.9f, 0.4f, 0.4f));
            MakeUpgradePad("Pad_Radius", new Vector3(-9f, 0.1f, 3f), UpgradeTrack.CollectRadius, controller, new Color(0.4f, 0.9f, 0.4f));
            MakeUpgradePad("Pad_Kapazitaet", new Vector3(-9f, 0.1f, -3f), UpgradeTrack.CarryCapacity, controller, new Color(0.4f, 0.5f, 0.9f));

            // Worker-Hire-Pads (Station 0 + 1)
            MakeHirePad("Hire_0", new Vector3(-6f, 0.1f, 3.5f), 0, controller, workerPrefab, stationTransforms[0], counterGo.transform);
            MakeHirePad("Hire_1", new Vector3(0f, 0.1f, 5f), 1, controller, workerPrefab, stationTransforms[1], counterGo.transform);

            // Plot-Bauzaun (Station 3)
            var fenceGo = MakeBox("PlotFence", new Vector3(0f, 0.6f, -4.5f), new Vector3(4f, 1.2f, 0.4f), new Color(0.8f, 0.7f, 0.2f), trigger: false);
            var fenceTrig = MakeTriggerZone("PlotFence_Pay", new Vector3(0f, 0.1f, -3.4f), new Vector3(3f, 2f, 2f));
            var plot = fenceTrig.AddComponent<PlotFenceView>();
            SetInt(plot, "stationIndex", 3);
            SetRef(plot, "controller", controller);
            SetRef(plot, "fenceVisual", fenceGo);

            // Avatar-/Kamera-Verdrahtung
            SetRef(avatar, "controller", controller);
            SetRef(avatar, "carryAnchor", carryAnchor);
            SetRef(avatar, "carryWarePrefab", warePrefab);
            SetRef(avatarGo.GetComponent<InteractionTriggerSystem>(), "controller", controller);
            SetRef(follow, "target", avatarGo.transform);
            camGo.transform.position = avatarGo.transform.position + new Vector3(0f, 13f, -10f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GreyboxSceneBuilder] Fertig: {ScenePath} (+ {ConfigPath}). Play druecken fuer den Fun-Check.");
            EditorUtility.DisplayDialog("Greybox", "P0_Greybox.unity gebaut.\nPlay druecken: WASD laufen, an Stationen sammeln, am Tresen abgeben, auf Pads halten.", "OK");
        }

        // ── Helfer ─────────────────────────────────────────────────────────

        private static BalancingConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BalancingConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BalancingConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }
            return config;
        }

        private static void MakeUpgradePad(string name, Vector3 pos, UpgradeTrack track, GreyboxGameController controller, Color color)
        {
            var pad = MakeTriggerZone(name, pos, new Vector3(2.5f, 1.5f, 2.5f));
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            marker.transform.SetParent(pad.transform, false);
            marker.transform.localScale = new Vector3(2f, 0.1f, 2f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            Paint(marker, color);
            var view = pad.AddComponent<UpgradePadView>();
            SetEnum(view, "track", (int)track);
            SetRef(view, "controller", controller);
        }

        private static void MakeHirePad(string name, Vector3 pos, int stationIndex, GreyboxGameController controller, GameObject workerPrefab, Transform stationPoint, Transform counterPoint)
        {
            var pad = MakeTriggerZone(name, pos, new Vector3(2.5f, 1.5f, 2.5f));
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            marker.transform.SetParent(pad.transform, false);
            marker.transform.localScale = new Vector3(2f, 0.1f, 2f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            Paint(marker, new Color(0.2f, 0.8f, 0.9f));
            var view = pad.AddComponent<WorkerHirePadView>();
            SetInt(view, "stationIndex", stationIndex);
            SetRef(view, "controller", controller);
            SetRef(view, "workerNpcPrefab", workerPrefab);
            SetRef(view, "stationPoint", stationPoint);
            SetRef(view, "counterPoint", counterPoint);
            SetRef(view, "spawnPoint", pad.transform);
        }

        private static GameObject MakeBox(string name, Vector3 pos, Vector3 scale, Color color, bool trigger)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            Paint(go, color);
            if (trigger)
            {
                go.GetComponent<Collider>().isTrigger = true;
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            return go;
        }

        private static GameObject MakeTriggerZone(string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            return go;
        }

        private static GameObject MakeCubePrefab(string name, Color color, Vector3 scale, bool addCash, bool addWorker)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = scale;
            Paint(go, color);
            if (addCash)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
                var col = go.AddComponent<SphereCollider>();
                col.radius = 0.6f;
                go.AddComponent<CashCube>();
            }
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject MakeWorkerPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "P0_Worker";
            go.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Paint(go, new Color(0.95f, 0.55f, 0.25f));
            go.AddComponent<WorkerNpc>();
            string path = $"{PrefabDir}/P0_Worker.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void Paint(GameObject go, Color color)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            mr.sharedMaterial = mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetRef(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetInt(Component c, string field, int value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null) { p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetEnum(Component c, string field, int enumIndex)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null) { p.enumValueIndex = enumIndex; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }
}
