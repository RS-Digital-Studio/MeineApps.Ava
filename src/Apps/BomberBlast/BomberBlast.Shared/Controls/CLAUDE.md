# Controls — SkiaSharp-Canvas-Controls

App-eigene Custom-Controls. Alle erben von `SKCanvasView` und zeichnen via SkiaSharp.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `GameButtonCanvas.cs` | Torn-Metal-Buttons via `TornMetalRenderer`. `ButtonSeed` (10-181) für deterministischen Riss-Pattern. |
| `AchievementIconCanvas.cs` | Achievement-Trophäen-Icon mit Rarity-Glow via `AchievementIconRenderer`. |
| `EmptyStateCanvas.cs` | Leerer-Listen-State-Illustration (prozedural, Neon-Stil). Wechselt Bild je `EmptyStateType`. |
| `MenuBackgroundCanvas.cs` | Animierter Menü-Hintergrund via `MenuBackgroundRenderer` (7 Themes, 60-Partikel-Pool). |
| `MedalCanvas.cs` | Level-Sterne-Medaillen-Darstellung (Bronze/Silber/Gold). |
| `ShopUpgradeIconCanvas.cs` | Shop-Upgrade-Icon mit Level-Indikator via `ShopIconRenderer`. |

---

## Architektur-Pattern

Alle Controls folgen dem gleichen Pattern:

1. AvaloniaProperty für Daten-Properties (mit `InvalidateSurface()` als `PropertyChanged`-Callback).
2. `OnAttachedToVisualTree` / `OnDetachedFromVisualTree` für Timer-Lifecycle.
3. `PaintSurface` delegiert an den zugehörigen Renderer.

### Torn-Metal-Buttons (`GameButtonCanvas`)

`TornMetalRenderer` erzeugt eine deterministisch zerrissene Metalloptik.
`ButtonSeed`-Werte sind per Konvention festgelegt:

| Verwendung | Seed-Bereich |
|-----------|-------------|
| CTA-Buttons | 0.5 |
| Erfolgs-Buttons | 0.3 |
| Gefahr-Buttons | 0.7 |
| Gold-Buttons | 0.6 |
| Sekundär-Buttons | 0.2-0.3 |

### SKCanvasView-Sichtbarkeits-Gotcha

`InvalidateSurface()` auf einer unsichtbaren `SKCanvasView` wird ignoriert.
Nach `IsVisible = true` → Daten erneut setzen oder `InvalidateSurface()` explizit aufrufen.
Details → Haupt-CLAUDE.md Troubleshooting.
