---
name: Games + BingXBot Pre-Release 18.04.2026
description: Ergebnisse der Pre-Release-Pruefung fuer HandwerkerImperium v2.0.30, BomberBlast v2.0.30, BingXBot v1.2.0
type: project
---

Stand: 18.04.2026. Alle drei Android-Builds (Debug) kompilierten fehlerfrei.

**HandwerkerImperium v2.0.30 (VersionCode 38, Produktion)**
- Shared 0W/0E, Android 9W/0E (harmlose nullable + deprecated SystemUiVisibility). RELEASE OK.
- Manifest: allowBackup=false, usesCleartextTraffic=false, enableOnBackInvokedCallback=false (bewusst fuer Android 13+ Back-Press-Behaviour). Permissions minimal.
- Directory.Build.targets: RunAOTCompilation nicht explizit, aber AndroidEnableProfiledAot=false (Full AOT). AOT-Flags korrekt.
- Purchase-Init in Loading-Pipeline: `services.GetRequiredService<IPurchaseService>().InitializeAsync()`. Premium-Restore OK.
- MaskFilter-Pattern: 5 static readonly oder Dispose-geschuetzt in Renderer-Dateien. OK.
- Debug.WriteLine in 20+ Stellen — alle try-catch-Logs fuer nicht-kritische Exceptions. WARN (nicht FAIL).
- Firebase: database.rules.json existiert im App-Root (nicht im Shared-Output). OK.

**BomberBlast v2.0.30 (VersionCode 40, geschlossener Test)**
- Shared 0W/0E, Android 0W/0E. RELEASE OK.
- Manifest: allowBackup=false, usesCleartextTraffic=false, 4 minimal Permissions. OK.
- AppLogger-Pattern ersetzt Debug.WriteLine konsequent. Nur 2 Treffer in Kommentaren.
- Purchase-Init in Loading-Pipeline via Task-Parallelisierung: `var purchaseTask = services.GetRequiredService<IPurchaseService>().InitializeAsync();`. OK.
- LevelSelectVisualization.cs Zeile 311: MaskFilter = SKMaskFilter.CreateBlur ohne Dispose des alten Filters. WARN — nur 1x/Rendering, ueberschaubarer Leak.
- Firebase: KEINE lokale database.rules.json im App-Ordner — nur in Firebase Console deployed. Fuer Liga-System + Cloud-Save relevant.

**BingXBot v1.2.0 (VersionCode 7, Entwicklung)**
- Shared 0W/0E, Android 0W/0E. BUILD OK (kein Play-Store-Release, Remote-Client fuer Pi-Server).
- Manifest: allowBackup=false, networkSecurityConfig fuer LAN/Tailscale/localhost, usesCleartextTraffic NICHT gesetzt (aber networkSecurityConfig deckt das ab). 3 Permissions. OK.
- IAppPaths-Factory-Pattern aktiv: `App.AppPathsFactory = () => new AndroidAppPaths(this)` in MainActivity Zeile 32 — VOR DI-Build. Android-Sandbox-Crash beseitigt.
- SecureStorageService wrapped Directory.CreateDirectory in try-catch (Zeile 23-26). OK.
- ViewLocator als DataTemplate in App.axaml: `<local:ViewLocator />`. OK. MainView + MainViewMobile nutzen `<ContentControl Content="{Binding CurrentPageViewModel}">`. OK.
- Keine Service-Locator-Aufrufe in View-Ctoren. BingXBot v1.1.4 Android-Crash-Pattern behoben.
- Version-Inkonsistenz: CLAUDE.md sagt v1.1.4, csproj hat ApplicationDisplayVersion=1.2.0 / VersionCode=7. WARN — Doku nicht synchron.

**Wiederkehrende Gotchas zu beachten:**
1. Warnung in HandwerkerImperium.Android MainActivity.cs: `SystemUiVisibility` deprecated ab API 30 — funktioniert noch, fuer v2.0.31+ auf SystemUiFlags migrieren
2. `SetDecorFitsSystemWindows` deprecated ab API 35 — funktioniert noch
3. BomberBlast LevelSelectVisualization.cs Zeile 311: `_glowPaint.MaskFilter = SKMaskFilter.CreateBlur(...)` ohne Dispose — bei naechstem Release pruefen
