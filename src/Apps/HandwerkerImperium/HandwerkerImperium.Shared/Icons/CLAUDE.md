# Icons — Eigenes GameIcon-System (224 spielspezifische Icons)

Kein `Material.Icons.Avalonia` im Game-Kontext. Alle spielspezifischen Icons sind
WebP-Bitmaps (128×128) in `Assets/visuals/icons/` und ein eigenes Bitmap-Icon-System.
Generische Icon-Strategie → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `GameIconKind.cs` | Enum mit 224 spielspezifischen Werten + `None` (Sentinel) |
| `GameIcon.cs` | Custom Control, erbt `TemplatedControl` (hat `Foreground`). Rendert Bitmaps via Avalonia `DrawingContext`. `TintableIcons`-HashSet zentral hier gepflegt — `GameIconRenderer` liest davon (keine Duplikation). `Initialize(IGameAssetService)` wird in `App.axaml.cs` aufgerufen und ruft intern `GameIconRenderer.Initialize()` mit auf. |
| `GameIconRenderer.cs` | SkiaSharp-Renderer für Icons auf `SKCanvas`. Bitmap via `IGameAssetService.GetBitmap()`. Tinting via `SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn)` (gecachter Filter, vermeidet Allokation bei gleicher Farbe). `Cleanup()` in `App.DisposeServices()`. |

---

## Zwei Render-Kontexte

| Kontext | Klasse | Tinting-Mechanismus |
|---------|--------|---------------------|
| Avalonia UI | `GameIcon` | `OpacityMask` (ImageBrush als Maske + `FillRectangle(Foreground)`) |
| SkiaSharp Canvas | `GameIconRenderer` | `SKColorFilter.CreateBlendMode(color, SrcIn)` |

**Tintable vs. vollfarb:** UI-Icons (Pfeile, Chevrons, Close, Check, Swap…) werden per
`Foreground` getintet. AI-generierte Objekt-Icons (Werkzeuge, Gebäude, Figuren) werden
vollfarb gerendert. Die Trennung ist die `GameIcon.TintableIcons`-HashSet — beide Render-Pfade
lesen daraus.

---

## XAML-Nutzung

```axaml
xmlns:icons="using:HandwerkerImperium.Icons"

<icons:GameIcon Kind="Workshop" Width="24" Height="24" />
```

---

## Lifecycle

```
App.axaml.cs nach BuildServiceProvider():
    GameIcon.Initialize(assetService)       // setzt _assetService + ruft GameIconRenderer.Initialize() auf
    → Loading-Pipeline: GameIcon.PreloadAllAsync()  // ~224 Icons parallel laden, SKBitmap → Avalonia Bitmap

App.DisposeServices():
    GameIcon.ClearCache()                   // _bitmapCache + _brushCache + _pathMap leeren, Avalonia-Bitmaps disposed
    GameIconRenderer.Cleanup()              // _lastTintFilter disposed; static SKPaint-Felder bleiben (Prozessende)
```

**Achtung:** `GameIcon.ClearCache()` bereinigt auch `_pathMap` (snake_case-Pfad-Cache),
damit kein staler Zustand bei Re-Initialisierung (z.B. Tests) entsteht.
Ohne `GameIcon.Initialize()` vor dem ersten Render sind alle `GameIcon`-Controls leer —
`PreloadAllAsync` deckt 99 % ab, der `Render()`-Fallback retried 3× mit 150ms-Verzögerung.

---

## AppChecker-Verhalten

Der AppChecker meldet für HandwerkerImperium keinen „Material Icons fehlen"-Fehler im
Game-Kontext — das eigene Icon-System ist bewusste Konvention.
Generische App-UI (Settings, Dialoge) nutzt weiterhin `MaterialIcon`.

---

## Pfad-Konvention

`GameIconKind.ArrowDown` → `icons/arrow_down.webp` (PascalCase → snake_case via Regex,
gecacht in `_pathMap`). Identische Logik in `GameIcon.GetIconPath()` und
`GameIconRenderer.GetIconAssetPath()`.
