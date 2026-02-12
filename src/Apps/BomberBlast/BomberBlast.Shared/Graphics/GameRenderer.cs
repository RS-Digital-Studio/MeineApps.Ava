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
        float remainingTime, int score, int lives)
    {
        canvas.Clear(_palette.Background);

        // Save canvas state and apply transform for game field
        canvas.Save();
        canvas.Translate(_offsetX, _offsetY);
        canvas.Scale(_scale);

        RenderGrid(canvas, grid);
        RenderExit(canvas, grid);

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
                }
            }
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
    // EXIT
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderExit(SKCanvas canvas, GameGrid grid)
    {
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;
        float pulse = MathF.Sin(_globalTimer * 3) * 0.2f + 0.8f;

        foreach (var cell in grid.GetCellsOfType(CellType.Exit))
        {
            float px = cell.X * GameGrid.CELL_SIZE;
            float py = cell.Y * GameGrid.CELL_SIZE;
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
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOMB
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderBomb(SKCanvas canvas, Bomb bomb)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        float pulse = MathF.Sin(_globalTimer * 8) * 0.1f + 0.9f;
        float drawSize = cs * pulse;

        // Glow behind bomb
        float glowPulse = MathF.Sin(_globalTimer * 6) * 0.3f + 0.5f;
        _glowPaint.Color = _palette.BombGlowColor.WithAlpha((byte)(100 * glowPulse));
        _glowPaint.MaskFilter = _mediumGlow;
        canvas.DrawCircle(bomb.X, bomb.Y, drawSize * 0.5f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Bomb body (glossy sphere)
        float radius = drawSize * 0.38f;
        _fillPaint.Color = _palette.BombBody;
        _fillPaint.MaskFilter = null;
        canvas.DrawCircle(bomb.X, bomb.Y, radius, _fillPaint);

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
        float alpha = 1f - explosion.Timer / Explosion.DURATION * 0.3f;
        float cs = GameGrid.CELL_SIZE;

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
        float scale = 1 + progress * 0.5f;

        _fillPaint.Color = SKColors.Red.WithAlpha(alpha);
        _fillPaint.MaskFilter = null;
        float drawSize = cs * scale;
        canvas.DrawCircle(player.X, player.Y, drawSize / 3, _fillPaint);
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
        float drawSize = cs * (1 - progress * 0.5f);
        canvas.DrawCircle(enemy.X, enemy.Y, drawSize / 3, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POWER-UP
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderPowerUp(SKCanvas canvas, PowerUp powerUp)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        // Blinking when about to expire
        if (powerUp.IsBlinking && ((int)(_globalTimer * 8) % 2) == 0)
            return;

        // Bobbing animation
        float bob = MathF.Sin(_globalTimer * 3) * 2;
        float px = powerUp.X;
        float py = powerUp.Y + bob;

        SKColor color = GetPowerUpColor(powerUp.Type);
        float radius = cs * 0.35f;

        // Neon glow aura
        if (isNeon)
        {
            _glowPaint.Color = color.WithAlpha(50);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawCircle(px, py, radius + 3, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Rounded background
        _fillPaint.Color = color;
        _fillPaint.MaskFilter = null;
        canvas.DrawCircle(px, py, radius, _fillPaint);

        // Icon/symbol
        _fillPaint.Color = SKColors.White;
        RenderPowerUpIcon(canvas, powerUp.Type, px, py, radius * 0.6f);
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
            default:
                // Question mark
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
            if (player.HasSpeed) _activePowers.Add(("SPD", new SKColor(60, 220, 80)));
            if (player.HasWallpass) _activePowers.Add(("WLP", new SKColor(150, 100, 50)));
            if (player.HasDetonator) _activePowers.Add(("DET", new SKColor(240, 40, 40)));
            if (player.HasBombpass) _activePowers.Add(("BMP", new SKColor(50, 50, 150)));
            if (player.HasFlamepass) _activePowers.Add(("FLP", new SKColor(240, 190, 40)));
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

        int gridX = (int)(gameX / GameGrid.CELL_SIZE);
        int gridY = (int)(gameY / GameGrid.CELL_SIZE);

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
