using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// (08.05.2026, Foundation): Daily-Bundle-Rotation.
///
/// Lifecycle:
/// 1. <see cref="InitializeAsync"/> beim App-Start — RemoteConfig laden, aktuelles Bundle berechnen
/// 2. Tick-basiert: <see cref="GetCurrentBundle"/> wird alle 60s vom GameLoop abgefragt
/// 3. Um 00:00 UTC rotiert das Bundle automatisch (DayOfWeek-Index)
/// 4. <see cref="PurchaseCurrentBundleAsync"/> startet IAP-Flow + verbucht Bonus-Items
///
/// Diese Foundation hat noch keine UI — die <c>ShopViewModel</c>-Integration kommt in
/// einem späteren Sprint, wenn Robert die SKUs in der Google Play Console angelegt hat.
/// </summary>
public interface IDailyBundleService
{
    /// <summary>Lädt RemoteConfig + aktuelles Bundle.</summary>
    System.Threading.Tasks.Task InitializeAsync();

    /// <summary>Aktuelles Tages-Bundle. <c>null</c> wenn deaktiviert oder kein Slot konfiguriert.</summary>
    DailyBundleOffer? GetCurrentBundle();

    /// <summary>Wird gefeuert wenn um 00:00 UTC rotiert wurde.</summary>
    event System.Action? BundleRotated;

    /// <summary>True wenn Daily-Bundle-Feature global aktiv ist (siehe <see cref="RemoteConfigKeys.DailyBundleEnabled"/>).</summary>
    bool IsEnabled { get; }

    /// <summary>Startet IAP-Flow für das aktuelle Bundle. Bonus-Items werden bei Erfolg verbucht.</summary>
    System.Threading.Tasks.Task<bool> PurchaseCurrentBundleAsync();
}
