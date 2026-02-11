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
public class AndroidBarcodeService : Services.IBarcodeService
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
        _tcs = new TaskCompletionSource<string?>();

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

        return _tcs.Task;
    }

    private void StartScannerActivity()
    {
        var intent = new Intent(_activity, typeof(BarcodeScannerActivity));
        _activity.StartActivityForResult(intent, BarcodeScannerActivity.REQUEST_CODE);
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
    /// Wird von MainActivity.OnRequestPermissionsResult aufgerufen
    /// </summary>
    public void HandlePermissionResult(int requestCode, Permission[] grantResults)
    {
        if (requestCode != CAMERA_PERMISSION_CODE) return;

        if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
        {
            StartScannerActivity();
        }
        else
        {
            // Permission verweigert → null zurueckgeben (Desktop-Fallback in View)
            _tcs?.TrySetResult(null);
        }
    }
}
