# Loading — Startup-Pipeline

> Pipeline-Framework (`LoadingPipelineBase`, `LoadingStep`, `ShaderPreloader`) →
> [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md). Aufruf aus `App.axaml.cs` →
> [RechnerPlus.Shared](../CLAUDE.md).

| Datei | Zweck |
|-------|-------|
| `RechnerPlusLoadingPipeline.cs` | Erbt `LoadingPipelineBase`. Registriert die Lade-Schritte mit Gewichtung für den Fortschrittsbalken. |

## Schritte

Ein einziger gewichteter Schritt (Weight 40):

**ViewModel** — `MainViewModel`-Auflösung aus dem DI-Container (`Task.Run`).

**Kein Shader-Preload:** RechnerPlus rendert KEINEN der 12 SkSL-Effekte — weder direkt noch über
ein MeineApps.UI-Control. Alle Grafiken (VFD-Display, Result-Burst, Funktionsgraph, animierter
Hintergrund, Splash-Renderer) nutzen klassische SkiaSharp-Gradienten + MaskFilter, kein SkSL.
`ShaderPreloader.PreloadAll()` hätte 12 ungenutzte Shader (bis 2,4s auf Android) kompiliert.

**Eine einzige Splash:** Die Pipeline + `SkiaLoadingSplash` (in `App.axaml.cs`) sind der einzige
Splash-/Preload-Mechanismus. Die frühere zweite `SplashOverlay`-Splash in `MainView.axaml`
(`x:Name="Splash"`) mit eigenem `PreloadAction` → zweitem `ShaderPreloader.PreloadAll()`-Lauf ist
entfernt. Der Onboarding-Trigger (früher an `Splash.PreloadCompleted`) hängt jetzt am ersten
`LayoutUpdated` nach VM-Zuweisung (`MainView.OnDataContextChanged`) — siehe Views-Doku.

`App.axaml.cs` hält die Splash mindestens **800 ms** sichtbar, damit die Sweep-Wellen-Animation
vollständig abläuft (Details → [RechnerPlus.Shared](../CLAUDE.md)).
