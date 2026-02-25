using BomberBlast.Models;
using BomberBlast.Models.Cosmetics;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Renders the game using SkiaSharp with two visual styles (Classic HD / Neon)
/// </summary>
public partial class GameRenderer : IDisposable
{
    private bool _disposed;
    private readonly IGameStyleService _styleService;
    private readonly ICustomizationService _customizationService;

    // Rendering settings
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

    // HUD constants
    private const float HUD_LOGICAL_WIDTH = 120f;

    // Combo-Daten (gesetzt von GameEngine vor jedem Render)
    public int ComboCount { get; set; }
    public float ComboTimer { get; set; }
    public bool IsSurvivalMode { get; set; }
    public int SurvivalKills { get; set; }
    public int EnemiesRemaining { get; set; }

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

    // ReducedEffects: Atmosphärische Systeme deaktivieren (Performance-Modus)
    public bool ReducedEffects { get; set; }

    // Animation timing
    private float _globalTimer;
    private float _lastDeltaTime;

    // Nebel-Overlay (Welt 10: Schattenwelt)
    private bool _fogEnabled;

    // Current palette (swapped on style change)
    private StylePalette _palette;

    // Effektive Explosionsfarben (Skin-Override oder Palette)
    private SKColor _explOuter, _explInner, _explCore;

    // ═══════════════════════════════════════════════════════════════════════
    // COLOR PALETTES
    // ═══════════════════════════════════════════════════════════════════════

    private sealed class StylePalette
    {
        // Background
        public SKColor Background;

        // Floor
        public SKColor FloorBase;
        public SKColor FloorAlt;
        public SKColor FloorLine;

        // Wall
        public SKColor WallBase;
        public SKColor WallHighlight;
        public SKColor WallShadow;
        public SKColor WallEdge;

        // Block
        public SKColor BlockBase;
        public SKColor BlockMortar;
        public SKColor BlockHighlight;
        public SKColor BlockShadow;

        // Exit
        public SKColor ExitGlow;
        public SKColor ExitInner;

        // Bomb
        public SKColor BombBody;
        public SKColor BombGlowColor;
        public SKColor BombFuse;
        public SKColor BombHighlight;

        // Explosion
        public SKColor ExplosionOuter;
        public SKColor ExplosionInner;
        public SKColor ExplosionCore;

        // Player
        public SKColor PlayerBody;
        public SKColor PlayerHelm;
        public SKColor PlayerAura;

        // Enemy
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
    /// Welt-Theme setzen (0-4 für Welt 1-5)
    /// </summary>
    public void SetWorldTheme(int worldIndex)
    {
        var palettes = _styleService.CurrentStyle switch
        {
            GameVisualStyle.Neon => NeonWorldPalettes,
            GameVisualStyle.Retro => RetroWorldPalettes,
            _ => ClassicWorldPalettes
        };
        worldIndex = Math.Clamp(worldIndex, 0, palettes.Length - 1);
        _worldPalette = palettes[worldIndex];
        _currentWorldIndex = worldIndex;

        // Boden-Cache invalidieren (wird beim nächsten RenderGrid neu aufgebaut)
        InvalidateFloorCache();

        // Atmosphärische Subsysteme für neue Welt initialisieren
        _weatherSystem.SetWorld(worldIndex, _screenWidth, _screenHeight);
        _ambientParticles.SetWorld(worldIndex, _screenWidth, _screenHeight);
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
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POOLED PAINT OBJECTS
    // ═══════════════════════════════════════════════════════════════════════

    private readonly SKPaint _fillPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _strokePaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _glowPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKFont _hudFontLarge = new(SKTypeface.FromFamilyName("monospace", SKFontStyle.Bold), 22);
    private readonly SKFont _hudFontMedium = new(SKTypeface.FromFamilyName("monospace", SKFontStyle.Bold), 16);
    private readonly SKFont _hudFontSmall = new(SKTypeface.FromFamilyName("monospace"), 13);
    private readonly SKFont _powerUpFont = new() { Size = 14, Embolden = true };
    private readonly SKPath _fusePath = new();

    // Cached glow filters
    private readonly SKMaskFilter _smallGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3);
    private readonly SKMaskFilter _mediumGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6);
    private readonly SKMaskFilter _outerGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 4);
    private readonly SKMaskFilter _hudTextGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 3);

    // HUD gradient cache
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

        _styleService.StyleChanged += OnStyleChanged;
    }

    private static StylePalette GetPaletteForStyle(GameVisualStyle style) => style switch
    {
        GameVisualStyle.Neon => NeonPalette,
        GameVisualStyle.Retro => RetroPalette,
        _ => ClassicPalette
    };

    /// <summary>Explosionsfarben aus Skin oder Palette aktualisieren</summary>
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
        UpdateExplosionSkinColors();
        // Invalidate cached HUD gradient
        _hudGradientShader?.Dispose();
        _hudGradientShader = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VIEWPORT (HUD rechts, Spielfeld links)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate rendering scale and offset (Landscape: game left, HUD right)
    /// </summary>
    public void CalculateViewport(float screenWidth, float screenHeight, int gridPixelWidth, int gridPixelHeight)
    {
        if (screenWidth <= 0 || screenHeight <= 0 || gridPixelWidth <= 0 || gridPixelHeight <= 0)
            return;

        _screenWidth = screenWidth;
        _screenHeight = screenHeight;

        // Effektive Höhe: abzüglich Banner-Ad oben
        float effectiveHeight = screenHeight - BannerTopOffset;

        // Reserve HUD space on the right side
        float hudReserved = HUD_LOGICAL_WIDTH;

        // Scale to fit grid in remaining area
        float availableWidth = screenWidth - hudReserved;
        float scaleX = availableWidth / gridPixelWidth;
        float scaleY = effectiveHeight / gridPixelHeight;
        _scale = Math.Min(scaleX, scaleY);

        // Center the game field vertically (unterhalb des Banners)
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
    /// Update animation timer
    /// </summary>
    public void Update(float deltaTime)
    {
        _globalTimer += deltaTime;
        _lastDeltaTime = deltaTime;

        if (!ReducedEffects)
        {
            _weatherSystem.Update(deltaTime, _globalTimer);
            _ambientParticles.Update(deltaTime, _globalTimer);
            _shaderEffects.Update(deltaTime);
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
    /// Render the entire game
    /// </summary>
    public void Render(SKCanvas canvas, GameGrid grid, Player player,
        IEnumerable<Enemy> enemies, IEnumerable<Bomb> bombs,
        IEnumerable<Explosion> explosions, IEnumerable<PowerUp> powerUps,
        float remainingTime, int score, int lives, Cell? exitCell = null)
    {
        // Rainbow-Explosion muss pro Frame aktualisiert werden
        if (_customizationService.ExplosionSkin.Id == "expl_rainbow")
            UpdateExplosionSkinColors();

        // Hintergrund mit Welt-Gradient (statt canvas.Clear)
        RenderBackground(canvas, _screenWidth, _screenHeight);

        // Hintergrund-Elemente (Bäume, Zahnräder, Stalaktiten etc. am Rand)
        if (!ReducedEffects)
            RenderBackgroundElements(canvas, _screenWidth, _screenHeight);

        // Ambient-Partikel (unter dem Grid, über dem Hintergrund)
        if (!ReducedEffects)
        {
            canvas.Save();
            canvas.Translate(_offsetX, _offsetY);
            canvas.Scale(_scale);
            _ambientParticles.Render(canvas, _globalTimer);
            canvas.Restore();
        }

        // Save canvas state and apply transform for game field
        canvas.Save();
        canvas.Translate(_offsetX, _offsetY);
        canvas.Scale(_scale);

        RenderGrid(canvas, grid);
        RenderSpecialBombCellEffects(canvas, grid);
        RenderAfterglow(canvas, grid);
        RenderDangerWarning(canvas, grid, bombs);
        RenderExit(canvas, grid, exitCell);

        // Trail-System: Fußabdrücke, Geister-Spuren, Feuer-Trails, Kosmetik-Trails (auf dem Boden, unter Entities)
        if (!ReducedEffects)
        {
            _trailSystem.ActiveCosmeticTrail = _customizationService.ActiveTrail;
            _trailSystem.Update(_lastDeltaTime, player, enemies, _globalTimer);
            _trailSystem.Render(canvas, _globalTimer);
        }

        foreach (var powerUp in powerUps)
        {
            if (powerUp.IsActive && powerUp.IsVisible)
                RenderPowerUp(canvas, powerUp);
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

        foreach (var enemy in enemies)
            RenderEnemy(canvas, enemy, grid);

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
            RenderPlayer(canvas, player);

        // Dynamische Beleuchtung (Lichtquellen aus Bomben, Explosionen, Lava etc.)
        if (!ReducedEffects)
        {
            _dynamicLighting.Clear();
            CollectLightSources(canvas, grid, bombs, explosions, enemies, powerUps, player, exitCell);
            _dynamicLighting.Render(canvas);
        }

        // Wetter-Partikel (über dem Grid, unter HUD)
        if (!ReducedEffects)
            _weatherSystem.Render(canvas);

        // Nebel-Overlay (Welt 10: Schattenwelt - eingeschränkte Sicht)
        if (_fogEnabled && player != null)
        {
            RenderFogOverlay(canvas, grid, player.X, player.Y);
        }

        canvas.Restore();

        // Post-Processing: Color Grading, Water Ripples, Heat Shimmer, Damage Flash, Chromatic Aberration
        if (!ReducedEffects)
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

        // Draw HUD (not scaled with game)
        RenderHUD(canvas, remainingTime, score, lives, player);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COORDINATE CONVERSION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Convert screen coordinates to grid coordinates
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
        _hudFontSmall.Dispose();
        _powerUpFont.Dispose();
        _fusePath.Dispose();
        _smallGlow.Dispose();
        _mediumGlow.Dispose();
        _outerGlow.Dispose();
        _hudTextGlow.Dispose();
        _hudGradientShader?.Dispose();
        _dynamicLighting.Dispose();
        _weatherSystem.Dispose();
        _ambientParticles.Dispose();
        _shaderEffects.Dispose();
        _trailSystem.Dispose();
        _floorCacheBitmap?.Dispose();
    }
}
