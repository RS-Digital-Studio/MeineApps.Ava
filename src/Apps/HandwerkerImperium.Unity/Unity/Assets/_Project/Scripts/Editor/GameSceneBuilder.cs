using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

            // Trag-Ware je Gewerk (GDD §6.1: jede Station eigene Ware). Fehlt ein GLB (lokal
            // regenerierbar), greift der generische Holz-Würfel.
            string[] wareModels =
            {
                ModelDir + "/stool.glb",                    // 0 schreiner (Pilot-Produkt)
                ModelDir + "/ware_plumber.glb",             // 1 Kupfer-Winkelrohr
                ModelDir + "/ware_electrician.glb",         // 2 Kabeltrommel
                ModelDir + "/ware_painter.glb",             // 3 Farbeimer
                ModelDir + "/ware_roofer.glb",              // 4 Ziegel-Paket
                ModelDir + "/ware_contractor.glb",          // 5 Ziegelstein-Block
                ModelDir + "/ware_architect.glb",           // 6 Bauplan-Bündel
                ModelDir + "/ware_general_contractor.glb",  // 7 Bauhelm
                ModelDir + "/ware_master_smith.glb",        // 8 Schmiede-Tiegel
                ModelDir + "/ware_innovation_lab.glb"       // 9 Zahnrad-Kiste
            };
            var stationWares = new GameObject[wareModels.Length];
            for (int i = 0; i < wareModels.Length; i++)
            {
                var ware = MakeStationWarePrefab("Game_Ware_" + i, wareModels[i]);
                stationWares[i] = ware != null ? ware : warePrefab;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Licht (warm, Werkstatt-Nachmittag) mit weichen Schatten
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1.0f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            // Landschaft: Himmel, Nebel-Tiefe, Gras-Welt, Pflaster-Hof + Plaza, Bäume/Hügel/Felsen
            BuildEnvironment(light);

            // Runtime (einziger Ticker + HMAC-Save) + Diagnose-HUD
            var runtimeGo = new GameObject("RuntimeGameController");
            var runtime = runtimeGo.AddComponent<RuntimeGameController>();
            SetRef(runtime, "config", gameConfig);
            var hud = runtimeGo.AddComponent<RuntimeHud>();
            SetRef(hud, "controller", runtime);

            // Audio-Hub: kuratierter SoundForge-Bestand (AudioSync aus dem Avalonia-HWI), Musik-Loop + SFX-Hooks
            AudioSync.Sync();
            var audioGo = new GameObject("GameAudio");
            var gameAudio = audioGo.AddComponent<GameAudio>();
            string[] sfxNames =
            {
                "sfx_button_tap", "sfx_money_earned", "sfx_coin_collect", "sfx_building_complete",
                "sfx_intern_ready", "sfx_hammering", "sfx_milestone_major", "sfx_prestige_complete",
                "sfx_offline_earnings", "sfx_news_ping", "sfx_costs_paid", "sfx_drop_common"
            };
            var clips = new Object[sfxNames.Length];
            for (int i = 0; i < sfxNames.Length; i++)
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Sfx/" + sfxNames[i] + ".ogg");
            SetRefArray(gameAudio, "sfxClips", clips);
            SetRef(gameAudio, "musicLoop", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Music/music_idle_workshop.ogg"));

            // Premium-HUD (UI Toolkit): Statuskarten/Tagesaufgaben/Hans-Toast (GameHud.uxml/.uss)
            var panelSettings = LoadOrCreate<UnityEngine.UIElements.PanelSettings>(SceneDir + "/Runtime_PanelSettings.asset");
            panelSettings.scaleMode = UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.match = 0.5f;
            var theme = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
            if (theme != null) panelSettings.themeStyleSheet = theme;
            EditorUtility.SetDirty(panelSettings);
            var uxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/_Project/UI/Hud/GameHud.uxml");
            if (uxml != null)
            {
                var hudGo = new GameObject("GameHud");
                var doc = hudGo.AddComponent<UnityEngine.UIElements.UIDocument>();
                doc.panelSettings = panelSettings;
                doc.visualTreeAsset = uxml;
                var binder = hudGo.AddComponent<HandwerkerImperium.UI.Hud.GameHudBinder>();
                SetRef(binder, "controller", runtime);
                SetRef(binder, "audioHub", gameAudio);
                var joystick = hudGo.AddComponent<TouchJoystick>(); // Android-Primärsteuerung (GDD §4)
                SetRef(joystick, "hudDocument", doc);
            }
            else
            {
                Debug.LogWarning("[GameSceneBuilder] GameHud.uxml nicht gefunden — Premium-HUD übersprungen (Import ausstehend?).");
            }

            // Physik-Loop-Controller, an den Runtime gekoppelt (kein DI-Scope, kein eigener Tick/Save)
            var controllerGo = new GameObject("GreyboxGameController");
            var controller = controllerGo.AddComponent<GreyboxGameController>();
            SetRef(controller, "runtime", runtime);
            SetRef(controller, "audioHub", gameAudio);

            // Avatar: CharacterController-Root (Füße auf y=0) + echtes Meister-Hans-Modell
            var avatarGo = new GameObject("Avatar");
            avatarGo.transform.position = new Vector3(0f, 0f, -4f);
            var cc = avatarGo.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f; cc.radius = 0.4f;
            AttachCharacter(avatarGo.transform, "avatar_hans", 1.8f); // geriggt: ProceduralBoneWalker, sonst ToonBob
            var carryAnchor = new GameObject("CarryAnchor").transform;
            carryAnchor.SetParent(avatarGo.transform);
            carryAnchor.localPosition = new Vector3(0f, 2.05f, 0f);
            var avatar = avatarGo.AddComponent<AvatarController>();
            var triggers = avatarGo.AddComponent<InteractionTriggerSystem>();

            // Kamera (fixer Schräg-Follow, Genre-Framing: ~52°, nah dran, Avatar im unteren Drittel)
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; // echter Horizont statt Solid-Color
            cam.fieldOfView = 45f; // enger als der 60er-Default — weniger Verzerrung, Figuren größer
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<FollowCamera>();
            var postVolume = BuildPostProcessing(cam);

            // Tresen: Marktstand-Modell der Pipeline (Fallback: brauner Block) + Trigger + Cash-Spawn
            GameObject counterGo;
            float counterSignY;
            if (AssetDatabase.LoadMainAssetAtPath(ModelDir + "/market_counter.glb") != null)
            {
                counterGo = new GameObject("Counter");
                counterGo.transform.position = Vector3.zero;
                AttachModel(counterGo.transform, ModelDir + "/market_counter.glb", 2.4f);
                var counterTrigger = counterGo.AddComponent<BoxCollider>();
                counterTrigger.isTrigger = true;
                counterTrigger.center = new Vector3(0f, 1f, 0f);
                counterTrigger.size = new Vector3(3.4f, 2f, 2.4f);
                var counterRb = counterGo.AddComponent<Rigidbody>();
                counterRb.isKinematic = true;
                counterRb.useGravity = false;
                counterSignY = 3.0f; // über dem Marktstand-Dach
            }
            else
            {
                counterGo = MakeBox("Counter", new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 1f, 1.4f), new Color(0.38f, 0.30f, 0.23f), trigger: true);
                counterSignY = 1.9f;
            }
            var counter = counterGo.AddComponent<CounterView>();
            var cashSpawn = new GameObject("CashSpawn").transform;
            cashSpawn.SetParent(counterGo.transform);
            cashSpawn.localPosition = new Vector3(0f, 0f, -1.6f);
            SetRef(counter, "controller", controller);
            SetRef(counter, "cashSpawnPoint", cashSpawn);
            SetRef(counter, "cashPrefab", coinPrefab);
            MakeSign("Verkauf", counterGo.transform.position.WithY(0f) + new Vector3(0f, counterSignY, 0f), 0.34f,
                new Color(0.98f, 0.95f, 0.88f), new Color(0.30f, 0.22f, 0.15f));

            // Lebendige Kunden-Schlange: NPCs kommen vom Stadttor, stellen sich an, gehen nach
            // der Bedienung ab — spiegelt die Domain-Queue (OrderQueueState.PendingCustomers).
            var customerPrefabs = new Object[]
            {
                MakeCustomerPrefab("customer_npc", 1.65f),
                MakeCustomerPrefab("customer_woman", 1.62f),
                MakeCustomerPrefab("customer_elder", 1.6f)
            };
            var queueFront = new GameObject("CustomerQueueFront").transform;
            queueFront.position = new Vector3(0f, 0f, 2.6f);                  // Kundenseite des Tresens
            queueFront.rotation = Quaternion.LookRotation(Vector3.forward);   // Schlange wächst nach Norden
            var customerSpawn = new GameObject("CustomerSpawn").transform;
            customerSpawn.position = new Vector3(2.5f, 0f, 14f);              // neben dem Stadttor
            var queue = counterGo.AddComponent<CustomerQueueView>();
            SetRef(queue, "controller", controller);
            SetRefArray(queue, "customerPrefabs", customerPrefabs);
            SetRef(queue, "spawnPoint", customerSpawn);
            SetRef(queue, "queueFront", queueFront);

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
            // Marktplatz-Dorf statt Kasernen-Reihen: die 10 Werkstätten stehen im Hufeisen-Bogen
            // (Radius 17 m) um den zentralen Platz, alle zur Mitte orientiert. Die Lücke zeigt nach
            // Norden (90°) — dort laufen Stadttor, Kundenweg und der Blick in die Landschaft.
            // Progression mit Spieler-Logik: Die Start-Schreinerei steht im Süden direkt vor dem
            // Spieler (Avatar-Start/Kamera), jedes weitere Gewerk wandert abwechselnd links/rechts
            // den Bogen hinauf — die teuersten Endgame-Gewerke rahmen oben sichtbar das Stadttor.
            int[] arcSlot = { 4, 5, 3, 6, 2, 7, 1, 8, 0, 9 }; // Bogen-Plätze 0..9 = 125°..415°
            for (int i = 0; i < stationCount; i++)
            {
                float angleDeg = 125f + arcSlot[i] * (290f / (stationCount - 1)); // Lücke um 90° (Norden)
                float rad = angleDeg * Mathf.Deg2Rad;
                stationPos[i] = new Vector3(Mathf.Cos(rad) * 17f, 0f, Mathf.Sin(rad) * 17f);
            }

            var fencePalette = new Color(0.85f, 0.68f, 0.20f); // Bauzaun-Gelb
            var stationTransforms = new Transform[stationCount];
            for (int i = 0; i < stationCount; i++)
            {
                Vector3 pos = stationPos[i];
                Vector3 toCenter = (Vector3.zero - pos).WithY(0f).normalized;

                var stGo = new GameObject($"Station_{i}");
                stGo.transform.position = pos;
                stGo.transform.rotation = Quaternion.LookRotation(toCenter);

                // Radialer Weg vom Marktplatz zum Plot (Hencoop-Lesbarkeit: Wege führen den Blick)
                Vector3 dir = pos.WithY(0f).normalized;
                MakePath(stGo.transform.parent != null ? stGo.transform.parent : stGo.transform,
                    dir * 10.8f, Quaternion.LookRotation(dir), new Vector2(2.2f, 8.6f), new Vector2(1f, 4f));

                // Gebäude-Modell (unlockedVisual — gesperrt ausgeblendet); Schornstein-Rauch lebt
                // am Modell-Root und erscheint damit automatisch erst nach dem Unlock.
                var modelRoot = new GameObject("Model");
                modelRoot.transform.SetParent(stGo.transform, false);
                AttachModel(modelRoot.transform, stationModels[i], 3.4f);
                MakeChimneySmoke(modelRoot.transform, new Vector3(0.7f, 3.3f, -0.4f));

                // Pickup-Zone + Waren-Stapel vor der Station
                var trig = MakeTriggerZone($"Station_{i}_Pickup", pos + toCenter * 2.9f + Vector3.up * 0.9f, new Vector3(2.6f, 2f, 2.6f));
                var view = trig.AddComponent<StationView>();
                var stackAnchor = new GameObject("StackAnchor").transform;
                stackAnchor.SetParent(stGo.transform, false);
                stackAnchor.position = pos + toCenter * 2.1f + Vector3.up * 0.25f;

                SetInt(view, "stationIndex", i);
                SetRef(view, "controller", controller);
                SetRef(view, "stackAnchor", stackAnchor);
                SetRef(view, "warePrefab", stationWares[i]); // Gewerk-eigene Ware im Stapel
                SetRef(view, "unlockedVisual", modelRoot);
                stationTransforms[i] = stGo.transform;

                // Gewerk-Schild knapp über dem Zaun an der Plot-Front (über der Station läge es außerhalb
                // des Kamera-Framings; höher im Sichtkegel würde es den Avatar verdecken)
                MakeSign(stationNames[i], pos + toCenter * 3.2f + Vector3.up * 2.2f, 0.36f,
                    new Color(0.99f, 0.96f, 0.90f), new Color(0.33f, 0.24f, 0.16f), post: true);

                // Gesperrte Plots: Bauzaun (lockedVisual + fenceVisual) + Preis-Schild + Hold-to-Pay-Zone.
                // Die helle Bauplatz-Fläche hängt am Zaun und verschwindet mit dem Unlock —
                // freigeschaltete Gebäude stehen direkt auf Gras (keine Dauer-Platte mehr).
                if (i > 0)
                {
                    GameObject fence;
                    if (AssetDatabase.LoadMainAssetAtPath(ModelDir + "/fence_construction.glb") != null)
                    {
                        // Pipeline-Zaun: zwei Segmente decken die Plot-Front (~4,6 m)
                        fence = new GameObject($"Fence_{i}");
                        fence.transform.position = pos + toCenter * 2.6f;
                        fence.transform.rotation = Quaternion.LookRotation(toCenter);
                        var segL = new GameObject("Segment_L");
                        segL.transform.SetParent(fence.transform, false);
                        segL.transform.localPosition = new Vector3(-1.15f, 0f, 0f);
                        AttachModel(segL.transform, ModelDir + "/fence_construction.glb", 1.4f);
                        var segR = new GameObject("Segment_R");
                        segR.transform.SetParent(fence.transform, false);
                        segR.transform.localPosition = new Vector3(1.15f, 0f, 0f);
                        AttachModel(segR.transform, ModelDir + "/fence_construction.glb", 1.4f);
                        var fenceCol = fence.AddComponent<BoxCollider>();
                        fenceCol.center = new Vector3(0f, 0.7f, 0f);
                        fenceCol.size = new Vector3(4.6f, 1.4f, 0.35f);
                        fence.transform.SetParent(stGo.transform, true);
                    }
                    else
                    {
                        fence = MakeBox($"Fence_{i}", pos + toCenter * 2.6f + Vector3.up * 0.6f, new Vector3(4.6f, 1.2f, 0.3f), fencePalette, trigger: false);
                        fence.transform.rotation = Quaternion.LookRotation(toCenter);
                        fence.transform.SetParent(stGo.transform, true);
                    }
                    // Helle Bauplatz-Markierung (Casual-Stil) — Kind des Zauns, weg nach Unlock
                    var sitePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sitePlate.name = "BuildSite";
                    Object.DestroyImmediate(sitePlate.GetComponent<Collider>());
                    sitePlate.transform.position = pos + new Vector3(0f, 0.02f, 0f);
                    sitePlate.transform.rotation = stGo.transform.rotation;
                    sitePlate.transform.localScale = new Vector3(5.4f, 0.04f, 5.4f);
                    Paint(sitePlate, new Color(0.95f, 0.88f, 0.70f));
                    sitePlate.transform.SetParent(fence.transform, true);

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

                // Perfekt-Aktions-Pad (GDD §6.7) auf der Gegenseite: Tap-Timing -> Produktions-Buff
                MakeMiniGamePad($"Boost_{i}", pos + toCenter * 2.2f - side * 3.0f + Vector3.up * 0.1f, i, runtime, controller);
            }

            // Upgrade-Pads (3 Achsen) zentral links neben dem Tresen, beschriftet
            MakeUpgradePad("Pad_Tempo", new Vector3(-6f, 0.1f, 2.5f), UpgradeTrack.StationSpeed, controller, new Color(0.9f, 0.4f, 0.4f));
            MakeUpgradePad("Pad_Radius", new Vector3(-6f, 0.1f, 0f), UpgradeTrack.CollectRadius, controller, new Color(0.4f, 0.9f, 0.4f));
            MakeUpgradePad("Pad_Kapazitaet", new Vector3(-6f, 0.1f, -2.5f), UpgradeTrack.CarryCapacity, controller, new Color(0.4f, 0.5f, 0.9f));
            MakeSign("Tempo", new Vector3(-6f, 1.1f, 2.5f), 0.26f, new Color(0.95f, 0.40f, 0.40f), post: true);
            MakeSign("Radius", new Vector3(-6f, 1.1f, 0f), 0.26f, new Color(0.35f, 0.85f, 0.35f), post: true);
            MakeSign("Tragkraft", new Vector3(-6f, 1.1f, -2.5f), 0.26f, new Color(0.45f, 0.55f, 0.95f), post: true);

            // Wahrzeichen des Stadt-Wiederaufbaus (GDD §6.4): Ruine -> Hold-to-Pay-Sanierung -> saniertes Modell.
            // Ids = LandmarkCatalog (Domain); Index-frei per Id verdrahtet (Alt-Save-robust).
            // Wahrzeichen flankieren den Kundenweg zum Stadttor (Blickachse der Kamera nach Norden)
            MakeLandmark("brunnen", "Brunnen", new Vector3(8f, 0f, 8.5f), 2.4f,
                ModelDir + "/landmark_fountain_ruined.glb", ModelDir + "/landmark_fountain_restored.glb", controller);
            MakeLandmark("glockenturm", "Glockenturm", new Vector3(-8f, 0f, 8.5f), 5.0f,
                ModelDir + "/landmark_clocktower_ruined.glb", ModelDir + "/landmark_clocktower_restored.glb", controller);
            MakeLandmark("stadttor", "Stadttor", new Vector3(0f, 0f, 16f), 4.5f,
                ModelDir + "/landmark_gate_ruined.glb", ModelDir + "/landmark_gate_restored.glb", controller);

            // Free-Cash-Pad (GDD §9.1) auf dem Platz: 2x Einkommen je Zeitblock + Münz-Regen
            MakeFreeCashPad(new Vector3(6f, 0.1f, -3.5f), runtime, controller, coinPrefab);

            // Deko-Schicht gegen den "leeren Hof": Laternen (mit warmem Punktlicht), Fässer,
            // Blumenbeete am Plaza-Rand, Handkarren — nur platziert, wenn das Pipeline-GLB existiert.
            MakeStreetLamp(new Vector3(8.5f, 0f, 4.2f));
            MakeStreetLamp(new Vector3(-8.5f, 0f, 4.2f));
            MakeStreetLamp(new Vector3(8.5f, 0f, -4.2f));
            MakeStreetLamp(new Vector3(-8.5f, 0f, -4.2f));
            MakeStreetLamp(new Vector3(2.8f, 0f, 12.0f));
            MakeStreetLamp(new Vector3(-2.8f, 0f, 12.0f));
            TryPlaceProp("flower_planter", new Vector3(5.6f, 0f, 3.6f), 32f, 0.55f);
            TryPlaceProp("flower_planter", new Vector3(-5.6f, 0f, 3.6f), -32f, 0.55f);
            TryPlaceProp("flower_planter", new Vector3(5.6f, 0f, -3.6f), 148f, 0.55f);
            TryPlaceProp("flower_planter", new Vector3(-5.6f, 0f, -3.6f), -148f, 0.55f);
            TryPlaceProp("barrel_crates", new Vector3(4.3f, 0f, -1.6f), 15f, 1.1f);
            TryPlaceProp("barrel_crates", new Vector3(-11f, 0f, 5.2f), 70f, 1.1f);
            TryPlaceProp("handcart", new Vector3(7.2f, 0f, 1.6f), 205f, 1.15f);
            TryPlaceProp("handcart", new Vector3(-14.5f, 0f, -4.5f), 130f, 1.15f);

            // "Die Stadt heilt": Sanierungs-Fortschritt steuert Sättigung + drei Erblüh-Stufen
            // (WorldMoodController). Die Stufen-Deko ist vorplatziert und startet inaktiv.
            var bloom1 = new GameObject("Bloom_Stage1");
            TryPlaceProp("flower_planter", new Vector3(2.9f, 0f, 9.2f), 12f, 0.55f)?.transform.SetParent(bloom1.transform, true);
            TryPlaceProp("flower_planter", new Vector3(-2.9f, 0f, 9.2f), -12f, 0.55f)?.transform.SetParent(bloom1.transform, true);
            TryPlaceProp("flower_planter", new Vector3(0f, 0f, -8.4f), 180f, 0.55f)?.transform.SetParent(bloom1.transform, true);
            var bloom2 = new GameObject("Bloom_Stage2");
            MakeBushInto(bloom2.transform, new Vector3(10.4f, 0f, 6.0f), 0.9f);
            MakeBushInto(bloom2.transform, new Vector3(-10.4f, 0f, 6.0f), 0.9f);
            MakeBushInto(bloom2.transform, new Vector3(10.4f, 0f, -6.6f), 0.85f);
            MakeBushInto(bloom2.transform, new Vector3(-10.4f, 0f, -6.6f), 0.85f);
            var bloom3 = new GameObject("Bloom_Stage3");
            TryPlaceProp("flower_planter", new Vector3(6.4f, 0f, 7.2f), 40f, 0.6f)?.transform.SetParent(bloom3.transform, true);
            TryPlaceProp("flower_planter", new Vector3(-6.4f, 0f, 7.2f), -40f, 0.6f)?.transform.SetParent(bloom3.transform, true);
            MakeBushInto(bloom3.transform, new Vector3(2.2f, 0f, 14.2f), 0.8f);
            MakeBushInto(bloom3.transform, new Vector3(-2.2f, 0f, 14.2f), 0.8f);
            bloom1.SetActive(false);
            bloom2.SetActive(false);
            bloom3.SetActive(false);
            var moodGo = new GameObject("WorldMood");
            var mood = moodGo.AddComponent<WorldMoodController>();
            SetRef(mood, "runtime", runtime);
            SetRef(mood, "postVolume", postVolume);
            SetRef(mood, "bloomStage1", bloom1);
            SetRef(mood, "bloomStage2", bloom2);
            SetRef(mood, "bloomStage3", bloom3);

            // Avatar-/Kamera-Verdrahtung. WICHTIG: Start-Position UND Start-Rotation setzen —
            // FollowCamera richtet erst im Play-Mode aus, sonst schaut die Edit-Game-View horizontal in den Himmel.
            SetRef(avatar, "controller", controller);
            SetRef(avatar, "carryAnchor", carryAnchor);
            SetRef(avatar, "carryWarePrefab", warePrefab);
            SetRefArray(avatar, "stationWarePrefabs", stationWares); // Carry-Stack zeigt die Ware der Quell-Station
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

        // ── Landschaft & Stimmung ───────────────────────────────────────────

        /// <summary>
        /// Baut die komplette Kulisse: prozedurale Skybox (warmer Nachmittag) + Distanz-Nebel,
        /// Gras-Welt (200 m, prozedurale Textur), Pflaster-Hof mit Plaza-Rondell um den Tresen,
        /// Baum-Ring + Horizont-Hügel + Felsen als Rahmen. Alle Texturen/Materialien werden
        /// deterministisch generiert und als Assets persistiert (Szene-Referenzen brauchen Assets).
        /// </summary>
        private static void BuildEnvironment(Light sun)
        {
            // Himmel + Atmosphäre
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_AtmosphereThickness", 0.95f);
            sky.SetColor("_SkyTint", new Color(0.50f, 0.66f, 0.86f));
            sky.SetColor("_GroundColor", new Color(0.58f, 0.54f, 0.46f));
            sky.SetFloat("_Exposure", 1.25f);
            SaveEnvAsset(sky, "Mat_Sky.mat");
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.64f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.53f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.38f, 0.33f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 170f;
            RenderSettings.fogColor = new Color(0.74f, 0.80f, 0.88f); // Richtung Horizont-Farbe

            var envRoot = new GameObject("Environment");

            // Gras-Welt (trägt den CharacterController) — Pipeline-Tile (Casual-Stil, nahtlos verifiziert)
            var grass = GameObject.CreatePrimitive(PrimitiveType.Plane);
            grass.name = "Ground_Grass";
            grass.transform.SetParent(envRoot.transform, false);
            grass.transform.localScale = new Vector3(20f, 1f, 20f); // 200 m
            grass.GetComponent<MeshRenderer>().sharedMaterial =
                MakeAssetTexturedMaterial("tex_grass", Color.white, new Vector2(50f, 50f), "Mat_Grass");

            // Marktplatz: warmes Casual-Pflaster (Pipeline-Tile). Gebäude stehen auf GRAS,
            // nur die Lauffläche Tresen/Pads/Queue ist gepflastert.
            var yard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            yard.name = "Ground_Yard";
            Object.DestroyImmediate(yard.GetComponent<Collider>()); // rein visuell, 4 cm — Gras-Plane trägt
            yard.transform.SetParent(envRoot.transform, false);
            yard.transform.localScale = new Vector3(22f, 0.04f, 14.5f);
            yard.transform.position = new Vector3(0f, 0.02f, -0.25f);
            yard.GetComponent<MeshRenderer>().sharedMaterial =
                MakeAssetTexturedMaterial("tex_cobblestone", Color.white, new Vector2(6.3f, 4.1f), "Mat_Plaza");

            // Helle Platz-Einfassung (Hencoop-Lesbarkeit: Flächen sind klar eingefasst)
            var yardTrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            yardTrim.name = "Ground_YardTrim";
            Object.DestroyImmediate(yardTrim.GetComponent<Collider>());
            yardTrim.transform.SetParent(envRoot.transform, false);
            yardTrim.transform.localScale = new Vector3(22.8f, 0.035f, 15.3f);
            yardTrim.transform.position = new Vector3(0f, 0.016f, -0.25f);
            Paint(yardTrim, new Color(0.96f, 0.90f, 0.74f));

            // Kundenweg zum Stadttor + radiale Wege zu allen Stationen (sandwarm, mit Einfassung)
            MakePath(envRoot.transform, new Vector3(0f, 0f, 11.5f), Quaternion.identity, new Vector2(4.5f, 9f), new Vector2(2f, 4f));

            // Vegetation + Horizont (deterministisch — gleicher Build, gleiche Welt).
            // Bäume/Büsche kommen aus der Pipeline (Casual-Look) mit Wind-Sway; Felsen sind
            // bewusst gestrichen (Hencoop-Welten leben von Grün + Blumen, nicht Geröll).
            Random.InitState(20260611);
            for (int i = 0; i < 24; i++)
            {
                float angle = (i / 24f) * Mathf.PI * 2f + Random.Range(-0.10f, 0.10f);
                float radius = Random.Range(28f, 46f);
                var pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                MakeTree(envRoot.transform, pos, Random.Range(0.85f, 1.45f));
            }
            for (int i = 0; i < 14; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(24f, 40f);
                MakeBush(envRoot.transform, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius),
                    Random.Range(0.6f, 1.1f));
            }
            for (int i = 0; i < 10; i++)
            {
                float angle = (i / 10f) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                float radius = Random.Range(70f, 95f);
                MakeHill(envRoot.transform, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius),
                    Random.Range(18f, 34f), Random.Range(6f, 12f));
            }

            // Sanft fallende Blätter über dem Spielbereich (Leben in der Luft, sehr dezent)
            MakeFallingLeaves(envRoot.transform);
        }

        /// <summary>
        /// Wegband im Casual-Stil: sandwarmes Pipeline-Tile + hellere Einfassung knapp darunter
        /// (Hencoop-Lesbarkeit). <paramref name="size"/> = (Breite, Länge) in Metern.
        /// </summary>
        private static void MakePath(Transform parent, Vector3 center, Quaternion rotation, Vector2 size, Vector2 tiling)
        {
            var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "Ground_Path";
            Object.DestroyImmediate(path.GetComponent<Collider>());
            path.transform.SetParent(parent, false);
            path.transform.position = center.WithY(0.024f);
            path.transform.rotation = rotation;
            path.transform.localScale = new Vector3(size.x, 0.035f, size.y);
            path.GetComponent<MeshRenderer>().sharedMaterial =
                MakeAssetTexturedMaterial("tex_dirt_path", Color.white, tiling, $"Mat_Path_{size.x:0_0}x{size.y:0_0}");

            var trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim.name = "Ground_PathTrim";
            Object.DestroyImmediate(trim.GetComponent<Collider>());
            trim.transform.SetParent(parent, false);
            trim.transform.position = center.WithY(0.014f);
            trim.transform.rotation = rotation;
            trim.transform.localScale = new Vector3(size.x + 0.5f, 0.03f, size.y + 0.5f);
            Paint(trim, new Color(0.96f, 0.90f, 0.74f));
        }

        /// <summary>Pipeline-Busch mit Wind-Sway (leicht versenkt gegen Konzept-Grassaum). Fallback: nichts.</summary>
        private static void MakeBush(Transform parent, Vector3 pos, float scale) => MakeBushInto(parent, pos, scale);

        private static void MakeBushInto(Transform parent, Vector3 pos, float scale)
        {
            if (AssetDatabase.LoadMainAssetAtPath(ModelDir + "/bush_round.glb") == null) return;
            var root = new GameObject("Bush");
            root.transform.SetParent(parent, false);
            root.transform.position = pos + new Vector3(0f, -0.05f, 0f);
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var visual = AttachModel(root.transform, ModelDir + "/bush_round.glb", 0.9f * scale);
            visual.AddComponent<CrownSway>();
        }

        /// <summary>Gemütlicher Schornstein-Rauch (lebt am unlockedVisual — erscheint erst nach dem Unlock).</summary>
        private static void MakeChimneySmoke(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("ChimneySmoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 3.6f;
            main.startSpeed = 0.55f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startColor = new Color(0.95f, 0.95f, 0.97f, 0.4f);
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 2.2f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            ApplyParticleMaterial(ps);
        }

        /// <summary>Sehr dezentes Blätter-Treiben über dem Hof (Box-Emitter, Pipeline-Partikel-Material).</summary>
        private static void MakeFallingLeaves(Transform parent)
        {
            var go = new GameObject("FallingLeaves");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 7.5f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 11f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.78f, 0.35f, 0.85f), new Color(0.85f, 0.72f, 0.35f, 0.85f));
            main.startRotation3D = true;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.045f;
            var emission = ps.emission;
            emission.rateOverTime = 2.5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 0.5f, 26f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.25f;
            ApplyParticleMaterial(ps);
        }

        /// <summary>Pipeline-Default-Partikel-Material (Shader.Find wäre im Editor-Batch magenta-riskant).</summary>
        private static void ApplyParticleMaterial(ParticleSystem ps)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultParticleMaterial != null)
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = pipeline.defaultParticleMaterial;
        }

        /// <summary>
        /// Pipeline-Baum (Casual-Look) mit Wind-Sway: 60/40-Mix aus rundem und hohem Baum, leicht
        /// im Boden versenkt (Konzept-Sockel verschwindet). Fallback: Primitive-Baum (Stamm + Kugeln).
        /// </summary>
        private static void MakeTree(Transform parent, Vector3 pos, float scale)
        {
            string glb = Random.value < 0.6f ? "/tree_round.glb" : "/tree_tall.glb";
            float height = glb.Contains("tall") ? 5.6f * scale : 4.4f * scale;
            if (AssetDatabase.LoadMainAssetAtPath(ModelDir + glb) != null)
            {
                var glbRoot = new GameObject("Tree");
                glbRoot.transform.SetParent(parent, false);
                glbRoot.transform.position = pos + new Vector3(0f, -0.12f, 0f); // Sockel versenken
                glbRoot.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                var visual = AttachModel(glbRoot.transform, ModelDir + glb, height);
                visual.AddComponent<CrownSway>();
                return;
            }

            // Fallback: Primitive-Baum (nur wenn die Pipeline-GLBs lokal fehlen)
            var root = new GameObject("Tree");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.localScale = Vector3.one * scale;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(trunk.GetComponent<Collider>());
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localScale = new Vector3(0.45f, 1.1f, 0.45f);
            trunk.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            Paint(trunk, new Color(0.42f, 0.30f, 0.20f));

            float hueShift = Random.Range(-0.06f, 0.06f);
            var leaf = new Color(0.30f + hueShift, 0.52f + hueShift * 0.5f, 0.26f);
            int crowns = Random.Range(2, 4);
            for (int i = 0; i < crowns; i++)
            {
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(crown.GetComponent<Collider>());
                crown.name = "Crown";
                crown.transform.SetParent(root.transform, false);
                float s = Random.Range(1.6f, 2.4f);
                crown.transform.localScale = Vector3.one * s;
                crown.transform.localPosition = new Vector3(Random.Range(-0.5f, 0.5f), 2.3f + i * 0.7f, Random.Range(-0.5f, 0.5f));
                Paint(crown, new Color(leaf.r + i * 0.025f, leaf.g + i * 0.03f, leaf.b));
            }
        }

        /// <summary>Weicher Horizont-Hügel (plattgedrückte Kugel, sitzt im Distanz-Nebel).</summary>
        private static void MakeHill(Transform parent, Vector3 pos, float width, float height)
        {
            var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(hill.GetComponent<Collider>());
            hill.name = "Hill";
            hill.transform.SetParent(parent, false);
            hill.transform.position = pos;
            hill.transform.localScale = new Vector3(width, height * 2f, width);
            hill.transform.position = new Vector3(pos.x, 0f, pos.z); // Kugel-Mitte auf 0 -> Halbkugel ragt heraus
            Paint(hill, new Color(0.38f, 0.50f, 0.30f));
        }

        /// <summary>Material aus einer Pipeline-Textur unter <c>Art/Textures/</c> (Asset, ASTC via Import).</summary>
        private static Material MakeAssetTexturedMaterial(string textureName, Color tint, Vector2 tiling, string matName)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/" + textureName + ".png");
            var mat = MakePipelineMaterial(tint);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.mainTextureScale = tiling;
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f); // matt — Casual-Look glänzt nicht
            SaveEnvAsset(mat, matName + ".mat");
            return mat;
        }

        /// <summary>URP-Post-Processing: Bloom + Vignette + ACES-Tonemapping + warme Farb-Justage (AAA-Look).</summary>
        private static Volume BuildPostProcessing(Camera cam)
        {
            string path = SceneDir + "/Game_PostProfile.asset";
            AssetDatabase.DeleteAsset(path); // frisch erzeugen — Sub-Assets sauber halten
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>();
            bloom.name = "Bloom";
            bloom.intensity.Override(0.45f);
            bloom.threshold.Override(1.05f);
            AssetDatabase.AddObjectToAsset(bloom, profile);

            var vignette = profile.Add<Vignette>();
            vignette.name = "Vignette";
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.42f);
            AssetDatabase.AddObjectToAsset(vignette, profile);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.name = "Tonemapping";
            tonemapping.mode.Override(TonemappingMode.ACES);
            AssetDatabase.AddObjectToAsset(tonemapping, profile);

            var colors = profile.Add<ColorAdjustments>();
            colors.name = "ColorAdjustments";
            colors.saturation.Override(10f);
            colors.postExposure.Override(0.08f);
            colors.colorFilter.Override(new Color(1.0f, 0.985f, 0.955f)); // warmer Hauch
            AssetDatabase.AddObjectToAsset(colors, profile);

            AssetDatabase.SaveAssets();

            var volGo = new GameObject("PostProcessing");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            return vol;
        }

        private static void SaveEnvAsset(Object obj, string fileName)
        {
            string path = $"{PrefabDir}/{fileName}";
            AssetDatabase.DeleteAsset(path); // Rebuild erzeugt frisch (Szene wird ohnehin neu gebaut)
            AssetDatabase.CreateAsset(obj, path);
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
        private static GameObject MakeSign(string text, Vector3 worldPos, float textSize, Color textColor, Color? boardColor = null, bool post = false)
        {
            // Breite deckeln: lange Namen (z. B. "Generalunternehmer") skalieren den Text herunter,
            // statt ein meterbreites Brett mitten ins Blickfeld zu haengen.
            const float maxWidth = 3.2f;
            float fitted = Mathf.Min(textSize, maxWidth / Mathf.Max(1, text.Length) / 0.62f);
            textSize = fitted;

            var root = new GameObject("Sign_" + text);
            root.transform.position = worldPos;
            root.AddComponent<BillboardLabel>();

            // Holzpfosten bis zum Boden — Schilder schweben sonst frei in der Luft (Generik-Killer).
            // Zylindrisch, darf also mit dem Billboard mitdrehen.
            if (post && worldPos.y > 0.2f)
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Post";
                Object.DestroyImmediate(pole.GetComponent<Collider>());
                pole.transform.SetParent(root.transform, false);
                pole.transform.localScale = new Vector3(0.09f, worldPos.y * 0.5f, 0.09f);
                pole.transform.localPosition = new Vector3(0f, -worldPos.y * 0.5f, 0f);
                Paint(pole, new Color(0.36f, 0.27f, 0.18f));
            }

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
            AttachCharacter(go.transform, "worker", 1.6f); // läuft Station<->Tresen -> echter Walk-Cycle
            go.AddComponent<WorkerNpc>();
            return SavePrefab(go, "Game_Worker");
        }

        /// <summary>
        /// Charakter-Visual: bevorzugt das UniRig-geriggte Modell (<c>{name}_rigged.glb</c>) mit
        /// <see cref="ProceduralBoneWalker"/> (echte Gelenk-Animation); Fallback aufs ungeriggte
        /// Modell mit <see cref="ToonBob"/>. Liefert das Visual.
        /// </summary>
        private static GameObject AttachCharacter(Transform parent, string baseName, float height)
        {
            string riggedPath = ModelDir + "/" + baseName + "_rigged.glb";
            if (AssetDatabase.LoadMainAssetAtPath(riggedPath) != null)
            {
                var visual = AttachModel(parent, riggedPath, height);
                visual.AddComponent<ProceduralBoneWalker>();
                return visual;
            }
            var fallback = AttachModel(parent, ModelDir + "/" + baseName + ".glb", height);
            fallback.AddComponent<ToonBob>();
            return fallback;
        }

        /// <summary>Gewerk-eigene Trag-Ware als Prefab (Modell, ~0,42 m). Fehlendes GLB -> null (Fallback generisch).</summary>
        private static GameObject MakeStationWarePrefab(string prefabName, string glbPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(glbPath) == null) return null;
            var go = new GameObject(prefabName);
            AttachModel(go.transform, glbPath, 0.42f);
            return SavePrefab(go, prefabName);
        }

        /// <summary>
        /// Wahrzeichen-Plot: Ruinen- + Saniert-Modell (LandmarkView tauscht beim Abschluss), Sanieren-Zone
        /// (Hold-to-Pay) Richtung Hof-Mitte, Namens-Schild + dynamisches Fortschritts-Schild ("2/4").
        /// </summary>
        private static void MakeLandmark(string id, string displayName, Vector3 pos, float height,
            string ruinedGlb, string restoredGlb, GreyboxGameController controller)
        {
            var root = new GameObject("Landmark_" + id);
            root.transform.position = pos;
            Vector3 toCenter = (Vector3.zero - pos).WithY(0f);
            toCenter = toCenter.sqrMagnitude < 0.01f ? Vector3.back : toCenter.normalized;
            root.transform.rotation = Quaternion.LookRotation(toCenter);

            var ruined = new GameObject("Ruined");
            ruined.transform.SetParent(root.transform, false);
            AttachModel(ruined.transform, ruinedGlb, height);
            var restored = new GameObject("Restored");
            restored.transform.SetParent(root.transform, false);
            AttachModel(restored.transform, restoredGlb, height);
            restored.SetActive(false); // LandmarkView blendet beim Sanierungs-Abschluss um

            var pay = MakeTriggerZone($"Landmark_{id}_Sanieren", pos + toCenter * (height * 0.5f + 2.0f) + Vector3.up * 0.9f, new Vector3(2.8f, 2f, 2.6f));
            var view = pay.AddComponent<LandmarkView>();

            MakeSign(displayName, pos + toCenter * 1.6f + Vector3.up * 2.5f, 0.36f,
                new Color(0.99f, 0.96f, 0.90f), new Color(0.30f, 0.24f, 0.18f), post: true);
            var progSign = MakeSign("0/0", pos + toCenter * 1.6f + Vector3.up * 1.75f, 0.30f,
                new Color(1.0f, 0.85f, 0.25f), new Color(0.25f, 0.20f, 0.12f));
            var progText = progSign.GetComponentInChildren<TextMesh>();

            SetString(view, "landmarkId", id);
            SetRef(view, "controller", controller);
            SetRef(view, "ruinedVisual", ruined);
            SetRef(view, "restoredVisual", restored);
            SetRef(view, "progressText", progText);
        }

        /// <summary>
        /// Straßenlaterne mit glaubwürdiger Silhouette: Das Pipeline-Modell (gedrungene Standlaterne)
        /// wird NUR als Laternenkopf (0,85 m) genutzt — voll skaliert wirkt der Korb wie eine
        /// Litfaßsäule. Darunter: Stein-Fuß + schlanker Eisen-Pfosten (Primitive), warmes Punktlicht.
        /// </summary>
        private static void MakeStreetLamp(Vector3 pos)
        {
            const float postHeight = 2.0f;
            var root = new GameObject("Prop_street_lamp");
            root.transform.position = pos;

            var foot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            foot.name = "Foot";
            Object.DestroyImmediate(foot.GetComponent<Collider>());
            foot.transform.SetParent(root.transform, false);
            foot.transform.localScale = new Vector3(0.42f, 0.09f, 0.42f);
            foot.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            Paint(foot, new Color(0.45f, 0.44f, 0.42f));

            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.SetParent(root.transform, false);
            post.transform.localScale = new Vector3(0.11f, postHeight * 0.5f, 0.11f);
            post.transform.localPosition = new Vector3(0f, postHeight * 0.5f, 0f);
            Paint(post, new Color(0.16f, 0.15f, 0.14f)); // Schmiedeeisen

            if (AssetDatabase.LoadMainAssetAtPath(ModelDir + "/street_lantern.glb") != null)
            {
                var head = new GameObject("Head");
                head.transform.SetParent(root.transform, false);
                head.transform.localPosition = new Vector3(0f, postHeight - 0.06f, 0f);
                AttachModel(head.transform, ModelDir + "/street_lantern.glb", 0.85f);
            }

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.4f, 0f);
            col.radius = 0.18f;
            col.height = 2.8f;

            var lightGo = new GameObject("LanternLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, postHeight + 0.35f, 0f);
            var pt = lightGo.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.color = new Color(1.0f, 0.78f, 0.45f);
            pt.intensity = 1.6f;
            pt.range = 7f;
            pt.shadows = LightShadows.None; // Punktlicht-Schatten sind auf Mobile zu teuer
        }

        /// <summary>
        /// Deko-Prop aus der Pipeline (nur wenn das GLB existiert): bounds-skaliert, grober
        /// Box-Collider (Avatar läuft nicht hindurch).
        /// </summary>
        private static GameObject TryPlaceProp(string glbName, Vector3 pos, float yRotation, float height, bool lantern = false)
        {
            string path = ModelDir + "/" + glbName + ".glb";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) return null;
            var root = new GameObject("Prop_" + glbName);
            root.transform.position = pos;
            var visual = AttachModel(root.transform, path, height);

            var bounds = CalcBounds(root);
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, height * 0.5f, 0f);
            col.size = new Vector3(Mathf.Max(0.3f, bounds.size.x * 0.8f), height, Mathf.Max(0.3f, bounds.size.z * 0.8f));

            if (lantern)
            {
                var lightGo = new GameObject("LanternLight");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, height * 0.78f, 0f);
                var pt = lightGo.AddComponent<Light>();
                pt.type = LightType.Point;
                pt.color = new Color(1.0f, 0.78f, 0.45f);
                pt.intensity = 1.6f;
                pt.range = 7f;
                pt.shadows = LightShadows.None; // Punktlicht-Schatten sind auf Mobile zu teuer
            }

            root.transform.rotation = Quaternion.Euler(0f, yRotation, 0f); // nach Collider-Aufbau (lokale Box dreht mit)
            return root;
        }

        /// <summary>Laufender Queue-Kunde als Prefab (CustomerQueueView spawnt zur Laufzeit; Gang via Walker).</summary>
        private static GameObject MakeCustomerPrefab(string baseName, float height)
        {
            var go = new GameObject("Game_" + baseName);
            AttachCharacter(go.transform, baseName, height);
            go.AddComponent<CustomerAgent>();
            return SavePrefab(go, "Game_" + baseName);
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ── Szene-Bausteine (wie GreyboxSceneBuilder) ───────────────────────

        /// <summary>
        /// Pad-Visual auf AAA-Niveau statt flacher Farbscheibe: heller Stein-Sockel + farbiger
        /// Akzent-Ring + leuchtendes Zentrum (Emission über Base-Color-Aufhellung).
        /// </summary>
        private static void MakePadVisual(Transform parent, Color accent)
        {
            var socket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socket.name = "Socket";
            Object.DestroyImmediate(socket.GetComponent<Collider>());
            socket.transform.SetParent(parent, false);
            socket.transform.localScale = new Vector3(2.3f, 0.05f, 2.3f);
            Paint(socket, new Color(0.74f, 0.71f, 0.66f));

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(parent, false);
            ring.transform.localScale = new Vector3(1.9f, 0.055f, 1.9f);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            Paint(ring, accent);

            var core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            core.name = "Core";
            Object.DestroyImmediate(core.GetComponent<Collider>());
            core.transform.SetParent(parent, false);
            core.transform.localScale = new Vector3(1.3f, 0.06f, 1.3f);
            core.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            Paint(core, Color.Lerp(accent, Color.white, 0.45f));
        }

        /// <summary>
        /// Perfekt-Aktions-Pad (GDD §6.7): Sockel + fester Gold-Ziel-Ring + weiße Puls-Scheibe
        /// KNAPP DARUNTER — schrumpft die Puls-Scheibe, verschwindet ihr weißer Rand exakt im
        /// Deckungs-Moment unter dem Gold-Ring ("Rand weg = JETZT tippen"). Feedback-Text als Billboard.
        /// </summary>
        private static void MakeMiniGamePad(string name, Vector3 pos, int stationIndex,
            RuntimeGameController runtime, GreyboxGameController controller)
        {
            var pad = MakeTriggerZone(name, pos, new Vector3(2.4f, 1.5f, 2.4f));

            var socket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socket.name = "Socket";
            Object.DestroyImmediate(socket.GetComponent<Collider>());
            socket.transform.SetParent(pad.transform, false);
            socket.transform.localScale = new Vector3(2.5f, 0.05f, 2.5f);
            Paint(socket, new Color(0.74f, 0.71f, 0.66f));

            var pulse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pulse.name = "PulseRing";
            Object.DestroyImmediate(pulse.GetComponent<Collider>());
            pulse.transform.SetParent(pad.transform, false);
            pulse.transform.localScale = new Vector3(2.3f, 0.05f, 2.3f);
            pulse.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            Paint(pulse, new Color(0.97f, 0.96f, 0.92f));

            var target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = "TargetRing";
            Object.DestroyImmediate(target.GetComponent<Collider>());
            target.transform.SetParent(pad.transform, false);
            target.transform.localScale = new Vector3(1.2f, 0.055f, 1.2f);
            target.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            Paint(target, new Color(1.0f, 0.78f, 0.20f));

            var feedbackRoot = new GameObject("Feedback");
            feedbackRoot.transform.SetParent(pad.transform, false);
            feedbackRoot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            feedbackRoot.AddComponent<BillboardLabel>();
            var tm = feedbackRoot.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = 0.30f * 10f / 64f;
            var feedbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (feedbackFont != null)
            {
                tm.font = feedbackFont;
                feedbackRoot.GetComponent<MeshRenderer>().sharedMaterial = feedbackFont.material;
            }

            var view = pad.AddComponent<MiniGamePadView>();
            SetRef(view, "runtime", runtime);
            SetRef(view, "controller", controller);
            SetInt(view, "stationIndex", stationIndex);
            SetRef(view, "pulseRing", pulse.transform);
            SetRef(view, "targetRing", target.transform);
            SetRef(view, "feedbackText", tm);

            var label = MakeSign("Boost", pos + Vector3.up * 1.0f, 0.24f, new Color(1.0f, 0.62f, 0.20f), post: true);
            label.transform.SetParent(pad.transform, true);
        }

        /// <summary>Free-Cash-Pad (GDD §9.1): Gold-Pad + Countdown-Schild; Claim + Münz-Regen via FreeCashPadView.</summary>
        private static void MakeFreeCashPad(Vector3 pos, RuntimeGameController runtime, GreyboxGameController controller, GameObject coinPrefab)
        {
            var pad = MakeTriggerZone("FreeCashPad", pos, new Vector3(2.5f, 1.5f, 2.5f));
            MakePadVisual(pad.transform, new Color(1.0f, 0.78f, 0.20f)); // Gold
            var core = pad.transform.Find("Core");

            var labelRoot = new GameObject("Label");
            labelRoot.transform.SetParent(pad.transform, false);
            labelRoot.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            labelRoot.AddComponent<BillboardLabel>();
            var tm = labelRoot.AddComponent<TextMesh>();
            tm.text = "Gratis-Geld!";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = 0.28f * 10f / 64f;
            var cashFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cashFont != null)
            {
                tm.font = cashFont;
                labelRoot.GetComponent<MeshRenderer>().sharedMaterial = cashFont.material;
            }

            var view = pad.AddComponent<FreeCashPadView>();
            SetRef(view, "runtime", runtime);
            SetRef(view, "controller", controller);
            SetRef(view, "cashPrefab", coinPrefab);
            SetRef(view, "labelText", tm);
            if (core != null) SetRef(view, "readyVisual", core.gameObject);
        }

        private static void MakeUpgradePad(string name, Vector3 pos, UpgradeTrack track, GreyboxGameController controller, Color color)
        {
            var pad = MakeTriggerZone(name, pos, new Vector3(2.5f, 1.5f, 2.5f));
            MakePadVisual(pad.transform, color);
            var view = pad.AddComponent<UpgradePadView>();
            SetEnum(view, "track", (int)track);
            SetRef(view, "controller", controller);
        }

        private static void MakeHirePad(string name, Vector3 pos, int stationIndex, GreyboxGameController controller, GameObject workerPrefab, Transform stationPoint, Transform counterPoint)
        {
            var pad = MakeTriggerZone(name, pos, new Vector3(2.5f, 1.5f, 2.5f));
            MakePadVisual(pad.transform, new Color(0.2f, 0.8f, 0.9f));
            var view = pad.AddComponent<WorkerHirePadView>();
            SetInt(view, "stationIndex", stationIndex);
            SetRef(view, "controller", controller);
            SetRef(view, "workerNpcPrefab", workerPrefab);
            SetRef(view, "stationPoint", stationPoint);
            SetRef(view, "counterPoint", counterPoint);
            SetRef(view, "spawnPoint", pad.transform);
            var label = MakeSign("Arbeiter", pos + Vector3.up * 1.0f, 0.24f, new Color(0.15f, 0.75f, 0.85f), post: true);
            label.transform.SetParent(pad.transform, true); // verschwindet mit dem Pad (Max-Stufe)
            SetRef(view, "labelText", label.GetComponentInChildren<TextMesh>()); // Pad aktualisiert: Arbeiter -> Tempo x/4 -> MAX
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

        private static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetRefArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
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
