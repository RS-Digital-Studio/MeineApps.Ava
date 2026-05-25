#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.UI.Foundation;
using ArcaneKingdom.UI.Login;
using VContainer;

namespace ArcaneKingdom.UI.Bootstrap
{
    /// <summary>
    /// Zentrale Registrierung aller Screens. Wird vom <c>RootLifetimeScope</c>
    /// aufgerufen.
    ///
    /// Wenn ein neuer Screen hinzukommt:
    ///   1. Konkrete Screen-Klasse erstellen (erbt von <see cref="ScreenBase"/>)
    ///   2. Hier in der Map registrieren: [ScreenId.NewName] = typeof(NewScreen)
    ///   3. UXML unter Assets/_Project/Resources/UI/NewScreen.uxml ablegen
    /// </summary>
    public static class UIBootstrap
    {
        public static void RegisterAllScreens(IContainerBuilder builder)
        {
            var screens = new Dictionary<string, Type>
            {
                [ScreenId.Login] = typeof(LoginScreen),

                // Stufen 3-10 werden hier nach und nach befuellt:
                //   [ScreenId.Hub]         = typeof(HubScreen),
                //   [ScreenId.DeckBuilder] = typeof(DeckBuilderScreen),
                //   [ScreenId.WorldMap]    = typeof(WorldMapScreen),
                //   [ScreenId.Battle]      = typeof(BattleScreen),
                //   ...
            };

            UIInstaller.RegisterUI(builder, screens);
        }
    }
}
