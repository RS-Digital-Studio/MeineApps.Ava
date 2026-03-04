using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace FitnessRechner.Android;

/// <summary>
/// Android-Implementation des IBarcodeService.
/// Startet BarcodeScannerActivity (CameraX + ML Kit) und gibt das Ergebnis zurueck.
/// </summary>
public sealed class AndroidBarcodeService : Services.IBarcodeService
{
    private readonly Activity _activity;
    private TaskCompletionSource<string?>? _tcs;

    private const int CAMERA_PERMISSION_CODE = 9002;

    public AndroidBarcodeService(Activity activity)
    {
        _activity = activity;
    }

    public Task<string?> ScanBarcodeAsync()
    {
        // Laufenden Scan abbrechen falls vorhanden
        _tcs?.TrySetResult(null);
        _tcs = new TaskCompletionSource<string?>();

        try
        {
            // Kamera-Permission pruefen
            if (ContextCompat.CheckSelfPermission(_activity, Manifest.Permission.Camera)
                != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(
                    _activity,
                    [Manifest.Permission.Camera],
                    CAMERA_PERMISSION_CODE);
            }
            else
            {
                StartScannerActivity();
            }
        }
        catch (Exception ex)
        {
            // Fehler beim Permission-Check oder Activity-Start → sicher abbrechen
            global::Android.Util.Log.Error("BarcodeService", $"ScanBarcodeAsync Fehler: {ex}");
            _tcs.TrySetResult(null);
        }

        return _tcs.Task;
    }

    private void StartScannerActivity()
    {
        try
        {
            var intent = new Intent(_activity, typeof(BarcodeScannerActivity));
            _activity.StartActivityForResult(intent, BarcodeScannerActivity.REQUEST_CODE);
        }
        catch (Exception ex)
        {
            // Activity-Start fehlgeschlagen → null zurueckgeben
            global::Android.Util.Log.Error("BarcodeService", $"StartScannerActivity Fehler: {ex}");
            _tcs?.TrySetResult(null);
        }
    }

    /// <summary>
    /// Wird von MainActivity.OnActivityResult aufgerufen
    /// </summary>
    public void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != BarcodeScannerActivity.REQUEST_CODE) return;

        if (resultCode == Result.Ok && data != null)
        {
            var barcode = data.GetStringExtra(BarcodeScannerActivity.EXTRA_BARCODE);
            _tcs?.TrySetResult(barcode);
        }
        else
        {
            _tcs?.TrySetResult(null);
        }
    }

    /// <summary>
    /// Wird von MainActivity.OnRequestPermissionsResult aufgerufen.
    /// Nach Permission-Grant wird die Scanner-Activity mit kurzem Delay gestartet,
    /// um sicherzustellen dass das System die Permission vollstaendig verarbeitet hat.
    /// </summary>
    public void HandlePermissionResult(int requestCode, Permission[] grantResults)
    {
        if (requestCode != CAMERA_PERMISSION_CODE) return;

        if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
        {
            // Delay nach Permission-Grant: Das System braucht einen Moment
            // um die Kamera-Permission zu aktivieren. Ohne Delay kann CameraX crashen.
            // 500ms statt 300ms - auf aelteren Geraeten braucht Permission-Aktivierung laenger.
            _activity.Window?.DecorView?.PostDelayed(() =>
            {
                try
                {
                    // Prüfen ob Activity noch aktiv ist
                    if (_activity.IsFinishing || _activity.IsDestroyed)
                    {
                        _tcs?.TrySetResult(null);
                        return;
                    }
                    StartScannerActivity();
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Error("BarcodeService",
                        $"Scanner-Start nach Permission fehlgeschlagen: {ex}");
                    _tcs?.TrySetResult(null);
                }
            }, 500);
        }
        else
        {
            // Permission verweigert → null zurueckgeben (Desktop-Fallback in View)
            _tcs?.TrySetResult(null);
        }
    }
}
