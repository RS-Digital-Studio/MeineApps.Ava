using System;
using HandwerkerImperium.Models.Enums;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Daten die pro Workshop-Karte an den Renderer uebergeben werden.
/// Struct fuer GC-freie Uebergabe.
/// </summary>
public struct WorkshopCardData
{
    public WorkshopType Type;
    public int Level;
    public int WorkerCount;
    public int MaxWorkers;
    public bool IsUnlocked;
    public bool CanBuyUnlock;
    public bool CanAffordUpgrade;
    public bool CanAffordUnlock;
    public bool IsMaxLevel;
    public float LevelProgress;
    public float MilestoneProgress;
    public int NextMilestone;
    public bool ShowMilestone;
    public string IncomeText;
    public string UpgradeCostText;
    public string NetIncomeText;
    public bool IsNetNegative;
    public int UnlockLevel;
}

/// <summary>
/// Rendert eine komplette Workshop-Karte als SkiaSharp.
/// Nutzt GameCardRenderer fuer Rahmen, Buttons, Icons etc.
/// Nutzt WorkshopCardRenderer fuer die Workshop-Illustrations-Szene.
///
/// Layout einer Karte (bei ~170x220dp):
/// - Header (40%): Workshop-Farbgradient + Mini-Szene
/// - Level-Badge: Oben-rechts auf dem Header
/// - Stats-Bereich (35%): Worker + Income
/// - Upgrade-Button (25%): 3D-Button am unteren Rand
///
/// Gesperrte Karte: Verdunkelt + grosses Schloss
/// Freischaltbare Karte: Leichter Glow + offenes Schloss + Kosten
/// </summary>
public static class WorkshopGameCardRenderer
{
    // Gecachte Paints
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White };
    private static readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

    /// <summary>
    /// Rendert eine einzelne Workshop-Karte in den angegebenen Bereich.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds, WorkshopCardData data, float time)
    {
        if (data.IsUnlocked)
            RenderUnlockedCard(canvas, bounds, data, time);
        else if (data.CanBuyUnlock)
            RenderUnlockableCard(canvas, bounds, data, time);
        else
            RenderLockedCard(canvas, bounds, data);
    }

    /// <summary>
    /// Gibt die Bounds des Upgrade-Buttons zurueck (fuer HitTest).
    /// Muss nach Render() mit denselben Bounds aufgerufen werden.
    /// </summary>
    public static SKRect GetUpgradeButtonBounds(SKRect cardBounds)
    {
        float buttonH = cardBounds.Height * 0.16f;
        float buttonPadding = 6f;
        return new SKRect(
            cardBounds.Left + buttonPadding + 3f, // +3 fuer Rahmen
            cardBounds.Bottom - buttonH - buttonPadding - 3f,
            cardBounds.Right - buttonPadding - 3f,
            cardBounds.Bottom - buttonPadding - 3f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Freigeschaltete Karte
    // ═══════════════════════════════════════════════════════════════════════

    private static void RenderUnlockedCard(SKCanvas canvas, SKRect bounds, WorkshopCardData data, float time)
    {
        var wsColor = WorkshopCardRenderer.GetWorkshopColor(data.Type);
        var tier = GameCardRenderer.GetFrameTier(data.Level);

        // Holz-Rahmen
        GameCardRenderer.DrawWoodFrame(canvas, bounds, tier);

        float frameW = 3f;
        var inner = new SKRect(bounds.Left + frameW, bounds.Top + frameW, bounds.Right - frameW, bounds.Bottom - frameW);
        float innerH = inner.Height;

        // === Header (40%): Workshop-Gradient + Szene ===
        float headerH = innerH * 0.40f;
        var headerBounds = new SKRect(inner.Left, inner.Top, inner.Right, inner.Top + headerH);

        // Workshop-Farbgradient als Header-Hintergrund
        using (var headerShader = SKShader.CreateLinearGradient(
            new SKPoint(headerBounds.MidX, headerBounds.Top),
            new SKPoint(headerBounds.MidX, headerBounds.Bottom),
            new[] { wsColor.WithAlpha(180), wsColor.WithAlpha(60) },
            null,
            SKShaderTileMode.Clamp))
        {
            _fillPaint.Shader = headerShader;
            canvas.DrawRect(headerBounds, _fillPaint);
            _fillPaint.Shader = null;
        }

        // Workshop-Illustration rendern (bestehender Renderer)
        canvas.Save();
        canvas.ClipRect(headerBounds);
        WorkshopCardRenderer.Render(canvas, headerBounds, data.Type, true, data.Level);
        canvas.Restore();

        // Level-Badge oben-rechts
        GameCardRenderer.DrawLevelBadge(canvas, inner.Right - 16f, inner.Top + 14f, data.Level, 22f);

        // Krone bei Lv.1000+
        if (data.Level >= 1000)
        {
            GameCardRenderer.DrawCrown(canvas, inner.Right - 16f, inner.Top - 2f, 16f, 10f);
        }

        // === Stats-Bereich (35%): Worker + Income ===
        float statsTop = inner.Top + headerH + 2f;
        float statsH = innerH * 0.35f;

        // Dunkler Hintergrund fuer Stats
        _fillPaint.Color = new SKColor(0x18, 0x18, 0x20, 200);
        canvas.DrawRect(inner.Left, statsTop, inner.Width, statsH, _fillPaint);

        float textX = inner.Left + 8f;
        float lineH = statsH / 4f;

        // Zeile 1: Workshop-Name
        _textPaint.Color = wsColor;
        _textPaint.TextSize = 11f;
        _textPaint.TextAlign = SKTextAlign.Left;
        _textPaint.FakeBoldText = true;
        canvas.DrawText(GetWorkshopName(data.Type), textX, statsTop + lineH * 0.8f, _textPaint);
        _textPaint.FakeBoldText = false;

        // Zeile 2: Worker-Anzeige (Helm-Icons + Zahl)
        float workerY = statsTop + lineH * 1.7f;
        DrawWorkerIcons(canvas, textX, workerY, data.WorkerCount, data.MaxWorkers, wsColor);

        // Zeile 3: Einkommen
        float incomeY = statsTop + lineH * 2.7f;
        GameCardRenderer.DrawCoinIcon(canvas, textX + 5f, incomeY, 5f);
        _textPaint.Color = data.IsNetNegative ? new SKColor(0xEF, 0x44, 0x44) : new SKColor(0x22, 0xC5, 0x5E);
        _textPaint.TextSize = 10f;
        canvas.DrawText(data.NetIncomeText ?? "", textX + 14f, incomeY + 3.5f, _textPaint);

        // Zeile 4: Level-Progress
        float progressY = statsTop + lineH * 3.4f;
        var progressBounds = new SKRect(inner.Left + 6f, progressY, inner.Right - 6f, progressY + 5f);

        // Workshop-Farbe fuer ProgressBar
        var barStart = wsColor.WithAlpha(200);
        var barEnd = wsColor;
        GameCardRenderer.DrawProgressBar(canvas, progressBounds, data.LevelProgress, barStart, barEnd, 5f);

        // Milestone-Anzeige (falls vorhanden)
        if (data.ShowMilestone)
        {
            _textPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 180);
            _textPaint.TextSize = 8f;
            _textPaint.TextAlign = SKTextAlign.Right;
            canvas.DrawText($"\u2192 Lv.{data.NextMilestone}", inner.Right - 8f, progressY - 1f, _textPaint);
        }

        // === Upgrade-Button (25%) ===
        var buttonBounds = GetUpgradeButtonBounds(bounds);

        if (data.IsMaxLevel)
        {
            // Max-Level: Goldener Badge statt Button
            _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 40);
            canvas.DrawRoundRect(buttonBounds, 8f, 8f, _fillPaint);
            _textPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
            _textPaint.TextSize = 11f;
            _textPaint.TextAlign = SKTextAlign.Center;
            _textPaint.FakeBoldText = true;
            canvas.DrawText("MAX", buttonBounds.MidX, buttonBounds.MidY + 4f, _textPaint);
            _textPaint.FakeBoldText = false;
        }
        else
        {
            // Upgrade-Button
            string upgradeText = data.UpgradeCostText ?? "";
            bool enabled = data.CanAffordUpgrade;
            var btnColor = enabled ? wsColor : new SKColor(0x50, 0x50, 0x58);
            GameCardRenderer.Draw3DButton(canvas, buttonBounds, btnColor, upgradeText, 10f, enabled, 6f);

            // Pfeil-Icon links im Button
            if (enabled)
            {
                _textPaint.Color = SKColors.White;
                _textPaint.TextSize = 12f;
                _textPaint.TextAlign = SKTextAlign.Left;
                canvas.DrawText("\u2191", buttonBounds.Left + 6f, buttonBounds.MidY + 4f, _textPaint);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Freischaltbare Karte (Level erreicht, Geld fehlt evtl.)
    // ═══════════════════════════════════════════════════════════════════════

    private static void RenderUnlockableCard(SKCanvas canvas, SKRect bounds, WorkshopCardData data, float time)
    {
        var wsColor = WorkshopCardRenderer.GetWorkshopColor(data.Type);

        // Holz-Rahmen (kein Level-Tier)
        GameCardRenderer.DrawWoodFrame(canvas, bounds, CardFrameTier.None);

        float frameW = 3f;
        var inner = new SKRect(bounds.Left + frameW, bounds.Top + frameW, bounds.Right - frameW, bounds.Bottom - frameW);

        // Dunkler Hintergrund mit Workshop-Farbschein
        _fillPaint.Color = new SKColor(0x18, 0x18, 0x20, 230);
        canvas.DrawRect(inner, _fillPaint);

        // Workshop-Farbglow von oben
        using (var glowShader = SKShader.CreateLinearGradient(
            new SKPoint(inner.MidX, inner.Top),
            new SKPoint(inner.MidX, inner.Top + inner.Height * 0.4f),
            new[] { wsColor.WithAlpha(60), SKColors.Transparent },
            null,
            SKShaderTileMode.Clamp))
        {
            _fillPaint.Shader = glowShader;
            canvas.DrawRect(inner, _fillPaint);
            _fillPaint.Shader = null;
        }

        // Pulsierender Glow-Rahmen (zeigt an: freischaltbar)
        byte glowAlpha = (byte)(40 + 25 * MathF.Sin(time * 3f));
        _strokePaint.Color = wsColor.WithAlpha(glowAlpha);
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawRoundRect(bounds.Left + 1f, bounds.Top + 1f, bounds.Width - 2f, bounds.Height - 2f, 10f, 10f, _strokePaint);
        _strokePaint.StrokeWidth = 1f;

        // Workshop-Name
        _textPaint.Color = wsColor;
        _textPaint.TextSize = 13f;
        _textPaint.TextAlign = SKTextAlign.Center;
        _textPaint.FakeBoldText = true;
        canvas.DrawText(GetWorkshopName(data.Type), inner.MidX, inner.Top + inner.Height * 0.25f, _textPaint);
        _textPaint.FakeBoldText = false;

        // Offenes Schloss
        GameCardRenderer.DrawLockIcon(canvas, inner.MidX, inner.Top + inner.Height * 0.35f, 28f, isOpen: true);

        // "Tippe zum Freischalten"
        _textPaint.Color = new SKColor(0x22, 0xC5, 0x5E, 200);
        _textPaint.TextSize = 9f;
        canvas.DrawText("Tippe zum Freischalten", inner.MidX, inner.Top + inner.Height * 0.68f, _textPaint);

        // Kosten-Anzeige
        float costY = inner.Top + inner.Height * 0.78f;
        GameCardRenderer.DrawCoinIcon(canvas, inner.MidX - 20f, costY, 6f);
        _textPaint.Color = data.CanAffordUnlock ? new SKColor(0x22, 0xC5, 0x5E) : new SKColor(0xEF, 0x44, 0x44);
        _textPaint.TextSize = 11f;
        canvas.DrawText(data.UpgradeCostText ?? "", inner.MidX, costY + 4f, _textPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Gesperrte Karte (Level nicht erreicht)
    // ═══════════════════════════════════════════════════════════════════════

    private static void RenderLockedCard(SKCanvas canvas, SKRect bounds, WorkshopCardData data)
    {
        // Holz-Rahmen (gedimmt)
        GameCardRenderer.DrawWoodFrame(canvas, bounds, CardFrameTier.None);

        float frameW = 3f;
        var inner = new SKRect(bounds.Left + frameW, bounds.Top + frameW, bounds.Right - frameW, bounds.Bottom - frameW);

        // Sehr dunkler Hintergrund
        _fillPaint.Color = new SKColor(0x12, 0x12, 0x18, 240);
        canvas.DrawRect(inner, _fillPaint);

        // Workshop-Name (gedimmt)
        var wsColor = WorkshopCardRenderer.GetWorkshopColor(data.Type);
        _textPaint.Color = wsColor.WithAlpha(100);
        _textPaint.TextSize = 12f;
        _textPaint.TextAlign = SKTextAlign.Center;
        _textPaint.FakeBoldText = true;
        canvas.DrawText(GetWorkshopName(data.Type), inner.MidX, inner.Top + inner.Height * 0.3f, _textPaint);
        _textPaint.FakeBoldText = false;

        // Grosses geschlossenes Schloss
        GameCardRenderer.DrawLockIcon(canvas, inner.MidX, inner.Top + inner.Height * 0.4f, 32f, isOpen: false);

        // "Ab Level X"
        _textPaint.Color = new SKColor(0x94, 0xA3, 0xB8, 160);
        _textPaint.TextSize = 10f;
        canvas.DrawText($"Ab Level {data.UnlockLevel}", inner.MidX, inner.Top + inner.Height * 0.75f, _textPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Worker-Icons
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet kleine Helm-Icons fuer die Worker-Anzeige.
    /// </summary>
    private static void DrawWorkerIcons(SKCanvas canvas, float x, float y, int workerCount, int maxWorkers, SKColor wsColor)
    {
        // Helm-Icons (max 5 anzeigen, Rest als Zahl)
        int showIcons = Math.Min(workerCount, 5);
        float iconSpacing = 10f;

        for (int i = 0; i < showIcons; i++)
        {
            float ix = x + i * iconSpacing + 4f;
            // Mini-Helm
            _iconPaint.Color = wsColor.WithAlpha(200);
            canvas.DrawRoundRect(ix - 3.5f, y - 5f, 7f, 5f, 1.5f, 1.5f, _iconPaint);
            // Helmschirm
            _iconPaint.Color = wsColor.WithAlpha(150);
            canvas.DrawRect(ix - 4.5f, y - 1f, 9f, 2f, _iconPaint);
        }

        // Zahl dahinter
        float textX = x + showIcons * iconSpacing + 8f;
        _textPaint.Color = new SKColor(0xB0, 0xB0, 0xB8);
        _textPaint.TextSize = 9f;
        _textPaint.TextAlign = SKTextAlign.Left;
        canvas.DrawText($"{workerCount}/{maxWorkers}", textX, y + 2f, _textPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Workshop-Namen (kurz, fuer Karten)
    // ═══════════════════════════════════════════════════════════════════════

    private static string GetWorkshopName(WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => "Tischlerei",
        WorkshopType.Plumber => "Sanit\u00e4r",
        WorkshopType.Electrician => "Elektrik",
        WorkshopType.Painter => "Malerei",
        WorkshopType.Roofer => "Dachdeckerei",
        WorkshopType.Contractor => "Renovierung",
        WorkshopType.Architect => "Architektur",
        WorkshopType.GeneralContractor => "Generalunternehmer",
        _ => "Werkstatt"
    };
}
