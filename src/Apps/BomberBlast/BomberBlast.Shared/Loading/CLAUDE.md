# Loading — Startup-Pipeline

Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `BomberBlastLoadingPipeline.cs` | Sequentielle Startup-Schritte, `ProgressChanged`-Event für Splash-Fortschritt |
| `LoadingTips.cs` | 33 globale + 10 welt-spezifische Tipps, Anti-Repeat-Picker, 30%-Chance auf welt-spezifischen Tipp |

---

## BomberBlastLoadingPipeline

Läuft via `App.RunLoadingAsync()`. Exceptions pro Schritt werden gefangen — ein
fehlgeschlagener optionaler Schritt bricht den Start nicht ab (Splash bleibt sichtbar,
nächster Schritt wird versucht).

**Schritte** (Reihenfolge relevant — Dependencies müssen vor Konsumenten stehen):

1. `ILocalizationService.Initialize()` — Sprache aus Preferences oder Gerät
2. `IRetentionService.TouchSession()` — Session-Tracking D1/D7-Fenster
3. `ShaderEffects.Preload()` — SkSL Water-Ripple-Shader kompilieren (einmalig, GPU-Upload)
4. `BloomEffect.Preload()` — Bloom-SkSL kompilieren
5. `IGameAssetService` — erste AI-WebP-Assets vorladen (LRU-Cache aufwärmen)
6. `IProgressService` — gespeicherten Fortschritt laden
7. `ICloudSaveService.PullAsync()` — Cloud-Save holen (Local-First-Konfliktauflösung)
8. Weitere Services initialisieren (Achievement, DailyChallenge, BattlePass, …)

**Warum Shader im Start?** SkSL-Kompilierung ist GPU-Driver-abhängig (10-150ms) und
würde beim ersten Frame des ersten Levels als Stutter sichtbar sein.

---

## LoadingTips

```csharp
// Anti-Repeat-Picker: nie den gleichen Tipp zweimal in Folge
string tip = LoadingTips.GetRandomTip(worldIndex: null);    // globaler Tipp
string tip = LoadingTips.GetRandomTip(worldIndex: 3);        // 30% Chance auf Welt-3-Tipp
int total  = LoadingTips.TotalTipCount;                      // für UI-Browser
```

`LocalizationManager.GetString` als Resolve-Pfad (RESX). Eindeutige Default-Hints
pro Key — kein Spam-Risiko bei fehlendem RESX-Key.
