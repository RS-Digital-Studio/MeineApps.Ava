#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using UnityEngine;

namespace ArcaneKingdom.Game.Localization
{
    /// <summary>
    /// Localization-Service der eine einzelne CSV-Datei aus Resources laedt.
    /// Format:  Key,DE,EN,ES,FR,IT,PT
    ///
    /// Die aktive Sprache wird in PlayerPrefs gespeichert ("ak.lang"). Beim Ändern
    /// feuert <see cref="LanguageChanged"/>.
    /// </summary>
    public sealed class CsvLocalizationService : ILocalizationService
    {
        private const string ResourcePath = "Localization/strings";
        private const string PrefsKey = "ak.lang";
        private const string DefaultLanguage = "DE";

        private static readonly string[] AllLanguages = { "DE", "EN", "ES", "FR", "IT", "PT" };

        private readonly Dictionary<string, Dictionary<string, string>> _byLanguage = new();
        private string _currentLanguage = DefaultLanguage;
        private int _columnIndex;

        public event Action? LanguageChanged;

        public string CurrentLanguage => _currentLanguage;
        public IReadOnlyList<string> SupportedLanguages => AllLanguages;

        public CsvLocalizationService()
        {
            LoadFromResources();
            var saved = PlayerPrefs.GetString(PrefsKey, "");
            var initial = !string.IsNullOrEmpty(saved) ? saved : DetectSystemLanguage();
            SetLanguageInternal(initial);
        }

        public string Get(string key, string? fallback = null)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;
            if (_byLanguage.TryGetValue(_currentLanguage, out var dict)
                && dict.TryGetValue(key, out var value)
                && !string.IsNullOrEmpty(value))
            {
                return value;
            }
            // Fallback auf DE
            if (_currentLanguage != DefaultLanguage
                && _byLanguage.TryGetValue(DefaultLanguage, out var deDict)
                && deDict.TryGetValue(key, out var deValue)
                && !string.IsNullOrEmpty(deValue))
            {
                return deValue;
            }
            return fallback ?? key;
        }

        public string GetFormatted(string key, params object[] args)
        {
            var template = Get(key);
            try { return string.Format(template, args); }
            catch (FormatException)
            {
                GameLogger.Warning("Localization", $"Format-Fehler bei Key '{key}'.");
                return template;
            }
        }

        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode)) return;
            if (!AllLanguages.Contains(languageCode))
            {
                GameLogger.Warning("Localization", $"Sprache '{languageCode}' nicht unterstuetzt — bleibe bei {_currentLanguage}.");
                return;
            }
            SetLanguageInternal(languageCode);
            PlayerPrefs.SetString(PrefsKey, languageCode);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke();
        }

        private void SetLanguageInternal(string lang)
        {
            _currentLanguage = lang;
            GameLogger.Info("Localization", $"Aktive Sprache: {_currentLanguage}");
        }

        private void LoadFromResources()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                GameLogger.Warning("Localization", $"Resources/{ResourcePath}.csv nicht gefunden.");
                return;
            }

            using var reader = new System.IO.StringReader(asset.text);
            var header = reader.ReadLine();
            if (header == null) return;
            var columns = SplitCsvLine(header);

            // Pro Sprache Dictionary anlegen
            foreach (var lang in AllLanguages)
                _byLanguage[lang] = new Dictionary<string, string>();

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = SplitCsvLine(line);
                if (parts.Count == 0) continue;
                var key = parts[0];

                for (var i = 1; i < parts.Count && i < columns.Count; i++)
                {
                    var lang = columns[i].Trim();
                    if (!_byLanguage.TryGetValue(lang, out var dict)) continue;
                    var value = parts[i].Trim();
                    if (!string.IsNullOrEmpty(value)) dict[key] = value;
                }
            }

            var loaded = string.Join(", ",
                AllLanguages.Select(l => $"{l}:{(_byLanguage[l]?.Count ?? 0)}"));
            GameLogger.Info("Localization", $"Strings geladen ({loaded}).");
        }

        /// <summary>
        /// Sehr simple CSV-Aufteilung — splittet bei Kommas. Kommas IN Zellen müssen
        /// mit doppelten Anfuehrungszeichen escape'd werden.
        /// </summary>
        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(c);
            }
            result.Add(current.ToString());
            return result;
        }

        private static string DetectSystemLanguage()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.English  => "EN",
                SystemLanguage.Spanish  => "ES",
                SystemLanguage.French   => "FR",
                SystemLanguage.Italian  => "IT",
                SystemLanguage.Portuguese => "PT",
                _                        => "DE"
            };
        }
    }
}
