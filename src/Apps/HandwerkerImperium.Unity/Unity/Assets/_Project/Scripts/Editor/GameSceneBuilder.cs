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

            // Boden + warmes Ambiente
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5.5f, 1f, 5.5f);
            Paint(ground, new Color(0.62f, 0.55f, 0.45f)); // warmer Hof-Boden
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.50f, 0.47f); // weiche warme Schatten

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
            var avatarVisual = AttachModel(avatarGo.transform, ModelDir + "/avatar_hans.glb", 1.8f);
            avatarVisual.AddComponent<ToonBob>(); // Lauf-Hüpfen + Idle-Atmen (bis zur Auto-Rig-Stufe)
            var carryAnchor = new GameObject("CarryAnchor").transform;
            carryAnchor.SetParent(avatarGo.transform);
            carryAnchor.localPosition = new Vector3(0f, 2.05f, 0f);
            var avatar = avatarGo.AddComponent<AvatarController>();
            var triggers = avatarGo.AddComponent<InteractionTriggerSystem>();

            // Kamera (fixer Schräg-Follow, Genre-Framing: ~52°, nah dran, Avatar im unteren Drittel)
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.70f, 0.82f);
            cam.fieldOfView = 45f; // enger als der 60er-Default — weniger Verzerrung, Figuren größer
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
            customerRoot.transform.position = new Vector3(0f, 0f, 3.1f);   // Kunden-Seite, mit Abstand (überlappt sonst aus Kamerasicht den Tresen)
            customerRoot.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // schaut zum Tresen
            var customerVisual = AttachModel(customerRoot.transform, ModelDir + "/customer_npc.glb", 1.65f);
            customerVisual.AddComponent<ToonBob>(); // Idle-Atmen
            MakeSign("Verkauf", counterGo.transform.position + new Vector3(0f, 1.9f, 0f), 0.34f,
                new Color(0.98f, 0.95f, 0.88f), new Color(0.30f, 0.22f, 0.15f));

            // Stationen: alle 10 Gewerke (GDD §6.1) im Hof-Layout, je eigenes Pipeline-Modell.
            // Nord-Reihe 0-4, Süd-Reihe 5-9, alle zur Hof-Mitte orientiert. Nur die Schreinerei startet offen;
            // gesperrte Plots zeigen Bodenplatte + Bauzaun (Hold-to-Pay), das Gebäude erscheint erst nach Unlock.
            string[] stationModels =
            {
                ModelDir + "/workshop.glb",                    // 0 schreiner
                ModelDir + "/workshop_plumber.glb",            // 1 klempner
                ModelDir + "/workshop_electrician.glb",        // 2 elektriker
                ModelDir + "/workshop_painter.glb",            // 3 maler
                ModelDir + "/workshop_roofer.glb",             // 4 dachdecker
                ModelDir + "/workshop_contractor.glb",         // 5 bauunternehmer
                ModelDir + "/workshop_architect.glb",          // 6 architekt
                ModelDir + "/workshop_general_contractor.glb", // 7 generalunternehmer
                ModelDir + "/workshop_smith.glb",              // 8 meisterschmied
                ModelDir + "/workshop_innovation_lab.glb"      // 9 innovationslabor
            };
            string[] stationNames =
            {
                "Schreinerei", "Klempnerei", "Elektriker", "Malerei", "Dachdeckerei",
                "Bauunternehmen", "Architekturbüro", "Generalunternehmer", "Meisterschmiede", "Innovationslabor"
            };
            var idleBalancing = idleConfig.ToDomain(); // für die Plot-Preise an den Bauzäunen
            int stationCount = stationModels.Length;
            var stationPos = new Vector3[stationCount];
            for (int i = 0; i < 5; i++) stationPos[i] = new Vector3(-16f + i * 8f, 0f, 9f);    // Nord-Reihe
            for (int i = 5; i < 10; i++) stationPos[i] = new Vector3(-16f + (i - 5) * 8f, 0f, -9f); // Süd-Reihe

            var fencePalette = new Color(0.85f, 0.68f, 0.20f); // Bauzaun-Gelb
            var stationTransforms = new Transform[stationCount];
            for (int i = 0; i < stationCount; i++)
            {
                Vector3 pos = stationPos[i];
                Vector3 toCenter = (Vector3.zero - pos).WithY(0f).normalized;

                var stGo = new GameObject($"Station_{i}");
                stGo.transform.position = pos;
                stGo.transform.rotation = Quaternion.LookRotation(toCenter);

                // Plot-Bodenplatte (immer sichtbar — markiert den Bauplatz)
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "PlotPlate";
                UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());
                plate.transform.SetParent(stGo.transform, false);
                plate.transform.position = pos + new Vector3(0f, 0.025f, 0f);
                plate.transform.rotation = stGo.transform.rotation;
                plate.transform.localScale = new Vector3(5.6f, 0.05f, 5.6f);
                Paint(plate, new Color(0.52f, 0.45f, 0.36f));

                // Gebäude-Modell (unlockedVisual — gesperrt ausgeblendet)
                var modelRoot = new GameObject("Model");
                modelRoot.transform.SetParent(stGo.transform, false);
                AttachModel(modelRoot.transform, stationModels[i], 3.4f);

                // Pickup-Zone + Waren-Stapel vor der Station
                var trig = MakeTriggerZone($"Station_{i}_Pickup", pos + toCenter * 2.9f + Vector3.up * 0.9f, new Vector3(2.6f, 2f, 2.6f));
                var view = trig.AddComponent<StationView>();
                var stackAnchor = new GameObject("StackAnchor").transform;
                stackAnchor.SetParent(stGo.transform, false);
                stackAnchor.position = pos + toCenter * 2.1f + Vector3.up * 0.25f;

                SetInt(view, "stationIndex", i);
                SetRef(view, "controller", controller);
                SetRef(view, "stackAnchor", stackAnchor);
                SetRef(view, "warePrefab", warePrefab);
                SetRef(view, "unlockedVisual", modelRoot);
                stationTransforms[i] = stGo.transform;

                // Gewerk-Schild knapp über dem Zaun an der Plot-Front (über der Station läge es außerhalb
                // des Kamera-Framings; höher im Sichtkegel würde es den Avatar verdecken)
                MakeSign(stationNames[i], pos + toCenter * 3.2f + Vector3.up * 2.2f, 0.36f,
                    new Color(0.99f, 0.96f, 0.90f), new Color(0.33f, 0.24f, 0.16f));

                // Gesperrte Plots: Bauzaun (lockedVisual + fenceVisual) + Preis-Schild + Hold-to-Pay-Zone
                if (i > 0)
                {
                    var fence = MakeBox($"Fence_{i}", pos + toCenter * 2.6f + Vector3.up * 0.6f, new Vector3(4.6f, 1.2f, 0.3f), fencePalette, trigger: false);
                    fence.transform.rotation = Quaternion.LookRotation(toCenter);
                    fence.transform.SetParent(stGo.transform, true);
                    SetRef(view, "lockedVisual", fence);

                    // Preis am Zaun (Kind des Zauns -> verschwindet mit dem Unlock)
                    decimal cost = HandwerkerImperium.Domain.Idle.GreyboxSimulation.UnlockCostFor(idleBalancing, i);
                    string price = cost.ToString("N0", new System.Globalization.CultureInfo("de-DE"));
                    var priceSign = MakeSign(price, pos + toCenter * 2.6f + Vector3.up * 1.4f, 0.36f,
                        new Color(1.0f, 0.85f, 0.25f), new Color(0.25f, 0.20f, 0.12f));
                    priceSign.transform.SetParent(fence.transform, true);

                    var payZone = MakeTriggerZone($"Fence_{i}_Pay", pos + toCenter * 4.2f + Vector3.up * 0.9f, new Vector3(2.8f, 2f, 2.4f));
                    var plotView = payZone.AddComponent<PlotFenceView>();
                    SetInt(plotView, "stationIndex", i);
                    SetRef(plotView, "controller", controller);
                    SetRef(plotView, "fenceVisual", fence);
                }

                // Worker-Hire-Pad seitlich vor jeder Station (zahlt erst nach Unlock — Domain prueft Unlocked)
                Vector3 side = Vector3.Cross(Vector3.up, toCenter).normalized;
                MakeHirePad($"Hire_{i}", pos + toCenter * 2.2f + side * 3.0f + Vector3.up * 0.1f, i, controller, workerPrefab, stationTransforms[i], counterGo.transform);
            }

            // Upgrade-Pads (3 Achsen) zentral links neben dem Tresen, beschriftet
            MakeUpgradePad("Pad_Tempo", new Vector3(-6f, 0.1f, 2.5f), UpgradeTrack.StationSpeed, controller, new Color(0.9f, 0.4f, 0.4f));
            MakeUpgradePad("Pad_Radius", new Vector3(-6f, 0.1f, 0f), UpgradeTrack.CollectRadius, controller, new Color(0.4f, 0.9f, 0.4f));
            MakeUpgradePad("Pad_Kapazitaet", new Vector3(-6f, 0.1f, -2.5f), UpgradeTrack.CarryCapacity, controller, new Color(0.4f, 0.5f, 0.9f));
            MakeSign("Tempo", new Vector3(-6f, 1.1f, 2.5f), 0.26f, new Color(0.95f, 0.40f, 0.40f));
            MakeSign("Radius", new Vector3(-6f, 1.1f, 0f), 0.26f, new Color(0.35f, 0.85f, 0.35f));
            MakeSign("Tragkraft", new Vector3(-6f, 1.1f, -2.5f), 0.26f, new Color(0.45f, 0.55f, 0.95f));

            // Avatar-/Kamera-Verdrahtung. WICHTIG: Start-Position UND Start-Rotation setzen —
            // FollowCamera richtet erst im Play-Mode aus, sonst schaut die Edit-Game-View horizontal in den Himmel.
            SetRef(avatar, "controller", controller);
            SetRef(avatar, "carryAnchor", carryAnchor);
            SetRef(avatar, "carryWarePrefab", warePrefab);
            SetRef(triggers, "controller", controller);
            SetRef(follow, "target", avatarGo.transform);
            var camOffset = new Vector3(0f, 8.5f, -6.5f); // ~52° Schrägaufsicht, nah (Genre-Standard)
            SetVector3(follow, "offset", camOffset);
            SetFloat(follow, "lookAheadZ", 2.5f); // Blick leicht vor den Avatar -> Avatar im unteren Bilddrittel
            camGo.transform.position = avatarGo.transform.position + camOffset;
            camGo.transform.LookAt(avatarGo.transform.position + Vector3.up * 1f + Vector3.forward * 2.5f);

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
        /// Liefert das Visual (z. B. für <see cref="ToonBob"/>).
        /// </summary>
        private static GameObject AttachModel(Transform parent, string assetPath, float targetHeight)
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
                return ph;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = prefab.name + "_Visual";
            go.transform.SetParent(parent, false);

            var bounds = CalcBounds(go);
            float scale = bounds.size.y > 0.0001f ? targetHeight / bounds.size.y : 1f;
            go.transform.localScale = Vector3.one * scale;
            // Nach der Skalierung: Basis auf 0, horizontal zentriert (Bounds skalieren mit)
            go.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            return go;
        }

        /// <summary>
        /// Welt-Schild (Billboard): Holzbrett + 3D-Text (built-in Font, kein TMP-Essentials-Zwang).
        /// Genre-Lesbarkeit: Gewerk-Namen über den Stationen, Preise an den Bauzäunen, Pad-Beschriftung.
        /// </summary>
        private static GameObject MakeSign(string text, Vector3 worldPos, float textSize, Color textColor, Color? boardColor = null)
        {
            // Breite deckeln: lange Namen (z. B. "Generalunternehmer") skalieren den Text herunter,
            // statt ein meterbreites Brett mitten ins Blickfeld zu haengen.
            const float maxWidth = 3.2f;
            float fitted = Mathf.Min(textSize, maxWidth / Mathf.Max(1, text.Length) / 0.62f);
            textSize = fitted;

            var root = new GameObject("Sign_" + text);
            root.transform.position = worldPos;
            root.AddComponent<BillboardLabel>();

            if (boardColor.HasValue)
            {
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "Board";
                Object.DestroyImmediate(board.GetComponent<Collider>());
                board.transform.SetParent(root.transform, false);
                float w = Mathf.Max(1.4f, text.Length * textSize * 0.62f);
                board.transform.localScale = new Vector3(w, textSize * 1.7f, 0.06f);
                board.transform.localPosition = new Vector3(0f, 0f, 0.06f);
                Paint(board, boardColor.Value);
            }

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform, false);
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = textSize * 10f / 64f;
            tm.color = textColor;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                textGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return root;
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
            var visual = AttachModel(go.transform, ModelDir + "/worker.glb", 1.6f);
            visual.AddComponent<ToonBob>(); // läuft Station<->Tresen -> Lauf-Bob
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
            var label = MakeSign("Arbeiter", pos + Vector3.up * 1.0f, 0.24f, new Color(0.15f, 0.75f, 0.85f));
            label.transform.SetParent(pad.transform, true); // verschwindet mit dem Pad nach der Anstellung
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

        private static void SetVector3(Object target, string field, Vector3 value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.vector3Value = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }

    internal static class Vector3Ext
    {
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
    }
}
