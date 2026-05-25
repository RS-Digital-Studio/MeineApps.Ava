#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Core.Services
{
    /// <summary>
    /// Liefert lokalisierte Strings + verwaltet die aktuelle Sprache.
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>Aktuelle Sprache (z.B. "DE", "EN").</summary>
        string CurrentLanguage { get; }

        /// <summary>Liste aller unterstuetzten Sprachen.</summary>
        IReadOnlyList<string> SupportedLanguages { get; }

        /// <summary>
        /// Liefert den uebersetzten String fuer den Key in der aktuellen Sprache.
        /// Wenn Key fehlt: fallback auf <paramref name="fallback"/> oder Key selbst.
        /// </summary>
        string Get(string key, string? fallback = null);

        /// <summary>
        /// Wie <see cref="Get(string, string?)"/>, aber mit Formatierung
        /// (String.Format-Argumente).
        /// </summary>
        string GetFormatted(string key, params object[] args);

        /// <summary>Aendert die aktive Sprache und feuert <see cref="LanguageChanged"/>.</summary>
        void SetLanguage(string languageCode);

        /// <summary>Wird gefeuert sobald sich die Sprache geaendert hat.</summary>
        event Action? LanguageChanged;
    }
}
