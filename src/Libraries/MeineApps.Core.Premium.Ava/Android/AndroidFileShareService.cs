using Android.App;
using Android.Content;
using MeineApps.Core.Ava.Services;

namespace MeineApps.Core.Premium.Ava.Droid;

/// <summary>
/// Android-Implementierung: Share-Intent mit FileProvider.
/// Dieses File wird via Compile Include in die Android-Projekte eingebunden.
/// Es wird NICHT als Teil der net10.0 Library kompiliert.
/// </summary>
public sealed class AndroidFileShareService : IFileShareService
{
    private readonly Activity _activity;

    public AndroidFileShareService(Activity activity)
    {
        _activity = activity;
    }

    public Task<bool> ShareFileAsync(string filePath, string title, string mimeType)
    {
        if (!System.IO.File.Exists(filePath))
            return Task.FromResult(false);

        try
        {
            var file = new Java.IO.File(filePath);

            // FileProvider fuer Scoped Storage (API 24+)
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                _activity,
                _activity.PackageName + ".fileprovider",
                file);

            var intent = new Intent(Intent.ActionSend);
            intent.SetType(mimeType);
            intent.PutExtra(Intent.ExtraStream, uri);
            intent.PutExtra(Intent.ExtraSubject, title);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);

            _activity.StartActivity(Intent.CreateChooser(intent, title));
            return Task.FromResult(true);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }

    public string GetExportDirectory(string appName)
    {
        // App-spezifisches externes Verzeichnis (kein Permission noetig)
        var externalDir = _activity.GetExternalFilesDir(null)?.AbsolutePath;
        var dir = !string.IsNullOrEmpty(externalDir)
            ? System.IO.Path.Combine(externalDir, "exports")
            : System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                appName, "exports");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
