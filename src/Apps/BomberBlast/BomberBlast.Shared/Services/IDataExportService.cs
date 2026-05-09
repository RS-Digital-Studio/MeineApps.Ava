namespace BomberBlast.Services;

/// <summary>
/// DSGVO Art. 20 — Recht auf Datenuebertragbarkeit (Phase 25 — AAA-Audit Compliance).
///
/// <para>Liefert alle vom Spieler gespeicherten Daten als strukturiertes JSON-Dokument zur
/// Ausgabe an den Spieler. Im Gegensatz zu <see cref="IAccountDeletionService"/> (DSGVO Art. 17 —
/// Recht auf Vergessenwerden) wird hier nichts geloescht, nur ein Snapshot exportiert.</para>
///
/// <para>Pflicht laut DSGVO + EU Digital Services Act 2026 sobald die App Mikrotransaktionen
/// (Gem-Shop, Battle-Pass) und Account-Daten (PlayerName, Liga-Punkte, Fortschritt) verarbeitet.</para>
///
/// <para>Format: JSON. Spieler erhält Text-Block den er per Share-Sheet (Android) oder Clipboard
/// kopieren kann. Keine Server-Rundtrip nötig — alle Daten sind lokal.</para>
/// </summary>
public interface IDataExportService
{
    /// <summary>
    /// Sammelt alle Spieler-Daten und liefert sie als JSON-String. Strukturiert nach Domänen
    /// (Profil, Fortschritt, Liga, Battle-Pass, Achievements, Cosmetics, Einstellungen).
    /// </summary>
    Task<string> ExportAsJsonAsync();

    /// <summary>
    /// Liefert eine menschenlesbare Zusammenfassung (TEXT — kein JSON) für Spieler die
    /// nicht mit JSON umgehen können. Show in Settings → "Meine Daten anzeigen".
    /// </summary>
    Task<string> ExportAsHumanReadableAsync();
}
