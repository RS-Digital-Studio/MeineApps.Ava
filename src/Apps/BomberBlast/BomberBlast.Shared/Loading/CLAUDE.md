# Loading — Startup-Pipeline

Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).
Composition Root + `RunLoadingAsync`-Kontext → [../CLAUDE.md](../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `BomberBlastLoadingPipeline.cs` | Einziger `AddStep` mit parallelen Tasks, `ProgressChanged`-Event für Splash-Fortschritt |
| `LoadingTips.cs` | 33 globale Tipps + 10 weltspezifische Tipps (5 Welten × 2), Anti-Repeat-Picker, 30 % Chance auf weltspezifischen Tipp |

---

## BomberBlastLoadingPipeline

Abgeleitet von `LoadingPipelineBase` (MeineApps.UI). Läuft via `App.RunLoadingAsync()` im
Composition Root.

**Im Konstruktor (vor `AddStep`):** `IRetentionService.TouchSession()` — setzt `FirstSessionUtc`
beim allerersten Start und aktualisiert `LastSessionUtc`. Läuft synchron, vor allem anderen, damit
D1/D7-Fenster + Comeback-Detection korrekte Zeitstempel erhalten.

**Einziger Step** (`Weight = 60`, DisplayName aus RESX `SplashStep_Graphics`):

Vier Tasks werden per `Task.WhenAll` parallel ausgeführt:

| Task | Inhalt |
|------|--------|
| `shaderTask` | `ShaderPreloader.PreloadAll()` (12 generische SkSL-Shader) + `ExplosionShaders.Preload()` (Noise-LUT + Paint-Cache) + `ShaderEffects.Preload()` (WaterRipple SkSL, Ocean-Welt) + `BloomEffect.Preload()` (Threshold + Box-Blur, Ultra-Tier-Gate) |
| `vmTask` | `MainViewModel` auf dem UI-Thread instanziieren (Dispatcher.UIThread.InvokeAsync) — ViewModels erzeugen im Ctor Brushes mit UI-Thread-Affinität |
| `purchaseTask` | `IPurchaseService.InitializeAsync()` |
| `assetTask` | `IGameAssetService.PreloadAsync(GetCriticalAssets())` — Splash, Menü-Hintergründe, Bosse, 12 PowerUps, 12 Gegner-Typen, Welt-1-Hintergrund |

**Warum Shader im Splash?** SkSL-Kompilierung ist GPU-Driver-abhängig (10–150 ms) und würde
beim ersten Frame des ersten Levels als Stutter sichtbar sein. Parallel zum VM-Build kostet
es keine Wartezeit.

**Warum VM auf dem UI-Thread?** `BottomTabBarViewModel` und andere VMs erzeugen im Ctor
`SolidColorBrush`-Objekte — Avalonia erzwingt UI-Thread-Affinität (`VerifyAccess`). Auf einem
Background-Thread instanziiert, crashen diese Objekte beim ersten Render.

**Kritische Assets** (`GetCriticalAssets()`): Pfade aus `GameAssetPaths` (Single Source of Truth —
`BossAssetPaths`, `GetAllPowerUpAssets()`, `GetAllEnemyAssets()`, `GetWorldAssetPath(0)`).
Welten 2–10 werden lazy beim LevelSelect/`GameViewModel.SetParameters` nachgeladen.

---

## LoadingTips

```csharp
// Anti-Repeat-Picker: nie den gleichen Tipp zweimal in Folge
string tip = LoadingTips.GetRandomTip(worldIndex: null);  // globaler Tipp
string tip = LoadingTips.GetRandomTip(worldIndex: 3);      // 30 % Chance auf Welt-3-Tipp (nur Welten 0–4 belegt)
int total  = LoadingTips.TotalTipCount;                    // 33 — für UI-Browser/Pagination
```

**Globaler Pool:** 33 Keys (`LoadingTip01_BombChain` … `LoadingTip33_BossRush`).

**Weltspezifischer Pool:** 5 Welten (Index 0–4), je 2 Keys. `WorldSpecificTips.Length` = 5
— für Welten-Index ≥ 5 fällt der Code auf den globalen Pool zurück.

**Resolve-Pfad:** `LocalizationManager.GetString(key)` → bei leerem/fehlendem Ergebnis
hartcodierter deutscher Default-Hint im `key switch`. Jeder Key hat einen **eindeutigen**
Default-Text, damit der Anti-Repeat-Picker auf Index-Ebene auch visuell wirkt.
