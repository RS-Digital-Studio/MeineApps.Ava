# RechnerPlus.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-
Lifecycle. **Werbefrei** → referenziert `MeineApps.Core.Premium.Ava` NICHT (keine AdMob-/
Billing-/Linked-Files). Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier **einmal pro Prozess**. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (kein `<App>`-Generic mehr in Avalonia 12). Factory-Wiring + Lifecycle + `AndroidHapticService`. |
| `AndroidManifest.xml` | Package `com.meineapps.rechnerplus`, `MyTheme.NoActionBar`, `INTERNET`-Permission, `AD_ID` explizit entfernt (`tools:node="remove"`), keine Billing-Permissions. |
| `Resources/mipmap-*` | App-Icon (`appicon`, `appicon_round`). |
| `Resources/values/styles.xml` | `MyTheme.NoActionBar`. |

## MainActivity — Reihenfolge in `OnCreate`

**Vor `base.OnCreate`** (Plattform-Hooks/Factories, die `this` brauchen):
- `UriLauncher.PlatformOpenUri` → `Intent.ActionView`.
- `UriLauncher.PlatformShareText` → `Intent.ActionSend` (natives Share-Sheet).
- `App.HapticServiceFactory = _ => new AndroidHapticService(this)`.

**Nach `base.OnCreate`:**
- `EnableImmersiveMode()` (StatusBar + NavigationBar ausblenden; API 30+ via `InsetsController`,
  sonst `SystemUiVisibility`-Fallback). Auch in `OnResume` + `OnWindowFocusChanged`.
- `MainViewModel` aus DI holen, `ExitHintRequested` → `Toast`.

**Back-Button:** `OnBackPressed()` delegiert an `_mainVm.HandleBackPressed()`; sonst
`MoveTaskToBack(true)`.

## AndroidHapticService

Liegt in `MainActivity.cs`. `IHapticService`-Impl über `Vibrator`: `Tick`/`Click`/`HeavyClick`
nutzen `VibrationEffect` (API Q+) mit `PerformHapticFeedback`-Fallback. Wird per Factory ins
Shared-DI injiziert.

## Build

```bash
dotnet build   src/Apps/RechnerPlus/RechnerPlus.Android
dotnet publish src/Apps/RechnerPlus/RechnerPlus.Android -c Release   # AAB, nur auf Anfrage
```
