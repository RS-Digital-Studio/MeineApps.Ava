#nullable enable

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>
    /// Abstrahiert die Screen-Erstellung vom <see cref="ScreenManager"/>.
    /// Implementierung lebt im UI-Bootstrap-Layer und holt Screens via VContainer
    /// (damit Domain-Services per Constructor injiziert werden koennen).
    /// </summary>
    public interface IScreenFactory
    {
        /// <summary>Erzeugt einen Screen anhand seiner ID (siehe <see cref="ScreenId"/>).</summary>
        /// <exception cref="System.InvalidOperationException">Wenn kein Screen fuer die ID registriert ist.</exception>
        IScreen Create(string screenId);
    }
}
