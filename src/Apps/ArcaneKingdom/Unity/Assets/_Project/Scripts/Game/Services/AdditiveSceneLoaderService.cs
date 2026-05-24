#nullable enable
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace ArcaneKingdom.Game.Services
{
    /// <summary>
    /// Laedt Unity-Scenes additive. Boot bleibt immer geladen (siehe ARCHITECTURE.md Kap. 7).
    /// </summary>
    public sealed class AdditiveSceneLoaderService : ISceneLoaderService
    {
        private readonly HashSet<string> _loadedScenes = new();
        private readonly object _lock = new();

        public AdditiveSceneLoaderService()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                _loadedScenes.Add(SceneManager.GetSceneAt(i).name);
        }

        public async UniTask LoadAdditiveAsync(string sceneName, CancellationToken ct = default)
        {
            lock (_lock) { if (_loadedScenes.Contains(sceneName)) { GameLogger.Verbose("SceneLoader", $"'{sceneName}' bereits geladen — skip."); return; } }
            GameLogger.Info("SceneLoader", $"Lade additive: {sceneName}");
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op != null) await op.ToUniTask(cancellationToken: ct);
            lock (_lock) _loadedScenes.Add(sceneName);
        }

        public async UniTask UnloadAsync(string sceneName, CancellationToken ct = default)
        {
            lock (_lock) { if (!_loadedScenes.Contains(sceneName)) { GameLogger.Verbose("SceneLoader", $"'{sceneName}' nicht geladen — skip."); return; } }
            GameLogger.Info("SceneLoader", $"Entlade: {sceneName}");
            var op = SceneManager.UnloadSceneAsync(sceneName);
            if (op != null) await op.ToUniTask(cancellationToken: ct);
            lock (_lock) _loadedScenes.Remove(sceneName);
        }

        public async UniTask ReplaceAsync(string oldSceneName, string newSceneName, CancellationToken ct = default)
        {
            await LoadAdditiveAsync(newSceneName, ct);
            await UnloadAsync(oldSceneName, ct);
        }
    }
}
