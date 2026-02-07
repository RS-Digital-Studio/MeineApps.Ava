using SkiaSharp;
using static StoreAssetGenerator.Gfx;

namespace StoreAssetGenerator;

static class ZeitManagerApp
{
    static readonly SKColor Blue1 = SKColor.Parse("#0D47A1");
    static readonly SKColor Blue2 = SKColor.Parse("#1976D2");
    static readonly string[] TabIcons = ["⏳", "⏱", "⏰", "⚙"];
    static readonly string[] TabLabels = ["Timer", "Stoppuhr", "Wecker", "Settings"];

    public static AppDef Create() => new(
        "ZeitManager", Cyan,
        DrawIcon, DrawFeature,
        [
            ("Dein Timer\nfür alles!", DrawTimerView),
            ("Präzise\nStoppuhr!", DrawStopwatch),
            ("Wecker mit\nHerausforderungen!", DrawAlarm),
            ("Schichtplan\nim Kalender!", DrawShiftSchedule),
            ("Schnell-Timer\nmit einem Tipp!", DrawQuickTimer),
            ("4 schöne\nThemes!", DrawSettingsView),
        ],
        [
            ("Dein Timer\nfür alles!", DrawTimerView),
            ("Präzise\nStoppuhr!", DrawStopwatch),
            ("Wecker mit\nHerausforderungen!", DrawAlarm),
            ("Schichtplan\nim Kalender!", DrawShiftSchedule),
        ]
    );

    static void DrawIcon(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var c = surface.Canvas;
        IconBg(c, 0, 0, size, Blue1, Blue2);
        float s = size / 512f;
        float cx = size / 2f, cy = size / 2f;

        // Clock face
        Circle(c, cx, cy, 140 * s, Cyan.WithAlpha(60));
        Circle(c, cx, cy, 130 * s, SKColors.White);
        Circle(c, cx, cy, 120 * s, SKColor.Parse("#E3F2FD"));

        // Hour marks
        using var markP = new SKPaint { Color = Blue1, IsAntialias = true, StrokeWidth = 4 * s, StrokeCap = SKStrokeCap.Round };
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30 * MathF.PI / 180;
            float r1 = 105 * s, r2 = 115 * s;
            c.DrawLine(cx + MathF.Sin(angle) * r1, cy - MathF.Cos(angle) * r1,
                       cx + MathF.Sin(angle) * r2, cy - MathF.Cos(angle) * r2, markP);
        }

        // Hour hand (10:10)
        using var hourP = new SKPaint { Color = Blue1, IsAntialias = true, StrokeWidth = 8 * s, StrokeCap = SKStrokeCap.Round };
        float hAngle = -60 * MathF.PI / 180;
        c.DrawLine(cx, cy, cx + MathF.Sin(hAngle) * 65 * s, cy - MathF.Cos(hAngle) * 65 * s, hourP);

        // Minute hand
        using var minP = new SKPaint { Color = Blue1, IsAntialias = true, StrokeWidth = 5 * s, StrokeCap = SKStrokeCap.Round };
        float mAngle = 60 * MathF.PI / 180;
        c.DrawLine(cx, cy, cx + MathF.Sin(mAngle) * 90 * s, cy - MathF.Cos(mAngle) * 90 * s, minP);

        // Center dot
        Circle(c, cx, cy, 8 * s, Cyan);

        SavePng(surface, "icon_512.png");
        Console.WriteLine("  ZeitManager Icon generiert");
    }

    static void DrawFeature()
    {
        const int w = 1024, h = 500;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var c = surface.Canvas;
        FeatureGraphicBase(c, w, h, Cyan, (c2, x, y, sz) => {
            IconBg(c2, x, y, sz, Blue1, Blue2);
            float s2 = sz / 512f;
            Circle(c2, x + sz / 2, y + sz / 2, 80 * s2, SKColors.White);
            Circle(c2, x + sz / 2, y + sz / 2, 70 * s2, SKColor.Parse("#E3F2FD"));
            using var hp = new SKPaint { Color = Blue1, IsAntialias = true, StrokeWidth = 5 * s2, StrokeCap = SKStrokeCap.Round };
            c2.DrawLine(x + sz / 2, y + sz / 2, x + sz / 2 - 30 * s2, y + sz / 2 - 40 * s2, hp);
            c2.DrawLine(x + sz / 2, y + sz / 2, x + sz / 2 + 35 * s2, y + sz / 2 - 25 * s2, hp);
            Circle(c2, x + sz / 2, y + sz / 2, 5 * s2, Cyan);
        },
        "Zeit", "Manager",
        "Timer • Stoppuhr • Wecker • Schichtplan",
        [("⏳", Cyan, w - 200, 60, 80), ("⏱", Warning, w - 110, 160, 70), ("⏰", Error, w - 190, 280, 65), ("📅", Success, w - 80, 340, 60)]);
        SavePng(surface, "feature_graphic.png");
        Console.WriteLine("  ZeitManager Feature Graphic generiert");
    }

    static void DrawTimerView(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "⏳  Timer", x + w / 2, hY + 35, 20, TextPrimary);

        // Quick Timer Chips
        float cY = hY + 70;
        string[] chips = ["1 min", "5 min", "10 min", "15 min", "30 min"];
        float chipX = x + 16;
        foreach (var chip in chips)
        {
            RoundRect(c, chipX, cY, 80, 32, 16, Card);
            TextC(c, chip, chipX + 40, cY + 22, 13, TextSecondary);
            chipX += 88;
        }

        // Active Timer - big circular display
        float tY = cY + 55;
        RoundRect(c, x + 16, tY, w - 32, 280, 16, Surface);

        float ringCx = x + w / 2, ringCy = tY + 130;
        // Ring background
        using var ringBg = new SKPaint { Color = Card, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 12 };
        c.DrawCircle(ringCx, ringCy, 100, ringBg);
        // Ring progress (75%)
        using var ringP = new SKPaint { Color = Cyan, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 12, StrokeCap = SKStrokeCap.Round };
        using var path = new SKPath();
        path.AddArc(new SKRect(ringCx - 100, ringCy - 100, ringCx + 100, ringCy + 100), -90, 270);
        c.DrawPath(path, ringP);

        TextC(c, "07:32", ringCx, ringCy + 15, 42, TextPrimary);
        TextC(c, "verbleibend", ringCx, ringCy + 38, 14, TextSecondary);

        // Pause/Stop buttons
        float btnY = tY + 240;
        RoundRect(c, x + w / 2 - 110, btnY, 100, 36, 18, Warning);
        TextC(c, "⏸  Pause", x + w / 2 - 60, btnY + 24, 14, TextPrimary);
        RoundRect(c, x + w / 2 + 10, btnY, 100, 36, 18, Error);
        TextC(c, "⏹  Stopp", x + w / 2 + 60, btnY + 24, 14, TextPrimary);

        // Timer Name
        TextC(c, "🍳 Kochen", ringCx, tY + 25, 16, TextSecondary);

        // Other timers list
        float lY = tY + 295;
        Text(c, "Weitere Timer", x + 24, lY, 16, TextSecondary, true);
        string[] names = ["Workout", "Wäsche", "Tee"];
        string[] times = ["25:00", "45:00", "03:30"];
        for (int i = 0; i < 3; i++)
        {
            float iy = lY + 15 + i * 52;
            RoundRect(c, x + 16, iy, w - 32, 44, 10, Surface);
            Text(c, names[i], x + 32, iy + 28, 15, TextPrimary);
            Text(c, times[i], x + w - 120, iy + 28, 18, TextMuted, true);
            // Play button
            Circle(c, x + w - 55, iy + 22, 15, Primary.WithAlpha(40));
            TextC(c, "▶", x + w - 55, iy + 28, 14, Primary);
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Cyan);
    }

    static void DrawStopwatch(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "⏱  Stoppuhr", x + w / 2, hY + 35, 20, TextPrimary);

        // Big timer display with ring
        float tY = hY + 80;
        float ringCx = x + w / 2, ringCy = tY + 120;
        using var ringP = new SKPaint { Color = Primary, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 10 };
        c.DrawCircle(ringCx, ringCy, 110, ringP);

        TextC(c, "02:34.82", ringCx, ringCy + 18, 48, TextPrimary);

        // Buttons
        float btnY = tY + 250;
        RoundRect(c, x + w / 2 - 130, btnY, 100, 40, 20, Card);
        TextC(c, "Runde", x + w / 2 - 80, btnY + 27, 15, TextSecondary);
        RoundRect(c, x + w / 2 + 30, btnY, 100, 40, 20, Error);
        TextC(c, "Stopp", x + w / 2 + 80, btnY + 27, 15, TextPrimary);

        // Lap times
        float lY = btnY + 60;
        Text(c, "Rundenzeiten", x + 24, lY, 16, TextSecondary, true);
        string[] laps = ["Runde 5", "Runde 4", "Runde 3", "Runde 2", "Runde 1"];
        string[] lapTimes = ["00:28.14", "00:31.62", "00:29.87", "00:32.41", "00:32.78"];
        string[] totals = ["02:34.82", "02:06.68", "01:35.06", "01:05.19", "00:32.78"];
        for (int i = 0; i < 5; i++)
        {
            float iy = lY + 15 + i * 40;
            if (iy + 35 > y + h - 70) break;
            RoundRect(c, x + 16, iy, w - 32, 34, 6, i == 0 ? Primary.WithAlpha(20) : Surface);
            Text(c, laps[i], x + 32, iy + 24, 13, TextSecondary);
            Text(c, lapTimes[i], x + w / 2 - 30, iy + 24, 14, i == 0 ? Cyan : TextPrimary, true);
            Text(c, totals[i], x + w - 120, iy + 24, 13, TextMuted);
        }

        TabBar(c, x, y + h - 60, w, 1, TabIcons, TabLabels, Cyan);
    }

    static void DrawAlarm(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "⏰  Wecker", x + w / 2, hY + 35, 20, TextPrimary);

        // Toggle bar
        float tY = hY + 70;
        RoundRect(c, x + 16, tY, (w - 40) / 2, 36, 8, Primary);
        TextC(c, "Wecker", x + 16 + (w - 40) / 4, tY + 24, 14, TextPrimary);
        RoundRect(c, x + 20 + (w - 40) / 2, tY, (w - 40) / 2, 36, 8, Card);
        TextC(c, "Schichtplan", x + 20 + 3 * (w - 40) / 4, tY + 24, 14, TextMuted);

        // Alarms
        float aY = tY + 50;
        (string time, string label, string days, bool active)[] alarms = [
            ("06:30", "Arbeit", "Mo Di Mi Do Fr", true),
            ("07:00", "Wochenende", "Sa So", true),
            ("08:00", "Sport", "Mo Mi Fr", false),
            ("22:00", "Schlafenszeit", "Täglich", true),
        ];

        foreach (var (time, label, days, active) in alarms)
        {
            RoundRect(c, x + 16, aY, w - 32, 90, 12, Surface);
            Text(c, time, x + 32, aY + 38, 32, active ? TextPrimary : TextMuted, true);
            Text(c, label, x + 32, aY + 60, 14, TextSecondary);
            Text(c, days, x + 32, aY + 78, 12, TextMuted);

            // Toggle
            float toggleX = x + w - 76;
            RoundRect(c, toggleX, aY + 28, 44, 24, 12, active ? Primary : Card);
            Circle(c, active ? toggleX + 32 : toggleX + 12, aY + 40, 10, SKColors.White);

            aY += 102;
        }

        // FAB
        Circle(c, x + w - 50, y + h - 100, 28, Primary);
        TextC(c, "+", x + w - 50, y + h - 93, 28, SKColors.White);

        TabBar(c, x, y + h - 60, w, 2, TabIcons, TabLabels, Cyan);
    }

    static void DrawShiftSchedule(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "📅  Schichtplan", x + w / 2, hY + 35, 20, TextPrimary);

        // Month header
        float mY = hY + 70;
        TextC(c, "◀  Februar 2026  ▶", x + w / 2, mY + 20, 18, TextPrimary);

        // Day headers
        float cY = mY + 35;
        string[] dayH = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];
        float cellW = (w - 32) / 7f;
        for (int i = 0; i < 7; i++)
            TextC(c, dayH[i], x + 16 + i * cellW + cellW / 2, cY + 14, 12, TextMuted);

        // Calendar grid
        float gY = cY + 25;
        SKColor early = SKColor.Parse("#22D3EE"); // Frueh
        SKColor late = SKColor.Parse("#F59E0B");  // Spaet
        SKColor night = SKColor.Parse("#8B5CF6"); // Nacht
        SKColor free = Success;

        SKColor[] shifts = [
            early, early, early, early, early, free, free,
            late, late, late, late, late, free, free,
            night, night, night, night, night, free, free,
            free, free, early, early, early, early, early,
            late, late, late, free, free, free, free,
        ];

        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 7; col++)
            {
                int idx = row * 7 + col;
                int day = idx - 0 + 1; // Feb starts on Sunday
                if (day < 1 || day > 28) continue;
                float cx = x + 16 + col * cellW;
                float cy2 = gY + row * 44;
                var sc = shifts[idx % shifts.Length];
                RoundRect(c, cx + 2, cy2, cellW - 4, 38, 6, sc.WithAlpha(40));
                TextC(c, day.ToString(), cx + cellW / 2, cy2 + 18, 13, TextPrimary);
                // Shift indicator dot
                Circle(c, cx + cellW / 2, cy2 + 30, 4, sc);
            }

        // Legend
        float leY = gY + 5 * 44 + 15;
        (string label, SKColor color)[] legend = [("Früh", early), ("Spät", late), ("Nacht", night), ("Frei", free)];
        float legX = x + 24;
        foreach (var (label, color) in legend)
        {
            Circle(c, legX + 6, leY + 8, 6, color);
            Text(c, label, legX + 18, leY + 14, 13, TextSecondary);
            legX += 90;
        }

        TabBar(c, x, y + h - 60, w, 2, TabIcons, TabLabels, Cyan);
    }

    static void DrawQuickTimer(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "⏳  Timer", x + w / 2, hY + 35, 20, TextPrimary);

        // Quick chips
        float cY = hY + 70;
        string[] chips = ["1 min", "5 min", "10 min", "15 min", "30 min"];
        float chipX = x + 16;
        foreach (var chip in chips)
        {
            RoundRect(c, chipX, cY, 80, 32, 16, Card);
            TextC(c, chip, chipX + 40, cY + 22, 13, TextSecondary);
            chipX += 88;
        }

        // Custom Timer Creation
        float fY = cY + 55;
        RoundRect(c, x + 16, fY, w - 32, 200, 16, Surface);
        TextC(c, "Neuen Timer erstellen", x + w / 2, fY + 30, 18, TextPrimary);

        // Wheel pickers
        float pY = fY + 50;
        string[] labels2 = ["Stunden", "Minuten", "Sekunden"];
        string[] values = ["00", "10", "00"];
        float pw = (w - 80) / 3f;
        for (int i = 0; i < 3; i++)
        {
            float px = x + 32 + i * (pw + 8);
            TextC(c, labels2[i], px + pw / 2, pY, 12, TextMuted);
            RoundRect(c, px, pY + 10, pw, 100, 12, Card);
            // Highlight center
            RoundRect(c, px + 4, pY + 40, pw - 8, 36, 6, Primary.WithAlpha(30));
            TextC(c, values[i], px + pw / 2, pY + 67, 28, TextPrimary);
            // Above/below numbers
            TextC(c, "—", px + pw / 2, pY + 30, 20, TextMuted);
            TextC(c, "—", px + pw / 2, pY + 100, 20, TextMuted);
        }

        // Name field
        float nY = pY + 120;
        RoundRect(c, x + 32, nY, w - 64, 40, 8, Card);
        Text(c, "Timer-Name (optional)", x + 48, nY + 26, 14, TextMuted);

        // Start button
        RoundRect(c, x + w / 2 - 100, fY + 165, 200, 42, 21, Primary);
        TextC(c, "▶  Timer starten", x + w / 2, fY + 192, 16, TextPrimary);

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Cyan);
    }

    static void DrawSettingsView(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "⚙  Einstellungen", x + w / 2, hY + 35, 20, TextPrimary);

        float sY = hY + 75;
        Text(c, "🎨  Theme", x + 24, sY, 18, TextPrimary, true);
        float tY = sY + 15;
        string[] themes = ["Midnight", "Aurora", "Daylight", "Forest"];
        SKColor[] tColors = [Primary, SKColor.Parse("#EC4899"), SKColor.Parse("#2563EB"), SKColor.Parse("#10B981")];
        SKColor[] tBgs = [Bg, SKColor.Parse("#1C1033"), SKColor.Parse("#F0F4FF"), SKColor.Parse("#022C22")];
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

        // Timer Sound
        float soY = tY + 175;
        Text(c, "🔔  Timer-Ton", x + 24, soY, 18, TextPrimary, true);
        RoundRect(c, x + 16, soY + 15, w - 32, 46, 12, Surface);
        Text(c, "Classic Bell", x + 32, soY + 44, 16, TextPrimary);

        TabBar(c, x, y + h - 60, w, 3, TabIcons, TabLabels, Cyan);
    }
}
