#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.UI.Foundation;
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
                // Stufen 2-10 werden nach und nach befuellt — leere Map fuer Stufe 1
                // ist gewollt (Foundation steht, konkrete Screens kommen in Folgestufen).
            };

            UIInstaller.RegisterUI(builder, screens);
        }
    }
}
