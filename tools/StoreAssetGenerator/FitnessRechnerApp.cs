using SkiaSharp;
using static StoreAssetGenerator.Gfx;

namespace StoreAssetGenerator;

static class FitnessRechnerApp
{
    static readonly SKColor Pink1 = SKColor.Parse("#880E4F");
    static readonly SKColor Pink2 = SKColor.Parse("#C2185B");
    static readonly SKColor Pink = SKColor.Parse("#E91E63");
    static readonly string[] TabIcons = ["🏠", "📈", "🍎", "⚙"];
    static readonly string[] TabLabels = ["Home", "Fortschritt", "Food", "Settings"];

    public static AppDef Create() => new(
        "FitnessRechner", Pink,
        DrawIcon, DrawFeature,
        [
            ("Dein Fitness-\nRechner!", DrawDashboard),
            ("BMI &\nKörperfett!", DrawBmi),
            ("Kalorien\nzählen!", DrawCalories),
            ("Wasser\ntrinken!", DrawWater),
            ("Nahrungsmittel\nsuchen!", DrawFoodSearch),
            ("Fortschritt\nverfolgen!", DrawProgress),
        ],
        [
            ("Dein Fitness-\nRechner!", DrawDashboard),
            ("BMI &\nKörperfett!", DrawBmi),
            ("Kalorien\nzählen!", DrawCalories),
            ("Fortschritt\nverfolgen!", DrawProgress),
        ]
    );

    static void DrawIcon(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var c = surface.Canvas;
        IconBg(c, 0, 0, size, Pink1, Pink2);
        float s = size / 512f, cx = size / 2f, cy = size / 2f;

        // Heart
        using var wp = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var path = new SKPath();
        float hx = cx, hy = cy + 20 * s;
        path.MoveTo(hx, hy + 80 * s);
        path.CubicTo(hx - 160 * s, hy - 30 * s, hx - 100 * s, hy - 140 * s, hx, hy - 60 * s);
        path.CubicTo(hx + 100 * s, hy - 140 * s, hx + 160 * s, hy - 30 * s, hx, hy + 80 * s);
        c.DrawPath(path, wp);

        // EKG line across heart
        using var ekgP = new SKPaint { Color = Pink, IsAntialias = true, StrokeWidth = 8 * s, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke };
        using var ekgPath = new SKPath();
        float ey = cy + 5 * s;
        ekgPath.MoveTo(cx - 120 * s, ey);
        ekgPath.LineTo(cx - 60 * s, ey);
        ekgPath.LineTo(cx - 35 * s, ey - 50 * s);
        ekgPath.LineTo(cx - 10 * s, ey + 45 * s);
        ekgPath.LineTo(cx + 15 * s, ey - 60 * s);
        ekgPath.LineTo(cx + 40 * s, ey + 30 * s);
        ekgPath.LineTo(cx + 60 * s, ey);
        ekgPath.LineTo(cx + 120 * s, ey);
        c.DrawPath(ekgPath, ekgP);

        SavePng(surface, "icon_512.png");
        Console.WriteLine("  FitnessRechner Icon generiert");
    }

    static void DrawFeature()
    {
        const int w = 1024, h = 500;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var c = surface.Canvas;
        FeatureGraphicBase(c, w, h, Pink, (c2, x, y, sz) => {
            IconBg(c2, x, y, sz, Pink1, Pink2);
            using var wp2 = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = sz * 0.35f,
                TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName("Segoe UI Emoji") };
            c2.DrawText("❤️", x + sz / 2, y + sz * 0.6f, wp2);
        },
        "Fitness", "Rechner",
        "BMI • Kalorien • Wasser • Körperfett",
        [("💪", Pink, w - 200, 60, 80), ("🏃", Success, w - 100, 160, 70), ("🥗", Warning, w - 190, 280, 65), ("💧", Cyan, w - 80, 340, 60)]);
        SavePng(surface, "feature_graphic.png");
        Console.WriteLine("  FitnessRechner Feature Graphic generiert");
    }

    static void DrawDashboard(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "💪  FitnessRechner", x + w / 2, hY + 35, 20, TextPrimary);

        (string icon, string name, string desc, SKColor color)[] calcs = [
            ("⚖", "BMI Rechner", "Body Mass Index berechnen", Primary),
            ("🔥", "Kalorien Rechner", "Täglichen Kalorienbedarf ermitteln", Warning),
            ("💧", "Wasser Rechner", "Optimale Trinkmenge berechnen", Cyan),
            ("📏", "Idealgewicht", "Idealgewicht nach Formeln", Success),
            ("📊", "Körperfett", "Körperfettanteil schätzen", Pink),
        ];

        float cY = hY + 75;
        foreach (var (icon, name, desc, color) in calcs)
        {
            RoundRect(c, x + 16, cY, w - 32, 80, 12, Surface);
            RoundRect(c, x + 28, cY + 14, 52, 52, 10, color.WithAlpha(40));
            TextC(c, icon, x + 54, cY + 48, 24);
            Text(c, name, x + 92, cY + 34, 17, TextPrimary, true);
            Text(c, desc, x + 92, cY + 56, 13, TextSecondary);
            // Arrow
            TextC(c, "›", x + w - 40, cY + 42, 24, TextMuted);
            cY += 92;
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Pink);
    }

    static void DrawBmi(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "⚖  BMI Rechner", x + w / 2, hY + 35, 20, TextPrimary);

        float fY = hY + 75;
        (string label, string val)[] fields = [("Gewicht (kg)", "78,5"), ("Größe (cm)", "180"), ("Alter", "32"), ("Geschlecht", "Männlich")];
        foreach (var (label, val) in fields)
        {
            RoundRect(c, x + 16, fY, w - 32, 50, 10, Surface);
            Text(c, label, x + 32, fY + 20, 13, TextSecondary);
            Text(c, val, x + w - 130, fY + 34, 16, TextPrimary, true);
            fY += 56;
        }

        RoundRect(c, x + 16, fY, w - 32, 44, 12, Pink);
        TextC(c, "Berechnen", x + w / 2, fY + 30, 17, TextPrimary);

        // Result
        float rY = fY + 60;
        RoundRect(c, x + 16, rY, w - 32, 170, 12, Surface);
        TextC(c, "Dein BMI", x + w / 2, rY + 30, 16, TextSecondary);
        TextC(c, "24,2", x + w / 2, rY + 75, 48, Success);
        TextC(c, "Normalgewicht", x + w / 2, rY + 100, 18, Success);

        // BMI Scale
        float scY = rY + 120;
        SKColor[] scaleColors = [Cyan, Success, Warning, Error];
        string[] scaleLabels = ["< 18.5", "18.5-25", "25-30", "> 30"];
        float sw = (w - 64) / 4f;
        for (int i = 0; i < 4; i++)
        {
            float sx = x + 32 + i * sw;
            RoundRect(c, sx, scY, sw - 4, 12, 4, scaleColors[i]);
            TextC(c, scaleLabels[i], sx + sw / 2, scY + 26, 10, TextMuted);
        }
        // Marker
        float markerX = x + 32 + 1.43f * sw;
        using var markerP = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var markerPath = new SKPath();
        markerPath.MoveTo(markerX, scY - 3);
        markerPath.LineTo(markerX - 5, scY - 10);
        markerPath.LineTo(markerX + 5, scY - 10);
        markerPath.Close();
        c.DrawPath(markerPath, markerP);

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Pink);
    }

    static void DrawCalories(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🔥  Kalorien Tracking", x + w / 2, hY + 35, 20, TextPrimary);

        // Daily overview
        float dY = hY + 70;
        RoundRect(c, x + 16, dY, w - 32, 200, 16, Surface);

        // Circular progress
        float ringCx = x + w / 2, ringCy = dY + 100;
        using var ringBg2 = new SKPaint { Color = Card, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 14 };
        c.DrawCircle(ringCx, ringCy, 70, ringBg2);
        using var ringP = new SKPaint { Color = Success, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 14, StrokeCap = SKStrokeCap.Round };
        using var arc = new SKPath();
        arc.AddArc(new SKRect(ringCx - 70, ringCy - 70, ringCx + 70, ringCy + 70), -90, 240);
        c.DrawPath(arc, ringP);

        TextC(c, "1.540", ringCx, ringCy + 8, 32, TextPrimary);
        TextC(c, "/ 2.100 kcal", ringCx, ringCy + 30, 13, TextSecondary);
        TextC(c, "560 kcal übrig", ringCx, ringCy + 50, 12, Success);

        // Macros
        float mY = dY + 215;
        float thirdW = (w - 64) / 3;
        (string label, string val, float pct, SKColor color)[] macros = [
            ("Protein", "82g / 120g", 0.68f, Cyan),
            ("Kohlenhydrate", "180g / 260g", 0.69f, Warning),
            ("Fett", "45g / 70g", 0.64f, Pink),
        ];
        for (int i = 0; i < 3; i++)
        {
            float mx = x + 24 + i * (thirdW + 8);
            Text(c, macros[i].label, mx, mY + 14, 11, TextSecondary);
            Text(c, macros[i].val, mx, mY + 32, 12, TextPrimary);
            Progress(c, mx, mY + 40, thirdW - 8, 6, macros[i].pct, macros[i].color);
        }

        // Meals
        float meY = mY + 65;
        Text(c, "Heute", x + 24, meY, 16, TextPrimary, true);
        (string icon, string meal, string cal)[] meals = [
            ("🥣", "Frühstück: Müsli + Joghurt", "420 kcal"),
            ("🥗", "Mittagessen: Salat + Hähnchen", "580 kcal"),
            ("🍌", "Snack: Banane", "105 kcal"),
            ("🍝", "Abendessen: Pasta", "435 kcal"),
        ];
        foreach (var (icon, meal, cal) in meals)
        {
            meY += 42;
            if (meY + 36 > y + h - 70) break;
            RoundRect(c, x + 16, meY, w - 32, 36, 8, Surface);
            TextC(c, icon, x + 38, meY + 25, 16);
            Text(c, meal, x + 56, meY + 24, 13, TextPrimary);
            Text(c, cal, x + w - 110, meY + 24, 13, Warning, true);
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Pink);
    }

    static void DrawWater(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "💧  Wasser Tracking", x + w / 2, hY + 35, 20, TextPrimary);

        // Water glass visualization
        float gY = hY + 80;
        RoundRect(c, x + 16, gY, w - 32, 250, 16, Surface);
        float glassCx = x + w / 2;
        float glassTop = gY + 30, glassBot = gY + 210;
        float glassW = 120;

        // Glass outline
        using var glassP = new SKPaint { Color = Cyan.WithAlpha(60), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
        c.DrawRoundRect(new SKRect(glassCx - glassW / 2, glassTop, glassCx + glassW / 2, glassBot), 8, 8, glassP);

        // Water fill (75%)
        float waterH = (glassBot - glassTop) * 0.75f;
        RoundRect(c, glassCx - glassW / 2 + 3, glassBot - waterH, glassW - 6, waterH - 3, 6, Cyan.WithAlpha(80));

        TextC(c, "1.875 ml", glassCx, gY + 135, 28, TextPrimary);
        TextC(c, "/ 2.500 ml", glassCx, gY + 158, 14, TextSecondary);
        TextC(c, "75%", glassCx, gY + 185, 20, Cyan);

        // Quick add buttons
        float aY = gY + 260;
        string[] adds = ["+150ml", "+250ml", "+330ml", "+500ml"];
        float btnW2 = (w - 60) / 4f;
        for (int i = 0; i < 4; i++)
        {
            float bx = x + 20 + i * (btnW2 + 5);
            RoundRect(c, bx, aY, btnW2, 38, 10, Cyan.WithAlpha(40));
            TextC(c, adds[i], bx + btnW2 / 2, aY + 26, 14, Cyan);
        }

        // History
        float hiY = aY + 55;
        Text(c, "Heute", x + 24, hiY, 16, TextPrimary, true);
        string[] times = ["08:00", "10:30", "12:15", "14:00", "16:30"];
        string[] amounts = ["250 ml", "330 ml", "500 ml", "250 ml", "545 ml"];
        for (int i = 0; i < 5; i++)
        {
            float hy2 = hiY + 15 + i * 38;
            if (hy2 + 30 > y + h - 70) break;
            RoundRect(c, x + 16, hy2, w - 32, 32, 6, Surface);
            Text(c, $"💧  {times[i]}", x + 32, hy2 + 22, 13, TextSecondary);
            Text(c, amounts[i], x + w - 110, hy2 + 22, 14, Cyan, true);
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Pink);
    }

    static void DrawFoodSearch(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🍎  Nahrungsmittel", x + w / 2, hY + 35, 20, TextPrimary);

        // Search bar
        float sY = hY + 70;
        RoundRect(c, x + 16, sY, w - 32, 46, 23, Card);
        Text(c, "🔍  Banane", x + 40, sY + 30, 16, TextPrimary);

        // Results
        (string icon, string name, string cal, string portion)[] results = [
            ("🍌", "Banane", "89 kcal", "100g"),
            ("🍌", "Banane getrocknet", "346 kcal", "100g"),
            ("🍞", "Bananenbrot", "326 kcal", "100g"),
            ("🥤", "Bananen-Smoothie", "89 kcal", "200ml"),
            ("🧁", "Bananen-Muffin", "295 kcal", "1 Stk."),
            ("🍦", "Bananen-Eis", "167 kcal", "100g"),
        ];

        float rY = sY + 58;
        foreach (var (icon, name, cal, portion) in results)
        {
            if (rY + 62 > y + h - 70) break;
            RoundRect(c, x + 16, rY, w - 32, 56, 10, Surface);
            RoundRect(c, x + 26, rY + 8, 40, 40, 8, Card);
            TextC(c, icon, x + 46, rY + 36, 20);
            Text(c, name, x + 78, rY + 28, 15, TextPrimary);
            Text(c, $"{cal} • {portion}", x + 78, rY + 48, 12, TextSecondary);
            // Add button
            Circle(c, x + w - 46, rY + 28, 15, Primary.WithAlpha(40));
            TextC(c, "+", x + w - 46, rY + 34, 18, Primary);
            rY += 64;
        }

        TabBar(c, x, y + h - 60, w, 2, TabIcons, TabLabels, Pink);
    }

    static void DrawProgress(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "📈  Fortschritt", x + w / 2, hY + 35, 20, TextPrimary);

        // Tab chips
        float cY = hY + 70;
        string[] tabs = ["Gewicht", "Kalorien", "Wasser", "Körperfett"];
        float chipX = x + 16;
        for (int i = 0; i < 4; i++)
        {
            float cw = i == 0 ? 90 : 90;
            RoundRect(c, chipX, cY, cw, 30, 15, i == 0 ? Pink : Card);
            TextC(c, tabs[i], chipX + cw / 2, cY + 20, 12, i == 0 ? TextPrimary : TextMuted);
            chipX += cw + 8;
        }

        // Weight Chart
        float chY = cY + 45;
        RoundRect(c, x + 16, chY, w - 32, 220, 12, Surface);
        Text(c, "Gewichtsverlauf", x + 32, chY + 28, 16, TextPrimary, true);
        Text(c, "Letzte 30 Tage", x + w - 150, chY + 28, 12, TextMuted);

        // Chart area
        float chartX = x + 48, chartY = chY + 50, chartW2 = w - 96, chartH2 = 140;

        // Grid lines
        using var gridP = new SKPaint { Color = Card, IsAntialias = true, StrokeWidth = 1 };
        for (int i = 0; i <= 4; i++)
        {
            float gy = chartY + i * chartH2 / 4;
            c.DrawLine(chartX, gy, chartX + chartW2, gy, gridP);
            Text(c, $"{82 - i * 2}", chartX - 30, gy + 5, 10, TextMuted);
        }

        // Line chart
        float[] weights = [80.5f, 80.2f, 79.8f, 80.0f, 79.5f, 79.2f, 78.8f, 79.0f, 78.5f, 78.2f];
        using var lineP = new SKPaint { Color = Pink, IsAntialias = true, StrokeWidth = 3, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        using var linePath = new SKPath();
        for (int i = 0; i < weights.Length; i++)
        {
            float lx = chartX + i * chartW2 / (weights.Length - 1);
            float ly = chartY + (82 - weights[i]) / 8 * chartH2;
            if (i == 0) linePath.MoveTo(lx, ly);
            else linePath.LineTo(lx, ly);
        }
        c.DrawPath(linePath, lineP);

        // Current weight
        float cwY = chY + 230;
        RoundRect(c, x + 16, cwY, w - 32, 80, 12, Surface);
        float hw = (w - 80) / 3;
        StatItem(c, x + 32, cwY + 10, "Aktuell", "78,2 kg", Pink);
        StatItem(c, x + 32 + hw, cwY + 10, "Ziel", "75,0 kg", Success);
        StatItem(c, x + 32 + 2 * hw, cwY + 10, "Verloren", "-2,3 kg", Cyan);

        TabBar(c, x, y + h - 60, w, 1, TabIcons, TabLabels, Pink);
    }
}
