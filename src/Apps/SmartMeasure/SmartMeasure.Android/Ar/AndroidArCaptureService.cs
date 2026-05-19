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
/// Startet ArCaptureActivity (ARCore) und gibt das Ergebnis zurück.
///
/// Fehler-Behandlung: Abbrüche werden mit einem Error-String im TCS-Result
/// getransport (<see cref="CaptureAsync"/> liefert null UND der Caller kann
/// <see cref="LastError"/> abfragen für UX-Feedback). Die vorherige Variante
/// gab still null zurück und User wusste nicht warum.
/// </summary>
public sealed class AndroidArCaptureService : IArCaptureService
{
    private readonly Activity _activity;
    private readonly object _tcsLock = new();
    private TaskCompletionSource<ArCaptureResult?>? _tcs;

    private const int CAMERA_PERMISSION_CODE = 9010;

    /// <summary>Letzter Fehler-Grund falls CaptureAsync null zurückgibt.</summary>
    public string? LastError { get; private set; }

    public AndroidArCaptureService(Activity activity)
    {
        _activity = activity;
    }

    public Task<bool> IsAvailableAsync()
    {
        try
        {
            var availability = Google.AR.Core.ArCoreApk.Instance!
                .CheckAvailability(_activity);
            var availabilityName = availability?.ToString() ?? string.Empty;
            // SUPPORTED_INSTALLED — alles ok, AR-Session kann sofort starten.
            // SUPPORTED_NOT_INSTALLED / SUPPORTED_APK_TOO_OLD — Gerät ist kompatibel, ARCore
            // muss aber aus dem Play Store installiert/aktualisiert werden. Plan Kap. 4.14:
            // statt still false zu liefern, beim CaptureAsync den Install-Flow anstoßen.
            // Hier melden wir trotzdem true zurück — UI bietet AR-Modus an, der Install-Flow
            // läuft beim ersten StartCaptureActivity (siehe StartCaptureActivity).
            var isSupported = availabilityName.Contains("SUPPORTED");
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
        // Laufende Capture-Session als abgebrochen markieren
        TaskCompletionSource<ArCaptureResult?> newTcs;
        lock (_tcsLock)
        {
            if (_tcs != null && !_tcs.Task.IsCompleted)
            {
                LastError = "Vorherige AR-Session wurde abgebrochen";
                _tcs.TrySetResult(null);
            }

            newTcs = new TaskCompletionSource<ArCaptureResult?>();
            _tcs = newTcs;
            LastError = null;
        }

        try
        {
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
            CompleteWithError($"AR-Session konnte nicht gestartet werden: {ex.Message}");
        }

        return newTcs.Task;
    }

    private void StartCaptureActivity()
    {
        try
        {
            // Plan 4.14: Prüfe ob ARCore-APK installiert ist, sonst Play-Store-Flow auslösen.
            var apk = Google.AR.Core.ArCoreApk.Instance;
            if (apk != null)
            {
                var availability = apk.CheckAvailability(_activity)?.ToString() ?? string.Empty;
                if (availability.Contains("NOT_INSTALLED") || availability.Contains("TOO_OLD"))
                {
                    try
                    {
                        // RequestInstall(activity, userRequestedInstall=true) öffnet Play-Store.
                        var status = apk.RequestInstall(_activity, true)?.ToString() ?? string.Empty;
                        if (status == "InstallRequested")
                        {
                            // Activity wird re-launched nachdem Install fertig ist; CaptureAsync
                            // muss vom User dann erneut getriggert werden.
                            CompleteWithError("ARCore wird im Play Store installiert. Bitte AR-Modus danach neu starten.");
                            return;
                        }
                        // Installed → weitermachen
                    }
                    catch (Exception installEx)
                    {
                        global::Android.Util.Log.Warn("ArCaptureService",
                            $"ARCore Install-Request fehlgeschlagen: {installEx.Message}");
                    }
                }
            }

            var intent = new Intent(_activity, typeof(ArCaptureActivity));
            _activity.StartActivityForResult(intent, ArCaptureActivity.REQUEST_CODE);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("ArCaptureService", $"StartCaptureActivity Fehler: {ex}");
            CompleteWithError($"AR-Activity konnte nicht gestartet werden: {ex.Message}");
        }
    }

    /// <summary>Wird von MainActivity.OnActivityResult aufgerufen.</summary>
    public void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != ArCaptureActivity.REQUEST_CODE) return;

        if (resultCode == Result.Ok)
        {
            // Result auch ohne data akzeptieren — ArCaptureActivity nutzt ConsumeLastResult-Static-Bridge
            var result = ArCaptureActivity.ConsumeLastResult();
            CompleteWithResult(result);
        }
        else
        {
            // Result.Canceled ist User-Abbruch (Back-Button), nicht Fehler
            CompleteWithError(resultCode == Result.Canceled ? null : "AR-Capture wurde nicht erfolgreich beendet");
        }
    }

    /// <summary>Wird von MainActivity.OnRequestPermissionsResult aufgerufen.</summary>
    public void HandlePermissionResult(int requestCode, Permission[] grantResults)
    {
        if (requestCode != CAMERA_PERMISSION_CODE) return;

        var allGranted = grantResults.Length > 0 && grantResults.All(r => r == Permission.Granted);
        if (!allGranted)
        {
            CompleteWithError("Kamera- und/oder Standort-Berechtigung verweigert");
            return;
        }

        // Kleiner Delay für das System (Camera-Subsystem braucht Zeit nach Permission-Grant)
        _activity.Window?.DecorView?.PostDelayed(() =>
        {
            try
            {
                if (_activity.IsFinishing || _activity.IsDestroyed)
                {
                    CompleteWithError("Activity wurde beendet bevor AR starten konnte");
                    return;
                }
                StartCaptureActivity();
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error("ArCaptureService",
                    $"Activity-Start nach Permission fehlgeschlagen: {ex}");
                CompleteWithError($"AR-Start nach Berechtigung fehlgeschlagen: {ex.Message}");
            }
        }, 500);
    }

    private void CompleteWithResult(ArCaptureResult? result)
    {
        lock (_tcsLock)
        {
            _tcs?.TrySetResult(result);
        }
    }

    private void CompleteWithError(string? error)
    {
        lock (_tcsLock)
        {
            if (error != null) LastError = error;
            _tcs?.TrySetResult(null);
        }
    }
}
