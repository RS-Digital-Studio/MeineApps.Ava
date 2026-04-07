using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Android-Implementation des IArCaptureService.
/// Startet ArCaptureActivity (ARCore) und gibt das Ergebnis zurueck.
/// Pattern: analog zu AndroidBarcodeService (FitnessRechner).
/// </summary>
public sealed class AndroidArCaptureService : IArCaptureService
{
    private readonly Activity _activity;
    private TaskCompletionSource<ArCaptureResult?>? _tcs;

    private const int CAMERA_PERMISSION_CODE = 9010;

    public AndroidArCaptureService(Activity activity)
    {
        _activity = activity;
    }

    public Task<bool> IsAvailableAsync()
    {
        try
        {
            // ARCore-Verfuegbarkeit pruefen
            var availability = Google.AR.Core.ArCoreApk.Instance!
                .CheckAvailability(_activity);

            // SUPPORTED_INSTALLED oder SUPPORTED_NOT_INSTALLED (kann installiert werden)
            var isSupported = availability?.ToString()?.Contains("SUPPORTED") == true;
            return Task.FromResult(isSupported);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("ArCaptureService", $"IsAvailableAsync Fehler: {ex}");
            return Task.FromResult(false);
        }
    }

    public Task<ArCaptureResult?> CaptureAsync()
    {
        // Laufende Capture-Session abbrechen falls vorhanden
        _tcs?.TrySetResult(null);
        _tcs = new TaskCompletionSource<ArCaptureResult?>();

        try
        {
            // Kamera + GPS Permissions pruefen (GPS fuer Georeferenzierung)
            var needCamera = ContextCompat.CheckSelfPermission(_activity, Manifest.Permission.Camera)
                != Permission.Granted;
            var needLocation = ContextCompat.CheckSelfPermission(_activity, Manifest.Permission.AccessFineLocation)
                != Permission.Granted;

            if (needCamera || needLocation)
            {
                var permissions = new List<string>();
                if (needCamera) permissions.Add(Manifest.Permission.Camera);
                if (needLocation) permissions.Add(Manifest.Permission.AccessFineLocation);

                ActivityCompat.RequestPermissions(
                    _activity,
                    permissions.ToArray(),
                    CAMERA_PERMISSION_CODE);
            }
            else
            {
                StartCaptureActivity();
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("ArCaptureService", $"CaptureAsync Fehler: {ex}");
            _tcs.TrySetResult(null);
        }

        return _tcs.Task;
    }

    private void StartCaptureActivity()
    {
        try
        {
            var intent = new Intent(_activity, typeof(ArCaptureActivity));
            _activity.StartActivityForResult(intent, ArCaptureActivity.REQUEST_CODE);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("ArCaptureService", $"StartCaptureActivity Fehler: {ex}");
            _tcs?.TrySetResult(null);
        }
    }

    /// <summary>Wird von MainActivity.OnActivityResult aufgerufen</summary>
    public void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != ArCaptureActivity.REQUEST_CODE) return;

        if (resultCode == Result.Ok && data != null)
        {
            var result = ArCaptureActivity.ConsumeLastResult();
            _tcs?.TrySetResult(result);
        }
        else
        {
            _tcs?.TrySetResult(null);
        }
    }

    /// <summary>Wird von MainActivity.OnRequestPermissionsResult aufgerufen</summary>
    public void HandlePermissionResult(int requestCode, Permission[] grantResults)
    {
        if (requestCode != CAMERA_PERMISSION_CODE) return;

        if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
        {
            // Delay nach Permission-Grant (System braucht Zeit fuer Kamera-Aktivierung)
            _activity.Window?.DecorView?.PostDelayed(() =>
            {
                try
                {
                    if (_activity.IsFinishing || _activity.IsDestroyed)
                    {
                        _tcs?.TrySetResult(null);
                        return;
                    }
                    StartCaptureActivity();
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Error("ArCaptureService",
                        $"Activity-Start nach Permission fehlgeschlagen: {ex}");
                    _tcs?.TrySetResult(null);
                }
            }, 500);
        }
        else
        {
            _tcs?.TrySetResult(null);
        }
    }
}
