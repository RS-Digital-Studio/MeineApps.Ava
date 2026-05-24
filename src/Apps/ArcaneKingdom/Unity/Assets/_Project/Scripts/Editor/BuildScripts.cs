#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArcaneKingdom.EditorTools
{
    /// <summary>
    /// CI/CD Build-Skripte fuer Android (AAB) und Desktop-Test-Builds.
    /// Aufruf via Unity CLI:
    /// <code>
    /// Unity.exe -batchmode -quit -nographics `
    ///   -projectPath . `
    ///   -executeMethod ArcaneKingdom.EditorTools.BuildScripts.BuildAndroidRelease `
    ///   -logFile build.log
    /// </code>
    /// </summary>
    public static class BuildScripts
    {
        private const string OutputDirectory = "Build";

        [MenuItem("ArcaneKingdom/Build/Android Release (AAB)")]
        public static void BuildAndroidRelease()
        {
            EnsureOutputDirectory();
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Android, 2 /* ARM64 */);
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            var outputPath = Path.Combine(OutputDirectory, $"arcanekingdom-{PlayerSettings.bundleVersion}.aab");

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            ReportBuild(report, outputPath);
        }

        [MenuItem("ArcaneKingdom/Build/Windows Test")]
        public static void BuildWindowsTest()
        {
            EnsureOutputDirectory();
            var outputPath = Path.Combine(OutputDirectory, "Windows", "ArcaneKingdom.exe");
            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            ReportBuild(report, outputPath);
        }

        private static string[] GetEnabledScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            var result = new System.Collections.Generic.List<string>(scenes.Length);
            foreach (var s in scenes)
            {
                if (s.enabled) result.Add(s.path);
            }
            return result.ToArray();
        }

        private static void EnsureOutputDirectory()
        {
            if (!Directory.Exists(OutputDirectory))
                Directory.CreateDirectory(OutputDirectory);
        }

        private static void ReportBuild(BuildReport report, string outputPath)
        {
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] Erfolgreich: {outputPath}  ({summary.totalSize / 1024 / 1024} MB, {summary.totalTime.TotalSeconds:F1}s)");
                if (Environment.GetCommandLineArgs() is { Length: > 0 })
                    EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[Build] Fehlgeschlagen: {summary.result}  ({summary.totalErrors} Fehler)");
                if (Environment.GetCommandLineArgs() is { Length: > 0 })
                    EditorApplication.Exit(1);
            }
        }
    }
}
