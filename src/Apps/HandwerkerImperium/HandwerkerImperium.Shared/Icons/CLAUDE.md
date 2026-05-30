# Icons — Eigenes GameIcon-System (224 Icons)

Kein `Material.Icons.Avalonia` im Game-Context. Alle spielspezifischen Icons sind
WebP-Bitmaps (128×128) in `Assets/visuals/icons/` und ein eigenes PathIcon-System.
Generische Icon-Strategie → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `GameIconKind.cs` | Enum mit 224 Werten — alle spielspezifischen Icon-Namen |
| `GameIcon.cs` | Custom Control, erbt `PathIcon`. **`StyleKeyOverride => typeof(PathIcon)`** — PFLICHT, sonst rendert das Control nicht (Avalonia 12 findet kein Template ohne Override). `Initialize(IGameAssetService)` wird in `App.axaml.cs` aufgerufen |
| `GameIconRenderer.cs` | SkiaSharp-Renderer für Icons auf SKCanvas. Bitmap via `GameAssetService.GetBitmap()` + `SKColorFilter.CreateBlendMode(color, SrcIn)` für Tint. `Cleanup()` in `App.DisposeServices()` |

---

## XAML-Nutzung

```axaml
xmlns:icons="using:HandwerkerImperium.Icons"

<icons:GameIcon Kind="Workshop" Width="24" Height="24" />
```

---

## AppChecker-Verhalten

Der AppChecker meldet für HandwerkerImperium keinen "Material Icons fehlen"-Fehler für
Game-Contexts — das eigene Icon-System ist bewusste Konvention.
Generische App-UI (Settings, Dialoge) nutzt weiterhin `MaterialIcon`.

---

## GameAssetService-Integration

`GameIcon.Initialize(assetService)` wird in `App.axaml.cs` nach `BuildServiceProvider()`
aufgerufen — **vor** dem ersten Render. Ohne diesen Aufruf sind alle GameIcon-Controls leer.
`GameIcon.ClearCache()` in `App.DisposeServices()` für sauberes Shutdown.
