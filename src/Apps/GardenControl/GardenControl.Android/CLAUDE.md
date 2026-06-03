# GardenControl.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-Lifecycle.
**Werbefrei, kein IAP** → referenziert `MeineApps.Core.Premium.Ava` nicht (keine
AdMob-/Billing-/Linked-Files). Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier **einmal pro Prozess**. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (kein `<App>`-Generic in Avalonia 12). VM-Referenz für Back-Button + Exit-Toast. |
| `AndroidManifest.xml` | `MyTheme.NoActionBar`, Netzwerk-Permissions. Package-ID `com.rsdigital.gardencontrol` steht in der `.csproj`. |
| `Resources/values/styles.xml` | `MyTheme.NoActionBar`. |
| `Resources/mipmap-*` | App-Icon. |

## MainActivity — Reihenfolge in `OnCreate`

**Nach `base.OnCreate`** (DI ist erst nach `base.OnCreate` vollständig):
- `_mainVm` aus `App.Services.GetService<MainViewModel>()` holen.
- `ExitHintRequested` → `Toast.MakeText` (kurzer Hinweis "Nochmal drücken").

`UriLauncher`-Factories fehlen bewusst — die App öffnet keine externen URIs
und hat kein natives Share-Sheet.

**Back-Button:** `OnBackPressed()` delegiert an `_mainVm.HandleBackPressed()`; sonst `base.OnBackPressed()`.

## AndroidManifest — Permissions

```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.ACCESS_WIFI_STATE" />
```

Internet + Netzwerk-State für SignalR-Verbindung zum Pi im lokalen WLAN.
`ACCESS_WIFI_STATE` ist auf Android 12+ nötig für lokale Netzwerkerkennung.

## Build

```bash
dotnet build   src/Apps/GardenControl/GardenControl.Android
dotnet publish src/Apps/GardenControl/GardenControl.Android -c Release   # AAB, nur auf Anfrage
```
