# BomberBlast — Firebase-Setup-Anleitung

> **Stand:** 09.05.2026 · v2.0.55
> **Ziel:** Crashlytics + Analytics + FCM Push-Notifications aktivieren.

## Vorbereitung (einmalig)

1. Firebase-Konsole öffnen: https://console.firebase.google.com
2. Projekt anlegen (`bomberblast-prod`).
3. Android-App registrieren: Package-Name `org.rsdigital.bomberblast`.
4. SHA-1 + SHA-256 Fingerprints aus dem Keystore extrahieren:
   ```bash
   keytool -list -v -keystore F:\Meine_Apps_Ava\Releases\meineapps.keystore -alias meineapps
   # Passwort: MeineApps2025
   ```
   In Firebase-Konsole bei "App-Konfiguration" eintragen.
5. **`google-services.json`** herunterladen + nach `src/Apps/BomberBlast/BomberBlast.Android/google-services.json` kopieren.

## Modul 1: Crashlytics (~20 min)

### NuGet
`Directory.Packages.props` ergänzen:
```xml
<PackageVersion Include="Plugin.Firebase.Crashlytics" Version="3.0.0" />
```

### Code aktivieren
`AndroidTelemetryService.cs` öffnen, alle `// TODO`-Kommentare einkommentieren.

### Wireup in MainActivity
```csharp
App.TelemetryServiceFactory = sp => new AndroidTelemetryService();
```

## Modul 2: Analytics (~20 min)

### NuGet
```xml
<PackageVersion Include="Plugin.Firebase.Analytics" Version="3.0.0" />
```

### DSGVO-Consent-Flow
Settings-View hat seit v2.0.55 Privacy-Sektion mit `AnalyticsConsent` + `CrashlyticsConsent`-Toggles.
`AndroidAnalyticsService.Initialize` liest den Consent + ruft `SetAnalyticsCollectionEnabled(consent)` auf.

### Wireup
```csharp
App.AnalyticsServiceFactory = sp => new AndroidAnalyticsService();
```

## Modul 3: Cloud Messaging (FCM) (~50 min)

### NuGet
```xml
<PackageVersion Include="Plugin.Firebase.CloudMessaging" Version="3.0.0" />
```

### AndroidManifest
```xml
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
<uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" />

<application ...>
  <service android:name="...FirebaseMessagingService"
           android:exported="false">
    <intent-filter>
      <action android:name="com.google.firebase.MESSAGING_EVENT" />
    </intent-filter>
  </service>
</application>
```

### Permission-Flow für Android 13+
- `RequestPermissionAsync` muss `Activity.RequestPermissions(new[] { Manifest.Permission.PostNotifications }, REQUEST_CODE)` aufrufen
- Callback in `MainActivity.OnRequestPermissionsResult` setzt `ArePermissionsGranted = true`

### Wireup
```csharp
App.PushNotificationServiceFactory = sp => new AndroidPushNotificationService();
```

## Wichtig: Firebase-Rules deployen

`bomberblast-league.rules.json` muss nach jedem Edit in der Firebase-Konsole deployed werden:
```bash
firebase deploy --only database
```

v2.0.55-Fix: `updatedUtc` aus Saison-Tier-Rule entfernt — vorher wurden ALLE Liga-Pushes seit v2.0.34 abgelehnt.

## Total-Aufwand

| Modul | NuGet | Code | Console | Total |
|---|---:|---:|---:|---:|
| Crashlytics | 5 min | 10 min | 5 min | **20 min** |
| Analytics | 5 min | 10 min | 5 min | **20 min** |
| FCM | 10 min | 30 min | 10 min | **50 min** |

**Gesamt: ~1.5h für alle 3 Module + Tests.**
