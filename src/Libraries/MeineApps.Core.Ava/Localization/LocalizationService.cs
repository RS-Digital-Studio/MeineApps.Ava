using System.Globalization;
using System.Resources;
using MeineApps.Core.Ava.Services;

namespace MeineApps.Core.Ava.Localization;

/// <summary>
/// Localization service backed by ResX ResourceManager.
/// Each app creates its own instance with its ResourceManager.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private const string LanguageKey = "selected_language";

    private readonly ResourceManager _resourceManager;
    private readonly IPreferencesService _preferences;
    private CultureInfo _currentCulture;

    public string CurrentLanguage => _currentCulture.TwoLetterISOLanguageName;
    public CultureInfo CurrentCulture => _currentCulture;

    public event EventHandler? LanguageChanged;

    public IReadOnlyList<LanguageInfo> AvailableLanguages { get; } =
    [
        new("en", "English", "English"),
        new("de", "Deutsch", "German"),
        new("es", "Espa\u00f1ol", "Spanish"),
        new("fr", "Fran\u00e7ais", "French"),
        new("it", "Italiano", "Italian"),
        new("pt", "Portugu\u00eas", "Portuguese")
    ];

    public LocalizationService(ResourceManager resourceManager, IPreferencesService preferences)
    {
        _resourceManager = resourceManager;
        _preferences = preferences;
        _currentCulture = CultureInfo.CurrentUICulture;
    }

    public void Initialize()
    {
        try
        {
            var saved = _preferences.Get(LanguageKey, string.Empty);

            if (!string.IsNullOrEmpty(saved))
            {
                SetCultureInternal(saved);
            }
            else
            {
                // Gerätesprache erkennen und sofort in Preferences speichern
                var deviceLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var lang = AvailableLanguages.Any(l => l.Code == deviceLang) ? deviceLang : "en";
                SetCultureInternal(lang);
                _preferences.Set(LanguageKey, lang);
            }
        }
        catch
        {
            SetCultureInternal("en");
        }
    }

    public void SetLanguage(string languageCode)
    {
        if (_currentCulture.TwoLetterISOLanguageName == languageCode) return;

        SetCultureInternal(languageCode);
        _preferences.Set(LanguageKey, languageCode);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key)
    {
        try
        {
            return _resourceManager.GetString(key, _currentCulture) ?? key;
        }
        catch
        {
            return key;
        }
    }

    private void SetCultureInternal(string languageCode)
    {
        _currentCulture = new CultureInfo(languageCode);
        CultureInfo.CurrentCulture = _currentCulture;
        CultureInfo.CurrentUICulture = _currentCulture;
    }
}
