using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Handles saving and loading game state to persistent storage.
/// </summary>
public interface ISaveGameService
{
    /// <summary>
    /// Wird bei Fehlern beim Speichern/Laden/Löschen/Importieren ausgelöst.
    /// Parameter: Titel, Nachricht.
    /// </summary>
    event Action<string, string>? ErrorOccurred;

    /// <summary>
    /// Whether a save file exists.
    /// </summary>
    bool SaveExists { get; }

    /// <summary>
    /// H-H09: True, wenn der letzte <see cref="LoadAsync"/>-Aufruf Save-Dateien vorfand,
    /// aber alle beschaedigt waren (Haupt- UND Backup-Datei) — Signal fuer einen
    /// Cloud-Recovery-Flow statt eines kommentarlosen CreateNew().
    /// </summary>
    bool LastLoadFailedCorrupt { get; }

    /// <summary>
    /// Path to the save file.
    /// </summary>
    string SaveFilePath { get; }

    /// <summary>
    /// Saves the current game state.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Loads the saved game state.
    /// Returns null if no save exists or loading fails.
    /// </summary>
    Task<GameState?> LoadAsync();

    /// <summary>
    /// Deletes the save file.
    /// </summary>
    Task DeleteSaveAsync();

    /// <summary>
    /// Exports the save data as a JSON string (for backup/sharing).
    /// </summary>
    Task<string> ExportSaveAsync();

    /// <summary>
    /// Imports save data from a JSON string.
    /// </summary>
    Task<bool> ImportSaveAsync(string json);
}
