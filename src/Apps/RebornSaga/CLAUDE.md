# Reborn Saga: Isekai Rising (Avalonia)

> Für Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Anime Isekai-RPG im Visual Novel Stil mit SkiaSharp-Engine. Der Spieler erwacht als gefallener Held
ohne Erinnerung in einer Game-World - mit Status-Fenstern (Solo Leveling inspiriert), Klassenwahl,
aktionsbasierten Kämpfen, NPCs und verzweigten Story-Pfaden.

**Version:** 1.0.0 | **Package-ID:** org.rsdigital.rebornsaga | **Status:** In Entwicklung

## Farbpalette ("Isekai System Blue")

| Farbe | Hex | Zweck |
|-------|-----|-------|
| Primary | #4A90D9 | System-Blau, UI-Glow, Mana |
| Secondary | #9B59B6 | Mystisches Lila, Magie |
| Accent | #F39C12 | Gold, EXP, Belohnungen |
| Health | #E74C3C | HP-Bars, Schaden |
| Background | #0D1117 | Dunkles UI (GitHub-Dark) |
| Surface | #161B22 | Karten, Panels |
| SystemGlow | #58A6FF | ARIA System-Fenster |

## Architektur

### Szenen-basierte SkiaSharp-Engine

Komplett SkiaSharp-gerendert (kein Avalonia XAML außer Host-View). Szenen-Stack mit Transitions.

```
MainView (SKCanvasView, 60fps DispatcherTimer)
  └─ MainViewModel (Update + Render + Input Delegation)
       └─ SceneManager (Scene-Stack + Overlays + Transitions)
            ├─ Scene (abstrakt: Update, Render, HandleInput, Lifecycle)
            ├─ TransitionEffect (Fade, Slide, Glitch, Dissolve, MangaWipe, Iris)
            └─ InputManager (Pointer→InputAction: Tap, Hold, Swipe, Drag)
```

### Scene-Lifecycle

```
OnEnter() → Update(dt) / Render(canvas, bounds) / HandleInput() → OnPause() ↔ OnResume() → OnExit()
```

- `ChangeScene<T>()` - Ersetzt aktive Szene (alte OnExit, neue OnEnter)
- `PushScene<T>()` - Szene drüber legen (alte OnPause)
- `PopScene()` - Obere entfernen (untere OnResume)
- `ShowOverlay<T>()` / `HideOverlay()` - Transparente Overlays
- `ConsumesInput` - Virtuelle Property (default: true). Bei `false` wird Input an darunterliegende Szene durchgereicht (z.B. EffectFeedbackOverlay)

### Szenen per DI erstellt

`ActivatorUtilities.CreateInstance<T>()` - Constructor Injection für Services.

## Szenen-Übersicht

| Szene | Beschreibung |
|-------|-------------|
| AssetDownloadScene | Download-Screen für AI-Assets (Fortschrittsbalken, Partikel, Retry bei Fehler), automatischer Wechsel zu TitleScene |
| TitleScene | Animierter Titelbildschirm, "Neues Spiel" vs "Fortsetzen" (SaveGame-Erkennung) |
| SaveSlotScene | 3 Speicherplätze, Long-Press zum Löschen |
| ClassSelectScene | 3 Klassen (Schwertmeister/Arkanist/Schattenklinke) |
| DialogueScene | Hintergrund + Portrait + Typewriter + Choices + MangaPanel-Modus + GlitchEffect (ARIA) + Kamera-Zoom/Shake |
| BattleScene | Aktions-basierter Kampf (Angriff/Ausweichen/Skill/Item) + AI-Enemy-Sprites (Fallback prozedural) + Angriffs-Animation, Dodge-Ghosting, SplashArt bei Ultimates |
| OverworldScene | Node-Map (Slay the Spire-inspiriert) mit Kamera-Pan, AI-Regions-Hintergründe |
| InventoryScene | Grid-Ansicht, 6 Kategorien, AI-Item-Icons mit Qualitäts-Glow, Ausrüsten/Benutzen |
| ShopScene | Kaufen/Verkaufen, Gold-Counter |
| StatusScene | 3 Tabs (Status/Skills/Equipment) |
| SettingsScene | Audio + Text-Geschwindigkeit, persistiert via IPreferencesService |

## Overlay-Übersicht

| Overlay | Beschreibung |
|---------|-------------|
| PauseOverlay | 7 Buttons (Fortsetzen bis Hauptmenü), Press-Feedback |
| StatusWindowOverlay | Solo Leveling Stil: Stats, HP/MP, Glitch-Ein |
| SystemMessageOverlay | ARIA-Nachrichten, auto-dismiss 3-5s |
| LevelUpOverlay | Fanfare, Stats hochzählen, +3 Punkte verteilen |
| FateChangedOverlay | Glitch-Effekt, "Das Schicksal hat sich verändert..." |
| GameOverOverlay | Revive (Rewarded Ad) oder Speicherpunkt laden |
| ChapterUnlockOverlay | Gold-Kosten, Freischalten oder Video ansehen |
| BacklogOverlay | Scrollbare Dialog-Historie (max 200), thread-safe, Scroll-Support |
| TutorialOverlay | Highlight + ARIA-Textbox |

## Rendering-System

### Charakter-Rendering (AI-Sprites Only)

Rein Sprite-basiertes System: AI-generierte Einzelbilder pro Pose+Emotion. KEIN Fallback auf prozedurales Rendering.

**Sprite-Pipeline:**
- `SpriteDefinitions` - Pose-Enum (Standing/Battle/Sitting/Kneeling/Floating/Lying/Running), CharacterSpriteData, OverlayInfo
- `SpriteAssetPaths` - Pfad-Konventionen: `characters/{id}/full/{pose}_{emotion}.webp` + Overlays
- `SpriteCharacterRenderer` - Statische Klasse: Komplette Bilder, Per-Character AnimState (unabhängiges Blinzeln), Crossfade, Idle-Breathing, Mund-Animation (3 Frames)
- `SpriteCache` (Service) - LRU-Cache (max 30 Einzelbilder), thread-safe, IDisposable, Preload-Support. Methoden: GetSprite, GetEnemySprite, GetBackground, GetItemIcon, GetMapNodeIcon
- `CharacterRenderer` - Fassade: DrawPortrait/DrawFullBody/DrawIcon, aktiv/inaktiv-Dimming. Ohne Sprites wird NICHTS gezeichnet
- `CharacterDefinitions` - 10 Definitionen (3 Protagonist-Klassen + 5 NPCs + 2 Bosse) + `GetById()` Lookup

**Asset-Pfade:**
```
characters/{charId}/full/{pose}_{emotion}.webp   → Komplettes Charakter-Bild
characters/{charId}/overlays/blink.webp            → Blinzel-Overlay
characters/{charId}/overlays/mouth_open.webp       → Mund-Overlay (offen)
characters/{charId}/overlays/mouth_wide.webp       → Mund-Overlay (weit)
enemies/{enemyId}.webp                             → Gegner-Sprite
backgrounds/{sceneKey}.webp                        → AI-generierter Hintergrund
scenes/{sceneId}.webp                              → Animated WebP (Cutscenes)
items/{category}/{itemId}.webp                     → Item-Icons
map/nodes/{nodeType}.webp                          → Map-Node-Icons (story, boss, etc.)
map/regions/{chapterId}.webp                       → Overworld-Regions-Hintergründe
```

**Per-Character AnimState:**
- Jeder Charakter hat eigenes Blinzel-Timing (3-5s Intervall, versetzt)
- Mund-Animation mit 3 Frames (geschlossen/offen/weit) bei Sprechen
- Crossfade (150ms) bei Emotions-Wechsel

**Asset-Delivery:**
- `AssetDeliveryService` - Firebase Storage REST API, SHA256-Hash-Verifikation, Delta-Updates
- `AssetManifest` - Beschreibt alle Packs (characters, backgrounds, enemies, items, scenes)
- Stream-basierter Download mit Retry (3x exponentieller Backoff), temporäre Dateien

**AnimatedWebPRenderer:**
- SKCodec-basiertes Frame-für-Frame Rendering für CG-Szenen/Cutscenes
- Loop-Support, gecachte Frame-Bitmap, Frame-Timing aus WebP-Metadaten

**Prozedurales Legacy-System: ENTFERNT (7. März 2026)**
- CharacterParts, FaceRenderer, EyeRenderer, HairRenderer, BodyRenderer, ClothingRenderer, AccessoryRenderer, CharacterEffects wurden gelöscht
- Nur noch AI-Sprites via SpriteCharacterRenderer

### Hintergründe (Multi-Layer Kompositions-System)

Datengetriebenes System mit C#-Records (`SceneDef`) und 6 spezialisierten Layer-Renderern.
`BackgroundCompositor` orchestriert alles mit Split-Rendering um Charaktere herum.

**Render-Reihenfolge:**
```
BackgroundCompositor.RenderBack()      // Sky + Elements + Ground + PointLights
BackgroundCompositor.BeginLighting()   // SaveLayer mit Ambient ColorFilter
  // ... Charaktere rendern ...
BackgroundCompositor.EndLighting()     // Restore (Ambient-Tönung angewendet)
BackgroundCompositor.RenderFront()     // Foreground + Partikel
```

**Layer-Renderer (in `Rendering/Backgrounds/Layers/`):**

| Renderer | Aufgabe |
|----------|---------|
| `SkyRenderer` | 3-Farben vertikaler LinearGradient, Shader-Caching |
| `ElementRenderer` | 24 Silhouetten-Typen (Bäume, Gebäude, Felsen, Architektur, Innenraum, Spezial) |
| `GroundRenderer` | Boden-Band mit Gradient-Übergang, 6 Texturen (Grass, Stone, Wood, Sand, Snow, Water) |
| `LightingRenderer` | Ambient (SaveLayer + ColorMatrix) + PointLight (radiale Gradienten, Flicker) |
| `SceneParticleRenderer` | 12 deterministische Partikel-Typen (kein Heap-State) |
| `ForegroundRenderer` | 5 Typen über Charakteren (GrassBlade, Fog, Branch, Cobweb, LightRay) mit Safezone-Clip |

**Datenmodell:**
- `SceneDef` — Positional record: Sky, Elements, Ground, Lights, Particles, Foreground
- `SceneDefinitions` — 14 statische Szenen + `Dictionary<string, SceneDef>` (case-insensitive)
- Rückwärtskompatible Keys: "forest"→ForestDay, "village"→VillageSquare, "dungeon"→DungeonHalls etc.

**14 Szenen:** SystemVoid, Title, ForestDay, ForestNight, Campfire, VillageSquare, VillageTavern, DungeonHalls, DungeonBoss, TowerLibrary, TowerSummit, Battlefield, CastleHall, Dreamworld

- `ParallaxRenderer` - Mehrschichtiger Parallax-Scrolling (orthogonal, nicht integriert)

### Effekte

| System | Beschreibung |
|--------|-------------|
| ParticleSystem | Struct-basiert, 11 Presets (MagicSparkle, LevelUpGlow, SystemGlitch, BloodSplatter, AmbientFloat + 6 Element-Presets: FireBurst, IceShard, LightningStrike, WindGust, HolyLight, ShadowVoid) |
| GlitchEffect | Horizontale Verschiebung + RGB-Split |
| ScreenShake | Canvas-Translation (3px normal, 5px Kritisch) |
| MangaPanelRenderer | Screen in Panels splitten |
| SplashArtRenderer | Charakter-Portrait bei Ultimates |

### UI-Renderer

- `UIRenderer` - Buttons, TextWithShadow, ProgressBars, HitTest (statisch, gepoolte Paints)
- `DialogBoxRenderer` - Halbtransparente Box, Sprecher-Name, Weiter-Indikator
- `TypewriterRenderer` - Buchstabe-für-Buchstabe (4 Geschwindigkeiten)
- `ChoiceButtonRenderer` - 2-4 Optionen vertikal, Tags ([Karma+], [STR Check])
- `StatusWindowRenderer` - Solo Leveling Stil: Dunkler BG, blaue Glow-Ränder

### Map-Renderer

- `OverworldRenderer` - Kapitel-Map mit Nodes und Pfaden, AI-Regions-Hintergrund (SetRegionBackground)
- `NodeRenderer` - Knoten-Typen (Story, Boss, Sidequest, Rest, Shop), AI-Icons via SpriteCache (SetSpriteCache)
- `PathRenderer` - Verbindungslinien (freigeschaltet/gesperrt)

## Story-System

### JSON-basierte Kapitel

13 Kapitel: Prolog P1-P3 + Arc 1 K1-K10.

```
Data/Chapters/chapter_{id}.json      → Kapitel-Struktur (Nodes, Verbindungen)
Data/Dialogue/{lang}/chapter_{id}.json → Lokalisierte Texte (DE, EN, ES, FR, IT, PT)
Data/Maps/overworld_{id}.json         → Overworld-Map (Knoten + Pfade)
```

### StoryEngine Service

- Lädt Kapitel aus EmbeddedResource
- Navigiert durch Knoten (Next, Choices, Conditions)
- Constructor Injection: `ProgressionService`, `FateTrackingService`, `GoldService`
- `SetPlayer(player)` muss nach SaveGame-Load oder Neues-Spiel aufgerufen werden (synchronisiert Engine-State)
- `AdvanceToNode()` prüft `node.Condition` - wenn nicht erfüllt, wird Knoten übersprungen (folgt node.Next)
- Condition-Parser unterstützt:
  - Vergleiche: `karma > 50`, `affinity:aria >= 10`, `class == 1`
  - Item-Check: `has_item:M001`, `!has_item:M001`
  - Flag-Check: `has_flag:betrayed_aldric`, `!has_flag:betrayed_aldric`
  - Einwort-Flags: `alliance_aria`, `!alliance_kael` (Fallback auf Flags.Contains)
- `ApplyEffects()` verarbeitet alle StoryEffects-Felder:
  - `karma` → FateTrackingService.ModifyKarma() + Player.Karma sync
  - `exp` → ProgressionService.AwardExp() (Level-Up Events)
  - `gold` → GoldService.AddGold/SpendGold() (Minimum 0 Clamp)
  - `affinity` → Engine + Player.Affinities sync
  - `addItems`/`removeItems` → Engine + Player.Inventory sync
  - `setFlags`/`removeFlags` → Engine + Player.Flags + FateTrackingService sync
  - `fateChanged` → Flag speichern + FateTrackingService.RecordFateChange() + FateChangeTriggered Event
- Sprach-Fallback: CurrentUICulture → Deutsch
- Events: `EffectsApplied`, `ChapterCompleted`, `FateChangeTriggered`

### Knoten-Typen (NodeType)

Dialogue, Choice, Battle, ClassSelect, Shop, Cutscene, Overworld, BondScene, FateChange, SystemMessage, ChapterEnd

## RPG-Systeme

### Klassen (3)

| Klasse | Stärke | Schwäche | Auto-Bonus |
|--------|--------|----------|------------|
| Schwertmeister | STR, DEF | MAG | +2 ATK/Level |
| Arkanist | MAG, MP | DEF | +3 MP/Level |
| Schattenklinke | AGI, LUK | HP | +5% Crit/Level |

### Elemente (6)

Feuer > Eis > Blitz > Wind > Licht > Dunkel > Feuer (1.5x bei Schwäche, 0.5x bei Resistenz)

### Services-Übersicht

| Service | Zweck |
|---------|-------|
| StoryEngine | Kapitel-Navigation, Conditions, Effekte (EXP/Gold/Karma/Flags), SetPlayer() |
| BattleEngine | Schadensberechnung, Element-System, Crits |
| SkillService | 15 Skills/Klasse, 5 Stufen, Freischaltung per Level |
| InventoryService | Items verwalten, Ausrüsten, Stack-Verwaltung |
| AffinityService | Bond-Stufen (0-100) für 5 NPCs |
| FateTrackingService | Karma (-100 bis +100), Entscheidungs-Log, FateFlags, ModifyKarma() |
| CodexService | Enzyklopädie (Charaktere, Orte, Lore) |
| ProgressionService | EXP, Level-Up, Stat-Verteilung |
| SaveGameService | SQLite, 3 Slots, Auto-Save bei Knoten-Wechsel |
| GoldService | Gold verwalten, Rewarded-Video-Cooldown (3x/Tag) |
| ChapterUnlockService | K6-K10 per Gold (2.000-6.000) |
| AudioService | SFX (SoundPool, 28 GameSfx) + BGM (MediaPlayer, 10 BgmTracks), Desktop-Stub, Android: AndroidAudioService + Vibrator |
| TutorialService | Erstbesucher-Hints per Preferences |
| DailyService | Login-Bonus (Gold), Prophezeiung, Streak |

## Gold-Economy

| Kapitel | Kosten | Status |
|---------|--------|--------|
| P1-P3 | Gratis | Prolog |
| K1-K5 | Gratis | Arc 1 (frei) |
| K6 | 800 Gold | Arc 1 (Gold) |
| K7 | 1.200 Gold | Arc 1 (Gold) |
| K8 | 1.800 Gold | Arc 1 (Gold) |
| K9 | 2.500 Gold | Arc 1 (Gold) |
| K10 | 3.500 Gold | Arc 1 (Gold) |

Gesamt K6-K10: 9.800 Gold (vorher 20.000). Story-Gold K1-K5 verdreifacht, K6-K7 verdoppelt, K8-K9 neue Gold-Drops.

Gold-Quellen: Kampf-Drops, Story-Belohnungen, Rewarded Video (500G, 3x/Tag), Daily Login.

## Premium & Ads

- **Kein Banner-Ad** (Vollbild-SkiaSharp-Spiel)
- **Rewarded Ads** (6 Placements): gold_bonus, time_rift, bonus_exp, revive, daily_prophecy, kodex_hint
- **IAP**: Gold-Pakete, Zeitkristalle, remove_ads (Preise noch offen)

## Lokalisierung

- **Story-Texte**: JSON in `Data/Dialogue/{lang}/` (DE, EN, ES, FR, IT, PT)
- **UI-Strings**: ILocalizationService per Constructor Injection in Scenes/Overlays. AppStrings.resx + 5 Kultur-Dateien (de/es/fr/it/pt). AppStrings.Designer.cs manuell gepflegt (CLI-Build generiert nicht automatisch)
- **Pattern**: Strings im Konstruktor cachen (`_localization.GetString("Key") ?? "Fallback"`), nicht per-Frame
- **Lokalisierte Scenes/Overlays**: BattleScene, LevelUpOverlay, GameOverOverlay, PauseOverlay, FateChangedOverlay, TutorialOverlay, BacklogOverlay, StatusWindowOverlay, ChapterUnlockOverlay, SettingsScene, AssetDownloadScene
- **Noch hardcodiert**: TitleScene, SaveSlotScene, CodexScene, ShopScene, InventoryScene, DialogueScene, ClassSelectScene, OverworldScene, StatusScene (in AppStrings.resx vorbereitet)
- **Fallback**: Englisch (Base .resx), Deutsch für Story-Texte (StoryEngine.LoadDialogueTextsAsync)
- **DI-Registrierung**: `ILocalizationService` als Singleton in App.axaml.cs mit `RebornSaga.Resources.Strings.AppStrings.ResourceManager`

## Daten-Dateien

| Pfad | Inhalt |
|------|--------|
| Data/Chapters/ | 13 Kapitel-JSONs (P1-P3, K1-K10) |
| Data/Dialogue/{lang}/ | Dialog-Texte pro Sprache |
| Data/Maps/ | 13 Overworld-Map-JSONs |
| Data/Skills/ | 3 Skill-JSONs (swordmaster, arcanist, shadowblade) |
| Data/Items/ | items.json (Waffen, Rüstungen, Consumables, Key-Items) |
| Data/Enemies/ | enemies.json (Gegner + Bosse) |

## Audio-Assets (Android)

| Pfad | Inhalt | Anzahl |
|------|--------|--------|
| Assets/Sounds/*.ogg | SFX (SoundPool, GameSfx-Enum) | 28 |
| Assets/Music/*.ogg | BGM (MediaPlayer, BgmTracks-Konstanten) | 10 |

Dateinamen-Mapping in `AndroidAudioService.SoundFileMap` (SFX) und `BgmFileMap` (BGM).

## Thread-Safety

| Komponente | Mechanismus |
|-----------|-------------|
| BacklogOverlay | `lock` auf Entries-Liste (Add/Clear/Render) |
| ChapterUnlockService | `SemaphoreSlim(1,1)` gegen Doppel-Unlock |
| SaveGameService | `SemaphoreSlim(1,1)` gegen parallele Saves + IDisposable (SQLite CloseAsync) |
| GoldService | `AddGold`/`RemoveGold` mit `Math.Clamp(0, int.MaxValue)` |

## BattleScene: Kampf-System

### Phasen (BattlePhase Enum)
```
Intro → PlayerTurn → (Attack | Dodge | SkillSelect | ItemSelect | PlayerSkillAttack) → EnemyTurn → (Victory | Defeat)
```

### Skill-Integration
- `SkillSelect`-Phase: Zeigt verfügbare Skills (max 6), MP-Kosten, Element-Icons
- `ExecuteSkillAttack()`: Multiplier-basierter Schaden, MP-Abzug, Element-System (1.5x/0.5x), Skill-Evolution bei Mastery-Schwelle
- `ApplySkillEffect()`: Verarbeitet `heal_Xpct` (HP-Heilung %) und `atk_buff_X` (ATK-Bonus temporär)
- Constructor: `BattleScene(BattleEngine, SkillService, InventoryService)` - alle drei per DI

### Item-Integration
- `ItemSelect`-Phase: Zeigt Consumables aus InventoryService (max 6)
- `UseItem()` via InventoryService: HealPercent oder HealHp/HealMp
- Items mit `IsUsable`-Flag und korrekten Stat-Clamps (HP min 1, MP min 0)

## Performance-Optimierungen

- **StatusWindowRenderer**: Gecachte Bar-Texte (3 Bars) + Stat-Texte (5 Stats), nur bei Wertänderung neu erzeugt
- **StatusScene**: `CachedSkillEntry` struct für Skill-Display, Dirty-Flags für Neuberechnung
- **ShopScene**: Gecachte Preis-Strings, nur bei Selektion aktualisiert
- **InventoryScene**: Gecachte Item-Strings pro Slot
- **Alle Renderer**: Statische SKPaint/SKFont/SKMaskFilter, keine per-Frame Allokationen

## Android-Besonderheiten

- Portrait-only, Immersive Fullscreen
- AudioServiceFactory: AndroidAudioService mit SoundPool + MediaPlayer
- RewardedAdServiceFactory: 6 Placements
- PurchaseServiceFactory: Gold-Pakete + remove_ads
- Double-Back-to-Exit via BackPressHelper

## Loading-Pipeline & Lifecycle

### InitializeServicesAsync (App.axaml.cs)
- Wird von MainView.OnAttachedToVisualTree via MainViewModel.InitializeAsync() aufgerufen
- Lädt SkillService.LoadSkills() und InventoryService.LoadItems() (Voraussetzung für SaveGameService.LoadGameAsync)
- IPurchaseService.InitializeAsync() fire-and-forget (stellt Käufe nach Gerätewechsel wieder her)
- TitleScene braucht keine geladenen Daten, daher kein Blocking

### DisposeServices (App.axaml.cs)
- Desktop: ShutdownRequested, Android: MainActivity.OnDestroy
- Disposed: IAudioService (SoundPool + MediaPlayer), SaveGameService (SQLite-Verbindung)
- SaveGameService implementiert IDisposable (CloseAsync auf SQLiteAsyncConnection)
