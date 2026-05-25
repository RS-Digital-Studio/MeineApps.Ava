#nullable enable
using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ArcaneKingdom.Core.Utility
{
    /// <summary>
    /// Zentrales Logging mit Tag-Präfix. Production-Builds können über Verbosity-Filter ans
    /// Backend (Firebase, Sentry) angebunden werden.
    /// </summary>
    public static class GameLogger
    {
        public enum LogLevel { Verbose, Info, Warning, Error }

        public static LogLevel MinimumLevel { get; set; } =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogLevel.Verbose;
#else
            LogLevel.Info;
#endif

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Verbose(string tag, string message)
        {
            if (MinimumLevel <= LogLevel.Verbose)
                Debug.Log($"[{tag}] {message}");
        }

        public static void Info(string tag, string message)
        {
            if (MinimumLevel <= LogLevel.Info)
                Debug.Log($"[{tag}] {message}");
        }

        public static void Warning(string tag, string message)
        {
            if (MinimumLevel <= LogLevel.Warning)
                Debug.LogWarning($"[{tag}] {message}");
        }

        public static void Error(string tag, string message, Exception? exception = null)
        {
            if (exception != null)
                Debug.LogError($"[{tag}] {message}\n{exception}");
            else
                Debug.LogError($"[{tag}] {message}");
        }
    }
}
