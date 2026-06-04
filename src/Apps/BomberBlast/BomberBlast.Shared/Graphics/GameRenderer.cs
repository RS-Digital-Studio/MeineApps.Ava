using BomberBlast.Models;
using BomberBlast.Models.Cosmetics;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Models.Levels;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Rendert das Spiel mit SkiaSharp in zwei visuellen Stilen (Classic HD / Neon)
/// </summary>
public sealed partial class GameRenderer : IDisposable
{
    private bool _disposed;
    private readonly IGameStyleService _styleService;
    private readonly ICustomizationService _customizationService;

    // Rendering-Einstellungen
    private float _scale = 1f;
    private float _offsetX, _offsetY;
    private float _hudX, _hudY, _hudWidth, _hudHeight;
    private float _screenWidth, _screenHeight;

    // Atmosphärische Subsysteme
    private readonly DynamicLighting _dynamicLighting = new();
    private readonly WeatherSystem _weatherSystem = new();
    private readonly AmbientParticleSystem _ambientParticles = new();
    private readonly ShaderEffects _shaderEffects = new();
    private readonly TrailSystem _trailSystem = new();
    private readonly FogOfWarSystem _fogOfWar = new();

    /// <summary>Zugriff auf das FoW-System für GameEngine (Enable/Update-Aufrufe).</summary>
    public FogOfWarSystem FogOfWar => _fogOfWar;

    // HUD constants
    private const float HUD_LOGICAL_WIDTH = 120f;

    // Combo-Daten (gesetzt von GameEngine vor jedem Render)
    public int ComboCount { get; set; }
    public float ComboTimer { get; set; }
    public bool IsSurvivalMode { get; set; }
    public int SurvivalKills { get; set; }
    public int EnemiesRemaining { get; set; }

    // Mutator-Daten (gesetzt von GameEngine vor jedem Render)
    public LevelMutator ActiveMutator { get; set; }
    public int PlayerGridX { get; set; }
    public int PlayerGridY { get; set; }

    // Saisonales Event (v2.0.42, Plan Task 3.4): Welt-Skin-Override
    // Wenn HasActiveEvent true: Tile-Tint-Overlay + Event-spezifische Particles werden gerendert.
    public bool HasActiveEvent { get; set; }
    public SKColor EventAccentColor { get; set; } = SKColors.Transparent;
    public BomberBlast.Services.SeasonalEventType EventType { get; set; }

    // Dungeon-Buffs (gesetzt von GameEngine vor jedem Render im Dungeon-Modus)
    public bool IsDungeonRun { get; set; }
    public List<BomberBlast.Models.Dungeon.DungeonBuffType>? DungeonActiveBuffs { get; set; }
    public BomberBlast.Models.Dungeon.DungeonRoomType DungeonRoomType { get; set; }
    public BomberBlast.Models.Dungeon.DungeonFloorModifier DungeonFloorModifier { get; set; }

    // Lokalisierte HUD-Labels (gesetzt von GameEngine vor jedem Render)
    public string HudLabelKills { get; set; } = "KILLS";
    public string HudLabelTime { get; set; } = "TIME";
    public string HudLabelScore { get; set; } = "SCORE";
    public string HudLabelLives { get; set; } = "LIVES";
    public string HudLabelBombs { get; set; } = "BOMBS";
    public string HudLabelPower { get; set; } = "POWER";
    public string HudLabelDeck { get; set; } = "DECK";
    public string HudLabelBuffs { get; set; } = "BUFFS";

    /// <summary>
    /// Verschiebung nach unten fuer Banner-Ad oben (in Canvas-Einheiten).
    /// Wenn > 0, werden Grid und HUD nach unten verschoben.
    /// </summary>
    public float BannerTopOffset { get; set; }

    // ReducedEffects: Atmosphärische Systeme manuell deaktivieren (Performance-Modus, User-Toggle)
    public bool ReducedEffects { get; set; }

    /// <summary>
    /// v2.0.60 (B-C10 / WCAG 2.1): Photosensitivity-Schutz. Wird vom GameEngine.Render
    /// pro Frame aus IAccessibilityService.ReducedFlashing gesetzt. Drosselt hochfrequente
    /// Pulse-/Blitz-Effekte (Combo-Pulse 12 Hz, UltraComboFlash, Damage-Flash).
    /// </summary>
    public bool ReducedFlashing { get; set; }

    // Adaptive Frame-Skipping: Ring-Buffer der letzten N Frame-Zeiten.
    // Wenn Durchschnitt > 40ms (< 25 FPS), werden atmosphärische Systeme für
    // SkipHoldMs ms ausgesetzt, damit Gameplay (Input, Collision, AI) vollen
    // Frame-Budget bekommt. Verhindert Death-Spiral bei GC-Pausen oder CPU-Spikes.
    private const int FrameTimeBufferSize = 5;
    private readonly float[] _frameTimeBuffer = new float[FrameTimeBufferSize];
    private int _frameTimeIndex;
    private int _frameTimeCount;
    // Stabile Hysterese-Werte für 30fps-Target (33ms): Schwelle und Release
    // sind über-/unter-target gewählt damit kleine GC-Spikes nicht das Flackern
    // von atmosphärischen Effekten triggern, was als "kleine Hänger" sichtbar wird.
    private const float SkipThresholdSeconds = 0.050f;   // Avg > 50ms (≤20 FPS) → skip
    private const float SkipReleaseSeconds = 0.036f;     // Avg < 36ms (≥27 FPS) → release
    private const float SkipHoldMinSeconds = 1.0f;       // Minimum-Hold-Zeit: 1.0s (verhindert schnelles Toggle)
    private float _skipHoldRemaining;
    private bool _adaptiveSkipActive;

    /// <summary>
    /// Ob atmosphärische Systeme (WeatherSystem, AmbientParticleSystem, TrailSystem,
    /// Background-Elements) für diesen Frame ausgesetzt werden sollen. Kombiniert
    /// manuellen User-Toggle (<see cref="ReducedEffects"/>) mit adaptiver
    /// Frame-Skipping-Logik.
    /// </summary>
    public bool SkipAtmosphere => ReducedEffects || _adaptiveSkipActive;

    // Animations-Timing
    private float _globalTimer;
    private float _lastDeltaTime;

    // Rainbow-Explosion: Farben nur alle 3 Frames aktualisieren (statt pro Frame)
    private int _rainbowUpdateCounter;

    // Nebel-Overlay (Welt 10: Schattenwelt)
    private bool _fogEnabled;

    // Gecachte Shader (vermeidet native SKShader-Allokationen pro Frame)
    private SKShader? _bgShader;
    private int _bgShaderWorldIndex = -1;
    private float _bgShaderHeight;
    private SKShader? _vignetteShader;
    private float _vignetteShaderW, _vignetteShaderH;
    private int _vignetteShaderWorldIndex = -1;

    // Aktuelle Palette (wird bei Style-Wechsel getauscht)
    private StylePalette _palette;

    // Effektive Explosionsfarben (Skin-ueberschreibt Palette)
    private SKColor _explOuter, _explInner, _explCore;

    // ═══════════════════════════════════════════════════════════════════════
    // COLOR PALETTES
    // ═══════════════════════════════════════════════════════════════════════

    private sealed class StylePalette
    {
        // Hintergrund
        public SKColor Background;

        // Boden
        public SKColor FloorBase;
        public SKColor FloorAlt;
        public SKColor FloorLine;

        // Wand
        public SKColor WallBase;
        public SKColor WallHighlight;
        public SKColor WallShadow;
        public SKColor WallEdge;

        // Block (zerstoerbar)
        public SKColor BlockBase;
        public SKColor BlockMortar;
        public SKColor BlockHighlight;
        public SKColor BlockShadow;

        // Ausgang
        public SKColor ExitGlow;
        public SKColor ExitInner;

        // Bombe
        public SKColor BombBody;
        public SKColor BombGlowColor;
        public SKColor BombFuse;
        public SKColor BombHighlight;

        // Explosion
        public SKColor ExplosionOuter;
        public SKColor ExplosionInner;
        public SKColor ExplosionCore;

        // Spieler
        public SKColor PlayerBody;
        public SKColor PlayerHelm;
        public SKColor PlayerAura;

        // Gegner
        public SKColor EnemyAura;

        // HUD
        public SKColor HudBg;
        public SKColor HudBorder;
        public SKColor HudText;
        public SKColor HudAccent;
        public SKColor HudTimeWarning;
    }

    private static readonly StylePalette ClassicPalette = new()
    {
        Background = new SKColor(40, 40, 45),

        FloorBase = new SKColor(220, 210, 190),
        FloorAlt = new SKColor(210, 200, 180),
        FloorLine = new SKColor(185, 175, 155),

        WallBase = new SKColor(80, 85, 95),
        WallHighlight = new SKColor(120, 125, 135),
        WallShadow = new SKColor(50, 52, 60),
        WallEdge = new SKColor(80, 85, 95),

        BlockBase = new SKColor(180, 120, 60),
        BlockMortar = new SKColor(210, 170, 110),
        BlockHighlight = new SKColor(210, 155, 85),
        BlockShadow = new SKColor(130, 85, 40),

        ExitGlow = new SKColor(50, 255, 100),
        ExitInner = new SKColor(0, 200, 80),

        BombBody = new SKColor(30, 30, 35),
        BombGlowColor = new SKColor(255, 100, 0),
        BombFuse = new SKColor(230, 140, 40),
        BombHighlight = new SKColor(200, 200, 210),

        ExplosionOuter = new SKColor(255, 150, 50),
        ExplosionInner = new SKColor(255, 220, 100),
        ExplosionCore = new SKColor(255, 255, 230),

        PlayerBody = new SKColor(245, 245, 250),
        PlayerHelm = new SKColor(60, 100, 200),
        PlayerAura = SKColor.Empty,

        EnemyAura = SKColor.Empty,

        HudBg = new SKColor(35, 35, 45, 235),
        HudBorder = new SKColor(80, 80, 100),
        HudText = SKColors.White,
        HudAccent = new SKColor(255, 220, 80),
        HudTimeWarning = new SKColor(255, 60, 60),
    };

    private static readonly StylePalette RetroPalette = new()
    {
        Background = new SKColor(48, 56, 32),

        FloorBase = new SKColor(156, 172, 112),
        FloorAlt = new SKColor(140, 156, 100),
        FloorLine = new SKColor(120, 136, 84),

        WallBase = new SKColor(72, 80, 56),
        WallHighlight = new SKColor(96, 108, 72),
        WallShadow = new SKColor(48, 52, 36),
        WallEdge = new SKColor(64, 72, 48),

        BlockBase = new SKColor(168, 128, 72),
        BlockMortar = new SKColor(192, 156, 96),
        BlockHighlight = new SKColor(184, 144, 84),
        BlockShadow = new SKColor(120, 88, 48),

        ExitGlow = new SKColor(80, 200, 80),
        ExitInner = new SKColor(40, 160, 60),

        BombBody = new SKColor(32, 28, 24),
        BombGlowColor = new SKColor(200, 80, 24),
        BombFuse = new SKColor(192, 120, 40),
        BombHighlight = new SKColor(160, 148, 120),

        ExplosionOuter = new SKColor(216, 128, 32),
        ExplosionInner = new SKColor(240, 192, 64),
        ExplosionCore = new SKColor(248, 240, 200),

        PlayerBody = new SKColor(224, 216, 192),
        PlayerHelm = new SKColor(72, 120, 176),
        PlayerAura = SKColor.Empty,

        EnemyAura = SKColor.Empty,

        HudBg = new SKColor(40, 48, 28, 235),
        HudBorder = new SKColor(96, 108, 72),
        HudText = new SKColor(224, 224, 200),
        HudAccent = new SKColor(216, 192, 64),
        HudTimeWarning = new SKColor(200, 48, 32),
    };

    private static readonly StylePalette NeonPalette = new()
    {
        Background = new SKColor(12, 14, 22),

        FloorBase = new SKColor(30, 34, 48),
        FloorAlt = new SKColor(26, 30, 42),
        FloorLine = new SKColor(0, 180, 220, 50),

        WallBase = new SKColor(50, 58, 80),
        WallHighlight = new SKColor(0, 200, 255, 120),
        WallShadow = new SKColor(28, 32, 50),
        WallEdge = new SKColor(0, 200, 255, 200),

        BlockBase = new SKColor(70, 60, 50),
        BlockMortar = new SKColor(255, 130, 40, 170),
        BlockHighlight = new SKColor(255, 150, 60, 100),
        BlockShadow = new SKColor(40, 32, 25),

        ExitGlow = new SKColor(0, 255, 150),
        ExitInner = new SKColor(0, 200, 120),

        BombBody = new SKColor(25, 20, 25),
        BombGlowColor = new SKColor(255, 40, 40),
        BombFuse = new SKColor(255, 80, 40),
        BombHighlight = new SKColor(60, 50, 70),

        ExplosionOuter = new SKColor(255, 120, 30),
        ExplosionInner = new SKColor(255, 255, 255),
        ExplosionCore = new SKColor(0, 220, 255),

        PlayerBody = new SKColor(240, 240, 255),
        PlayerHelm = new SKColor(0, 200, 255),
        PlayerAura = new SKColor(0, 200, 255, 40),

        EnemyAura = new SKColor(255, 255, 255, 30),

        HudBg = new SKColor(12, 14, 22, 235),
        HudBorder = new SKColor(0, 200, 255, 120),
        HudText = new SKColor(220, 240, 255),
        HudAccent = new SKColor(0, 255, 200),
        HudTimeWarning = new SKColor(255, 40, 80),
    };

    // ═══════════════════════════════════════════════════════════════════════
    // WELT-THEMES (5 Welten, je Classic + Neon Variante)
    // ═══════════════════════════════════════════════════════════════════════

    private sealed class WorldPalette
    {
        public SKColor Floor1, Floor2, FloorLine;
        public SKColor WallMain, WallHighlight, WallShadow;
        public SKColor BlockMain, BlockMortar, BlockHighlight, BlockShadow;
        public SKColor Accent;
    }

    // ───────────────────────────────────────────────────────────────────────
    // BombFxTheme — Welt-spezifische Bomb/Explosion-Farben (
    // ───────────────────────────────────────────────────────────────────────
    // Wird in RenderBomb (default-Branch ohne Custom-Skin) und im Explosion-
    // Renderer als Override verwendet. Custom-Skins haben weiterhin Vorrang.
    private sealed class BombFxTheme
    {
        public SKColor BombBody;        // Bombenkörper
        public SKColor BombGlow;        // Pulse-Glow um Bombe
        public SKColor BombFuse;        // Zündschnur-Farbe
        public SKColor BombHighlight;   // Glanzpunkt oben links
        public SKColor SparkGlow;       // Funken-Halo am Schnur-Ende
        public SKColor SparkCore;       // Funken-Stern (innerer Punkt)
        public SKColor ExplosionOuter;  // äußerer Explosion-Ring
        public SKColor ExplosionInner;  // mittlerer Explosion-Ring
        public SKColor ExplosionCore;   // innerer heißer Kern
    }

    // Classic Welt-Bomb-FX-Themes (10 Welten)
    private static readonly BombFxTheme[] ClassicBombFx =
    [
        // Welt 1: Forest — klassisches Orange (Default)
        new() { BombBody = new(30,30,35), BombGlow = new(255,100,0), BombFuse = new(230,140,40),
                BombHighlight = new(200,200,210),
                SparkGlow = new(255,180,50), SparkCore = SKColors.Yellow,
                ExplosionOuter = new(255,150,50), ExplosionInner = new(255,220,100), ExplosionCore = new(255,255,230) },
        // Welt 2: Industrial — kühles Blau-Stahl
        new() { BombBody = new(28,32,42), BombGlow = new(80,180,255), BombFuse = new(120,200,255),
                BombHighlight = new(180,200,230),
                SparkGlow = new(100,200,255), SparkCore = new(220,240,255),
                ExplosionOuter = new(80,160,255), ExplosionInner = new(180,220,255), ExplosionCore = new(240,250,255) },
        // Welt 3: Cavern — mystisches Lila
        new() { BombBody = new(35,25,45), BombGlow = new(180,80,255), BombFuse = new(200,120,255),
                BombHighlight = new(210,180,230),
                SparkGlow = new(200,100,255), SparkCore = new(230,180,255),
                ExplosionOuter = new(160,80,240), ExplosionInner = new(220,160,255), ExplosionCore = new(255,230,255) },
        // Welt 4: Sky — leuchtendes Cyan
        new() { BombBody = new(35,45,55), BombGlow = new(0,220,255), BombFuse = new(80,230,255),
                BombHighlight = new(200,235,245),
                SparkGlow = new(0,240,255), SparkCore = new(220,250,255),
                ExplosionOuter = new(0,200,240), ExplosionInner = new(150,230,255), ExplosionCore = new(240,255,255) },
        // Welt 5: Inferno — tiefes Rot/Orange
        new() { BombBody = new(40,15,15), BombGlow = new(255,40,20), BombFuse = new(255,80,20),
                BombHighlight = new(220,140,100),
                SparkGlow = new(255,80,0), SparkCore = new(255,200,80),
                ExplosionOuter = new(220,40,20), ExplosionInner = new(255,140,40), ExplosionCore = new(255,230,150) },
        // Welt 6: Ruinen — antikes Gold
        new() { BombBody = new(45,35,20), BombGlow = new(255,200,40), BombFuse = new(255,220,80),
                BombHighlight = new(220,200,140),
                SparkGlow = new(255,210,60), SparkCore = new(255,240,180),
                ExplosionOuter = new(220,170,40), ExplosionInner = new(255,220,120), ExplosionCore = new(255,250,200) },
        // Welt 7: Ozean — Türkis/Aqua
        new() { BombBody = new(20,35,45), BombGlow = new(0,200,220), BombFuse = new(60,220,230),
                BombHighlight = new(180,220,230),
                SparkGlow = new(0,220,240), SparkCore = new(200,250,255),
                ExplosionOuter = new(0,180,210), ExplosionInner = new(150,230,240), ExplosionCore = new(230,255,255) },
        // Welt 8: Vulkan — glühendes Magma-Orange
        new() { BombBody = new(40,15,10), BombGlow = new(255,120,0), BombFuse = new(255,150,30),
                BombHighlight = new(220,160,100),
                SparkGlow = new(255,140,0), SparkCore = new(255,210,100),
                ExplosionOuter = new(255,100,0), ExplosionInner = new(255,180,40), ExplosionCore = new(255,240,180) },
        // Welt 9: Himmelsfestung — heiliges Weiß-Gold
        new() { BombBody = new(40,35,25), BombGlow = new(255,230,120), BombFuse = new(255,240,180),
                BombHighlight = new(240,230,200),
                SparkGlow = new(255,240,150), SparkCore = SKColors.White,
                ExplosionOuter = new(255,200,80), ExplosionInner = new(255,240,180), ExplosionCore = SKColors.White },
        // Welt 10: Schattenwelt — dämonisches Violett
        new() { BombBody = new(20,10,30), BombGlow = new(180,40,255), BombFuse = new(200,80,255),
                BombHighlight = new(180,150,210),
                SparkGlow = new(200,60,255), SparkCore = new(240,180,255),
                ExplosionOuter = new(140,40,200), ExplosionInner = new(200,120,255), ExplosionCore = new(255,220,255) },
    ];

    // Neon Welt-Bomb-FX-Themes (gesättigter, leuchtender)
    private static readonly BombFxTheme[] NeonBombFx =
    [
        // Welt 1: Forest — Neon-Lime
        new() { BombBody = new(15,25,18), BombGlow = new(0,255,100), BombFuse = new(80,255,140),
                BombHighlight = new(150,255,200),
                SparkGlow = new(0,255,120), SparkCore = new(180,255,200),
                ExplosionOuter = new(0,220,80), ExplosionInner = new(120,255,160), ExplosionCore = new(220,255,240) },
        // Welt 2: Industrial — Neon-Blau (Cybertron)
        new() { BombBody = new(15,18,30), BombGlow = new(0,160,255), BombFuse = new(80,200,255),
                BombHighlight = new(150,210,255),
                SparkGlow = new(0,180,255), SparkCore = new(180,230,255),
                ExplosionOuter = new(0,140,255), ExplosionInner = new(120,200,255), ExplosionCore = new(230,245,255) },
        // Welt 3: Cavern — Neon-Magenta
        new() { BombBody = new(25,10,35), BombGlow = new(220,40,255), BombFuse = new(240,100,255),
                BombHighlight = new(220,170,240),
                SparkGlow = new(230,80,255), SparkCore = new(255,200,255),
                ExplosionOuter = new(200,40,240), ExplosionInner = new(240,140,255), ExplosionCore = new(255,230,255) },
        // Welt 4: Sky — Elektro-Cyan
        new() { BombBody = new(15,28,35), BombGlow = new(0,240,255), BombFuse = new(100,250,255),
                BombHighlight = new(200,245,255),
                SparkGlow = new(0,250,255), SparkCore = SKColors.White,
                ExplosionOuter = new(0,220,255), ExplosionInner = new(160,240,255), ExplosionCore = SKColors.White },
        // Welt 5: Inferno — Plasma-Rot
        new() { BombBody = new(30,8,8), BombGlow = new(255,30,40), BombFuse = new(255,60,60),
                BombHighlight = new(220,140,140),
                SparkGlow = new(255,40,40), SparkCore = new(255,200,180),
                ExplosionOuter = new(255,40,30), ExplosionInner = new(255,140,80), ExplosionCore = new(255,240,200) },
        // Welt 6: Ruinen — Neon-Gold
        new() { BombBody = new(30,22,8), BombGlow = new(255,200,0), BombFuse = new(255,220,40),
                BombHighlight = new(240,220,140),
                SparkGlow = new(255,210,20), SparkCore = new(255,250,180),
                ExplosionOuter = new(255,180,0), ExplosionInner = new(255,220,80), ExplosionCore = new(255,250,200) },
        // Welt 7: Ozean — Aqua-Türkis
        new() { BombBody = new(10,25,32), BombGlow = new(0,220,240), BombFuse = new(60,240,250),
                BombHighlight = new(180,235,245),
                SparkGlow = new(0,240,255), SparkCore = SKColors.White,
                ExplosionOuter = new(0,200,230), ExplosionInner = new(150,235,250), ExplosionCore = new(230,255,255) },
        // Welt 8: Vulkan — Glühendes Orange
        new() { BombBody = new(35,10,5), BombGlow = new(255,120,0), BombFuse = new(255,150,20),
                BombHighlight = new(220,160,80),
                SparkGlow = new(255,140,0), SparkCore = new(255,220,120),
                ExplosionOuter = new(255,100,0), ExplosionInner = new(255,180,40), ExplosionCore = new(255,240,180) },
        // Welt 9: Himmelsfestung — Heiliges Weiß
        new() { BombBody = new(30,28,18), BombGlow = new(255,240,180), BombFuse = new(255,250,210),
                BombHighlight = new(245,240,210),
                SparkGlow = new(255,250,200), SparkCore = SKColors.White,
                ExplosionOuter = new(255,220,120), ExplosionInner = new(255,250,200), ExplosionCore = SKColors.White },
        // Welt 10: Schattenwelt — Geisterhaftes Violett
        new() { BombBody = new(15,8,25), BombGlow = new(200,60,255), BombFuse = new(220,100,255),
                BombHighlight = new(200,170,230),
                SparkGlow = new(220,80,255), SparkCore = new(255,200,255),
                ExplosionOuter = new(160,40,220), ExplosionInner = new(220,140,255), ExplosionCore = new(255,230,255) },
    ];

    // Retro Welt-Bomb-FX-Themes (gedämpft, Pixel-Art-tauglich)
    private static readonly BombFxTheme[] RetroBombFx =
    [
        // Welt 1: Forest
        new() { BombBody = new(32,28,24), BombGlow = new(200,80,24), BombFuse = new(192,120,40),
                BombHighlight = new(160,148,120),
                SparkGlow = new(216,160,72), SparkCore = new(248,224,128),
                ExplosionOuter = new(216,128,32), ExplosionInner = new(240,192,64), ExplosionCore = new(248,240,200) },
        // Welt 2: Industrial — Stahl-Blau
        new() { BombBody = new(28,32,40), BombGlow = new(80,140,200), BombFuse = new(120,168,216),
                BombHighlight = new(168,184,208),
                SparkGlow = new(100,160,216), SparkCore = new(208,232,248),
                ExplosionOuter = new(72,128,184), ExplosionInner = new(176,200,232), ExplosionCore = new(232,240,248) },
        // Welt 3: Cavern — Lila
        new() { BombBody = new(32,24,36), BombGlow = new(160,80,200), BombFuse = new(184,120,216),
                BombHighlight = new(192,168,200),
                SparkGlow = new(180,100,216), SparkCore = new(232,200,240),
                ExplosionOuter = new(144,72,192), ExplosionInner = new(208,160,232), ExplosionCore = new(240,224,248) },
        // Welt 4: Sky — Hellblau
        new() { BombBody = new(28,40,52), BombGlow = new(72,184,216), BombFuse = new(120,208,232),
                BombHighlight = new(192,224,232),
                SparkGlow = new(96,200,224), SparkCore = new(216,240,248),
                ExplosionOuter = new(56,168,200), ExplosionInner = new(160,216,232), ExplosionCore = new(232,248,248) },
        // Welt 5: Inferno — Dunkelrot
        new() { BombBody = new(40,16,16), BombGlow = new(200,56,32), BombFuse = new(216,88,40),
                BombHighlight = new(208,144,128),
                SparkGlow = new(216,80,32), SparkCore = new(248,200,120),
                ExplosionOuter = new(184,48,24), ExplosionInner = new(232,144,72), ExplosionCore = new(248,232,168) },
        // Welt 6: Ruinen — Sand
        new() { BombBody = new(40,32,20), BombGlow = new(200,168,72), BombFuse = new(216,184,96),
                BombHighlight = new(208,192,144),
                SparkGlow = new(216,176,80), SparkCore = new(248,232,168),
                ExplosionOuter = new(184,144,48), ExplosionInner = new(232,200,120), ExplosionCore = new(248,240,200) },
        // Welt 7: Ozean — Blaugrün
        new() { BombBody = new(20,32,40), BombGlow = new(48,160,184), BombFuse = new(80,192,208),
                BombHighlight = new(176,216,224),
                SparkGlow = new(64,184,200), SparkCore = new(208,240,248),
                ExplosionOuter = new(40,144,176), ExplosionInner = new(160,208,224), ExplosionCore = new(232,248,248) },
        // Welt 8: Vulkan — Orange
        new() { BombBody = new(36,16,8), BombGlow = new(216,88,24), BombFuse = new(232,120,40),
                BombHighlight = new(208,160,112),
                SparkGlow = new(232,112,32), SparkCore = new(248,208,128),
                ExplosionOuter = new(200,80,16), ExplosionInner = new(232,160,80), ExplosionCore = new(248,232,176) },
        // Welt 9: Himmelsfestung — Beige-Gold
        new() { BombBody = new(40,36,24), BombGlow = new(216,184,48), BombFuse = new(232,208,96),
                BombHighlight = new(232,216,176),
                SparkGlow = new(224,196,72), SparkCore = new(248,240,208),
                ExplosionOuter = new(200,168,40), ExplosionInner = new(232,216,128), ExplosionCore = new(248,240,208) },
        // Welt 10: Schattenwelt — Dunkelviolett
        new() { BombBody = new(24,16,36), BombGlow = new(152,72,200), BombFuse = new(184,112,216),
                BombHighlight = new(184,160,200),
                SparkGlow = new(176,96,216), SparkCore = new(232,200,240),
                ExplosionOuter = new(136,56,192), ExplosionInner = new(200,152,232), ExplosionCore = new(240,224,248) },
    ];

    // Aktives Welt-Bomb-FX-Theme (wird bei Level-Wechsel gesetzt)
    private BombFxTheme? _bombFxTheme;

    // Classic Welt-Paletten
    private static readonly WorldPalette[] ClassicWorldPalettes =
    [
        // Welt 1: Forest (Grün)
        new() { Floor1 = new(180, 210, 160), Floor2 = new(160, 190, 140), FloorLine = new(140, 170, 120),
                WallMain = new(85, 90, 80), WallHighlight = new(125, 130, 115), WallShadow = new(55, 58, 50),
                BlockMain = new(140, 105, 55), BlockMortar = new(170, 140, 90), BlockHighlight = new(165, 130, 70), BlockShadow = new(105, 75, 35),
                Accent = new(80, 200, 80) },
        // Welt 2: Industrial (Grau/Blau)
        new() { Floor1 = new(195, 195, 200), Floor2 = new(180, 180, 188), FloorLine = new(160, 160, 170),
                WallMain = new(70, 80, 100), WallHighlight = new(100, 115, 140), WallShadow = new(45, 50, 65),
                BlockMain = new(170, 120, 70), BlockMortar = new(200, 155, 95), BlockHighlight = new(195, 140, 80), BlockShadow = new(120, 80, 40),
                Accent = new(80, 140, 220) },
        // Welt 3: Cavern (Lila)
        new() { Floor1 = new(190, 175, 200), Floor2 = new(170, 155, 185), FloorLine = new(150, 135, 165),
                WallMain = new(65, 55, 80), WallHighlight = new(100, 85, 125), WallShadow = new(40, 32, 55),
                BlockMain = new(140, 100, 160), BlockMortar = new(175, 140, 190), BlockHighlight = new(160, 120, 175), BlockShadow = new(100, 65, 120),
                Accent = new(180, 100, 240) },
        // Welt 4: Sky (Cyan/Blau)
        new() { Floor1 = new(200, 220, 235), Floor2 = new(185, 210, 228), FloorLine = new(165, 195, 215),
                WallMain = new(220, 225, 235), WallHighlight = new(240, 245, 250), WallShadow = new(180, 190, 210),
                BlockMain = new(150, 200, 220), BlockMortar = new(180, 220, 235), BlockHighlight = new(170, 215, 230), BlockShadow = new(110, 170, 195),
                Accent = new(0, 200, 240) },
        // Welt 5: Inferno (Rot/Schwarz)
        new() { Floor1 = new(120, 70, 60), Floor2 = new(100, 55, 45), FloorLine = new(80, 40, 30),
                WallMain = new(45, 40, 45), WallHighlight = new(70, 60, 65), WallShadow = new(25, 20, 25),
                BlockMain = new(200, 100, 40), BlockMortar = new(230, 140, 60), BlockHighlight = new(220, 120, 50), BlockShadow = new(150, 65, 25),
                Accent = new(240, 60, 40) },
        // Welt 6: Ruinen (Sandstein/Antik)
        new() { Floor1 = new(210, 195, 165), Floor2 = new(195, 180, 150), FloorLine = new(175, 160, 130),
                WallMain = new(140, 120, 90), WallHighlight = new(180, 160, 120), WallShadow = new(100, 85, 60),
                BlockMain = new(190, 160, 110), BlockMortar = new(210, 185, 140), BlockHighlight = new(200, 175, 130), BlockShadow = new(140, 115, 75),
                Accent = new(210, 180, 100) },
        // Welt 7: Ozean (Unterwasser/Blau)
        new() { Floor1 = new(140, 180, 200), Floor2 = new(120, 165, 190), FloorLine = new(100, 145, 175),
                WallMain = new(50, 80, 110), WallHighlight = new(80, 120, 160), WallShadow = new(30, 55, 80),
                BlockMain = new(80, 140, 170), BlockMortar = new(110, 170, 200), BlockHighlight = new(100, 160, 190), BlockShadow = new(50, 100, 130),
                Accent = new(0, 180, 220) },
        // Welt 8: Vulkan (Dunkelrot/Lava)
        new() { Floor1 = new(90, 50, 40), Floor2 = new(75, 40, 30), FloorLine = new(60, 30, 20),
                WallMain = new(40, 30, 30), WallHighlight = new(65, 45, 40), WallShadow = new(20, 15, 15),
                BlockMain = new(180, 80, 30), BlockMortar = new(220, 120, 40), BlockHighlight = new(200, 100, 35), BlockShadow = new(130, 55, 20),
                Accent = new(255, 100, 20) },
        // Welt 9: Himmelsfestung (Gold/Weiss)
        new() { Floor1 = new(230, 225, 210), Floor2 = new(220, 215, 195), FloorLine = new(200, 190, 170),
                WallMain = new(220, 200, 160), WallHighlight = new(245, 230, 190), WallShadow = new(180, 165, 130),
                BlockMain = new(210, 190, 140), BlockMortar = new(235, 215, 170), BlockHighlight = new(225, 205, 155), BlockShadow = new(175, 155, 110),
                Accent = new(255, 215, 0) },
        // Welt 10: Schattenwelt (Schwarz/Violett)
        new() { Floor1 = new(55, 40, 70), Floor2 = new(45, 30, 60), FloorLine = new(35, 20, 50),
                WallMain = new(30, 20, 45), WallHighlight = new(60, 40, 80), WallShadow = new(15, 10, 25),
                BlockMain = new(80, 50, 100), BlockMortar = new(110, 70, 130), BlockHighlight = new(95, 60, 115), BlockShadow = new(55, 35, 70),
                Accent = new(180, 80, 255) },
    ];

    // Neon Welt-Paletten (dunkler, leuchtender)
    private static readonly WorldPalette[] NeonWorldPalettes =
    [
        // Welt 1: Forest (Neon-Grün)
        new() { Floor1 = new(25, 40, 30), Floor2 = new(20, 35, 25), FloorLine = new(0, 180, 80, 50),
                WallMain = new(35, 50, 40), WallHighlight = new(0, 220, 80, 120), WallShadow = new(18, 28, 22),
                BlockMain = new(55, 45, 30), BlockMortar = new(0, 200, 80, 170), BlockHighlight = new(0, 180, 60, 100), BlockShadow = new(30, 24, 16),
                Accent = new(0, 255, 100) },
        // Welt 2: Industrial (Neon-Blau)
        new() { Floor1 = new(28, 30, 42), Floor2 = new(24, 26, 38), FloorLine = new(0, 140, 255, 50),
                WallMain = new(40, 45, 65), WallHighlight = new(0, 150, 255, 120), WallShadow = new(22, 25, 40),
                BlockMain = new(60, 50, 40), BlockMortar = new(0, 160, 255, 170), BlockHighlight = new(0, 140, 230, 100), BlockShadow = new(35, 28, 22),
                Accent = new(0, 160, 255) },
        // Welt 3: Cavern (Neon-Lila)
        new() { Floor1 = new(32, 24, 45), Floor2 = new(26, 20, 38), FloorLine = new(180, 0, 255, 50),
                WallMain = new(45, 35, 60), WallHighlight = new(180, 0, 255, 120), WallShadow = new(24, 18, 35),
                BlockMain = new(50, 35, 60), BlockMortar = new(180, 80, 255, 170), BlockHighlight = new(160, 60, 230, 100), BlockShadow = new(30, 20, 40),
                Accent = new(200, 80, 255) },
        // Welt 4: Sky (Neon-Cyan)
        new() { Floor1 = new(22, 32, 42), Floor2 = new(18, 28, 38), FloorLine = new(0, 220, 240, 50),
                WallMain = new(35, 50, 60), WallHighlight = new(0, 240, 255, 120), WallShadow = new(16, 24, 32),
                BlockMain = new(30, 55, 65), BlockMortar = new(0, 230, 255, 170), BlockHighlight = new(0, 210, 240, 100), BlockShadow = new(16, 38, 48),
                Accent = new(0, 240, 255) },
        // Welt 5: Inferno (Neon-Rot)
        new() { Floor1 = new(40, 18, 18), Floor2 = new(34, 14, 14), FloorLine = new(255, 40, 0, 50),
                WallMain = new(50, 25, 25), WallHighlight = new(255, 40, 40, 120), WallShadow = new(28, 12, 12),
                BlockMain = new(65, 30, 20), BlockMortar = new(255, 80, 0, 170), BlockHighlight = new(255, 60, 30, 100), BlockShadow = new(40, 18, 10),
                Accent = new(255, 40, 40) },
        // Welt 6: Ruinen (Neon-Gold)
        new() { Floor1 = new(35, 30, 20), Floor2 = new(30, 25, 16), FloorLine = new(255, 200, 0, 50),
                WallMain = new(50, 42, 28), WallHighlight = new(255, 200, 50, 120), WallShadow = new(28, 24, 14),
                BlockMain = new(60, 48, 25), BlockMortar = new(255, 190, 40, 170), BlockHighlight = new(240, 180, 30, 100), BlockShadow = new(38, 30, 14),
                Accent = new(255, 200, 0) },
        // Welt 7: Ozean (Neon-Türkis)
        new() { Floor1 = new(18, 32, 40), Floor2 = new(14, 28, 36), FloorLine = new(0, 200, 220, 50),
                WallMain = new(25, 45, 55), WallHighlight = new(0, 220, 240, 120), WallShadow = new(12, 24, 32),
                BlockMain = new(20, 50, 60), BlockMortar = new(0, 210, 230, 170), BlockHighlight = new(0, 190, 210, 100), BlockShadow = new(10, 32, 42),
                Accent = new(0, 220, 240) },
        // Welt 8: Vulkan (Neon-Orange)
        new() { Floor1 = new(38, 16, 12), Floor2 = new(32, 12, 8), FloorLine = new(255, 120, 0, 50),
                WallMain = new(48, 22, 16), WallHighlight = new(255, 120, 20, 120), WallShadow = new(26, 10, 6),
                BlockMain = new(60, 28, 14), BlockMortar = new(255, 100, 0, 170), BlockHighlight = new(240, 90, 10, 100), BlockShadow = new(38, 16, 8),
                Accent = new(255, 120, 0) },
        // Welt 9: Himmelsfestung (Neon-Weiss/Gold)
        new() { Floor1 = new(30, 28, 24), Floor2 = new(26, 24, 20), FloorLine = new(255, 240, 200, 50),
                WallMain = new(45, 40, 32), WallHighlight = new(255, 240, 180, 120), WallShadow = new(22, 20, 16),
                BlockMain = new(48, 42, 30), BlockMortar = new(255, 230, 160, 170), BlockHighlight = new(240, 220, 150, 100), BlockShadow = new(30, 26, 18),
                Accent = new(255, 230, 100) },
        // Welt 10: Schattenwelt (Neon-Violett)
        new() { Floor1 = new(22, 14, 35), Floor2 = new(18, 10, 30), FloorLine = new(180, 0, 255, 50),
                WallMain = new(30, 18, 48), WallHighlight = new(200, 40, 255, 120), WallShadow = new(14, 8, 25),
                BlockMain = new(38, 22, 55), BlockMortar = new(180, 60, 255, 170), BlockHighlight = new(160, 40, 240, 100), BlockShadow = new(22, 12, 35),
                Accent = new(200, 60, 255) },
    ];

    // Retro Welt-Paletten (gedämpfte Pixel-Art-Farben, Game-Boy-/8-Bit-Ästhetik)
    private static readonly WorldPalette[] RetroWorldPalettes =
    [
        // Welt 1: Forest (Grün-Braun)
        new() { Floor1 = new(144, 168, 104), Floor2 = new(128, 152, 88), FloorLine = new(108, 128, 72),
                WallMain = new(72, 80, 56), WallHighlight = new(96, 108, 72), WallShadow = new(48, 52, 36),
                BlockMain = new(136, 104, 56), BlockMortar = new(160, 128, 80), BlockHighlight = new(148, 116, 68), BlockShadow = new(96, 72, 40),
                Accent = new(80, 176, 64) },
        // Welt 2: Industrial (Grau-Blau)
        new() { Floor1 = new(152, 152, 160), Floor2 = new(136, 136, 148), FloorLine = new(116, 116, 128),
                WallMain = new(64, 72, 88), WallHighlight = new(88, 100, 120), WallShadow = new(40, 44, 56),
                BlockMain = new(144, 108, 64), BlockMortar = new(168, 136, 88), BlockHighlight = new(156, 120, 76), BlockShadow = new(104, 76, 40),
                Accent = new(80, 128, 184) },
        // Welt 3: Cavern (Lila-Braun)
        new() { Floor1 = new(152, 136, 164), Floor2 = new(136, 120, 148), FloorLine = new(116, 100, 128),
                WallMain = new(56, 48, 72), WallHighlight = new(80, 68, 100), WallShadow = new(36, 28, 48),
                BlockMain = new(120, 88, 136), BlockMortar = new(148, 116, 160), BlockHighlight = new(132, 100, 148), BlockShadow = new(84, 56, 100),
                Accent = new(152, 88, 200) },
        // Welt 4: Sky (Blassblau)
        new() { Floor1 = new(168, 188, 200), Floor2 = new(152, 176, 192), FloorLine = new(132, 156, 172),
                WallMain = new(184, 192, 200), WallHighlight = new(208, 216, 224), WallShadow = new(148, 156, 172),
                BlockMain = new(128, 168, 188), BlockMortar = new(152, 188, 204), BlockHighlight = new(140, 180, 196), BlockShadow = new(92, 136, 160),
                Accent = new(64, 168, 200) },
        // Welt 5: Inferno (Dunkelrot)
        new() { Floor1 = new(104, 60, 48), Floor2 = new(88, 48, 36), FloorLine = new(68, 36, 24),
                WallMain = new(40, 36, 36), WallHighlight = new(60, 52, 52), WallShadow = new(24, 20, 20),
                BlockMain = new(168, 88, 36), BlockMortar = new(196, 120, 52), BlockHighlight = new(184, 104, 44), BlockShadow = new(120, 56, 24),
                Accent = new(200, 56, 32) },
        // Welt 6: Ruinen (Sand)
        new() { Floor1 = new(184, 168, 140), Floor2 = new(168, 152, 124), FloorLine = new(148, 132, 104),
                WallMain = new(120, 100, 72), WallHighlight = new(152, 132, 100), WallShadow = new(84, 68, 48),
                BlockMain = new(164, 136, 92), BlockMortar = new(184, 160, 120), BlockHighlight = new(176, 148, 108), BlockShadow = new(116, 96, 60),
                Accent = new(184, 160, 80) },
        // Welt 7: Ozean (Blaugrün)
        new() { Floor1 = new(116, 152, 168), Floor2 = new(100, 136, 156), FloorLine = new(80, 116, 140),
                WallMain = new(44, 68, 92), WallHighlight = new(68, 100, 132), WallShadow = new(28, 48, 68),
                BlockMain = new(68, 120, 144), BlockMortar = new(92, 144, 168), BlockHighlight = new(80, 132, 156), BlockShadow = new(44, 84, 108),
                Accent = new(48, 152, 184) },
        // Welt 8: Vulkan (Dunkelorange)
        new() { Floor1 = new(76, 44, 32), Floor2 = new(64, 36, 24), FloorLine = new(52, 28, 16),
                WallMain = new(36, 28, 24), WallHighlight = new(56, 40, 32), WallShadow = new(20, 16, 12),
                BlockMain = new(152, 72, 28), BlockMortar = new(184, 104, 36), BlockHighlight = new(168, 88, 32), BlockShadow = new(108, 48, 20),
                Accent = new(216, 88, 24) },
        // Welt 9: Himmelsfestung (Gold-Beige)
        new() { Floor1 = new(200, 192, 176), Floor2 = new(188, 180, 160), FloorLine = new(168, 160, 140),
                WallMain = new(188, 172, 136), WallHighlight = new(212, 200, 164), WallShadow = new(152, 140, 108),
                BlockMain = new(180, 164, 120), BlockMortar = new(200, 188, 148), BlockHighlight = new(192, 176, 136), BlockShadow = new(148, 132, 92),
                Accent = new(216, 184, 48) },
        // Welt 10: Schattenwelt (Dunkelviolett)
        new() { Floor1 = new(48, 36, 60), Floor2 = new(40, 28, 52), FloorLine = new(32, 20, 44),
                WallMain = new(28, 20, 40), WallHighlight = new(52, 36, 68), WallShadow = new(16, 12, 24),
                BlockMain = new(68, 44, 84), BlockMortar = new(92, 60, 108), BlockHighlight = new(80, 52, 96), BlockShadow = new(48, 32, 60),
                Accent = new(152, 68, 216) },
    ];

    // Aktive Welt-Palette (wird bei Level-Wechsel gesetzt)
    private WorldPalette? _worldPalette;

    /// <summary>
    /// Welt-Theme setzen (0-9 für Welt 1-10) — laedt Welt-Palette und Welt-Bomb-FX-Theme.
    /// </summary>
    public void SetWorldTheme(int worldIndex)
    {
        var palettes = _styleService.CurrentStyle switch
        {
            GameVisualStyle.Neon => NeonWorldPalettes,
            GameVisualStyle.Retro => RetroWorldPalettes,
            _ => ClassicWorldPalettes
        };
        var bombFxThemes = _styleService.CurrentStyle switch
        {
            GameVisualStyle.Neon => NeonBombFx,
            GameVisualStyle.Retro => RetroBombFx,
            _ => ClassicBombFx
        };
        worldIndex = Math.Clamp(worldIndex, 0, palettes.Length - 1);
        _worldPalette = palettes[worldIndex];
        _bombFxTheme = bombFxThemes[Math.Clamp(worldIndex, 0, bombFxThemes.Length - 1)];
        _currentWorldIndex = worldIndex;

        // Boden-Cache invalidieren (wird beim nächsten RenderGrid neu aufgebaut)
        InvalidateFloorCache();

        // Atmosphärische Subsysteme für neue Welt initialisieren
        // Spielfeld-Dimensionen verwenden (nicht Screen), da beide Systeme
        // innerhalb des Spielfeld-Canvas-Transforms gerendert werden
        float fieldW = GameGrid.WIDTH * GameGrid.CELL_SIZE;
        float fieldH = GameGrid.HEIGHT * GameGrid.CELL_SIZE;
        _weatherSystem.SetWorld(worldIndex, fieldW, fieldH);
        _ambientParticles.SetWorld(worldIndex, fieldW, fieldH);
        _shaderEffects.SetWorld(worldIndex);

        // Basis-Palette Farben mit Welt-Theme überschreiben
        _palette.FloorBase = _worldPalette.Floor1;
        _palette.FloorAlt = _worldPalette.Floor2;
        _palette.FloorLine = _worldPalette.FloorLine;
        _palette.WallBase = _worldPalette.WallMain;
        _palette.WallHighlight = _worldPalette.WallHighlight;
        _palette.WallShadow = _worldPalette.WallShadow;
        _palette.BlockBase = _worldPalette.BlockMain;
        _palette.BlockMortar = _worldPalette.BlockMortar;
        _palette.BlockHighlight = _worldPalette.BlockHighlight;
        _palette.BlockShadow = _worldPalette.BlockShadow;

        // Welt-Bomb-FX-Theme uebernehmen — Bombe + Explosion bekommen welt-spezifischen
        // Look (.1 . Nur greift wenn Default-Skin aktiv ist
        // (Custom-Skins behalten ihre Farben).
        UpdateExplosionSkinColors();
    }

    /// <summary>
    /// Liefert das aktive Welt-Bomb-FX-Theme (.1 .
    /// Wird in RenderBomb verwendet um Default-Skin-Bomben Welt-spezifisch zu faerben.
    /// </summary>
    internal (SKColor body, SKColor glow, SKColor fuse, SKColor highlight, SKColor sparkGlow, SKColor sparkCore)?
        GetWorldBombFx() =>
        _bombFxTheme is { } t
            ? (t.BombBody, t.BombGlow, t.BombFuse, t.BombHighlight, t.SparkGlow, t.SparkCore)
            : null;

    /// <summary>
    /// Liefert die aktuelle Welt-Akzent-Farbe (.2 .
    /// Wird vom UltraComboFlash + Cinematic-Director als Welt-Tint verwendet.
    /// Default Gold wenn keine Welt aktiv ist.
    /// </summary>
    public SKColor GetWorldAccentColor() =>
        _worldPalette?.Accent ?? BomberBlastColors.Gold;

    // ═══════════════════════════════════════════════════════════════════════
    // POOLED PAINT OBJECTS
    // ═══════════════════════════════════════════════════════════════════════

    private readonly SKPaint _fillPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _strokePaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _glowPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    // Audit C11: Cached Paint fuer Player-Blink-SaveLayer (Spawn-Protection ~3-5s, 30-60x/s).
    private readonly SKPaint _blinkLayerPaint = new();
    private readonly SKFont _hudFontLarge = new(SKTypeface.FromFamilyName("monospace", SKFontStyle.Bold), 22);
    private readonly SKFont _hudFontMedium = new(SKTypeface.FromFamilyName("monospace", SKFontStyle.Bold), 16);
    private readonly SKFont _hudFontCombo = new(SKTypeface.FromFamilyName("monospace", SKFontStyle.Bold), 16);
    private readonly SKFont _hudFontSmall = new(SKTypeface.FromFamilyName("monospace"), 13);
    private readonly SKFont _powerUpFont = new() { Size = 14, Embolden = true };
    private readonly SKPath _fusePath = new();

    // Gecachte Glow-Filter
    private readonly SKMaskFilter _smallGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3);
    private readonly SKMaskFilter _mediumGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6);
    private readonly SKMaskFilter _outerGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 4);
    private readonly SKMaskFilter _hudTextGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 3);
    private readonly SKMaskFilter _bossAuraFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12);

    // HUD-Gradient-Cache (wird bei Style-Wechsel invalidiert)
    private SKShader? _hudGradientShader;
    private float _lastHudShaderHeight;

    // Gepoolte Liste fuer aktive PowerUps im HUD (vermeidet Allokation pro Frame)
    private readonly List<(string label, SKColor color)> _activePowers = new(6);

    // Gecachte HUD-Strings (werden nur bei Aenderung neu erstellt)
    private int _lastInvTimerValue = -1;
    private string _lastInvString = "";
    private int _lastTimeValue = -1;
    private string _lastTimeString = "";
    private int _lastScoreValue = -1;
    private string _lastScoreString = "";
    private int _lastBombsValue = -1;
    private string _lastBombsString = "";
    private int _lastFireValue = -1;
    private string _lastFireString = "";
    private int _lastSurvivalMins = -1;
    private int _lastSurvivalSecs = -1;
    private string _lastSurvivalTimeString = "";
    private int _lastSurvivalKills = -1;
    private string _lastSurvivalKillsString = "";
    private int _lastComboCount = -1;
    private string _lastComboString = "";
    private int _lastEnemiesRemaining = -1;
    private string _lastEnemiesString = "";
    private int _lastSpeedLevel = -1;
    private string _lastSpeedString = "";
    private int _lastCurseTimer = -1;
    private CurseType _lastCurseType = (CurseType)(-1);
    private string _lastCurseString = "";
    // Gecachte RemainingUses-Strings pro Kartenslot (vermeidet Int.ToString() pro Frame pro Karte)
    private readonly int[] _lastRemainingUses = { -1, -1, -1, -1, -1 };
    private readonly string[] _lastRemainingUsesStr = { "", "", "", "", "" };
    // Gecachter Blur-Filter für Combo-/Card-Glow im HUD (statt pro-Frame CreateBlur)
    private readonly SKMaskFilter _hudComboBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    public float Scale => _scale;
    public float OffsetX => _offsetX;
    public float OffsetY => _offsetY;

    public GameRenderer(IGameStyleService styleService, ICustomizationService customizationService)
    {
        _styleService = styleService;
        _customizationService = customizationService;
        _palette = GetPaletteForStyle(_styleService.CurrentStyle);
        UpdateExplosionSkinColors();

        // Kosmetik-Trail aus CustomizationService laden
        _trailSystem.ActiveCosmeticTrail = _customizationService.ActiveTrail;

        // Einmalig erstellte Outline-Render-Delegates (kein Per-Frame-Closure im Render-Loop).
        // Sie lesen die _outlineXxx-Felder, die unmittelbar vor dem Aufruf gesetzt werden.
        _renderOutlineEnemy = c => RenderEnemy(c, _outlineEnemy!, _outlineGrid!);
        _renderOutlinePowerUp = c => RenderPowerUp(c, _outlinePowerUp!);
        _renderOutlinePlayer = c => RenderPlayer(c, _outlinePlayer!);

        _styleService.StyleChanged += OnStyleChanged;
    }

    private static StylePalette GetPaletteForStyle(GameVisualStyle style) => style switch
    {
        GameVisualStyle.Neon => NeonPalette,
        GameVisualStyle.Retro => RetroPalette,
        _ => ClassicPalette
    };

    /// <summary>Explosionsfarben aus Skin oder Welt-BombFx-Theme aktualisieren (.</summary>
    private void UpdateExplosionSkinColors()
    {
        var eSkin = _customizationService.ExplosionSkin;
        if (eSkin.Id == "expl_rainbow")
        {
            // Regenbogen: Farben rotieren per globalTimer
            float hue = (_globalTimer * 60f) % 360f;
            _explOuter = SKColor.FromHsl(hue, 90, 55);
            _explInner = SKColor.FromHsl((hue + 120f) % 360f, 90, 70);
            _explCore = SKColor.FromHsl((hue + 240f) % 360f, 80, 85);
        }
        else if (eSkin.Id != "expl_default")
        {
            _explOuter = eSkin.OuterColor;
            _explInner = eSkin.InnerColor;
            _explCore = eSkin.CoreColor;
        }
        else if (_bombFxTheme is { } theme)
        {
            // Welt-spezifische Explosion-Farben (.1 — Wueste hat anderes
            // Look als Vulkan, Schattenwelt anderes als Forest. Default-Skin ueberlasst der Welt das Theme.
            _explOuter = theme.ExplosionOuter;
            _explInner = theme.ExplosionInner;
            _explCore = theme.ExplosionCore;
        }
        else
        {
            _explOuter = _palette.ExplosionOuter;
            _explInner = _palette.ExplosionInner;
            _explCore = _palette.ExplosionCore;
        }
    }

    private void OnStyleChanged(GameVisualStyle style)
    {
        _palette = GetPaletteForStyle(style);
        // Welt-Bomb-FX-Theme an neuen Style anpassen
        if (_bombFxTheme != null)
        {
            var bombFxThemes = style switch
            {
                GameVisualStyle.Neon => NeonBombFx,
                GameVisualStyle.Retro => RetroBombFx,
                _ => ClassicBombFx
            };
            _bombFxTheme = bombFxThemes[Math.Clamp(_currentWorldIndex, 0, bombFxThemes.Length - 1)];
        }
        UpdateExplosionSkinColors();
        // Gecachten HUD-Gradient invalidieren
        _hudGradientShader?.Dispose();
        _hudGradientShader = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VIEWPORT (HUD rechts, Spielfeld links)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rendering-Skalierung und Offset berechnen (Landscape: Spiel links, HUD rechts)
    /// </summary>
    public void CalculateViewport(float screenWidth, float screenHeight, int gridPixelWidth, int gridPixelHeight)
    {
        if (screenWidth <= 0 || screenHeight <= 0 || gridPixelWidth <= 0 || gridPixelHeight <= 0)
            return;

        _screenWidth = screenWidth;
        _screenHeight = screenHeight;

        // Effektive Höhe: abzüglich Banner-Ad oben
        float effectiveHeight = screenHeight - BannerTopOffset;

        // HUD-Platz auf der rechten Seite reservieren
        float hudReserved = HUD_LOGICAL_WIDTH;

        // Skalierung um Grid in verbleibende Flaeche einzupassen
        float availableWidth = screenWidth - hudReserved;
        float scaleX = availableWidth / gridPixelWidth;
        float scaleY = effectiveHeight / gridPixelHeight;
        _scale = Math.Min(scaleX, scaleY);

        // Spielfeld vertikal zentrieren (unterhalb des Banners)
        float scaledGridWidth = gridPixelWidth * _scale;
        float scaledGridHeight = gridPixelHeight * _scale;
        _offsetX = (availableWidth - scaledGridWidth) / 2f;
        _offsetY = BannerTopOffset + (effectiveHeight - scaledGridHeight) / 2f;

        // HUD panel position (right side, unterhalb des Banners)
        _hudX = availableWidth;
        _hudY = BannerTopOffset;
        _hudWidth = hudReserved;
        _hudHeight = effectiveHeight;
    }

    /// <summary>
    /// Animations-Timer aktualisieren.
    /// Adaptive Frame-Skipping: Bei anhaltenden Frame-Spikes (Avg > 40ms, &lt;25 FPS)
    /// werden atmosphärische Systeme ausgesetzt, damit Gameplay-Kritische Pfade
    /// (AI, Collision, Input) vollen Frame-Budget bekommen. Hysterese gegen Flackern.
    /// </summary>
    public void Update(float deltaTime)
    {
        _globalTimer += deltaTime;
        _lastDeltaTime = deltaTime;
        UpdateAdaptiveSkipping(deltaTime);

        if (!SkipAtmosphere)
        {
            _weatherSystem.Update(deltaTime, _globalTimer);
            _ambientParticles.Update(deltaTime, _globalTimer);
            _shaderEffects.Update(deltaTime);
        }
    }

    /// <summary>
    /// Aktualisiert den Ring-Buffer der Frame-Zeiten und entscheidet ob atmosphärische
    /// Systeme ausgesetzt werden. Hysterese: Enter-Threshold 40ms, Exit-Threshold 28ms,
    /// Minimum-Hold 500ms (verhindert schnelles On/Off-Flackern bei Grenzwerten).
    /// </summary>
    private void UpdateAdaptiveSkipping(float deltaTime)
    {
        // Ring-Buffer füllen
        _frameTimeBuffer[_frameTimeIndex] = deltaTime;
        _frameTimeIndex = (_frameTimeIndex + 1) % FrameTimeBufferSize;
        if (_frameTimeCount < FrameTimeBufferSize) _frameTimeCount++;

        // Minimum-Hold-Zeit respektieren (nach Skip-Activation mindestens 500ms im Skip-Modus)
        if (_skipHoldRemaining > 0)
        {
            _skipHoldRemaining -= deltaTime;
            return;
        }

        // Durchschnitt berechnen (nur wenn Buffer voll, sonst keine stabile Bewertung)
        if (_frameTimeCount < FrameTimeBufferSize) return;

        float sum = 0f;
        for (int i = 0; i < FrameTimeBufferSize; i++)
            sum += _frameTimeBuffer[i];
        float avg = sum / FrameTimeBufferSize;

        if (_adaptiveSkipActive)
        {
            // Skip aktiv → nur deaktivieren wenn Frame-Zeit stabil unter Exit-Threshold
            if (avg < SkipReleaseSeconds)
                _adaptiveSkipActive = false;
        }
        else
        {
            // Skip inaktiv → aktivieren wenn Frame-Zeit über Enter-Threshold
            if (avg > SkipThresholdSeconds)
            {
                _adaptiveSkipActive = true;
                _skipHoldRemaining = SkipHoldMinSeconds;
            }
        }
    }

    /// <summary>
    /// Nebel-Overlay aktivieren/deaktivieren (Welt 10: Schattenwelt - eingeschränkte Sicht)
    /// </summary>
    public void SetFogEnabled(bool enabled) => _fogEnabled = enabled;

    // ═══════════════════════════════════════════════════════════════════════
    // MAIN RENDER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Das gesamte Spiel rendern
    /// </summary>
    public void Render(SKCanvas canvas, GameGrid grid, Player player,
        List<Enemy> enemies, List<Bomb> bombs,
        List<Explosion> explosions, List<PowerUp> powerUps,
        float remainingTime, int score, int lives, Cell? exitCell = null,
        List<Cell>? specialEffectCells = null)
    {
        // Rainbow-Explosion: Farben nur alle 3 Frames aktualisieren (HSL-Berechnung sparen)
        if (_customizationService.ExplosionSkin.Id == "expl_rainbow")
        {
            _rainbowUpdateCounter++;
            if (_rainbowUpdateCounter % 3 == 0)
                UpdateExplosionSkinColors();
        }

        // Hintergrund mit Welt-Gradient (statt canvas.Clear)
        RenderBackground(canvas, _screenWidth, _screenHeight);

        // Saisonales Event-Tint subtil ueber Hintergrund (v2.0.42, Plan Task 3.4)
        RenderEventTint(canvas, _screenWidth, _screenHeight);

        // Hintergrund-Elemente (Bäume, Zahnräder, Stalaktiten etc. am Rand)
        if (!SkipAtmosphere)
            RenderBackgroundElements(canvas, _screenWidth, _screenHeight);

        // Ambient-Partikel (unter dem Grid, über dem Hintergrund)
        if (!SkipAtmosphere)
        {
            canvas.Save();
            canvas.Translate(_offsetX, _offsetY);
            canvas.Scale(_scale);
            _ambientParticles.Render(canvas, _globalTimer);
            canvas.Restore();
        }

        // Canvas-Zustand sichern und Transformation fuer Spielfeld anwenden
        canvas.Save();
        canvas.Translate(_offsetX, _offsetY);
        canvas.Scale(_scale);

        RenderGrid(canvas, grid);
        RenderSpecialBombCellEffects(canvas, grid);
        RenderAfterglow(canvas, grid);
        RenderDangerWarning(canvas, grid, bombs);
        RenderExit(canvas, grid, exitCell);

        // Trail-System: Fußabdrücke, Geister-Spuren, Feuer-Trails, Kosmetik-Trails (auf dem Boden, unter Entities)
        if (!SkipAtmosphere)
        {
            _trailSystem.ActiveCosmeticTrail = _customizationService.ActiveTrail;
            _trailSystem.Update(_lastDeltaTime, player, enemies, _globalTimer);
            _trailSystem.Render(canvas, _globalTimer);
        }

        foreach (var powerUp in powerUps)
        {
            if (powerUp.IsActive && powerUp.IsVisible)
            {
                _outlinePowerUp = powerUp;
                RenderEntityWithOptionalOutline(canvas, powerUp, _renderOutlinePowerUp);
            }
        }

        foreach (var bomb in bombs)
        {
            if (bomb.IsActive)
                RenderBomb(canvas, bomb);
        }

        foreach (var explosion in explosions)
        {
            if (explosion.IsActive)
                RenderExplosion(canvas, explosion);
        }

        //.1 : Burning-Modifier — Lava-Trail UNTER den Bossen rendern
        // (vor Enemies, damit der Trail von Boss-Sprite ueberlagert wird).
        RenderBurningTrails(canvas, enemies);

        foreach (var enemy in enemies)
        {
            _outlineEnemy = enemy;
            _outlineGrid = grid;
            RenderEntityWithOptionalOutline(canvas, enemy, _renderOutlineEnemy);
        }

        // Boss Angriffs-Warnung und HP-Balken (über den Gegnern, unter dem Spieler)
        foreach (var enemy in enemies)
        {
            if (enemy is BossEnemy bossEnemy && !enemy.IsDying)
            {
                if (bossEnemy.IsTelegraphing || bossEnemy.IsAttacking)
                    RenderBossAttackWarning(canvas, bossEnemy);
                RenderBossHPBar(canvas, bossEnemy);
            }
        }

        if (player != null)
        {
            _outlinePlayer = player;
            RenderEntityWithOptionalOutline(canvas, player, _renderOutlinePlayer);
        }

        // Dynamische Beleuchtung (Lichtquellen aus Bomben, Explosionen, Lava etc.)
        if (!SkipAtmosphere)
        {
            _dynamicLighting.Clear();
            CollectLightSources(canvas, grid, bombs, explosions, enemies, powerUps, player, exitCell, specialEffectCells);
            _dynamicLighting.Render(canvas);
        }

        // Wetter-Partikel (über dem Grid, unter HUD)
        if (!SkipAtmosphere)
            _weatherSystem.Render(canvas);

        // Saisonales Event-Overlay-Particles (v2.0.42, Plan Task 3.4)
        // Schneeflocken/Pumpkin-Funken/Feuerwerk/Bubbles ueber dem Grid, unter HUD.
        RenderEventOverlay(canvas, _screenWidth, _screenHeight, _lastDeltaTime);

        // Nebel-Overlay (Welt 10: Schattenwelt - einfacher Sichtkreis ohne Memory)
        if (_fogEnabled && player != null)
        {
            RenderFogOverlay(canvas, grid, player.X, player.Y);
        }

        // Fog-of-War-Overlay (v2.0.35: L50+ Normal / ab L1 Master — mit Explored-Memory)
        // Gerendert VOR canvas.Restore() damit Grid-Koordinaten aktiv sind.
        if (_fogOfWar.IsEnabled && player != null)
        {
            _fogOfWar.Render(canvas, player.X, player.Y, _fillPaint);
        }

        canvas.Restore();

        // Post-Processing: Color Grading, Water Ripples, Heat Shimmer, Damage Flash, Chromatic Aberration.
        // Damage-Flash und Chromatic Aberration sind Gameplay-Feedback, aber bei Stutter
        // ist der Frame-Gewinn wichtiger als das kurze Flash-Feedback (im SkipAtmosphere-Modus).
        if (!SkipAtmosphere)
        {
            if (player != null)
            {
                // Spieler-Hit erkennen (Invincibility-Flanke → Damage-Effekte)
                _shaderEffects.CheckPlayerHit(player.IsInvincible);
                // Spieler-Bildschirmposition für Water Ripples
                _shaderEffects.UpdatePlayerScreenPos(
                    player.X * _scale + _offsetX,
                    player.Y * _scale + _offsetY);
            }
            _shaderEffects.RenderPostProcessing(canvas, _screenWidth, _screenHeight, _globalTimer);

            // Vignette + Stimmungsbeleuchtung (über allem, unter HUD)
            RenderVignette(canvas, _screenWidth, _screenHeight);
            RenderMoodLighting(canvas, _screenWidth, _screenHeight);
        }

        // HUD zeichnen (nicht mit Spiel skaliert)
        RenderHUD(canvas, remainingTime, score, lives, player);
    }

    /// <summary>
    ///.1 : Rendert die Burning-Trail-Lava-Spuren aller Bosse mit
    /// Burning-Modifier. Trail-Eintraege haben TTL (3s) und fade-en linear aus —
    /// orange-roter Lava-Glow mit Anti-Spam-Pulse.
    /// </summary>
    private void RenderBurningTrails(SKCanvas canvas, IReadOnlyList<BomberBlast.Models.Entities.Enemy> enemies)
    {
        float cs = BomberBlast.Models.Grid.GameGrid.CELL_SIZE;
        const float trailMaxTtl = 3.0f;
        foreach (var enemy in enemies)
        {
            if (enemy is not BomberBlast.Models.Entities.BossEnemy boss || boss.BurningTrail.Count == 0)
                continue;
            foreach (var (tx, ty, ttl) in boss.BurningTrail)
            {
                float alpha = Math.Clamp(ttl / trailMaxTtl, 0f, 1f);
                byte a = (byte)(alpha * 200);  // bis ~0.8 Alpha am Anfang, fade zu 0
                // Lava-Glow: orange-rot mit warmer Mitte.
                float px = tx * cs;
                float py = ty * cs;
                // Gepooltes _fillPaint statt new SKPaint pro Trail-Eintrag/Frame (Allokation+Dispose gespart).
                _fillPaint.IsAntialias = true;
                _fillPaint.Style = SKPaintStyle.Fill;
                _fillPaint.MaskFilter = null;
                // Aussere Halo
                _fillPaint.Color = new SKColor(255, 80, 30, (byte)(a * 0.55f));
                canvas.DrawRect(px, py, cs, cs, _fillPaint);
                // Innere heisse Mitte (kleineres Rect, gelblicher)
                _fillPaint.Color = new SKColor(255, 180, 60, a);
                float inset = cs * 0.25f;
                canvas.DrawRect(px + inset, py + inset, cs - 2 * inset, cs - 2 * inset, _fillPaint);
            }
        }
    }

    /// <summary>
    ///.4 : Rendert eine Entity mit optionalem Outline-Pass.
    /// Wenn <see cref="BomberBlast.Models.Entities.Entity.RenderOutline"/> gesetzt ist
    /// (und keine Performance-Drosselung aktiv), wird der Sprite ueber den
    /// <see cref="OutlineRenderHelper"/> mit dunklem Outline-Ring gezeichnet —
    /// vereinheitlicht Vektor-Sprites und AI-WebP-Bitmaps optisch.
    /// Bei <see cref="SkipAtmosphere"/> faellt es auf den normalen Single-Pass zurueck
    /// (Outline kostet 2x DrawCalls — bei Stutter ist der Frame-Gewinn wichtiger).
    /// </summary>
    // Outline-Render ohne Per-Frame-Closure: Statt pro Entity pro Frame ein neues Lambda
    // (Display-Class + Delegate) zu allokieren, werden diese "current"-Felder vor dem Aufruf
    // gesetzt und von den einmalig im Ctor erstellten Delegates (_renderOutlineXxx) gelesen.
    // Render läuft synchron auf dem UI-Thread (DispatcherTimer) → Setzen+Lesen ist sicher.
    private Enemy? _outlineEnemy;
    private GameGrid? _outlineGrid;
    private PowerUp? _outlinePowerUp;
    private Player? _outlinePlayer;
    private readonly Action<SKCanvas> _renderOutlineEnemy;
    private readonly Action<SKCanvas> _renderOutlinePowerUp;
    private readonly Action<SKCanvas> _renderOutlinePlayer;

    private void RenderEntityWithOptionalOutline(
        SKCanvas canvas,
        BomberBlast.Models.Entities.Entity entity,
        Action<SKCanvas> render)
    {
        if (entity.RenderOutline && !SkipAtmosphere)
            OutlineRenderHelper.RenderWithOutline(canvas, render);
        else
            render(canvas);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COORDINATE CONVERSION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bildschirmkoordinaten in Grid-Koordinaten umrechnen
    /// </summary>
    public (int gridX, int gridY) ScreenToGrid(float screenX, float screenY)
    {
        float gameX = (screenX - _offsetX) / _scale;
        float gameY = (screenY - _offsetY) / _scale;

        int gridX = (int)MathF.Floor(gameX / GameGrid.CELL_SIZE);
        int gridY = (int)MathF.Floor(gameY / GameGrid.CELL_SIZE);

        return (
            Math.Clamp(gridX, 0, GameGrid.WIDTH - 1),
            Math.Clamp(gridY, 0, GameGrid.HEIGHT - 1)
        );
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _styleService.StyleChanged -= OnStyleChanged;

        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _glowPaint.Dispose();
        _textPaint.Dispose();
        _hudFontLarge.Dispose();
        _hudFontMedium.Dispose();
        _hudFontCombo.Dispose();
        _hudFontSmall.Dispose();
        _enrageColorFilter?.Dispose();
        _bossBitmapPaint.Dispose();
        _powerUpFont.Dispose();
        _fusePath.Dispose();
        _smallGlow.Dispose();
        _mediumGlow.Dispose();
        _outerGlow.Dispose();
        _hudTextGlow.Dispose();
        _bossAuraFilter.Dispose();
        _hudGradientShader?.Dispose();
        _dynamicLighting.Dispose();
        _weatherSystem.Dispose();
        _ambientParticles.Dispose();
        _shaderEffects.Dispose();
        _trailSystem.Dispose();
        _fogOfWar.Dispose();
        _eventPaint.Dispose();
        _eventShapePath.Dispose();
        _floorCacheBitmap?.Dispose();
        _bgShader?.Dispose();
        _vignetteShader?.Dispose();
        _hudComboBlur.Dispose();
        _bgPath.Dispose();
        _charPath1.Dispose();
        _charPath2.Dispose();
        _tilePath.Dispose();
        _blinkLayerPaint.Dispose();
    }
}
