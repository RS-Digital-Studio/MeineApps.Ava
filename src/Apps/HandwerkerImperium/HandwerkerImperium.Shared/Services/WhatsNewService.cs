using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Services;

/// <summary>
/// Zeigt Bestandsspielern beim Update einen kumulativen
/// Hinweis-Dialog mit den neuen Features seit ihrer letzten App-Version.
/// Vermeidet dass Reputation-Tiers, Notification-Bell, Strategy-EV, Imperium-
/// Sub-Tabs etc. ohne Erklaerung im UI auftauchen.
///
/// <para>
/// Pattern: Zwei sortierte Arrays (Versionen + Feature-RESX-Keys) - leichtgewichtig,
/// keine Map-Allokation, deterministisch traversierbar. Bei Versions-Bump in der
/// Hauptdatei <c>HandwerkerImperium.Shared.csproj</c> einfach einen neuen Eintrag
/// hinten anhaengen mit den 1-3 wichtigsten neuen Features (RESX-Keys, mehrsprachig).
/// </para>
/// </summary>
public sealed class WhatsNewService : IWhatsNewService
{
    private readonly IGameStateService _gameStateService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localization;

    /// <summary>
    /// Versionen, ab denen Feature-Keys ausgespielt werden. Aufsteigend sortiert.
    /// Eintraege werden in <see cref="ShowWhatsNewIfNeededAsync"/> gegen
    /// <see cref="SettingsData.LastWhatsNewVersion"/> abgeglichen.
    /// </summary>
    private static readonly (string Version, string[] FeatureKeys)[] s_releases =
    {
        ("2.0.36", ["WhatsNewBell", "WhatsNewStrategyEV", "WhatsNewReputation"]),
        ("2.0.37", ["WhatsNewReputationShop", "WhatsNewImperiumTabs", "WhatsNewWhatsNewItself"]),
        ("2.1.2", ["WhatsNewMinigameFlow", "WhatsNewBalancingPolish", "WhatsNewCraftingStability", "WhatsNewGuildHallBonuses", "WhatsNewEconomySaveFixes"]),
        // Eintrag fuer die aktuelle Version (2.1.3) — bei jeder funktionalen Aenderung
        // kumulativ erweitern, beim naechsten Release neuen Eintrag fuer die Folge-Version anhaengen.
        // WICHTIG: Jeder Key MUSS in den RESX (neutral + de/en/es/fr/it/pt) existieren — GetString gibt
        // bei Miss den ROHEN Key-Namen zurueck (kein Fallback-Helper), sonst zeigt der Dialog die Key-ID.
        ("2.1.3", ["WhatsNewGuildMultiplayerLive", "WhatsNewNoBannerAds", "WhatsNewPrestigeShopReset", "WhatsNewFairCostsAndMaterials", "WhatsNewSaveStabilityV213", "WhatsNewSmoothnessAndTouch"]),
    };

    public WhatsNewService(
        IGameStateService gameStateService,
        IDialogService dialogService,
        ILocalizationService localization)
    {
        _gameStateService = gameStateService;
        _dialogService = dialogService;
        _localization = localization;
    }

    /// <summary>
    /// Zeigt den "Was ist neu"-Dialog wenn der Spieler ein Update von einer aelteren
    /// Version installiert hat. No-op bei brandneuen Spielern (LastSavedAt == default)
    /// und bei aktuellen Spielern (LastWhatsNewVersion >= aktuelle App-Version).
    /// </summary>
    public Task ShowWhatsNewIfNeededAsync()
    {
        var settings = _gameStateService.Settings;
        var lastSeenStr = string.IsNullOrEmpty(settings.LastWhatsNewVersion)
            ? "0.0.0"
            : settings.LastWhatsNewVersion;

        var currentVersion = GetCurrentAppVersion();
        if (!Version.TryParse(currentVersion, out var current))
            return Task.CompletedTask;

        if (!Version.TryParse(lastSeenStr, out var lastSeen))
            lastSeen = new Version(0, 0, 0);

        // Nichts zu zeigen wenn der Spieler bereits aktuell ist.
        if (lastSeen >= current)
            return Task.CompletedTask;

        // Brandneue Spieler (kein Save vorhanden): nicht beim ersten Start mit
        // einem Was-ist-neu-Dialog erschlagen — sie kennen die alten Features sowieso nicht.
        // Wir merken uns die aktuelle Version trotzdem, damit zukuenftige Updates korrekt zeigen.
        var state = _gameStateService.State;
        if (state.LastSavedAt == default || state.Statistics.TotalOrdersCompleted == 0)
        {
            settings.LastWhatsNewVersion = currentVersion;
            return Task.CompletedTask;
        }

        // Liste der seit lastSeen hinzugekommenen Feature-Keys sammeln (kumulativ).
        var newKeys = new List<string>();
        for (int i = 0; i < s_releases.Length; i++)
        {
            if (!Version.TryParse(s_releases[i].Version, out var releaseVer)) continue;
            if (releaseVer > lastSeen)
                newKeys.AddRange(s_releases[i].FeatureKeys);
        }

        if (newKeys.Count == 0)
        {
            settings.LastWhatsNewVersion = currentVersion;
            return Task.CompletedTask;
        }

        // Body zusammenbauen — Bullet-Liste mit lokalisierten Feature-Texten.
        var bullet = "•"; // Bullet-Char (kein Emoji, ASCII-vertraeglich)
        var lines = new List<string>();
        for (int i = 0; i < newKeys.Count; i++)
        {
            var line = _localization.GetString(newKeys[i]);
            if (!string.IsNullOrEmpty(line))
                lines.Add($"{bullet} {line}");
        }

        if (lines.Count == 0)
        {
            settings.LastWhatsNewVersion = currentVersion;
            return Task.CompletedTask;
        }

        var title = _localization.GetString("WhatsNewTitle") ?? "What's new";
        var body = string.Join("\n", lines);
        var ok = _localization.GetString("Confirm") ?? "OK";

        _dialogService.ShowAlertDialog(title, body, ok);

        // Sofort persistieren — Dialog gilt als "gesehen" sobald er triggert,
        // auch wenn der Spieler noch nicht klickt. Verhindert Doppel-Anzeige bei Crash.
        settings.LastWhatsNewVersion = currentVersion;

        return Task.CompletedTask;
    }

    private static string GetCurrentAppVersion()
    {
        // Liest die <Version>-Property aus HandwerkerImperium.Shared.csproj. Bei Build-Pipelines
        // kann das Assembly auch eine Suffix-Komponente haben — wir ziehen nur die ersten 3 Felder.
        var version = typeof(WhatsNewService).Assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "2.0.0";
    }
}
