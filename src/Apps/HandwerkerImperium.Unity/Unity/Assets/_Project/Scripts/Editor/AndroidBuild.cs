using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HandwerkerImperium.Editor
{
    /// <summary>
    /// Android-Build-Durchstich (P3-Vorbereitung, Beta-App-ID laut CLAUDE.md §1):
    /// IL2CPP + ARM64 (Play-Store-Baseline), Szene = lokal generierte Game.unity
    /// (vorher Build Game Scene (3D) ausführen). Output unter <c>Builds/Android/</c>
    /// (lokal, git-ignored). Release-Signing/AAB folgt in P4 — der Durchstich beweist,
    /// dass Manifest, IL2CPP, glTFast, Input System und UI Toolkit auf Android bauen.
    /// </summary>
    public static class AndroidBuild
    {
        private const string AppId = "com.meineapps.handwerkerimperium2.beta";
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string OutputPath = "Builds/Android/HandwerkerImperium-Durchstich.apk";

        [MenuItem("HandwerkerImperium/Build/Android APK (Durchstich)")]
        public static void BuildApk()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("[AndroidBuild] Game.unity fehlt — erst 'Build Game Scene (3D)' ausführen.");
                return;
            }

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25; // Unity-6-Minimum
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
            // Größen-Hebel (Durchstich 1 war 536 MB): ASTC statt unkomprimierter Texturen + Engine-Stripping
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[AndroidBuild] Ergebnis: {report.summary.result} | Fehler: {report.summary.totalErrors} | " +
                      $"Dauer: {report.summary.totalTime} | Output: {report.summary.outputPath}");
        }
    }
}
