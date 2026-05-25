#nullable enable
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.UI.Common;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace ArcaneKingdom.Bootstrap
{
    /// <summary>
    /// Wird einmalig nach dem DI-Container-Build aufgerufen. Setzt den initialen
    /// Screen (Login) und verdrahtet UI-Globals (z.B. Artwork-Service an die
    /// statische CardTileFactory).
    ///
    /// Wartet auf <see cref="UIRoot.IsReady"/> bevor der erste Screen gepusht wird —
    /// sonst werden UXML-var()-Variablen (z.B. background-color: var(--ak-bg-deep))
    /// gegen einen leeren StyleSheet-Tree aufgeloest -&gt; NullReferenceException
    /// im StyleVariableResolver.
    /// </summary>
    public sealed class BootEntryPoint : IAsyncStartable
    {
        private readonly ScreenManager _screenManager;
        private readonly CardArtworkService _artworkService;
        private readonly UIRoot _uiRoot;

        public BootEntryPoint(ScreenManager screenManager,
                              CardArtworkService artworkService,
                              UIRoot uiRoot)
        {
            _screenManager = screenManager;
            _artworkService = artworkService;
            _uiRoot = uiRoot;
        }

        public async UniTask StartAsync(System.Threading.CancellationToken ct)
        {
            // UI-Globals binden bevor erste View entsteht
            CardTileFactory.ArtworkService = _artworkService;

            // Warten bis UIRoot fertig ist (UIDocument.rootVisualElement vorhanden +
            // ScreenContainer + Theme-StyleSheet in Parent-Chain).
            // Max 2 Sekunden, sonst Warning + trotzdem starten.
            var waited = 0;
            while (!_uiRoot.IsReady && waited < 200)
            {
                await UniTask.Yield(ct);
                waited++;
            }
            if (!_uiRoot.IsReady)
                GameLogger.Warning("Boot",
                    "UIRoot nach 200 Frames nicht ready — starte Login trotzdem, " +
                    "Theme-Variablen werden vermutlich nicht aufgeloest.");

            GameLogger.Info("Boot", "ArcaneKingdom gestartet — Initial-Screen: Login.");
            await _screenManager.ReplaceAsync(ScreenId.Login, ct);
        }
    }
}
