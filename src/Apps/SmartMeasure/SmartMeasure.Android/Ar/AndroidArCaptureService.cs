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
/// Fehler-Behandlung: Plan Kap. 4.3 — User-Abbruch wird per
/// <see cref="LastCompletionStatus"/> = <see cref="ArCaptureCompletionStatus.UserCancelled"/>
/// signalisiert, echte Fehler per <see cref="ArCaptureCompletionStatus.Error"/> +
/// Klartext in <see cref="LastError"/>. Vorher waren beide Fälle null und der
/// UI-Layer konnte nicht differenzieren.
/// </summary>
public sealed class AndroidArCaptureService : IArCaptureService
{
    // Mutable + volatile: Der Service lebt als DI-Singleton ueber MainActivity-Recreates
    // (Split-Screen, Schriftgroesse, Locale) hinweg — die Activity-Referenz wird bei jedem
    // MainActivity.OnCreate via AttachActivity aktualisiert. Vorher band der Ctor die ERSTE
    // Instanz dauerhaft: nach einem Recreate landeten OnActivityResult-Ergebnisse in einer
    // zweiten Service-Instanz, das awaitende CaptureAsync der alten kehrte nie zurueck
    // (IsArBusy dauerhaft true, AR-Button tot).
    private volatile Activity _activity;
    private readonly object _tcsLock = new();
    private TaskCompletionSource<ArCaptureResult?>? _tcs;

    private const int CAMERA_PERMISSION_CODE = 9010;

    /// <summary>Letzter Fehler-Grund falls CaptureAsync null zurückgibt und
    /// <see cref="LastCompletionStatus"/> = <see cref="ArCaptureCompletionStatus.Error"/>.
    /// Bei User-Cancel null.</summary>
    public string? LastError { get; private set; }

    /// <summary>Plan Kap. 4.3: Status der letzten Capture-Operation.</summary>
    public ArCaptureCompletionStatus LastCompletionStatus { get; private set; }

    /// <summary>Plan Kap. 5.2: Delegiert an die statische Bruecke in
    /// <see cref="ArCaptureActivity.SetSitePoints"/>.</summary>
    public void SetSitePoints(IReadOnlyList<SurveyPoint>? points)
        => ArCaptureActivity.SetSitePoints(points);

    /// <summary>Vorlade-Punkte (Geo-unabhaengig, "Lage relativ") — delegiert an die statische
    /// Bruecke in <see cref="ArCaptureActivity.SetPreloadPoints"/>.</summary>
    public void SetPreloadPoints(IReadOnlyList<SurveyPoint>? points)
        => ArCaptureActivity.SetPreloadPoints(points);

    public AndroidArCaptureService(Activity activity)
    {
        _activity = activity;
    }

    /// <summary>Aktualisiert die Activity-Referenz nach einem MainActivity-Recreate —
    /// laufende TCS/Status bleiben erhalten, neue Intents nutzen die lebende Activity.</summary>
    public void AttachActivity(Activity activity) => _activity = activity;

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            // CheckAvailability kann beim allerersten Aufruf transient sein
            // (UNKNOWN_CHECKING/UNKNOWN_TIMED_OUT — asynchrone Erst-Abfrage). Kurz nachfragen
            // statt faelschlich "nicht verfuegbar" zu melden. IsSupported statt String-Match:
            // "UNSUPPORTED_DEVICE_NOT_CAPABLE".Contains("SUPPORTED") war true — inkompatible
            // Geraete galten als AR-faehig.
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var availability = Google.AR.Core.ArCoreApk.Instance!
                    .CheckAvailability(_activity);
                if (availability == null) return false;
                if (!availability.IsTransient)
                {
                    // SUPPORTED_NOT_INSTALLED / SUPPORTED_APK_TOO_OLD melden wir als true —
                    // der Install-Flow laeuft beim ersten StartCaptureActivity (Plan Kap. 4.14).
                    return availability.IsSupported;
                }
                await Task.Delay(250);
            }
            return false;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("ArCaptureService", $"IsAvailableAsync Fehler: {ex}");
            return false;
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
                LastCompletionStatus = ArCaptureCompletionStatus.Error;
                _tcs.TrySetResult(null);
            }

            newTcs = new TaskCompletionSource<ArCaptureResult?>();
            _tcs = newTcs;
            LastError = null;
            LastCompletionStatus = ArCaptureCompletionStatus.None;
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
            // Objekt-Vergleiche statt ToString-Matching: die Java-Enum-Wrapper liefern
            // SCREAMING_SNAKE-Namen ("INSTALL_REQUESTED"), ein Vergleich gegen "InstallRequested"
            // war NIE wahr — Play-Store-Flow und AR-Activity starteten dann doppelt.
            var apk = Google.AR.Core.ArCoreApk.Instance;
            if (apk != null)
            {
                var availability = apk.CheckAvailability(_activity);
                var needsInstall = availability != null
                    && availability.IsSupported
                    && availability != Google.AR.Core.ArCoreApk.Availability.SupportedInstalled;
                if (needsInstall)
                {
                    try
                    {
                        // RequestInstall(activity, userRequestedInstall=true) öffnet Play-Store.
                        var status = apk.RequestInstall(_activity, true);
                        if (status == Google.AR.Core.ArCoreApk.InstallStatus.InstallRequested)
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
        else if (resultCode == Result.Canceled)
        {
            // Plan Kap. 4.3: User-Abbruch ist KEIN Fehler — eigener Status, kein LastError.
            CompleteCancelled();
        }
        else
        {
            CompleteWithError("AR-Capture wurde nicht erfolgreich beendet");
        }
    }

    /// <summary>Wird von MainActivity.OnRequestPermissionsResult aufgerufen.
    /// Nur CAMERA ist harte Voraussetzung — die AR-Activity degradiert ohne Standort sauber
    /// (Relativ-Messung ohne Geo-Referenz, VPS-Fallback deaktiviert sich selbst). Vorher
    /// blockierte eine verweigerte Standort-Berechtigung den AR-Start komplett.</summary>
    public void HandlePermissionResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (requestCode != CAMERA_PERMISSION_CODE) return;

        var cameraGranted = false;
        for (var i = 0; i < permissions.Length && i < grantResults.Length; i++)
        {
            if (permissions[i] == Manifest.Permission.Camera)
                cameraGranted = grantResults[i] == Permission.Granted;
        }

        if (!cameraGranted)
        {
            // Ab Android 11 fuehrt zweimaliges Ablehnen zu automatischem Deny ohne Dialog —
            // Rationale=false bedeutet dann "dauerhaft verweigert": App-Einstellungen oeffnen,
            // sonst gibt es fuer den Nutzer keinen Ausweg aus der Fehlermeldung.
            var permanentlyDenied = !ActivityCompat.ShouldShowRequestPermissionRationale(
                _activity, Manifest.Permission.Camera);
            if (permanentlyDenied)
            {
                CompleteWithError("Kamera-Berechtigung dauerhaft verweigert — bitte in den App-Einstellungen erlauben");
                try
                {
                    var settingsIntent = new Intent(global::Android.Provider.Settings.ActionApplicationDetailsSettings,
                        global::Android.Net.Uri.Parse($"package:{_activity.PackageName}"));
                    _activity.StartActivity(settingsIntent);
                }
                catch { /* Settings-Deeplink optional */ }
            }
            else
            {
                CompleteWithError("Kamera-Berechtigung verweigert — AR braucht die Kamera");
            }
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
            LastCompletionStatus = result != null
                ? ArCaptureCompletionStatus.Success
                : ArCaptureCompletionStatus.UserCancelled;
            _tcs?.TrySetResult(result);
        }
    }

    private void CompleteWithError(string? error)
    {
        lock (_tcsLock)
        {
            if (error != null) LastError = error;
            LastCompletionStatus = ArCaptureCompletionStatus.Error;
            _tcs?.TrySetResult(null);
        }
    }

    private void CompleteCancelled()
    {
        lock (_tcsLock)
        {
            LastError = null;
            LastCompletionStatus = ArCaptureCompletionStatus.UserCancelled;
            _tcs?.TrySetResult(null);
        }
    }
}
