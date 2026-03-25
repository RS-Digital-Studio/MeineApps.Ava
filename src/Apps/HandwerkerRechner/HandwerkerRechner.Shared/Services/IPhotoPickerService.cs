namespace HandwerkerRechner.Services;

/// <summary>
/// Plattform-spezifischer Photo-Picker (Desktop: StorageProvider, Android: Intent).
/// </summary>
public interface IPhotoPickerService
{
    /// <summary>Öffnet Foto-Auswahl, kopiert Datei ins AppData, gibt Pfad zurück (null = abgebrochen)</summary>
    Task<string?> PickPhotoAsync();

    /// <summary>Löscht ein Foto aus dem AppData-Verzeichnis</summary>
    Task DeletePhotoAsync(string photoPath);
}
