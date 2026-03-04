using System.Diagnostics;

namespace MeineApps.Core.Ava.Services;

/// <summary>
/// Desktop-Implementierung: Oeffnet Dateien mit der Standard-App.
/// </summary>
public sealed class DesktopFileShareService : IFileShareService
{
    public Task<bool> ShareFileAsync(string filePath, string title, string mimeType)
    {
        if (!File.Exists(filePath))
            return Task.FromResult(false);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
            return Task.FromResult(true);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }

    public string GetExportDirectory(string appName)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            appName);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
