using SkiaSharp;
using static StoreAssetGenerator.Gfx;

namespace StoreAssetGenerator;

static class HandwerkerImperiumApp
{
    static readonly SKColor Purple1 = SKColor.Parse("#4A148C");
    static readonly SKColor Purple2 = SKColor.Parse("#7B1FA2");
    static readonly SKColor Purple = SKColor.Parse("#9C27B0");
    static readonly string[] TabIcons = ["🏠", "📊", "🏆", "🛒", "⚙"];
    static readonly string[] TabLabels = ["Home", "Stats", "Erfolge", "Shop", "Settings"];

    public static AppDef Create() => new(
        "HandwerkerImperium", Primary,
        DrawIcon, DrawFeature,
        [
            ("Baue dein\nHandwerker-Imperium!", DrawDashboard),
            ("4 einzigartige\nMini-Spiele!", DrawSawingGame),
            ("Löse Rätsel,\nverdiene Belohnungen!", DrawPipePuzzle),
            ("Upgrade &\nWachse!", DrawShop),
            ("Sammle 26\nAchievements!", DrawAchievements),
            ("Verfolge deinen\nFortschritt!", DrawStatistics),
        ],
        [
            ("Baue dein\nHandwerker-Imperium!", DrawDashboard),
            ("4 einzigartige\nMini-Spiele!", DrawSawingGame),
            ("Upgrade &\nWachse!", DrawShop),
            ("Sammle 26\nAchievements!", DrawAchievements),
        ]
    );

    static void DrawIcon(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var c = surface.Canvas;
        IconBg(c, 0, 0, size, Purple1, Purple2);
        float s = size / 512f, cx = size / 2f;

        // Castle base
        RoundRect(c, cx - 100 * s, 200 * s, 200 * s, 200 * s, 8 * s, SKColors.White);

        // Left tower
        RoundRect(c, cx - 120 * s, 150 * s, 60 * s, 250 * s, 4 * s, SKColors.White);
        // Left tower battlements
        RoundRect(c, cx - 125 * s, 140 * s, 20 * s, 30 * s, 2 * s, SKColors.White);
        RoundRect(c, cx - 95 * s, 140 * s, 20 * s, 30 * s, 2 * s, SKColors.White);

        // Right tower
        RoundRect(c, cx + 60 * s, 150 * s, 60 * s, 250 * s, 4 * s, SKColors.White);
        // Right tower battlements
        RoundRect(c, cx + 55 * s, 140 * s, 20 * s, 30 * s, 2 * s, SKColors.White);
        RoundRect(c, cx + 85 * s, 140 * s, 20 * s, 30 * s, 2 * s, SKColors.White);

        // Center battlements
        RoundRect(c, cx - 30 * s, 190 * s, 20 * s, 25 * s, 2 * s, SKColors.White);
        RoundRect(c, cx + 10 * s, 190 * s, 20 * s, 25 * s, 2 * s, SKColors.White);

        // Door (pink)
        SKColor doorPink = SKColor.Parse("#F48FB1");
        RoundRect(c, cx - 25 * s, 320 * s, 50 * s, 80 * s, 25 * s, doorPink);

        // Windows (pink)
        RoundRect(c, cx - 70 * s, 260 * s, 30 * s, 35 * s, 6 * s, doorPink);
        RoundRect(c, cx + 40 * s, 260 * s, 30 * s, 35 * s, 6 * s, doorPink);

        // Tower windows
        RoundRect(c, cx - 105 * s, 220 * s, 24 * s, 28 * s, 5 * s, doorPink);
        RoundRect(c, cx + 80 * s, 220 * s, 24 * s, 28 * s, 5 * s, doorPink);

        // Flag on right tower
        using var flagP = new SKPaint { Color = SKColor.Parse("#FFD700"), IsAntialias = true, StrokeWidth = 3 * s, StrokeCap = SKStrokeCap.Round };
        c.DrawLine(cx + 100 * s, 140 * s, cx + 100 * s, 90 * s, flagP);
        using var flagPath = new SKPath();
        flagPath.MoveTo(cx + 100 * s, 90 * s);
        flagPath.LineTo(cx + 135 * s, 100 * s);
        flagPath.LineTo(cx + 100 * s, 112 * s);
        flagPath.Close();
        using var flagFill = new SKPaint { Color = SKColor.Parse("#FFD700"), IsAntialias = true };
        c.DrawPath(flagPath, flagFill);

        SavePng(surface, "icon_512.png");
        Console.WriteLine("  HandwerkerImperium Icon generiert");
    }

    static void DrawFeature()
    {
        const int w = 1024, h = 500;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var c = surface.Canvas;
        FeatureGraphicBase(c, w, h, Purple, (c2, x, y, sz) => {
            IconBg(c2, x, y, sz, Purple1, Purple2);
            float s2 = sz / 512f;
            // Simplified castle
            RoundRect(c2, x + sz / 2 - 50 * s2, y + 160 * s2, 100 * s2, 100 * s2, 4 * s2, SKColors.White);
            RoundRect(c2, x + sz / 2 - 60 * s2, y + 130 * s2, 30 * s2, 130 * s2, 3 * s2, SKColors.White);
            RoundRect(c2, x + sz / 2 + 30 * s2, y + 130 * s2, 30 * s2, 130 * s2, 3 * s2, SKColors.White);
            RoundRect(c2, x + sz / 2 - 12 * s2, y + 220 * s2, 24 * s2, 40 * s2, 12 * s2, SKColor.Parse("#F48FB1"));
        },
        "Handwerker", "Imperium",
        "Workshops • Mini-Spiele • Upgrades",
        [("🔨", Purple, w - 200, 60, 80), ("⚡", Warning, w - 100, 160, 70), ("🏆", Gold, w - 190, 280, 65), ("💰", Success, w - 80, 340, 60)]);
        SavePng(surface, "feature_graphic.png");
        Console.WriteLine("  HandwerkerImperium Feature Graphic generiert");
    }

    static void DrawDashboard(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🏠  Handwerker Imperium", x + w / 2, hY + 35, 20, TextPrimary);

        // Money + Level header
        float mY = hY + 70;
        RoundRect(c, x + 16, mY, w - 32, 80, 12, Surface);
        TextC(c, "💰 12.450", x + w / 2, mY + 32, 28, Success);
        Text(c, "+24,5 / Sek", x + w / 2 - 40, mY + 52, 13, TextSecondary);

        // Level bar
        float lvlY = mY + 60;
        Text(c, "⭐ Level 8", x + 32, lvlY + 12, 13, Gold, true);
        Progress(c, x + 130, lvlY + 6, w - 180, 10, 0.65f, Primary);
        Text(c, "65%", x + w - 56, lvlY + 12, 11, TextMuted);

        // Workshop grid (3x2)
        float gY = mY + 95;
        Text(c, "Workshops", x + 24, gY, 16, TextPrimary, true);
        float cardW = (w - 56) / 3f;
        float cardH = 110;
        (string icon, string name, int level, string income, bool locked)[] workshops = [
            ("🪚", "Schreinerei", 5, "+8,2/s", false),
            ("🔧", "Klempnerei", 3, "+4,5/s", false),
            ("⚡", "Elektrik", 4, "+6,1/s", false),
            ("🎨", "Malerei", 2, "+2,8/s", false),
            ("🏗", "Dachdeckerei", 1, "+1,2/s", false),
            ("🔒", "Schmied", 0, "Lvl 10", true),
        ];

        float gy2 = gY + 18;
        for (int i = 0; i < 6; i++)
        {
            int col = i % 3, row = i / 3;
            float cx = x + 16 + col * (cardW + 8);
            float cy = gy2 + row * (cardH + 8);
            var ws = workshops[i];

            RoundRect(c, cx, cy, cardW, cardH, 10, ws.locked ? Card.WithAlpha(100) : Surface);
            TextC(c, ws.icon, cx + cardW / 2, cy + 30, 24);
            TextC(c, ws.name, cx + cardW / 2, cy + 54, 11, ws.locked ? TextMuted : TextPrimary);

            if (ws.locked)
            {
                TextC(c, ws.income, cx + cardW / 2, cy + 74, 10, TextMuted);
            }
            else
            {
                Progress(c, cx + 8, cy + 65, cardW - 16, 6, ws.level / 10f, Primary);
                Text(c, $"Lvl {ws.level}", cx + 8, cy + 84, 10, TextSecondary);
                Text(c, ws.income, cx + cardW - 60, cy + 84, 10, Success);
                // Upgrade button
                RoundRect(c, cx + 8, cy + 90, cardW - 16, 16, 4, Primary.WithAlpha(80));
                TextC(c, "⬆ Upgrade", cx + cardW / 2, cy + 102, 9, TextPrimary);
            }
        }

        // Orders section
        float oY = gy2 + 2 * (cardH + 8) + 10;
        Text(c, "Aufträge", x + 24, oY, 16, TextPrimary, true);
        float oy2 = oY + 18;
        (string title, string workshop, string reward, string xp)[] orders = [
            ("Holztisch anfertigen", "Schreinerei", "💰 350", "⭐ 45"),
            ("Rohr reparieren", "Klempnerei", "💰 280", "⭐ 35"),
        ];
        foreach (var (title, workshop, reward, xpVal) in orders)
        {
            if (oy2 + 68 > y + h - 70) break;
            RoundRect(c, x + 16, oy2, w - 32, 60, 10, Surface);
            Text(c, title, x + 32, oy2 + 22, 14, TextPrimary, true);
            Text(c, workshop, x + 32, oy2 + 42, 12, Primary);
            Text(c, reward, x + w - 160, oy2 + 22, 12, Success);
            Text(c, xpVal, x + w - 160, oy2 + 42, 12, Gold);
            RoundRect(c, x + w - 90, oy2 + 15, 58, 30, 8, Primary);
            TextC(c, "Start", x + w - 61, oy2 + 35, 13, TextPrimary);
            oy2 += 68;
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Primary);
    }

    static void DrawSawingGame(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        Text(c, "🪚  Sägen", x + 32, hY + 35, 20, TextPrimary);
        // Difficulty stars
        Text(c, "⭐⭐⭐", x + w - 120, hY + 35, 18, Gold);

        // Wood board
        float bY = hY + 80;
        SKColor wood1 = SKColor.Parse("#8D6E63");
        SKColor wood2 = SKColor.Parse("#6D4C41");
        RoundRect(c, x + 40, bY, w - 80, 250, 12, wood1);

        // Wood grain lines
        using var grainP = new SKPaint { Color = wood2.WithAlpha(80), IsAntialias = true, StrokeWidth = 2 };
        for (int i = 0; i < 8; i++)
        {
            float gy = bY + 25 + i * 30;
            c.DrawLine(x + 55, gy, x + w - 55, gy, grainP);
        }

        // Center cut line (dashed)
        float cutY = bY + 125;
        using var cutP = new SKPaint { Color = SKColor.Parse("#FF5722"), IsAntialias = true, StrokeWidth = 3, PathEffect = SKPathEffect.CreateDash([8, 6], 0) };
        c.DrawLine(x + 55, cutY, x + w - 55, cutY, cutP);

        // Timing bar
        float tY = bY + 275;
        RoundRect(c, x + 40, tY, w - 80, 50, 8, Card);

        // Zones
        float barW = w - 96;
        float barX = x + 48;
        RoundRect(c, barX, tY + 8, barW, 34, 6, Error.WithAlpha(120)); // Miss
        RoundRect(c, barX, tY + 8, barW * 0.5f, 34, 6, Warning.WithAlpha(150)); // OK
        RoundRect(c, barX, tY + 8, barW * 0.3f, 34, 6, Primary.WithAlpha(180)); // Good
        RoundRect(c, barX, tY + 8, barW * 0.15f, 34, 6, Success); // Perfect
        Text(c, "PERFEKT", barX + 8, tY + 30, 10, TextPrimary, true);
        Text(c, "GUT", barX + barW * 0.2f, tY + 30, 10, TextPrimary, true);
        Text(c, "OK", barX + barW * 0.4f, tY + 30, 10, TextPrimary, true);

        // Sliding marker
        float markerX = barX + barW * 0.35f;
        RoundRect(c, markerX - 3, tY + 4, 6, 42, 3, SKColors.White);

        // Action button
        float btnY = tY + 65;
        RoundRect(c, x + 60, btnY, w - 120, 50, 12, Primary);
        TextC(c, "🪚  SÄGEN!", x + w / 2, btnY + 34, 20, TextPrimary);

        // Result preview
        float rY = btnY + 65;
        RoundRect(c, x + 16, rY, w - 32, 80, 12, Surface);
        TextC(c, "Letzte Bewertung", x + w / 2, rY + 22, 13, TextSecondary);
        TextC(c, "⭐ PERFEKT!", x + w / 2, rY + 52, 22, Gold);
        TextC(c, "💰 +120  ⭐ +15", x + w / 2, rY + 72, 14, Success);

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Primary);
    }

    static void DrawPipePuzzle(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        Text(c, "🔧  Rohr-Puzzle", x + 32, hY + 35, 20, TextPrimary);
        Text(c, "⭐⭐", x + w - 100, hY + 35, 18, Gold);

        // Timer + Moves
        float iY = hY + 70;
        RoundRect(c, x + 16, iY, (w - 40) / 2, 36, 8, Surface);
        TextC(c, "⏱ 01:24", x + 16 + (w - 40) / 4, iY + 24, 15, Warning);
        RoundRect(c, x + 24 + (w - 40) / 2, iY, (w - 40) / 2, 36, 8, Surface);
        TextC(c, "Züge: 8", x + 24 + 3 * (w - 40) / 4, iY + 24, 15, TextSecondary);

        // Instructions
        float insY = iY + 44;
        TextC(c, "Tippe zum Drehen • Verbinde Start → Ziel", x + w / 2, insY + 14, 12, TextMuted);

        // Puzzle grid (5x5)
        float gY = insY + 28;
        int gridSize = 5;
        float cellSize = Math.Min((w - 64) / gridSize, 60);
        float gridW = gridSize * cellSize;
        float gridX = x + (w - gridW) / 2;

        RoundRect(c, gridX - 8, gY - 8, gridW + 16, gridSize * cellSize + 16, 12, Surface);

        // Pipe segments
        bool[,] hasTop = { { false, true, false, false, true }, { true, true, false, true, false }, { false, true, true, true, false }, { false, false, true, false, true }, { false, true, true, false, true } };
        bool[,] hasBot = { { true, true, false, true, false }, { false, true, true, true, false }, { false, false, true, false, true }, { false, true, true, false, false }, { false, false, false, false, false } };
        bool[,] hasLeft = { { false, false, true, true, false }, { false, false, true, false, true }, { true, false, false, true, true }, { false, true, false, true, false }, { true, false, true, false, true } };
        bool[,] hasRight = { { false, true, true, false, true }, { false, true, false, true, false }, { false, false, true, true, false }, { true, false, true, false, false }, { false, true, false, true, false } };

        for (int row = 0; row < gridSize; row++)
            for (int col = 0; col < gridSize; col++)
            {
                float cx2 = gridX + col * cellSize;
                float cy2 = gY + row * cellSize;
                RoundRect(c, cx2 + 2, cy2 + 2, cellSize - 4, cellSize - 4, 4, Card);

                float mid = cellSize / 2;
                float pipeW = 10;
                using var pipeP = new SKPaint { Color = Primary, IsAntialias = true };

                // Draw pipe segments
                if (hasTop[row, col]) c.DrawRect(cx2 + mid - pipeW / 2, cy2 + 2, pipeW, mid - 2, pipeP);
                if (hasBot[row, col]) c.DrawRect(cx2 + mid - pipeW / 2, cy2 + mid, pipeW, mid - 2, pipeP);
                if (hasLeft[row, col]) c.DrawRect(cx2 + 2, cy2 + mid - pipeW / 2, mid - 2, pipeW, pipeP);
                if (hasRight[row, col]) c.DrawRect(cx2 + mid, cy2 + mid - pipeW / 2, mid - 2, pipeW, pipeP);

                // Center junction
                bool hasPipe = hasTop[row, col] || hasBot[row, col] || hasLeft[row, col] || hasRight[row, col];
                if (hasPipe)
                    c.DrawRect(cx2 + mid - pipeW / 2, cy2 + mid - pipeW / 2, pipeW, pipeW, pipeP);

                // Source marker
                if (row == 0 && col == 0)
                {
                    Circle(c, cx2 + mid, cy2 + mid, 12, Success);
                    TextC(c, "💧", cx2 + mid, cy2 + mid + 5, 12);
                }
                // Target marker
                if (row == gridSize - 1 && col == gridSize - 1)
                {
                    Circle(c, cx2 + mid, cy2 + mid, 12, Warning);
                    TextC(c, "🏠", cx2 + mid, cy2 + mid + 5, 12);
                }
            }

        // Reward info
        float rY = gY + gridSize * cellSize + 25;
        if (rY + 50 < y + h - 70)
        {
            RoundRect(c, x + 16, rY, w - 32, 50, 10, Surface);
            TextC(c, "Belohnung: 💰 250  ⭐ 30", x + w / 2, rY + 32, 15, Success);
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Primary);
    }

    static void DrawShop(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        Text(c, "🛒  Shop", x + 32, hY + 35, 20, TextPrimary);
        // Balance pill
        RoundRect(c, x + w - 150, hY + 14, 120, 28, 14, Success.WithAlpha(40));
        TextC(c, "💰 12.450", x + w - 90, hY + 33, 13, Success);

        // Premium banner
        float pY = hY + 70;
        RoundRect(c, x + 16, pY, w - 32, 60, 12, Gold.WithAlpha(30));
        using var goldBorderP = new SKPaint { Color = Gold, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        c.DrawRoundRect(new SKRect(x + 16, pY, x + w - 16, pY + 60), 12, 12, goldBorderP);
        TextC(c, "👑 Premium Upgrade", x + w / 2, pY + 26, 16, Gold);
        TextC(c, "Keine Werbung + 2x Einkommen!", x + w / 2, pY + 48, 12, TextSecondary);

        // Shop items
        float sY = pY + 75;
        (string icon, string name, string desc, string price, bool canAfford)[] items = [
            ("⚡", "Speed Boost", "+50% Geschwindigkeit (5 Min)", "💰 500", true),
            ("💰", "Geld Boost", "2x Einkommen (10 Min)", "💰 1.000", true),
            ("⭐", "XP Boost", "+50% Erfahrung (10 Min)", "💰 800", true),
            ("🔨", "Auto-Arbeiter", "Produziert ohne Tippen", "💰 2.500", true),
            ("🏗", "Werkstatt-Slot", "1 zusätzlicher Workshop", "💰 5.000", true),
            ("🎲", "Glücks-Paket", "Zufälliger Bonus!", "📺 Werbung", false),
        ];

        foreach (var (icon, name, desc, price, canAfford) in items)
        {
            if (sY + 70 > y + h - 70) break;
            RoundRect(c, x + 16, sY, w - 32, 64, 10, Surface);
            RoundRect(c, x + 26, sY + 10, 44, 44, 8, Primary.WithAlpha(40));
            TextC(c, icon, x + 48, sY + 40, 22);
            Text(c, name, x + 80, sY + 26, 15, TextPrimary, true);
            Text(c, desc, x + 80, sY + 46, 11, TextSecondary);

            if (!canAfford)
            {
                RoundRect(c, x + w - 118, sY + 18, 86, 28, 8, Warning.WithAlpha(40));
                TextC(c, price, x + w - 75, sY + 37, 12, Warning);
            }
            else
            {
                RoundRect(c, x + w - 108, sY + 18, 76, 28, 8, Primary);
                TextC(c, price, x + w - 70, sY + 37, 12, TextPrimary);
            }
            sY += 72;
        }

        TabBar(c, x, y + h - 60, w, 3, TabIcons, TabLabels, Primary);
    }

    static void DrawAchievements(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        Text(c, "🏆  Achievements", x + 32, hY + 35, 20, TextPrimary);
        // Progress badge
        RoundRect(c, x + w - 100, hY + 14, 68, 28, 14, Primary.WithAlpha(40));
        TextC(c, "18/26", x + w - 66, hY + 33, 14, Primary);

        // Overall progress
        float pY = hY + 70;
        RoundRect(c, x + 16, pY, w - 32, 50, 12, Surface);
        Text(c, "Gesamtfortschritt", x + 32, pY + 22, 14, TextSecondary);
        Progress(c, x + 32, pY + 32, w - 64, 10, 18f / 26f, Primary);
        Text(c, "69%", x + w - 60, pY + 22, 14, Primary, true);

        // Achievement list
        float aY = pY + 65;
        (string icon, string title, string desc, string reward, bool unlocked, float progress)[] achievements = [
            ("🪚", "Meister-Schreiner", "100 Aufträge abgeschlossen", "💰 500", true, 1f),
            ("⚡", "Blitzschnell", "10 Perfekte Bewertungen", "💰 300", true, 1f),
            ("💰", "Millionär", "1.000.000 verdient", "⭐ 100", true, 1f),
            ("🏗", "Baumeister", "Alle 6 Workshops freigeschaltet", "💰 1.000", false, 5f / 6f),
            ("🎯", "Perfektionist", "50 Perfekte in Folge", "⭐ 200", false, 32f / 50f),
            ("🔧", "Handwerks-Meister", "Level 20 erreichen", "💰 2.000", false, 8f / 20f),
            ("🏆", "Legende", "Alle Achievements freischalten", "👑 Premium", false, 18f / 26f),
        ];

        foreach (var (icon, title, desc, reward, unlocked, progress) in achievements)
        {
            if (aY + 68 > y + h - 70) break;
            RoundRect(c, x + 16, aY, w - 32, 62, 10, Surface);

            // Icon with colored background
            var iconBg = unlocked ? Success.WithAlpha(60) : Card;
            RoundRect(c, x + 26, aY + 10, 42, 42, 8, iconBg);
            TextC(c, icon, x + 47, aY + 39, 20);

            Text(c, title, x + 78, aY + 24, 14, TextPrimary, true);
            Text(c, desc, x + 78, aY + 42, 11, TextSecondary);

            if (unlocked)
            {
                TextC(c, "✅", x + w - 40, aY + 31, 18, Success);
            }
            else
            {
                Progress(c, x + 78, aY + 50, w - 170, 5, progress, Primary);
                Text(c, reward, x + w - 86, aY + 24, 11, Gold);
            }
            aY += 68;
        }

        TabBar(c, x, y + h - 60, w, 2, TabIcons, TabLabels, Primary);
    }

    static void DrawStatistics(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "📊  Statistiken", x + w / 2, hY + 35, 20, TextPrimary);

        // Player overview
        float sY = hY + 70;
        RoundRect(c, x + 16, sY, w - 32, 100, 12, Surface);
        Text(c, "Spieler-Übersicht", x + 32, sY + 22, 15, TextPrimary, true);
        float hw = (w - 64) / 3;
        StatItem(c, x + 32, sY + 28, "Level", "8", Primary);
        StatItem(c, x + 32 + hw, sY + 28, "Gesamt-XP", "4.520", Gold);
        StatItem(c, x + 32 + 2 * hw, sY + 28, "Spielzeit", "12h 35m", TextSecondary);

        float s2Y = sY + 68;
        StatItem(c, x + 32, s2Y, "Guthaben", "💰 12.450", Success);
        StatItem(c, x + 32 + hw, s2Y, "Verdient", "💰 156.780", Success);
        StatItem(c, x + 32 + 2 * hw, s2Y, "Ausgegeben", "💰 144.330", Warning);

        // Mini-Games section
        float mgY = sY + 115;
        RoundRect(c, x + 16, mgY, w - 32, 80, 12, Surface);
        Text(c, "Mini-Spiele", x + 32, mgY + 22, 15, TextPrimary, true);
        float hw2 = (w - 64) / 4;
        StatItem(c, x + 32, mgY + 28, "Aufträge", "142", Primary);
        StatItem(c, x + 32 + hw2, mgY + 28, "Gespielt", "284", TextSecondary);
        StatItem(c, x + 32 + 2 * hw2, mgY + 28, "Perfekt", "89", Gold);
        StatItem(c, x + 32 + 3 * hw2, mgY + 28, "Rate", "31%", Success);

        // Streaks
        float stY = mgY + 68;
        Text(c, "🔥 Streak: 12", x + 32, stY, 13, Warning, true);
        Text(c, "⭐ Bester: 24", x + w / 2, stY, 13, Gold, true);

        // Workshops overview
        float wsY = mgY + 95;
        RoundRect(c, x + 16, wsY, w - 32, 165, 12, Surface);
        Text(c, "Werkstätten", x + 32, wsY + 22, 15, TextPrimary, true);

        (string icon, string name, int level, string income)[] wsStats = [
            ("🪚", "Schreinerei", 5, "+8,2/s"),
            ("🔧", "Klempnerei", 3, "+4,5/s"),
            ("⚡", "Elektrik", 4, "+6,1/s"),
            ("🎨", "Malerei", 2, "+2,8/s"),
            ("🏗", "Dachdeckerei", 1, "+1,2/s"),
        ];

        float wsy2 = wsY + 30;
        foreach (var (icon, name, level, income) in wsStats)
        {
            if (wsy2 + 28 > y + h - 70) break;
            RoundRect(c, x + 28, wsy2, w - 56, 24, 4, Card.WithAlpha(80));
            TextC(c, icon, x + 44, wsy2 + 17, 12);
            Text(c, name, x + 60, wsy2 + 17, 12, TextPrimary);
            Text(c, $"Lvl {level}", x + w / 2 + 20, wsy2 + 17, 12, TextSecondary);
            Text(c, income, x + w - 86, wsy2 + 17, 12, Success, true);
            wsy2 += 28;
        }

        TabBar(c, x, y + h - 60, w, 1, TabIcons, TabLabels, Primary);
    }
}
