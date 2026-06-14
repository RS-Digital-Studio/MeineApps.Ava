# RebornSaga: Isekai Rising — Anime-RPG

Vollständig SkiaSharp-gerendertes Anime Isekai-RPG im Visual-Novel-Stil. **Kein Avalonia-XAML im
Spielbereich** — alle Szenen, Overlays und UI werden direkt auf eine einzige `SKCanvasView`
gezeichnet. Diese Datei beschreibt App-Identität und das spielweite Game-Design (Klassen,
Elemente, Economy, Premium). Aufbau pro Bereich → die Unterordner-Doku.

| Aspekt | Wert |
|--------|------|
| Package-ID | org.rsdigital.rebornsaga |
| Farbpalette | "Isekai System Blue" — #4A90D9 Primary, #9B59B6 Lila, #F39C12 Gold (`Themes/AppPalette.axaml`) |
| Firebase-Assets | `gs://rebornsaga-671b6.firebasestorage.app/assets/` (317 Dateien, 69,2 MB) |

> Build-Befehle, generische Conventions (MVVM, DI, DateTime, Localization-Mechanik),
> Architektur-Regeln → [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Architektur-Überblick

Drei Projekte, ViewModel-First, kein Service-Locator:

```
RebornSaga.Android ┐
                   ├─> RebornSaga.Shared ──> MeineApps.Core.Ava          (Preferences, Localization, ViewLocator)
RebornSaga.Desktop ┘                       ├─> MeineApps.Core.Premium.Ava (Rewarded Ads, IAP)
                                           └─> MeineApps.UI               (SkiaThemeHelper, Helpers)
```

Composition-Flow: Host (`AndroidApp` / `Program.cs`) → `App.axaml.cs` (DI-Build, 18 Services +
`MainViewModel`) → `MainView` (~30fps `DispatcherTimer`, Bedarfs-Rendering) → `MainViewModel` (delegiert an
`SceneManager`) → aktive `Scene` (Update/Render/Input). Details der Verdrahtung →
[RebornSaga.Shared/CLAUDE.md](RebornSaga.Shared/CLAUDE.md).

---

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Namespaces, Loading-Flow | `App.axaml.cs`, Service-/VM-Registrierung | [RebornSaga.Shared](RebornSaga.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, Factories, Immersive, AdMob, `OnDestroy` | [RebornSaga.Android](RebornSaga.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs` | [RebornSaga.Desktop](RebornSaga.Desktop/CLAUDE.md) |
| ViewModel | `MainViewModel` — Render/Update/Input-Delegation, Back-Press | [Shared/ViewModels](RebornSaga.Shared/ViewModels/CLAUDE.md) |
| View | `MainView` — Game-Loop, DPI-Touch, Event-Verdrahtung | [Shared/Views](RebornSaga.Shared/Views/CLAUDE.md) |
| Engine | Scene-Basisklasse, SceneManager, InputManager, Camera, Transitions | [Shared/Engine](RebornSaga.Shared/Engine/CLAUDE.md) |
| Szenen (12) | Title, Battle, Dialogue, Overworld, Inventory, … + Performance-Gotchas | [Shared/Scenes](RebornSaga.Shared/Scenes/CLAUDE.md) |
| Overlays (10) | Pause, LevelUp, GameOver, Backlog, … + ConsumesInput-Regeln | [Shared/Overlays](RebornSaga.Shared/Overlays/CLAUDE.md) |
| Rendering | Backgrounds, Characters, Effects, Map, UI — SkiaSharp-Renderer, statische Paints | [Shared/Rendering](RebornSaga.Shared/Rendering/CLAUDE.md) |
| Services (18) | Story, Battle, Save, Audio, Asset-Delivery, Gold, RPG-Systeme + Thread-Safety | [Shared/Services](RebornSaga.Shared/Services/CLAUDE.md) |
| Models | Player, Enemy, Item, Skill, Chapter, Enums | [Shared/Models](RebornSaga.Shared/Models/CLAUDE.md) |
| Data (Embedded JSON) | Chapters, Dialogue, Maps, Skills, Items, Enemies | [Shared/Data](RebornSaga.Shared/Data/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner ohne eigene Doku: `Shared/Themes/`, `Shared/Resources/Strings/`
(`AppStrings.resx`, 6 Sprachen), `Shared/Assets/`, `Shared/Icons/` (`RebornSaga.Icons`).

---

## Game-Design (spielweit)

Diese Systeme spannen die ganze App auf — die Berechnungslogik dazu lebt in
[Shared/Services](RebornSaga.Shared/Services/CLAUDE.md) (`BattleEngine`, `ProgressionService`,
`GoldService`, `ChapterUnlockService`, `SkillService`).

### Klassen

| Klasse | Stärke | Schwäche | Auto-Bonus/Level |
|--------|--------|----------|------------------|
| Schwertmeister | STR, DEF | MAG | +2 ATK |
| Arkanist | MAG, MP | DEF | +3 MP |
| Schattenklinke | AGI, LUK | HP | +5% Crit |

### Elemente (6)

Feuer > Eis > Blitz > Wind > Licht > Dunkel > Feuer (Schwäche: 1,5×, Resistenz: 0,5×).
Implementierung in `BattleEngine`.

### Gold-Economy

Kapitel P1–P3 und K1–K5 sind gratis. Kostenpflichtige Kapitel (`ChapterUnlockService`):

| Kapitel | Kosten |
|---------|--------|
| K6 | 500 Gold |
| K7 | 800 Gold |
| K8 | 1.200 Gold |
| K9 | 1.800 Gold |
| K10 | 2.700 Gold |

Gold-Quellen: Kampf-Drops, Story-Belohnungen, Rewarded Video (500 G, 3×/Tag via `GoldService`),
Daily Login (`DailyService`).

### Story-Struktur

13 Kapitel (P1–P3 + K1–K10) als Embedded JSON. Kapitel-Navigation, Condition-Parser und
StoryEffects → `StoryEngine` ([Shared/Services](RebornSaga.Shared/Services/CLAUDE.md)),
JSON-Layout → [Shared/Data](RebornSaga.Shared/Data/CLAUDE.md).

---

## Premium & Ads

- **Kein Banner** (Vollbild-SkiaSharp-Spiel, Portrait).
- **Rewarded Ads (6 Placements):** `gold_bonus`, `time_rift`, `bonus_exp`, `revive`,
  `daily_prophecy`, `kodex_hint`.
- **IAP:** Gold-Pakete, Zeitkristalle, `remove_ads` (Preise noch offen).

Ad-/Billing-Mechanik → [MeineApps.Core.Premium.Ava](../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md).

---

## App-spezifische Regeln

| Regel | Begründung |
|-------|------------|
| `RequestedThemeVariant = Dark` fest, kein Theme-Wechsel | Spiel ist durchgängig dunkel gestaltet |
| Statische `SKPaint`/`SKFont`/`SKMaskFilter`/`SKPath` als `static readonly`, `Cleanup()` bleibt leer | Renderer leben für die App-Lifetime; Dispose crasht bei Wiederverwendung (Detail → [Shared/Rendering](RebornSaga.Shared/Rendering/CLAUDE.md)) |
| `StoryEngine.SetPlayer(player)` nach jedem Load und nach "Neues Spiel" | Engine liefert sonst falschen State (Detail → [Shared/Services](RebornSaga.Shared/Services/CLAUDE.md)) |
| `SkillService.LoadSkills()` + `InventoryService.LoadItems()` vor `SaveGameService.LoadGameAsync()` | Save-Daten referenzieren Skill-/Item-IDs |

---

## AI-Asset-Pipeline (ComfyUI)

Sprites werden mit ComfyUI + Animagine XL 4.0 generiert (pro Charakter ein LoRA für Konsistenz),
als WebP nach Firebase Storage hochgeladen. Vollständige Pipeline (LoRA-Liste, Chroma-Key-Regeln,
Workflow-Scripts unter `F:\AI\ComfyUI_workflows\`) → Memory `comfyui-pipeline.md` und
`reborn-saga-visuals.md`. Asset-Delivery zur Laufzeit → `AssetDeliveryService`
([Shared/Services](RebornSaga.Shared/Services/CLAUDE.md)).

---

## Build & Test

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
dotnet run   --project src/Apps/RebornSaga/RebornSaga.Desktop
dotnet build src/Apps/RebornSaga/RebornSaga.Android
dotnet run   --project tools/AppChecker RebornSaga
```

---

## Verweise

| Was | Wo |
|-----|----|
| Build, Conventions, Architektur, Localization-Mechanik | [Haupt-CLAUDE.md](../../../CLAUDE.md) |
| Preferences, BackPressHelper, ViewLocator | [MeineApps.Core.Ava](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) |
| Rewarded Ads, IAP, Linked-Files | [MeineApps.Core.Premium.Ava](../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md) |
| SkiaSharp-Renderer, Paint-Lifecycle, DPI, MaskFilter-Leak | [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md) |
