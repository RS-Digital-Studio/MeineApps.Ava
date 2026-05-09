using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Empty-State-Illustrationen (Phase 28c — AAA-Audit PR3).
///
/// <para>Prozedurale SkiaSharp-Illustrationen für leere Zustände — kein externes Asset.
/// Royal-Match-Pattern: jeder leere Zustand hat ein eigenes Bild + Hint-Text. Statt
/// generisches "Keine Einträge" zeigt der Spieler eine sympathische Illustration.</para>
///
/// <para>Use-Cases:</para>
/// <list type="bullet">
///   <item>Shop ohne aktive Deals — leerer Shop-Stall mit "Komm bald wieder"-Schild.</item>
///   <item>Achievements ohne Unlocks — leeres Trophäen-Regal.</item>
///   <item>Sammlung ohne Karten — leeres Album-Buch.</item>
///   <item>Liga ohne Spieler — Stadion-Silhouette.</item>
///   <item>Inventar leer — leere Kiste.</item>
/// </list>
/// </summary>
public static class EmptyStateRenderer
{
    /// <summary>Empty-State-Typ → bestimmt das Illustrations-Pattern.</summary>
    public enum EmptyStateType
    {
        Shop,
        Achievements,
        Collection,
        Leaderboard,
        Inbox,
        Friends,
        Cards,
        Generic,
    }

    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKMaskFilter _softGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6);

    /// <summary>
    /// Rendert eine Empty-State-Illustration zentriert in <paramref name="bounds"/>.
    /// Default-Farben passen zum BomberBlast-Theme (Orange/Cyan).
    /// </summary>
    /// <param name="canvas">Ziel-Canvas.</param>
    /// <param name="bounds">Render-Bereich (Illustration wird zentriert).</param>
    /// <param name="type">Empty-State-Typ.</param>
    /// <param name="primary">Haupt-Farbe (Default Orange).</param>
    /// <param name="secondary">Akzent-Farbe (Default Cyan).</param>
    /// <param name="time">Zeit-Akkumulator für Idle-Animationen.</param>
    public static void Draw(SKCanvas canvas, SKRect bounds, EmptyStateType type,
        SKColor primary = default, SKColor secondary = default, float time = 0f)
    {
        if (primary == default) primary = new SKColor(255, 140, 60);
        if (secondary == default) secondary = new SKColor(80, 200, 220);

        var cx = bounds.MidX;
        var cy = bounds.MidY;
        var size = MathF.Min(bounds.Width, bounds.Height) * 0.35f;

        switch (type)
        {
            case EmptyStateType.Shop: DrawShop(canvas, cx, cy, size, primary, secondary, time); break;
            case EmptyStateType.Achievements: DrawTrophyShelf(canvas, cx, cy, size, primary, secondary); break;
            case EmptyStateType.Collection: DrawAlbum(canvas, cx, cy, size, primary, secondary); break;
            case EmptyStateType.Leaderboard: DrawStadium(canvas, cx, cy, size, primary, secondary, time); break;
            case EmptyStateType.Inbox: DrawMailbox(canvas, cx, cy, size, primary, secondary); break;
            case EmptyStateType.Friends: DrawFriendsIcon(canvas, cx, cy, size, primary, secondary); break;
            case EmptyStateType.Cards: DrawCardStack(canvas, cx, cy, size, primary, secondary, time); break;
            default: DrawGeneric(canvas, cx, cy, size, primary, secondary, time); break;
        }
    }

    // === Shop: Stand mit "Geschlossen"-Schild =============================
    private static void DrawShop(SKCanvas canvas, float cx, float cy, float size, SKColor p, SKColor s, float time)
    {
        // Stand-Boden
        _fillPaint.Color = p.WithAlpha(80);
        canvas.DrawRect(cx - size, cy + size * 0.5f, size * 2, size * 0.3f, _fillPaint);
        // Dach (schräg)
        _fillPaint.Color = p;
        using var roof = new SKPath();
        roof.MoveTo(cx - size * 1.2f, cy - size * 0.2f);
        roof.LineTo(cx, cy - size * 0.7f);
        roof.LineTo(cx + size * 1.2f, cy - size * 0.2f);
        roof.Close();
        canvas.DrawPath(roof, _fillPaint);
        // Stützen
        _fillPaint.Color = p.WithAlpha(180);
        canvas.DrawRect(cx - size, cy - size * 0.2f, size * 0.1f, size * 0.7f, _fillPaint);
        canvas.DrawRect(cx + size * 0.9f, cy - size * 0.2f, size * 0.1f, size * 0.7f, _fillPaint);
        // "Geschlossen"-Schild (schwingt subtil)
        var sway = MathF.Sin(time * 1.5f) * 5f;
        _fillPaint.Color = s;
        canvas.DrawRoundRect(new SKRect(cx - size * 0.4f + sway, cy - size * 0.05f,
            cx + size * 0.4f + sway, cy + size * 0.25f), 4, 4, _fillPaint);
        // Strichelung als "Text"-Andeutung
        _strokePaint.Color = SKColors.White;
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawLine(cx - size * 0.25f + sway, cy + size * 0.05f, cx + size * 0.25f + sway, cy + size * 0.05f, _strokePaint);
        canvas.DrawLine(cx - size * 0.2f + sway, cy + size * 0.15f, cx + size * 0.2f + sway, cy + size * 0.15f, _strokePaint);
    }

    // === Achievements: Leeres Trophäen-Regal ==============================
    private static void DrawTrophyShelf(SKCanvas canvas, float cx, float cy, float size, SKColor p, SKColor s)
    {
        // Regal-Linie
        _fillPaint.Color = p.WithAlpha(180);
        canvas.DrawRect(cx - size, cy + size * 0.4f, size * 2, size * 0.1f, _fillPaint);
        // 3 leere Trophäen-Sockel (graue Outlines)
        _strokePaint.Color = s.WithAlpha(120);
        _strokePaint.StrokeWidth = 2f;
        for (int i = -1; i <= 1; i++)
        {
            var x = cx + i * size * 0.6f;
            // Sockel
            canvas.DrawRect(new SKRect(x - size * 0.15f, cy + size * 0.2f,
                x + size * 0.15f, cy + size * 0.4f), _strokePaint);
            // "Schatten" einer fehlenden Trophäe
            using var trophy = new SKPath();
            trophy.MoveTo(x - size * 0.12f, cy + size * 0.2f);
            trophy.LineTo(x - size * 0.18f, cy - size * 0.1f);
            trophy.LineTo(x + size * 0.18f, cy - size * 0.1f);
            trophy.LineTo(x + size * 0.12f, cy + size * 0.2f);
            trophy.Close();
            canvas.DrawPath(trophy, _strokePaint);
        }
    }

    // === Collection: Leeres Album =========================================
    private static void DrawAlbum(SKCanvas canvas, float cx, float cy, float size, SKColor p, SKColor s)
    {
        // Album-Buch-Form
        _fillPaint.Color = p;
        canvas.DrawRoundRect(new SKRect(cx - size * 0.7f, cy - size * 0.5f,
            cx + size * 0.7f, cy + size * 0.5f), 6, 6, _fillPaint);
        // Innerer Rahmen (heller)
        _fillPaint.Color = SKColors.White.WithAlpha(180);
        canvas.DrawRoundRect(new SKRect(cx - size * 0.6f, cy - size * 0.4f,
            cx + size * 0.6f, cy + size * 0.4f), 4, 4, _fillPaint);
        // Mittlere Falz-Linie
        _strokePaint.Color = p.WithAlpha(160);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawLine(cx, cy - size * 0.4f, cx, cy + size * 0.4f, _strokePaint);
        // 4 leere Karten-Slots (gestrichelt)
        _strokePaint.Color = s.WithAlpha(100);
        _strokePaint.StrokeWidth = 1f;
        canvas.DrawRect(cx - size * 0.5f, cy - size * 0.3f, size * 0.4f, size * 0.25f, _strokePaint);
        canvas.DrawRect(cx + size * 0.1f, cy - size * 0.3f, size * 0.4f, size * 0.25f, _strokePaint);
        canvas.DrawRect(cx - size * 0.5f, cy + size * 0.05f, size * 0.4f, size * 0.25f, _strokePaint);
        canvas.DrawRect(cx + size * 0.1f, cy + size * 0.05f, size * 0.4f, size * 0.25f, _strokePaint);
    }

    // === Leaderboard: Stadion-Silhouette ==================================
    private static void DrawStadium(SKCanvas canvas, float cx, float cy, float size, SKColor p, SKColor s, float time)
    {
        // Stadion-Halbkreis
        _strokePaint.Color = p;
        _strokePaint.StrokeWidth = 3f;
        var rect = new SKRect(cx - size * 1.1f, cy - size * 0.4f, cx + size * 1.1f, cy + size * 0.4f);
        canvas.DrawArc(rect, 180, 180, false, _strokePaint);
        // Reihen-Linien
        _strokePaint.Color = s.WithAlpha(160);
        _strokePaint.StrokeWidth = 1.5f;
        for (int r = 1; r <= 3; r++)
        {
            var inset = r * size * 0.12f;
            var inner = new SKRect(rect.Left + inset, rect.Top + inset, rect.Right - inset, rect.Bottom - inset);
            canvas.DrawArc(inner, 180, 180, false, _strokePaint);
        }
        // Spotlight (animiert)
        _fillPaint.Color = s.WithAlpha((byte)(60 + MathF.Sin(time * 2f) * 40));
        _fillPaint.MaskFilter = _softGlow;
        canvas.DrawCircle(cx + MathF.Cos(time) * size * 0.6f, cy - size * 0.1f, size * 0.15f, _fillPaint);
        _fillPaint.MaskFilter = null;
    }

    // === Inbox: Leerer Briefkasten ========================================
    private static void DrawMailbox(SKCanvas canvas, float cx, float cy, float size, SKColor p, SKColor s)
    {
        // Briefkasten-Body
        _fillPaint.Color = p;
        canvas.DrawRoundRect(new SKRect(cx - size * 0.5f, cy - size * 0.3f,
            cx + size * 0.5f, cy + size * 0.3f), 6, 6, _fillPaint);
        // Schlitz
        _fillPaint.Color = SKColors.Black.WithAlpha(180);
        canvas.DrawRect(cx - size * 0.35f, cy - size * 0.05f, size * 0.7f, size * 0.06f, _fillPaint);
        // Fähnchen (steht auf "leer" — also unten)
        _fillPaint.Color = s;
        using var flag = new SKPath();
        flag.MoveTo(cx + size * 0.5f, cy);
        flag.LineTo(cx + size * 0.85f, cy - size * 0.1f);
        flag.LineTo(cx + size * 0.85f, cy + size * 0.1f);
        flag.Close();
        canvas.DrawPath(flag, _fillPaint);
        // Stange
        _strokePaint.Color = s.WithAlpha(200);
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawLine(cx + size * 0.5f, cy - size * 0.1f, cx + size * 0.5f, cy + size * 0.3f, _strokePaint);
    }

    // === Friends: Leere Freundes-Liste (zwei Silhouetten) =================
    private static void DrawFriendsIcon(SKCanvas canvas, float cx, float cy, float size, SKColor p, SKColor s)
    {
        _fillPaint.Color = p.WithAlpha(180);
        // Zwei Personen-Silhouetten nebeneinander
        canvas.DrawCircle(cx - size * 0.35f, cy - size * 0.15f, size * 0.18f, _fillPaint);
        canvas.DrawCircle(cx + size * 0.35f, cy - size * 0.15f, size * 0.18f, _fillPaint);
        // Körper
        canvas.DrawRoundRect(new SKRect(cx - size * 0.55f, cy + size * 0.05f,
            cx - size * 0.15f, cy + size * 0.45f), 8, 8, _fillPaint);
        canvas.DrawRoundRect(new SKRect(cx + size * 0.15f, cy + size * 0.05f,
            cx + size * 0.55f, cy + size * 0.45f), 8, 8, _fillPaint);
        // "+"-Symbol dazwischen
        _strokePaint.Color = s;
        _strokePaint.StrokeWidth = 3f;
        canvas.DrawLine(cx - size * 0.06f, cy, cx + size * 0.06f, cy, _strokePaint);
        canvas.DrawLine(cx, cy - size * 0.06f, cx, cy + size * 0.06f, _strokePaint);
    }

    // === Cards: Karten-Stapel mit Fragezeichen ============================
    private static void DrawCardStack(SKCanvas canvas, float cx, float cy, float size, SKColor p, SKColor s, float time)
    {
        // 3 leicht versetzte Karten (gestrichelt)
        _strokePaint.Color = p.WithAlpha(160);
        _strokePaint.StrokeWidth = 2f;
        for (int i = 2; i >= 0; i--)
        {
            var off = i * 4f;
            canvas.DrawRoundRect(new SKRect(cx - size * 0.4f + off, cy - size * 0.5f + off,
                cx + size * 0.4f + off, cy + size * 0.5f + off), 6, 6, _strokePaint);
        }
        // "?" in der Mitte (animiert pulsierend)
        var pulse = 1f + MathF.Sin(time * 2f) * 0.1f;
        _strokePaint.Color = s;
        _strokePaint.StrokeWidth = 4f * pulse;
        canvas.DrawCircle(cx, cy - size * 0.05f, size * 0.12f * pulse, _strokePaint);
        _fillPaint.Color = s;
        canvas.DrawCircle(cx, cy + size * 0.18f, 2.5f, _fillPaint);
    }

    // === Generic: Leere Kiste mit Question-Mark ===========================
    private static void DrawGeneric(SKCanvas canvas, float cx, float cy, float size, SKColor p, SKColor s, float time)
    {
        // Kiste
        _fillPaint.Color = p.WithAlpha(180);
        canvas.DrawRoundRect(new SKRect(cx - size * 0.6f, cy - size * 0.4f,
            cx + size * 0.6f, cy + size * 0.4f), 6, 6, _fillPaint);
        // Deckel offen (leichte Rotation)
        canvas.Save();
        canvas.RotateDegrees(-15f + MathF.Sin(time) * 3f, cx - size * 0.6f, cy - size * 0.4f);
        _fillPaint.Color = p;
        canvas.DrawRect(cx - size * 0.6f, cy - size * 0.55f, size * 1.2f, size * 0.15f, _fillPaint);
        canvas.Restore();
        // "Leer"-Symbol (Fragezeichen) in der Kiste
        _strokePaint.Color = s;
        _strokePaint.StrokeWidth = 3f;
        canvas.DrawCircle(cx, cy + size * 0.05f, size * 0.12f, _strokePaint);
        _fillPaint.Color = s;
        canvas.DrawCircle(cx, cy + size * 0.25f, 2f, _fillPaint);
    }
}
