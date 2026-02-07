using SkiaSharp;
using static StoreAssetGenerator.Gfx;

namespace StoreAssetGenerator;

static class FinanzRechnerApp
{
    static readonly SKColor Green1 = SKColor.Parse("#1B5E20");
    static readonly SKColor Green2 = SKColor.Parse("#388E3C");
    static readonly string[] TabIcons = ["🏠", "💳", "📊", "⚙"];
    static readonly string[] TabLabels = ["Home", "Tracker", "Stats", "Settings"];

    public static AppDef Create() => new(
        "FinanzRechner", Success,
        DrawIcon, DrawFeature,
        [
            ("Deine Finanzen\nim Griff!", DrawDashboard),
            ("Ausgaben\nerfassen!", DrawExpenseTracker),
            ("Statistiken\n& Charts!", DrawStatistics),
            ("Budgets\nverwalten!", DrawBudgets),
            ("Zinseszins-\nrechner!", DrawCalculator),
            ("4 schöne\nThemes!", DrawSettings),
        ],
        [
            ("Deine Finanzen\nim Griff!", DrawDashboard),
            ("Ausgaben\nerfassen!", DrawExpenseTracker),
            ("Statistiken\n& Charts!", DrawStatistics),
            ("Zinseszins-\nrechner!", DrawCalculator),
        ]
    );

    static void DrawIcon(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var c = surface.Canvas;
        IconBg(c, 0, 0, size, Green1, Green2);
        float s = size / 512f;
        float cx = size / 2f;

        // Euro sign
        using var euroP = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = 200 * s,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextAlign = SKTextAlign.Center };
        c.DrawText("€", cx - 20 * s, cx + 70 * s, euroP);

        // Trend arrow (cyan, going up)
        using var arrowP = new SKPaint { Color = Cyan, IsAntialias = true, StrokeWidth = 14 * s, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke };
        using var path = new SKPath();
        path.MoveTo(cx + 20 * s, 340 * s);
        path.LineTo(cx + 70 * s, 240 * s);
        path.LineTo(cx + 120 * s, 280 * s);
        path.LineTo(cx + 160 * s, 160 * s);
        c.DrawPath(path, arrowP);

        // Arrow head
        using var headP = new SKPaint { Color = Cyan, IsAntialias = true };
        using var head = new SKPath();
        head.MoveTo(cx + 160 * s, 160 * s);
        head.LineTo(cx + 130 * s, 170 * s);
        head.LineTo(cx + 148 * s, 195 * s);
        head.Close();
        c.DrawPath(head, headP);

        SavePng(surface, "icon_512.png");
        Console.WriteLine("  FinanzRechner Icon generiert");
    }

    static void DrawFeature()
    {
        const int w = 1024, h = 500;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var c = surface.Canvas;
        FeatureGraphicBase(c, w, h, Success, (c2, x, y, sz) => {
            IconBg(c2, x, y, sz, Green1, Green2);
            using var ep = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = sz * 0.4f,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                TextAlign = SKTextAlign.Center };
            c2.DrawText("€", x + sz / 2, y + sz * 0.6f, ep);
        },
        "Finanz", "Rechner",
        "Ausgaben • Budgets • Sparplan • Charts",
        [("💰", Success, w - 200, 60, 80), ("📊", Primary, w - 100, 160, 70), ("💳", Warning, w - 190, 280, 65), ("🏦", Cyan, w - 80, 340, 60)]);
        SavePng(surface, "feature_graphic.png");
        Console.WriteLine("  FinanzRechner Feature Graphic generiert");
    }

    static void DrawDashboard(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🏠  Dashboard", x + w / 2, hY + 35, 20, TextPrimary);

        // Balance Card
        float bY = hY + 70;
        RoundRect(c, x + 16, bY, w - 32, 110, 16, Surface);
        Text(c, "Kontostand", x + 32, bY + 28, 14, TextSecondary);
        Text(c, "€ 3.847,52", x + 32, bY + 68, 34, Success, true);
        Text(c, "+€ 245,00 diesen Monat", x + 32, bY + 92, 13, Success);

        // Income/Expense Summary
        float sY = bY + 125;
        float halfW2 = (w - 48) / 2;
        RoundRect(c, x + 16, sY, halfW2, 70, 12, Surface);
        Text(c, "📈  Einnahmen", x + 32, sY + 28, 13, TextSecondary);
        Text(c, "€ 3.200,00", x + 32, sY + 55, 20, Success, true);

        RoundRect(c, x + 28 + halfW2, sY, halfW2, 70, 12, Surface);
        Text(c, "📉  Ausgaben", x + 44 + halfW2, sY + 28, 13, TextSecondary);
        Text(c, "€ 2.955,00", x + 44 + halfW2, sY + 55, 20, Error, true);

        // Recent Transactions
        float tY = sY + 85;
        Text(c, "Letzte Buchungen", x + 24, tY, 16, TextPrimary, true);
        (string icon, string name, string amount, bool isExpense)[] txns = [
            ("🛒", "Supermarkt", "-€ 67,34", true),
            ("⛽", "Tankstelle", "-€ 52,00", true),
            ("💰", "Gehalt", "+€ 3.200,00", false),
            ("🏠", "Miete", "-€ 850,00", true),
            ("📱", "Handy", "-€ 24,99", true),
        ];
        float ty = tY + 15;
        foreach (var (icon, name, amount, isExp) in txns)
        {
            RoundRect(c, x + 16, ty, w - 32, 48, 8, Surface);
            TextC(c, icon, x + 40, ty + 32, 18);
            Text(c, name, x + 60, ty + 32, 15, TextPrimary);
            Text(c, amount, x + w - 160, ty + 32, 15, isExp ? Error : Success, true);
            ty += 56;
        }

        // Calculators section
        float caY = ty + 10;
        if (caY + 80 < y + h - 70)
        {
            Text(c, "Finanzrechner", x + 24, caY, 16, TextPrimary, true);
            string[] calcs = ["💹 Zinseszins", "📈 Sparplan", "🏦 Kredit"];
            float calcX = x + 16;
            foreach (var calc in calcs)
            {
                RoundRect(c, calcX, caY + 15, 130, 36, 10, Card);
                TextC(c, calc, calcX + 65, caY + 39, 13, TextSecondary);
                calcX += 140;
            }
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Success);
    }

    static void DrawExpenseTracker(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "💳  Ausgaben", x + w / 2, hY + 35, 20, TextPrimary);

        // Filter chips
        float fY = hY + 70;
        RoundRect(c, x + 16, fY, 80, 30, 15, Primary);
        TextC(c, "Alle", x + 56, fY + 20, 13, TextPrimary);
        RoundRect(c, x + 104, fY, 100, 30, 15, Card);
        TextC(c, "Essen", x + 154, fY + 20, 13, TextMuted);
        RoundRect(c, x + 212, fY, 100, 30, 15, Card);
        TextC(c, "Wohnen", x + 262, fY + 20, 13, TextMuted);

        // Month summary
        float mY = fY + 44;
        RoundRect(c, x + 16, mY, w - 32, 50, 10, Surface);
        Text(c, "Februar 2026", x + 32, mY + 22, 14, TextSecondary);
        Text(c, "€ 1.847,33", x + 32, mY + 42, 18, Error, true);
        Text(c, "47 Buchungen", x + w - 160, mY + 32, 13, TextMuted);

        // Expense List
        (string icon, string cat, string name, string date, string amount)[] expenses = [
            ("🛒", "Essen", "REWE Einkauf", "07.02.2026", "-€ 67,34"),
            ("☕", "Essen", "Starbucks", "07.02.2026", "-€ 5,40"),
            ("⛽", "Auto", "Shell Tankstelle", "06.02.2026", "-€ 52,00"),
            ("🏠", "Wohnen", "Miete Februar", "01.02.2026", "-€ 850,00"),
            ("📱", "Medien", "Netflix", "01.02.2026", "-€ 12,99"),
            ("💡", "Wohnen", "Strom", "01.02.2026", "-€ 85,00"),
            ("🚌", "Transport", "BVG Monatsticket", "01.02.2026", "-€ 49,00"),
        ];

        float eY = mY + 64;
        foreach (var (icon, cat, name, date, amount) in expenses)
        {
            if (eY + 62 > y + h - 70) break;
            RoundRect(c, x + 16, eY, w - 32, 56, 10, Surface);
            RoundRect(c, x + 26, eY + 10, 38, 38, 8, Card);
            TextC(c, icon, x + 45, eY + 37, 18);
            Text(c, name, x + 74, eY + 26, 14, TextPrimary);
            Text(c, $"{cat} • {date}", x + 74, eY + 44, 11, TextMuted);
            Text(c, amount, x + w - 130, eY + 34, 15, Error, true);
            eY += 64;
        }

        // FAB
        Circle(c, x + w - 50, y + h - 100, 28, Primary);
        TextC(c, "+", x + w - 50, y + h - 93, 28, SKColors.White);

        TabBar(c, x, y + h - 60, w, 1, TabIcons, TabLabels, Success);
    }

    static void DrawStatistics(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "📊  Statistiken", x + w / 2, hY + 35, 20, TextPrimary);

        // Pie chart
        float cY = hY + 75;
        RoundRect(c, x + 16, cY, w - 32, 220, 12, Surface);
        Text(c, "Ausgaben nach Kategorie", x + 32, cY + 28, 16, TextPrimary, true);
        float chartCx = x + 140, chartCy = cY + 135;
        float r = 70;
        SKColor[] segColors = [Error, Warning, Primary, Success, Secondary, Cyan];
        float[] angles = [0, 108, 180, 252, 300, 340];
        for (int i = 0; i < 6; i++)
        {
            using var p = new SKPaint { Color = segColors[i], IsAntialias = true };
            float start = angles[i] - 90;
            float sweep = (i < 5 ? angles[i + 1] : 360) - angles[i];
            using var path = new SKPath();
            path.MoveTo(chartCx, chartCy);
            path.ArcTo(new SKRect(chartCx - r, chartCy - r, chartCx + r, chartCy + r), start, sweep, false);
            path.Close();
            c.DrawPath(path, p);
        }

        // Legend
        string[] cats = ["Wohnen 30%", "Essen 20%", "Transport 15%", "Freizeit 13%", "Medien 12%", "Sonstiges 10%"];
        float legX = x + 240;
        for (int i = 0; i < 6; i++)
        {
            float ly = cY + 60 + i * 26;
            Circle(c, legX + 6, ly + 4, 5, segColors[i]);
            Text(c, cats[i], legX + 18, ly + 10, 12, TextSecondary);
        }

        // Bar chart
        float bY = cY + 235;
        RoundRect(c, x + 16, bY, w - 32, 200, 12, Surface);
        Text(c, "Monatlicher Verlauf", x + 32, bY + 28, 16, TextPrimary, true);

        string[] months = ["Sep", "Okt", "Nov", "Dez", "Jan", "Feb"];
        float[] incomes = [3200, 3200, 3400, 3200, 3200, 3200];
        float[] expensesA = [2800, 2600, 3100, 2900, 2700, 2955];
        float barW = (w - 120) / 6f;
        float maxVal = 3500;
        float barBaseY = bY + 180;
        float barH = 130;

        for (int i = 0; i < 6; i++)
        {
            float bx = x + 48 + i * barW;
            float incH = (incomes[i] / maxVal) * barH;
            float expH = (expensesA[i] / maxVal) * barH;
            RoundRect(c, bx, barBaseY - incH, barW / 2 - 4, incH, 4, Success.WithAlpha(150));
            RoundRect(c, bx + barW / 2, barBaseY - expH, barW / 2 - 4, expH, 4, Error.WithAlpha(150));
            TextC(c, months[i], bx + barW / 2, barBaseY + 14, 10, TextMuted);
        }

        TabBar(c, x, y + h - 60, w, 2, TabIcons, TabLabels, Success);
    }

    static void DrawBudgets(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "💰  Budgets", x + w / 2, hY + 35, 20, TextPrimary);

        (string icon, string name, float spent, float budget, SKColor color)[] budgets = [
            ("🛒", "Lebensmittel", 320, 500, Success),
            ("🏠", "Wohnen", 850, 900, Warning),
            ("🚗", "Transport", 145, 150, Error),
            ("🎬", "Freizeit", 80, 200, Success),
            ("📱", "Medien", 62, 80, Warning),
            ("👕", "Kleidung", 0, 100, Success),
        ];

        float bY = hY + 75;
        foreach (var (icon, name, spent, budget, color) in budgets)
        {
            if (bY + 90 > y + h - 70) break;
            RoundRect(c, x + 16, bY, w - 32, 82, 12, Surface);
            TextC(c, icon, x + 44, bY + 30, 22);
            Text(c, name, x + 66, bY + 28, 16, TextPrimary, true);
            Text(c, $"€ {spent:N0} / € {budget:N0}", x + 66, bY + 50, 13, TextSecondary);
            float pct = spent / budget;
            Progress(c, x + 32, bY + 62, w - 64, 8, pct, pct > 0.9f ? Error : pct > 0.7f ? Warning : Success);
            Text(c, $"{(int)(pct * 100)}%", x + w - 65, bY + 50, 13, pct > 0.9f ? Error : TextMuted);
            bY += 94;
        }

        Circle(c, x + w - 50, y + h - 100, 28, Primary);
        TextC(c, "+", x + w - 50, y + h - 93, 28, SKColors.White);

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Success);
    }

    static void DrawCalculator(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "💹  Zinseszinsrechner", x + w / 2, hY + 35, 20, TextPrimary);

        // Input fields
        float fY = hY + 75;
        (string label, string value)[] fields = [
            ("Anfangskapital", "€ 10.000"),
            ("Monatliche Einzahlung", "€ 200"),
            ("Zinssatz p.a.", "5,0 %"),
            ("Laufzeit", "20 Jahre"),
        ];
        foreach (var (label, value) in fields)
        {
            RoundRect(c, x + 16, fY, w - 32, 58, 12, Surface);
            Text(c, label, x + 32, fY + 22, 13, TextSecondary);
            Text(c, value, x + 32, fY + 46, 18, TextPrimary, true);
            fY += 66;
        }

        // Calculate button
        RoundRect(c, x + 16, fY, w - 32, 44, 12, Primary);
        TextC(c, "Berechnen", x + w / 2, fY + 30, 17, TextPrimary);

        // Result
        float rY = fY + 60;
        RoundRect(c, x + 16, rY, w - 32, 130, 12, Surface);
        Text(c, "Ergebnis", x + 32, rY + 28, 16, TextPrimary, true);
        Text(c, "Endkapital", x + 32, rY + 55, 13, TextSecondary);
        Text(c, "€ 92.785,34", x + 32, rY + 82, 28, Success, true);
        float halfW2 = (w - 80) / 2;
        Text(c, "Einzahlungen", x + 32, rY + 105, 12, TextSecondary);
        Text(c, "€ 58.000", x + 32, rY + 122, 15, Primary, true);
        Text(c, "Zinserträge", x + 40 + halfW2, rY + 105, 12, TextSecondary);
        Text(c, "€ 34.785", x + 40 + halfW2, rY + 122, 15, Gold, true);

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Success);
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

        float lY = sY + 190;
        Text(c, "💱  Währung", x + 24, lY, 18, TextPrimary, true);
        RoundRect(c, x + 16, lY + 15, w - 32, 46, 12, Surface);
        Text(c, "Euro (€)", x + 32, lY + 44, 16, TextPrimary);

        TabBar(c, x, y + h - 60, w, 3, TabIcons, TabLabels, Success);
    }
}
