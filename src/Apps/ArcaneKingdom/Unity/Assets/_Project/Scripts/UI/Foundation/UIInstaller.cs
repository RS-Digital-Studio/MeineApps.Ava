#nullable enable
using System;
using System.Collections.Generic;
using VContainer;

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>
    /// Zentrale UI-DI-Registrierung. Wird vom <c>RootLifetimeScope</c> aufgerufen
    /// nachdem <see cref="UIRoot"/> via RegisterComponent registriert wurde.
    ///
    /// Hier kommen NEUE Screens hin: <c>builder.Register&lt;LoginScreen&gt;(...)</c>
    /// und in der idToType-Map als <c>[ScreenId.Login] = typeof(LoginScreen)</c>.
    /// </summary>
    public static class UIInstaller
    {
        /// <summary>
        /// Registriert UI-Foundation (ScreenManager, Factory, ToastService) + alle
        /// konkreten Screens. <paramref name="screenRegistrations"/> bestimmt welche
        /// Screens die App kennt.
        /// </summary>
        public static void RegisterUI(
            IContainerBuilder builder,
            IReadOnlyDictionary<string, Type> screenRegistrations)
        {
            // Foundation
            builder.Register<ToastService>(Lifetime.Singleton);

            // Screens als Transient registrieren — pro Push neue Instanz waere theoretisch
            // moeglich, aber ScreenManager cached gebaute Screens (built-cache). Singleton
            // wuerde Lifetime-Ueberlapp ergeben, deswegen Transient.
            foreach (var (_, type) in screenRegistrations)
                builder.Register(type, Lifetime.Transient).AsSelf();

            // ScreenFactory + ScreenManager kommen als Singletons. Beide brauchen
            // Argumente, die zur Build-Zeit aufgeloest werden.
            builder.Register<IScreenFactory>(resolver =>
                new VContainerScreenFactory(resolver, screenRegistrations), Lifetime.Singleton);

            builder.Register<ScreenManager>(resolver =>
            {
                var ui = resolver.Resolve<UIRoot>();
                var factory = resolver.Resolve<IScreenFactory>();
                return new ScreenManager(ui.ScreenContainer, factory);
            }, Lifetime.Singleton);
        }
    }
}
