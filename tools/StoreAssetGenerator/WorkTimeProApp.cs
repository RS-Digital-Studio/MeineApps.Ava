using SkiaSharp;
using static StoreAssetGenerator.Gfx;

namespace StoreAssetGenerator;

static class WorkTimeProApp
{
    static readonly SKColor Teal1 = SKColor.Parse("#004D40");
    static readonly SKColor Teal2 = SKColor.Parse("#00897B");
    static readonly SKColor Teal = SKColor.Parse("#009688");
    static readonly string[] TabIcons = ["🕐", "📅", "📆", "📊", "⚙"];
    static readonly string[] TabLabels = ["Heute", "Woche", "Kalender", "Stats", "Settings"];

    public static AppDef Create() => new(
        "WorkTimePro", Teal,
        DrawIcon, DrawFeature,
        [
            ("Arbeitszeit\nerfassen!", DrawToday),
            ("Wochen-\nübersicht!", DrawWeek),
            ("Kalender\nHeatmap!", DrawCalendar),
            ("Statistiken\n& Charts!", DrawStatistics),
            ("Urlaub &\nFeiertage!", DrawVacation),
            ("Export als\nPDF & Excel!", DrawExport),
        ],
        [
            ("Arbeitszeit\nerfassen!", DrawToday),
            ("Kalender\nHeatmap!", DrawCalendar),
            ("Statistiken\n& Charts!", DrawStatistics),
            ("Urlaub &\nFeiertage!", DrawVacation),
        ]
    );

    static void DrawIcon(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var c = surface.Canvas;
        IconBg(c, 0, 0, size, Teal1, Teal2);
        float s = size / 512f, cx = size / 2f, cy = size / 2f - 10 * s;

        // Clock
        Circle(c, cx, cy, 140 * s, SKColors.White);
        Circle(c, cx, cy, 128 * s, SKColor.Parse("#E0F2F1"));

        // Hour marks
        using var markP = new SKPaint { Color = Teal1, IsAntialias = true, StrokeWidth = 4 * s, StrokeCap = SKStrokeCap.Round };
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30 * MathF.PI / 180;
            float r1 = 108 * s, r2 = 120 * s;
            c.DrawLine(cx + MathF.Sin(angle) * r1, cy - MathF.Cos(angle) * r1,
                       cx + MathF.Sin(angle) * r2, cy - MathF.Cos(angle) * r2, markP);
        }

        // Clock hands (9:00)
        using var hourP = new SKPaint { Color = Teal1, IsAntialias = true, StrokeWidth = 8 * s, StrokeCap = SKStrokeCap.Round };
        c.DrawLine(cx, cy, cx - 70 * s, cy, hourP);
        using var minP = new SKPaint { Color = Teal1, IsAntialias = true, StrokeWidth = 5 * s, StrokeCap = SKStrokeCap.Round };
        c.DrawLine(cx, cy, cx, cy - 90 * s, minP);
        Circle(c, cx, cy, 7 * s, Teal);

        // Checkmark badge
        float badgeX = cx + 80 * s, badgeY = cy + 80 * s;
        Circle(c, badgeX, badgeY, 40 * s, Teal);
        Circle(c, badgeX, badgeY, 36 * s, SKColors.White);
        using var checkP = new SKPaint { Color = Teal, IsAntialias = true, StrokeWidth = 8 * s, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke };
        c.DrawLine(badgeX - 15 * s, badgeY, badgeX - 3 * s, badgeY + 14 * s, checkP);
        c.DrawLine(badgeX - 3 * s, badgeY + 14 * s, badgeX + 18 * s, badgeY - 12 * s, checkP);

        SavePng(surface, "icon_512.png");
        Console.WriteLine("  WorkTimePro Icon generiert");
    }

    static void DrawFeature()
    {
        const int w = 1024, h = 500;
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var c = surface.Canvas;
        FeatureGraphicBase(c, w, h, Teal, (c2, x, y, sz) => {
            IconBg(c2, x, y, sz, Teal1, Teal2);
            Circle(c2, x + sz / 2, y + sz / 2, sz * 0.3f, SKColors.White);
            using var hp = new SKPaint { Color = Teal1, IsAntialias = true, StrokeWidth = 4, StrokeCap = SKStrokeCap.Round };
            c2.DrawLine(x + sz / 2, y + sz / 2, x + sz / 2 - 20, y + sz / 2, hp);
            c2.DrawLine(x + sz / 2, y + sz / 2, x + sz / 2, y + sz / 2 - 25, hp);
        },
        "WorkTime", "Pro",
        "Zeiterfassung • Kalender • Statistiken",
        [("🕐", Teal, w - 200, 60, 80), ("📊", Primary, w - 100, 160, 70), ("🏖", Warning, w - 190, 280, 65), ("📋", Success, w - 80, 340, 60)]);
        SavePng(surface, "feature_graphic.png");
        Console.WriteLine("  WorkTimePro Feature Graphic generiert");
    }

    static void DrawToday(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🕐  Heute", x + w / 2, hY + 35, 20, TextPrimary);

        // Date
        float dY = hY + 70;
        TextC(c, "Freitag, 07. Februar 2026", x + w / 2, dY + 14, 16, TextSecondary);

        // Status card
        float sY = dY + 30;
        RoundRect(c, x + 16, sY, w - 32, 140, 16, Surface);
        TextC(c, "Eingecheckt seit 08:15", x + w / 2, sY + 30, 15, Success);

        // Big time display
        TextC(c, "05:24:18", x + w / 2, sY + 80, 48, TextPrimary);
        TextC(c, "Arbeitszeit heute", x + w / 2, sY + 105, 14, TextSecondary);

        // Buttons
        float btnY = sY + 115;
        float btnW2 = (w - 56) / 2;
        RoundRect(c, x + 20, btnY, btnW2, 38, 10, Warning);
        TextC(c, "☕ Pause", x + 20 + btnW2 / 2, btnY + 26, 14, TextPrimary);
        RoundRect(c, x + 28 + btnW2, btnY, btnW2, 38, 10, Error);
        TextC(c, "🏠 Auschecken", x + 28 + btnW2 + btnW2 / 2, btnY + 26, 14, TextPrimary);

        // Time entries
        float eY = sY + 160;
        Text(c, "Zeiteinträge", x + 24, eY, 16, TextPrimary, true);
        (string icon, string type, string time, string dur)[] entries = [
            ("✅", "Arbeit", "08:15 - 10:30", "2h 15m"),
            ("☕", "Pause", "10:30 - 10:45", "15m"),
            ("✅", "Arbeit", "10:45 - 12:00", "1h 15m"),
            ("🍽", "Mittagspause", "12:00 - 12:45", "45m"),
            ("✅", "Arbeit", "12:45 - laufend", "1h 54m"),
        ];

        float ey2 = eY + 15;
        foreach (var (icon, type, time, dur) in entries)
        {
            if (ey2 + 44 > y + h - 70) break;
            RoundRect(c, x + 16, ey2, w - 32, 40, 8, Surface);
            TextC(c, icon, x + 36, ey2 + 27, 14);
            Text(c, type, x + 52, ey2 + 18, 13, TextPrimary);
            Text(c, time, x + 52, ey2 + 34, 11, TextMuted);
            Text(c, dur, x + w - 90, ey2 + 26, 14, Teal, true);
            ey2 += 46;
        }

        // Summary
        float suY = ey2 + 10;
        if (suY + 50 < y + h - 70)
        {
            RoundRect(c, x + 16, suY, w - 32, 45, 10, Teal.WithAlpha(30));
            float hw = (w - 64) / 3;
            Text(c, "Arbeit: 5h 24m", x + 32, suY + 28, 13, Teal, true);
            Text(c, "Pause: 1h 00m", x + 32 + hw, suY + 28, 13, Warning, true);
            Text(c, "Soll: 8h 00m", x + 32 + 2 * hw, suY + 28, 13, TextSecondary);
        }

        TabBar(c, x, y + h - 60, w, 0, TabIcons, TabLabels, Teal);
    }

    static void DrawWeek(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "📅  KW 6 • 2026", x + w / 2, hY + 35, 20, TextPrimary);

        // Week summary
        float sY = hY + 70;
        RoundRect(c, x + 16, sY, w - 32, 70, 12, Surface);
        float hw = (w - 64) / 3;
        StatItem(c, x + 32, sY + 5, "Gearbeitet", "29h 15m", Teal);
        StatItem(c, x + 32 + hw, sY + 5, "Soll", "40h 00m", TextSecondary);
        StatItem(c, x + 32 + 2 * hw, sY + 5, "Differenz", "-10h 45m", Warning);

        // Daily bars
        float bY = sY + 90;
        string[] days2 = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];
        float[] hours = [8.5f, 7.75f, 8.0f, 5.0f, 0, 0, 0];
        float barMax = 10;
        float barH2 = 160;
        float bw = (w - 64) / 7f;

        RoundRect(c, x + 16, bY, w - 32, barH2 + 40, 12, Surface);
        Text(c, "Tagesübersicht", x + 32, bY + 22, 16, TextPrimary, true);

        // 8h target line
        float targetY2 = bY + 35 + barH2 - (8f / barMax * barH2);
        using var targetP = new SKPaint { Color = Warning, IsAntialias = true, StrokeWidth = 1, PathEffect = SKPathEffect.CreateDash([6, 4], 0) };
        c.DrawLine(x + 32, targetY2, x + w - 32, targetY2, targetP);
        Text(c, "8h", x + w - 52, targetY2 - 4, 10, Warning);

        for (int i = 0; i < 7; i++)
        {
            float bx = x + 28 + i * bw;
            float barHi = hours[i] / barMax * barH2;
            var clr = hours[i] >= 8 ? Teal : hours[i] > 0 ? Warning : Card;
            if (barHi > 0)
                RoundRect(c, bx + 4, bY + 35 + barH2 - barHi, bw - 8, barHi, 4, clr);
            else
                RoundRect(c, bx + 4, bY + 35 + barH2 - 4, bw - 8, 4, 4, Card);
            TextC(c, days2[i], bx + bw / 2, bY + 35 + barH2 + 16, 11, i == 4 ? Teal : TextMuted);
            if (hours[i] > 0)
                TextC(c, $"{hours[i]:F1}h", bx + bw / 2, bY + 35 + barH2 - barHi - 8, 10, TextSecondary);
        }

        TabBar(c, x, y + h - 60, w, 1, TabIcons, TabLabels, Teal);
    }

    static void DrawCalendar(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "📆  Februar 2026", x + w / 2, hY + 35, 20, TextPrimary);

        float cY = hY + 70;
        string[] dayH = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];
        float cellW = (w - 32) / 7f;
        for (int i = 0; i < 7; i++)
            TextC(c, dayH[i], x + 16 + i * cellW + cellW / 2, cY + 14, 12, TextMuted);

        float gY = cY + 25;
        // Status colors for each day (1-28)
        SKColor[] statuses = [
            Card, Teal, Teal, Teal, Teal, Teal, Card,  // Woche 1 (Sa=1)
            Teal, Teal, Teal, Teal, Warning, Card, Card,  // Woche 2
            Teal, Teal, Teal, Teal, Teal, Card, Card,  // Woche 3
            Teal, Success, Success, Success, Success, Card, Card,  // Woche 4 (Urlaub Di-Fr)
        ];

        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 7; col++)
            {
                int idx = row * 7 + col;
                int day = idx + 1;
                if (day > 28) continue;
                float cx = x + 16 + col * cellW;
                float cy2 = gY + row * 48;
                var sc = day <= 28 ? statuses[idx] : Card;
                bool isToday = day == 7;

                RoundRect(c, cx + 2, cy2, cellW - 4, 42, 6, sc.WithAlpha(40));
                if (isToday)
                {
                    using var bp = new SKPaint { Color = Teal, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                    c.DrawRoundRect(new SKRect(cx + 2, cy2, cx + cellW - 2, cy2 + 42), 6, 6, bp);
                }
                TextC(c, day.ToString(), cx + cellW / 2, cy2 + 18, 14, isToday ? Teal : TextPrimary);

                // Status indicator
                if (sc == Teal) TextC(c, "✓", cx + cellW / 2, cy2 + 34, 10, Teal);
                else if (sc == Success) TextC(c, "🏖", cx + cellW / 2, cy2 + 35, 10);
                else if (sc == Warning) TextC(c, "!", cx + cellW / 2, cy2 + 34, 10, Warning);
            }

        // Legend
        float leY = gY + 5 * 48 + 10;
        (string label, SKColor color)[] legend = [("Gearbeitet", Teal), ("Urlaub", Success), ("Krank", Warning), ("Frei", Card)];
        float legX = x + 24;
        foreach (var (label, color) in legend)
        {
            Circle(c, legX + 6, leY + 8, 6, color.WithAlpha(150));
            Text(c, label, legX + 18, leY + 14, 12, TextSecondary);
            legX += 100;
        }

        TabBar(c, x, y + h - 60, w, 2, TabIcons, TabLabels, Teal);
    }

    static void DrawStatistics(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "📊  Statistiken", x + w / 2, hY + 35, 20, TextPrimary);

        // Period chips
        float pY = hY + 70;
        string[] periods = ["Woche", "Monat", "Quartal", "Jahr"];
        float chipX = x + 16;
        for (int i = 0; i < 4; i++)
        {
            float cw = 85;
            RoundRect(c, chipX, pY, cw, 30, 15, i == 1 ? Teal : Card);
            TextC(c, periods[i], chipX + cw / 2, pY + 20, 13, i == 1 ? TextPrimary : TextMuted);
            chipX += cw + 8;
        }

        // Summary cards
        float sY = pY + 45;
        float hw = (w - 48) / 2;
        RoundRect(c, x + 16, sY, hw, 65, 12, Surface);
        StatItem(c, x + 28, sY + 5, "Gearbeitet", "156h 30m", Teal);
        RoundRect(c, x + 24 + hw, sY, hw, 65, 12, Surface);
        StatItem(c, x + 36 + hw, sY + 5, "Soll-Stunden", "168h 00m", TextSecondary);

        RoundRect(c, x + 16, sY + 75, hw, 65, 12, Surface);
        StatItem(c, x + 28, sY + 80, "Überstunden", "-11h 30m", Warning);
        RoundRect(c, x + 24 + hw, sY + 75, hw, 65, 12, Surface);
        StatItem(c, x + 36 + hw, sY + 80, "Ø pro Tag", "7h 49m", Primary);

        // Chart
        float chY = sY + 155;
        RoundRect(c, x + 16, chY, w - 32, 180, 12, Surface);
        Text(c, "Arbeitsstunden pro Woche", x + 32, chY + 24, 14, TextPrimary, true);

        // Bar chart
        float[] weekHours = [38.5f, 40.0f, 42.5f, 36.0f, 29.25f];
        string[] weekLabels = ["KW 2", "KW 3", "KW 4", "KW 5", "KW 6"];
        float barW2 = (w - 96) / 5f;
        float chartBase = chY + 160;
        float chartH2 = 110;

        for (int i = 0; i < 5; i++)
        {
            float bx = x + 40 + i * barW2;
            float barHi = weekHours[i] / 45f * chartH2;
            var clr = weekHours[i] >= 40 ? Teal : Warning;
            RoundRect(c, bx + 4, chartBase - barHi, barW2 - 8, barHi, 4, clr);
            TextC(c, weekLabels[i], bx + barW2 / 2, chartBase + 14, 10, TextMuted);
            TextC(c, $"{weekHours[i]:F1}h", bx + barW2 / 2, chartBase - barHi - 10, 10, TextSecondary);
        }

        TabBar(c, x, y + h - 60, w, 3, TabIcons, TabLabels, Teal);
    }

    static void DrawVacation(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "🏖  Urlaub & Feiertage", x + w / 2, hY + 35, 20, TextPrimary);

        // Vacation overview
        float oY = hY + 70;
        RoundRect(c, x + 16, oY, w - 32, 90, 12, Surface);
        float hw = (w - 64) / 3;
        StatItem(c, x + 32, oY + 5, "Gesamt", "30 Tage", Teal);
        StatItem(c, x + 32 + hw, oY + 5, "Genommen", "8 Tage", Warning);
        StatItem(c, x + 32 + 2 * hw, oY + 5, "Verbleibend", "22 Tage", Success);
        Progress(c, x + 32, oY + 72, w - 64, 8, 8f / 30f, Teal);

        // Vacation list
        float lY = oY + 105;
        Text(c, "Meine Urlaubstage", x + 24, lY, 16, TextPrimary, true);

        (string type, string dates, string days, SKColor color)[] vacations = [
            ("🏖 Urlaub", "24. - 28. Feb 2026", "4 Tage", Success),
            ("🏖 Urlaub", "10. - 13. Jan 2026", "4 Tage", Success),
            ("🤒 Krank", "05. Jan 2026", "1 Tag", Warning),
        ];

        float vY = lY + 15;
        foreach (var (type, dates, days, color) in vacations)
        {
            RoundRect(c, x + 16, vY, w - 32, 56, 10, Surface);
            Text(c, type, x + 32, vY + 22, 15, TextPrimary, true);
            Text(c, dates, x + 32, vY + 44, 13, TextSecondary);
            Text(c, days, x + w - 100, vY + 34, 14, color, true);
            vY += 64;
        }

        // Public holidays section
        float fhY = vY + 15;
        Text(c, "Nächste Feiertage", x + 24, fhY, 16, TextPrimary, true);
        (string name, string date)[] holidays = [
            ("Karfreitag", "03.04.2026"),
            ("Ostermontag", "06.04.2026"),
            ("Tag der Arbeit", "01.05.2026"),
        ];
        float hhY = fhY + 15;
        foreach (var (name, date) in holidays)
        {
            if (hhY + 38 > y + h - 70) break;
            RoundRect(c, x + 16, hhY, w - 32, 34, 6, Surface);
            Text(c, $"🎉  {name}", x + 32, hhY + 23, 13, TextPrimary);
            Text(c, date, x + w - 130, hhY + 23, 13, TextMuted);
            hhY += 40;
        }

        Circle(c, x + w - 50, y + h - 100, 28, Primary);
        TextC(c, "+", x + w - 50, y + h - 93, 28, SKColors.White);

        TabBar(c, x, y + h - 60, w, 2, TabIcons, TabLabels, Teal);
    }

    static void DrawExport(SKCanvas c, SKRect b)
    {
        float x = b.Left, y = b.Top, w = b.Width, h = b.Height;
        StatusBar(c, x, y, w);
        float hY = y + 40;
        RoundRect(c, x + 16, hY, w - 32, 55, 12, Surface);
        TextC(c, "📋  Daten exportieren", x + w / 2, hY + 35, 20, TextPrimary);

        // Export options
        float eY = hY + 80;
        (string icon, string format, string desc)[] exports = [
            ("📄", "PDF Bericht", "Monatlicher Arbeitszeitbericht mit allen Details"),
            ("📊", "Excel Tabelle", "Detaillierte Tabelle mit allen Zeiteinträgen"),
            ("📝", "CSV Export", "Kompatibel mit allen Tabellenkalkulationen"),
        ];
        foreach (var (icon, format, desc) in exports)
        {
            RoundRect(c, x + 16, eY, w - 32, 80, 12, Surface);
            RoundRect(c, x + 28, eY + 14, 52, 52, 10, Primary.WithAlpha(40));
            TextC(c, icon, x + 54, eY + 48, 24);
            Text(c, format, x + 92, eY + 34, 17, TextPrimary, true);
            Text(c, desc, x + 92, eY + 56, 12, TextSecondary);
            RoundRect(c, x + w - 110, eY + 24, 80, 34, 10, Primary);
            TextC(c, "Export", x + w - 70, eY + 46, 14, TextPrimary);
            eY += 92;
        }

        // Period selection
        float pY = eY + 20;
        Text(c, "Zeitraum", x + 24, pY, 16, TextPrimary, true);
        RoundRect(c, x + 16, pY + 15, w - 32, 46, 12, Surface);
        Text(c, "Februar 2026", x + 32, pY + 44, 16, TextPrimary);
        Text(c, "📅", x + w - 55, pY + 45, 18, TextMuted);

        TabBar(c, x, y + h - 60, w, 3, TabIcons, TabLabels, Teal);
    }
}
