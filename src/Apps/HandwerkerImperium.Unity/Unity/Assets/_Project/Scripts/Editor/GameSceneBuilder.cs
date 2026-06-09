using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HandwerkerImperium.Game;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Editor
{
    /// <summary>
    /// Editor-Tool: baut die <b>gekoppelte 3D-Game-Szene</b> `Game.unity` — der physische P0-Loop
    /// (Avatar läuft/sammelt/trägt, Stationen produzieren, Tresen verkauft, Hold-to-Pay-Pads) arbeitet
    /// direkt auf dem <c>GameModel.Idle</c> des <see cref="RuntimeGameController"/> (eine Wahrheit,
    /// ein HMAC-Save, GameSimulation als einziger Ticker) und nutzt die echten 3D-Assets der
    /// ComfyUI-Pipeline (avatar_hans/workshop/workshop_smith/customer_npc/worker, glTFast-Import).
    /// Wegen der projektweiten <c>*.meta</c>-Gitignore-Policy wird die Scene <b>lokal generiert</b>
    /// (nicht versioniert) — dieser Builder ist das versionierte Artefakt.
    /// Menü: HandwerkerImperium ▸ Runtime ▸ Build Game Scene (3D).
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string SceneDir = "Assets/_Project/Scenes";
        private const string PrefabDir = "Assets/_Project/Prefabs/Game";
        private const string ModelDir = "Assets/_Project/Art/Models";
        private const string ScenePath = SceneDir + "/Game.unity";
        private const string IdleAsset = SceneDir + "/Runtime_IdleBalancing.asset";
        private const string GameAsset = SceneDir + "/Runtime_GameBalancing.asset";

        [MenuItem("HandwerkerImperium/Runtime/Build Game Scene (3D)")]
        public static void Build()
        {
            EnsureFolder(SceneDir);
            EnsureFolder(PrefabDir);

            var idleConfig = LoadOrCreate<BalancingConfig>(IdleAsset);
            var gameConfig = LoadOrCreate<GameBalancingConfig>(GameAsset);
            SetRef(gameConfig, "idle", idleConfig);

            var warePrefab = MakeWarePrefab();
            var coinPrefab = MakeCoinPrefab();
            var workerPrefab = MakeWorkerPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Licht (warm, Werkstatt-Nachmittag)
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1.0f, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            // Boden
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4.5f, 1f, 4.5f);
            Paint(ground, new Color(0.62f, 0.55f, 0.45f)); // warmer Hof-Boden

            // Runtime (einziger Ticker + HMAC-Save) + Diagnose-HUD
            var runtimeGo = new GameObject("RuntimeGameController");
            var runtime = runtimeGo.AddComponent<RuntimeGameController>();
            SetRef(runtime, "config", gameConfig);
            var hud = runtimeGo.AddComponent<RuntimeHud>();
            SetRef(hud, "controller", runtime);

            // Physik-Loop-Controller, an den Runtime gekoppelt (kein DI-Scope, kein eigener Tick/Save)
            var controllerGo = new GameObject("GreyboxGameController");
            var controller = controllerGo.AddComponent<GreyboxGameController>();
            SetRef(controller, "runtime", runtime);

            // Avatar: CharacterController-Root (Füße auf y=0) + echtes Meister-Hans-Modell
            var avatarGo = new GameObject("Avatar");
            avatarGo.transform.position = new Vector3(0f, 0f, -4f);
            var cc = avatarGo.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f; cc.radius = 0.4f;
            AttachModel(avatarGo.transform, ModelDir + "/avatar_hans.glb", 1.8f);
            var carryAnchor = new GameObject("CarryAnchor").transform;
            carryAnchor.SetParent(avatarGo.transform);
            carryAnchor.localPosition = new Vector3(0f, 2.05f, 0f);
            var avatar = avatarGo.AddComponent<AvatarController>();
            var triggers = avatarGo.AddComponent<InteractionTriggerSystem>();

            // Kamera (fixer Schräg-Follow)
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.70f, 0.82f);
            var follow = camGo.AddComponent<FollowCamera>();

            // Tresen + Kunde (echtes Modell) + Cash-Spawn
            var counterGo = MakeBox("Counter", new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 1f, 1.4f), new Color(0.38f, 0.30f, 0.23f), trigger: true);
            var counter = counterGo.AddComponent<CounterView>();
            var cashSpawn = new GameObject("CashSpawn").transform;
            cashSpawn.SetParent(counterGo.transform);
            cashSpawn.localPosition = new Vector3(0f, 0f, -1.6f);
            SetRef(counter, "controller", controller);
            SetRef(counter, "cashSpawnPoint", cashSpawn);
            SetRef(counter, "cashPrefab", coinPrefab);
            var customerRoot = new GameObject("Customer");
            customerRoot.transform.position = new Vector3(0f, 0f, 2.4f);   // Kunden-Seite des Tresens
            customerRoot.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // schaut zum Tresen
            AttachModel(customerRoot.transform, ModelDir + "/customer_npc.glb", 1.65f);

            // Stationen: 2× Schreinerei (workshop) + 2× Schmiede (workshop_smith), Station 3 gesperrt
            string[] stationModels =
            {
                ModelDir + "/workshop.glb", ModelDir + "/workshop_smith.glb",
                ModelDir + "/workshop.glb", ModelDir + "/workshop_smith.glb"
            };
            var stationPos = new[]
            {
                new Vector3(-8f, 0f, 7f), new Vector3(8f, 0f, 7f),
                new Vector3(-8f, 0f, -7f), new Vector3(8f, 0f, -7f)
            };
            var stationTransforms = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var stGo = new GameObject($"Station_{i}");
                stGo.transform.position = stationPos[i];
                stGo.transform.rotation = Quaternion.LookRotation((Vector3.zero - stationPos[i]).WithY(0f)); // Front zur Mitte
                AttachModel(stGo.transform, stationModels[i], 3.4f);

                // Pickup-Zone vor der Station (Richtung Mitte)
                Vector3 toCenter = (Vector3.zero - stationPos[i]).WithY(0f).normalized;
                var trig = MakeTriggerZone($"Station_{i}_Pickup", stationPos[i] + toCenter * 2.6f + Vector3.up * 0.9f, new Vector3(2.6f, 2f, 2.6f));
                var view = trig.AddComponent<StationView>();
                var stackAnchor = new GameObject("StackAnchor").transform;
                stackAnchor.SetParent(stGo.transform, false);
                stackAnchor.position = stationPos[i] + toCenter * 1.9f + Vector3.up * 0.2f;

                GameObject locked = null;
                if (i == 3)
                {
                    locked = MakeBox("Locked", stationPos[i] + new Vector3(0f, 1.8f, 0f), new Vector3(3.8f, 3.8f, 3.8f), new Color(0.32f, 0.32f, 0.34f), trigger: false);
                    locked.transform.SetParent(stGo.transform, true);
                }

                SetInt(view, "stationIndex", i);
                SetRef(view, "controller", controller);
                SetRef(view, "stackAnchor", stackAnchor);
                SetRef(view, "warePrefab", warePrefab);
                if (locked != null) SetRef(view, "lockedVisual", locked);
                stationTransforms[i] = stGo.transform;
            }

            // Upgrade-Pads (3 Achsen) am linken Rand
            MakeUpgradePad("Pad_Tempo", new Vector3(-14f, 0.1f, 3f), UpgradeTrack.StationSpeed, controller, new Color(0.9f, 0.4f, 0.4f));
            MakeUpgradePad("Pad_Radius", new Vector3(-14f, 0.1f, 0f), UpgradeTrack.CollectRadius, controller, new Color(0.4f, 0.9f, 0.4f));
            MakeUpgradePad("Pad_Kapazitaet", new Vector3(-14f, 0.1f, -3f), UpgradeTrack.CarryCapacity, controller, new Color(0.4f, 0.5f, 0.9f));

            // Worker-Hire-Pads (Station 0 + 1)
            MakeHirePad("Hire_0", stationPos[0] + new Vector3(3.2f, 0.1f, -2.2f), 0, controller, workerPrefab, stationTransforms[0], counterGo.transform);
            MakeHirePad("Hire_1", stationPos[1] + new Vector3(-3.2f, 0.1f, -2.2f), 1, controller, workerPrefab, stationTransforms[1], counterGo.transform);

            // Plot-Bauzaun (Station 3)
            var fenceGo = MakeBox("PlotFence", stationPos[3] + new Vector3(-2.2f, 0.6f, 2.2f), new Vector3(3.4f, 1.2f, 0.4f), new Color(0.8f, 0.7f, 0.2f), trigger: false);
            fenceGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var fenceTrig = MakeTriggerZone("PlotFence_Pay", stationPos[3] + new Vector3(-3.1f, 0.1f, 3.1f), new Vector3(2.6f, 2f, 2.6f));
            var plot = fenceTrig.AddComponent<PlotFenceView>();
            SetInt(plot, "stationIndex", 3);
            SetRef(plot, "controller", controller);
            SetRef(plot, "fenceVisual", fenceGo);

            // Avatar-/Kamera-Verdrahtung
            SetRef(avatar, "controller", controller);
            SetRef(avatar, "carryAnchor", carryAnchor);
            SetRef(avatar, "carryWarePrefab", warePrefab);
            SetRef(triggers, "controller", controller);
            SetRef(follow, "target", avatarGo.transform);
            camGo.transform.position = avatarGo.transform.position + new Vector3(0f, 13f, -10f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GameSceneBuilder] Fertig: {ScenePath} — physischer Loop gekoppelt an den Runtime (echte 3D-Assets).");
        }

        // ── Modell-Helfer ───────────────────────────────────────────────────

        /// <summary>
        /// Hängt das glTFast-importierte GLB als Visual-Kind an: bounds-basiert uniform auf
        /// <paramref name="targetHeight"/> skaliert, Füße/Basis auf der Eltern-Null (y=0), zentriert.
        /// </summary>
        private static void AttachModel(Transform parent, string assetPath, float targetHeight)
        {
            var prefab = AssetDatabase.LoadMainAssetAtPath(assetPath) as GameObject;
            if (prefab == null)
            {
                Debug.LogWarning($"[GameSceneBuilder] Modell fehlt (lokal generierbar, siehe Art/Models/README.md): {assetPath} — Platzhalter-Würfel.");
                var ph = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ph.name = "Placeholder";
                Object.DestroyImmediate(ph.GetComponent<Collider>());
                ph.transform.SetParent(parent, false);
                ph.transform.localScale = new Vector3(1f, targetHeight, 1f);
                ph.transform.localPosition = new Vector3(0f, targetHeight * 0.5f, 0f);
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = prefab.name + "_Visual";
            go.transform.SetParent(parent, false);

            var bounds = CalcBounds(go);
            float scale = bounds.size.y > 0.0001f ? targetHeight / bounds.size.y : 1f;
            go.transform.localScale = Vector3.one * scale;
            // Nach der Skalierung: Basis auf 0, horizontal zentriert (Bounds skalieren mit)
            go.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
        }

        private static Bounds CalcBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            // In lokale Koordinaten des Roots übersetzen (Root steht bei Instanziierung an parent-Null)
            b.center -= go.transform.position;
            return b;
        }

        // ── Prefabs ─────────────────────────────────────────────────────────

        private static GameObject MakeWarePrefab()
        {
            // Ware bewusst als leichter Primitive-Würfel (Stapel können groß werden — 12k-Tris-Modelle wären zu schwer)
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Game_Ware";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            PaintAsset(go, new Color(0.78f, 0.56f, 0.30f), "Mat_Ware");
            return SavePrefab(go, "Game_Ware");
        }

        private static GameObject MakeCoinPrefab()
        {
            // Goldmünze als flacher Zylinder (Image-to-3D für flache Props ungeeignet — bewusste Primitive-Entscheidung)
            var go = new GameObject("Game_Coin");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(0.5f, 0.055f, 0.5f);
            visual.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            PaintAsset(visual, new Color(1.0f, 0.82f, 0.18f), "Mat_Coin");
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.45f;
            col.isTrigger = true; // blockiert den CharacterController nicht
            go.AddComponent<CashCube>();
            return SavePrefab(go, "Game_Coin");
        }

        private static GameObject MakeWorkerPrefab()
        {
            var go = new GameObject("Game_Worker");
            AttachModel(go.transform, ModelDir + "/worker.glb", 1.6f);
            go.AddComponent<WorkerNpc>();
            return SavePrefab(go, "Game_Worker");
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ── Szene-Bausteine (wie GreyboxSceneBuilder) ───────────────────────

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

        private static void Paint(GameObject go, Color color)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            mr.sharedMaterial = MakePipelineMaterial(color);
        }

        /// <summary>Wie <see cref="Paint"/>, aber das Material wird als Asset persistiert —
        /// Pflicht für Prefabs (in-memory-Materialien überleben SaveAsPrefabAsset nicht → magenta).</summary>
        private static void PaintAsset(GameObject go, Color color, string assetName)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            string path = $"{PrefabDir}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                mr.sharedMaterial = existing;
                return;
            }
            var mat = MakePipelineMaterial(color);
            AssetDatabase.CreateAsset(mat, path);
            mr.sharedMaterial = mat;
        }

        private static Material MakePipelineMaterial(Color color)
        {
            // Default-Material der AKTIVEN Pipeline klonen (Shader.Find liefert im Editor-Batch
            // unter URP sonst einen inkompatiblen Shader -> magenta).
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var mat = pipeline != null && pipeline.defaultMaterial != null
                ? new Material(pipeline.defaultMaterial)
                : new Material(Shader.Find("Standard"));
            mat.color = color; // mapped auf [MainColor] (_BaseColor bei URP-Lit)
            return mat;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
                AssetDatabase.SaveAssets();
            }
            return a;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(target);
        }

        private static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetEnum(Object target, string field, int enumIndex)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.enumValueIndex = enumIndex; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }

    internal static class Vector3Ext
    {
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
    }
}
