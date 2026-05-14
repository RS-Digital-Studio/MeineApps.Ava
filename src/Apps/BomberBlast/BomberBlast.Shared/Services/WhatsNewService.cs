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
            "2.0.58" => new[]
            {
                new WhatsNewEntry
                {
                    Title = _localization.GetString("WhatsNew_2_0_58_Title")
                        ?? "Mini-Bosses, Phase-2 Attacks & Hero Powers",
                    Bullets = new[]
                    {
                        _localization.GetString("WhatsNew_2_0_58_BulletMiniBoss") ?? "Mini-Bosses zwischen den Welten — alle 10 Level taucht ein Trainings-Boss mit halber HP auf",
                        _localization.GetString("WhatsNew_2_0_58_BulletPhase2") ?? "Boss-Phase-2 — wenn Bosse enraged sind, schalten sie auf staerkere Attack-Patterns um (mehr Bloecke, doppelte Reihen, Schatten-Klone)",
                        _localization.GetString("WhatsNew_2_0_58_BulletHero") ?? "Hero-Traits wirken jetzt — TwinTina's Bomben explodieren doppelt, SpeedySam ist immun gegen Slow-Curse, BrickBoris findet mehr PowerUps",
                        _localization.GetString("WhatsNew_2_0_58_BulletStory") ?? "World-Story-Beats jetzt cinematischer — 6-7s Lesezeit + dezenter Pull-Back-Effekt",
                    }
                }
            },
            "2.0.57" => new[]
            {
                new WhatsNewEntry
                {
                    Title = _localization.GetString("WhatsNew_2_0_57_Title")
                        ?? "Live-Ops, Polish & Pacing",
                    Bullets = new[]
                    {
                        _localization.GetString("WhatsNew_2_0_57_BulletStory") ?? "Mini-Story-Beats — every world starts with a 2-line intro, every boss-clear has a cliffhanger outro",
                        _localization.GetString("WhatsNew_2_0_57_BulletBoss") ?? "Boss-Modifier complete — Reflective spiegelt 30% Schaden zurueck, Shielded blockt Hits, Burning hinterlaesst Lava-Spuren",
                        _localization.GetString("WhatsNew_2_0_57_BulletElite") ?? "Elite-Gegner ab Welt 3 — lila pulsierender Glow, mehr HP, mehr Punkte",
                        _localization.GetString("WhatsNew_2_0_57_BulletTutorial") ?? "Tutorial in 3 geschuetzte Phasen aufgeteilt — Movement, Bomben, Power-Ups jetzt mit Resume-Punkt",
                        _localization.GetString("WhatsNew_2_0_57_BulletAudio") ?? "Musik konsistent gemastered (-16 LUFS, EBU R128) — keine Lautstaerke-Spruenge zwischen Welten",
                        _localization.GetString("WhatsNew_2_0_57_BulletOutline") ?? "Outline-Pass auf allen Entities — Sprites + AI-Assets sehen jetzt aus einem Guss aus",
                        _localization.GetString("WhatsNew_2_0_57_BulletHero") ?? "Hero-System aktiv — Coin- und PowerUp-Multiplier wirken im Spiel",
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
