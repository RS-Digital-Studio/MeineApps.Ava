# Rendering — SkiaSharp-Renderer

Alle SkiaSharp-Renderer des Spiels. Kein Avalonia-XAML im Spielbereich — alles wird direkt
auf `SKCanvas` gezeichnet. SkiaSharp-Grundlagen, Paint-Lifecycle, DPI und MaskFilter-Leak →
[MeineApps.UI/CLAUDE.md](../../../../UI/MeineApps.UI/CLAUDE.md).

## Kern-Regel: Statische Paints

Alle `SKPaint` / `SKFont` / `SKMaskFilter` / `SKPath` sind `static readonly`. `Cleanup()` bleibt
bewusst leer — statische Ressourcen leben für die gesamte App-Lifetime.
Kommentar-Pflicht: `// _glowBlur ist static readonly — NICHT disposen`

## Unterverzeichnisse

| Verzeichnis | Inhalt |
|-------------|--------|
| `Backgrounds/` | `BackgroundCompositor`, `SceneDef/SceneDefinitions` (14 Szenen), 5 Layer-Renderer |
| `Characters/` | `CharacterRenderer` (Fassade), `SpriteCharacterRenderer` (Blinzeln, Crossfade), `CharacterDefinitions` (11), `SpriteDefinitions`, `Emotion` |
| `Effects/` | `ParticleSystem`, `GlitchEffect`, `ScreenShake`, `MangaPanelRenderer`, `SplashArtRenderer` |
| `Map/` | `OverworldRenderer`, `NodeRenderer`, `PathRenderer` |
| `UI/` | `UIRenderer`, `DialogBoxRenderer`, `TypewriterRenderer`, `ChoiceButtonRenderer`, `StatusWindowRenderer` |
| `AnimatedWebPRenderer.cs` | SKCodec-basiertes Frame-Rendering für Cutscene-Animated-WebPs |

## Backgrounds

`BackgroundCompositor` orchestriert 6 Layer-Renderer:

```
BackgroundCompositor.RenderBack()      // Sky + Elements + Ground + PointLights
BackgroundCompositor.BeginLighting()   // SaveLayer mit Ambient ColorFilter
  // ... Charaktere rendern ...
BackgroundCompositor.EndLighting()     // Restore
BackgroundCompositor.RenderFront()     // Foreground + Partikel
```

| Renderer | Zweck |
|----------|-------|
| `SkyRenderer` | 3-Farben LinearGradient, Shader gecacht |
| `ElementRenderer` | 24 Silhouetten-Typen (Bäume, Gebäude, Felsen, Architektur, Innenraum, Spezial) |
| `GroundRenderer` | Boden-Band mit Gradient, 6 Texturen — Fade-Shader nur bei Szenen-/Bounds-Wechsel neu erstellt |
| `LightingRenderer` | Ambient SaveLayer + PointLights (radiale Gradienten, Flicker) |
| `SceneParticleRenderer` | 12 deterministische Partikel-Typen (kein Heap-State) |
| `ForegroundRenderer` | 5 Typen über Charakteren (GrassBlade, Fog, Branch, Cobweb, LightRay) mit Safezone-Clip |

`SceneDef` (positional record) + `SceneDefinitions` (14 statische Szenen, Dictionary
case-insensitive). Rückwärtskompatible Keys: `"forest"→ForestDay`, `"dungeon"→DungeonHalls`.

## Characters

Rein Sprite-basiertes System — kein prozeduraler Fallback. Fehlendes Asset = nichts gezeichnet.

| Klasse | Zweck |
|--------|-------|
| `CharacterRenderer` | Fassade: `DrawPortrait` / `DrawFullBody` / `DrawIcon`, aktiv/inaktiv-Dimming |
| `SpriteCharacterRenderer` | Blinzeln (unabhängig pro Charakter), Crossfade 150 ms, Mund-Animation (3 Frames) |
| `CharacterDefinitions` | 11 Definitionen (3 Protagonist-Klassen + 6 NPCs + 2 Bosse), `GetById()` |
| `SpriteDefinitions` | `Pose`-Enum (Standing/Battle/Sitting/Kneeling/Floating/Lying/Running) |

**Asset-Pfade:**
```
characters/{charId}/full/{pose}_{emotion}.webp
characters/{charId}/overlays/blink.webp
characters/{charId}/overlays/mouth_open.webp   (+ mouth_wide.webp)
enemies/{enemyId}.webp
```

**`SpriteCache`** (`Services/SpriteCache.cs`) — LRU-Cache (max 30 Bilder), thread-safe, `IDisposable`.
`PeekPixels()` statt `GetPixel()` in `ComputeContentBounds` (kein JNI-Overhead auf Android).

## Effects

| System | Pattern |
|--------|---------|
| `ParticleSystem` | Struct-basiert, 11 Presets (MagicSparkle, LevelUpGlow, SystemGlitch, BloodSplatter, AmbientFloat + 6 Element-Presets) |
| `GlitchEffect` | Horizontale Verschiebung + RGB-Split — `static readonly SKPaint` für jeden Kanal |
| `ScreenShake` | Canvas-Translation (3 px normal, 5 px kritisch), Exponential-Decay |
| `MangaPanelRenderer` | Screen in Panels splitten (dynamische Anzahl + Winkel) |
| `DissolveTransition` | `SKPath` als Instanzfeld mit `Rewind()` statt `new` pro Frame |

## UI

| Klasse | Zweck |
|--------|-------|
| `UIRenderer` | Buttons, TextWithShadow, ProgressBars, HitTest — statisch, gepoolte Paints |
| `DialogBoxRenderer` | Halbtransparente Box, Sprecher-Name, Weiter-Indikator |
| `TypewriterRenderer` | Buchstabe-für-Buchstabe (4 Geschwindigkeiten: Slow/Normal/Fast/Instant) |
| `ChoiceButtonRenderer` | 2–4 Optionen vertikal, Tags ([Karma+], [STR Check]) |
| `StatusWindowRenderer` | Solo Leveling Stil: dunkler BG, blaue Glow-Ränder, gecachte Bar- und Stat-Texte (nur bei Wertänderung neu erzeugt) |

## AnimatedWebPRenderer

SKCodec-basiertes Frame-für-Frame-Rendering für Cutscenes. Loop-Support, gecachte
Frame-Bitmaps, Frame-Timing aus WebP-Metadaten.

## Map

| Klasse | Zweck |
|--------|-------|
| `OverworldRenderer` | Kapitel-Map mit Nodes und Pfaden, AI-Regions-Hintergrund |
| `NodeRenderer` | Knoten-Typen (Story, Boss, SideQuest, Npc, Dungeon, Rest, Locked), AI-Icons via SpriteCache |
| `PathRenderer` | Verbindungslinien (freigeschaltet/gesperrt/aktuell) |
