using SkiaSharp;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Renders the interior of a workshop: floor, workbenches, workers, tool wall.
/// Simple pixel-art style with flat fills.
/// </summary>
public class WorkshopInteriorRenderer
{
    // Tier -> Farbe fuer Worker-Kreis
    private static readonly Dictionary<WorkerTier, SKColor> TierColors = new()
    {
        { WorkerTier.F, new SKColor(0x9E, 0x9E, 0x9E) },  // Grey
        { WorkerTier.E, new SKColor(0x4C, 0xAF, 0x50) },  // Green
        { WorkerTier.D, new SKColor(0x21, 0x96, 0xF3) },  // Blue
        { WorkerTier.C, new SKColor(0x9C, 0x27, 0xB0) },  // Purple
        { WorkerTier.B, new SKColor(0xFF, 0xC1, 0x07) },  // Gold
        { WorkerTier.A, new SKColor(0xF4, 0x43, 0x36) },  // Red
        { WorkerTier.S, new SKColor(0xFF, 0x98, 0x00) }   // Orange
    };

    /// <summary>
    /// Renders the interior of a single workshop.
    /// </summary>
    /// <param name="canvas">SkiaSharp canvas to draw on.</param>
    /// <param name="bounds">Available drawing area.</param>
    /// <param name="workshop">Workshop to render.</param>
    public void Render(SKCanvas canvas, SKRect bounds, Workshop workshop)
    {
        DrawFloor(canvas, bounds);
        DrawToolWall(canvas, bounds);
        DrawWorkbenches(canvas, bounds, workshop);
        DrawWorkers(canvas, bounds, workshop);
    }

    private void DrawFloor(SKCanvas canvas, SKRect bounds)
    {
        // Boden: Holz-Farbton
        using (var floorPaint = new SKPaint
        {
            Color = new SKColor(0xBC, 0xAA, 0x84),
            IsAntialias = false
        })
        {
            canvas.DrawRect(bounds, floorPaint);
        }

        // Boden-Linien (Dielenbretter)
        using (var linePaint = new SKPaint
        {
            Color = new SKColor(0xA6, 0x93, 0x72),
            IsAntialias = false,
            StrokeWidth = 1
        })
        {
            float spacing = 16;
            for (float y = bounds.Top + spacing; y < bounds.Bottom; y += spacing)
            {
                canvas.DrawLine(bounds.Left, y, bounds.Right, y, linePaint);
            }
        }

        // Waende (oberer und linker Rand)
        using (var wallPaint = new SKPaint
        {
            Color = new SKColor(0xD7, 0xCC, 0xB7),
            IsAntialias = false
        })
        {
            canvas.DrawRect(bounds.Left, bounds.Top, bounds.Width, 8, wallPaint);
            canvas.DrawRect(bounds.Left, bounds.Top, 8, bounds.Height, wallPaint);
        }
    }

    private void DrawToolWall(SKCanvas canvas, SKRect bounds)
    {
        // Werkzeugwand rechts (dunklerer Streifen)
        float wallWidth = 24;
        float wallX = bounds.Right - wallWidth;
        using (var wallBgPaint = new SKPaint
        {
            Color = new SKColor(0x8D, 0x6E, 0x63),
            IsAntialias = false
        })
        {
            canvas.DrawRect(wallX, bounds.Top + 8, wallWidth, bounds.Height - 8, wallBgPaint);
        }

        // Werkzeuge als kleine Rechtecke an der Wand
        using (var toolPaint = new SKPaint { IsAntialias = false })
        {
            float toolX = wallX + 4;
            float toolY = bounds.Top + 16;

            // Hammer (vertikal)
            toolPaint.Color = new SKColor(0x5D, 0x40, 0x37);
            canvas.DrawRect(toolX + 6, toolY, 4, 18, toolPaint);
            toolPaint.Color = new SKColor(0x78, 0x78, 0x78);
            canvas.DrawRect(toolX + 2, toolY, 12, 5, toolPaint);
            toolY += 24;

            // Saege (horizontal)
            toolPaint.Color = new SKColor(0xB0, 0xB0, 0xB0);
            canvas.DrawRect(toolX, toolY, 16, 3, toolPaint);
            toolPaint.Color = new SKColor(0x5D, 0x40, 0x37);
            canvas.DrawRect(toolX, toolY + 3, 6, 8, toolPaint);
            toolY += 18;

            // Schraubenschluessel (vertikal)
            toolPaint.Color = new SKColor(0x90, 0x90, 0x90);
            canvas.DrawRect(toolX + 5, toolY, 6, 16, toolPaint);
            canvas.DrawRect(toolX + 3, toolY, 10, 4, toolPaint);
            toolY += 22;

            // Zollstock (diagonal dargestellt als L-Form)
            toolPaint.Color = new SKColor(0xFF, 0xC1, 0x07);
            canvas.DrawRect(toolX + 2, toolY, 12, 3, toolPaint);
            canvas.DrawRect(toolX + 2, toolY, 3, 12, toolPaint);
        }
    }

    private void DrawWorkbenches(SKCanvas canvas, SKRect bounds, Workshop workshop)
    {
        int benchCount = workshop.MaxWorkers;
        if (benchCount == 0) benchCount = 1;

        float workArea = bounds.Width - 40; // Links: 8 Wand + Rechts: 24 Werkzeugwand + 8 Abstand
        float workTop = bounds.Top + 16;
        float workHeight = bounds.Height - 24;

        // Baenke in 2 Reihen verteilen
        int cols = Math.Max(1, (int)Math.Ceiling(benchCount / 2.0));
        int rows = benchCount <= cols ? 1 : 2;
        float benchW = Math.Min(28, (workArea - (cols + 1) * 6) / cols);
        float benchH = Math.Min(20, (workHeight - (rows + 1) * 8) / rows);

        using (var benchPaint = new SKPaint
        {
            Color = new SKColor(0x6D, 0x4C, 0x41),
            IsAntialias = false
        })
        using (var benchTopPaint = new SKPaint
        {
            Color = new SKColor(0x8D, 0x6E, 0x63),
            IsAntialias = false
        })
        {
            for (int i = 0; i < benchCount; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float bx = bounds.Left + 16 + col * (benchW + 6);
                float by = workTop + row * (benchH + workHeight / 2);

                // Tisch-Koerper
                canvas.DrawRect(bx, by, benchW, benchH, benchPaint);
                // Tisch-Oberflaeche (hellerer Streifen oben)
                canvas.DrawRect(bx, by, benchW, 3, benchTopPaint);
            }
        }
    }

    private void DrawWorkers(SKCanvas canvas, SKRect bounds, Workshop workshop)
    {
        if (workshop.Workers.Count == 0) return;

        int benchCount = workshop.MaxWorkers;
        if (benchCount == 0) benchCount = 1;
        int cols = Math.Max(1, (int)Math.Ceiling(benchCount / 2.0));

        float workArea = bounds.Width - 40;
        float workTop = bounds.Top + 16;
        float workHeight = bounds.Height - 24;
        float benchW = Math.Min(28, (workArea - (cols + 1) * 6) / cols);
        float benchH = Math.Min(20, (workHeight - 3 * 8) / 2);

        for (int i = 0; i < workshop.Workers.Count; i++)
        {
            var worker = workshop.Workers[i];
            int col = i % cols;
            int row = i / cols;

            float bx = bounds.Left + 16 + col * (benchW + 6);
            float by = workTop + row * (benchH + workHeight / 2);

            // Worker-Position: unterhalb des Tischs
            float workerX = bx + benchW / 2;
            float workerY = by + benchH + 10;
            float radius = 6;

            // Worker-Kreis in Tier-Farbe
            var tierColor = TierColors.GetValueOrDefault(worker.Tier, new SKColor(0x90, 0x90, 0x90));
            using (var workerPaint = new SKPaint { Color = tierColor, IsAntialias = false })
            {
                canvas.DrawCircle(workerX, workerY, radius, workerPaint);
            }

            // Status-Anzeige
            if (worker.IsResting)
            {
                // "Zzz" Text
                using (var zzzPaint = new SKPaint { Color = new SKColor(0x64, 0xB5, 0xF6), IsAntialias = false })
                using (var zzzFont = new SKFont(SKTypeface.Default, 8))
                {
                    canvas.DrawText("Zzz", workerX + radius + 2, workerY - 2, SKTextAlign.Left, zzzFont, zzzPaint);
                }
            }
            else if (worker.IsTraining)
            {
                // Buch-Symbol (kleines Rechteck)
                using (var bookPaint = new SKPaint
                {
                    Color = new SKColor(0x42, 0xA5, 0xF5),
                    IsAntialias = false
                })
                {
                    canvas.DrawRect(workerX + radius + 2, workerY - 4, 6, 8, bookPaint);
                }
                // Buchruecken
                using (var spinePaint = new SKPaint
                {
                    Color = new SKColor(0x1E, 0x88, 0xE5),
                    IsAntialias = false
                })
                {
                    canvas.DrawRect(workerX + radius + 2, workerY - 4, 2, 8, spinePaint);
                }
            }

            // Mood-Indikator (kleiner Punkt rechts oben am Worker)
            var moodColor = GetMoodColor(worker.Mood);
            using (var moodPaint = new SKPaint { Color = moodColor, IsAntialias = false })
            {
                canvas.DrawCircle(workerX + radius - 1, workerY - radius + 1, 3, moodPaint);
            }
        }
    }

    /// <summary>
    /// Returns green/yellow/red based on mood value.
    /// </summary>
    private static SKColor GetMoodColor(decimal mood)
    {
        if (mood >= 70) return new SKColor(0x4C, 0xAF, 0x50); // Green (happy)
        if (mood >= 40) return new SKColor(0xFF, 0xC1, 0x07); // Yellow (neutral)
        return new SKColor(0xF4, 0x43, 0x36);                  // Red (unhappy)
    }
}
