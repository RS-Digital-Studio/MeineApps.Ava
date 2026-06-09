using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HandwerkerImperium.Game;

namespace HandwerkerImperium.Editor
{
    /// <summary>
    /// Editor-Menü, das die spielbare Runtime-Scene reproduzierbar erzeugt: Config-Assets (Idle + Game-Balancing),
    /// den <see cref="RuntimeGameController"/> und das Diagnose-<see cref="RuntimeHud"/>, alle Referenzen verdrahtet.
    /// Wegen der projektweiten <c>*.meta</c>-Gitignore-Policy wird die Scene <b>lokal generiert</b> (nicht versioniert) —
    /// dieser Builder ist das versionierte Artefakt.
    /// </summary>
    public static class RuntimeSceneBuilder
    {
        private const string Dir = "Assets/_Project/Scenes";
        private const string IdleAsset = Dir + "/Runtime_IdleBalancing.asset";
        private const string GameAsset = Dir + "/Runtime_GameBalancing.asset";
        private const string ScenePath = Dir + "/Runtime.unity";

        [MenuItem("HandwerkerImperium/Runtime/Build Runtime Scene")]
        public static void Build()
        {
            var idleConfig = LoadOrCreate<BalancingConfig>(IdleAsset);
            var gameConfig = LoadOrCreate<GameBalancingConfig>(GameAsset);
            SetRef(gameConfig, "idle", idleConfig);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            camGo.transform.position = new Vector3(0f, 1f, -10f);

            var ctrlGo = new GameObject("RuntimeGameController");
            var ctrl = ctrlGo.AddComponent<RuntimeGameController>();
            SetRef(ctrl, "config", gameConfig);
            var hud = ctrlGo.AddComponent<RuntimeHud>();
            SetRef(hud, "controller", ctrl);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[RuntimeSceneBuilder] Runtime scene built + wired: " + ScenePath);
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

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(target);
        }
    }
}
