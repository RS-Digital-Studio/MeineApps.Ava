using SkiaSharp;
using static StoreAssetGenerator.Gfx;

namespace StoreAssetGenerator;

static class HandwerkerRechnerApp
{
    static readonly SKColor Orange1 = SKColor.Parse("#BF360C");
    static readonly SKColor Orange2 = SKColor.Parse("#E65100");
    static readonly SKColor Orange = SKColor.Parse("#FF6D00");
    static readonly string[] TabIcons = ["🏠", "📁", "⚙"];
    static readonly string[] TabLabels = ["Rechner", "Projekte", "Settings"];

    public static AppDef Create() => new(
        "HandwerkerRechner", Orange,
        DrawIcon, DrawFeature,
        [
            ("9 Handwerker-\nRechner!", DrawCalculatorGrid),
            ("Fliesen\nberechnen!", DrawTileCalc),
            ("Farbe &\nWandberechnung!", DrawPaintCalc),
            ("Projekte\nspeichern!", DrawProjects),
            ("Premium\nRechner!", DrawPremiumCalcs),
            ("4 schöne\nThemes!", DrawSettings),
        ],
        [
            ("9 Handwerker-\nRechner!", DrawCalculatorGrid),
            ("Fliesen\nberechnen!", DrawTileCalc),
            ("Projekte\nspeichern!", DrawProjects),
            ("Premium\nRechner!", DrawPremiumCalcs),
        ]
    );

    static void DrawIcon(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var c = surface.Canvas;
        IconBg(c, 0, 0, size, Orange1, Orange2);
        float s = size / 512f, cx = size / 2f;

        // Ruler
        using var wp = new SKPaint { Color = SKColor.Parse("#FFECB3"), IsAntialias = true };
        var rulerRect = new SKRect(cx - 120 * s, 140 * s, cx + 30 * s, 380 * s);
        c.Save();
        c.RotateDegrees(-20, cx, size / 2f);
        c.DrawRoundRect(rulerRect, 8 * s, 8 * s, wp);
        // Ruler marks
        using var markP = new SKPaint { Color = Orange1, IsAntialias = true, StrokeWidth = 2 * s };
        for (float my = 160 * s; my < 370 * s; my += 24 * s)
        {
            float mw = (int)((my - 160 * s) / (24 * s)) % 5 == 0 ? 35 * s : 18 * s;
            c.DrawLine(cx - 120 * s, my, cx - 120 * s + mw, my, markP);
        }
        c.Restore();

        // Wrench
        c.Save();
        c.RotateDegrees(25, cx, size / 2f);
        using var wrenchP = new SKPaint { Color = SKColors.White, IsAntialias = true };
        // Handle
        c.DrawRoundRect(new SKRect(cx - 10 * s, 200 * s, cx + 60 * s, 380 * s), 12 * s, 12 * s, wrenchP);
        // Head
        c.DrawCircle(cx + 25 * s, 185 * s, 35 * s, wrenchP);
        Circle(c, cx + 25 * s, 185 * s, 18 * s, Orange1);
        c.Restore();

        SavePng(surface, "icon_512.png");
        Console.WriteLine("  HandwerkerRechner Icon generiert");
    }

    static void DrawFeature()
    {
        const int w = 1024, h = 500;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var c = surface.Canvas;
        FeatureGraphicBase(c, w, h, Orange, (c2, x, y, sz) => {
            IconBg(c2, x, y, sz, Orange1, Orange2);
            using var wp2 = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = sz * 0.35f,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                TextAlign = SKTextAlign.Center };
            c2.DrawText("📐", x + sz / 2, y + sz * 0.6f, wp2);
        },
        "Handwerker", "Rechner",
        "Fliesen • Farbe • Elektro • Dach & mehr",
        [("🔧", Orange, w - 200, 60, 80), ("📐", Warning, w - 100, 160, 70), ("⚡", Cyan, w - 190, 280, 65), ("🏠", Success, w - 80, 340, 60)]);
        SavePng(surface, "feature_graphic.png");
        Console.WriteLine("  HandwerkerRechner Feature Graphic generiert");
    }

    static void DrawCalculatorGrid(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "📐  HandwerkerRechner", x + w / 2, hY + 35, 20, TextPrimary);

        // Calculator grid (3x3)
        string[] names = ["Fliesen", "Tapete", "Farbe", "Bodenbelag", "Trockenbau", "Elektro", "Metall", "Garten", "Dach & Solar"];
        string[] icons = ["🔲", "🖼", "🎨", "🪵", "🧱", "⚡", "⚙", "🌱", "🏠"];
        SKColor[] colors = [Primary, Secondary, SKColor.Parse("#EC4899"), Orange, Warning, Cyan, TextMuted, Success, Error];
        bool[] premium = [false, false, false, false, true, true, true, true, true];

        float cardW = (w - 52) / 3f, cardH = 115;
        float gY = hY + 75;

        for (int i = 0; i < 9; i++)
        {
            int col = i % 3, row = i / 3;
            float cx = x + 16 + col * (cardW + 4);
            float cy = gY + row * (cardH + 8);
            RoundRect(c, cx, cy, cardW, cardH, 12, Surface);
            Circle(c, cx + cardW / 2, cy + 40, 25, colors[i].WithAlpha(40));
            TextC(c, icons[i], cx + cardW / 2, cy + 48, 22);
            TextC(c, names[i], cx + cardW / 2, cy + 80, 12, TextSecondary);
            if (premium[i])
            {
                RoundRect(c, cx + cardW - 40, cy + 4, 36, 18, 9, Gold.WithAlpha(60));
                TextC(c, "PRO", cx + cardW - 22, cy + 17, 10, Gold);
            }
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Orange);
    }

    static void DrawTileCalc(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🔲  Fliesenrechner", x + w / 2, hY + 35, 20, TextPrimary);

        float fY = hY + 75;
        (string label, string val)[] fields = [("Raumlänge (m)", "4,50"), ("Raumbreite (m)", "3,20"),
            ("Fliesenlänge (cm)", "30"), ("Fliesenbreite (cm)", "30"), ("Fugenstärke (mm)", "3"), ("Verschnitt (%)", "10")];
        foreach (var (label, val) in fields)
        {
            RoundRect(c, x + 16, fY, w - 32, 50, 10, Surface);
            Text(c, label, x + 32, fY + 20, 13, TextSecondary);
            Text(c, val, x + w - 100, fY + 34, 16, TextPrimary, true);
            fY += 56;
        }

        RoundRect(c, x + 16, fY, w - 32, 44, 12, Primary);
        TextC(c, "Berechnen", x + w / 2, fY + 30, 17, TextPrimary);

        float rY = fY + 60;
        RoundRect(c, x + 16, rY, w - 32, 120, 12, Surface);
        Text(c, "Ergebnis", x + 32, rY + 24, 16, TextPrimary, true);
        float hw = (w - 80) / 2;
        StatItem(c, x + 32, rY + 35, "Fläche", "14,40 m²", Primary);
        StatItem(c, x + 40 + hw, rY + 35, "Fliesen", "178 Stk.", Success);
        StatItem(c, x + 32, rY + 75, "Kleber", "22,0 kg", Warning);
        StatItem(c, x + 40 + hw, rY + 75, "Fugenmasse", "4,8 kg", Cyan);

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Orange);
    }

    static void DrawPaintCalc(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🎨  Farbrechner", x + w / 2, hY + 35, 20, TextPrimary);

        float fY = hY + 75;
        (string label, string val)[] fields = [("Wandlänge (m)", "12,00"), ("Wandhöhe (m)", "2,50"),
            ("Fenster/Türen (m²)", "4,80"), ("Anstriche", "2"), ("Ergiebigkeit (m²/L)", "10")];
        foreach (var (label, val) in fields)
        {
            RoundRect(c, x + 16, fY, w - 32, 50, 10, Surface);
            Text(c, label, x + 32, fY + 20, 13, TextSecondary);
            Text(c, val, x + w - 100, fY + 34, 16, TextPrimary, true);
            fY += 56;
        }

        RoundRect(c, x + 16, fY, w - 32, 44, 12, Primary);
        TextC(c, "Berechnen", x + w / 2, fY + 30, 17, TextPrimary);

        float rY = fY + 60;
        RoundRect(c, x + 16, rY, w - 32, 90, 12, Surface);
        Text(c, "Ergebnis", x + 32, rY + 24, 16, TextPrimary, true);
        float hw = (w - 80) / 2;
        StatItem(c, x + 32, rY + 35, "Wandfläche", "25,20 m²", Primary);
        StatItem(c, x + 40 + hw, rY + 35, "Farbe benötigt", "5,04 Liter", Success);

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Orange);
    }

    static void DrawProjects(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "📁  Projekte", x + w / 2, hY + 35, 20, TextPrimary);

        (string name, string type, string date, string desc)[] projects = [
            ("Bad-Renovierung", "🔲 Fliesen", "05.02.2026", "Badezimmer 8,5 m² komplett"),
            ("Wohnzimmer", "🎨 Farbe", "03.02.2026", "4 Wände + Decke streichen"),
            ("Küche Boden", "🪵 Bodenbelag", "01.02.2026", "Vinyl 12m² verlegen"),
            ("Kinderzimmer", "🖼 Tapete", "28.01.2026", "3 Wände tapezieren"),
            ("Garage", "⚡ Elektro", "25.01.2026", "Beleuchtung + 3 Steckdosen"),
        ];

        float pY = hY + 75;
        foreach (var (name, type, date, desc) in projects)
        {
            if (pY + 85 > y + h - 70) break;
            RoundRect(c, x + 16, pY, w - 32, 78, 12, Surface);
            Text(c, name, x + 32, pY + 24, 17, TextPrimary, true);
            Text(c, $"{type} • {date}", x + 32, pY + 46, 13, TextSecondary);
            Text(c, desc, x + 32, pY + 66, 12, TextMuted);
            pY += 90;
        }

        Circle(c, x + w - 50, y + h - 100, 28, Primary);
        TextC(c, "+", x + w - 50, y + h - 93, 28, SKColors.White);

        TabBar(c, x, y + h - 60, w, 1, TabIcons, TabLabels, Orange);
    }

    static void DrawPremiumCalcs(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "⭐  Premium Rechner", x + w / 2, hY + 35, 20, TextPrimary);

        (string icon, string name, string desc)[] calcs = [
            ("🧱", "Trockenbau", "Gipskarton, Profile, Schrauben"),
            ("⚡", "Elektro", "Kabel, Sicherungen, Ohm'sches Gesetz"),
            ("⚙", "Metall", "Gewicht, Profile, Materialkosten"),
            ("🌱", "Garten", "Rasen, Beet, Zaun, Terrasse"),
            ("🏠", "Dach & Solar", "Dachfläche, Ziegel, Solarertrag"),
        ];

        float cY = hY + 75;
        foreach (var (icon, name, desc) in calcs)
        {
            RoundRect(c, x + 16, cY, w - 32, 80, 12, Surface);
            RoundRect(c, x + 28, cY + 16, 50, 50, 10, Card);
            TextC(c, icon, x + 53, cY + 50, 24);
            Text(c, name, x + 92, cY + 34, 17, TextPrimary, true);
            Text(c, desc, x + 92, cY + 56, 13, TextSecondary);
            RoundRect(c, x + w - 70, cY + 28, 40, 26, 8, Gold.WithAlpha(60));
            TextC(c, "PRO", x + w - 50, cY + 46, 11, Gold);
            cY += 92;
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Orange);
    }

    static void DrawSettings(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "⚙  Einstellungen", x + w / 2, hY + 35, 20, TextPrimary);

        float sY = hY + 75;
        Text(c, "🎨  Theme", x + 24, sY, 18, TextPrimary, true);
        string[] themes = ["Midnight", "Aurora", "Daylight", "Forest"];
        SKColor[] tC = [Primary, SKColor.Parse("#EC4899"), SKColor.Parse("#2563EB"), SKColor.Parse("#10B981")];
        SKColor[] tB = [Bg, SKColor.Parse("#1C1033"), SKColor.Parse("#F0F4FF"), SKColor.Parse("#022C22")];
        float tw = (w - 52) / 2;
        for (int i = 0; i < 4; i++)
        {
            float tx = x + 16 + (i % 2) * (tw + 12);
            float ty = sY + 15 + (i / 2) * 80;
            RoundRect(c, tx, ty, tw, 70, 12, tB[i]);
            using var bp = new SKPaint { Color = i == 0 ? tC[i] : Border, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = i == 0 ? 3 : 1 };
            c.DrawRoundRect(new SKRect(tx, ty, tx + tw, ty + 70), 12, 12, bp);
            Circle(c, tx + 24, ty + 35, 12, tC[i]);
            Text(c, themes[i], tx + 44, ty + 40, 15, i == 2 ? SKColor.Parse("#1E293B") : TextPrimary, true);
            if (i == 0) TextC(c, "✓", tx + tw - 24, ty + 40, 18, tC[i]);
        }

        TabBar(c, x, y + h - 60, w, 2, TabIcons, TabLabels, Orange);
    }
}
