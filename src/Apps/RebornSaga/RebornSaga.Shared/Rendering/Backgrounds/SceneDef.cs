namespace RebornSaga.Rendering.Backgrounds;

using SkiaSharp;

// --- Enums ---

/// <summary>Silhouetten-Typ für Mittelgrund-Elemente.</summary>
public enum ElementType
{
    ConiferTree, DeciduousTree, Bush, DeadTree, Willow,
    House, Well, Fence,
    Rock, Boulder, Stump, Log,
    Pillar, Arch, BrokenWall,
    Bookshelf, Table, Barrel,
    Throne, Banner, SwordInGround,
    Railing, RuneCircle,
    GeometricFragment
}

/// <summary>Boden-Textur-Typ.</summary>
public enum GroundType { Grass, Stone, Wood, Sand, Snow, Water }

/// <summary>Lichtquellen-Typ.</summary>
public enum LightType { Ambient, PointLight }

/// <summary>Atmosphärischer Partikel-Stil.</summary>
public enum ParticleStyle
{
    Firefly, Spark, Dust, Leaf, Snowflake,
    MagicOrb, Ember, Star, ScanLine, GlitchLine,
    Smoke, RingOrbit
}

/// <summary>Vordergrund-Element-Stil (über Charakteren).</summary>
public enum ForegroundStyle { GrassBlade, Fog, Branch, Cobweb, LightRay }

// --- Records ---

/// <summary>Komplette Szenen-Definition aus der Komposition aller Layer.</summary>
public record SceneDef(
    SkyDef Sky,
    ElementDef[] Elements,
    GroundDef? Ground,
    LightDef[] Lights,
    ParticleDef[] Particles,
    ForegroundDef[] Foreground
);

/// <summary>Himmel-Gradient (3 Farben vertikal).</summary>
public record SkyDef(SKColor Top, SKColor Mid, SKColor Bottom, float MidStop = 0.5f);

/// <summary>Silhouetten-Element im Mittelgrund (hinter Charakteren).</summary>
public record ElementDef(
    ElementType Type,
    int Count,
    SKColor Color,
    float MinHeight,
    float MaxHeight,
    float YBase = 1f,
    float Spacing = 0f
)
{
    /// <summary>Optionale Akzentfarbe (z.B. Banner-Streifen, Fenster-Licht).</summary>
    public SKColor? AccentColor { get; init; }
}

/// <summary>Boden-Band am unteren Rand.</summary>
public record GroundDef(GroundType Type, SKColor Color, float Height = 0.15f, SKColor? AccentColor = null);

/// <summary>Lichtquelle (Ambient oder Punkt).</summary>
public record LightDef(
    LightType Type,
    SKColor Color,
    float Intensity = 0.15f,
    float X = 0.5f,
    float Y = 0.3f,
    float Radius = 80f,
    bool Flickers = false
);

/// <summary>Atmosphärische Partikel.</summary>
public record ParticleDef(ParticleStyle Style, int Count, SKColor Color, byte Alpha = 60);

/// <summary>Vordergrund-Element (über Charakteren, nur unterer Bereich).</summary>
public record ForegroundDef(ForegroundStyle Style, SKColor Color, byte Alpha = 30, float MaxY = 0.6f);
