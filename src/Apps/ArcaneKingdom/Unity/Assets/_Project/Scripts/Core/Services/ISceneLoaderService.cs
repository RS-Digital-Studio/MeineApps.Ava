#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Core.Services
{
    /// <summary>
    /// Lade-/Entlade-Service fuer Unity-Scenes. Bevorzugt additive Loads
    /// (Boot bleibt immer geladen, andere Scenes kommen daruef).
    /// </summary>
    public interface ISceneLoaderService
    {
        UniTask LoadAdditiveAsync(string sceneName, CancellationToken ct = default);
        UniTask UnloadAsync(string sceneName, CancellationToken ct = default);
        UniTask ReplaceAsync(string oldSceneName, string newSceneName, CancellationToken ct = default);
    }
}
