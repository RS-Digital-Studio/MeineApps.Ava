using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace HandwerkerRechner.Services;

/// <summary>
/// Desktop-Implementierung des Photo-Pickers über Avalonia StorageProvider.
/// Kopiert ausgewählte Bilder in ein lokales AppData-Verzeichnis mit GUID-Dateinamen.
/// </summary>
public sealed class DesktopPhotoPickerService : IPhotoPickerService
{
    private static readonly string PhotoDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MeineApps", "HandwerkerRechner", "photos");

    public async Task<string?> PickPhotoAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Foto auswählen",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Bilder")
                {
                    Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp"]
                }
            ]
        });

        if (files.Count == 0) return null;

        // Zielverzeichnis sicherstellen
        Directory.CreateDirectory(PhotoDirectory);

        // Datei mit GUID-Namen kopieren
        var sourceFile = files[0];
        var extension = Path.GetExtension(sourceFile.Name) ?? ".jpg";
        var targetName = $"{Guid.NewGuid()}{extension}";
        var targetPath = Path.Combine(PhotoDirectory, targetName);

        await using var sourceStream = await sourceFile.OpenReadAsync();
        await using var targetStream = File.Create(targetPath);
        await sourceStream.CopyToAsync(targetStream);

        return targetPath;
    }

    public Task DeletePhotoAsync(string photoPath)
    {
        try
        {
            // Path-Traversal-Schutz: Nur Dateien innerhalb von PhotoDirectory löschen.
            // Verhindert dass manipulierte projects.json (z.B. via Backup-Restore von gerooteten Geräten)
            // Pfade wie "../../etc/passwd" enthält und die App beliebige Files in ihrer Sandbox löscht.
            if (!IsPathInPhotoDirectory(photoPath))
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] Foto-Pfad außerhalb PhotoDirectory abgelehnt: {photoPath}");
#endif
                return Task.CompletedTask;
            }

            if (File.Exists(photoPath))
                File.Delete(photoPath);
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] Foto löschen fehlgeschlagen: {ex.Message}");
#endif
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Prüft ob ein Pfad sicher innerhalb des erwarteten PhotoDirectory liegt
    /// (canonical-form-Vergleich nach Auflösung von '..'-Segmenten und Symlinks).
    /// </summary>
    private static bool IsPathInPhotoDirectory(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullDir = Path.GetFullPath(PhotoDirectory);
            // Trailing-Separator damit "/photos2" nicht "/photos" matcht
            if (!fullDir.EndsWith(Path.DirectorySeparatorChar))
                fullDir += Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Ermittelt das TopLevel-Fenster für den StorageProvider-Zugriff</summary>
    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            return TopLevel.GetTopLevel(singleView.MainView);
        return null;
    }
}
