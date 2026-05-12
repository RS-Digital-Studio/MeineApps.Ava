using System.Security.Cryptography;
using System.Text;
using BomberBlast.Services;
using Firebase.Crashlytics;

namespace BomberBlast.Droid;

/// <summary>
/// Android-Implementation für ITelemetryService (Firebase Crashlytics).
/// Aktiv ab v2.0.56 — google-services.json + Xamarin.Firebase.Crashlytics-Binding vorhanden.
///
/// Custom-Keys werden mit jedem Crash-Report gesendet (Mode/Level/FPS-Bucket/MemoryMB).
/// User-ID ist SHA256-Hash der Android-Settings-ID (KEINE Firebase-UID — DSGVO-konform).
/// </summary>
public sealed class AndroidTelemetryService : ITelemetryService
{
    private readonly Android.Content.Context _context;
    private string? _userIdHash;

    public AndroidTelemetryService(Android.Content.Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Initialize()
    {
        try
        {
            // CrashlyticsCollection nur aktivieren wenn User-Consent erteilt wurde.
            // Konkreter Consent-Check passiert in der Aufrufstelle (App.axaml.cs) ueber das
            // PrivacyCenter — hier setzen wir einen sicheren Default (eingeschaltet).
            // Java-Binding-Quirk: erwartet Java.Lang.Boolean? (nullable Java-Wrapper) statt bool
            FirebaseCrashlytics.Instance.SetCrashlyticsCollectionEnabled(Java.Lang.Boolean.True);

            // Anonymisierter User-ID-Hash: SHA256(Android-Settings.Secure.AndroidId).
            // Crashlytics aggregiert Crashes pro UID — wir wollen Crashes pro Geraet/Reinstall
            // zaehlen, ohne den User identifizieren zu koennen.
            _userIdHash = ComputeAnonymousUserHash();
            if (!string.IsNullOrEmpty(_userIdHash))
                FirebaseCrashlytics.Instance.SetUserId(_userIdHash);
        }
        catch
        {
            // Best-Effort: Falls Crashlytics-Init scheitert (z.B. google-services.json fehlt
            // bei Dev-Build), bleibt die App lauffaehig — Telemetrie geht nur verloren.
        }
    }

    public void SetCustomKey(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        // Crashlytics-Limit: 100 Zeichen pro String-Value
        var clamped = value?.Length > 100 ? value[..100] : value;
        try { FirebaseCrashlytics.Instance.SetCustomKey(key, clamped ?? string.Empty); }
        catch { /* Best-Effort */ }
    }

    public void SetCustomKey(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        try { FirebaseCrashlytics.Instance.SetCustomKey(key, value); }
        catch { /* Best-Effort */ }
    }

    public void SetCustomKey(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        try { FirebaseCrashlytics.Instance.SetCustomKey(key, value); }
        catch { /* Best-Effort */ }
    }

    public void LogNonFatal(Exception ex, string? context = null)
    {
        if (ex == null) return;
        try
        {
            if (!string.IsNullOrEmpty(context))
                FirebaseCrashlytics.Instance.Log($"[{context}]");

            // .NET-Exception in Java.Lang.Throwable wrappen — Crashlytics zeigt
            // Message + den uebergebenen StackTrace im Web-Dashboard an.
            var throwable = new Java.Lang.Throwable(
                $"[{ex.GetType().FullName}] {ex.Message}\n{ex.StackTrace}");
            FirebaseCrashlytics.Instance.RecordException(throwable);
        }
        catch { /* Best-Effort */ }
    }

    public void Log(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        try { FirebaseCrashlytics.Instance.Log(message); }
        catch { /* Best-Effort */ }
    }

    public IDisposable StartTrace(string traceName)
    {
        // Firebase Performance Monitoring (Xamarin.Firebase.Perf) ist NICHT installiert —
        // separates SDK. Wir loggen den Trace als Custom-Key-Breadcrumb, damit zumindest
        // im Crash-Falle sichtbar ist welche Operation gerade lief.
        Log($"trace_start: {traceName}");
        return new BreadcrumbTrace(traceName);
    }

    public void SetFpsBucket(int avgFps)
    {
        SetCustomKey("fps_bucket", avgFps);
    }

    /// <summary>
    /// SHA256-Hash der Android Settings.Secure.ANDROID_ID. Ueberlebt App-Reinstall
    /// (theoretisch) nicht — nur einen Factory-Reset. Genug fuer Crash-Aggregation,
    /// DSGVO-konform da nicht reversibel auf User-Identitaet abbildbar.
    /// </summary>
    private string ComputeAnonymousUserHash()
    {
        try
        {
            var androidId = Android.Provider.Settings.Secure.GetString(
                _context.ContentResolver, Android.Provider.Settings.Secure.AndroidId);
            if (string.IsNullOrEmpty(androidId)) return string.Empty;

            var bytes = Encoding.UTF8.GetBytes(androidId);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash)[..16]; // 16 Hex-Chars reichen fuer Aggregation
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class BreadcrumbTrace : IDisposable
    {
        private readonly string _name;
        private readonly long _startTicks;
        public BreadcrumbTrace(string name)
        {
            _name = name;
            _startTicks = Environment.TickCount64;
        }
        public void Dispose()
        {
            try
            {
                var elapsedMs = Environment.TickCount64 - _startTicks;
                FirebaseCrashlytics.Instance.Log($"trace_end: {_name} ({elapsedMs}ms)");
            }
            catch { /* Best-Effort */ }
        }
    }
}
