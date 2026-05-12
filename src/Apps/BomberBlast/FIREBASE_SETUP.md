# BomberBlast — Firebase-Aktivierung

> **Stand:** 12.05.2026 · v2.0.56
> **Status:** ✅ AKTIV — Crashlytics + Analytics + FCM Push-Notifications live

## Konfiguriertes Firebase-Projekt

| Wert | Inhalt |
|---|---|
| Projekt-ID | `bomberblast-league` |
| Region | europe-west1 (Belgien) |
| Package-Name | `org.rsdigital.bomberblast` |
| App-ID | `1:1083743311749:android:5b2e88d43a81b3c6ed16ec` |
| Realtime DB URL | `bomberblast-league-default-rtdb.europe-west1.firebasedatabase.app` |
| google-services.json | `src/Apps/BomberBlast/BomberBlast.Android/google-services.json` |
| Release-Keystore SHA-1 | `31:0B:ED:E6:29:97:93:12:58:47:80:9A:3E:47:0D:76:0E:DF:E5:7C` |
| Release-Keystore SHA-256 | `7E:30:58:3C:89:AC:03:9D:71:90:7E:7E:28:19:FC:2B:99:6E:BC:07:52:35:4C:AE:A8:15:11:C6:35:35:C5:66` |

## NuGet-Konfiguration

`Directory.Packages.props`:
```xml
<PackageVersion Include="Xamarin.Firebase.Crashlytics" Version="119.4.4" />
<PackageVersion Include="Xamarin.Firebase.Analytics" Version="123.2.0" />
<PackageVersion Include="Xamarin.Firebase.Messaging" Version="124.1.2" />
```

**Versions-Hintergrund**: Analytics auf 123.x (statt 122.x) damit die transitive
`Xamarin.GooglePlayServices.Measurement.Base` 123.x konsistent bleibt — sonst zerbricht
der R8-Dexer an duplicate `com.google.android.gms.internal.measurement.zzov`-Klassen
(Konflikt mit Roberts existierender `Xamarin.GooglePlayServices.Ads.Lite 124.x`).

`BomberBlast.Android.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="Xamarin.Firebase.Crashlytics" />
  <PackageReference Include="Xamarin.Firebase.Analytics" />
  <PackageReference Include="Xamarin.Firebase.Messaging" />
</ItemGroup>
<ItemGroup>
  <GoogleServicesJson Include="google-services.json" />
</ItemGroup>
```

`Xamarin.GooglePlayServices.Tasks` kommt transitiv über die Firebase-Pakete — keine
explizite Referenz nötig.

## Implementierungs-Files

| Datei | Verantwortung |
|---|---|
| `AndroidTelemetryService.cs` | Crashlytics — SetCustomKey, RecordException, Log, SetUserId (SHA256-Hash von Android-Settings.Secure.ANDROID_ID, NICHT Firebase-UID) |
| `AndroidAnalyticsService.cs` | Analytics — LogEvent (Dictionary → Android.OS.Bundle), SetUserProperty, SetAnalyticsCollectionEnabled (Consent-Check via IPreferencesService `"AnalyticsConsent"`) |
| `AndroidPushNotificationService.cs` | FCM-Token via Tasks-API, Topic-Sub/Unsub, AlarmManager-LocalNotifications, POST_NOTIFICATIONS-Permission (Android 13+) |
| `BomberBlastMessagingService.cs` | FirebaseMessagingService-Subclass — OnNewToken + OnMessageReceived (Data-Payload → eigene Notification, Hybrid-Payload → System übernimmt) |
| `NotificationReceiver.cs` | BroadcastReceiver für AlarmManager-PendingIntents — postet Local-Notifications mit BigTextStyle |
| `MainActivity.cs` | 3 Factories vor `base.OnCreate` registriert + `OnRequestPermissionsResult` an PushNotificationService delegiert |
| `AndroidManifest.xml` | POST_NOTIFICATIONS, RECEIVE_BOOT_COMPLETED, SCHEDULE_EXACT_ALARM, USE_EXACT_ALARM, WAKE_LOCK + `<service>` für FCM + `<receiver>` für Local-Notifications + default-Channel/Icon meta-data |

## Notification-Channels

3 Channels werden in `AndroidPushNotificationService.EnsureNotificationChannels` registriert
(Android 8+):

| Channel-ID | Importance | Verwendung |
|---|---|---|
| `bomberblast_daily` | Low | Daily-Reward-Reminder (silent) |
| `bomberblast_liveops` | Default | Events / Liga / Battle Pass (mit Sound) |
| `bomberblast_important` | High | Saison-Ende, Cloud-Save-Konflikt (Sound + Vibration) |

Default-Channel im Manifest: `bomberblast_liveops` — FCM-Server-SDK fällt automatisch
darauf zurück wenn der Push keine eigene Channel-ID setzt.

## Token-Refresh-Architektur

`BomberBlastMessagingService.OnNewToken(token)` ist der einzige Eintrittspunkt für
FCM-Refresh. Da Events nur in der Klasse gefeuert werden können, in der sie definiert
sind, ruft der Service `AndroidPushNotificationService.RaiseTokenRefresh(token)` auf —
eine `internal static`-Helper-Methode, die das statische Event `FcmTokenChangedStatic`
feuert. Die Service-Instanz hängt sich im Constructor an dieses Event und propagiert
es an Abonnenten via `FcmTokenChanged` (Instanz-Event).

## Privacy-Verhalten

| Toggle | Wirkung |
|---|---|
| `IPreferencesService.Get("AnalyticsConsent", false)` | `SetAnalyticsCollectionEnabled(consent)` in `AndroidAnalyticsService.Initialize` |
| `IPreferencesService.Get("CrashlyticsConsent", false)` | wird vom `PrivacyCenter` ausgelesen — Crashlytics-Toggle live über Settings-UI |

Beide Toggles sind seit v2.0.55 in der Privacy-Sektion der Settings-View.

## Test-Anleitung (nach Build + Deploy)

### Crashlytics-Test
```csharp
// Irgendwo in einem ViewModel:
var tel = App.Services.GetRequiredService<ITelemetryService>();
tel.LogNonFatal(new Exception("Crashlytics-Test"), "manual_check");
```
Erscheint in Firebase-Console → Crashlytics → Issues nach ~5 min (initialer Bootstrap kann
bis zu 30 min dauern).

### Analytics-Test
```csharp
var ana = App.Services.GetRequiredService<IAnalyticsService>();
ana.LogEvent(AnalyticsEvents.LevelStart, new Dictionary<string, object> { ["level"] = 1, ["world"] = "forest" });
```
DebugView in Firebase-Console → Analytics → DebugView. **Wichtig:** Auf dem Test-Device
`adb shell setprop debug.firebase.analytics.app org.rsdigital.bomberblast` setzen,
damit DebugView die Events sofort anzeigt (sonst dauert es bis 24h).

### FCM-Test (Push aus der Console)
1. Console → Cloud Messaging → "Send your first message"
2. Notification Title + Text eingeben
3. Target: App `org.rsdigital.bomberblast` auswählen
4. "Send test message" → FCM-Token des Test-Devices einkleben.
   → Token holen via `App.Services.GetRequiredService<IPushNotificationService>().FcmToken`
   → oder in LogCat nach "BomberBlast" filtern (Token-Refresh wird beim ersten Start geloggt)
5. App in Foreground/Background versetzen, Push sollte ankommen.

### Local-Notification-Test
```csharp
var push = App.Services.GetRequiredService<IPushNotificationService>();
push.ScheduleLocalNotification(
    id: "test-reminder",
    triggerUtc: DateTime.UtcNow.AddSeconds(10),
    title: "BomberBlast",
    body: "Test-Reminder in 10 Sekunden",
    channel: NotificationChannel.LiveOps);
```
Nach 10s Push-Notification mit dem Body.

## Bekannte Limitierungen

- **POST_NOTIFICATIONS-Permission wird NICHT automatisch beim App-Start angefragt** —
  `InitializeAsync` prüft nur den Status. Apps müssen `RequestPermissionAsync` explizit
  rufen wenn der User opt-in wird (z.B. nach Wahl von "Daily-Reward-Reminder aktivieren").
- **Firebase Performance Monitoring ist NICHT integriert** — `ITelemetryService.StartTrace`
  loggt nur Breadcrumb-Marker in Crashlytics. Echtes Performance Monitoring würde
  `Xamarin.Firebase.Perf` benötigen.
- **Debug-Keystore-SHA fehlt in Firebase-Console** — Debug-Builds können daher die
  Firebase-REST-APIs nutzen, aber NICHT Play Games Services validieren. Falls Robert
  später im Debug-Mode FCM/Analytics testen will: `keytool -list -v -keystore ~/.android/debug.keystore`
  → SHA-1 in Firebase-Console nachtragen.
- **Realtime-Database-Rules** liegen in `database.rules.json` (im Repo-Root) — müssen
  via `firebase deploy --only database` aktiv geschaltet werden, wenn sie sich ändern.

## Build-Verifikation

```bash
dotnet restore src/Apps/BomberBlast/BomberBlast.Android
dotnet build src/Apps/BomberBlast/BomberBlast.Android --no-restore
```

Status: **0 Errors, 1 vorbestehende Warnung (TextBox.Watermark in ProfileView.axaml).**

## Versionierung

| Component | Version |
|---|---|
| ApplicationVersion (VersionCode) | 66 |
| ApplicationDisplayVersion | 2.0.56 |
| Splash-Versions-String | v2.0.56 |
