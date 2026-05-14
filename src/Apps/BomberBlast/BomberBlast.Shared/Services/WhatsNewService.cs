using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// "What's New"-Modal. Eintraege werden pro entwickelter Version kumulativ gesammelt
/// (auch fuer noch nicht released Zwischenversionen). Beim Update sieht der Spieler
/// ALLE Eintraege seit seiner zuletzt installierten Version, nicht nur die der
/// aktuellen Version. Damit gehen lange Develop-Phasen ohne Store-Release nicht verloren.
///
/// <para>
/// Workflow: Bei jedem Versions-Bump in <c>BomberBlast.Android.csproj</c> einen neuen
/// Eintrag in <see cref="BuildReleases"/> anhaengen. Beim eigentlichen Store-Release
/// passiert nichts Zusaetzliches — die installierte Version triggert den Modal-Diff.
/// </para>
///
/// <para>
/// CurrentVersion wird aus dem Shared-Assembly gelesen. RESX-Texte fuer Lokalisierung.
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

    /// <summary>
    /// Releases sortiert aufsteigend nach Version. Pro Versions-Bump in
    /// <c>BomberBlast.Android.csproj</c> einfach einen Eintrag hinten anhaengen —
    /// auch fuer noch nicht released Zwischenversionen. Eintraege bleiben kumulativ
    /// erhalten und werden beim spaeteren Store-Release allesamt dem Spieler angezeigt.
    /// </summary>
    private (string Version, Func<WhatsNewEntry> EntryFactory)[] BuildReleases() =>
    [
        ("2.0.57", () => new WhatsNewEntry
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
        }),
        ("2.0.58", () => new WhatsNewEntry
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
        }),
    ];

    public IReadOnlyList<WhatsNewEntry> GetEntries()
    {
        var lastSeen = LastSeenVersion;
        var current = CurrentVersion;
        var releases = BuildReleases();

        // Sammelt alle Eintraege deren Version > lastSeen UND <= current ist.
        // Bei Erstinstall (lastSeen leer) zaehlt das System als komplett neu — der
        // ShouldShow-Pfad blockt das eh, also wird die Liste nie ausgespielt. Wir liefern
        // sie hier trotzdem korrekt, damit Tests + Debug-Tooling den Stand inspizieren koennen.
        var result = new List<WhatsNewEntry>(releases.Length);
        foreach (var (version, factory) in releases)
        {
            if (!string.IsNullOrEmpty(lastSeen) && CompareVersions(version, lastSeen) <= 0)
                continue;
            if (CompareVersions(version, current) > 0)
                continue;
            result.Add(factory());
        }
        return result;
    }

    public void MarkSeen()
    {
        _prefs.Set(KeyLastSeenVersion, CurrentVersion);
    }

    /// <summary>
    /// Vergleicht zwei Versionsstrings im "X.Y.Z"-Format.
    /// Returns: 1 wenn a &gt; b, -1 wenn a &lt; b, 0 wenn gleich (oder Parse-Fehler).
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
