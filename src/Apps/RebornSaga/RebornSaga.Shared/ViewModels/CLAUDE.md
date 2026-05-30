# ViewModels — Render/Update/Input-Delegation

Einziges ViewModel (`MainViewModel`) ist **Singleton** (in `App.axaml.cs` registriert).
Enthält **keine Spiellogik** — delegiert alles an `SceneManager` und `InputManager`.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | 60fps Game-Loop-Bridge (Update/Render), Input-Delegation, Back-Press-Flow, `ExitHintRequested`-Event. |

## MainViewModel — Verantwortlichkeiten

`MainViewModel` ist bewusst dünn gehalten. Die View ruft `Update(dt)` und `Render(canvas, bounds)` pro Frame;
alles andere liegt in `SceneManager` und den konkreten Szenen.

```csharp
// Konstruktor: Asset-Download-Szene direkt setzen
_sceneManager.ChangeScene<AssetDownloadScene>();
// → wechselt automatisch zu TitleScene wenn keine Downloads nötig
```

**`InitializeAsync()`** — wird von `MainView.OnAttachedToVisualTree` aufgerufen:
delegiert an `App.InitializeServicesAsync()` (Skills + Items parallel laden, SpriteCache initialisieren,
PurchaseService fire-and-forget).

## Input-Flow

```
MainView.OnPointerPressed/Moved/Released
  → GetSkiaPoint()  (DPI-skalierte Koordinaten: Avalonia-Bounds → SkiaSharp-Bounds)
  → MainViewModel.HandlePointer*
    → InputManager.OnPointer*
      → SceneManager.CurrentScene.HandlePointerDown/Move/Up
```

Keyboard (Desktop): `MainView.OnKeyDown` → `InputManager.OnKeyDown` → `CurrentScene.HandleInput`.

## Back-Press-Flow (`HandleBackPressed`)

1. Oberstes Overlay schließen (`_sceneManager.Overlays[^1]`).
2. Szene vom Stack poppen wenn aktuelle Szene nicht `TitleScene`.
3. Double-Back-to-Exit auf `TitleScene` via `BackPressHelper`.
