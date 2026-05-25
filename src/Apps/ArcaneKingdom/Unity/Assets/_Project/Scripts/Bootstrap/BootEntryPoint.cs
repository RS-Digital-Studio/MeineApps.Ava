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
    /// </summary>
    public sealed class BootEntryPoint : IStartable
    {
        private readonly ScreenManager _screenManager;
        private readonly CardArtworkService _artworkService;

        public BootEntryPoint(ScreenManager screenManager,
                              CardArtworkService artworkService)
        {
            _screenManager = screenManager;
            _artworkService = artworkService;
        }

        public void Start()
        {
            // UI-Globals binden bevor erste View entsteht
            CardTileFactory.ArtworkService = _artworkService;

            GameLogger.Info("Boot", "ArcaneKingdom gestartet — Initial-Screen: Login.");
            // ReplaceAsync feuert OnEnterAsync des Login-Screens, der dann den
            // LoginController triggert und Status-Updates an den User zeigt.
            _screenManager.ReplaceAsync(ScreenId.Login).Forget();
        }
    }
}
