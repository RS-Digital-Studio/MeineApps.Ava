using MeineApps.Core.Ava.Services;
using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services.Anker;

/// <summary>
/// Liest/schreibt die Anker-Zugangsdaten in den <see cref="IPreferencesService"/> (App-Sandbox).
/// Einziger Ort, der die Pref-Keys kennt — VM und Monitor-Service nutzen ausschließlich diesen Store.
/// Privates Tool → Speicherung im App-privaten Preferences-JSON (kein Play-Store-Vertrieb).
/// </summary>
public static class AnkerCredentialStore
{
    private const string KeyEmail = "anker.email";
    private const string KeyPassword = "anker.password";
    private const string KeyCountry = "anker.country";

    public static bool Has(IPreferencesService prefs) =>
        !string.IsNullOrWhiteSpace(prefs.Get(KeyEmail, "")) &&
        !string.IsNullOrWhiteSpace(prefs.Get(KeyPassword, ""));

    public static AnkerCredentials? Load(IPreferencesService prefs)
    {
        var email = prefs.Get(KeyEmail, "");
        var password = prefs.Get(KeyPassword, "");
        var country = prefs.Get(KeyCountry, "DE");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return null;
        return new AnkerCredentials(email.Trim(), password, string.IsNullOrWhiteSpace(country) ? "DE" : country.Trim().ToUpperInvariant());
    }

    public static void Save(IPreferencesService prefs, AnkerCredentials credentials)
    {
        prefs.Set(KeyEmail, credentials.Email.Trim());
        prefs.Set(KeyPassword, credentials.Password);
        prefs.Set(KeyCountry, credentials.CountryId.Trim().ToUpperInvariant());
    }

    public static void Clear(IPreferencesService prefs)
    {
        prefs.Set(KeyEmail, "");
        prefs.Set(KeyPassword, "");
    }
}
