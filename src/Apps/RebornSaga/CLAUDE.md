# RebornSaga: Isekai Rising — Anime-RPG

Vollständig SkiaSharp-gerendertes Anime Isekai-RPG im Visual Novel Stil. Kein Avalonia-XAML im
Spielbereich — alles Szenen, Overlays und UI werden direkt auf SKCanvasView gezeichnet.

| Aspekt | Wert |
|--------|------|
| Package-ID | org.rsdigital.rebornsaga |
| Farbpalette | "Isekai System Blue" — #4A90D9 Primary, #9B59B6 Lila, #F39C12 Gold |
| Firebase | `gs://rebornsaga-671b6.firebasestorage.app/assets/` (317 Dateien, 69,2 MB) |

> Für generische Build-Befehle, Conventions, Architektur und Packaging-Patterns: [Haupt-CLAUDE.md](../../../CLAUDE.md)

---

## Architektur-Überblick

Drei Projekte, ViewModel-First, kein Service-Locator:

```
RebornSaga.Android ┐
                   ├─> RebornSaga.Shared ──> MeineApps.Core.Ava         (Preferences, Localization, ViewLocator)
RebornSaga.Desktop ┘                       ├─> MeineApps.Core.Premium.Ava (Rewarded Ads, IAP)
                                           └─> MeineApps.UI              (SkiaThemeHelper, Helpers)
```

Composition-Flow: Host (`AndroidApp` / `Program.cs`) → `RebornSaga.Shared/App.axaml.cs`
(DI-Build: 18 Services + MainViewModel) → `MainView` (60fps DispatcherTimer) → `MainViewModel`
(delegiert an `SceneManager`) → aktive `Scene` (Update/Render/Input).

---

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Namespaces | `App.axaml.cs`, Service-/VM-Registrierung, Loading-Flow | [RebornSaga.Shared](RebornSaga.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, Factories, Immersive, AdMob | [RebornSaga.Android](RebornSaga.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs` | [RebornSaga.Desktop](RebornSaga.Desktop/CLAUDE.md) |
| ViewModel | `MainViewModel` — Render/Update/Input-Delegation, Back-Press | [Shared/ViewModels](RebornSaga.Shared/ViewModels/CLAUDE.md) |
| View | `MainView` — Game-Loop, DPI-Touch, Event-Verdrahtung | [Shared/Views](RebornSaga.Shared/Views/CLAUDE.md) |
| Engine | Scene-Basisklasse, SceneManager, InputManager, Camera, Transitions | [Shared/Engine](RebornSaga.Shared/Engine/CLAUDE.md) |
| Szenen (12) | Title, Battle, Dialogue, Overworld, Inventory, … | [Shared/Scenes](RebornSaga.Shared/Scenes/CLAUDE.md) |
| Overlays (10) | Pause, LevelUp, GameOver, BacklogOverlay, … | [Shared/Overlays](RebornSaga.Shared/Overlays/CLAUDE.md) |
| Rendering | Backgrounds, Characters, Effects, Map, UI — alle SkiaSharp-Renderer | [Shared/Rendering](RebornSaga.Shared/Rendering/CLAUDE.md) |
| Services (18) | Story, Battle, Save, Audio, Asset-Delivery, Gold, RPG-Systeme | [Shared/Services](RebornSaga.Shared/Services/CLAUDE.md) |
| Models | Player, Enemy, Item, Skill, Chapter, Enums | [Shared/Models](RebornSaga.Shared/Models/CLAUDE.md) |
| Data (Embedded JSON) | Chapters, Dialogue, Maps, Skills, Items, Enemies | [Shared/Data](RebornSaga.Shared/Data/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner ohne eigene Doku: `Shared/Themes/` (AppPalette.axaml),
`Shared/Resources/Strings/` (AppStrings.resx, 6 Sprachen), `Shared/Assets/`, `Shared/Icons/`.

---

## Scene-Lifecycle (Kern-Pattern)

```
OnEnter() → Update(dt) / Render(canvas, bounds) / HandleInput() → OnPause() ↔ OnResume() → OnExit()
```

`ChangeScene<T>()` ersetzt — `PushScene<T>()` / `PopScene()` verwalten den Stack —
`ShowOverlay<T>()` / `HideOverlay()` für transparente Overlays.
Szenen werden via `ActivatorUtilities.CreateInstance<T>()` mit Constructor Injection erstellt.

---

## Szenen-Übersicht

| Szene | Beschreibung |
|-------|-------------|
| `AssetDownloadScene` | Download-Screen (Fortschrittsbalken, Partikel, Retry), wechselt automatisch zu TitleScene |
| `TitleScene` | Animierter Titelbildschirm, "Neues Spiel" vs. "Fortsetzen" (SaveGame-Erkennung) |
| `SaveSlotScene` | 3 Speicherplätze, Long-Press zum Löschen, `_isLoading`-Guard gegen Race Condition |
| `ClassSelectScene` | 3 Klassen (Schwertmeister / Arkanist / Schattenklinke) |
| `DialogueScene` | Hintergrund + Portrait + Typewriter + Choices + MangaPanel + GlitchEffect (ARIA) + Kamera-Zoom/Shake |
| `BattleScene` | Aktionsbasierter Kampf + Element-System + geführtes 5-Phasen-Tutorial (Prolog P1) |
| `OverworldScene` | Node-Map (Slay the Spire-inspiriert) mit Kamera-Pan, AI-Regions-Hintergründe |
| `InventoryScene` | Grid-Ansicht, 6 Kategorien, AI-Item-Icons mit Qualitäts-Glow |
| `ShopScene` | Kaufen/Verkaufen, Gold-Counter |
| `StatusScene` | 3 Tabs (Status / Skills / Equipment) |
| `CodexScene` | Bestiary, Lore und Charakter-Profile nach Kategorien sortiert |
| `SettingsScene` | Audio + Text-Geschwindigkeit, persistiert via `IPreferencesService` |

---

## Overlay-Übersicht

| Overlay | Beschreibung |
|---------|-------------|
| `PauseOverlay` | 7 Buttons (Fortsetzen bis Hauptmenü), Press-Feedback |
| `StatusWindowOverlay` | Solo Leveling Stil: Stats, HP/MP, Glitch-Einblendung |
| `SystemMessageOverlay` | ARIA-Nachrichten, auto-dismiss 3-5 s |
| `LevelUpOverlay` | Fanfare, Stats hochzählen, +3 Punkte verteilen |
| `FateChangedOverlay` | Glitch-Effekt, "Das Schicksal hat sich verändert..." |
| `GameOverOverlay` | Revive (Rewarded Ad) oder Speicherpunkt laden |
| `ChapterUnlockOverlay` | Gold-Kosten, Freischalten oder Video ansehen |
| `BacklogOverlay` | Scrollbare Dialog-Historie (max 200), thread-safe via `lock` |
| `EffectFeedbackOverlay` | Floating-Texte (Karma/Affinität/EXP/Gold), auto-dismiss 2,5 s, `ConsumesInput=false` |
| `TutorialOverlay` | Highlight + ARIA-Textbox, `ConsumesInput=true` blockiert BattleScene |

---

## Story-System

### JSON-basierte Kapitel (13 gesamt: P1-P3 + K1-K10)

```
Data/Chapters/chapter_{id}.json          → Kapitel-Struktur (Nodes, Verbindungen)
Data/Dialogue/{lang}/chapter_{id}.json   → Lokalisierte Texte (DE, EN, ES, FR, IT, PT)
Data/Maps/overworld_{id}.json            → Overworld-Map (Knoten + Pfade)
```

### Condition-Parser

```
karma > 50   affinity:aria >= 10   class == 1
has_item:M001   !has_item:M001   has_flag:betrayed_aldric
```

`AdvanceToNode()` überspringt Knoten wenn Condition nicht erfüllt (iterativ, Limit 100).
**`StoryEngine.SetPlayer(player)` MUSS nach SaveGame-Load und nach "Neues Spiel" aufgerufen werden.**

---

## RPG-Systeme

### Klassen

| Klasse | Stärke | Schwäche | Auto-Bonus/Level |
|--------|--------|----------|------------------|
| Schwertmeister | STR, DEF | MAG | +2 ATK |
| Arkanist | MAG, MP | DEF | +3 MP |
| Schattenklinke | AGI, LUK | HP | +5% Crit |

### Elemente (6)

Feuer > Eis > Blitz > Wind > Licht > Dunkel > Feuer (Schwäche: 1,5×, Resistenz: 0,5×)

### Gold-Economy

| Kapitel | Kosten |
|---------|--------|
| P1–P3, K1–K5 | Gratis |
| K6 | 500 Gold |
| K7 | 800 Gold |
| K8 | 1.200 Gold |
| K9 | 1.800 Gold |
| K10 | 2.700 Gold |

Gold-Quellen: Kampf-Drops, Story-Belohnungen, Rewarded Video (500 G, 3×/Tag), Daily Login.

---

## Performance-Patterns

**Kern-Regel:** Alle statischen `SKPaint` / `SKFont` / `SKMaskFilter` / `SKPath` werden als
`static readonly` Felder gehalten. `Cleanup()` ist bewusst leer — statische Ressourcen leben
für die gesamte App-Lifetime. Dispose crasht bei Wiederverwendung.
Kommentar-Pflicht: `// _glowBlur ist static readonly — NICHT disposen`

| Bereich | Pattern |
|---------|---------|
| `StatusWindowRenderer` | Gecachte Bar- und Stat-Texte, nur bei Wertänderung neu erzeugt |
| `BattleScene` | Gecachter `_cachedEnemySpriteKey`, `BackgroundCompositor.SetScene()` in `OnEnter()` |
| `SaveSlotScene` | `_isLoading`-Guard gegen async Race Condition |
| `OverworldScene` | Vorgänger-Index (Dictionary) beim Map-Load aufgebaut, O(V+E) |
| `SpriteCache` | `PeekPixels()` statt `GetPixel()` für `ComputeContentBounds` (kein JNI-Overhead) |
| `DissolveTransition` | `SKPath` als Instanzfeld mit `Rewind()` statt `new` pro Frame |
| `GroundRenderer` | Fade-Shader gecacht, nur bei Szenen-/Bounds-Wechsel neu erstellt |
| Services parallel | `LoadSkills()` + `LoadItems()` via `Task.WhenAll` in `InitializeServicesAsync` |

---

## Gotchas & Fallstricke

| Problem | Ursache | Lösung |
|---------|---------|--------|
| `SKMaskFilter` Memory Leak | `paint.MaskFilter = CreateBlur(...)` ohne Dispose des alten Filters | Gecachte `static readonly SKMaskFilter` verwenden; bei dynamischem Radius `paint.MaskFilter?.Dispose()` vor Neuzuweisung |
| Render-Loop startet nicht | `InvalidateCanvasRequested` hat beim `StartGameLoop()` noch keinen Subscriber | 3-stufige VM-Subscription: (1) `OnDataContextChanged`, (2) `OnLoaded` Backup, (3) `OnPaintSurface` Safety-Net. Zentrale `TrySubscribeToViewModel()` idempotent |
| SKCanvasView leer nach IsVisible-Toggle | `InvalidateSurface()` auf unsichtbare Canvas wird ignoriert | Nach Sichtbar-Werden Daten erneut setzen / `Calculate()` aufrufen |
| `JsonSerializer.Serialize` auf Background-Thread crasht | GameLoop modifiziert State während Serialisierung | Serialisierung auf dem UI-Thread belassen (State klein, ~5-20 ms) oder DeepCopy vor Serialize |
| Premium-Nutzer sieht Werbung nach Gerätewechsel | `PurchaseService.InitializeAsync()` nie aufgerufen | In `InitializeServicesAsync` aufrufen (Google-Play-Abgleich stellt Käufe wieder her) |
| `StoryEngine` liefert falschen State | `SetPlayer()` nach SaveGame-Load vergessen | IMMER `SetPlayer(player)` nach Load und nach "Neues Spiel" aufrufen |
| BGM-Loops kurz (12 s) | Designentscheidung — kurze Loops | `MediaPlayer.Looping=true` stellt nahtlosen Loop sicher |

---

## Lokalisierung

- **Story-Texte:** JSON in `Data/Dialogue/{lang}/` (DE, EN, ES, FR, IT, PT)
- **UI-Strings:** `ILocalizationService` per Constructor Injection in Scenes/Overlays
- **Strings im Konstruktor cachen:** `_localization.GetString("Key") ?? "Fallback"` — nie per Frame
- **Fallback:** Englisch (Base .resx), Deutsch für Story-Texte (`StoryEngine.LoadDialogueTextsAsync`)
- **AppStrings.Designer.cs** manuell gepflegt (CLI-Build generiert nicht automatisch)
- **103 RESX-Keys**, 6 Sprachen, 10 Scenes + 9 Overlays vollständig lokalisiert

---

## Premium & Ads

- Kein Banner-Ad (Vollbild-SkiaSharp-Spiel, Portrait)
- **Rewarded Ads** (6 Placements): `gold_bonus`, `time_rift`, `bonus_exp`, `revive`, `daily_prophecy`, `kodex_hint`
- **IAP:** Gold-Pakete, Zeitkristalle, `remove_ads` (Preise noch offen)

---

## Android-Konfiguration

- Portrait-only, Immersive Fullscreen
- `AudioServiceFactory` → `AndroidAudioService` (SoundPool + MediaPlayer + Vibrator)
- `RewardedAdServiceFactory` → 6 Placements
- `PurchaseServiceFactory` → Gold-Pakete + remove_ads
- Double-Back-to-Exit via `BackPressHelper`
- `OnDestroy`: `App.DisposeServices()` → Audio, SpriteCache, SQLite freigeben

---

## AI-Asset-Pipeline (ComfyUI)

Sprites werden mit ComfyUI + Animagine XL 4.0 generiert. Pro Charakter ein LoRA-Modell für
dauerhafte Konsistenz über Posen, Emotionen und Outfits hinweg.

| Parameter | Wert |
|-----------|------|
| Auflösung | 832×1216 (Portrait), 1216×832 (Landscape), 512×512 (Icons) |
| Format | WebP q90, 1248×1824 für Full-Body-Sprites |
| Sampler | euler_ancestral, 28 Steps, CFG 7.0 |
| LoRA Training | 20 Bilder, 20 Repeats, 10 Epochen = 4.000 Steps |

**11 Charakter-LoRAs fertig** (Aria, Protagonist Schwert/Magier/Assassin, Luna, Kael, Aldric,
System ARIA, Vex, Nihilus, Xaroth). Gespeichert in `F:\AI\kohya_ss\`.

**Chroma-Key-Regel:** Dunkle Chars (Nihilus, Xaroth, Vex, Kael, Aldric, Sword, Assassin) →
Green Screen + Chroma-Key. Helle Chars (Aria, Luna, System Aria, Mage) → White BG + BiRefNet.

**Workflow-Scripts** in `F:\AI\ComfyUI_workflows\`: `regenerate_all_assets.py`,
`generate_manifest.py`, `upload_assets.py` (Firebase Uniform Bucket Access, kein `make_public()`).

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
| Build, Conventions, Architektur | [Haupt-CLAUDE.md](../../../CLAUDE.md) |
| Preferences, BackPressHelper, ViewLocator | [MeineApps.Core.Ava](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) |
| Rewarded Ads, IAP, Linked-Files | [MeineApps.Core.Premium.Ava](../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md) |
| SkiaSharp-Renderer, Paint-Lifecycle, DPI | [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md) |
