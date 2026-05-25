# Firebase-Integration — Anleitung

Status: Die Service-Klassen sind als Stubs vorbereitet — sobald das Firebase Unity SDK
installiert ist, koennen die TODOs im Code durch echte Firebase-Calls ersetzt werden.

## 1. Firebase-Projekt erstellen

1. https://console.firebase.google.com -> Neues Projekt
2. Android-App hinzufuegen:
   - Package-ID: `com.meineapps.arcanekingdom` (oder eigener Namespace)
   - SHA-1 spaeter ergaenzen
3. `google-services.json` herunterladen -> nach
   `Unity/Assets/StreamingAssets/google-services.json` legen
   (Firebase Unity SDK liest es automatisch aus diesem Pfad)

## 2. Firebase Unity SDK installieren

Quelle: https://firebase.google.com/docs/unity/setup

Option A — Per `.tgz` (empfohlen fuer Versions-Kontrolle):

1. Firebase Unity SDK herunterladen (z.B. `firebase_unity_sdk_12.x.zip`)
2. `.unitypackage`-Files extrahieren:
   - `FirebaseAuth.unitypackage`
   - `FirebaseDatabase.unitypackage` (Realtime DB)
   - `FirebaseAnalytics.unitypackage`
   - `FirebaseStorage.unitypackage` (optional fuer Asset-Uploads)
3. In Unity: Assets > Import Package > Custom Package... -> jedes `.unitypackage`
4. Auto-Resolver bestaetigen (laedt Android Resolver Dependencies)

Option B — Per Unity Package Manager (neuere Firebase-Versionen):

In `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "Game Package Registry by Google",
      "url": "https://unityregistry-pa.googleapis.com",
      "scopes": ["com.google"]
    }
  ],
  "dependencies": {
    "com.google.external-dependency-manager": "1.2.179",
    "com.google.firebase.app": "12.6.0",
    "com.google.firebase.auth": "12.6.0",
    "com.google.firebase.database": "12.6.0",
    "com.google.firebase.analytics": "12.6.0"
  }
}
```

## 3. Code-Switch in den Stubs

Nach Install funktionieren folgende Namespaces:

```csharp
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Analytics;
```

### 3.1 FirebaseAuthService.SignInAnonymouslyAsync

Ersetze den lokalen Stub durch:

```csharp
public async UniTask<Result> SignInAnonymouslyAsync(CancellationToken ct = default)
{
    try
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        var result = await auth.SignInAnonymouslyAsync().AsUniTask();
        CurrentUserId = result.User.UserId;
        CurrentUserDisplayName = result.User.DisplayName ?? "Gast";
        IsAuthenticated = true;
        return Result.Success();
    }
    catch (Exception ex)
    {
        GameLogger.Error("Auth", "SignInAnonymouslyAsync fehlgeschlagen", ex);
        return Result.Failure(ex.Message);
    }
}
```

### 3.2 FirebaseSaveService.LoadAsync / SaveAsync

Im LoadAsync nach Local-Read den Server-Snapshot pruefen:

```csharp
var dbRef = FirebaseDatabase.DefaultInstance.GetReference($"users/{userId}/save");
var snapshot = await dbRef.GetValueAsync().AsUniTask();
if (snapshot.Exists)
{
    var json = snapshot.GetRawJsonValue();
    var serverSave = JsonConvert.DeserializeObject<PlayerSave>(json, _jsonSettings);
    if (serverSave != null && serverSave.LastSavedAtUtc > localSave.LastSavedAtUtc)
        localSave = serverSave; // Server-Wins
}
```

Im SaveAsync parallel pushen:

```csharp
await FirebaseDatabase.DefaultInstance
    .GetReference($"users/{userId}/save")
    .SetRawJsonValueAsync(json)
    .AsUniTask();
```

### 3.3 FirebaseAnalyticsService

```csharp
public void Track(string eventName, IDictionary<string, object>? parameters = null)
{
    var fbParams = parameters?.Select(p => new Parameter(p.Key, p.Value.ToString())).ToArray();
    Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName, fbParams);
}
```

## 4. Build-Konfiguration

Android:
- Player Settings > Other Settings > Minimum API Level: **21** (Android 5.0)
- Scripting Backend: **IL2CPP**
- Target Architecture: **ARM64**
- Identification > Package Name: muss zur Firebase-Konfig passen

## 5. Firebase Security Rules (Realtime DB)

Default-Rules verbieten alles. Fuer Test:

```json
{
  "rules": {
    "users": {
      "$uid": {
        ".read": "$uid === auth.uid",
        ".write": "$uid === auth.uid"
      }
    }
  }
}
```

Produktion: feinere Server-Validierung (max Save-Groesse, Currencies-Cap etc.) via
Cloud-Functions oder Database-Rules.

## 6. Was BLEIBT lokal

- `PlayerPrefs` fuer Settings (Sprache, Audio-Volumes, Vibration-Toggle)
- `Application.persistentDataPath` fuer Save-Backup (Offline-Fallback)
- `CardCatalog` + `WorldCatalog` als ScriptableObjects (im Build gebundelt)

Firebase ist die **Source-of-Truth** fuer PlayerSave, aber das lokale JSON bleibt als
Offline-Fallback (wichtig fuer Spieler ohne stabile Netzverbindung).
