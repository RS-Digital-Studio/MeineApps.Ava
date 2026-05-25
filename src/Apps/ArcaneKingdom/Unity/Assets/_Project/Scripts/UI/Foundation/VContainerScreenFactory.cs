#nullable enable
using System;
using System.Collections.Generic;
using VContainer;

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>
    /// IScreenFactory-Implementierung die Screens via VContainer-Resolution erzeugt.
    /// Jeder Screen muss vorher per <see cref="UIInstaller.RegisterScreens"/> registriert
    /// werden — sowohl im DI-Container als auch in der internen ID-&gt;Type-Map.
    /// </summary>
    public sealed class VContainerScreenFactory : IScreenFactory
    {
        private readonly IObjectResolver _resolver;
        private readonly IReadOnlyDictionary<string, Type> _idToType;

        public VContainerScreenFactory(IObjectResolver resolver,
                                       IReadOnlyDictionary<string, Type> idToType)
        {
            _resolver = resolver;
            _idToType = idToType;
        }

        public IScreen Create(string screenId)
        {
            if (!_idToType.TryGetValue(screenId, out var type))
                throw new InvalidOperationException(
                    $"Kein Screen registriert fuer ID '{screenId}'. Pruefe UIInstaller.RegisterScreens.");

            if (_resolver.Resolve(type) is not IScreen screen)
                throw new InvalidOperationException(
                    $"Type '{type.Name}' fuer Screen-ID '{screenId}' ist kein IScreen.");

            return screen;
        }
    }
}
