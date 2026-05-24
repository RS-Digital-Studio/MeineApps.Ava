#nullable enable
using System.IO;
using System.Linq;
using ArcaneKingdom.Domain.Config;
using ArcaneKingdom.EditorTools.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArcaneKingdom.EditorTools.Setup
{
    /// <summary>
    /// Setup-Wizard fuer den ersten Project-Open in Unity 6.
    /// Macht alles in einem Klick: BalancingConfig anlegen, JSON-Daten importieren,
    /// Build-Scenes registrieren, Setup-Status anzeigen.
    /// </summary>
    public sealed class FirstTimeSetupWizard : EditorWindow
    {
        private const string MenuPath = "ArcaneKingdom/Setup/First-Time Setup Wizard";
        private const string BalancingConfigPath = "Assets/_Project/ScriptableObjects/Config/BalancingConfig.asset";
        private const string SetupCompletedPrefKey = "ArcaneKingdom.SetupCompleted";

        private static readonly string[] RequiredScenes =
        {
            "Assets/_Project/Scenes/Boot/Boot.unity",
            "Assets/_Project/Scenes/Hub/Hub.unity",
            "Assets/_Project/Scenes/Battle/Battle.unity",
            "Assets/_Project/Scenes/Arena/Arena.unity",
            "Assets/_Project/Scenes/Guild/Guild.unity",
            "Assets/_Project/Scenes/GuildWorld/GuildWorld.unity"
        };

        private Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<FirstTimeSetupWizard>("ArcaneKingdom Setup");
            window.minSize = new Vector2(540, 480);
            window.Show();
        }

        [InitializeOnLoadMethod]
        private static void AutoOpenOnce()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorPrefs.GetBool(SetupCompletedPrefKey, false))
                    Open();
            };
        }

        // ------------------------------------------------------------ Check-Funktionen

        private static bool BalancingConfigExists() => AssetDatabase.LoadAssetAtPath<BalancingConfig>(BalancingConfigPath) != null;

        private static bool ScriptableObjectsImported()
        {
            var cardCount = AssetDatabase.FindAssets("t:CardDefinition").Length;
            return cardCount >= 30;
        }

        private static bool AllBuildScenesRegistered()
        {
            var existing = EditorBuildSettings.scenes.Select(s => s.path).ToHashSet();
            foreach (var s in RequiredScenes) if (!existing.Contains(s)) return false;
            return true;
        }

        private static bool BootSceneIsFirst()
        {
            var scenes = EditorBuildSettings.scenes;
            return scenes.Length > 0 && scenes[0].path == RequiredScenes[0];
        }

        // ------------------------------------------------------------ Aktionen

        private static void CreateBalancingConfig()
        {
            var dir = Path.GetDirectoryName(BalancingConfigPath)!;
            EnsureFolder(dir);
            var asset = ScriptableObject.CreateInstance<BalancingConfig>();
            AssetDatabase.CreateAsset(asset, BalancingConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] BalancingConfig.asset angelegt unter {BalancingConfigPath}");
        }

        private static void ImportData()
        {
            DataImporter.ImportAll();
        }

        private static void RegisterBuildScenes()
        {
            var existing = EditorBuildSettings.scenes.ToList();
            foreach (var scenePath in RequiredScenes)
            {
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"[Setup] Scene fehlt: {scenePath}");
                    continue;
                }
                if (existing.Any(s => s.path == scenePath)) continue;
                existing.Add(new EditorBuildSettingsScene(scenePath, enabled: true));
            }
            // Boot.unity muss Index 0 sein
            existing = existing.OrderBy(s => s.path == RequiredScenes[0] ? 0 : 1).ToList();
            EditorBuildSettings.scenes = existing.ToArray();
            Debug.Log($"[Setup] Build-Scenes registriert ({existing.Count} Scenes, Boot.unity als Index 0).");
        }

        [MenuItem("ArcaneKingdom/Setup/Run All Setup Steps")]
        public static void RunAllSteps()
        {
            if (!BalancingConfigExists()) CreateBalancingConfig();
            if (!ScriptableObjectsImported()) ImportData();
            if (!AllBuildScenesRegistered() || !BootSceneIsFirst()) RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorPrefs.SetBool(SetupCompletedPrefKey, true);
            Debug.Log("[Setup] Alle Schritte ausgefuehrt — Boot.unity oeffnen + manuelles Bootstrapper-Setup (siehe Wizard).");
        }

        // ------------------------------------------------------------ UI

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ArcaneKingdom — First-Time Setup (Unity 6)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Stand: 2026-05-24 — Pre-MVP, vollstaendige Business-Logik vorhanden.");
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawStep(
                index: 1,
                title: "BalancingConfig.asset",
                description: "Globale Konstanten (Energie-Cap, Mana, Kampf-Regeln) — wird einmalig erzeugt.",
                isDone: BalancingConfigExists(),
                actionLabel: "Anlegen",
                action: CreateBalancingConfig
            );

            DrawStep(
                index: 2,
                title: "JSON-Daten importieren",
                description: "Generiert alle ScriptableObjects aus Resources/Data/ (30 Karten, 32 Faehigkeiten, 18 Runen, 6 Helden, 9 Welten, 4 Sets).",
                isDone: ScriptableObjectsImported(),
                actionLabel: "Import All",
                action: ImportData
            );

            DrawStep(
                index: 3,
                title: "Build-Scenes registrieren",
                description: "Fuegt Boot/Hub/Battle/Arena/Guild/GuildWorld in den Build-Settings ein. Boot.unity wird Index 0.",
                isDone: AllBuildScenesRegistered() && BootSceneIsFirst(),
                actionLabel: "Registrieren",
                action: RegisterBuildScenes
            );

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Manueller Schritt — Bootstrapper verdrahten", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Boot.unity oeffnen (Doppelklick im Project-Fenster)\n" +
                "2. GameObject [Bootstrapper] in Hierarchy waehlen\n" +
                "3. Add Component → ArcaneKingdom.Bootstrap.RootLifetimeScope\n" +
                "4. Im RootLifetimeScope-Inspector den Slot 'Balancing Config' mit BalancingConfig.asset belegen\n" +
                "5. Unter [Bootstrapper] ein leeres Child '[Audio]' anlegen\n" +
                "6. Add Component → ArcaneKingdom.Game.Services.UnityAudioService am [Audio]-GameObject\n" +
                "7. Im RootLifetimeScope den Slot 'Audio Service' mit [Audio] verknuepfen\n" +
                "8. Boot.unity speichern (Strg+S)",
                MessageType.Info);

            if (GUILayout.Button("Boot.unity jetzt oeffnen", GUILayout.Height(26)))
            {
                EditorSceneManager.OpenScene(RequiredScenes[0]);
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Sammelaktion", EditorStyles.boldLabel);
            if (GUILayout.Button("Alle automatischen Schritte ausfuehren (1-3)", GUILayout.Height(30)))
            {
                RunAllSteps();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Optionale Werkzeuge", EditorStyles.boldLabel);
            if (GUILayout.Button("Card Preview oeffnen")) EditorApplication.ExecuteMenuItem("ArcaneKingdom/Inspectors/Card Preview");
            if (GUILayout.Button("Balancing Dashboard oeffnen")) EditorApplication.ExecuteMenuItem("ArcaneKingdom/Inspectors/Balancing Dashboard");
            if (GUILayout.Button("Localization Check oeffnen")) EditorApplication.ExecuteMenuItem("ArcaneKingdom/Inspectors/Localization Check");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Nach erfolgreichem Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drueck Play in der Boot.unity. Im Console-Log sollte erscheinen:\n" +
                "  [Boot] ArcaneKingdom gestartet.\n" +
                "  [FirebaseAuth] SignInAnonymouslyAsync — STUB.\n" +
                "  [Save] Neuer Save initialisiert.\n" +
                "  [Login] Lade Hub-Scene...\n\n" +
                "Wenn das laeuft, ist die gesamte Business-Logik funktional — UI-Layouts kommen separat.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawStep(int index, string title, string description, bool isDone, string actionLabel, System.Action action)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            var status = isDone ? "✓" : "○";
            GUI.color = isDone ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.95f, 0.78f, 0.30f);
            EditorGUILayout.LabelField($"{status}  Schritt {index}: {title}", EditorStyles.boldLabel);
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(isDone))
            {
                if (GUILayout.Button(actionLabel, GUILayout.Width(130))) action();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        private static void EnsureFolder(string relativePath)
        {
            if (AssetDatabase.IsValidFolder(relativePath)) return;
            var parts = relativePath.Replace("\\", "/").Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
