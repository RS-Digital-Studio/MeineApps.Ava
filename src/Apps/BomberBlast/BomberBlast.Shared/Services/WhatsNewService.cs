using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation von <see cref="IWhatsNewService"/> (Sprint 4.3 AAA-Audit #17).
///
/// <para>
/// Eintraege sind hardcoded pro Version (Single-Source-of-Truth fuer den Release-Workflow).
/// Bei jedem Release wird dieser Service erweitert um den neuen Eintrag, dann VersionCode
/// erhoeht — beim naechsten App-Start sieht der User das Modal automatisch.
/// </para>
///
/// <para>
/// CurrentVersion wird aus dem Shared-Assembly gelesen (siehe Sprint 1.4a) — keine
/// Doppelung der Versionsangabe noetig. RESX-Texte fuer Lokalisierung.
/// </para>
/// </summary>
public sealed class WhatsNewService : IWhatsNewService
{
    private const string KeyLastSeenVersion = "WhatsNew_LastSeenVersion";

    private readonly IPreferencesService _prefs;
    private readonly ILocalizationService _localization;

    public WhatsNewService(IPreferencesService prefs, ILocalizationService localization)
    {
        _prefs = prefs;
        _localization = localization;
    }

    public string CurrentVersion
    {
        get
        {
            var v = typeof(WhatsNewService).Assembly.GetName().Version;
            return v?.ToString(3) ?? "0.0.0";
        }
    }

    public string LastSeenVersion => _prefs.Get(KeyLastSeenVersion, string.Empty);

    public bool ShouldShow
    {
        get
        {
            // Erstinstall: kein Modal anzeigen — der User soll erst mal das Spiel starten,
            // nicht direkt mit einem Changelog ueberlastet werden.
            if (string.IsNullOrEmpty(LastSeenVersion)) return false;
            // Version-Vergleich: nur wenn aktuelle > letzt-gesehene UND wir haben Eintraege
            if (CompareVersions(CurrentVersion, LastSeenVersion) <= 0) return false;
            return GetEntries().Count > 0;
        }
    }

    public IReadOnlyList<WhatsNewEntry> GetEntries()
    {
        // Eintraege fuer die aktuelle Version. Pro Release ein neuer if-Branch hinzufuegen
        // (Single-Source-of-Truth), VersionCode in Android-csproj auf das Major.Minor.Patch erhoehen.
        return CurrentVersion switch
        {
            "2.0.56" => new[]
            {
                new WhatsNewEntry
                {
                    Title = _localization.GetString("WhatsNew_2_0_56_Title")
                        ?? "Bigger, better, more polished",
                    Bullets = new[]
                    {
                        _localization.GetString("WhatsNew_2_0_56_BulletWorld") ?? "World-themed bombs and explosions — every world feels different now",
                        _localization.GetString("WhatsNew_2_0_56_BulletUltra") ?? "ULTRA-Combo (x10+) now triggers a full-screen flash — the iconic moment",
                        _localization.GetString("WhatsNew_2_0_56_BulletDamage") ?? "Damage-Flash + better player blink — feels respected, not punished",
                        _localization.GetString("WhatsNew_2_0_56_BulletAnticipation") ?? "Bomb-place + boss attacks have anticipation frames — Hades-style feel",
                        _localization.GetString("WhatsNew_2_0_56_BulletPush") ?? "Daily reminder notifications — never miss a daily reward again",
                    }
                }
            },
            _ => Array.Empty<WhatsNewEntry>()
        };
    }

    public void MarkSeen()
    {
        _prefs.Set(KeyLastSeenVersion, CurrentVersion);
    }

    /// <summary>
    /// Vergleicht zwei Versionsstrings im "X.Y.Z"-Format.
    /// Returns: 1 wenn a > b, -1 wenn a &lt; b, 0 wenn gleich (oder Parse-Fehler).
    /// </summary>
    internal static int CompareVersions(string a, string b)
    {
        if (System.Version.TryParse(a, out var va) && System.Version.TryParse(b, out var vb))
        {
            return va.CompareTo(vb);
        }
        return 0;
    }
}
