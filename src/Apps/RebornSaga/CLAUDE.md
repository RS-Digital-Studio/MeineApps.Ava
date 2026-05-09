# RebornSaga: Isekai Rising — Anime-RPG

Vollständig SkiaSharp-gerendertes Anime Isekai-RPG im Visual Novel Stil. Kein Avalonia-XAML im
Spielbereich — alles Szenen, Overlays und UI werden direkt auf SKCanvasView gezeichnet.

| Aspekt | Wert |
|--------|------|
| Aktuelle Version | v1.0.0 |
| Package-ID | org.rsdigital.rebornsaga |
| Modus | In Entwicklung (Android-Test ausstehend) |
| Farbpalette | "Isekai System Blue" — #4A90D9 Primary, #9B59B6 Lila, #F39C12 Gold |
| Firebase | `gs://rebornsaga-671b6.firebasestorage.app/assets/` (317 Dateien, 69,2 MB) |

> Für Build-Befehle, Conventions, Troubleshooting und Packaging-Patterns: [Haupt-CLAUDE.md](../../../CLAUDE.md)

---

## Architektur: Szenen-basierte SkiaSharp-Engine

```
MainView (SKCanvasView, 60fps DispatcherTimer)
  └── MainViewModel (Update + Render + Input-Delegation)
        └── SceneManager (Scene-Stack + Overlays + Transitions)
              ├── Scene (abstrakt: Update, Render, HandleInput, Lifecycle)
              ├── TransitionEffect (Fade, Slide, Glitch, Dissolve, MangaWipe, Iris)
              └── InputManager (Pointer → InputAction: Tap, Hold, Swipe, Drag)
```

### Scene-Lifecycle

```
OnEnter() → Update(dt) / Render(canvas, bounds) / HandleInput() → OnPause() ↔ OnResume() → OnExit()
```

| Methode | Wann |
|---------|------|
| `ChangeScene<T>()` | Ersetzt aktive Szene (alte: OnExit, neue: OnEnter) |
| `PushScene<T>()` | Szene drüberlegen (alte: OnPause) |
| `PopScene()` | Obere entfernen (untere: OnResume) |
| `ShowOverlay<T>()` / `HideOverlay()` | Transparente Overlays |

`ConsumesInput` — virtuelle Property (default: `true`). Bei `false` wird Input an die darunterliegende
Szene durchgereicht (Beispiel: `EffectFeedbackOverlay`).

Szenen werden per `ActivatorUtilities.CreateInstance<T>()` mit Constructor Injection erstellt.

### Camera (Engine/Camera.cs)

Viewport-Kamera mit Pan, Zoom und Screen-Shake. Properties: `X`, `Y`, `Zoom`.
Methoden: `Shake(intensity, duration)`, `Update(dt)`, `ApplyTransform(canvas, bounds)`.

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

## Rendering-System

### Charakter-Sprites (AI-Only)

Rein Sprite-basiertes System. Kein prozeduraler Fallback — fehlendes Asset = nichts gezeichnet.
Prozedurales Legacy-System (CharacterParts, FaceRenderer, HairRenderer usw.) wurde entfernt.

**Sprite-Pipeline:**

| Klasse | Aufgabe |
|--------|---------|
| `SpriteCharacterRenderer` | Komplette Bilder, unabhängiges Blinzeln pro Charakter, Crossfade 150 ms, Mund-Animation (3 Frames) |
| `SpriteCache` (Service) | LRU-Cache (max 30 Bilder), thread-safe, `IDisposable`, Preload-Support |
| `CharacterRenderer` | Fassade: `DrawPortrait` / `DrawFullBody` / `DrawIcon`, aktiv/inaktiv-Dimming |
| `CharacterDefinitions` | 10 Definitionen (3 Protagonist-Klassen + 5 NPCs + 2 Bosse), `GetById()` |
| `SpriteDefinitions` | `Pose`-Enum (Standing/Battle/Sitting/Kneeling/Floating/Lying/Running) |
| `SpriteAssetPaths` | Pfad-Konventionen aller Asset-Typen |

**Asset-Pfade:**
```
characters/{charId}/full/{pose}_{emotion}.webp
characters/{charId}/overlays/blink.webp
characters/{charId}/overlays/mouth_open.webp
characters/{charId}/overlays/mouth_wide.webp
enemies/{enemyId}.webp
backgrounds/{sceneKey}.webp
scenes/{sceneId}.webp          → Animated WebP (Cutscenes)
items/{category}/{itemId}.webp
map/nodes/{nodeType}.webp
map/regions/{chapterId}.webp
```

**Asset-Delivery:**
- `AssetDeliveryService` — Firebase Storage REST API, SHA256-Hash-Verifikation, Delta-Updates
- Stream-basierter Download mit Retry (3× exponentieller Backoff), temporäre Dateien
- `AssetManifest` beschreibt alle Packs (characters, backgrounds, enemies, items, scenes)

**AnimatedWebPRenderer:** SKCodec-basiertes Frame-für-Frame-Rendering für Cutscenes.
Loop-Support, gecachte Frame-Bitmap, Frame-Timing aus WebP-Metadaten.

### Hintergründe (Multi-Layer Komposition)

`BackgroundCompositor` orchestriert 6 Layer-Renderer rund um Charakter-Rendering:

```
BackgroundCompositor.RenderBack()      // Sky + Elements + Ground + PointLights
BackgroundCompositor.BeginLighting()   // SaveLayer mit Ambient ColorFilter
  // ... Charaktere rendern ...
BackgroundCompositor.EndLighting()     // Restore (Ambient-Tönung angewendet)
BackgroundCompositor.RenderFront()     // Foreground + Partikel
```

| Layer-Renderer | Aufgabe |
|----------------|---------|
| `SkyRenderer` | 3-Farben vertikaler LinearGradient, Shader-Caching |
| `ElementRenderer` | 24 Silhouetten-Typen (Bäume, Gebäude, Felsen, Architektur, Innenraum, Spezial) |
| `GroundRenderer` | Boden-Band mit Gradient-Übergang, 6 Texturen (Grass, Stone, Wood, Sand, Snow, Water) |
| `LightingRenderer` | Ambient (SaveLayer + ColorMatrix) + PointLight (radiale Gradienten, Flicker) |
| `SceneParticleRenderer` | 12 deterministische Partikel-Typen (kein Heap-State) |
| `ForegroundRenderer` | 5 Typen über Charakteren (GrassBlade, Fog, Branch, Cobweb, LightRay) mit Safezone-Clip |

`SceneDef` (positional record) + `SceneDefinitions` (14 statische Szenen, Dictionary case-insensitive).
Rückwärtskompatible Keys: `"forest"→ForestDay`, `"dungeon"→DungeonHalls` usw.

**14 Szenen:** SystemVoid, Title, ForestDay, ForestNight, Campfire, VillageSquare, VillageTavern,
DungeonHalls, DungeonBoss, TowerLibrary, TowerSummit, Battlefield, CastleHall, Dreamworld.

### Effekte

| System | Beschreibung |
|--------|-------------|
| `ParticleSystem` | Struct-basiert, 11 Presets (MagicSparkle, LevelUpGlow, SystemGlitch, BloodSplatter, AmbientFloat + 6 Element-Presets: FireBurst, IceShard, LightningStrike, WindGust, HolyLight, ShadowVoid) |
| `GlitchEffect` | Horizontale Verschiebung + RGB-Split |
| `ScreenShake` | Canvas-Translation (3 px normal, 5 px kritisch) |
| `MangaPanelRenderer` | Screen in Panels splitten |
| `SplashArtRenderer` | Charakter-Portrait bei Ultimates |

### UI-Renderer

| Klasse | Aufgabe |
|--------|---------|
| `UIRenderer` | Buttons, TextWithShadow, ProgressBars, HitTest — statisch, gepoolte Paints |
| `DialogBoxRenderer` | Halbtransparente Box, Sprecher-Name, Weiter-Indikator |
| `TypewriterRenderer` | Buchstabe-für-Buchstabe (4 Geschwindigkeiten) |
| `ChoiceButtonRenderer` | 2-4 Optionen vertikal, Tags ([Karma+], [STR Check]) |
| `StatusWindowRenderer` | Solo Leveling Stil: dunkler BG, blaue Glow-Ränder |

### Map-Renderer

- `OverworldRenderer` — Kapitel-Map mit Nodes und Pfaden, AI-Regions-Hintergrund
- `NodeRenderer` — Knoten-Typen (Story, Boss, Sidequest, Rest, Shop), AI-Icons via SpriteCache
- `PathRenderer` — Verbindungslinien (freigeschaltet/gesperrt)

---

## Story-System

### JSON-basierte Kapitel (13 gesamt: P1-P3 + K1-K10)

```
Data/Chapters/chapter_{id}.json          → Kapitel-Struktur (Nodes, Verbindungen)
Data/Dialogue/{lang}/chapter_{id}.json   → Lokalisierte Texte (DE, EN, ES, FR, IT, PT)
Data/Maps/overworld_{id}.json            → Overworld-Map (Knoten + Pfade)
```

### StoryEngine (Service)

Navigiert durch Knoten, prüft Conditions und wendet StoryEffects an.
`SetPlayer(player)` muss nach SaveGame-Load oder "Neues Spiel" aufgerufen werden.

**Condition-Parser unterstützt:**
- Vergleiche: `karma > 50`, `affinity:aria >= 10`, `class == 1`
- Item-Check: `has_item:M001`, `!has_item:M001`
- Flag-Check: `has_flag:betrayed_aldric`, `!has_flag:betrayed_aldric`
- Einwort-Flags: `alliance_aria` (Fallback auf `Flags.Contains`)

`AdvanceToNode()` überspringt Knoten wenn Condition nicht erfüllt (iterativ, Limit 100).

**ApplyEffects() verarbeitet:**
- `karma` → `FateTrackingService.ModifyKarma()` + `Player.Karma` sync
- `exp` → `ProgressionService.AwardExp()` (Level-Up Events)
- `gold` → `GoldService.AddGold/SpendGold()` (Minimum 0 Clamp)
- `affinity` → Engine + `Player.Affinities` sync
- `addItems`/`removeItems` → `InventoryService` + Engine + `Player.Inventory` sync
- `setFlags`/`removeFlags` → Engine + `Player.Flags` + `FateTrackingService` sync
- `fateChanged` → `FateChangeTriggered` Event

**Knoten-Typen:** Dialogue, Choice, Battle, ClassSelect, Shop, Cutscene, Overworld, BondScene,
FateChange, SystemMessage, ChapterEnd.

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

### Battle-System

**Kampf-Phasen (BattlePhase Enum):**
```
Intro → PlayerTurn → (Attack | Dodge | SkillSelect | ItemSelect | PlayerSkillAttack)
      → EnemyTurn → (Victory | Defeat | BossPhaseChange | Done)
```

`BossPhaseChange` — Boss wechselt Phase (Mini-Cutscene, volle HP).
`Done` — Kampf abgeschlossen, keine weitere Interaktion.

**Tutorial (Prolog P1, Gegner B001):**
- Aktivierung: `enemy.Id == "B001" && TutorialService.ShouldShow("FirstBattle")`
- 5 Phasen (_tutorialStep 0-5): Intro → Angriff → Skill → Item → Ausweichen → Frei
- `IsTutorialActionEnabled()` erlaubt pro Phase nur die zu lernende Aktion
- Schaden-Override: Vor Phase 3 max 10% HP, Phase 4 erzwingt erfolgreichen Dodge
- Abschluss: `TutorialService.MarkSeen("FirstBattle")`

### Services-Übersicht

| Service | Zweck |
|---------|-------|
| `StoryEngine` | Kapitel-Navigation, Conditions, Effekte (EXP/Gold/Karma/Flags) |
| `BattleEngine` | Schadensberechnung, Element-System, Crits |
| `SkillService` | 15 Skills/Klasse, 5 Stufen, Freischaltung per Level |
| `InventoryService` | Items verwalten, Ausrüsten, Stack-Verwaltung |
| `AffinityService` | Bond-Stufen (0-100) für 5 NPCs |
| `FateTrackingService` | Karma (-100 bis +100), Entscheidungs-Log, FateFlags |
| `CodexService` | Enzyklopädie (Charaktere, Orte, Lore) |
| `ProgressionService` | EXP, Level-Up, Stat-Verteilung |
| `SaveGameService` | SQLite, 3 Slots, Auto-Save bei Knoten-Wechsel, `SemaphoreSlim` + `IDisposable` |
| `GoldService` | Gold verwalten, Rewarded-Video-Cooldown (3×/Tag) |
| `ChapterUnlockService` | K6-K10 per Gold, `SemaphoreSlim` gegen Doppel-Unlock |
| `AudioService` | SFX (SoundPool, 28 GameSfx) + BGM (MediaPlayer, 10 BgmTracks) |
| `TutorialService` | Erstbesucher-Hints per Preferences |
| `DailyService` | Login-Bonus (Gold), Prophezeiung (RESX-Keys Prophecy_0-13), Streak |
| `EnemyLoader` | Lazy-Load aus enemies.json, `GetById()` gibt geklonten Enemy zurück |
| `AssetDeliveryService` | Firebase Storage REST API, SHA256-Verifikation, Delta-Updates |

### Gold-Economy

| Kapitel | Kosten | Kategorie |
|---------|--------|-----------|
| P1-P3 | Gratis | Prolog |
| K1-K5 | Gratis | Arc 1 (frei) |
| K6 | 500 Gold | Arc 1 (kostenpflichtig) |
| K7 | 800 Gold | Arc 1 (kostenpflichtig) |
| K8 | 1.200 Gold | Arc 1 (kostenpflichtig) |
| K9 | 1.800 Gold | Arc 1 (kostenpflichtig) |
| K10 | 2.700 Gold | Arc 1 (kostenpflichtig) |

Gold-Quellen: Kampf-Drops, Story-Belohnungen, Rewarded Video (500 G, 3×/Tag), Daily Login.

---

## Performance-Patterns

**Kern-Regel:** Alle statischen `SKPaint` / `SKFont` / `SKMaskFilter` / `SKPath` werden als
`static readonly` Felder gehalten. `Cleanup()` ist bewusst leer — statische Ressourcen leben
für die gesamte App-Lifetime. Dispose crasht bei Wiederverwendung.

| Bereich | Pattern |
|---------|---------|
| `StatusWindowRenderer` | Gecachte Bar- und Stat-Texte, nur bei Wertänderung neu erzeugt |
| `BattleScene` | Gecachter `_cachedEnemySpriteKey`, `BackgroundCompositor.SetScene()` in `OnEnter()` |
| `SaveSlotScene` | Gecachte Slot-Labels, `_isLoading`-Guard gegen async Race Condition |
| `OverworldScene` | Vorgänger-Index (Dictionary) beim Map-Load aufgebaut, `MarkPredecessorsCompleted` O(V+E) |
| `SpriteCache` | `PeekPixels()` statt `GetPixel()` für `ComputeContentBounds` (kein JNI-Overhead) |
| `DissolveTransition` | `SKPath` als Instanzfeld mit `Rewind()` statt `new` pro Frame |
| `GroundRenderer` | Fade-Shader gecacht, nur bei Szenen-/Bounds-Wechsel neu erstellt |
| Services parallel | `LoadSkills()` + `LoadItems()` via `Task.WhenAll` in `InitializeServicesAsync` |

**SKMaskFilter-Konvention:** Statische `static readonly SKMaskFilter` werden in `Cleanup()` NICHT
disposed. Kommentar-Pflicht: `// _glowBlur ist static readonly — NICHT disposen`

---

## Gotchas & Fallstricke

| Problem | Ursache | Lösung |
|---------|---------|--------|
| `SKMaskFilter` Memory Leak | `paint.MaskFilter = CreateBlur(...)` ohne Dispose des alten Filters | Gecachte `static readonly SKMaskFilter` verwenden; bei dynamischem Radius `paint.MaskFilter?.Dispose()` vor Neuzuweisung |
| Render-Loop startet nicht (Countdown stuck) | `InvalidateCanvasRequested` hat beim `StartGameLoop()` noch keinen Subscriber (ContentControl+ViewLocator setzt DataContext verzögert) | 3-stufige VM-Subscription: (1) OnDataContextChanged, (2) OnLoaded Backup, (3) OnPaintSurface Safety-Net. Zentrale `TrySubscribeToViewModel()`-Methode (idempotent) |
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
- **135 RESX-Keys**, 6 Sprachen, 10 Scenes + 9 Overlays vollständig lokalisiert

---

## Premium & Ads

- Kein Banner-Ad (Vollbild-SkiaSharp-Spiel, Landscape/Portrait)
- **Rewarded Ads** (6 Placements): `gold_bonus`, `time_rift`, `bonus_exp`, `revive`, `daily_prophecy`, `kodex_hint`
- **IAP:** Gold-Pakete, Zeitkristalle, `remove_ads` (Preise noch offen)

---

## Daten-Dateien

| Pfad | Inhalt |
|------|--------|
| `Data/Chapters/` | 13 Kapitel-JSONs (P1-P3, K1-K10) |
| `Data/Dialogue/{lang}/` | Dialog-Texte pro Sprache |
| `Data/Maps/` | 13 Overworld-Map-JSONs |
| `Data/Skills/` | 3 Skill-JSONs (swordmaster, arcanist, shadowblade) |
| `Data/Items/` | items.json (Waffen, Rüstungen, Consumables, Key-Items) |
| `Data/Enemies/` | enemies.json (Gegner + Bosse) |

---

## Audio-Assets (Android)

| Pfad | Inhalt | Anzahl |
|------|--------|--------|
| `Assets/Sounds/*.ogg` | SFX (SoundPool, GameSfx-Enum) | 28 |
| `Assets/Music/*.ogg` | BGM (MediaPlayer, BgmTracks-Konstanten) | 10 |

Dateinamen-Mapping in `AndroidAudioService.SoundFileMap` (SFX) und `BgmFileMap` (BGM).

---

## Loading-Pipeline & Android-Besonderheiten

### InitializeServicesAsync (App.axaml.cs)

Wird von `MainView.OnAttachedToVisualTree` via `MainViewModel.InitializeAsync()` aufgerufen.
`LoadSkills()` + `LoadItems()` parallel via `Task.WhenAll` (Voraussetzung für `SaveGameService.LoadGameAsync`).
`IPurchaseService.InitializeAsync()` fire-and-forget.

### DisposeServices

Desktop: `ShutdownRequested` | Android: `MainActivity.OnDestroy`.
Disposed: `IAudioService` (SoundPool + MediaPlayer), `SaveGameService` (SQLite `CloseAsync`).

### Android-Konfiguration

- Portrait-only, Immersive Fullscreen
- `AudioServiceFactory` → `AndroidAudioService` (SoundPool + MediaPlayer + Vibrator)
- `RewardedAdServiceFactory` → 6 Placements
- `PurchaseServiceFactory` → Gold-Pakete + remove_ads
- Double-Back-to-Exit via `BackPressHelper`

---

## Thread-Safety

| Komponente | Mechanismus |
|-----------|-------------|
| `BacklogOverlay` | `lock` auf Entries-Liste (Add/Clear/Render) |
| `ChapterUnlockService` | `SemaphoreSlim(1,1)` gegen Doppel-Unlock |
| `SaveGameService` | `SemaphoreSlim(1,1)` gegen parallele Saves + `IDisposable` (SQLite `CloseAsync`) |
| `GoldService` | `Math.Clamp(0, int.MaxValue)` in AddGold/RemoveGold |

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

**Workflow-Scripts** in `F:\AI\ComfyUI_workflows\`:
`regenerate_all_assets.py`, `generate_manifest.py`, `upload_assets.py` (Firebase Uniform Bucket
Access, kein `make_public()`).

---

## Build & Test

```bash
# Shared-Projekt bauen
dotnet build src/Apps/RebornSaga/RebornSaga.Shared

# Desktop (Schnell-Test ohne Android)
dotnet run --project src/Apps/RebornSaga/RebornSaga.Desktop

# Android
dotnet build src/Apps/RebornSaga/RebornSaga.Android

# AppChecker
dotnet run --project tools/AppChecker RebornSaga
```

Nächste Schritte: Android-Test auf physischem Gerät. Release nur auf Anfrage.
