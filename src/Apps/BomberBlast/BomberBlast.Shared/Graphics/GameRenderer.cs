using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Renders the game using SkiaSharp with two visual styles (Classic HD / Neon)
/// </summary>
public class GameRenderer : IDisposable
{
    private bool _disposed;
    private readonly SpriteSheet _spriteSheet;
    private readonly IGameStyleService _styleService;
    private readonly ICustomizationService _customizationService;

    // Rendering settings
    private float _scale = 1f;
    private float _offsetX, _offsetY;
    private float _hudX, _hudY, _hudWidth, _hudHeight;

    // HUD constants
    private const float HUD_LOGICAL_WIDTH = 120f;

    /// <summary>
    /// Verschiebung nach unten fuer Banner-Ad oben (in Canvas-Einheiten).
    /// Wenn > 0, werden Grid und HUD nach unten verschoben.
    /// </summary>
    public float BannerTopOffset { get; set; }

    // Animation timing
    private float _globalTimer;

    // Current palette (swapped on style change)
    private StylePalette _palette;

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
    ];

    // Aktive Welt-Palette (wird bei Level-Wechsel gesetzt)
    private WorldPalette? _worldPalette;

    /// <summary>
    /// Welt-Theme setzen (0-4 für Welt 1-5)
    /// </summary>
    public void SetWorldTheme(int worldIndex)
    {
        worldIndex = Math.Clamp(worldIndex, 0, 4);
        var palettes = _styleService.CurrentStyle == GameVisualStyle.Neon ? NeonWorldPalettes : ClassicWorldPalettes;
        _worldPalette = palettes[worldIndex];

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

    public GameRenderer(SpriteSheet spriteSheet, IGameStyleService styleService, ICustomizationService customizationService)
    {
        _spriteSheet = spriteSheet;
        _styleService = styleService;
        _customizationService = customizationService;
        _palette = _styleService.CurrentStyle == GameVisualStyle.Neon ? NeonPalette : ClassicPalette;

        _styleService.StyleChanged += OnStyleChanged;
    }

    private void OnStyleChanged(GameVisualStyle style)
    {
        _palette = style == GameVisualStyle.Neon ? NeonPalette : ClassicPalette;
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
    }

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
        canvas.Clear(_palette.Background);

        // Save canvas state and apply transform for game field
        canvas.Save();
        canvas.Translate(_offsetX, _offsetY);
        canvas.Scale(_scale);

        RenderGrid(canvas, grid);
        RenderAfterglow(canvas, grid);
        RenderDangerWarning(canvas, grid, bombs);
        RenderExit(canvas, grid, exitCell);

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
            RenderEnemy(canvas, enemy);

        if (player != null)
            RenderPlayer(canvas, player);

        canvas.Restore();

        // Draw HUD (not scaled with game)
        RenderHUD(canvas, remainingTime, score, lives, player);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GRID RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderGrid(SKCanvas canvas, GameGrid grid)
    {
        int cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = grid[x, y];
                float px = x * cs;
                float py = y * cs;

                // Floor tile
                RenderFloorTile(canvas, px, py, cs, x, y, isNeon);

                switch (cell.Type)
                {
                    case CellType.Wall:
                        RenderWallTile(canvas, px, py, cs, isNeon);
                        break;

                    case CellType.Block:
                        if (cell.IsDestroying)
                            RenderBlockDestruction(canvas, px, py, cs, cell.DestructionProgress, isNeon);
                        else
                            RenderBlockTile(canvas, px, py, cs, x, isNeon);
                        break;

                    case CellType.Ice:
                        RenderIceTile(canvas, px, py, cs, x, y, isNeon);
                        break;

                    case CellType.Conveyor:
                        RenderConveyorTile(canvas, px, py, cs, cell, isNeon);
                        break;

                    case CellType.Teleporter:
                        RenderTeleporterTile(canvas, px, py, cs, cell, isNeon);
                        break;

                    case CellType.LavaCrack:
                        RenderLavaCrackTile(canvas, px, py, cs, cell, isNeon);
                        break;
                }
            }
        }
    }

    /// <summary>Nachglühen auf Zellen nach Explosionsende (warmer Schimmer)</summary>
    private void RenderAfterglow(SKCanvas canvas, GameGrid grid)
    {
        int cs = GameGrid.CELL_SIZE;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = grid[x, y];
                if (cell.AfterglowTimer <= 0)
                    continue;

                float intensity = cell.AfterglowTimer / Models.Entities.Explosion.AFTERGLOW_DURATION;
                byte alpha = (byte)(60 * intensity);
                _fillPaint.Color = _palette.ExplosionOuter.WithAlpha(alpha);
                _fillPaint.MaskFilter = null;
                canvas.DrawRect(x * cs, y * cs, cs, cs, _fillPaint);
            }
        }
    }

    /// <summary>
    /// Gefahrenzone: Subtiler roter Boden-Schimmer bei Bomben kurz vor Explosion
    /// </summary>
    private void RenderDangerWarning(SKCanvas canvas, GameGrid grid, IEnumerable<Bomb> bombs)
    {
        int cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        foreach (var bomb in bombs)
        {
            if (!bomb.IsActive || bomb.HasExploded) continue;
            // Nur warnen wenn Zünder < 0.8s (und nicht bei manueller Detonation ohne ablaufenden Timer)
            if (bomb.IsManualDetonation || bomb.FuseTimer > 0.8f) continue;

            // Intensität steigt je näher an Explosion (0 bei 0.8s → 1 bei 0s)
            float intensity = 1f - (bomb.FuseTimer / 0.8f);
            // Pulsieren (schneller bei weniger Zeit)
            float pulse = MathF.Sin(_globalTimer * (10f + intensity * 15f)) * 0.3f + 0.7f;
            byte alpha = (byte)(50 * intensity * pulse);
            if (alpha < 5) continue;

            var warningColor = isNeon ? new SKColor(255, 40, 80, alpha) : new SKColor(255, 60, 30, alpha);

            int centerX = bomb.GridX;
            int centerY = bomb.GridY;
            int range = bomb.Range;

            // Zentrum markieren
            _fillPaint.Color = warningColor;
            _fillPaint.MaskFilter = null;
            canvas.DrawRect(centerX * cs, centerY * cs, cs, cs, _fillPaint);

            // 4 Richtungen (wie Explosion.CalculateSpread, aber read-only)
            RenderDangerLine(canvas, grid, centerX, centerY, range, -1, 0, cs, warningColor);
            RenderDangerLine(canvas, grid, centerX, centerY, range, 1, 0, cs, warningColor);
            RenderDangerLine(canvas, grid, centerX, centerY, range, 0, -1, cs, warningColor);
            RenderDangerLine(canvas, grid, centerX, centerY, range, 0, 1, cs, warningColor);
        }
    }

    private void RenderDangerLine(SKCanvas canvas, GameGrid grid, int startX, int startY,
        int range, int dx, int dy, int cs, SKColor color)
    {
        for (int i = 1; i <= range; i++)
        {
            int x = startX + dx * i;
            int y = startY + dy * i;

            var cell = grid.TryGetCell(x, y);
            if (cell == null || cell.Type == CellType.Wall) break;

            _fillPaint.Color = color;
            _fillPaint.MaskFilter = null;
            canvas.DrawRect(x * cs, y * cs, cs, cs, _fillPaint);

            // Blöcke stoppen die Warnung (wie echte Explosionen)
            if (cell.Type == CellType.Block) break;
        }
    }

    private void RenderFloorTile(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        // Checkerboard pattern (two brightness levels)
        bool alt = (gx + gy) % 2 == 0;
        _fillPaint.Color = alt ? _palette.FloorBase : _palette.FloorAlt;
        _fillPaint.MaskFilter = null;
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        // Grid lines
        _strokePaint.Color = _palette.FloorLine;
        _strokePaint.StrokeWidth = isNeon ? 0.5f : 1f;
        _strokePaint.MaskFilter = null;
        canvas.DrawLine(px, py, px + cs, py, _strokePaint);
        canvas.DrawLine(px, py, px, py + cs, _strokePaint);
    }

    private void RenderWallTile(SKCanvas canvas, float px, float py, int cs, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        if (isNeon)
        {
            // Dark steel block
            _fillPaint.Color = _palette.WallBase;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Neon edge glow (cyan border lines)
            _strokePaint.Color = _palette.WallEdge;
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = _smallGlow;
            canvas.DrawRect(px + 1, py + 1, cs - 2, cs - 2, _strokePaint);
            _strokePaint.MaskFilter = null;
        }
        else
        {
            // 3D stone block
            _fillPaint.Color = _palette.WallBase;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Highlight (top + left edge)
            _fillPaint.Color = _palette.WallHighlight;
            canvas.DrawRect(px, py, cs, 3, _fillPaint);
            canvas.DrawRect(px, py, 3, cs, _fillPaint);

            // Shadow (bottom + right edge)
            _fillPaint.Color = _palette.WallShadow;
            canvas.DrawRect(px, py + cs - 3, cs, 3, _fillPaint);
            canvas.DrawRect(px + cs - 3, py, 3, cs, _fillPaint);
        }
    }

    private void RenderBlockTile(SKCanvas canvas, float px, float py, int cs, int gx, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        if (isNeon)
        {
            // Dunkler Block mit sichtbarem Rand
            _fillPaint.Color = _palette.BlockBase;
            canvas.DrawRect(px + 1, py + 1, cs - 2, cs - 2, _fillPaint);

            // Heller Rand oben/links fuer 3D-Effekt
            _fillPaint.Color = _palette.BlockHighlight;
            canvas.DrawRect(px + 1, py + 1, cs - 2, 2, _fillPaint);
            canvas.DrawRect(px + 1, py + 1, 2, cs - 2, _fillPaint);

            // Dunkler Rand unten/rechts
            _fillPaint.Color = _palette.BlockShadow;
            canvas.DrawRect(px + 1, py + cs - 3, cs - 2, 2, _fillPaint);
            canvas.DrawRect(px + cs - 3, py + 1, 2, cs - 2, _fillPaint);

            // Orange Glow-Riss-Muster
            _strokePaint.Color = _palette.BlockMortar;
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = _smallGlow;

            // Horizontaler Riss
            canvas.DrawLine(px + 4, py + cs / 2f, px + cs - 4, py + cs / 2f, _strokePaint);
            // Vertikaler Riss (versetzt pro Spalte)
            float vx = (gx % 2 == 0) ? px + cs / 2f : px + cs / 3f;
            canvas.DrawLine(vx, py + 4, vx, py + cs / 2f, _strokePaint);

            // Zusaetzlicher diagonaler Riss fuer mehr Detail
            float vx2 = (gx % 2 == 0) ? px + cs * 0.65f : px + cs * 0.6f;
            canvas.DrawLine(vx2, py + cs / 2f + 2, vx2 - cs * 0.15f, py + cs - 4, _strokePaint);
            _strokePaint.MaskFilter = null;
        }
        else
        {
            // 3D brick with mortar lines
            _fillPaint.Color = _palette.BlockBase;
            canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _fillPaint);

            // Highlight (top-left)
            _fillPaint.Color = _palette.BlockHighlight;
            canvas.DrawRect(px + 2, py + 2, cs - 4, 2, _fillPaint);
            canvas.DrawRect(px + 2, py + 2, 2, cs - 4, _fillPaint);

            // Shadow (bottom-right)
            _fillPaint.Color = _palette.BlockShadow;
            canvas.DrawRect(px + 2, py + cs - 4, cs - 4, 2, _fillPaint);
            canvas.DrawRect(px + cs - 4, py + 2, 2, cs - 4, _fillPaint);

            // Mortar cross-lines
            _strokePaint.Color = _palette.BlockMortar;
            _strokePaint.StrokeWidth = 1f;
            _strokePaint.MaskFilter = null;
            canvas.DrawLine(px + 2, py + cs / 2f, px + cs - 2, py + cs / 2f, _strokePaint);
            float vx = (gx % 2 == 0) ? px + cs / 2f : px + cs / 3f;
            canvas.DrawLine(vx, py + 2, vx, py + cs / 2f, _strokePaint);
        }
    }

    private void RenderBlockDestruction(SKCanvas canvas, float px, float py, int cs, float progress, bool isNeon)
    {
        byte alpha = (byte)(255 * (1 - progress));
        float shrink = progress * cs * 0.3f;
        _fillPaint.MaskFilter = null;
        _fillPaint.Color = _palette.BlockBase.WithAlpha(alpha);
        canvas.DrawRect(px + shrink, py + shrink, cs - shrink * 2, cs - shrink * 2, _fillPaint);

        if (isNeon)
        {
            // Neon glow burst on destruction
            _glowPaint.Color = _palette.BlockMortar.WithAlpha((byte)(alpha * 0.5f));
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawRect(px + shrink, py + shrink, cs - shrink * 2, cs - shrink * 2, _glowPaint);
            _glowPaint.MaskFilter = null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WELT-MECHANIK-TILES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Eis-Boden: Hellblauer reflektiver Glanz mit Schimmer-Animation</summary>
    private void RenderIceTile(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Basis-Eis-Farbe (Schachbrett-Variation)
        bool alt = (gx + gy) % 2 == 0;
        if (isNeon)
        {
            _fillPaint.Color = alt ? new SKColor(40, 60, 80) : new SKColor(35, 55, 75);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Neon-Glow-Linien (Riss-Muster)
            _strokePaint.Color = new SKColor(100, 200, 255, 80);
            _strokePaint.StrokeWidth = 0.8f;
            _strokePaint.MaskFilter = _smallGlow;
            canvas.DrawLine(px + 3, py + cs * 0.3f, px + cs - 5, py + cs * 0.6f, _strokePaint);
            canvas.DrawLine(px + cs * 0.4f, py + 2, px + cs * 0.7f, py + cs - 3, _strokePaint);
            _strokePaint.MaskFilter = null;
        }
        else
        {
            _fillPaint.Color = alt ? new SKColor(180, 210, 235) : new SKColor(170, 200, 225);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Riss-Linien
            _strokePaint.Color = new SKColor(200, 230, 250, 120);
            _strokePaint.StrokeWidth = 0.8f;
            _strokePaint.MaskFilter = null;
            canvas.DrawLine(px + 3, py + cs * 0.3f, px + cs - 5, py + cs * 0.6f, _strokePaint);
            canvas.DrawLine(px + cs * 0.4f, py + 2, px + cs * 0.7f, py + cs - 3, _strokePaint);
        }

        // Wandernder Glanz-Highlight (Lichtreflexion)
        float shimmerX = (MathF.Sin(_globalTimer * 1.5f + gx * 0.5f) * 0.5f + 0.5f) * cs;
        float shimmerY = (MathF.Cos(_globalTimer * 1.2f + gy * 0.7f) * 0.5f + 0.5f) * cs;
        byte shimmerAlpha = isNeon ? (byte)60 : (byte)90;
        _fillPaint.Color = new SKColor(255, 255, 255, shimmerAlpha);
        canvas.DrawCircle(px + shimmerX, py + shimmerY, cs * 0.15f, _fillPaint);

        // Grid-Linie
        _strokePaint.Color = isNeon ? new SKColor(80, 160, 220, 40) : new SKColor(150, 190, 215);
        _strokePaint.StrokeWidth = 0.5f;
        _strokePaint.MaskFilter = null;
        canvas.DrawLine(px, py, px + cs, py, _strokePaint);
        canvas.DrawLine(px, py, px, py + cs, _strokePaint);
    }

    /// <summary>Förderband: Animierte Pfeile in Förderrichtung</summary>
    private void RenderConveyorTile(SKCanvas canvas, float px, float py, int cs, Cell cell, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Basis (metallisch-grauer Boden)
        if (isNeon)
        {
            _fillPaint.Color = new SKColor(45, 45, 55);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);
        }
        else
        {
            _fillPaint.Color = new SKColor(160, 165, 175);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);
        }

        // Seitenleisten (metallische Ränder)
        bool horizontal = cell.ConveyorDirection is Models.Entities.Direction.Left or Models.Entities.Direction.Right;
        _fillPaint.Color = isNeon ? new SKColor(60, 60, 75) : new SKColor(130, 135, 145);
        if (horizontal)
        {
            canvas.DrawRect(px, py, cs, 3, _fillPaint);
            canvas.DrawRect(px, py + cs - 3, cs, 3, _fillPaint);
        }
        else
        {
            canvas.DrawRect(px, py, 3, cs, _fillPaint);
            canvas.DrawRect(px + cs - 3, py, 3, cs, _fillPaint);
        }

        // Animierte Pfeil-Chevrons (3 Stück, wandern in Förderrichtung)
        float animOffset = (_globalTimer * 40f) % cs; // Pixel-Offset Animation

        var arrowColor = isNeon ? new SKColor(255, 200, 0, 180) : new SKColor(220, 180, 40, 200);
        _strokePaint.Color = arrowColor;
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;

        float cx = px + cs / 2f;
        float cy = py + cs / 2f;

        for (int i = 0; i < 3; i++)
        {
            float offset = (i * cs / 3f + animOffset) % cs - cs / 2f;
            float chevronSize = cs * 0.2f;

            switch (cell.ConveyorDirection)
            {
                case Models.Entities.Direction.Right:
                    canvas.DrawLine(cx + offset - chevronSize, cy - chevronSize, cx + offset, cy, _strokePaint);
                    canvas.DrawLine(cx + offset, cy, cx + offset - chevronSize, cy + chevronSize, _strokePaint);
                    break;
                case Models.Entities.Direction.Left:
                    canvas.DrawLine(cx - offset + chevronSize, cy - chevronSize, cx - offset, cy, _strokePaint);
                    canvas.DrawLine(cx - offset, cy, cx - offset + chevronSize, cy + chevronSize, _strokePaint);
                    break;
                case Models.Entities.Direction.Down:
                    canvas.DrawLine(cx - chevronSize, cy + offset - chevronSize, cx, cy + offset, _strokePaint);
                    canvas.DrawLine(cx, cy + offset, cx + chevronSize, cy + offset - chevronSize, _strokePaint);
                    break;
                case Models.Entities.Direction.Up:
                    canvas.DrawLine(cx - chevronSize, cy - offset + chevronSize, cx, cy - offset, _strokePaint);
                    canvas.DrawLine(cx, cy - offset, cx + chevronSize, cy - offset + chevronSize, _strokePaint);
                    break;
            }
        }
        _strokePaint.MaskFilter = null;
    }

    /// <summary>Teleporter: Leuchtender pulsierender Ring mit Farb-ID</summary>
    private void RenderTeleporterTile(SKCanvas canvas, float px, float py, int cs, Cell cell, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Boden (Basis)
        bool alt = (cell.X + cell.Y) % 2 == 0;
        _fillPaint.Color = alt ? _palette.FloorBase : _palette.FloorAlt;
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        // Portal-Farbe basierend auf ColorId
        SKColor portalColor = cell.TeleporterColorId switch
        {
            0 => new SKColor(50, 150, 255),  // Blau
            1 => new SKColor(50, 255, 120),  // Grün
            2 => new SKColor(255, 150, 50),  // Orange
            _ => new SKColor(200, 100, 255)  // Lila
        };

        float cx = px + cs / 2f;
        float cy = py + cs / 2f;
        float pulse = MathF.Sin(_globalTimer * 4f + cell.X * 0.5f) * 0.15f + 0.85f;
        float cooldownFade = cell.TeleporterCooldown > 0 ? 0.3f : 1f;

        // Äußerer Glow
        _glowPaint.Color = portalColor.WithAlpha((byte)(80 * pulse * cooldownFade));
        _glowPaint.MaskFilter = _mediumGlow;
        canvas.DrawCircle(cx, cy, cs * 0.45f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Rotierender Ring
        float rotation = _globalTimer * 90f; // 90° pro Sekunde
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(rotation);

        // Ring zeichnen (4 Arcs)
        _strokePaint.Color = portalColor.WithAlpha((byte)(220 * cooldownFade));
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;

        float r = cs * 0.35f * pulse;
        var arcRect = new SKRect(-r, -r, r, r);

        // 3 Arcs für rotierenden Portal-Ring (wiederverwendeter _fusePath)
        _fusePath.Reset();
        _fusePath.AddArc(arcRect, 0, 80);
        canvas.DrawPath(_fusePath, _strokePaint);
        _fusePath.Reset();
        _fusePath.AddArc(arcRect, 120, 80);
        canvas.DrawPath(_fusePath, _strokePaint);
        _fusePath.Reset();
        _fusePath.AddArc(arcRect, 240, 80);
        canvas.DrawPath(_fusePath, _strokePaint);
        _strokePaint.MaskFilter = null;

        canvas.Restore();

        // Innerer Punkt (Kern des Portals)
        _fillPaint.Color = portalColor.WithAlpha((byte)(180 * pulse * cooldownFade));
        _fillPaint.MaskFilter = isNeon ? _smallGlow : null;
        canvas.DrawCircle(cx, cy, cs * 0.1f, _fillPaint);
        _fillPaint.MaskFilter = null;
    }

    /// <summary>Lava-Riss: Pulsierender roter Riss, gefährlich wenn aktiv</summary>
    private void RenderLavaCrackTile(SKCanvas canvas, float px, float py, int cs, Cell cell, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Boden (dunkler als normal, vulkanisch)
        _fillPaint.Color = isNeon ? new SKColor(45, 20, 20) : new SKColor(100, 65, 55);
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        float cx = px + cs / 2f;
        float cy = py + cs / 2f;

        bool isActive = cell.IsLavaCrackActive;
        float timerMod = cell.LavaCrackTimer % 4f;

        // Riss-Muster (immer sichtbar, auch wenn inaktiv)
        byte crackAlpha = isActive ? (byte)255 : (byte)100;
        var crackColor = isActive
            ? (isNeon ? new SKColor(255, 60, 0, crackAlpha) : new SKColor(255, 80, 20, crackAlpha))
            : (isNeon ? new SKColor(200, 80, 40, crackAlpha) : new SKColor(180, 90, 50, crackAlpha));

        _strokePaint.Color = crackColor;
        _strokePaint.StrokeWidth = isActive ? 2.5f : 1.5f;
        _strokePaint.MaskFilter = isActive && isNeon ? _smallGlow : null;

        // Zickzack-Riss
        _fusePath.Reset();
        _fusePath.MoveTo(px + cs * 0.2f, py + 2);
        _fusePath.LineTo(px + cs * 0.45f, py + cs * 0.35f);
        _fusePath.LineTo(px + cs * 0.3f, py + cs * 0.5f);
        _fusePath.LineTo(px + cs * 0.6f, py + cs * 0.65f);
        _fusePath.LineTo(px + cs * 0.5f, py + cs - 2);
        canvas.DrawPath(_fusePath, _strokePaint);

        // Zweiter kleinerer Riss
        _fusePath.Reset();
        _fusePath.MoveTo(px + cs * 0.7f, py + 4);
        _fusePath.LineTo(px + cs * 0.55f, py + cs * 0.4f);
        _fusePath.LineTo(px + cs * 0.8f, py + cs * 0.7f);
        canvas.DrawPath(_fusePath, _strokePaint);
        _strokePaint.MaskFilter = null;

        // Aktiver Zustand: Glühende Lava-Füllung
        if (isActive)
        {
            float intensity = (timerMod - 2.5f) / 1.5f; // 0→1 während aktiver Phase
            byte lavaAlpha = (byte)(120 + 80 * MathF.Sin(_globalTimer * 8f));

            // Roter/orangener Glow über die ganze Zelle
            _glowPaint.Color = isNeon
                ? new SKColor(255, 40, 0, lavaAlpha)
                : new SKColor(255, 100, 20, lavaAlpha);
            _glowPaint.MaskFilter = _smallGlow;
            canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _glowPaint);
            _glowPaint.MaskFilter = null;

            // Gefahren-Indikator: Pulsierendes X in der Mitte
            _strokePaint.Color = new SKColor(255, 255, 200, (byte)(200 * intensity));
            _strokePaint.StrokeWidth = 2f;
            float xSize = cs * 0.15f;
            canvas.DrawLine(cx - xSize, cy - xSize, cx + xSize, cy + xSize, _strokePaint);
            canvas.DrawLine(cx + xSize, cy - xSize, cx - xSize, cy + xSize, _strokePaint);
        }
        else
        {
            // Inaktiv: Schwacher Warn-Glow wenn fast aktiv (timerMod > 2.0)
            if (timerMod > 2.0f)
            {
                float warnIntensity = (timerMod - 2.0f) / 0.5f;
                byte warnAlpha = (byte)(40 * warnIntensity);
                _fillPaint.Color = isNeon
                    ? new SKColor(255, 60, 0, warnAlpha)
                    : new SKColor(255, 100, 20, warnAlpha);
                canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _fillPaint);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXIT
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderExit(SKCanvas canvas, GameGrid grid, Cell? exitCell)
    {
        // Gecachte Exit-Zelle nutzen statt Grid-Iteration (150 Zellen pro Frame gespart)
        if (exitCell == null || exitCell.Type != CellType.Exit)
            return;

        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;
        float pulse = MathF.Sin(_globalTimer * 3) * 0.2f + 0.8f;

        float px = exitCell.X * GameGrid.CELL_SIZE;
        float py = exitCell.Y * GameGrid.CELL_SIZE;
        int cs = GameGrid.CELL_SIZE;
        float cx = px + cs / 2f;
        float cy = py + cs / 2f;

        if (isNeon)
        {
            // Neon green glow circle
            _glowPaint.Color = _palette.ExitGlow.WithAlpha((byte)(120 * pulse));
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawCircle(cx, cy, cs * 0.45f, _glowPaint);

            _fillPaint.Color = _palette.ExitInner.WithAlpha((byte)(255 * pulse));
            _fillPaint.MaskFilter = null;
            canvas.DrawCircle(cx, cy, cs * 0.3f, _fillPaint);
            _glowPaint.MaskFilter = null;
        }
        else
        {
            // Classic green door with pulsing glow
            _fillPaint.Color = _palette.ExitGlow.WithAlpha((byte)(80 * pulse));
            _fillPaint.MaskFilter = null;
            canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _fillPaint);

            _fillPaint.Color = _palette.ExitInner;
            canvas.DrawRect(px + 8, py + 6, 16, 20, _fillPaint);

            // Door handle
            _fillPaint.Color = _palette.ExitGlow;
            canvas.DrawCircle(px + 20, py + 16, 2, _fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOMB
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderBomb(SKCanvas canvas, Bomb bomb)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        // Squash/Stretch: Platzierungs-Bounce in den ersten 0.3s
        float age = Bomb.DEFAULT_FUSE_TIME - bomb.FuseTimer;
        float birthScale = 1f;
        if (age < 0.3f)
        {
            float t = age / 0.3f; // 0→1
            // Bounce: schnell groß, dann einpendeln (overshoot + settle)
            birthScale = 1f + MathF.Sin(t * MathF.PI) * 0.25f * (1f - t);
        }

        // Slide-Indikator: Leichtes Strecken in Gleitrichtung
        float stretchX = 1f, stretchY = 1f;
        if (bomb.IsSliding)
        {
            float stretch = 0.15f;
            if (bomb.SlideDirection is Direction.Left or Direction.Right)
            { stretchX = 1f + stretch; stretchY = 1f - stretch * 0.5f; }
            else
            { stretchY = 1f + stretch; stretchX = 1f - stretch * 0.5f; }
        }

        // Pulsation beschleunigt sich je näher die Explosion (4→12 Hz)
        float fuseProgress = 1f - (bomb.FuseTimer / Bomb.DEFAULT_FUSE_TIME);
        float pulseSpeed = 4f + fuseProgress * 8f;
        float pulseAmount = 0.06f + fuseProgress * 0.04f; // Dezente Pulsation, stärker kurz vor Explosion
        float pulse = MathF.Sin(_globalTimer * pulseSpeed) * pulseAmount + (1f - pulseAmount);
        float drawSize = cs * pulse * birthScale;

        // Glow beschleunigt und intensiviert sich
        float glowSpeed = 3f + fuseProgress * 6f;
        float glowPulse = MathF.Sin(_globalTimer * glowSpeed) * 0.3f + 0.5f;
        byte glowAlpha = (byte)(80 + fuseProgress * 80); // Heller kurz vor Explosion
        _glowPaint.Color = _palette.BombGlowColor.WithAlpha((byte)(glowAlpha * glowPulse));
        _glowPaint.MaskFilter = _mediumGlow;
        canvas.DrawCircle(bomb.X, bomb.Y, drawSize * 0.5f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Bomb body (glossy sphere mit Squash/Stretch)
        float radius = drawSize * 0.38f;
        _fillPaint.Color = _palette.BombBody;
        _fillPaint.MaskFilter = null;
        if (bomb.IsSliding)
        {
            // Oval bei Slide
            canvas.DrawOval(bomb.X, bomb.Y, radius * stretchX, radius * stretchY, _fillPaint);
        }
        else
        {
            canvas.DrawCircle(bomb.X, bomb.Y, radius, _fillPaint);
        }

        // Gloss highlight (top-left)
        _fillPaint.Color = _palette.BombHighlight.WithAlpha(120);
        canvas.DrawCircle(bomb.X - radius * 0.3f, bomb.Y - radius * 0.3f, radius * 0.25f, _fillPaint);

        // Fuse (wavy line upward)
        _strokePaint.Color = bomb.IsAboutToExplode ? SKColors.Red : _palette.BombFuse;
        _strokePaint.StrokeWidth = 2;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
        _fusePath.Reset();
        _fusePath.MoveTo(bomb.X, bomb.Y - radius);
        _fusePath.QuadTo(bomb.X + 5, bomb.Y - radius - 8, bomb.X + 8, bomb.Y - radius - 4);
        canvas.DrawPath(_fusePath, _strokePaint);
        _strokePaint.MaskFilter = null;

        // Spark at tip
        if (((int)(_globalTimer * 10) % 2) == 0)
        {
            _fillPaint.Color = isNeon ? new SKColor(255, 200, 100) : SKColors.Yellow;
            _fillPaint.MaskFilter = isNeon ? _smallGlow : null;
            canvas.DrawCircle(bomb.X + 8, bomb.Y - radius - 4, 3, _fillPaint);
            _fillPaint.MaskFilter = null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXPLOSION
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderExplosion(SKCanvas canvas, Explosion explosion)
    {
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;
        float progress = 1f - (explosion.Timer / Explosion.DURATION); // 0→1
        float alpha = 1f - progress * 0.3f;
        float cs = GameGrid.CELL_SIZE;

        // Initialer Explosions-Blitz (erste 20% der Dauer)
        if (progress < 0.2f)
        {
            float flashAlpha = (1f - progress / 0.2f) * 0.6f;
            _fillPaint.Color = SKColors.White.WithAlpha((byte)(255 * flashAlpha));
            _fillPaint.MaskFilter = _outerGlow;
            foreach (var cell in explosion.AffectedCells)
            {
                float fx = cell.X * cs - 2;
                float fy = cell.Y * cs - 2;
                canvas.DrawRect(fx, fy, cs + 4, cs + 4, _fillPaint);
            }
            _fillPaint.MaskFilter = null;
        }

        // Shockwave-Ring (expandierender Kreis in den ersten 40%)
        if (progress < 0.4f && explosion.SourceBomb != null)
        {
            float shockProgress = progress / 0.4f; // 0→1 innerhalb des Shockwave-Fensters
            float maxRadius = explosion.SourceBomb.Range * cs;
            float radius = shockProgress * maxRadius;
            float shockAlpha = (1f - shockProgress) * 0.5f;

            float centerX = explosion.X + cs / 2f;
            float centerY = explosion.Y + cs / 2f;

            _strokePaint.Color = _palette.ExplosionCore.WithAlpha((byte)(255 * shockAlpha));
            _strokePaint.StrokeWidth = 2f + (1f - shockProgress) * 2f; // Dünner werdend
            _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
            canvas.DrawCircle(centerX, centerY, radius, _strokePaint);
            _strokePaint.MaskFilter = null;
        }

        // Outer glow ring
        _glowPaint.Color = _palette.ExplosionOuter.WithAlpha((byte)(80 * alpha));
        _glowPaint.MaskFilter = isNeon ? _mediumGlow : _outerGlow;

        foreach (var cell in explosion.AffectedCells)
        {
            float gpx = cell.X * cs;
            float gpy = cell.Y * cs;
            canvas.DrawRect(gpx - 2, gpy - 2, cs + 4, cs + 4, _glowPaint);
        }
        _glowPaint.MaskFilter = null;

        // Main explosion fill
        foreach (var cell in explosion.AffectedCells)
        {
            float px = cell.X * cs;
            float py = cell.Y * cs;
            float inset = 4;

            // Outer color
            _fillPaint.Color = _palette.ExplosionOuter.WithAlpha((byte)(255 * alpha));
            _fillPaint.MaskFilter = null;

            switch (cell.Type)
            {
                case ExplosionCellType.Center:
                    canvas.DrawRect(px + inset, py + inset, cs - inset * 2, cs - inset * 2, _fillPaint);
                    break;
                case ExplosionCellType.HorizontalMiddle:
                    canvas.DrawRect(px, py + inset, cs, cs - inset * 2, _fillPaint);
                    break;
                case ExplosionCellType.VerticalMiddle:
                    canvas.DrawRect(px + inset, py, cs - inset * 2, cs, _fillPaint);
                    break;
                default:
                    canvas.DrawRect(px + inset, py + inset, cs - inset * 2, cs - inset * 2, _fillPaint);
                    break;
            }

            // Inner core
            _fillPaint.Color = _palette.ExplosionInner.WithAlpha((byte)(200 * alpha));
            canvas.DrawRect(px + cs / 4f, py + cs / 4f, cs / 2f, cs / 2f, _fillPaint);

            // Bright center dot (Neon: cyan, Classic: white-yellow)
            if (isNeon)
            {
                _fillPaint.Color = _palette.ExplosionCore.WithAlpha((byte)(180 * alpha));
                _fillPaint.MaskFilter = _smallGlow;
                canvas.DrawCircle(px + cs / 2f, py + cs / 2f, cs * 0.15f, _fillPaint);
                _fillPaint.MaskFilter = null;
            }
            else
            {
                _fillPaint.Color = _palette.ExplosionCore.WithAlpha((byte)(200 * alpha));
                canvas.DrawCircle(px + cs / 2f, py + cs / 2f, cs * 0.15f, _fillPaint);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PLAYER
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderPlayer(SKCanvas canvas, Player player)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        // Blink effect for invincibility / spawn protection
        if ((player.IsInvincible || player.HasSpawnProtection) && ((int)(_globalTimer * 10) % 2) == 0)
            return;

        if (player.IsDying)
        {
            RenderPlayerDeath(canvas, player, cs);
            return;
        }

        float bodyW = cs * 0.55f;
        float bodyH = cs * 0.65f;

        // Prozedurale Walk-Animation: Wippen wenn in Bewegung
        float walkBob = 0f;
        if (player.IsMoving)
        {
            walkBob = MathF.Sin(_globalTimer * 14f) * 1.5f;
        }

        float bx = player.X - bodyW / 2f;
        float by = player.Y - bodyH / 2f + walkBob;

        // Skin-Farben bestimmen
        var skin = _customizationService.PlayerSkin;
        var skinBody = skin.Id != "default" ? skin.PrimaryColor : _palette.PlayerBody;
        var skinHelm = skin.Id != "default" ? skin.SecondaryColor : _palette.PlayerHelm;

        // Skin-Glow oder Neon-Aura
        if (skin.GlowColor.HasValue)
        {
            _glowPaint.Color = skin.GlowColor.Value;
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawRoundRect(bx - 3, by - 3, bodyW + 6, bodyH + 6, 8, 8, _glowPaint);
            _glowPaint.MaskFilter = null;
        }
        else if (isNeon && _palette.PlayerAura.Alpha > 0)
        {
            _glowPaint.Color = _palette.PlayerAura;
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawRoundRect(bx - 3, by - 3, bodyW + 6, bodyH + 6, 8, 8, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Schutzschild-Indikator: Cyan-Glow wenn aktiv
        if (player.HasShield)
        {
            float shieldPulse = MathF.Sin(_globalTimer * 4f) * 0.2f + 0.8f;
            _glowPaint.Color = new SKColor(0, 229, 255, (byte)(100 * shieldPulse));
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawCircle(player.X, player.Y + walkBob, cs * 0.55f, _glowPaint);
            _glowPaint.MaskFilter = null;

            // Innerer Ring
            _strokePaint.Color = new SKColor(0, 229, 255, (byte)(180 * shieldPulse));
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = _smallGlow;
            canvas.DrawCircle(player.X, player.Y + walkBob, cs * 0.5f, _strokePaint);
            _strokePaint.MaskFilter = null;
        }

        // Curse-Indikator: Lila Schimmer wenn verflucht
        if (player.IsCursed)
        {
            float cursePulse = MathF.Sin(_globalTimer * 8f) * 0.3f + 0.7f;
            _glowPaint.Color = new SKColor(180, 0, 180, (byte)(80 * cursePulse));
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawRoundRect(bx - 4, by - 4, bodyW + 8, bodyH + 8, 10, 10, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Body (rounded rect)
        _fillPaint.Color = skinBody;
        _fillPaint.MaskFilter = null;
        canvas.DrawRoundRect(bx, by, bodyW, bodyH, 6, 6, _fillPaint);

        // Helm/cap (semicircle on top)
        _fillPaint.Color = skinHelm;
        float helmR = bodyW * 0.45f;
        canvas.DrawCircle(player.X, by + 2, helmR, _fillPaint);
        // Cut off bottom half of helm circle by drawing body color rect over it
        _fillPaint.Color = skinBody;
        canvas.DrawRect(bx, by + 2, bodyW, helmR, _fillPaint);

        // Eyes (white circles with pupils that follow facing direction)
        float eyeY = player.Y - bodyH * 0.1f;
        float eyeSpacing = bodyW * 0.22f;
        float eyeR = 3.5f;
        float pupilR = 1.8f;
        float dx = player.FacingDirection.GetDeltaX() * 1.5f;
        float dy = player.FacingDirection.GetDeltaY() * 1.5f;

        // Eye whites
        _fillPaint.Color = SKColors.White;
        canvas.DrawCircle(player.X - eyeSpacing, eyeY, eyeR, _fillPaint);
        canvas.DrawCircle(player.X + eyeSpacing, eyeY, eyeR, _fillPaint);

        // Pupils
        _fillPaint.Color = SKColors.Black;
        canvas.DrawCircle(player.X - eyeSpacing + dx, eyeY + dy, pupilR, _fillPaint);
        canvas.DrawCircle(player.X + eyeSpacing + dx, eyeY + dy, pupilR, _fillPaint);
    }

    private void RenderPlayerDeath(SKCanvas canvas, Player player, float cs)
    {
        float progress = player.DeathTimer / 1.5f;
        byte alpha = (byte)(255 * (1 - progress));

        _fillPaint.Color = SKColors.Red.WithAlpha(alpha);
        _fillPaint.MaskFilter = null;

        // Squash/Stretch: Erst nach oben strecken, dann breit zusammenfallen
        float phase1 = Math.Min(progress / 0.3f, 1f); // Erste 30%: Strecken
        float phase2 = progress > 0.3f ? (progress - 0.3f) / 0.7f : 0f; // Rest: Squash
        float scaleX = 1f - phase1 * 0.3f + phase2 * 0.8f;
        float scaleY = 1f + phase1 * 0.4f - phase2 * 0.6f;
        float drawSize = cs * (1f + progress * 0.2f);
        float rx = drawSize / 3 * scaleX;
        float ry = drawSize / 3 * scaleY;
        canvas.DrawOval(player.X, player.Y + phase2 * 6f, rx, ry, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ENEMY
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderEnemy(SKCanvas canvas, Enemy enemy)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        if (enemy.IsDying)
        {
            RenderEnemyDeath(canvas, enemy, cs);
            return;
        }

        var (r, g, b) = enemy.Type.GetColor();
        var bodyColor = new SKColor(r, g, b);
        float bodyW = cs * 0.6f;
        float bodyH = cs * 0.65f;

        // Wobble-Effekt: Leichtes Pulsieren wenn in Bewegung
        float wobbleScale = 1f;
        float wobbleY = 0f;
        if (enemy.IsMoving)
        {
            wobbleScale = 1f + MathF.Sin(_globalTimer * 12f + enemy.X * 0.1f) * 0.04f;
            wobbleY = MathF.Sin(_globalTimer * 10f + enemy.Y * 0.1f) * 1.2f;
        }

        float bx = enemy.X - bodyW * wobbleScale / 2f;
        float by = enemy.Y - bodyH / 2f + wobbleY;

        // Neon aura (in enemy's color)
        if (isNeon)
        {
            _glowPaint.Color = new SKColor(r, g, b, 40);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawOval(enemy.X, enemy.Y, bodyW * 0.55f, bodyH * 0.55f, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Oval body (mit Wobble-Skalierung)
        _fillPaint.Color = bodyColor;
        _fillPaint.MaskFilter = null;
        canvas.DrawOval(enemy.X, enemy.Y + wobbleY, bodyW * 0.45f * wobbleScale, bodyH * 0.45f, _fillPaint);

        // Angry eyes (slightly angled/slanted)
        float eyeY = enemy.Y - bodyH * 0.08f;
        float eyeSpacing = bodyW * 0.2f;
        float eyeR = 3f;
        float pupilR = 1.5f;
        float pdx = enemy.FacingDirection.GetDeltaX() * 1.5f;
        float pdy = enemy.FacingDirection.GetDeltaY() * 1.5f;

        // Eye whites
        _fillPaint.Color = SKColors.White;
        canvas.DrawCircle(enemy.X - eyeSpacing, eyeY, eyeR, _fillPaint);
        canvas.DrawCircle(enemy.X + eyeSpacing, eyeY, eyeR, _fillPaint);

        // Pupils (follow direction)
        _fillPaint.Color = SKColors.Black;
        canvas.DrawCircle(enemy.X - eyeSpacing + pdx, eyeY + pdy, pupilR, _fillPaint);
        canvas.DrawCircle(enemy.X + eyeSpacing + pdx, eyeY + pdy, pupilR, _fillPaint);

        // Angry eyebrow lines (small angled strokes above eyes)
        _strokePaint.Color = new SKColor(40, 0, 0);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.MaskFilter = null;
        float browY = eyeY - eyeR - 1.5f;
        canvas.DrawLine(enemy.X - eyeSpacing - 2, browY - 1, enemy.X - eyeSpacing + 2, browY + 1, _strokePaint);
        canvas.DrawLine(enemy.X + eyeSpacing + 2, browY - 1, enemy.X + eyeSpacing - 2, browY + 1, _strokePaint);

        // Mouth (varies by enemy type)
        float mouthY = enemy.Y + bodyH * 0.15f;
        _strokePaint.Color = new SKColor(60, 0, 0);
        _strokePaint.StrokeWidth = 1.2f;

        if ((int)enemy.Type % 3 == 0)
        {
            // Toothy grin
            canvas.DrawLine(enemy.X - 3, mouthY, enemy.X + 3, mouthY, _strokePaint);
            _fillPaint.Color = SKColors.White;
            canvas.DrawRect(enemy.X - 2, mouthY - 1, 1.5f, 2, _fillPaint);
            canvas.DrawRect(enemy.X + 0.5f, mouthY - 1, 1.5f, 2, _fillPaint);
        }
        else if ((int)enemy.Type % 3 == 1)
        {
            // Frown
            _fusePath.Reset();
            _fusePath.MoveTo(enemy.X - 3, mouthY);
            _fusePath.QuadTo(enemy.X, mouthY + 3, enemy.X + 3, mouthY);
            canvas.DrawPath(_fusePath, _strokePaint);
        }
        else
        {
            // Simple line
            canvas.DrawLine(enemy.X - 3, mouthY, enemy.X + 3, mouthY, _strokePaint);
        }
    }

    private void RenderEnemyDeath(SKCanvas canvas, Enemy enemy, float cs)
    {
        float progress = enemy.DeathTimer / 0.8f;
        byte alpha = (byte)(255 * (1 - progress));

        var (r, g, b) = enemy.Type.GetColor();
        _fillPaint.Color = new SKColor(r, g, b, alpha);
        _fillPaint.MaskFilter = null;

        // Squash/Stretch: Breiterer Squash der nach oben schießt, dann zusammenfällt
        float squashX = 1f + progress * 0.6f; // Breiter werden
        float squashY = 1f - progress * 0.4f; // Flacher werden
        float drawSize = cs * (1 - progress * 0.3f);
        float rx = drawSize / 3 * squashX;
        float ry = drawSize / 3 * squashY;
        canvas.DrawOval(enemy.X, enemy.Y + progress * 4f, rx, ry, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POWER-UP
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderPowerUp(SKCanvas canvas, PowerUp powerUp)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        // Blinking when about to expire
        if (powerUp.IsBlinking && !powerUp.IsBeingCollected && ((int)(_globalTimer * 8) % 2) == 0)
            return;

        // Einsammel-Animation: Shrink + Spin + Fade
        float collectScale = 1f;
        float collectRotation = 0f;
        byte collectAlpha = 255;
        if (powerUp.IsBeingCollected)
        {
            float progress = 1f - (powerUp.CollectTimer / PowerUp.COLLECT_DURATION); // 0→1
            collectScale = 1f - progress; // 1→0
            collectRotation = progress * 720f; // 2 volle Drehungen
            collectAlpha = (byte)(255 * (1f - progress));
        }

        // Bobbing animation
        float bob = MathF.Sin(_globalTimer * 3) * 2;
        float px = powerUp.X;
        float py = powerUp.Y + bob;

        SKColor color = GetPowerUpColor(powerUp.Type);
        float radius = cs * 0.35f * collectScale;

        // Einsammel-Animation: Canvas transformieren
        if (powerUp.IsBeingCollected)
        {
            canvas.Save();
            canvas.Translate(px, py);
            canvas.RotateDegrees(collectRotation);
            canvas.Translate(-px, -py);
        }

        // Neon glow aura
        if (isNeon)
        {
            _glowPaint.Color = color.WithAlpha(50);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawCircle(px, py, radius + 3, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Rounded background (mit Alpha bei Einsammel-Animation)
        byte bgAlpha = powerUp.IsBeingCollected
            ? (byte)(255 * Math.Clamp(powerUp.CollectTimer / PowerUp.COLLECT_DURATION, 0f, 1f))
            : (byte)255;
        _fillPaint.Color = color.WithAlpha(bgAlpha);
        _fillPaint.MaskFilter = null;
        canvas.DrawCircle(px, py, radius, _fillPaint);

        // Icon/symbol (mit Alpha bei Einsammel-Animation)
        byte iconAlpha = powerUp.IsBeingCollected
            ? (byte)(255 * Math.Clamp(powerUp.CollectTimer / PowerUp.COLLECT_DURATION, 0f, 1f))
            : (byte)255;
        _fillPaint.Color = SKColors.White.WithAlpha(iconAlpha);
        RenderPowerUpIcon(canvas, powerUp.Type, px, py, radius * 0.6f);

        // Canvas-Transform wiederherstellen nach Einsammel-Animation
        if (powerUp.IsBeingCollected)
        {
            canvas.Restore();
        }
    }

    private void RenderPowerUpIcon(SKCanvas canvas, PowerUpType type, float cx, float cy, float size)
    {
        _strokePaint.Color = SKColors.White;
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.MaskFilter = null;

        switch (type)
        {
            case PowerUpType.BombUp:
                // Small bomb circle
                canvas.DrawCircle(cx, cy + 1, size * 0.5f, _fillPaint);
                canvas.DrawLine(cx, cy - size * 0.3f, cx + size * 0.3f, cy - size * 0.6f, _strokePaint);
                break;

            case PowerUpType.Fire:
                // Flame shape (triangle-ish)
                _fusePath.Reset();
                _fusePath.MoveTo(cx, cy - size * 0.7f);
                _fusePath.LineTo(cx + size * 0.4f, cy + size * 0.5f);
                _fusePath.LineTo(cx - size * 0.4f, cy + size * 0.5f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                break;

            case PowerUpType.Speed:
                // Arrow pointing right
                _fusePath.Reset();
                _fusePath.MoveTo(cx - size * 0.4f, cy - size * 0.3f);
                _fusePath.LineTo(cx + size * 0.5f, cy);
                _fusePath.LineTo(cx - size * 0.4f, cy + size * 0.3f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                break;

            case PowerUpType.Wallpass:
                // Ghost-like shape
                canvas.DrawCircle(cx, cy - size * 0.15f, size * 0.35f, _fillPaint);
                canvas.DrawRect(cx - size * 0.35f, cy, size * 0.7f, size * 0.3f, _fillPaint);
                break;

            case PowerUpType.Detonator:
                // Lightning bolt
                _fusePath.Reset();
                _fusePath.MoveTo(cx + size * 0.15f, cy - size * 0.6f);
                _fusePath.LineTo(cx - size * 0.2f, cy + size * 0.05f);
                _fusePath.LineTo(cx + size * 0.1f, cy + size * 0.05f);
                _fusePath.LineTo(cx - size * 0.15f, cy + size * 0.6f);
                canvas.DrawPath(_fusePath, _strokePaint);
                break;

            case PowerUpType.Bombpass:
                // Circle with arrow through it
                _strokePaint.StrokeWidth = 1.5f;
                canvas.DrawCircle(cx, cy, size * 0.4f, _strokePaint);
                canvas.DrawLine(cx - size * 0.6f, cy, cx + size * 0.6f, cy, _strokePaint);
                break;

            case PowerUpType.Flamepass:
                // Shield shape
                _fusePath.Reset();
                _fusePath.MoveTo(cx, cy - size * 0.5f);
                _fusePath.LineTo(cx + size * 0.4f, cy - size * 0.2f);
                _fusePath.LineTo(cx + size * 0.3f, cy + size * 0.4f);
                _fusePath.LineTo(cx, cy + size * 0.6f);
                _fusePath.LineTo(cx - size * 0.3f, cy + size * 0.4f);
                _fusePath.LineTo(cx - size * 0.4f, cy - size * 0.2f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                break;

            case PowerUpType.Mystery:
                // Question mark
                _textPaint.Color = SKColors.White;
                canvas.DrawText("?", cx, cy + size * 0.25f, SKTextAlign.Center, _powerUpFont, _textPaint);
                break;

            case PowerUpType.Kick:
                // Schuh/Boot-Form (Pfeil nach rechts)
                _fusePath.Reset();
                _fusePath.MoveTo(cx - size * 0.5f, cy - size * 0.3f);
                _fusePath.LineTo(cx + size * 0.5f, cy);
                _fusePath.LineTo(cx - size * 0.5f, cy + size * 0.3f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                // Kleiner Kreis (= Bombe)
                canvas.DrawCircle(cx + size * 0.3f, cy - size * 0.4f, size * 0.2f, _fillPaint);
                break;

            case PowerUpType.LineBomb:
                // Drei kleine Kreise in einer Reihe
                canvas.DrawCircle(cx - size * 0.4f, cy, size * 0.2f, _fillPaint);
                canvas.DrawCircle(cx, cy, size * 0.2f, _fillPaint);
                canvas.DrawCircle(cx + size * 0.4f, cy, size * 0.2f, _fillPaint);
                break;

            case PowerUpType.PowerBomb:
                // Großer Kreis mit Stern
                canvas.DrawCircle(cx, cy, size * 0.4f, _fillPaint);
                _strokePaint.Color = new SKColor(255, 255, 100);
                _strokePaint.StrokeWidth = 2f;
                _strokePaint.MaskFilter = null;
                canvas.DrawLine(cx, cy - size * 0.3f, cx, cy + size * 0.3f, _strokePaint);
                canvas.DrawLine(cx - size * 0.3f, cy, cx + size * 0.3f, cy, _strokePaint);
                break;

            case PowerUpType.Skull:
                // Totenkopf (Kreis + Augenhöhlen + Kiefer)
                canvas.DrawCircle(cx, cy - size * 0.1f, size * 0.4f, _fillPaint);
                _fillPaint.Color = SKColors.Black;
                canvas.DrawCircle(cx - size * 0.15f, cy - size * 0.15f, size * 0.12f, _fillPaint);
                canvas.DrawCircle(cx + size * 0.15f, cy - size * 0.15f, size * 0.12f, _fillPaint);
                canvas.DrawRect(cx - size * 0.2f, cy + size * 0.15f, size * 0.4f, size * 0.08f, _fillPaint);
                _fillPaint.Color = SKColors.White;
                break;

            default:
                _textPaint.Color = SKColors.White;
                canvas.DrawText("?", cx, cy + size * 0.25f, SKTextAlign.Center, _powerUpFont, _textPaint);
                break;
        }
    }

    private static SKColor GetPowerUpColor(PowerUpType type) => type switch
    {
        PowerUpType.BombUp => new SKColor(80, 80, 240),
        PowerUpType.Fire => new SKColor(240, 90, 40),
        PowerUpType.Speed => new SKColor(60, 220, 80),
        PowerUpType.Wallpass => new SKColor(150, 100, 50),
        PowerUpType.Detonator => new SKColor(240, 40, 40),
        PowerUpType.Bombpass => new SKColor(50, 50, 150),
        PowerUpType.Flamepass => new SKColor(240, 190, 40),
        PowerUpType.Mystery => new SKColor(180, 80, 240),
        PowerUpType.Kick => new SKColor(255, 165, 0),
        PowerUpType.LineBomb => new SKColor(0, 180, 255),
        PowerUpType.PowerBomb => new SKColor(255, 50, 50),
        PowerUpType.Skull => new SKColor(100, 0, 100),
        _ => SKColors.White
    };

    // ═══════════════════════════════════════════════════════════════════════
    // HUD (right side panel)
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderHUD(SKCanvas canvas, float remainingTime, int score, int lives, Player? player)
    {
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;
        float x = _hudX;
        float y = _hudY;
        float w = _hudWidth;
        float h = _hudHeight;

        // Background panel
        if (_hudGradientShader == null || Math.Abs(_lastHudShaderHeight - h) > 1f)
        {
            _hudGradientShader?.Dispose();
            _hudGradientShader = SKShader.CreateLinearGradient(
                new SKPoint(x, y),
                new SKPoint(x, y + h),
                new[] { _palette.HudBg, _palette.HudBg.WithAlpha(210) },
                null, SKShaderTileMode.Clamp);
            _lastHudShaderHeight = h;
        }

        _fillPaint.Shader = _hudGradientShader;
        _fillPaint.MaskFilter = null;
        canvas.DrawRect(x, y, w, h, _fillPaint);
        _fillPaint.Shader = null;

        // Left border line
        _strokePaint.Color = _palette.HudBorder;
        _strokePaint.StrokeWidth = isNeon ? 1.5f : 1f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
        canvas.DrawLine(x, y, x, y + h, _strokePaint);
        _strokePaint.MaskFilter = null;

        float cx = x + w / 2f;
        float cy = y + 20;

        // ── TIME ──
        _textPaint.Color = _palette.HudText.WithAlpha(150);
        _textPaint.MaskFilter = null;
        canvas.DrawText("TIME", cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
        cy += 24;

        bool timeWarning = remainingTime <= 30;
        _textPaint.Color = timeWarning ? _palette.HudTimeWarning : _palette.HudText;
        _textPaint.MaskFilter = isNeon ? _hudTextGlow : null;
        int timeInt = (int)remainingTime;
        if (timeInt != _lastTimeValue)
        {
            _lastTimeValue = timeInt;
            _lastTimeString = $"{timeInt:D3}";
        }
        canvas.DrawText(_lastTimeString, cx, cy, SKTextAlign.Center, _hudFontLarge, _textPaint);
        _textPaint.MaskFilter = null;
        cy += 32;

        // Separator
        _strokePaint.Color = _palette.HudBorder.WithAlpha(80);
        _strokePaint.StrokeWidth = 1;
        _strokePaint.MaskFilter = null;
        canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
        cy += 16;

        // ── SCORE ──
        _textPaint.Color = _palette.HudText.WithAlpha(150);
        canvas.DrawText("SCORE", cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
        cy += 22;

        _textPaint.Color = _palette.HudAccent;
        _textPaint.MaskFilter = isNeon ? _hudTextGlow : null;
        if (score != _lastScoreValue)
        {
            _lastScoreValue = score;
            _lastScoreString = $"{score:D6}";
        }
        canvas.DrawText(_lastScoreString, cx, cy, SKTextAlign.Center, _hudFontMedium, _textPaint);
        _textPaint.MaskFilter = null;
        cy += 28;

        // Separator
        canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
        cy += 16;

        // ── LIVES (heart icons) ──
        _textPaint.Color = _palette.HudText.WithAlpha(150);
        canvas.DrawText("LIVES", cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
        cy += 20;

        float heartSize = 12;
        float heartsWidth = lives * (heartSize + 4) - 4;
        float heartStartX = cx - heartsWidth / 2f;

        for (int i = 0; i < lives; i++)
        {
            float hx = heartStartX + i * (heartSize + 4) + heartSize / 2f;
            RenderHeart(canvas, hx, cy, heartSize * 0.5f);
        }
        cy += heartSize + 16;

        // Separator
        canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
        cy += 16;

        // ── BOMB/FIRE info ──
        if (player != null)
        {
            _textPaint.Color = _palette.HudText.WithAlpha(150);
            canvas.DrawText("BOMBS", cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
            cy += 20;

            _textPaint.Color = _palette.HudText;
            _textPaint.MaskFilter = isNeon ? _hudTextGlow : null;
            if (player.MaxBombs != _lastBombsValue)
            {
                _lastBombsValue = player.MaxBombs;
                _lastBombsString = $"{player.MaxBombs}";
            }
            canvas.DrawText(_lastBombsString, cx - 20, cy, SKTextAlign.Center, _hudFontMedium, _textPaint);

            _textPaint.Color = new SKColor(255, 120, 40);
            if (player.FireRange != _lastFireValue)
            {
                _lastFireValue = player.FireRange;
                _lastFireString = $"{player.FireRange}";
            }
            canvas.DrawText(_lastFireString, cx + 20, cy, SKTextAlign.Center, _hudFontMedium, _textPaint);
            _textPaint.MaskFilter = null;

            // Mini-Bomben-Icon unter der Zahl
            RenderMiniBomb(canvas, cx - 20, cy + 10, 5f, isNeon);
            // Mini-Flammen-Icon unter der Zahl
            RenderMiniFlame(canvas, cx + 20, cy + 10, 5f, isNeon);
            cy += 30;

            // ── Active power-ups (stacked vertically, gepoolte Liste) ──
            _activePowers.Clear();
            if (player.SpeedLevel > 0)
            {
                string spdLabel = player.SpeedLevel > 1 ? $"SPD x{player.SpeedLevel}" : "SPD";
                _activePowers.Add((spdLabel, new SKColor(60, 220, 80)));
            }
            if (player.HasWallpass) _activePowers.Add(("WLP", new SKColor(150, 100, 50)));
            if (player.HasDetonator) _activePowers.Add(("DET", new SKColor(240, 40, 40)));
            if (player.HasBombpass) _activePowers.Add(("BMP", new SKColor(50, 50, 150)));
            if (player.HasFlamepass) _activePowers.Add(("FLP", new SKColor(240, 190, 40)));
            if (player.HasShield) _activePowers.Add(("SHIELD", new SKColor(0, 229, 255)));
            if (player.HasKick) _activePowers.Add(("KICK", new SKColor(255, 165, 0)));
            if (player.HasLineBomb) _activePowers.Add(("LINE", new SKColor(0, 180, 255)));
            if (player.HasPowerBomb) _activePowers.Add(("PWR", new SKColor(255, 50, 50)));
            if (player.IsCursed)
            {
                string curseLabel = player.ActiveCurse switch
                {
                    CurseType.Diarrhea => "DIA",
                    CurseType.Slow => "SLOW",
                    CurseType.Constipation => "BLOCK",
                    CurseType.ReverseControls => "REV",
                    _ => "???"
                };
                _activePowers.Add(($"☠{curseLabel}:{(int)player.CurseTimer}", new SKColor(180, 0, 180)));
            }
            if (player.IsInvincible)
            {
                // INV-String nur bei Aenderung des Timer-Werts neu erstellen
                int invTimer = (int)player.InvincibilityTimer;
                if (invTimer != _lastInvTimerValue)
                {
                    _lastInvTimerValue = invTimer;
                    _lastInvString = $"INV:{invTimer}";
                }
                _activePowers.Add((_lastInvString, new SKColor(180, 80, 240)));
            }

            if (_activePowers.Count > 0)
            {
                cy += 6;
                canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
                cy += 14;

                _textPaint.Color = _palette.HudText.WithAlpha(150);
                canvas.DrawText("POWER", cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
                cy += 18;

                foreach (var (label, color) in _activePowers)
                {
                    _textPaint.Color = color;
                    _textPaint.MaskFilter = isNeon ? _hudTextGlow : null;
                    canvas.DrawText(label, cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
                    _textPaint.MaskFilter = null;
                    cy += 16;
                }
            }
        }
    }

    private void RenderHeart(SKCanvas canvas, float cx, float cy, float r)
    {
        _fillPaint.Color = new SKColor(240, 50, 60);
        _fillPaint.MaskFilter = null;

        // Simple heart: two circles + triangle
        float d = r * 0.7f;
        canvas.DrawCircle(cx - d * 0.5f, cy - d * 0.2f, d * 0.55f, _fillPaint);
        canvas.DrawCircle(cx + d * 0.5f, cy - d * 0.2f, d * 0.55f, _fillPaint);

        _fusePath.Reset();
        _fusePath.MoveTo(cx - r, cy);
        _fusePath.LineTo(cx, cy + r);
        _fusePath.LineTo(cx + r, cy);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);
    }

    private void RenderMiniBomb(SKCanvas canvas, float cx, float cy, float r, bool isNeon)
    {
        // Bomben-Koerper (Kreis)
        _fillPaint.Color = isNeon ? new SKColor(180, 180, 200) : new SKColor(50, 50, 60);
        _fillPaint.MaskFilter = null;
        canvas.DrawCircle(cx, cy, r, _fillPaint);

        // Glanz-Highlight
        _fillPaint.Color = isNeon ? new SKColor(220, 230, 255, 80) : new SKColor(120, 120, 140, 100);
        canvas.DrawCircle(cx - r * 0.25f, cy - r * 0.3f, r * 0.35f, _fillPaint);

        // Lunte (kurze Linie nach oben)
        _strokePaint.Color = isNeon ? new SKColor(255, 160, 60) : new SKColor(160, 120, 60);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.MaskFilter = isNeon ? _hudTextGlow : null;
        canvas.DrawLine(cx, cy - r, cx + r * 0.4f, cy - r * 1.6f, _strokePaint);

        // Funken-Punkt
        _fillPaint.Color = isNeon ? new SKColor(255, 200, 80) : new SKColor(255, 180, 40);
        canvas.DrawCircle(cx + r * 0.4f, cy - r * 1.6f, 1.5f, _fillPaint);
        _strokePaint.MaskFilter = null;
    }

    private void RenderMiniFlame(SKCanvas canvas, float cx, float cy, float r, bool isNeon)
    {
        // Aeussere Flamme (orange)
        _fillPaint.Color = isNeon ? new SKColor(255, 130, 40) : new SKColor(255, 120, 30);
        _fillPaint.MaskFilter = isNeon ? _hudTextGlow : null;

        _fusePath.Reset();
        _fusePath.MoveTo(cx - r * 0.7f, cy + r);
        _fusePath.QuadTo(cx - r, cy - r * 0.3f, cx, cy - r * 1.4f);
        _fusePath.QuadTo(cx + r, cy - r * 0.3f, cx + r * 0.7f, cy + r);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);

        // Innere Flamme (gelb)
        _fillPaint.Color = isNeon ? new SKColor(255, 220, 80) : new SKColor(255, 200, 60);
        _fillPaint.MaskFilter = null;

        _fusePath.Reset();
        _fusePath.MoveTo(cx - r * 0.35f, cy + r);
        _fusePath.QuadTo(cx - r * 0.5f, cy, cx, cy - r * 0.6f);
        _fusePath.QuadTo(cx + r * 0.5f, cy, cx + r * 0.35f, cy + r);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);
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
    }
}
