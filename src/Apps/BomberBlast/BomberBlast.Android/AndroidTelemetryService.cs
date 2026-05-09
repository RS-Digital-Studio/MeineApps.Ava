using BomberBlast.Services;

namespace BomberBlast.Droid;

/// <summary>
/// Android-Implementation für ITelemetryService (Firebase Crashlytics).
///
/// AKTUELLER STAND: Stub. Erfüllt das Interface, ruft aber noch keine echten Crashlytics-APIs auf.
/// Wird zur funktionalen Implementation sobald folgende Schritte erledigt sind:
///
/// SETUP-VORAUSSETZUNGEN (Robert):
/// 1. Firebase-Projekt anlegen + Android-App registrieren (Package: org.rsdigital.bomberblast)
/// 2. google-services.json herunterladen + nach src/Apps/BomberBlast/BomberBlast.Android/Resources/ kopieren
/// 3. NuGet: `Plugin.Firebase.Crashlytics` (oder Xamarin.Firebase.Crashlytics) zu Directory.Packages.props
/// 4. AndroidManifest.xml: keine Permissions nötig (Crashlytics nutzt INTERNET die schon da ist)
/// 5. MainActivity.cs: `App.TelemetryServiceFactory = sp =&gt; new AndroidTelemetryService();` in OnCreate
///
/// IMPLEMENTATION-CODE (auskommentiert, Stand des Plugin.Firebase.Crashlytics-API):
/// <code>
/// using Plugin.Firebase.Crashlytics;
/// CrossFirebaseCrashlytics.Current.SetCustomKey(key, value);
/// CrossFirebaseCrashlytics.Current.RecordException(ex);
/// CrossFirebaseCrashlytics.Current.Log(message);
/// </code>
/// </summary>
public sealed class AndroidTelemetryService : ITelemetryService
{
#pragma warning disable CS0169 // Wird nach Firebase-Setup verwendet
    private string? _userIdHash;
#pragma warning restore CS0169

    public void Initialize()
    {
        // TODO nach Firebase-Setup:
        // CrossFirebaseCrashlytics.Current.SetCrashlyticsCollectionEnabled(true);
        // var anonHash = ComputeAnonymousUserHash();  // SHA256 von Device-ID, NICHT echte UID
        // CrossFirebaseCrashlytics.Current.SetUserId(anonHash);
        // _userIdHash = anonHash;
    }

    public void SetCustomKey(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        var clamped = value?.Length > 100 ? value[..100] : value;
        // TODO: CrossFirebaseCrashlytics.Current.SetCustomKey(key, clamped ?? string.Empty);
    }

    public void SetCustomKey(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        // TODO: CrossFirebaseCrashlytics.Current.SetCustomKey(key, value);
    }

    public void SetCustomKey(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        // TODO: CrossFirebaseCrashlytics.Current.SetCustomKey(key, value);
    }

    public void LogNonFatal(Exception ex, string? context = null)
    {
        if (ex == null) return;
        if (!string.IsNullOrEmpty(context))
        {
            // TODO: CrossFirebaseCrashlytics.Current.Log($"[{context}]");
        }
        // TODO: CrossFirebaseCrashlytics.Current.RecordException(ex);
    }

    public void Log(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        // TODO: CrossFirebaseCrashlytics.Current.Log(message);
    }

    public IDisposable StartTrace(string traceName)
    {
        // TODO: Firebase Performance Monitoring
        // var trace = CrossFirebasePerformance.Current.NewTrace(traceName);
        // trace.Start();
        // return new TraceWrapper(trace);
        return AndroidTrace.Instance;
    }

    public void SetFpsBucket(int avgFps)
    {
        SetCustomKey("fps_bucket", avgFps);
    }

    private sealed class AndroidTrace : IDisposable
    {
        public static readonly AndroidTrace Instance = new();
        public void Dispose() { /* TODO: trace.Stop() */ }
    }
}
