using SkiaSharp;
using static StoreAssetGenerator.Gfx;

namespace StoreAssetGenerator;

static class RechnerPlusApp
{
    static readonly SKColor Indigo1 = SKColor.Parse("#1A237E");
    static readonly SKColor Indigo2 = SKColor.Parse("#3949AB");
    static readonly string[] TabIcons = ["🔢", "🔄", "⚙"];
    static readonly string[] TabLabels = ["Rechner", "Umrechner", "Settings"];

    public static AppDef Create() => new(
        "RechnerPlus", Primary,
        DrawIcon, DrawFeature,
        [
            ("Dein smarter\nTaschenrechner!", DrawCalculator),
            ("Wissenschaftlicher\nModus!", DrawScientific),
            ("8 Kategorien\nUmrechner!", DrawConverter),
            ("Berechnungs-\nverlauf!", DrawHistory),
            ("Einheiten schnell\numrechnen!", DrawConverterCategories),
            ("4 schöne\nThemes!", DrawSettings),
        ],
        [
            ("Dein smarter\nTaschenrechner!", DrawCalculator),
            ("Wissenschaftlicher\nModus!", DrawScientific),
            ("8 Kategorien\nUmrechner!", DrawConverter),
            ("4 schöne\nThemes!", DrawSettings),
        ]
    );

    static void DrawIcon(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var c = surface.Canvas;
        IconBg(c, 0, 0, size, Indigo1, Indigo2);
        float s = size / 512f;
        float cx = size / 2f;

        // Calculator body
        using var wp = new SKPaint { Color = SKColors.White, IsAntialias = true };
        RoundRect(c, cx - 110 * s, 120 * s, 220 * s, 300 * s, 20 * s, SKColors.White);

        // Display
        RoundRect(c, cx - 90 * s, 140 * s, 180 * s, 60 * s, 8 * s, Indigo1);
        Text(c, "1,234", cx + 70 * s, 185 * s, 32 * s, SKColor.Parse("#81D4FA"), true);

        // Buttons grid 4x4
        SKColor btnColor = SKColor.Parse("#E8EAF6");
        SKColor opColor = SKColor.Parse("#E91E63");
        float bw = 36 * s, bh = 30 * s, gap = 8 * s;
        float startX = cx - 90 * s, startY = 220 * s;
        string[,] btns = { { "7", "8", "9", "÷" }, { "4", "5", "6", "×" }, { "1", "2", "3", "−" }, { "C", "0", ".", "+" } };

        for (int r = 0; r < 4; r++)
            for (int col = 0; col < 4; col++)
            {
                float bx = startX + col * (bw + gap);
                float by = startY + r * (bh + gap);
                bool isOp = col == 3;
                RoundRect(c, bx, by, bw, bh, 6 * s, isOp ? opColor : btnColor);
                TextC(c, btns[r, col], bx + bw / 2, by + bh / 2 + 5 * s, 16 * s, isOp ? SKColors.White : Indigo1);
            }

        // = button
        RoundRect(c, startX, startY + 4 * (bh + gap), 180 * s, bh, 6 * s, Primary);
        TextC(c, "=", cx, startY + 4 * (bh + gap) + bh / 2 + 5 * s, 20 * s, SKColors.White);

        SavePng(surface, "icon_512.png");
        Console.WriteLine("  RechnerPlus Icon generiert");
    }

    static void DrawFeature()
    {
        const int w = 1024, h = 500;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var c = surface.Canvas;
        FeatureGraphicBase(c, w, h, Primary, (c2, x, y, sz) => {
            IconBg(c2, x, y, sz, Indigo1, Indigo2);
            float s2 = sz / 512f;
            RoundRect(c2, x + sz / 2 - 80 * s2, y + 100 * s2, 160 * s2, 220 * s2, 15 * s2, SKColors.White);
            RoundRect(c2, x + sz / 2 - 65 * s2, y + 115 * s2, 130 * s2, 42 * s2, 6 * s2, Indigo1);
            Text(c2, "1,234", x + sz / 2 + 48 * s2, y + 148 * s2, 24 * s2, SKColor.Parse("#81D4FA"), true);
        },
        "Rechner", "Plus",
        "Taschenrechner • Umrechner • Verlauf",
        [("🔢", Primary, w - 200, 60, 80), ("🔄", Success, w - 110, 160, 70), ("📐", Warning, w - 180, 280, 65), ("📊", Cyan, w - 90, 350, 60)]);
        SavePng(surface, "feature_graphic.png");
        Console.WriteLine("  RechnerPlus Feature Graphic generiert");
    }

    static void DrawCalculator(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🔢  Taschenrechner", x + w / 2, hY + 35, 20, TextPrimary);

        // Display
        float dY = hY + 70;
        RoundRect(c, x + 16, dY, w - 32, 120, 12, Surface);
        Text(c, "245 × 18.5 =", x + 32, dY + 40, 18, TextMuted);
        Text(c, "4.532,50", x + w - 230, dY + 95, 42, TextPrimary, true);

        // Mode Toggle
        float mY = dY + 135;
        RoundRect(c, x + 16, mY, (w - 40) / 2, 36, 8, Primary);
        TextC(c, "Standard", x + 16 + (w - 40) / 4, mY + 24, 14, TextPrimary);
        RoundRect(c, x + 20 + (w - 40) / 2, mY, (w - 40) / 2, 36, 8, Card);
        TextC(c, "Wissenschaftlich", x + 20 + 3 * (w - 40) / 4, mY + 24, 14, TextMuted);

        // Number Pad
        float padY = mY + 50;
        float bw = (w - 60) / 4f - 8;
        float bh = 52;
        string[,] keys = { { "C", "()", "%", "÷" }, { "7", "8", "9", "×" }, { "4", "5", "6", "−" }, { "1", "2", "3", "+" }, { "±", "0", ",", "=" } };
        for (int r = 0; r < 5; r++)
            for (int col = 0; col < 4; col++)
            {
                float bx = x + 24 + col * (bw + 8);
                float by = padY + r * (bh + 8);
                bool isOp = col == 3 || (r == 0 && col >= 2);
                bool isEq = r == 4 && col == 3;
                var bg = isEq ? Primary : isOp ? SKColor.Parse("#4338CA") : Card;
                RoundRect(c, bx, by, bw, bh, 12, bg);
                TextC(c, keys[r, col], bx + bw / 2, by + bh / 2 + 8, 22, TextPrimary);
            }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Primary);
    }

    static void DrawScientific(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🔢  Taschenrechner", x + w / 2, hY + 35, 20, TextPrimary);

        float dY = hY + 70;
        RoundRect(c, x + 16, dY, w - 32, 120, 12, Surface);
        Text(c, "sin(45°) =", x + 32, dY + 40, 18, TextMuted);
        Text(c, "0,7071", x + w - 200, dY + 95, 42, TextPrimary, true);

        float mY = dY + 135;
        RoundRect(c, x + 16, mY, (w - 40) / 2, 36, 8, Card);
        TextC(c, "Standard", x + 16 + (w - 40) / 4, mY + 24, 14, TextMuted);
        RoundRect(c, x + 20 + (w - 40) / 2, mY, (w - 40) / 2, 36, 8, Primary);
        TextC(c, "Wissenschaftlich", x + 20 + 3 * (w - 40) / 4, mY + 24, 14, TextPrimary);

        // Scientific buttons row
        float sY = mY + 50;
        string[] sciFns = ["sin", "cos", "tan", "log", "ln", "√", "x²", "π"];
        float sbw = (w - 48) / 4f - 6;
        for (int i = 0; i < 8; i++)
        {
            float bx = x + 20 + (i % 4) * (sbw + 6);
            float by = sY + (i / 4) * 46;
            RoundRect(c, bx, by, sbw, 40, 8, SKColor.Parse("#4338CA"));
            TextC(c, sciFns[i], bx + sbw / 2, by + 27, 15, TextPrimary);
        }

        // Number pad (compact)
        float padY = sY + 105;
        float bw = (w - 60) / 4f - 8, bh2 = 48;
        string[,] keys = { { "7", "8", "9", "÷" }, { "4", "5", "6", "×" }, { "1", "2", "3", "−" }, { "0", ",", "=", "+" } };
        for (int r = 0; r < 4; r++)
            for (int col = 0; col < 4; col++)
            {
                float bx = x + 24 + col * (bw + 8);
                float by = padY + r * (bh2 + 8);
                var bg = (col == 3 || (r == 3 && col == 2)) ? SKColor.Parse("#4338CA") : Card;
                if (r == 3 && col == 2) bg = Primary;
                RoundRect(c, bx, by, bw, bh2, 12, bg);
                TextC(c, keys[r, col], bx + bw / 2, by + bh2 / 2 + 7, 20, TextPrimary);
            }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Primary);
    }

    static void DrawConverter(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🔄  Umrechner", x + w / 2, hY + 35, 20, TextPrimary);

        // Category chip
        float cY = hY + 70;
        RoundRect(c, x + 16, cY, 90, 34, 17, Primary);
        TextC(c, "Länge", x + 61, cY + 23, 14, TextPrimary);
        RoundRect(c, x + 115, cY, 90, 34, 17, Card);
        TextC(c, "Masse", x + 160, cY + 23, 14, TextMuted);
        RoundRect(c, x + 215, cY, 110, 34, 17, Card);
        TextC(c, "Temperatur", x + 270, cY + 23, 14, TextMuted);

        // From
        float fY = cY + 50;
        RoundRect(c, x + 16, fY, w - 32, 100, 12, Surface);
        Text(c, "Kilometer", x + 32, fY + 30, 16, TextSecondary);
        Text(c, "42,195", x + 32, fY + 75, 36, TextPrimary, true);
        Text(c, "km", x + w - 70, fY + 75, 20, TextMuted);

        // Swap icon
        float swY = fY + 105;
        Circle(c, x + w / 2, swY + 16, 20, Primary);
        TextC(c, "⇅", x + w / 2, swY + 22, 22, SKColors.White);

        // To
        float tY = swY + 38;
        RoundRect(c, x + 16, tY, w - 32, 100, 12, Surface);
        Text(c, "Meilen", x + 32, tY + 30, 16, TextSecondary);
        Text(c, "26,2188", x + 32, tY + 75, 36, Primary, true);
        Text(c, "mi", x + w - 60, tY + 75, 20, TextMuted);

        // Quick conversions
        float qY = tY + 120;
        Text(c, "Weitere Ergebnisse", x + 24, qY, 16, TextSecondary, true);
        string[] units = ["Meter", "Zentimeter", "Fuß", "Yard"];
        string[] vals = ["42.195,00", "4.219.500", "138.435,04", "46.145,01"];
        for (int i = 0; i < 4; i++)
        {
            float iy = qY + 15 + i * 50;
            RoundRect(c, x + 16, iy, w - 32, 42, 8, Surface);
            Text(c, units[i], x + 32, iy + 28, 14, TextSecondary);
            Text(c, vals[i], x + w - 180, iy + 28, 16, TextPrimary, true);
        }

        TabBar(c, x, y + h - 60, w, 1, TabIcons, TabLabels, Primary);
    }

    static void DrawHistory(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🔢  Taschenrechner", x + w / 2, hY + 35, 20, TextPrimary);

        // Display
        float dY = hY + 70;
        RoundRect(c, x + 16, dY, w - 32, 80, 12, Surface);
        Text(c, "= 4.532,50", x + w - 230, dY + 55, 36, TextPrimary, true);

        // History Panel (overlay)
        float pY = dY + 100;
        RoundRect(c, x, pY, w, h - (pY - y) - 60, 16, Surface);

        // History header
        Text(c, "📜  Verlauf", x + 24, pY + 30, 20, TextPrimary, true);
        Text(c, "Löschen", x + w - 100, pY + 30, 14, Error);

        // History items
        string[] exprs = ["245 × 18.5", "1.024 ÷ 4", "sin(45°)", "√(144)", "15% von 2.400", "128 + 372"];
        string[] results = ["= 4.532,50", "= 256", "= 0,7071", "= 12", "= 360", "= 500"];
        for (int i = 0; i < 6; i++)
        {
            float iy = pY + 50 + i * 65;
            if (iy + 55 > y + h - 70) break;
            RoundRect(c, x + 16, iy, w - 32, 55, 8, i == 0 ? Primary.WithAlpha(30) : Card.WithAlpha(120));
            Text(c, exprs[i], x + 32, iy + 24, 15, TextSecondary);
            Text(c, results[i], x + 32, iy + 46, 18, i == 0 ? Primary : TextPrimary, true);
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Primary);
    }

    static void DrawConverterCategories(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🔄  Umrechner", x + w / 2, hY + 35, 20, TextPrimary);

        // Category Grid
        float gY = hY + 75;
        string[] cats = ["📏 Länge", "⚖ Masse", "🌡 Temperatur", "⏱ Zeit", "🧪 Volumen", "📐 Fläche", "🚗 Geschwindigkeit", "💾 Daten"];
        string[] descs = ["km, m, mi, ft, in", "kg, g, lb, oz", "°C, °F, K", "h, min, s, ms", "L, mL, gal, fl oz", "m², km², ha, ft²", "km/h, mph, m/s", "GB, MB, KB, TB"];
        float cardW = (w - 52) / 2;
        float cardH = 95;

        for (int i = 0; i < 8; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float cx = x + 16 + col * (cardW + 12);
            float cy = gY + row * (cardH + 12);
            bool active = i == 0;

            RoundRect(c, cx, cy, cardW, cardH, 12, active ? Primary.WithAlpha(40) : Surface);
            if (active)
            {
                using var bp = new SKPaint { Color = Primary, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                c.DrawRoundRect(new SKRect(cx, cy, cx + cardW, cy + cardH), 12, 12, bp);
            }
            Text(c, cats[i], cx + 16, cy + 35, 17, active ? TextPrimary : TextSecondary, true);
            Text(c, descs[i], cx + 16, cy + 60, 12, TextMuted);
        }

        TabBar(c, x, y + h - 60, w, 1, TabIcons, TabLabels, Primary);
    }

    static void DrawSettings(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "⚙  Einstellungen", x + w / 2, hY + 35, 20, TextPrimary);

        // Theme section
        float sY = hY + 75;
        Text(c, "🎨  Theme", x + 24, sY, 18, TextPrimary, true);
        float tY = sY + 15;
        string[] themes = ["Midnight", "Aurora", "Daylight", "Forest"];
        SKColor[] tColors = [Primary, SKColor.Parse("#EC4899"), SKColor.Parse("#2563EB"), SKColor.Parse("#10B981")];
        SKColor[] tBgs = [SKColor.Parse("#0F172A"), SKColor.Parse("#1C1033"), SKColor.Parse("#F0F4FF"), SKColor.Parse("#022C22")];
        float tw = (w - 52) / 2;
        for (int i = 0; i < 4; i++)
        {
            float tx = x + 16 + (i % 2) * (tw + 12);
            float ty = tY + (i / 2) * 80;
            RoundRect(c, tx, ty, tw, 70, 12, tBgs[i]);
            using var bp = new SKPaint { Color = i == 0 ? tColors[i] : Border, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = i == 0 ? 3 : 1 };
            c.DrawRoundRect(new SKRect(tx, ty, tx + tw, ty + 70), 12, 12, bp);
            Circle(c, tx + 24, ty + 35, 12, tColors[i]);
            Text(c, themes[i], tx + 44, ty + 40, 15, i == 2 ? SKColor.Parse("#1E293B") : TextPrimary, true);
            if (i == 0) TextC(c, "✓", tx + tw - 24, ty + 40, 18, tColors[i]);
        }

        // Language
        float lY = tY + 175;
        Text(c, "🌍  Sprache", x + 24, lY, 18, TextPrimary, true);
        RoundRect(c, x + 16, lY + 15, w - 32, 46, 12, Surface);
        Text(c, "Deutsch", x + 32, lY + 45, 16, TextPrimary);
        Text(c, "🇩🇪", x + w - 65, lY + 46, 20, TextPrimary);

        // About
        float aY = lY + 80;
        Text(c, "ℹ️  Info", x + 24, aY, 18, TextPrimary, true);
        RoundRect(c, x + 16, aY + 15, w - 32, 46, 12, Surface);
        Text(c, "Version", x + 32, aY + 44, 14, TextSecondary);
        Text(c, "v2.0.0", x + w - 100, aY + 44, 14, TextPrimary);

        TabBar(c, x, y + h - 60, w, 2, TabIcons, TabLabels, Primary);
    }
}
