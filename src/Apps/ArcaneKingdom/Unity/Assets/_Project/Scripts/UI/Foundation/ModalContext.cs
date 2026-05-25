#nullable enable

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>
    /// Container fuer Daten die einem Modal/Overlay-Screen uebergeben werden.
    /// Wird im DI als Singleton registriert — Caller setzt vor Push, Screen liest in OnEnter.
    ///
    /// Pattern:
    /// <code>
    ///   _modalContext.Set("card", cardDefinition);
    ///   await _screenManager.PushAsync(ScreenId.CardDetailOverlay);
    ///
    ///   // Im Modal-Screen:
    ///   var card = _modalContext.Get&lt;CardDefinition&gt;("card");
    /// </code>
    /// </summary>
    public sealed class ModalContext
    {
        private readonly System.Collections.Generic.Dictionary<string, object?> _bag = new();

        public void Set(string key, object? value) => _bag[key] = value;

        public T? Get<T>(string key) where T : class
            => _bag.TryGetValue(key, out var v) ? v as T : null;

        public void Clear() => _bag.Clear();

        public void Remove(string key) => _bag.Remove(key);
    }
}
