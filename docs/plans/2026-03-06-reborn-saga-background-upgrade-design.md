# Reborn Saga: Immersives Hintergrund-System — Design

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:writing-plans to create the implementation plan.

## Ziel

Ersetze das monolithische BackgroundRenderer (8 flache Gradient-Szenen) durch ein datengetriebenes
Kompositions-System mit 14+ feingranularen Szenen, Multi-Layer-Rendering, atmosphärischen Partikeln,
Beleuchtung und Vordergrund-Elementen die Charaktere in die Szene einbetten.

## Architektur

C#-Record-Definitionen + wiederverwendbare Layer-Renderer. Kein JSON — typsicher, kompiliert, IntelliSense.

```
SceneDef (C# record)              BackgroundCompositor (Orchestrator)
  |-- SkyDef (Gradient-Farben)          |-- SkyRenderer
  |-- ElementDef[] (Silhouetten)        |-- ElementRenderer
  |-- GroundDef (Typ, Farbe, Hoehe)     |-- GroundRenderer
  |-- LightDef[] (Ambient + Punkt)      |-- LightingRenderer
  |-- ParticleDef[] (Atmosphaere)       |-- SceneParticleRenderer
  +-- ForegroundDef[] (ueber Chars)     +-- ForegroundRenderer
```

**Split-Rendering in der DialogueScene:**
1. `compositor.RenderBack()` — Sky, Elemente, Ground
2. `CharacterRenderer` — Charaktere (wie bisher)
3. `compositor.RenderFront()` — Lighting-Overlay, Foreground, Partikel

## Datenmodell (C#-Records)

```csharp
public record SceneDef {
    public required SkyDef Sky { get; init; }
    public ElementDef[] Elements { get; init; } = [];
    public GroundDef? Ground { get; init; }
    public LightDef[] Lights { get; init; } = [];
    public ParticleDef[] Particles { get; init; } = [];
    public ForegroundDef[] Foreground { get; init; } = [];
}

public record SkyDef(SKColor Top, SKColor Mid, SKColor Bottom, float MidStop = 0.5f);

public record ElementDef(
    ElementType Type,    // Tree, Building, Mountain, Rock, Pillar, Ruin...
    int Count,
    SKColor Color,
    float MinHeight,     // Relativ zu Bounds (0-1)
    float MaxHeight,
    float YPosition      // Wo die Basis steht (0=oben, 1=unten)
);

public record GroundDef(
    GroundType Type,     // Grass, Stone, Wood, Sand, Snow, Water
    SKColor Color,
    SKColor? AccentColor,
    float Height         // Relativ-Hoehe des Bodens (0.1-0.25)
);

public record LightDef(
    LightType Type,      // Ambient, PointLight, Directional
    SKColor Color,
    float Intensity,     // 0-1
    float X, float Y,    // Relativ-Position (0-1)
    float Radius,        // Nur fuer PointLight
    bool Flickers        // Fackel-Flackern
);

public record ParticleDef(
    ParticleType Type,   // Firefly, Spark, Dust, Leaf, Snowflake, MagicOrb, Ember
    int Count,
    SKColor Color,
    byte Alpha
);

public record ForegroundDef(
    ForegroundType Type, // GrassBlade, Fog, Branch, Cobweb, LightRay
    SKColor Color,
    byte Alpha,          // Max 40% um Gesichter nicht zu verdecken
    float MaxY           // Nur unterhalb dieser Y-Position (0.6 = untere 40%)
);
```

## Phase 1: 14 Szenen

| # | ID | Beschreibung | Elemente | Partikel | Licht |
|---|-----|-------------|----------|----------|-------|
| 1 | SystemVoid | ARIA System-Raum | - | Scan-Lines | Blau ambient |
| 2 | Title | Titelbildschirm | - | Partikel-Ringe | Dunkel ambient |
| 3 | ForestDay | Wald bei Tag | Nadelbaeume, Buesche | Sonnenstrahlen, Blaetter | Warmes Sonnenlicht |
| 4 | ForestNight | Wald bei Nacht | Nadelbaeume (dunkel) | Gluehwuermchen | Mondlicht (blau) |
| 5 | Campfire | Lagerfeuer-Szene | Baumstaemme, Steine | Funken, Glut | Punkt-Licht (orange, flackert) |
| 6 | VillageSquare | Dorfplatz | Haeuser, Brunnen | Rauch aus Kaminen | Warmes Abendlicht |
| 7 | VillageTavern | Taverne innen | Balken, Faesser | Staub-Partikel | Kerzen-Punktlichter |
| 8 | DungeonHalls | Dungeon-Gaenge | Steinwaende, Saeulen | Staub | Fackeln (Punktlicht) |
| 9 | DungeonBoss | Boss-Raum | Saeulen, Ruinen | Glut, Magie-Orbs | Rot ambient + Punkt |
| 10 | TowerLibrary | Magier-Bibliothek | Buecherregale, Tische | Magie-Funken | Kerzen + Magie-Glow |
| 11 | TowerSummit | Turm-Spitze | Gelaender, Runen | Sterne, Magie-Orbs | Sternenlicht (lila) |
| 12 | Battlefield | Schlachtfeld | Schwerter im Boden, Fahnen | Rauch, Glut | Rot-orange ambient |
| 13 | CastleHall | Thronsaal | Saeulen, Banner, Thron | Staub im Licht | Fackeln + Fenster-Licht |
| 14 | Dreamworld | Traumwelt | Fragmentierte Geometrie | Glitch-Partikel | Wechselnder HSL-Ambient |

## Charakter-Integration

### Ambient Light
`SaveLayer` + `SKColorFilter.CreateColorMatrix()` ueber den gesamten Bereich (Charaktere + Foreground).
Toent alles einheitlich — Fackellicht macht Charaktere orange, Mondlicht blau.

### Punkt-Licht
Additive radiale Gradienten. Position unabhaengig von Charakteren. Flackern via `sin(time)` fuer Fackeln/Feuer.

### Ground
Nur visuell relevant wenn Charaktere im FullBody-Modus stehen. Im Portrait-Modus: atmosphaerischer
Nebel/Fade am unteren Rand stattdessen.

### Foreground-Safezone
Vordergrund-Elemente (Gras, Nebel, Aeste) nur im unteren 40% des Screens, max 40% Alpha.
Nie im Dialog-Box-Bereich.

## Betroffene Dateien

**Neu:**
- `Rendering/Backgrounds/SceneDef.cs` — Records + Enums
- `Rendering/Backgrounds/SceneDefinitions.cs` — 14 statische Szenen-Definitionen
- `Rendering/Backgrounds/BackgroundCompositor.cs` — Orchestrator (RenderBack/RenderFront)
- `Rendering/Backgrounds/Layers/SkyRenderer.cs`
- `Rendering/Backgrounds/Layers/ElementRenderer.cs`
- `Rendering/Backgrounds/Layers/GroundRenderer.cs`
- `Rendering/Backgrounds/Layers/LightingRenderer.cs`
- `Rendering/Backgrounds/Layers/SceneParticleRenderer.cs`
- `Rendering/Backgrounds/Layers/ForegroundRenderer.cs`

**Modifiziert:**
- `DialogueScene.cs` — RenderBack/RenderFront Split
- `BattleScene.cs` — Gleicher Split
- `ClassSelectScene.cs` — Gleicher Split
- `BackgroundRenderer.cs` — Wird durch BackgroundCompositor ersetzt

## Migration

`SceneBackground` Enum wird durch String-Keys ersetzt. `BackgroundCompositor` hat ein
`Dictionary<string, SceneDef>` das `backgroundKey` aus den Story-JSONs direkt auf `SceneDef` mappt.
Unbekannte Keys fallen auf `ForestDay` zurueck.
