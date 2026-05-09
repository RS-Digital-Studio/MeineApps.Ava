using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// AAA-Audit P1: House-Ads zwischen den 11 eigenen Apps. Zero-Cost, statisch konfiguriert.
///
/// Architektur:
/// - Static Catalog der 11 Apps (Hardcoded — Apps wechseln nur sehr selten, RemoteConfig-
///   Override koennten wir spaeter via IRemoteConfigService nachschieben).
/// - Tagesrotation: Day-of-Year % AppCount = aktuelle Auswahl. Rotiert deterministisch
///   ohne Client-State, ohne Banner-Blindness und ohne Server-Roundtrip.
/// - HandwerkerImperium-Eintrag wird beim Get gefiltert.
/// </summary>
public sealed class CrossPromoService : ICrossPromoService
{
    private readonly IAnalyticsService? _analytics;

    private const string SelfPackageId = "com.meineapps.handwerkerimperium";

    /// <summary>
    /// Statischer Katalog der 11 Apps. Reihenfolge stabil — beeinflusst die Tagesrotation.
    /// </summary>
    // Icon-Kinds sind aus GameIconKind.cs ausgewaehlt (existieren garantiert).
    private static readonly CrossPromoApp[] s_catalog =
    [
        new() { Id = "rechnerplus",       NameKey = "CrossPromo_RechnerPlus_Name",       HookKey = "CrossPromo_RechnerPlus_Hook",       IconKind = "Cash",            AccentColor = "#7C7FF7", PackageId = "com.meineapps.rechnerplus" },
        new() { Id = "zeitmanager",       NameKey = "CrossPromo_ZeitManager_Name",       HookKey = "CrossPromo_ZeitManager_Hook",       IconKind = "TimerOutline",    AccentColor = "#F7A833", PackageId = "com.meineapps.zeitmanager" },
        new() { Id = "finanzrechner",     NameKey = "CrossPromo_FinanzRechner_Name",     HookKey = "CrossPromo_FinanzRechner_Hook",     IconKind = "CashMultiple",    AccentColor = "#10B981", PackageId = "com.meineapps.finanzrechner" },
        new() { Id = "fitnessrechner",    NameKey = "CrossPromo_FitnessRechner_Name",    HookKey = "CrossPromo_FitnessRechner_Hook",    IconKind = "Dumbbell",        AccentColor = "#06B6D4", PackageId = "com.meineapps.fitnessrechner" },
        new() { Id = "handwerkerrechner", NameKey = "CrossPromo_HandwerkerRechner_Name", HookKey = "CrossPromo_HandwerkerRechner_Hook", IconKind = "HammerWrench",    AccentColor = "#3B82F6", PackageId = "com.meineapps.handwerkerrechner" },
        new() { Id = "worktimepro",       NameKey = "CrossPromo_WorkTimePro_Name",       HookKey = "CrossPromo_WorkTimePro_Hook",       IconKind = "Cog",             AccentColor = "#4F8BF9", PackageId = "com.meineapps.worktimepro" },
        new() { Id = "handwerkerimperium",NameKey = "CrossPromo_HandwerkerImperium_Name",HookKey = "CrossPromo_HandwerkerImperium_Hook",IconKind = "Hammer",          AccentColor = "#D97706", PackageId = SelfPackageId },
        new() { Id = "bomberblast",       NameKey = "CrossPromo_BomberBlast_Name",       HookKey = "CrossPromo_BomberBlast_Hook",       IconKind = "RocketLaunch",    AccentColor = "#FF6B35", PackageId = "com.meineapps.bomberblast" },
        new() { Id = "rebornsaga",        NameKey = "CrossPromo_RebornSaga_Name",        HookKey = "CrossPromo_RebornSaga_Hook",        IconKind = "Sword",           AccentColor = "#4A90D9", PackageId = "com.meineapps.rebornsaga" },
        new() { Id = "bingxbot",          NameKey = "CrossPromo_BingXBot_Name",          HookKey = "CrossPromo_BingXBot_Hook",          IconKind = "FlaskOutline",    AccentColor = "#3B82F6", PackageId = "com.meineapps.bingxbot" },
        new() { Id = "gardencontrol",     NameKey = "CrossPromo_GardenControl_Name",     HookKey = "CrossPromo_GardenControl_Hook",     IconKind = "ShieldHalfFull",  AccentColor = "#2E7D32", PackageId = "com.meineapps.gardencontrol" },
    ];

    public CrossPromoService(IAnalyticsService? analytics = null)
    {
        _analytics = analytics;
    }

    public IReadOnlyList<CrossPromoApp> GetAvailable()
    {
        // HandwerkerImperium aus dem Katalog ausfiltern — wir bewerben uns nicht selbst.
        return s_catalog.Where(a => a.PackageId != SelfPackageId).ToList();
    }

    public IReadOnlyList<CrossPromoApp> GetCurrentRotation(int count = 1)
    {
        var available = GetAvailable();
        if (available.Count == 0 || count <= 0) return [];

        // Tages-Rotation: Day-of-Year als Hash. Liefert deterministisch denselben Output
        // pro Tag, ohne Persistenz noetig.
        var dayOfYear = DateTime.UtcNow.DayOfYear;
        var startIdx = dayOfYear % available.Count;
        var result = new List<CrossPromoApp>(count);
        for (int i = 0; i < count && i < available.Count; i++)
            result.Add(available[(startIdx + i) % available.Count]);
        return result;
    }

    public void TrackClick(CrossPromoApp app)
    {
        _analytics?.TrackEvent("cross_promo_click", new Dictionary<string, object?>
        {
            ["target_app"] = app.Id,
            ["target_package"] = app.PackageId,
        });
    }
}
