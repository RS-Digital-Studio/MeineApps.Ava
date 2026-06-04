# Controls — SkiaSharp-Canvas-Controls

App-eigene Custom-Controls. Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Basisklasse | Zweck |
|-------|-------------|-------|
| `GameButtonCanvas.cs` | `SKCanvasView` | Torn-Metal-Buttons via `TornMetalRenderer`. `ButtonSeed` (int) für deterministisches Riss-Muster. |
| `AchievementIconCanvas.cs` | `SKCanvasView` | Achievement-Icon: Trophy (freigeschaltet) oder Schloss + Fortschrittsring (gesperrt) via `AchievementIconRenderer`. |
| `EmptyStateCanvas.cs` | `SKCanvasView` | Leerer-Listen-State-Illustration (prozedural, Neon-Stil). `StateType` + `PrimaryColor`/`SecondaryColor` konfigurierbar. Eigener 30-fps-`DispatcherTimer` für Idle-Animation. |
| `MenuBackgroundCanvas.cs` | `UserControl` | Animierter Menü-Hintergrund via `MenuBackgroundRenderer` (7 Themes, ~30 fps). **Kein** `SKCanvasView` — wraps intern einen `SKCanvasView` in `Content`. |
| `MedalCanvas.cs` | `SKCanvasView` | Level-Sterne-Medaillen (Bronze/Silber/Gold) via `GameOverVisualization.DrawMedal`. Sterne-zu-Rang: ≥3=Gold, 2=Silber, 1=Bronze. |
| `ShopUpgradeIconCanvas.cs` | `SKCanvasView` | Shop-Upgrade-Icon via `ShopIconRenderer`. `UpgradeTypeIndex` (int, 0=StartBombs …) + `IconColorArgb` (uint). |

---

## Architektur-Pattern

Die meisten Controls folgen dem gleichen Pattern:

1. `StyledProperty` für Daten-Properties mit `InvalidateSurface()` als `Changed`-Handler (statischer Klassenkonstruktor).
2. `PaintSurface += OnPaintSurface` im Ctor, Delegation an den zugehörigen Renderer.
3. Für Controls mit Animation (`EmptyStateCanvas`, `MenuBackgroundCanvas`): eigener `DispatcherTimer` (33 ms ≈ 30 fps), Start/Stop über `AttachedToVisualTree`/`DetachedFromVisualTree`-Events.

`MenuBackgroundCanvas` weicht ab: erbt von `UserControl`, baut intern einen `SKCanvasView` als `Content`. Stoppt seinen 30-fps-Timer per `EffectiveViewportChanged`-Handler **komplett** (nicht nur Tick-Skip), wenn `IsEffectivelyVisible` false wird — sonst tickt er nach dem Wechsel ins Spiel als UI-Thread-Dauerlast weiter, weil nur der Parent-Border `IsVisible=false` wird, die eigene `IsVisible`-Property aber unverändert bleibt (und `IsEffectivelyVisible` keine eigene `AvaloniaProperty` hat, auf die man im `OnPropertyChanged` reagieren könnte). Der `IsEffectivelyVisible`-Guard im Tick bleibt als Sicherheitsnetz.

### Torn-Metal-Buttons (`GameButtonCanvas`)

`TornMetalRenderer` erzeugt eine deterministisch zerrissene Metalloptik. `ButtonSeed` ist ein `int`. Konventionell verwendete Werte:

| Verwendung | Seed-Beispiele |
|-----------|----------------|
| CTA-Buttons | positive gerade Zahlen |
| Sekundär-/Gefahr-Buttons | andere eindeutige Werte pro Button |

Jede Button-Instanz sollte einen eigenen Seed bekommen — gleicher Seed → gleiches Muster.

### SKCanvasView-Sichtbarkeits-Gotcha

`InvalidateSurface()` auf einer unsichtbaren `SKCanvasView` wird ignoriert. Nach `IsVisible = true` müssen Daten erneut gesetzt oder `InvalidateSurface()` explizit aufgerufen werden.

`MenuBackgroundCanvas` löst dies mit einer `IsEffectivelyVisible`-Prüfung im Timer-Tick: inaktive Seiten (PageView-Border in `MainView` unsichtbar) rendern keine Frames. Details zum SkiaSharp-Lifecycle → [MeineApps.UI/CLAUDE.md](../../../../../src/UI/MeineApps.UI/CLAUDE.md).
