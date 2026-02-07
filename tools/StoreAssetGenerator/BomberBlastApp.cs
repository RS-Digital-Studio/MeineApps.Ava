using SkiaSharp;
using static StoreAssetGenerator.Gfx;

namespace StoreAssetGenerator;

static class BomberBlastApp
{
    static readonly SKColor Red1 = SKColor.Parse("#B71C1C");
    static readonly SKColor Red2 = SKColor.Parse("#E53935");
    static readonly SKColor RedAccent = SKColor.Parse("#FF5252");
    static readonly SKColor Neon = SKColor.Parse("#00E5FF");

    public static AppDef Create() => new(
        "BomberBlast", RedAccent,
        DrawIcon, DrawFeature,
        [
            ("BOMBER\nBLAST!", DrawMainMenu),
            ("Sprengstoff-\nAction!", DrawGameplay),
            ("50 Level\nStory Mode!", DrawLevelSelect),
            ("Einstellungen\nanpassen!", DrawSettings),
            ("Highscores &\nRekorde!", DrawHighScores),
            ("Hilfe &\nAnleitung!", DrawHelp),
        ],
        [
            ("BOMBER\nBLAST!", DrawMainMenu),
            ("Sprengstoff-\nAction!", DrawGameplay),
            ("50 Level\nStory Mode!", DrawLevelSelect),
            ("Highscores &\nRekorde!", DrawHighScores),
        ]
    );

    static void DrawIcon(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var c = surface.Canvas;
        IconBg(c, 0, 0, size, Red1, Red2);
        float s = size / 512f, cx = size / 2f, cy = size / 2f + 20 * s;

        // Bomb body
        Circle(c, cx, cy, 120 * s, SKColor.Parse("#212121"));
        Circle(c, cx, cy, 110 * s, SKColor.Parse("#333333"));

        // Glint/highlight
        using var glintP = new SKPaint { Color = SKColor.Parse("#555555"), IsAntialias = true };
        c.DrawOval(new SKRect(cx - 60 * s, cy - 80 * s, cx - 15 * s, cy - 40 * s), glintP);

        // Fuse stem (top)
        using var fuseBaseP = new SKPaint { Color = SKColor.Parse("#795548"), IsAntialias = true, StrokeWidth = 12 * s, StrokeCap = SKStrokeCap.Round };
        c.DrawLine(cx + 10 * s, cy - 105 * s, cx + 30 * s, cy - 140 * s, fuseBaseP);

        // Fuse string (curved)
        using var fuseP = new SKPaint { Color = SKColor.Parse("#8D6E63"), IsAntialias = true, StrokeWidth = 5 * s, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke };
        using var fusePath = new SKPath();
        fusePath.MoveTo(cx + 30 * s, cy - 140 * s);
        fusePath.CubicTo(cx + 50 * s, cy - 160 * s, cx + 70 * s, cy - 155 * s, cx + 60 * s, cy - 180 * s);
        c.DrawPath(fusePath, fuseP);

        // Spark/flame at fuse tip
        Circle(c, cx + 60 * s, cy - 185 * s, 18 * s, SKColor.Parse("#FF9800"));
        Circle(c, cx + 60 * s, cy - 185 * s, 12 * s, SKColor.Parse("#FFEB3B"));
        Circle(c, cx + 60 * s, cy - 185 * s, 6 * s, SKColors.White);

        // Spark particles
        using var sparkP = new SKPaint { Color = SKColor.Parse("#FFD54F"), IsAntialias = true };
        Circle(c, cx + 45 * s, cy - 200 * s, 4 * s, SKColor.Parse("#FFD54F"));
        Circle(c, cx + 75 * s, cy - 195 * s, 3 * s, SKColor.Parse("#FFD54F"));
        Circle(c, cx + 55 * s, cy - 210 * s, 3 * s, SKColor.Parse("#FFA726"));
        Circle(c, cx + 70 * s, cy - 175 * s, 3 * s, SKColor.Parse("#FFCC02"));

        SavePng(surface, "icon_512.png");
        Console.WriteLine("  BomberBlast Icon generiert");
    }

    static void DrawFeature()
    {
        const int w = 1024, h = 500;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var c = surface.Canvas;
        FeatureGraphicBase(c, w, h, RedAccent, (c2, x, y, sz) => {
            IconBg(c2, x, y, sz, Red1, Red2);
            float s2 = sz / 512f;
            Circle(c2, x + sz / 2, y + sz / 2 + 10 * s2, sz * 0.24f, SKColor.Parse("#333333"));
            // Fuse
            using var fp = new SKPaint { Color = SKColor.Parse("#8D6E63"), IsAntialias = true, StrokeWidth = 3, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke };
            using var fPath = new SKPath();
            fPath.MoveTo(x + sz / 2 + 10 * s2, y + sz / 2 - sz * 0.2f);
            fPath.CubicTo(x + sz / 2 + 25 * s2, y + sz / 2 - sz * 0.3f, x + sz / 2 + 35 * s2, y + sz / 2 - sz * 0.28f, x + sz / 2 + 30 * s2, y + sz / 2 - sz * 0.35f);
            c2.DrawPath(fPath, fp);
            Circle(c2, x + sz / 2 + 30 * s2, y + sz / 2 - sz * 0.37f, 8, SKColor.Parse("#FFEB3B"));
        },
        "Bomber", "Blast",
        "Action • Puzzle • 50 Level • Arcade",
        [("💣", RedAccent, w - 200, 60, 80), ("🔥", Warning, w - 100, 160, 70), ("⚡", Neon, w - 190, 280, 65), ("🏆", Gold, w - 80, 340, 60)]);
        SavePng(surface, "feature_graphic.png");
        Console.WriteLine("  BomberBlast Feature Graphic generiert");
    }

    static void DrawMainMenu(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;

        // Dark atmospheric background
        using var bgP = new SKPaint { IsAntialias = true };
        using var bgS = SKShader.CreateLinearGradient(new SKPoint(x, y), new SKPoint(x, y + h),
            [SKColor.Parse("#1a0a0a"), Bg], SKShaderTileMode.Clamp);
        bgP.Shader = bgS;
        c.DrawRect(b, bgP);

        // Decorative particles
        using var particleP = new SKPaint { Color = RedAccent.WithAlpha(30), IsAntialias = true };
        c.DrawCircle(x + w * 0.2f, y + h * 0.3f, 80, particleP);
        c.DrawCircle(x + w * 0.8f, y + h * 0.5f, 60, particleP);
        c.DrawCircle(x + w * 0.5f, y + h * 0.7f, 100, particleP);

        // Title
        float tY = y + h * 0.15f;
        using var titleP = new SKPaint
        {
            Color = TextSecondary, IsAntialias = true, TextSize = 52,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };
        c.DrawText("BOMBER", x + w / 2, tY, titleP);
        titleP.Color = RedAccent;
        c.DrawText("BLAST", x + w / 2, tY + 60, titleP);

        // Version
        TextC(c, "v2.0.0", x + w / 2, tY + 85, 12, TextMuted);

        // Menu buttons
        float btnY = tY + 120;
        float btnW = w * 0.7f;
        float btnX = x + (w - btnW) / 2;

        (string label, SKColor color)[] buttons = [
            ("🎮  Story Mode", Primary),
            ("▶  Fortsetzen", Success),
            ("🎲  Arcade Modus", Warning),
            ("⚡  Quick Play", Cyan),
        ];

        foreach (var (label, color) in buttons)
        {
            if (btnY + 50 > y + h - 120) break;
            RoundRect(c, btnX, btnY, btnW, 46, 12, color.WithAlpha(40));
            using var borderP = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
            c.DrawRoundRect(new SKRect(btnX, btnY, btnX + btnW, btnY + 46), 12, 12, borderP);
            TextC(c, label, x + w / 2, btnY + 31, 18, color);
            btnY += 56;
        }

        // Secondary buttons row
        float secY = btnY + 15;
        float secW = (btnW - 16) / 3;
        string[] secLabels = ["🏆 Highscores", "❓ Hilfe", "⚙ Settings"];
        for (int i = 0; i < 3; i++)
        {
            float sx = btnX + i * (secW + 8);
            RoundRect(c, sx, secY, secW, 38, 8, Surface);
            TextC(c, secLabels[i], sx + secW / 2, secY + 26, 12, TextSecondary);
        }
    }

    static void DrawGameplay(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;

        // Game area background (dark)
        RoundRect(c, x, y, w, h, 0, SKColor.Parse("#0a0a0a"));

        // HUD on right side
        float hudW = 100;
        float gameW = w - hudW;
        RoundRect(c, x + gameW, y, hudW, h, 0, SKColor.Parse("#111122"));
        using var hudBorder = new SKPaint { Color = Primary.WithAlpha(60), IsAntialias = true, StrokeWidth = 1 };
        c.DrawLine(x + gameW, y, x + gameW, y + h, hudBorder);

        // HUD items
        float hy = y + 20;
        TextC(c, "TIME", x + gameW + hudW / 2, hy, 10, TextMuted);
        TextC(c, "2:45", x + gameW + hudW / 2, hy + 18, 16, Warning);
        hy += 45;
        TextC(c, "SCORE", x + gameW + hudW / 2, hy, 10, TextMuted);
        TextC(c, "15.200", x + gameW + hudW / 2, hy + 18, 14, TextPrimary);
        hy += 45;
        TextC(c, "LIVES", x + gameW + hudW / 2, hy, 10, TextMuted);
        TextC(c, "❤❤❤", x + gameW + hudW / 2, hy + 20, 14);
        hy += 45;
        TextC(c, "BOMBS", x + gameW + hudW / 2, hy, 10, TextMuted);
        TextC(c, "3", x + gameW + hudW / 2, hy + 18, 16, Primary);
        hy += 45;
        TextC(c, "FIRE", x + gameW + hudW / 2, hy, 10, TextMuted);
        TextC(c, "4", x + gameW + hudW / 2, hy + 18, 16, RedAccent);

        // PowerUps
        hy += 45;
        TextC(c, "POWER", x + gameW + hudW / 2, hy, 10, TextMuted);
        Circle(c, x + gameW + 25, hy + 22, 10, Success.WithAlpha(80));
        TextC(c, "⚡", x + gameW + 25, hy + 27, 10);
        Circle(c, x + gameW + 55, hy + 22, 10, Cyan.WithAlpha(80));
        TextC(c, "👻", x + gameW + 55, hy + 27, 10);

        // Game grid (15x10 ish, simplified)
        int cols = 13, rows = 10;
        float cellSize = Math.Min(gameW / cols, (h - 20) / rows);
        float gridStartX = x + (gameW - cols * cellSize) / 2;
        float gridStartY = y + (h - rows * cellSize) / 2;

        // Floor tiles
        SKColor floorLight = SKColor.Parse("#1E3050");
        SKColor floorDark = SKColor.Parse("#172640");
        for (int r = 0; r < rows; r++)
            for (int col = 0; col < cols; col++)
            {
                float cx2 = gridStartX + col * cellSize;
                float cy2 = gridStartY + r * cellSize;
                c.DrawRect(cx2, cy2, cellSize, cellSize,
                    new SKPaint { Color = (r + col) % 2 == 0 ? floorLight : floorDark });
            }

        // Walls (border + pillars at odd positions)
        SKColor wallColor = SKColor.Parse("#546E7A");
        SKColor wallHighlight = SKColor.Parse("#78909C");
        for (int r = 0; r < rows; r++)
            for (int col = 0; col < cols; col++)
            {
                bool isWall = r == 0 || r == rows - 1 || col == 0 || col == cols - 1;
                bool isPillar = r > 0 && r < rows - 1 && col > 0 && col < cols - 1 && r % 2 == 0 && col % 2 == 0;
                if (isWall || isPillar)
                {
                    float cx2 = gridStartX + col * cellSize;
                    float cy2 = gridStartY + r * cellSize;
                    RoundRect(c, cx2 + 1, cy2 + 1, cellSize - 2, cellSize - 2, 2, wallColor);
                    RoundRect(c, cx2 + 2, cy2 + 2, cellSize * 0.4f, cellSize * 0.3f, 1, wallHighlight.WithAlpha(60));
                }
            }

        // Destructible blocks
        SKColor blockColor = SKColor.Parse("#8D6E63");
        SKColor blockLine = SKColor.Parse("#6D4C41");
        int[,] blocks = { { 1, 3 }, { 1, 5 }, { 2, 1 }, { 3, 3 }, { 3, 5 }, { 3, 7 }, { 5, 1 }, { 5, 3 }, { 5, 9 }, { 7, 5 }, { 7, 7 }, { 7, 9 } };
        for (int i = 0; i < blocks.GetLength(0); i++)
        {
            int r = blocks[i, 0], col2 = blocks[i, 1];
            if (r < rows - 1 && col2 < cols - 1)
            {
                float bx = gridStartX + col2 * cellSize;
                float by = gridStartY + r * cellSize;
                RoundRect(c, bx + 1, by + 1, cellSize - 2, cellSize - 2, 2, blockColor);
                // Brick lines
                using var brickP = new SKPaint { Color = blockLine, IsAntialias = true, StrokeWidth = 1 };
                c.DrawLine(bx + 2, by + cellSize / 2, bx + cellSize - 2, by + cellSize / 2, brickP);
                c.DrawLine(bx + cellSize / 2, by + 2, bx + cellSize / 2, by + cellSize / 2, brickP);
                c.DrawLine(bx + cellSize / 3, by + cellSize / 2, bx + cellSize / 3, by + cellSize - 2, brickP);
            }
        }

        // Player (position 1,1)
        float playerX = gridStartX + 1 * cellSize + cellSize / 2;
        float playerY = gridStartY + 1 * cellSize + cellSize / 2;
        Circle(c, playerX, playerY, cellSize * 0.35f, SKColor.Parse("#42A5F5"));
        // Helmet
        using var helmetP = new SKPaint { Color = SKColor.Parse("#1565C0"), IsAntialias = true };
        c.DrawArc(new SKRect(playerX - cellSize * 0.3f, playerY - cellSize * 0.4f, playerX + cellSize * 0.3f, playerY), 180, 180, true, helmetP);
        // Eyes
        Circle(c, playerX - 5, playerY - 2, 3, SKColors.White);
        Circle(c, playerX + 5, playerY - 2, 3, SKColors.White);
        Circle(c, playerX - 4, playerY - 2, 1.5f, SKColor.Parse("#212121"));
        Circle(c, playerX + 6, playerY - 2, 1.5f, SKColor.Parse("#212121"));

        // Enemies
        (int r, int col, SKColor color)[] enemies = [(3, 9, SKColor.Parse("#EF5350")), (7, 3, SKColor.Parse("#AB47BC")), (5, 7, SKColor.Parse("#FF7043"))];
        foreach (var (er, ec, eColor) in enemies)
        {
            if (er < rows - 1 && ec < cols - 1)
            {
                float ex = gridStartX + ec * cellSize + cellSize / 2;
                float ey = gridStartY + er * cellSize + cellSize / 2;
                // Oval body
                using var ep = new SKPaint { Color = eColor, IsAntialias = true };
                c.DrawOval(new SKRect(ex - cellSize * 0.3f, ey - cellSize * 0.25f, ex + cellSize * 0.3f, ey + cellSize * 0.3f), ep);
                // Eyes
                Circle(c, ex - 4, ey - 3, 3, SKColors.White);
                Circle(c, ex + 4, ey - 3, 3, SKColors.White);
                Circle(c, ex - 3, ey - 3, 1.5f, SKColor.Parse("#212121"));
                Circle(c, ex + 5, ey - 3, 1.5f, SKColor.Parse("#212121"));
                // Angry eyebrows
                using var browP = new SKPaint { Color = SKColor.Parse("#212121"), IsAntialias = true, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round };
                c.DrawLine(ex - 7, ey - 8, ex - 2, ey - 6, browP);
                c.DrawLine(ex + 7, ey - 8, ex + 2, ey - 6, browP);
            }
        }

        // Bomb (position 2,3)
        float bombX = gridStartX + 3 * cellSize + cellSize / 2;
        float bombY = gridStartY + 2 * cellSize + cellSize / 2 + 2;
        Circle(c, bombX, bombY, cellSize * 0.3f, SKColor.Parse("#333333"));
        Circle(c, bombX - 3, bombY - 5, cellSize * 0.08f, SKColor.Parse("#666666")); // Glint
        // Fuse
        using var bombFuseP = new SKPaint { Color = SKColor.Parse("#8D6E63"), IsAntialias = true, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round };
        c.DrawLine(bombX + 3, bombY - cellSize * 0.28f, bombX + 8, bombY - cellSize * 0.42f, bombFuseP);
        Circle(c, bombX + 8, bombY - cellSize * 0.44f, 4, SKColor.Parse("#FFD54F")); // Spark

        // Explosion (position 5,5)
        float expX = gridStartX + 5 * cellSize + cellSize / 2;
        float expY = gridStartY + 5 * cellSize + cellSize / 2;
        // Cross explosion
        using var expOuter = new SKPaint { Color = RedAccent.WithAlpha(100), IsAntialias = true };
        using var expInner = new SKPaint { Color = Warning.WithAlpha(180), IsAntialias = true };
        using var expCore = new SKPaint { Color = SKColor.Parse("#FFEB3B"), IsAntialias = true };
        // Horizontal
        c.DrawRect(expX - cellSize * 2, expY - cellSize * 0.3f, cellSize * 4, cellSize * 0.6f, expOuter);
        c.DrawRect(expX - cellSize * 1.8f, expY - cellSize * 0.2f, cellSize * 3.6f, cellSize * 0.4f, expInner);
        // Vertical
        c.DrawRect(expX - cellSize * 0.3f, expY - cellSize * 2, cellSize * 0.6f, cellSize * 4, expOuter);
        c.DrawRect(expX - cellSize * 0.2f, expY - cellSize * 1.8f, cellSize * 0.4f, cellSize * 3.6f, expInner);
        // Center
        Circle(c, expX, expY, cellSize * 0.35f, SKColor.Parse("#FFEB3B"));

        // PowerUp (position 7,1)
        float puX = gridStartX + 1 * cellSize + cellSize / 2;
        float puY = gridStartY + 7 * cellSize + cellSize / 2;
        Circle(c, puX, puY, cellSize * 0.28f, Success);
        TextC(c, "🔥", puX, puY + 4, 12);

        // Exit door (bottom right area)
        float exitX = gridStartX + (cols - 2) * cellSize;
        float exitY = gridStartY + (rows - 2) * cellSize;
        RoundRect(c, exitX + 2, exitY + 2, cellSize - 4, cellSize - 4, 3, SKColor.Parse("#2E7D32"));
        TextC(c, "🚪", exitX + cellSize / 2, exitY + cellSize / 2 + 4, 14);
    }

    static void DrawLevelSelect(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        Text(c, "◀", x + 28, hY + 35, 22, TextPrimary);
        TextC(c, "🎮  Level auswählen", x + w / 2, hY + 35, 20, TextPrimary);

        // Level grid
        float gY = hY + 75;
        int levelsPerRow = 5;
        float cellW = (w - 48) / levelsPerRow;
        float cellH = 72;

        // Stars progress
        Text(c, "⭐ 42 / 150 Sterne", x + 24, gY - 8, 13, Gold);
        Progress(c, x + 200, gY - 12, w - 230, 8, 42f / 150f, Gold);

        for (int i = 0; i < 25; i++)
        {
            int col = i % levelsPerRow;
            int row = i / levelsPerRow;
            float cx2 = x + 20 + col * cellW;
            float cy2 = gY + 10 + row * cellH;

            if (cy2 + cellH > y + h - 30) break;

            bool completed = i < 12;
            bool current = i == 12;
            bool locked = i > 12;

            var bg = current ? RedAccent.WithAlpha(60) : completed ? Surface : Card.WithAlpha(60);
            RoundRect(c, cx2, cy2, cellW - 6, cellH - 6, 10, bg);

            if (current)
            {
                using var bP = new SKPaint { Color = RedAccent, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                c.DrawRoundRect(new SKRect(cx2, cy2, cx2 + cellW - 6, cy2 + cellH - 6), 10, 10, bP);
            }

            if (locked)
            {
                TextC(c, "🔒", cx2 + (cellW - 6) / 2, cy2 + 35, 18);
            }
            else
            {
                TextC(c, $"{i + 1}", cx2 + (cellW - 6) / 2, cy2 + 30, 22, current ? RedAccent : TextPrimary);

                if (completed)
                {
                    // Stars
                    int stars = i < 5 ? 3 : i < 9 ? 2 : 1;
                    string starStr = new string('⭐', stars) + new string('☆', 3 - stars);
                    TextC(c, starStr, cx2 + (cellW - 6) / 2, cy2 + 52, 9);
                }
            }
        }
    }

    static void DrawSettings(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        Text(c, "◀", x + 28, hY + 35, 22, TextPrimary);
        TextC(c, "⚙  Einstellungen", x + w / 2, hY + 35, 20, TextPrimary);

        // Controls section
        float sY = hY + 70;
        Text(c, "🎮  Steuerung", x + 24, sY, 17, TextPrimary, true);
        float cY = sY + 16;
        string[] controls = ["Joystick", "Swipe", "D-Pad"];
        for (int i = 0; i < 3; i++)
        {
            float cx2 = x + 20 + i * ((w - 48) / 3f);
            float cw = (w - 48) / 3f - 6;
            var bg = i == 0 ? RedAccent.WithAlpha(40) : Surface;
            RoundRect(c, cx2, cY, cw, 36, 10, bg);
            if (i == 0)
            {
                using var bP = new SKPaint { Color = RedAccent, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                c.DrawRoundRect(new SKRect(cx2, cY, cx2 + cw, cY + 36), 10, 10, bP);
            }
            TextC(c, controls[i], cx2 + cw / 2, cY + 24, 14, i == 0 ? RedAccent : TextMuted);
        }

        // Slider settings
        float slY = cY + 50;
        (string label, float val)[] sliders = [("Joystick-Größe", 0.6f), ("Joystick-Transparenz", 0.8f)];
        foreach (var (label, val) in sliders)
        {
            Text(c, label, x + 24, slY + 14, 14, TextSecondary);
            Progress(c, x + 220, slY + 8, w - 260, 10, val, RedAccent);
            Circle(c, x + 220 + (w - 260) * val, slY + 13, 8, RedAccent);
            slY += 38;
        }

        // Sound
        Text(c, "🔊  Sound", x + 24, slY + 10, 17, TextPrimary, true);
        slY += 28;
        (string name, bool on)[] toggles = [("Sound-Effekte", true), ("Musik", true), ("Haptik", false)];
        foreach (var (name, on) in toggles)
        {
            RoundRect(c, x + 16, slY, w - 32, 40, 8, Surface);
            Text(c, name, x + 32, slY + 26, 14, TextPrimary);
            // Toggle
            float togX = x + w - 70;
            RoundRect(c, togX, slY + 10, 44, 20, 10, on ? Success : Card);
            Circle(c, on ? togX + 32 : togX + 12, slY + 20, 8, SKColors.White);
            slY += 46;
        }

        // Visual Style
        Text(c, "🎨  Visueller Stil", x + 24, slY + 10, 17, TextPrimary, true);
        slY += 28;
        string[] styles = ["Classic HD", "Neon"];
        float stW = (w - 48) / 2;
        for (int i = 0; i < 2; i++)
        {
            float sx = x + 20 + i * (stW + 8);
            var bg = i == 0 ? Surface : SKColor.Parse("#0D0D20");
            RoundRect(c, sx, slY, stW, 55, 10, bg);
            if (i == 0)
            {
                // Classic HD preview: earth tones
                using var prev = new SKPaint { IsAntialias = true };
                using var prevS = SKShader.CreateLinearGradient(new SKPoint(sx, slY), new SKPoint(sx + stW, slY + 35),
                    [SKColor.Parse("#8D6E63"), SKColor.Parse("#546E7A")], SKShaderTileMode.Clamp);
                prev.Shader = prevS;
                c.DrawRoundRect(new SKRect(sx + 4, slY + 4, sx + stW - 4, slY + 32), 6, 6, prev);
                using var bP = new SKPaint { Color = RedAccent, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                c.DrawRoundRect(new SKRect(sx, slY, sx + stW, slY + 55), 10, 10, bP);
            }
            else
            {
                // Neon preview: dark with cyan glow
                using var prev = new SKPaint { IsAntialias = true };
                using var prevS = SKShader.CreateLinearGradient(new SKPoint(sx, slY), new SKPoint(sx + stW, slY + 35),
                    [SKColor.Parse("#0a1628"), Neon.WithAlpha(60)], SKShaderTileMode.Clamp);
                prev.Shader = prevS;
                c.DrawRoundRect(new SKRect(sx + 4, slY + 4, sx + stW - 4, slY + 32), 6, 6, prev);
            }
            TextC(c, styles[i], sx + stW / 2, slY + 48, 12, i == 0 ? RedAccent : TextMuted);
        }

        // Language
        slY += 70;
        Text(c, "🌍  Sprache", x + 24, slY, 17, TextPrimary, true);
        RoundRect(c, x + 16, slY + 16, w - 32, 40, 10, Surface);
        Text(c, "Deutsch", x + 32, slY + 42, 15, TextPrimary);
        Text(c, "🇩🇪", x + w - 56, slY + 43, 18, TextPrimary);
    }

    static void DrawHighScores(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        Text(c, "◀", x + 28, hY + 35, 22, TextPrimary);
        TextC(c, "🏆  Highscores", x + w / 2, hY + 35, 20, TextPrimary);

        // Highscore list
        float sY = hY + 70;
        (int rank, string name, int score, int wave, SKColor rankColor)[] scores = [
            (1, "Robert", 52400, 18, Gold),
            (2, "Spieler 2", 41200, 15, SKColor.Parse("#B0BEC5")),
            (3, "Spieler 3", 38100, 14, SKColor.Parse("#CD7F32")),
            (4, "Spieler 4", 29500, 12, TextSecondary),
            (5, "Spieler 5", 24800, 10, TextSecondary),
            (6, "Spieler 6", 19200, 8, TextSecondary),
            (7, "Spieler 7", 15600, 7, TextSecondary),
            (8, "Spieler 8", 12100, 6, TextSecondary),
            (9, "Spieler 9", 8400, 5, TextSecondary),
            (10, "Spieler 10", 5200, 3, TextSecondary),
        ];

        foreach (var (rank, name, score, wave, rankColor) in scores)
        {
            if (sY + 62 > y + h - 20) break;
            bool isFirst = rank <= 3;
            RoundRect(c, x + 16, sY, w - 32, 56, 10, isFirst ? rankColor.WithAlpha(20) : Surface);

            // Rank
            TextC(c, rank.ToString(), x + 44, sY + 32, 24, rankColor);

            // Name + Wave
            Text(c, name, x + 72, sY + 24, 16, TextPrimary, true);
            Text(c, $"Wave {wave}", x + 72, sY + 44, 12, TextMuted);

            // Score
            Text(c, score.ToString("N0"), x + w - 130, sY + 32, 18, rank <= 3 ? rankColor : RedAccent, true);

            // Trophy for top 3
            if (rank == 1) TextC(c, "🥇", x + w - 50, sY + 32, 22);
            else if (rank == 2) TextC(c, "🥈", x + w - 50, sY + 32, 22);
            else if (rank == 3) TextC(c, "🥉", x + w - 50, sY + 32, 22);

            sY += 62;
        }
    }

    static void DrawHelp(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        Text(c, "◀", x + 28, hY + 35, 22, TextPrimary);
        TextC(c, "❓  Hilfe", x + w / 2, hY + 35, 20, TextPrimary);

        // How to play
        float sY = hY + 70;
        RoundRect(c, x + 16, sY, w - 32, 100, 12, Surface);
        Text(c, "🎮  So geht's", x + 32, sY + 22, 16, TextPrimary, true);
        Text(c, "Platziere Bomben, zerstöre Blöcke", x + 32, sY + 44, 13, TextSecondary);
        Text(c, "und finde den Ausgang!", x + 32, sY + 62, 13, TextSecondary);
        Text(c, "Weiche Feinden und Explosionen aus.", x + 32, sY + 80, 13, TextSecondary);

        // Power-ups
        float puY = sY + 115;
        RoundRect(c, x + 16, puY, w - 32, 150, 12, Surface);
        Text(c, "⚡  Power-Ups", x + 32, puY + 22, 16, TextPrimary, true);

        (string icon, string name, SKColor color)[] powerUps = [
            ("💣", "BombUp", Primary),
            ("🔥", "Fire", RedAccent),
            ("⚡", "Speed", Warning),
            ("👻", "WallPass", Cyan),
            ("🎯", "Detonator", Success),
            ("💨", "BombPass", Secondary),
            ("🛡", "FlamePass", Gold),
            ("❓", "Mystery", TextMuted),
        ];

        float puGridY = puY + 32;
        float puW2 = (w - 60) / 4f;
        for (int i = 0; i < 8; i++)
        {
            int col = i % 4, row = i / 4;
            float px = x + 24 + col * puW2;
            float py = puGridY + row * 52;
            RoundRect(c, px, py, puW2 - 6, 46, 8, powerUps[i].color.WithAlpha(30));
            TextC(c, powerUps[i].icon, px + (puW2 - 6) / 2, py + 22, 18);
            TextC(c, powerUps[i].name, px + (puW2 - 6) / 2, py + 40, 10, TextSecondary);
        }

        // Enemies
        float enY = puY + 165;
        if (enY + 110 < y + h - 20)
        {
            RoundRect(c, x + 16, enY, w - 32, 110, 12, Surface);
            Text(c, "👾  Gegner", x + 32, enY + 22, 16, TextPrimary, true);

            (string icon, string name, SKColor color)[] enemies2 = [
                ("🟥", "Ballom", SKColor.Parse("#EF5350")),
                ("🟧", "Onil", SKColor.Parse("#FF7043")),
                ("🟨", "Doll", SKColor.Parse("#FFC107")),
                ("🟪", "Minvo", SKColor.Parse("#AB47BC")),
            ];

            float enGridY = enY + 32;
            float enW = (w - 60) / 4f;
            for (int i = 0; i < 4; i++)
            {
                float ex = x + 24 + i * enW;
                Circle(c, ex + (enW - 6) / 2, enGridY + 18, 16, enemies2[i].color);
                // Eyes
                Circle(c, ex + (enW - 6) / 2 - 5, enGridY + 16, 3, SKColors.White);
                Circle(c, ex + (enW - 6) / 2 + 5, enGridY + 16, 3, SKColors.White);
                TextC(c, enemies2[i].name, ex + (enW - 6) / 2, enGridY + 48, 11, TextSecondary);
            }
        }

        // Tips
        float tipY = enY + 120;
        if (tipY + 70 < y + h - 20)
        {
            RoundRect(c, x + 16, tipY, w - 32, 70, 12, Surface);
            Text(c, "💡  Tipps", x + 32, tipY + 22, 16, TextPrimary, true);
            Text(c, "• Sammle Power-Ups für Vorteile", x + 32, tipY + 42, 13, TextSecondary);
            Text(c, "• Merke dir sichere Positionen", x + 32, tipY + 60, 13, TextSecondary);
        }
    }
}
