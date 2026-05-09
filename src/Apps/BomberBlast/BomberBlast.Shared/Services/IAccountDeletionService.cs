using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// DSGVO Art. 17 Account-Löschung (Play-Store-Compliance seit 2023).
/// Cascading-Delete: Firebase League/Reports, Cloud-Save, Preferences, lokale DB.
/// </summary>
public interface IAccountDeletionService
{
    /// <summary>
    /// Löscht ALLE Spieler-Daten:
    /// 1. Firebase: Liga-Eintrag, Reports
    /// 2. Cloud-Save: Snapshot löschen (Google Play Games)
    /// 3. Preferences: Alle Schlüssel
    /// 4. Lokale DB: HighScores, Achievements
    /// Return true wenn vollständig erfolgreich.
    /// </summary>
    Task<AccountDeletionResult> DeleteAccountAsync();
}

public sealed record AccountDeletionResult(
    bool Success,
    bool FirebaseLeagueDeleted,
    bool CloudSaveDeleted,
    bool LocalDataDeleted,
    string? ErrorMessage);

/// <summary>
/// Standard-Implementierung. Best-Effort: Wenn ein Schritt fehlschlägt, geht der nächste trotzdem weiter.
/// Lokale Daten werden IMMER gelöscht (auch bei Firebase-/Cloud-Fehler), damit die App-State konsistent ist.
/// </summary>
public sealed class AccountDeletionService : IAccountDeletionService
{
    private readonly ILeagueService _leagueService;
    private readonly ICloudSaveService _cloudSaveService;
    private readonly IPlayGamesService _playGames;
    private readonly IPreferencesService _preferences;
    private readonly IProgressService _progressService;
    private readonly IHighScoreService _highScoreService;
    private readonly IAppLogger _logger;

    public AccountDeletionService(
        ILeagueService leagueService,
        ICloudSaveService cloudSaveService,
        IPlayGamesService playGames,
        IPreferencesService preferences,
        IProgressService progressService,
        IHighScoreService highScoreService,
        IAppLogger logger)
    {
        _leagueService = leagueService;
        _cloudSaveService = cloudSaveService;
        _playGames = playGames;
        _preferences = preferences;
        _progressService = progressService;
        _highScoreService = highScoreService;
        _logger = logger;
    }

    public async Task<AccountDeletionResult> DeleteAccountAsync()
    {
        bool firebaseOk = false;
        bool cloudOk = false;
        bool localOk = false;
        string? error = null;

        // v2.0.55 — Phase 15 P1-Fix: Local-First-Reihenfolge.
        // Vorher (Firebase → Cloud → Local) erlaubte Race: Wenn die App nach Schritt 1+2 gekillt wird,
        // bleiben lokale Daten bestehen + werden beim nächsten Start zurück in die Cloud gesynct
        // (Re-Anlegen der gelöschten Cloud-Daten). Lokal-First setzt direkt einen "deleted"-State —
        // bei Kill ist die App lokal sauber, Cloud-/Firebase-Reste sind harmlos und werden beim
        // nächsten Login re-trigggert.

        // 1. Lokale Daten ZUERST löschen (atomar via Preferences.Clear)
        try
        {
            _highScoreService.ClearScores();
            _progressService.ResetProgress();
            _preferences.Clear();
            localOk = true;
        }
        catch (Exception ex)
        {
            _logger.LogError("AccountDeletion: Local data delete failed", ex);
            error = $"Local: {ex.Message}";
        }

        // 2. Cloud-Save Snapshot löschen
        try
        {
            await _cloudSaveService.DeleteCloudSaveAsync();
            cloudOk = true;
        }
        catch (Exception ex)
        {
            _logger.LogError("AccountDeletion: Cloud-Save delete failed", ex);
            error ??= $"CloudSave: {ex.Message}";
        }

        // 3. Firebase-Liga-Eintrag löschen (zuletzt — bei Network-Fehler bleibt Firebase-Reste,
        //    aber lokal + Cloud sind sauber. User kann den Reste über Service-Account anfordern.)
        try
        {
            await _leagueService.DeleteOwnEntryAsync();
            firebaseOk = true;
        }
        catch (Exception ex)
        {
            _logger.LogError("AccountDeletion: Firebase delete failed", ex);
            error ??= $"Firebase: {ex.Message}";
        }

        return new AccountDeletionResult(
            firebaseOk && cloudOk && localOk,
            firebaseOk,
            cloudOk,
            localOk,
            error);
    }
}
