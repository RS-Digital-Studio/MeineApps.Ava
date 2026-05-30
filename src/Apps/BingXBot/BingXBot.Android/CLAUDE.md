# BingXBot.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Reiner Remote-Client — keine Trading-Engine auf dem
Handy. Verbindet sich zum Pi-Server via SignalR. Werbefrei → **keine** `MeineApps.Core.Premium.Ava`-
Referenz (kein AdMob, kein Billing). Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Bootstrap einmal pro Prozess. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (non-generic, Avalonia 12). Factory-Wiring VOR `base.OnCreate`, Back-Button-Delegation. |
| `AndroidManifest.xml` | Package `com.meineapps.bingxbot`. Permissions: INTERNET, ACCESS_NETWORK_STATE, VIBRATE, POST_NOTIFICATIONS. |
| `Resources/xml/network_security_config.xml` | Cleartext nur für LAN/Tailscale/localhost — öffentliche Domains erzwingen HTTPS. |
| `Resources/mipmap-*` | App-Icon (`appicon`, `appicon_round`). |
| `Resources/values/styles.xml` | `MyTheme.NoActionBar`. |

## `MainActivity.OnCreate` — Reihenfolge

**Vor `base.OnCreate`** (muss vor DI-Build gesetzt sein, weil `OnFrameworkInitializationCompleted`
`AppPathsFactory` abfragt):

```csharp
App.AppPathsFactory = () => new AndroidAppPaths(this);
// AndroidAppPaths nutzt Context.FilesDir — KEIN Environment.SpecialFolder.UserProfile (crasht auf Android)
```

**Nach `base.OnCreate`:**
- `MainViewModel` aus `App.Services` holen.
- `ExitHintRequested`-Event → `Toast.MakeText` (Double-Back-to-Exit).

## Back-Button

```csharp
public override void OnBackPressed()
{
    if (_mainVm != null && _mainVm.HandleBackPressed()) return;
    base.OnBackPressed();  // MoveTaskToBack(true) default
}
```

Kein `MoveTaskToBack` manuell nötig — `base.OnBackPressed()` übernimmt das.

## Manifest-Besonderheiten

- `android:enableOnBackInvokedCallback="false"` — Avalonia 12 braucht das für Back-Gesture-Kompatibilität.
- `android:allowBackup="false"` — Trading-Credentials dürfen nicht per ADB-Backup extrahiert werden.
- `android:largeHeap="false"` — App ist Remote-Client, kein Trading-Engine-RAM nötig.
- `android:networkSecurityConfig="@xml/network_security_config"` — Cleartext für Pi im LAN/Tailscale erlaubt.

## Keine Premium-Linked-Files

BingXBot ist werbefrei + kein IAP. Daher **keine** `<Compile Include="..." Link="..." />`-Einträge für
`AdMobHelper`, `AndroidRewardedAdService` oder `AndroidPurchaseService`. Unterschied zu z.B.
`HandwerkerRechner.Android` beachten.

## Build

```bash
dotnet build   src/Apps/BingXBot/BingXBot.Android
dotnet publish src/Apps/BingXBot/BingXBot.Android -c Release   # AAB, nur auf Anfrage
```
