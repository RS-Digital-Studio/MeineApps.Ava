using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Hintergrund-Theme für verschiedene Menü-Screens.
/// Jedes Theme hat eigenen Gradient + eigene Partikel-Typen.
/// </summary>
public enum BackgroundTheme
{
    Default,      // Bomben + Funken + Flammen (MainMenu, Settings, Help, HighScores etc.)
    Dungeon,      // Violett/Dunkel, Fackeln, Fledermäuse, fallende Steine
    Shop,         // Gold-Gradient, schwebende Münzen, Shimmer-Partikel
    League,       // Cyan/Teal-Gradient, aufsteigende Trophäen, Sterne-Funken
    BattlePass,   // Lila/Orange-Gradient, XP-Orbs, aufsteigende Streifen
    Victory,      // Gold-Explosion, Confetti-Partikel, Fireworks
    LuckySpin     // Regenbogen-Schimmer, rotierende Lichtpunkte, Glitzer
}

/// <summary>
/// Animierter Hintergrund-Renderer für Menü-Screens.
/// Unterstützt 7 Themes mit jeweils eigenen Gradienten und Partikel-Systemen.
/// Struct-basiert, keine per-Frame-Allokationen, gepoolte SKPaint.
/// </summary>
public static class MenuBackgroundRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // KONSTANTEN
    // ═══════════════════════════════════════════════════════════════════════

    private const float GRID_SPACING = 48f;
    private const byte GRID_ALPHA = 20;

    // Default Theme
    private const int DEFAULT_BOMB_COUNT = 10;
    private const int DEFAULT_SPARK_COUNT = 35;
    private const int DEFAULT_FLAME_COUNT = 8;

    // Dungeon Theme
    private const int DUNGEON_TORCH_COUNT = 4;
    private const int DUNGEON_BAT_COUNT = 6;
    private const int DUNGEON_STONE_COUNT = 8;

    // Shop Theme
    private const int SHOP_COIN_COUNT = 12;
    private const int SHOP_SHIMMER_COUNT = 15;
    private const int SHOP_GEM_COUNT = 3;

    // League Theme
    private const int LEAGUE_TROPHY_COUNT = 6;
    private const int LEAGUE_STAR_COUNT = 20;
    private const int LEAGUE_LIGHT_COUNT = 4;

    // BattlePass Theme
    private const int BP_ORB_COUNT = 15;
    private const int BP_STRIPE_COUNT = 8;
    private const int BP_BADGE_COUNT = 6;

    // Victory Theme
    private const int VICTORY_CONFETTI_COUNT = 30;
    private const int VICTORY_FIREWORK_COUNT = 8;
    private const int VICTORY_SPARK_COUNT = 12;

    // LuckySpin Theme
    private const int SPIN_RAINBOW_COUNT = 30;
    private const int SPIN_GLITTER_COUNT = 20;
    private const int SPIN_LIGHT_COUNT = 6;

    // ═══════════════════════════════════════════════════════════════════════
    // GRADIENT-FARBEN PRO THEME
    // ═══════════════════════════════════════════════════════════════════════

    // Default: Bomberman-Blau (kontrastreich)
    private static readonly SKColor DefaultGrad1 = new(0x1A, 0x1A, 0x35);
    private static readonly SKColor DefaultGrad2 = new(0x1E, 0x33, 0x55);

    // Dungeon: Violett/Dunkel
    private static readonly SKColor DungeonGrad1 = new(0x1A, 0x0A, 0x2E);
    private static readonly SKColor DungeonGrad2 = new(0x0D, 0x0D, 0x1A);

    // Shop: Gold
    private static readonly SKColor ShopGrad1 = new(0x1A, 0x1A, 0x00);
    private static readonly SKColor ShopGrad2 = new(0x2D, 0x2D, 0x00);

    // League: Cyan/Teal
    private static readonly SKColor LeagueGrad1 = new(0x0A, 0x1A, 0x2E);
    private static readonly SKColor LeagueGrad2 = new(0x0D, 0x2D, 0x3A);

    // BattlePass: Lila/Orange
    private static readonly SKColor BPGrad1 = new(0x1A, 0x0A, 0x2E);
    private static readonly SKColor BPGrad2 = new(0x2E, 0x1A, 0x0A);

    // Victory: Gold/Dunkel
    private static readonly SKColor VictoryGrad1 = new(0x2E, 0x1A, 0x00);
    private static readonly SKColor VictoryGrad2 = new(0x1A, 0x0A, 0x00);

    // LuckySpin: Tiefes Violett/Blau (deutlich farbiger)
    private static readonly SKColor SpinGrad1 = new(0x12, 0x0A, 0x35);
    private static readonly SKColor SpinGrad2 = new(0x0A, 0x18, 0x38);

    // ═══════════════════════════════════════════════════════════════════════
    // GEPOOLTE PAINT-OBJEKTE
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKPaint _gradientPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _gridPaint = new()
    {
        IsAntialias = false, Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.5f, Color = new SKColor(255, 255, 255, GRID_ALPHA)
    };
    private static readonly SKPaint _p1 = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _p2 = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _p3 = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKMaskFilter _smallGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f);
    private static readonly SKMaskFilter _mediumGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
    private static readonly SKMaskFilter _largeGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);

    // Confetti-Farben (Victory Theme)
    private static readonly SKColor[] ConfettiColors =
    [
        new(0xFF, 0xD7, 0x00), // Gold
        new(0xFF, 0x44, 0x44), // Rot
        new(0x44, 0xFF, 0x44), // Grün
        new(0x44, 0x44, 0xFF), // Blau
        new(0x00, 0xBC, 0xD4), // Cyan
        new(0xFF, 0x00, 0xFF)  // Magenta
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-STRUCTS (GC-freundlich, kein Heap)
    // ═══════════════════════════════════════════════════════════════════════

    // Universeller Partikel-Struct (für alle Themes wiederverwendbar)
    private struct Particle
    {
        public float X, Y, Size, Speed, Phase;
        public byte R, G, B, A;
        public float Extra1, Extra2; // Theme-spezifische Daten
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-ARRAYS PRO THEME
    // ═══════════════════════════════════════════════════════════════════════

    // Default
    private static readonly Particle[] _defaultBombs = new Particle[10];
    private static readonly Particle[] _defaultSparks = new Particle[35];
    private static readonly Particle[] _defaultFlames = new Particle[8];

    // Dungeon
    private static readonly Particle[] _dungeonTorches = new Particle[DUNGEON_TORCH_COUNT];
    private static readonly Particle[] _dungeonBats = new Particle[DUNGEON_BAT_COUNT];
    private static readonly Particle[] _dungeonStones = new Particle[DUNGEON_STONE_COUNT];

    // Shop
    private static readonly Particle[] _shopCoins = new Particle[SHOP_COIN_COUNT];
    private static readonly Particle[] _shopShimmer = new Particle[SHOP_SHIMMER_COUNT];
    private static readonly Particle[] _shopGems = new Particle[SHOP_GEM_COUNT];

    // League
    private static readonly Particle[] _leagueTrophies = new Particle[LEAGUE_TROPHY_COUNT];
    private static readonly Particle[] _leagueStars = new Particle[LEAGUE_STAR_COUNT];
    private static readonly Particle[] _leagueLights = new Particle[LEAGUE_LIGHT_COUNT];

    // BattlePass
    private static readonly Particle[] _bpOrbs = new Particle[BP_ORB_COUNT];
    private static readonly Particle[] _bpStripes = new Particle[BP_STRIPE_COUNT];
    private static readonly Particle[] _bpBadges = new Particle[BP_BADGE_COUNT];

    // Victory
    private static readonly Particle[] _victoryConfetti = new Particle[VICTORY_CONFETTI_COUNT];
    private static readonly Particle[] _victoryFireworks = new Particle[VICTORY_FIREWORK_COUNT];
    private static readonly Particle[] _victorySparks = new Particle[VICTORY_SPARK_COUNT];

    // LuckySpin
    private static readonly Particle[] _spinRainbow = new Particle[30];
    private static readonly Particle[] _spinGlitter = new Particle[20];
    private static readonly Particle[] _spinLights = new Particle[6];

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALISIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    private static BackgroundTheme _activeTheme;
    private static bool _initialized;

    /// <summary>
    /// Initialisiert alle Partikel für ein bestimmtes Theme.
    /// </summary>
    public static void Initialize(int seed, BackgroundTheme theme = BackgroundTheme.Default)
    {
        _activeTheme = theme;
        var rng = new Random(seed + (int)theme * 1337);

        switch (theme)
        {
            case BackgroundTheme.Default:
                InitDefault(rng);
                break;
            case BackgroundTheme.Dungeon:
                InitDungeon(rng);
                break;
            case BackgroundTheme.Shop:
                InitShop(rng);
                break;
            case BackgroundTheme.League:
                InitLeague(rng);
                break;
            case BackgroundTheme.BattlePass:
                InitBattlePass(rng);
                break;
            case BackgroundTheme.Victory:
                InitVictory(rng);
                break;
            case BackgroundTheme.LuckySpin:
                InitLuckySpin(rng);
                break;
        }

        _initialized = true;
    }

    /// <summary>
    /// Rendert den animierten Menü-Hintergrund für das aktive Theme.
    /// </summary>
    public static void Render(SKCanvas canvas, float width, float height, float time,
        BackgroundTheme theme = BackgroundTheme.Default)
    {
        // Theme-Wechsel erkennen und re-initialisieren
        if (!_initialized || _activeTheme != theme)
            Initialize(42, theme);

        // Gradient + Grid (immer)
        RenderGradient(canvas, width, height, theme);
        RenderGrid(canvas, width, height, theme);

        // Subtiler radialer Glow nur für Default-Theme (vor den Partikeln)
        if (theme == BackgroundTheme.Default)
            RenderDefaultVignette(canvas, width, height, time);

        // Theme-spezifische Partikel
        switch (theme)
        {
            case BackgroundTheme.Default:
                RenderDefaultBombs(canvas, width, height, time);
                RenderDefaultSparks(canvas, width, height, time);
                RenderDefaultFlames(canvas, width, height, time);
                break;
            case BackgroundTheme.Dungeon:
                RenderDungeonTorches(canvas, width, height, time);
                RenderDungeonBats(canvas, width, height, time);
                RenderDungeonStones(canvas, width, height, time);
                break;
            case BackgroundTheme.Shop:
                RenderShopCoins(canvas, width, height, time);
                RenderShopShimmer(canvas, width, height, time);
                RenderShopGems(canvas, width, height, time);
                break;
            case BackgroundTheme.League:
                RenderLeagueTrophies(canvas, width, height, time);
                RenderLeagueStars(canvas, width, height, time);
                RenderLeagueLights(canvas, width, height, time);
                break;
            case BackgroundTheme.BattlePass:
                RenderBPOrbs(canvas, width, height, time);
                RenderBPStripes(canvas, width, height, time);
                RenderBPBadges(canvas, width, height, time);
                break;
            case BackgroundTheme.Victory:
                RenderVictoryConfetti(canvas, width, height, time);
                RenderVictoryFireworks(canvas, width, height, time);
                RenderVictorySparks(canvas, width, height, time);
                break;
            case BackgroundTheme.LuckySpin:
                RenderSpinRainbowRing(canvas, width, height, time);
                RenderSpinRainbow(canvas, width, height, time);
                RenderSpinGlitter(canvas, width, height, time);
                RenderSpinLights(canvas, width, height, time);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GEMEINSAME RENDER-METHODEN
    // ═══════════════════════════════════════════════════════════════════════

    private static void RenderGradient(SKCanvas canvas, float width, float height, BackgroundTheme theme)
    {
        var (c1, c2) = theme switch
        {
            BackgroundTheme.Dungeon => (DungeonGrad1, DungeonGrad2),
            BackgroundTheme.Shop => (ShopGrad1, ShopGrad2),
            BackgroundTheme.League => (LeagueGrad1, LeagueGrad2),
            BackgroundTheme.BattlePass => (BPGrad1, BPGrad2),
            BackgroundTheme.Victory => (VictoryGrad1, VictoryGrad2),
            BackgroundTheme.LuckySpin => (SpinGrad1, SpinGrad2),
            _ => (DefaultGrad1, DefaultGrad2)
        };

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(width, height),
            [c1, c2], SKShaderTileMode.Clamp);
        _gradientPaint.Shader = shader;
        canvas.DrawRect(0, 0, width, height, _gradientPaint);
        _gradientPaint.Shader = null;
    }

    private static void RenderGrid(SKCanvas canvas, float width, float height, BackgroundTheme theme)
    {
        // Kein Grid bei Victory (zu festlich) und LuckySpin (zu bunt)
        if (theme is BackgroundTheme.Victory or BackgroundTheme.LuckySpin)
            return;

        byte alpha = theme switch
        {
            BackgroundTheme.Dungeon => (byte)12,
            BackgroundTheme.Shop => (byte)15,
            _ => GRID_ALPHA
        };
        _gridPaint.Color = new SKColor(255, 255, 255, alpha);

        for (float x = GRID_SPACING; x < width; x += GRID_SPACING)
            canvas.DrawLine(x, 0, x, height, _gridPaint);
        for (float y = GRID_SPACING; y < height; y += GRID_SPACING)
            canvas.DrawLine(0, y, width, y, _gridPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DEFAULT THEME: Bomben + Funken + Flammen
    // ═══════════════════════════════════════════════════════════════════════

    private static void InitDefault(Random rng)
    {
        // Bomben-Silhouetten (stärker sichtbar)
        for (int i = 0; i < DEFAULT_BOMB_COUNT; i++)
        {
            ref var b = ref _defaultBombs[i];
            b.X = (float)rng.NextDouble();
            b.Y = (float)rng.NextDouble();
            b.Size = 22f + (float)rng.NextDouble() * 25f;
            b.Speed = 8f + (float)rng.NextDouble() * 12f;
            b.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            b.Speed *= 1f - (b.Size - 22f) / 47f * 0.5f;
            b.A = (byte)(40 + rng.Next(15));
        }

        // Funken (mehr + heller)
        for (int i = 0; i < DEFAULT_SPARK_COUNT; i++)
        {
            ref var s = ref _defaultSparks[i];
            s.X = (float)rng.NextDouble();
            s.Y = (float)rng.NextDouble();
            s.Size = 2.5f + (float)rng.NextDouble() * 2.5f;
            s.Speed = 10f + (float)rng.NextDouble() * 15f;
            s.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            if (rng.NextDouble() < 0.5)
            { s.R = 255; s.G = (byte)(140 + rng.Next(40)); s.B = 30; }
            else
            { s.R = 255; s.G = (byte)(200 + rng.Next(40)); s.B = (byte)(60 + rng.Next(40)); }
        }

        // Flammen (größer + heller)
        for (int i = 0; i < DEFAULT_FLAME_COUNT; i++)
        {
            ref var f = ref _defaultFlames[i];
            f.X = (i + 0.5f) / DEFAULT_FLAME_COUNT;
            f.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            f.Extra1 = 35f + (float)rng.NextDouble() * 25f; // Width
            f.Extra2 = 30f + (float)rng.NextDouble() * 25f; // Height (deutlich höher)
            f.A = (byte)(65 + rng.Next(20));
        }
    }

    private static void RenderDefaultBombs(SKCanvas canvas, float w, float h, float t)
    {
        _p1.MaskFilter = null;
        for (int i = 0; i < DEFAULT_BOMB_COUNT; i++)
        {
            ref var b = ref _defaultBombs[i];
            float baseX = b.X * w;
            float baseY = h - ((t * b.Speed + b.Y * h) % (h + b.Size * 2)) + b.Size;
            float sway = MathF.Sin(t * 0.5f + b.Phase) * b.Size * 0.8f;
            float x = baseX + sway, y = baseY, r = b.Size * 0.5f;

            _p1.Color = new SKColor(255, 255, 255, b.A);
            canvas.DrawCircle(x, y, r, _p1);
            // Zündschnur
            float fuseTop = y - r - r * 0.4f;
            canvas.DrawRect(x - r * 0.15f - 1f, fuseTop, r * 0.3f + 2f, r * 0.4f, _p1);
            canvas.DrawCircle(x, fuseTop - 1.5f, 2f, _p1);
        }
    }

    private static void RenderDefaultSparks(SKCanvas canvas, float w, float h, float t)
    {
        _p2.MaskFilter = _smallGlow;
        for (int i = 0; i < DEFAULT_SPARK_COUNT; i++)
        {
            ref var s = ref _defaultSparks[i];
            float x = s.X * w + MathF.Sin(t * 1.2f + s.Phase) * 8f;
            float y = h - ((t * s.Speed + s.Y * h) % (h + 20f));
            float pulse = MathF.Sin(t * 3f + s.Phase) * 0.3f + 0.7f;
            _p2.Color = new SKColor(s.R, s.G, s.B, (byte)(120 * pulse));
            canvas.DrawCircle(x, y, s.Size, _p2);
        }
        _p2.MaskFilter = null;
    }

    private static void RenderDefaultFlames(SKCanvas canvas, float w, float h, float t)
    {
        _p3.MaskFilter = _mediumGlow;
        for (int i = 0; i < DEFAULT_FLAME_COUNT; i++)
        {
            ref var f = ref _defaultFlames[i];
            float x = f.X * w;
            float flicker1 = MathF.Sin(t * 2.5f + f.Phase) * 0.3f + 0.7f;
            float flicker2 = MathF.Sin(t * 3.8f + f.Phase * 1.3f) * 0.2f + 0.8f;
            float combined = flicker1 * flicker2;
            float ch = f.Extra2 * combined, cw = f.Extra1 * (0.9f + flicker2 * 0.1f);
            byte alpha = (byte)(f.A * combined);
            float wind = MathF.Sin(t * 0.8f + f.Phase * 0.7f) * 4f;

            using var path = new SKPath();
            path.MoveTo(x - cw * 0.5f, h);
            path.QuadTo(x - cw * 0.3f + wind * 0.5f, h - ch * 0.6f, x + wind, h - ch);
            path.QuadTo(x + cw * 0.3f + wind * 0.5f, h - ch * 0.6f, x + cw * 0.5f, h);
            path.Close();
            _p3.Color = new SKColor(255, 80, 20, alpha);
            canvas.DrawPath(path, _p3);

            // Innerer Kern
            float ih = ch * 0.5f, iw = cw * 0.5f;
            using var inner = new SKPath();
            inner.MoveTo(x - iw * 0.4f, h);
            inner.QuadTo(x - iw * 0.2f + wind * 0.3f, h - ih * 0.6f, x + wind * 0.6f, h - ih);
            inner.QuadTo(x + iw * 0.2f + wind * 0.3f, h - ih * 0.6f, x + iw * 0.4f, h);
            inner.Close();
            _p3.Color = new SKColor(255, 200, 80, (byte)Math.Min(255, alpha * 0.7f));
            canvas.DrawPath(inner, _p3);
        }
        _p3.MaskFilter = null;
    }

    /// <summary>Subtiler warmer radialer Glow in der Bildmitte (nur Default-Theme).</summary>
    private static void RenderDefaultVignette(SKCanvas canvas, float w, float h, float t)
    {
        float pulse = MathF.Sin(t * 0.3f) * 0.15f + 0.85f;
        float cx = w * 0.5f, cy = h * 0.45f;
        float radius = MathF.Max(w, h) * 0.6f;

        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy), radius * pulse,
            [new SKColor(255, 120, 40, 12), new SKColor(0, 0, 0, 0)],
            SKShaderTileMode.Clamp);
        _p1.Shader = shader;
        _p1.MaskFilter = null;
        canvas.DrawRect(0, 0, w, h, _p1);
        _p1.Shader = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DUNGEON THEME: Fackeln + Fledermäuse + fallende Steine
    // ═══════════════════════════════════════════════════════════════════════

    private static void InitDungeon(Random rng)
    {
        // Fackeln am Boden, gleichmäßig verteilt
        for (int i = 0; i < DUNGEON_TORCH_COUNT; i++)
        {
            ref var t = ref _dungeonTorches[i];
            t.X = (i + 0.5f) / DUNGEON_TORCH_COUNT;
            t.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            t.Size = 20f + (float)rng.NextDouble() * 10f;
            t.A = (byte)(60 + rng.Next(20));
        }

        // Fledermäuse: klein, Sinus-Flug
        for (int i = 0; i < DUNGEON_BAT_COUNT; i++)
        {
            ref var b = ref _dungeonBats[i];
            b.X = (float)rng.NextDouble();
            b.Y = 0.1f + (float)rng.NextDouble() * 0.5f;
            b.Size = 6f + (float)rng.NextDouble() * 4f;
            b.Speed = 20f + (float)rng.NextDouble() * 30f;
            b.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            b.A = (byte)(20 + rng.Next(20));
        }

        // Fallende Steinbrocken
        for (int i = 0; i < DUNGEON_STONE_COUNT; i++)
        {
            ref var s = ref _dungeonStones[i];
            s.X = (float)rng.NextDouble();
            s.Y = (float)rng.NextDouble();
            s.Size = 3f + (float)rng.NextDouble() * 4f;
            s.Speed = 15f + (float)rng.NextDouble() * 25f;
            s.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            s.R = (byte)(80 + rng.Next(40)); // Grau-Braun
            s.G = (byte)(70 + rng.Next(30));
            s.B = (byte)(60 + rng.Next(20));
            s.A = 40;
        }
    }

    private static void RenderDungeonTorches(SKCanvas canvas, float w, float h, float t)
    {
        _p1.MaskFilter = _mediumGlow;
        for (int i = 0; i < DUNGEON_TORCH_COUNT; i++)
        {
            ref var torch = ref _dungeonTorches[i];
            float x = torch.X * w;
            float baseY = h - 10f;

            // Flacker-Effekt
            float flicker = MathF.Sin(t * 4f + torch.Phase) * 0.3f + 0.7f;
            float flicker2 = MathF.Sin(t * 6f + torch.Phase * 1.5f) * 0.15f + 0.85f;
            float intensity = flicker * flicker2;

            // Fackel-Halter (Stange)
            _p1.MaskFilter = null;
            _p1.Color = new SKColor(100, 80, 60, 50);
            canvas.DrawRect(x - 2, baseY - torch.Size * 1.5f, 4, torch.Size * 1.5f, _p1);

            // Flamme
            _p1.MaskFilter = _mediumGlow;
            float flameH = torch.Size * intensity;
            float flameW = torch.Size * 0.6f;
            float flameY = baseY - torch.Size * 1.5f;
            byte alpha = (byte)(torch.A * intensity);

            _p1.Color = new SKColor(255, 140, 30, alpha);
            canvas.DrawOval(x, flameY - flameH * 0.5f, flameW, flameH, _p1);

            // Heller Kern
            _p1.Color = new SKColor(255, 220, 100, (byte)(alpha * 0.6f));
            canvas.DrawOval(x, flameY - flameH * 0.3f, flameW * 0.4f, flameH * 0.5f, _p1);

            // Licht-Kreis am Boden
            _p1.Color = new SKColor(255, 140, 30, (byte)(20 * intensity));
            canvas.DrawCircle(x, baseY, torch.Size * 2f, _p1);
        }
        _p1.MaskFilter = null;
    }

    private static void RenderDungeonBats(SKCanvas canvas, float w, float h, float t)
    {
        _p2.MaskFilter = null;
        for (int i = 0; i < DUNGEON_BAT_COUNT; i++)
        {
            ref var bat = ref _dungeonBats[i];
            // Horizontal fliegen mit Sinus-Wellung
            float x = ((bat.X * w + t * bat.Speed) % (w + 40f)) - 20f;
            float y = bat.Y * h + MathF.Sin(t * 2f + bat.Phase) * 20f;

            // Flügel-Schlag-Animation (Sinus-basiert)
            float wingAngle = MathF.Sin(t * 8f + bat.Phase) * 0.4f;
            float wingSpan = bat.Size * (1f + wingAngle * 0.3f);

            _p2.Color = new SKColor(60, 40, 60, bat.A);

            // Körper
            canvas.DrawOval(x, y, bat.Size * 0.3f, bat.Size * 0.2f, _p2);

            // Linker Flügel (Dreieck)
            using var lWing = new SKPath();
            lWing.MoveTo(x - bat.Size * 0.2f, y);
            lWing.LineTo(x - wingSpan, y - bat.Size * 0.3f * (1f + wingAngle));
            lWing.LineTo(x - wingSpan * 0.6f, y + bat.Size * 0.1f);
            lWing.Close();
            canvas.DrawPath(lWing, _p2);

            // Rechter Flügel
            using var rWing = new SKPath();
            rWing.MoveTo(x + bat.Size * 0.2f, y);
            rWing.LineTo(x + wingSpan, y - bat.Size * 0.3f * (1f + wingAngle));
            rWing.LineTo(x + wingSpan * 0.6f, y + bat.Size * 0.1f);
            rWing.Close();
            canvas.DrawPath(rWing, _p2);
        }
    }

    private static void RenderDungeonStones(SKCanvas canvas, float w, float h, float t)
    {
        _p3.MaskFilter = null;
        for (int i = 0; i < DUNGEON_STONE_COUNT; i++)
        {
            ref var s = ref _dungeonStones[i];
            float x = s.X * w + MathF.Sin(t * 0.5f + s.Phase) * 5f;
            float y = ((t * s.Speed + s.Y * h) % (h + 20f)) - 10f; // Fallend

            // Rotation
            float rot = t * 2f + s.Phase;

            canvas.Save();
            canvas.Translate(x, y);
            canvas.RotateRadians(rot);

            _p3.Color = new SKColor(s.R, s.G, s.B, s.A);
            canvas.DrawRect(-s.Size * 0.5f, -s.Size * 0.4f, s.Size, s.Size * 0.8f, _p3);

            canvas.Restore();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SHOP THEME: Münzen + Shimmer + Gem-Silhouetten
    // ═══════════════════════════════════════════════════════════════════════

    private static void InitShop(Random rng)
    {
        for (int i = 0; i < SHOP_COIN_COUNT; i++)
        {
            ref var c = ref _shopCoins[i];
            c.X = (float)rng.NextDouble();
            c.Y = (float)rng.NextDouble();
            c.Size = 6f + (float)rng.NextDouble() * 6f;
            c.Speed = 5f + (float)rng.NextDouble() * 10f;
            c.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            c.R = 255; c.G = 215; c.B = 0; c.A = 50;
        }

        for (int i = 0; i < SHOP_SHIMMER_COUNT; i++)
        {
            ref var s = ref _shopShimmer[i];
            s.X = (float)rng.NextDouble();
            s.Y = (float)rng.NextDouble();
            s.Size = 1.5f + (float)rng.NextDouble() * 2f;
            s.Speed = 3f + (float)rng.NextDouble() * 8f;
            s.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            s.R = 255; s.G = (byte)(200 + rng.Next(55)); s.B = (byte)(100 + rng.Next(50));
            s.A = 60;
        }

        for (int i = 0; i < SHOP_GEM_COUNT; i++)
        {
            ref var g = ref _shopGems[i];
            g.X = 0.2f + (float)rng.NextDouble() * 0.6f;
            g.Y = 0.2f + (float)rng.NextDouble() * 0.6f;
            g.Size = 30f + (float)rng.NextDouble() * 20f;
            g.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            g.Speed = 0.3f + (float)rng.NextDouble() * 0.3f;
            g.R = 0; g.G = 188; g.B = 212; g.A = 15;
        }
    }

    private static void RenderShopCoins(SKCanvas canvas, float w, float h, float t)
    {
        _p1.MaskFilter = _smallGlow;
        for (int i = 0; i < SHOP_COIN_COUNT; i++)
        {
            ref var c = ref _shopCoins[i];
            float x = c.X * w + MathF.Sin(t * 0.8f + c.Phase) * 10f;
            float y = h - ((t * c.Speed + c.Y * h) % (h + 30f));

            // Münz-Rotation (Oval-Squash)
            float rotPhase = MathF.Sin(t * 1.5f + c.Phase);
            float scaleX = 0.3f + MathF.Abs(rotPhase) * 0.7f;

            _p1.Color = new SKColor(c.R, c.G, c.B, c.A);
            canvas.DrawOval(x, y, c.Size * scaleX, c.Size, _p1);

            // Heller Rand
            _p1.Color = new SKColor(255, 240, 150, (byte)(c.A * 0.5f));
            canvas.DrawOval(x, y, c.Size * scaleX * 0.7f, c.Size * 0.7f, _p1);
        }
        _p1.MaskFilter = null;
    }

    private static void RenderShopShimmer(SKCanvas canvas, float w, float h, float t)
    {
        _p2.MaskFilter = _smallGlow;
        for (int i = 0; i < SHOP_SHIMMER_COUNT; i++)
        {
            ref var s = ref _shopShimmer[i];
            float x = s.X * w + MathF.Sin(t * 1.5f + s.Phase) * 6f;
            float y = h - ((t * s.Speed + s.Y * h) % (h + 15f));
            float blink = MathF.Sin(t * 4f + s.Phase) * 0.4f + 0.6f;
            _p2.Color = new SKColor(s.R, s.G, s.B, (byte)(s.A * blink));
            canvas.DrawCircle(x, y, s.Size, _p2);
        }
        _p2.MaskFilter = null;
    }

    private static void RenderShopGems(SKCanvas canvas, float w, float h, float t)
    {
        _p3.MaskFilter = _largeGlow;
        for (int i = 0; i < SHOP_GEM_COUNT; i++)
        {
            ref var g = ref _shopGems[i];
            float x = g.X * w + MathF.Sin(t * g.Speed + g.Phase) * 20f;
            float y = g.Y * h + MathF.Cos(t * g.Speed * 0.7f + g.Phase) * 15f;
            float pulse = MathF.Sin(t * 0.5f + g.Phase) * 0.2f + 0.8f;

            // Rauten-Form (Gem-Silhouette)
            using var path = new SKPath();
            float s = g.Size * pulse;
            path.MoveTo(x, y - s);
            path.LineTo(x + s * 0.6f, y);
            path.LineTo(x, y + s * 0.4f);
            path.LineTo(x - s * 0.6f, y);
            path.Close();

            _p3.Color = new SKColor(g.R, g.G, g.B, (byte)(g.A * pulse));
            canvas.DrawPath(path, _p3);
        }
        _p3.MaskFilter = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LEAGUE THEME: Trophäen + Sterne + Lichtstreifen
    // ═══════════════════════════════════════════════════════════════════════

    private static void InitLeague(Random rng)
    {
        for (int i = 0; i < LEAGUE_TROPHY_COUNT; i++)
        {
            ref var t = ref _leagueTrophies[i];
            t.X = (float)rng.NextDouble();
            t.Y = (float)rng.NextDouble();
            t.Size = 14f + (float)rng.NextDouble() * 10f;
            t.Speed = 4f + (float)rng.NextDouble() * 6f;
            t.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            t.A = 25;
        }

        for (int i = 0; i < LEAGUE_STAR_COUNT; i++)
        {
            ref var s = ref _leagueStars[i];
            s.X = (float)rng.NextDouble();
            s.Y = (float)rng.NextDouble();
            s.Size = 1.5f + (float)rng.NextDouble() * 2f;
            s.Speed = 8f + (float)rng.NextDouble() * 12f;
            s.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            s.R = rng.NextDouble() < 0.5 ? (byte)255 : (byte)0;
            s.G = rng.NextDouble() < 0.5 ? (byte)255 : (byte)188;
            s.B = rng.NextDouble() < 0.3 ? (byte)255 : (byte)212;
            s.A = 70;
        }

        for (int i = 0; i < LEAGUE_LIGHT_COUNT; i++)
        {
            ref var l = ref _leagueLights[i];
            l.X = (i + 0.5f) / LEAGUE_LIGHT_COUNT;
            l.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            l.Size = 3f;
            l.A = 20;
        }
    }

    private static void RenderLeagueTrophies(SKCanvas canvas, float w, float h, float t)
    {
        _p1.MaskFilter = null;
        for (int i = 0; i < LEAGUE_TROPHY_COUNT; i++)
        {
            ref var tr = ref _leagueTrophies[i];
            float x = tr.X * w + MathF.Sin(t * 0.3f + tr.Phase) * 8f;
            float y = h - ((t * tr.Speed + tr.Y * h) % (h + tr.Size * 3));
            float s = tr.Size;

            _p1.Color = new SKColor(0, 188, 212, tr.A);

            // Kelch-Form (vereinfacht)
            using var path = new SKPath();
            path.MoveTo(x - s * 0.5f, y - s * 0.3f);
            path.LineTo(x - s * 0.3f, y + s * 0.3f);
            path.LineTo(x + s * 0.3f, y + s * 0.3f);
            path.LineTo(x + s * 0.5f, y - s * 0.3f);
            path.Close();
            canvas.DrawPath(path, _p1);

            // Fuß
            canvas.DrawRect(x - s * 0.15f, y + s * 0.3f, s * 0.3f, s * 0.2f, _p1);
            canvas.DrawRect(x - s * 0.25f, y + s * 0.5f, s * 0.5f, s * 0.1f, _p1);
        }
    }

    private static void RenderLeagueStars(SKCanvas canvas, float w, float h, float t)
    {
        _p2.MaskFilter = _smallGlow;
        for (int i = 0; i < LEAGUE_STAR_COUNT; i++)
        {
            ref var s = ref _leagueStars[i];
            float x = s.X * w + MathF.Sin(t * 1f + s.Phase) * 6f;
            float y = h - ((t * s.Speed + s.Y * h) % (h + 15f));
            float pulse = MathF.Sin(t * 3.5f + s.Phase) * 0.35f + 0.65f;
            _p2.Color = new SKColor(s.R, s.G, s.B, (byte)(s.A * pulse));
            canvas.DrawCircle(x, y, s.Size, _p2);
        }
        _p2.MaskFilter = null;
    }

    private static void RenderLeagueLights(SKCanvas canvas, float w, float h, float t)
    {
        _p3.MaskFilter = _largeGlow;
        for (int i = 0; i < LEAGUE_LIGHT_COUNT; i++)
        {
            ref var l = ref _leagueLights[i];
            float x = l.X * w;
            float sweep = MathF.Sin(t * 0.5f + l.Phase) * w * 0.15f;

            // Vertikaler Lichtstreifen
            _p3.Color = new SKColor(0, 188, 212, (byte)(l.A * (MathF.Sin(t * 0.8f + l.Phase) * 0.3f + 0.7f)));
            canvas.DrawRect(x + sweep - 1.5f, 0, 3f, h, _p3);
        }
        _p3.MaskFilter = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BATTLEPASS THEME: XP-Orbs + Streifen + Badge-Silhouetten
    // ═══════════════════════════════════════════════════════════════════════

    private static void InitBattlePass(Random rng)
    {
        for (int i = 0; i < BP_ORB_COUNT; i++)
        {
            ref var o = ref _bpOrbs[i];
            o.X = (float)rng.NextDouble();
            o.Y = (float)rng.NextDouble();
            o.Size = 3f + (float)rng.NextDouble() * 4f;
            o.Speed = 6f + (float)rng.NextDouble() * 12f;
            o.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            // Lila oder Orange
            if (rng.NextDouble() < 0.5)
            { o.R = 156; o.G = 39; o.B = 176; } // Lila
            else
            { o.R = 255; o.G = 152; o.B = 0; } // Orange
            o.A = 60;
        }

        for (int i = 0; i < BP_STRIPE_COUNT; i++)
        {
            ref var s = ref _bpStripes[i];
            s.X = (float)rng.NextDouble();
            s.Y = (float)rng.NextDouble();
            s.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            s.Speed = 30f + (float)rng.NextDouble() * 40f;
            s.Size = 20f + (float)rng.NextDouble() * 30f; // Länge
            s.Extra1 = 2f + (float)rng.NextDouble() * 2f; // Breite
            s.A = 20;
        }

        for (int i = 0; i < BP_BADGE_COUNT; i++)
        {
            ref var b = ref _bpBadges[i];
            b.X = (float)rng.NextDouble();
            b.Y = (float)rng.NextDouble();
            b.Size = 15f + (float)rng.NextDouble() * 10f;
            b.Speed = 3f + (float)rng.NextDouble() * 5f;
            b.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            b.A = 15;
        }
    }

    private static void RenderBPOrbs(SKCanvas canvas, float w, float h, float t)
    {
        _p1.MaskFilter = _smallGlow;
        for (int i = 0; i < BP_ORB_COUNT; i++)
        {
            ref var o = ref _bpOrbs[i];
            float x = o.X * w + MathF.Sin(t * 1.2f + o.Phase) * 8f;
            float y = h - ((t * o.Speed + o.Y * h) % (h + 20f));
            float pulse = MathF.Sin(t * 2.5f + o.Phase) * 0.3f + 0.7f;
            _p1.Color = new SKColor(o.R, o.G, o.B, (byte)(o.A * pulse));
            canvas.DrawCircle(x, y, o.Size, _p1);
        }
        _p1.MaskFilter = null;
    }

    private static void RenderBPStripes(SKCanvas canvas, float w, float h, float t)
    {
        _p2.MaskFilter = null;
        for (int i = 0; i < BP_STRIPE_COUNT; i++)
        {
            ref var s = ref _bpStripes[i];
            float x = s.X * w;
            float y = h - ((t * s.Speed + s.Y * h) % (h + s.Size * 2));

            // Diagonaler Streifen
            canvas.Save();
            canvas.Translate(x, y);
            canvas.RotateDegrees(-30f);
            _p2.Color = new SKColor(255, 152, 0, s.A);
            canvas.DrawRect(-s.Extra1 * 0.5f, -s.Size * 0.5f, s.Extra1, s.Size, _p2);
            canvas.Restore();
        }
    }

    private static void RenderBPBadges(SKCanvas canvas, float w, float h, float t)
    {
        _p3.MaskFilter = _mediumGlow;
        for (int i = 0; i < BP_BADGE_COUNT; i++)
        {
            ref var b = ref _bpBadges[i];
            float x = b.X * w + MathF.Sin(t * 0.4f + b.Phase) * 12f;
            float y = h - ((t * b.Speed + b.Y * h) % (h + b.Size * 2));
            float pulse = MathF.Sin(t * 0.8f + b.Phase) * 0.2f + 0.8f;
            float s = b.Size * pulse;

            // Schild-Form
            using var path = new SKPath();
            path.MoveTo(x, y - s);
            path.LineTo(x + s * 0.6f, y - s * 0.5f);
            path.LineTo(x + s * 0.6f, y + s * 0.2f);
            path.LineTo(x, y + s * 0.6f);
            path.LineTo(x - s * 0.6f, y + s * 0.2f);
            path.LineTo(x - s * 0.6f, y - s * 0.5f);
            path.Close();

            _p3.Color = new SKColor(156, 39, 176, (byte)(b.A * pulse));
            canvas.DrawPath(path, _p3);
        }
        _p3.MaskFilter = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VICTORY THEME: Confetti + Fireworks + Gold-Funken
    // ═══════════════════════════════════════════════════════════════════════

    private static void InitVictory(Random rng)
    {
        for (int i = 0; i < VICTORY_CONFETTI_COUNT; i++)
        {
            ref var c = ref _victoryConfetti[i];
            c.X = (float)rng.NextDouble();
            c.Y = (float)rng.NextDouble();
            c.Size = 4f + (float)rng.NextDouble() * 4f;
            c.Speed = 15f + (float)rng.NextDouble() * 20f;
            c.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            int colorIdx = rng.Next(ConfettiColors.Length);
            var color = ConfettiColors[colorIdx];
            c.R = color.Red; c.G = color.Green; c.B = color.Blue;
            c.A = (byte)(60 + rng.Next(30));
            c.Extra1 = (float)rng.NextDouble() * 3f; // Rotation speed
            c.Extra2 = (float)rng.NextDouble() * 20f - 10f; // Drift
        }

        for (int i = 0; i < VICTORY_FIREWORK_COUNT; i++)
        {
            ref var f = ref _victoryFireworks[i];
            f.X = 0.1f + (float)rng.NextDouble() * 0.8f;
            f.Y = 0.1f + (float)rng.NextDouble() * 0.5f;
            f.Phase = (float)rng.NextDouble() * 4f; // Stagger
            f.Size = 30f + (float)rng.NextDouble() * 20f;
            f.Speed = 2f + (float)rng.NextDouble() * 2f; // Zyklus-Dauer
            int colorIdx = rng.Next(ConfettiColors.Length);
            var color = ConfettiColors[colorIdx];
            f.R = color.Red; f.G = color.Green; f.B = color.Blue;
            f.A = 70;
        }

        for (int i = 0; i < VICTORY_SPARK_COUNT; i++)
        {
            ref var s = ref _victorySparks[i];
            s.X = (float)rng.NextDouble();
            s.Y = (float)rng.NextDouble();
            s.Size = 2f + (float)rng.NextDouble() * 3f;
            s.Speed = 12f + (float)rng.NextDouble() * 18f;
            s.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            s.R = 255; s.G = 215; s.B = 0; s.A = 80;
        }
    }

    private static void RenderVictoryConfetti(SKCanvas canvas, float w, float h, float t)
    {
        _p1.MaskFilter = null;
        for (int i = 0; i < VICTORY_CONFETTI_COUNT; i++)
        {
            ref var c = ref _victoryConfetti[i];
            float x = c.X * w + MathF.Sin(t * 0.8f + c.Phase) * c.Extra2;
            float y = ((t * c.Speed + c.Y * h) % (h + 20f)) - 10f;
            float rot = t * c.Extra1 + c.Phase;

            canvas.Save();
            canvas.Translate(x, y);
            canvas.RotateRadians(rot);

            _p1.Color = new SKColor(c.R, c.G, c.B, c.A);
            canvas.DrawRect(-c.Size * 0.5f, -c.Size * 0.25f, c.Size, c.Size * 0.5f, _p1);

            canvas.Restore();
        }
    }

    private static void RenderVictoryFireworks(SKCanvas canvas, float w, float h, float t)
    {
        _p2.MaskFilter = _mediumGlow;
        for (int i = 0; i < VICTORY_FIREWORK_COUNT; i++)
        {
            ref var f = ref _victoryFireworks[i];
            float cycle = (t + f.Phase) % f.Speed;
            float progress = cycle / f.Speed;

            // Nur in der "Explosions-Phase" sichtbar (30-80% des Zyklus)
            if (progress < 0.3f || progress > 0.8f) continue;

            float burstProgress = (progress - 0.3f) / 0.5f; // 0→1
            float x = f.X * w;
            float y = f.Y * h;
            float radius = f.Size * burstProgress;
            byte alpha = (byte)(f.A * (1f - burstProgress));

            // 8 radiale Partikel
            for (int j = 0; j < 8; j++)
            {
                float angle = j * MathF.PI * 2f / 8f;
                float px = x + MathF.Cos(angle) * radius;
                float py = y + MathF.Sin(angle) * radius;

                _p2.Color = new SKColor(f.R, f.G, f.B, alpha);
                canvas.DrawCircle(px, py, 3f * (1f - burstProgress * 0.5f), _p2);
            }

            // Zentrale Glow
            _p2.Color = new SKColor(255, 255, 255, (byte)(alpha * 0.3f));
            canvas.DrawCircle(x, y, radius * 0.3f, _p2);
        }
        _p2.MaskFilter = null;
    }

    private static void RenderVictorySparks(SKCanvas canvas, float w, float h, float t)
    {
        _p3.MaskFilter = _smallGlow;
        for (int i = 0; i < VICTORY_SPARK_COUNT; i++)
        {
            ref var s = ref _victorySparks[i];
            float x = s.X * w + MathF.Sin(t * 1.5f + s.Phase) * 10f;
            float y = h - ((t * s.Speed + s.Y * h) % (h + 20f));
            float pulse = MathF.Sin(t * 4f + s.Phase) * 0.3f + 0.7f;
            _p3.Color = new SKColor(s.R, s.G, s.B, (byte)(s.A * pulse));
            canvas.DrawCircle(x, y, s.Size, _p3);
        }
        _p3.MaskFilter = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LUCKYSPIN THEME: Regenbogen-Orbit + Glitzer + Lichtstreifen
    // ═══════════════════════════════════════════════════════════════════════

    private static void InitLuckySpin(Random rng)
    {
        // Regenbogen-Punkte in Orbit-Rotation (mehr + größer + heller + breiter verteilt)
        for (int i = 0; i < SPIN_RAINBOW_COUNT; i++)
        {
            ref var r = ref _spinRainbow[i];
            r.Phase = i * MathF.PI * 2f / SPIN_RAINBOW_COUNT;
            r.Size = 4f + (float)rng.NextDouble() * 4f;
            r.Speed = 0.3f + (float)rng.NextDouble() * 0.4f;
            r.Extra1 = 0.15f + (float)rng.NextDouble() * 0.35f; // Breitere Orbit-Radien
            r.Extra2 = (float)rng.NextDouble();

            // Regenbogen-HSV
            float hue = i * 360f / SPIN_RAINBOW_COUNT;
            HsvToRgb(hue, 0.8f, 1f, out r.R, out r.G, out r.B);
            r.A = 100;
        }

        // Glitzer (mehr + größer + deutlich heller)
        for (int i = 0; i < SPIN_GLITTER_COUNT; i++)
        {
            ref var g = ref _spinGlitter[i];
            g.X = (float)rng.NextDouble();
            g.Y = (float)rng.NextDouble();
            g.Size = 2.5f + (float)rng.NextDouble() * 3f;
            g.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            g.Speed = (float)rng.NextDouble() * 3f;
            g.R = 255; g.G = 255; g.B = 255; g.A = 130;
        }

        // Lichtstreifen (mehr + stärker)
        for (int i = 0; i < SPIN_LIGHT_COUNT; i++)
        {
            ref var l = ref _spinLights[i];
            l.Phase = i * MathF.PI * 2f / SPIN_LIGHT_COUNT;
            l.Size = 2f;
            l.Speed = 0.5f + (float)rng.NextDouble() * 0.3f;
            l.A = 30;
        }
    }

    /// <summary>Langsam rotierender Regenbogen-Ring in der Bildmitte (LuckySpin).</summary>
    private static void RenderSpinRainbowRing(SKCanvas canvas, float w, float h, float t)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float radius = MathF.Min(w, h) * 0.35f;
        float pulse = MathF.Sin(t * 0.4f) * 0.1f + 0.9f;

        _strokePaint.StrokeWidth = 4f;
        _strokePaint.MaskFilter = _largeGlow;
        _strokePaint.Style = SKPaintStyle.Stroke;

        // 12 Segmente mit Regenbogenfarben, rotierend
        int segments = 12;
        float sweepAngle = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float hue = (i * 360f / segments + t * 20f) % 360f;
            HsvToRgb(hue, 0.7f, 1f, out byte r, out byte g, out byte b);
            byte alpha = (byte)(25 * pulse);

            _strokePaint.Color = new SKColor(r, g, b, alpha);

            float startAngle = i * sweepAngle + t * 20f;
            using var path = new SKPath();
            path.AddArc(new SKRect(cx - radius * pulse, cy - radius * pulse * 0.6f,
                                   cx + radius * pulse, cy + radius * pulse * 0.6f),
                        startAngle, sweepAngle - 2f);
            canvas.DrawPath(path, _strokePaint);
        }

        _strokePaint.MaskFilter = null;
        _strokePaint.Style = SKPaintStyle.Stroke;
    }

    private static void RenderSpinRainbow(SKCanvas canvas, float w, float h, float t)
    {
        _p1.MaskFilter = _smallGlow;
        float cx = w * 0.5f, cy = h * 0.5f;
        float maxR = MathF.Min(w, h) * 0.4f;

        for (int i = 0; i < SPIN_RAINBOW_COUNT; i++)
        {
            ref var r = ref _spinRainbow[i];
            float angle = t * r.Speed + r.Phase;
            float radius = maxR * r.Extra1 + MathF.Sin(t * 0.5f + r.Phase) * 10f;

            float x = cx + MathF.Cos(angle) * radius;
            float y = cy + MathF.Sin(angle) * radius * 0.6f + (r.Extra2 - 0.5f) * h * 0.4f;

            float pulse = MathF.Sin(t * 2f + r.Phase) * 0.2f + 0.8f;
            _p1.Color = new SKColor(r.R, r.G, r.B, (byte)(r.A * pulse));
            canvas.DrawCircle(x, y, r.Size, _p1);
        }
        _p1.MaskFilter = null;
    }

    private static void RenderSpinGlitter(SKCanvas canvas, float w, float h, float t)
    {
        _p2.MaskFilter = _smallGlow;
        for (int i = 0; i < SPIN_GLITTER_COUNT; i++)
        {
            ref var g = ref _spinGlitter[i];
            float x = g.X * w;
            float y = g.Y * h;

            // Blink-Effekt (volle Sinus-Kurve nutzen, weniger unsichtbare Zeit)
            float blink = MathF.Sin(t * 5f + g.Phase);
            if (blink < 0f) continue; // Nur negative Hälfte unsichtbar
            float alpha = blink;

            _p2.Color = new SKColor(g.R, g.G, g.B, (byte)(g.A * alpha));

            // Stern-Form (Kreuz)
            float s = g.Size * (0.8f + alpha * 0.2f);
            canvas.DrawRect(x - s * 0.15f, y - s, s * 0.3f, s * 2f, _p2);
            canvas.DrawRect(x - s, y - s * 0.15f, s * 2f, s * 0.3f, _p2);
        }
        _p2.MaskFilter = null;
    }

    private static void RenderSpinLights(SKCanvas canvas, float w, float h, float t)
    {
        _p3.MaskFilter = _largeGlow;
        float cx = w * 0.5f, cy = h * 0.5f;
        float maxR = MathF.Min(w, h) * 0.45f;

        for (int i = 0; i < SPIN_LIGHT_COUNT; i++)
        {
            ref var l = ref _spinLights[i];
            float angle = t * l.Speed + l.Phase;
            float x = cx + MathF.Cos(angle) * maxR;
            float y = cy + MathF.Sin(angle) * maxR * 0.5f;

            // Langer Lichtstreifen tangential zur Rotation (stärker + länger)
            float tangentX = -MathF.Sin(angle);
            float tangentY = MathF.Cos(angle) * 0.5f;
            float len = 60f;

            _strokePaint.Color = new SKColor(255, 255, 255, l.A);
            _strokePaint.StrokeWidth = 3f;
            _strokePaint.MaskFilter = _largeGlow;
            canvas.DrawLine(
                x - tangentX * len, y - tangentY * len,
                x + tangentX * len, y + tangentY * len,
                _strokePaint);
            _strokePaint.MaskFilter = null;
        }
        _p3.MaskFilter = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFS-METHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>HSV nach RGB konvertieren (H: 0-360, S/V: 0-1)</summary>
    private static void HsvToRgb(float h, float s, float v, out byte r, out byte g, out byte b)
    {
        float c = v * s;
        float x = c * (1f - MathF.Abs(h / 60f % 2f - 1f));
        float m = v - c;
        float rf, gf, bf;

        if (h < 60) { rf = c; gf = x; bf = 0; }
        else if (h < 120) { rf = x; gf = c; bf = 0; }
        else if (h < 180) { rf = 0; gf = c; bf = x; }
        else if (h < 240) { rf = 0; gf = x; bf = c; }
        else if (h < 300) { rf = x; gf = 0; bf = c; }
        else { rf = c; gf = 0; bf = x; }

        r = (byte)((rf + m) * 255);
        g = (byte)((gf + m) * 255);
        b = (byte)((bf + m) * 255);
    }
}
