using HandwerkerImperium.Models.Enums;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert die Branch-Tab-Leiste als SkiaSharp-Grafik.
/// 3 Tabs (Tools/Management/Marketing) mit:
/// - Aktiver Tab: Leuchtender Hintergrund + animierter Unterstrich
/// - Inaktive Tabs: Dezent, transparent
/// - Branch-farbige Icons (Hammer/Aktentasche/Megaphon)
/// - Smooth-Sliding Unterstrich beim Tab-Wechsel
/// </summary>
public class ResearchTabRenderer : IDisposable
{
    private bool _disposed;
    private float _time;

    // Animierter Unterstrich: glättet den Übergang zwischen Tabs
    private float _underlineX;
    private float _underlineTargetX;
    private float _underlineW;
    private bool _initialized;

    // Farben
    private static readonly SKColor BgColor = new(0x22, 0x1A, 0x14);
    private static readonly SKColor TabBg = new(0x2A, 0x1F, 0x18);
    private static readonly SKColor TabBorder = new(0x3A, 0x2C, 0x24);
    private static readonly SKColor TextActive = new(0xF5, 0xF0, 0xEB);
    private static readonly SKColor TextInactive = new(0x80, 0x70, 0x60);

    // Gecachte Paints
    private static readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _text = new() { IsAntialias = true };

    // Gecachte Font- und Path-Objekte (vermeidet Allokationen pro Frame)
    private readonly SKFont _tabFont = new() { Edging = SKFontEdging.Antialias };
    private readonly SKPath _megaphonPath = new();

    /// <summary>
    /// Rendert die Tab-Leiste.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="bounds">Verfügbarer Bereich.</param>
    /// <param name="selectedTab">Aktuell ausgewählter Tab.</param>
    /// <param name="toolsLabel">Lokalisierter Label für Tools-Tab.</param>
    /// <param name="managementLabel">Lokalisierter Label für Management-Tab.</param>
    /// <param name="marketingLabel">Lokalisierter Label für Marketing-Tab.</param>
    /// <param name="deltaTime">Zeitdelta in Sekunden.</param>
    public void Render(SKCanvas canvas, SKRect bounds, ResearchBranch selectedTab,
        string toolsLabel, string managementLabel, string marketingLabel, float deltaTime)
    {
        _time += deltaTime;

        float x = bounds.Left;
        float y = bounds.Top;
        float w = bounds.Width;
        float h = bounds.Height;

        // Hintergrund
        var bgRect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 12);
        _fill.Color = BgColor;
        canvas.DrawRoundRect(bgRect, _fill);

        _stroke.Color = TabBorder;
        _stroke.StrokeWidth = 1;
        canvas.DrawRoundRect(bgRect, _stroke);

        // 3 Tabs
        float tabW = (w - 16) / 3;
        float tabX = x + 4;
        float tabY = y + 4;
        float tabH = h - 8;

        // Unterstrich-Position animieren
        float targetX = tabX + (int)selectedTab * (tabW + 4);
        if (!_initialized)
        {
            _underlineX = targetX;
            _underlineW = tabW;
            _initialized = true;
        }
        _underlineTargetX = targetX;

        // Smooth-Sliding (Lerp)
        float lerpSpeed = 8f * deltaTime;
        _underlineX += (_underlineTargetX - _underlineX) * Math.Min(lerpSpeed, 1.0f);

        // Tabs zeichnen
        DrawTab(canvas, tabX, tabY, tabW, tabH, ResearchBranch.Tools, selectedTab, toolsLabel);
        DrawTab(canvas, tabX + tabW + 4, tabY, tabW, tabH, ResearchBranch.Management, selectedTab, managementLabel);
        DrawTab(canvas, tabX + (tabW + 4) * 2, tabY, tabW, tabH, ResearchBranch.Marketing, selectedTab, marketingLabel);

        // Animierter Unterstrich
        DrawSlidingUnderline(canvas, _underlineX, y + h - 4, tabW, selectedTab);
    }

    /// <summary>
    /// Gibt die Bounds für einen bestimmten Tab zurück (für Touch-Hit-Testing).
    /// </summary>
    public static SKRect GetTabBounds(SKRect containerBounds, ResearchBranch tab)
    {
        float w = containerBounds.Width;
        float tabW = (w - 16) / 3;
        float tabX = containerBounds.Left + 4 + (int)tab * (tabW + 4);
        return new SKRect(tabX, containerBounds.Top, tabX + tabW, containerBounds.Bottom);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TAB RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawTab(SKCanvas canvas, float x, float y, float w, float h,
        ResearchBranch branch, ResearchBranch selectedTab, string label)
    {
        bool isActive = branch == selectedTab;
        var branchColor = ResearchItemRenderer.GetBranchColor(branch);

        // Tab-Hintergrund
        var tabRect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 8);

        if (isActive)
        {
            _fill.Color = branchColor.WithAlpha(40);
            canvas.DrawRoundRect(tabRect, _fill);
        }

        // Branch-Icon (SkiaSharp-gezeichnet)
        float iconCx = x + 16;
        float iconCy = y + h / 2;
        float iconSize = 7;
        DrawBranchIcon(canvas, iconCx, iconCy, iconSize, branch, isActive ? branchColor : TextInactive);

        // Label-Text
        _tabFont.Size = 11;
        _tabFont.Embolden = isActive;
        _text.Color = isActive ? TextActive : TextInactive;

        // Label kürzen wenn nötig
        string displayLabel = label;
        float maxLabelW = w - 32;
        if (_tabFont.MeasureText(displayLabel) > maxLabelW)
        {
            while (displayLabel.Length > 3 && _tabFont.MeasureText(displayLabel + "..") > maxLabelW)
                displayLabel = displayLabel[..^1];
            displayLabel += "..";
        }

        canvas.DrawText(displayLabel, x + 28, y + h / 2 + 4, _tabFont, _text);
    }

    /// <summary>
    /// Zeichnet Branch-spezifische Icons als SkiaSharp-Grafik.
    /// </summary>
    private void DrawBranchIcon(SKCanvas canvas, float cx, float cy, float s,
        ResearchBranch branch, SKColor color)
    {
        _fill.Color = color;
        _stroke.Color = color;
        _stroke.StrokeWidth = s * 0.2f;

        switch (branch)
        {
            case ResearchBranch.Tools:
                // Hammer
                // Stiel
                canvas.DrawLine(cx, cy + s * 0.6f, cx, cy - s * 0.3f, _stroke);
                // Kopf
                _fill.Color = color;
                canvas.DrawRect(cx - s * 0.5f, cy - s * 0.7f, s, s * 0.4f, _fill);
                break;

            case ResearchBranch.Management:
                // Aktentasche
                float bw = s * 0.9f, bh = s * 0.65f;
                canvas.DrawRect(cx - bw, cy - bh * 0.3f, bw * 2, bh, _stroke);
                // Griff
                canvas.DrawArc(new SKRect(cx - s * 0.4f, cy - bh - s * 0.1f, cx + s * 0.4f, cy - bh * 0.3f + s * 0.2f),
                    180, 180, false, _stroke);
                // Schnalle
                canvas.DrawLine(cx, cy - bh * 0.3f, cx, cy + bh * 0.3f, _stroke);
                break;

            case ResearchBranch.Marketing:
                // Megaphon (gecachter Path)
                _megaphonPath.Reset();
                _megaphonPath.MoveTo(cx - s * 0.5f, cy - s * 0.2f);
                _megaphonPath.LineTo(cx + s * 0.6f, cy - s * 0.6f);
                _megaphonPath.LineTo(cx + s * 0.6f, cy + s * 0.6f);
                _megaphonPath.LineTo(cx - s * 0.5f, cy + s * 0.2f);
                _megaphonPath.Close();
                canvas.DrawPath(_megaphonPath, _fill);
                // Griff
                canvas.DrawRect(cx - s * 0.8f, cy - s * 0.15f, s * 0.35f, s * 0.3f, _fill);
                break;
        }
    }

    /// <summary>
    /// Zeichnet den animierten Unterstrich.
    /// </summary>
    private void DrawSlidingUnderline(SKCanvas canvas, float x, float y, float w, ResearchBranch selectedTab)
    {
        var branchColor = ResearchItemRenderer.GetBranchColor(selectedTab);

        // Glow-Effekt
        float glowPulse = 0.6f + MathF.Sin(_time * 3f) * 0.2f;
        _fill.Color = branchColor.WithAlpha((byte)(glowPulse * 80));
        var glowRect = new SKRoundRect(new SKRect(x + 4, y - 2, x + w - 4, y + 4), 2);
        canvas.DrawRoundRect(glowRect, _fill);

        // Unterstrich
        _fill.Color = branchColor;
        var underlineRect = new SKRoundRect(new SKRect(x + 8, y, x + w - 8, y + 3), 1.5f);
        canvas.DrawRoundRect(underlineRect, _fill);
    }

    /// <summary>
    /// Gibt native SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _tabFont?.Dispose();
        _megaphonPath?.Dispose();
    }
}
