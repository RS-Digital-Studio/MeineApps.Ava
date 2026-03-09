namespace RebornSaga.Rendering.Backgrounds;

using SkiaSharp;
using System;
using System.Collections.Generic;

/// <summary>
/// 14 statische Szenen-Definitionen + Dictionary-Mapping für backgroundKey-Lookup.
/// Rückwärtskompatibel: alte Keys (forest, village, dungeon, tower, castle) werden gemappt.
/// </summary>
public static class SceneDefinitions
{
    // ── Szenen ──────────────────────────────────────────────

    public static readonly SceneDef SystemVoid = new(
        Sky: new(new SKColor(0x05, 0x05, 0x0A), new SKColor(0x05, 0x05, 0x0A), new SKColor(0x05, 0x05, 0x0A)),
        Elements: Array.Empty<ElementDef>(),
        Ground: null,
        Lights: new[] { new LightDef(LightType.Ambient, new SKColor(0x4A, 0x90, 0xD9), 0.1f) },
        Particles: new[] { new ParticleDef(ParticleStyle.ScanLine, 0, new SKColor(0x4A, 0x90, 0xD9), 15) },
        Foreground: Array.Empty<ForegroundDef>()
    );

    public static readonly SceneDef Title = new(
        Sky: new(new SKColor(0x0A, 0x0D, 0x14), new SKColor(0x0D, 0x11, 0x17), new SKColor(0x06, 0x08, 0x0D)),
        Elements: Array.Empty<ElementDef>(),
        Ground: null,
        Lights: new[] { new LightDef(LightType.Ambient, new SKColor(0x10, 0x15, 0x25), 0.15f) },
        Particles: new[]
        {
            new ParticleDef(ParticleStyle.RingOrbit, 3, new SKColor(0x4A, 0x90, 0xD9), 28),
            new ParticleDef(ParticleStyle.Star, 15, new SKColor(0x58, 0xA6, 0xFF), 60)
        },
        Foreground: Array.Empty<ForegroundDef>()
    );

    public static readonly SceneDef ForestDay = new(
        Sky: new(new SKColor(0x1A, 0x3A, 0x1A), new SKColor(0x0D, 0x20, 0x0D), new SKColor(0x0A, 0x15, 0x0A)),
        Elements: new[]
        {
            new ElementDef(ElementType.ConiferTree, 7, new SKColor(0x0A, 0x18, 0x0A), 0.30f, 0.50f, 0.85f),
            new ElementDef(ElementType.Bush, 4, new SKColor(0x0C, 0x1A, 0x0C), 0.08f, 0.12f, 0.90f)
        },
        Ground: new GroundDef(GroundType.Grass, new SKColor(0x1A, 0x30, 0x12), 0.12f),
        Lights: new[]
        {
            new LightDef(LightType.Ambient, new SKColor(0xFF, 0xE8, 0xB0), 0.1f),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0xD7, 0x00), 0.15f, 0.8f, 0.15f, 80f)
        },
        Particles: new[]
        {
            new ParticleDef(ParticleStyle.Leaf, 8, new SKColor(0x4A, 0x80, 0x20), 80),
            new ParticleDef(ParticleStyle.Dust, 5, new SKColor(0xFF, 0xE8, 0xB0), 30)
        },
        Foreground: new[]
        {
            new ForegroundDef(ForegroundStyle.GrassBlade, new SKColor(0x1A, 0x30, 0x12), 80, 0.6f),
            new ForegroundDef(ForegroundStyle.LightRay, new SKColor(0xFF, 0xD7, 0x00), 20, 0.0f)
        }
    );

    public static readonly SceneDef ForestNight = new(
        Sky: new(new SKColor(0x05, 0x0A, 0x18), new SKColor(0x08, 0x10, 0x20), new SKColor(0x03, 0x06, 0x10)),
        Elements: new[]
        {
            new ElementDef(ElementType.ConiferTree, 7, new SKColor(0x05, 0x0A, 0x08), 0.30f, 0.50f, 0.85f),
            new ElementDef(ElementType.Bush, 3, new SKColor(0x06, 0x0C, 0x08), 0.08f, 0.12f, 0.90f)
        },
        Ground: new GroundDef(GroundType.Grass, new SKColor(0x0A, 0x18, 0x08), 0.12f),
        Lights: new[] { new LightDef(LightType.Ambient, new SKColor(0x30, 0x40, 0x80), 0.2f) },
        Particles: new[] { new ParticleDef(ParticleStyle.Firefly, 15, new SKColor(0xC0, 0xFF, 0x60), 120) },
        Foreground: new[] { new ForegroundDef(ForegroundStyle.Fog, new SKColor(0x20, 0x30, 0x50), 40, 0.5f) }
    );

    public static readonly SceneDef Campfire = new(
        Sky: new(new SKColor(0x1A, 0x0A, 0x02), new SKColor(0x10, 0x08, 0x03), new SKColor(0x05, 0x03, 0x01)),
        Elements: new[]
        {
            new ElementDef(ElementType.Log, 3, new SKColor(0x20, 0x12, 0x08), 0.06f, 0.10f, 0.88f),
            new ElementDef(ElementType.Rock, 4, new SKColor(0x18, 0x14, 0x10), 0.06f, 0.12f, 0.90f),
            new ElementDef(ElementType.DeadTree, 2, new SKColor(0x0A, 0x08, 0x05), 0.25f, 0.40f, 0.85f)
        },
        Ground: new GroundDef(GroundType.Grass, new SKColor(0x0A, 0x15, 0x06), 0.12f),
        Lights: new[]
        {
            new LightDef(LightType.Ambient, new SKColor(0x20, 0x15, 0x08), 0.15f),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0x8C, 0x20), 0.5f, 0.5f, 0.75f, 100f, true)
        },
        Particles: new[]
        {
            new ParticleDef(ParticleStyle.Spark, 12, new SKColor(0xFF, 0xA0, 0x20), 150),
            new ParticleDef(ParticleStyle.Ember, 6, new SKColor(0xFF, 0x60, 0x10), 100)
        },
        Foreground: new[] { new ForegroundDef(ForegroundStyle.Fog, new SKColor(0x20, 0x18, 0x10), 30, 0.5f) }
    );

    public static readonly SceneDef VillageSquare = new(
        Sky: new(new SKColor(0x30, 0x18, 0x10), new SKColor(0x20, 0x10, 0x0A), new SKColor(0x10, 0x08, 0x05)),
        Elements: new[]
        {
            new ElementDef(ElementType.House, 4, new SKColor(0x15, 0x10, 0x08), 0.20f, 0.30f, 0.80f),
            new ElementDef(ElementType.Well, 1, new SKColor(0x18, 0x14, 0x10), 0.12f, 0.15f, 0.88f),
            new ElementDef(ElementType.Fence, 2, new SKColor(0x12, 0x0E, 0x08), 0.05f, 0.08f, 0.92f)
        },
        Ground: new GroundDef(GroundType.Stone, new SKColor(0x28, 0x22, 0x1A), 0.10f),
        Lights: new[] { new LightDef(LightType.Ambient, new SKColor(0xFF, 0xC8, 0x80), 0.15f) },
        Particles: new[]
        {
            new ParticleDef(ParticleStyle.Smoke, 3, new SKColor(0x80, 0x70, 0x60), 30),
            new ParticleDef(ParticleStyle.Dust, 4, new SKColor(0xD0, 0xC0, 0xA0), 25)
        },
        Foreground: Array.Empty<ForegroundDef>()
    );

    public static readonly SceneDef VillageTavern = new(
        Sky: new(new SKColor(0x1A, 0x12, 0x0A), new SKColor(0x14, 0x0E, 0x08), new SKColor(0x0D, 0x0A, 0x06)),
        Elements: new[]
        {
            new ElementDef(ElementType.Barrel, 3, new SKColor(0x20, 0x15, 0x0A), 0.10f, 0.15f, 0.88f),
            new ElementDef(ElementType.Table, 2, new SKColor(0x1A, 0x12, 0x08), 0.08f, 0.12f, 0.90f),
            new ElementDef(ElementType.Bookshelf, 1, new SKColor(0x15, 0x10, 0x08), 0.25f, 0.35f, 0.85f)
        },
        Ground: new GroundDef(GroundType.Wood, new SKColor(0x30, 0x20, 0x10), 0.10f),
        Lights: new[]
        {
            new LightDef(LightType.Ambient, new SKColor(0xFF, 0xC0, 0x60), 0.12f),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0xC8, 0x40), 0.3f, 0.2f, 0.3f, 50f, true),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0xC8, 0x40), 0.3f, 0.5f, 0.25f, 50f, true),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0xC8, 0x40), 0.3f, 0.8f, 0.35f, 50f, true)
        },
        Particles: new[] { new ParticleDef(ParticleStyle.Dust, 8, new SKColor(0xD0, 0xB8, 0x80), 20) },
        Foreground: Array.Empty<ForegroundDef>()
    );

    public static readonly SceneDef DungeonHalls = new(
        Sky: new(new SKColor(0x08, 0x08, 0x10), new SKColor(0x0A, 0x0A, 0x14), new SKColor(0x05, 0x05, 0x0A)),
        Elements: new[]
        {
            new ElementDef(ElementType.Pillar, 4, new SKColor(0x14, 0x14, 0x1A), 0.30f, 0.45f, 0.82f),
            new ElementDef(ElementType.BrokenWall, 2, new SKColor(0x10, 0x10, 0x16), 0.15f, 0.25f, 0.88f)
        },
        Ground: new GroundDef(GroundType.Stone, new SKColor(0x18, 0x18, 0x20), 0.10f),
        Lights: new[]
        {
            new LightDef(LightType.Ambient, new SKColor(0x15, 0x15, 0x25), 0.15f),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0x8C, 0x00), 0.3f, 0.25f, 0.35f, 45f, true),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0x8C, 0x00), 0.3f, 0.75f, 0.35f, 45f, true)
        },
        Particles: new[] { new ParticleDef(ParticleStyle.Dust, 6, new SKColor(0x80, 0x80, 0x90), 20) },
        Foreground: new[] { new ForegroundDef(ForegroundStyle.Cobweb, new SKColor(0x80, 0x80, 0x90), 40, 0.0f) }
    );

    public static readonly SceneDef DungeonBoss = new(
        Sky: new(new SKColor(0x1A, 0x05, 0x05), new SKColor(0x12, 0x03, 0x03), new SKColor(0x08, 0x02, 0x02)),
        Elements: new[]
        {
            new ElementDef(ElementType.Pillar, 6, new SKColor(0x18, 0x08, 0x08), 0.35f, 0.50f, 0.80f),
            new ElementDef(ElementType.Arch, 1, new SKColor(0x14, 0x06, 0x06), 0.40f, 0.50f, 0.78f),
            new ElementDef(ElementType.BrokenWall, 3, new SKColor(0x12, 0x06, 0x06), 0.15f, 0.25f, 0.88f)
        },
        Ground: new GroundDef(GroundType.Stone, new SKColor(0x20, 0x0A, 0x0A), 0.10f, new SKColor(0x30, 0x10, 0x10)),
        Lights: new[]
        {
            new LightDef(LightType.Ambient, new SKColor(0x80, 0x20, 0x20), 0.2f),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0x30, 0x30), 0.4f, 0.5f, 0.5f, 90f)
        },
        Particles: new[]
        {
            new ParticleDef(ParticleStyle.Ember, 10, new SKColor(0xFF, 0x40, 0x10), 120),
            new ParticleDef(ParticleStyle.MagicOrb, 4, new SKColor(0xFF, 0x20, 0x20), 80)
        },
        Foreground: new[] { new ForegroundDef(ForegroundStyle.Fog, new SKColor(0x40, 0x10, 0x10), 35, 0.5f) }
    );

    public static readonly SceneDef TowerLibrary = new(
        Sky: new(new SKColor(0x10, 0x0A, 0x25), new SKColor(0x15, 0x10, 0x30), new SKColor(0x0A, 0x08, 0x1A)),
        Elements: new[]
        {
            new ElementDef(ElementType.Bookshelf, 4, new SKColor(0x18, 0x10, 0x28), 0.30f, 0.45f, 0.82f),
            new ElementDef(ElementType.Table, 2, new SKColor(0x14, 0x0C, 0x20), 0.10f, 0.14f, 0.90f)
        },
        Ground: new GroundDef(GroundType.Wood, new SKColor(0x20, 0x15, 0x30), 0.10f),
        Lights: new[]
        {
            new LightDef(LightType.Ambient, new SKColor(0x60, 0x40, 0x90), 0.1f),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0xC8, 0x40), 0.25f, 0.3f, 0.4f, 40f, true),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0xC8, 0x40), 0.25f, 0.7f, 0.35f, 40f, true)
        },
        Particles: new[]
        {
            new ParticleDef(ParticleStyle.MagicOrb, 6, new SKColor(0x9B, 0x59, 0xB6), 80),
            new ParticleDef(ParticleStyle.Dust, 4, new SKColor(0xC0, 0xA0, 0xE0), 25)
        },
        Foreground: new[] { new ForegroundDef(ForegroundStyle.LightRay, new SKColor(0x9B, 0x59, 0xB6), 18, 0.0f) }
    );

    public static readonly SceneDef TowerSummit = new(
        Sky: new(new SKColor(0x08, 0x05, 0x18), new SKColor(0x0A, 0x08, 0x20), new SKColor(0x05, 0x03, 0x10)),
        Elements: new[]
        {
            new ElementDef(ElementType.Railing, 3, new SKColor(0x14, 0x10, 0x28), 0.06f, 0.10f, 0.92f),
            new ElementDef(ElementType.RuneCircle, 2, new SKColor(0x9B, 0x59, 0xB6, 60), 0.08f, 0.12f, 0.90f)
        },
        Ground: new GroundDef(GroundType.Stone, new SKColor(0x18, 0x14, 0x28), 0.08f),
        Lights: new[] { new LightDef(LightType.Ambient, new SKColor(0x60, 0x40, 0x90), 0.15f) },
        Particles: new[]
        {
            new ParticleDef(ParticleStyle.Star, 20, new SKColor(0xFF, 0xFF, 0xE0), 80),
            new ParticleDef(ParticleStyle.MagicOrb, 5, new SKColor(0x9B, 0x59, 0xB6), 60)
        },
        Foreground: Array.Empty<ForegroundDef>()
    );

    public static readonly SceneDef Battlefield = new(
        Sky: new(new SKColor(0x25, 0x08, 0x05), new SKColor(0x1A, 0x0A, 0x05), new SKColor(0x10, 0x05, 0x03)),
        Elements: new[]
        {
            new ElementDef(ElementType.SwordInGround, 5, new SKColor(0x30, 0x18, 0x10), 0.12f, 0.20f, 0.88f),
            new ElementDef(ElementType.Banner, 2, new SKColor(0x28, 0x10, 0x08), 0.18f, 0.28f, 0.85f) { AccentColor = new SKColor(0xC0, 0x30, 0x10) },
            new ElementDef(ElementType.BrokenWall, 3, new SKColor(0x20, 0x12, 0x0A), 0.15f, 0.22f, 0.87f)
        },
        Ground: new GroundDef(GroundType.Sand, new SKColor(0x28, 0x18, 0x0A), 0.10f),
        Lights: new[] { new LightDef(LightType.Ambient, new SKColor(0xC0, 0x50, 0x20), 0.2f) },
        Particles: new[]
        {
            new ParticleDef(ParticleStyle.Ember, 8, new SKColor(0xFF, 0x60, 0x10), 100),
            new ParticleDef(ParticleStyle.Smoke, 4, new SKColor(0x50, 0x30, 0x20), 30)
        },
        Foreground: new[] { new ForegroundDef(ForegroundStyle.Fog, new SKColor(0x40, 0x20, 0x10), 30, 0.5f) }
    );

    public static readonly SceneDef CastleHall = new(
        Sky: new(new SKColor(0x20, 0x18, 0x0A), new SKColor(0x18, 0x12, 0x08), new SKColor(0x10, 0x0C, 0x06)),
        Elements: new[]
        {
            new ElementDef(ElementType.Pillar, 6, new SKColor(0x28, 0x20, 0x10), 0.35f, 0.50f, 0.80f),
            new ElementDef(ElementType.Banner, 4, new SKColor(0x25, 0x18, 0x0A), 0.15f, 0.22f, 0.84f) { AccentColor = new SKColor(0xD4, 0xA5, 0x20) },
            new ElementDef(ElementType.Throne, 1, new SKColor(0x30, 0x22, 0x10), 0.25f, 0.35f, 0.82f)
        },
        Ground: new GroundDef(GroundType.Stone, new SKColor(0x2A, 0x22, 0x18), 0.10f, new SKColor(0x40, 0x35, 0x28)),
        Lights: new[]
        {
            new LightDef(LightType.Ambient, new SKColor(0xFF, 0xD0, 0x80), 0.12f),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0xA0, 0x30), 0.25f, 0.2f, 0.4f, 50f, true),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0xA0, 0x30), 0.25f, 0.5f, 0.35f, 50f, true),
            new LightDef(LightType.PointLight, new SKColor(0xFF, 0xA0, 0x30), 0.25f, 0.8f, 0.4f, 50f, true)
        },
        Particles: new[] { new ParticleDef(ParticleStyle.Dust, 5, new SKColor(0xD0, 0xC0, 0x90), 20) },
        Foreground: new[] { new ForegroundDef(ForegroundStyle.LightRay, new SKColor(0xFF, 0xD0, 0x80), 15, 0.0f) }
    );

    public static readonly SceneDef Dreamworld = new(
        Sky: new(new SKColor(0x10, 0x08, 0x18), new SKColor(0x0A, 0x12, 0x18), new SKColor(0x08, 0x08, 0x14)),
        Elements: new[]
        {
            new ElementDef(ElementType.GeometricFragment, 8, new SKColor(0x4A, 0x90, 0xD9, 40), 0.08f, 0.20f, 0.70f)
        },
        Ground: null,
        Lights: new[] { new LightDef(LightType.Ambient, new SKColor(0x60, 0x40, 0xA0), 0.15f) },
        Particles: new[]
        {
            new ParticleDef(ParticleStyle.GlitchLine, 10, new SKColor(0x4A, 0x90, 0xD9), 50),
            new ParticleDef(ParticleStyle.MagicOrb, 5, new SKColor(0x9B, 0x59, 0xB6), 60)
        },
        Foreground: Array.Empty<ForegroundDef>()
    );

    // ── Dictionary-Mapping ──────────────────────────────────

    private static readonly Dictionary<string, SceneDef> _scenes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["systemVoid"] = SystemVoid,
        ["title"] = Title,
        ["forest"] = ForestDay,
        ["forestDay"] = ForestDay,
        ["forestNight"] = ForestNight,
        ["campfire"] = Campfire,
        ["village"] = VillageSquare,
        ["villageSquare"] = VillageSquare,
        ["villageTavern"] = VillageTavern,
        ["dungeon"] = DungeonHalls,
        ["dungeonHalls"] = DungeonHalls,
        ["dungeonBoss"] = DungeonBoss,
        ["tower"] = TowerLibrary,
        ["towerLibrary"] = TowerLibrary,
        ["towerSummit"] = TowerSummit,
        ["battlefield"] = Battlefield,
        ["castle"] = CastleHall,
        ["castleHall"] = CastleHall,
        ["dreamworld"] = Dreamworld,
    };

    /// <summary>Liefert die SceneDef für einen backgroundKey. Fallback: ForestDay.</summary>
    public static SceneDef Get(string key) =>
        _scenes.TryGetValue(key, out var def) ? def : Default;

    /// <summary>Standard-Szene wenn kein Key passt.</summary>
    public static SceneDef Default => ForestDay;
}
