using System;
using System.Collections.Generic;
using HandwerkerImperium.Icons;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Daten die pro Workshop-Karte an den Renderer uebergeben werden.
/// Struct fuer GC-freie Uebergabe.
/// </summary>
public struct WorkshopCardData
{
    public WorkshopType Type;
    public string Name;
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
    public string TimeToUpgrade;
    public int RebirthStars;
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
    // AI-Asset-Service für Workshop-Hintergründe in den Karten
    private static IGameAssetService? _assetService;

    // Workshop-Typ → GameIcon für Button-Symbole
    private static readonly Dictionary<WorkshopType, GameIconKind> _workshopIcons = new()
    {
        { WorkshopType.Carpenter, GameIconKind.HandSaw },
        { WorkshopType.Plumber, GameIconKind.Pipe },
        { WorkshopType.Electrician, GameIconKind.LightningBolt },
        { WorkshopType.Painter, GameIconKind.Palette },
        { WorkshopType.Roofer, GameIconKind.Hammer },
        { WorkshopType.Contractor, GameIconKind.HammerWrench },
        { WorkshopType.Architect, GameIconKind.Compass },
        { WorkshopType.GeneralContractor, GameIconKind.Crown },
        { WorkshopType.MasterSmith, GameIconKind.Anvil },
        { WorkshopType.InnovationLab, GameIconKind.FlaskOutline },
    };

    // Workshop-Typ → Asset-Dateiname
    private static readonly Dictionary<WorkshopType, string> _assetNames = new()
    {
        { WorkshopType.Carpenter, "carpenter" },
        { WorkshopType.Plumber, "plumber" },
        { WorkshopType.Electrician, "electrician" },
        { WorkshopType.Painter, "painter" },
        { WorkshopType.Roofer, "roofer" },
        { WorkshopType.Contractor, "contractor" },
        { WorkshopType.Architect, "architect" },
        { WorkshopType.GeneralContractor, "general_contractor" },
        { WorkshopType.MasterSmith, "master_smith" },
        { WorkshopType.InnovationLab, "innovation_lab" },
    };

    /// <summary>
    /// Initialisiert den Renderer mit dem Asset-Service für AI-Workshop-Bilder.
    /// </summary>
    public static void Initialize(IGameAssetService assetService) => _assetService = assetService;

    // Gecachte Paints
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White };
    private static readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

    // Gecachte Header-Shader pro WorkshopType (vermeidet Shader-Allokation pro Karte pro Frame)
    private static readonly Dictionary<WorkshopType, SKShader> _headerShaderCache = new();
    private static float _lastHeaderW, _lastHeaderH;

    // Gecachte Glow-Shader fuer Unlockable-Karten (1 pro WorkshopType)
    private static readonly Dictionary<WorkshopType, SKShader> _glowShaderCache = new();
    private static float _lastGlowInnerH;

    // Gecachte Fonts (verschiedene Groessen fuer Text-Rendering)
    private static readonly SKFont _font8 = new() { Size = 8f };
    private static readonly SKFont _font9 = new() { Size = 9f };
    private static readonly SKFont _font10 = new() { Size = 10f };
    private static readonly SKFont _font11 = new() { Size = 11f };
    private static readonly SKFont _font11Bold = new() { Size = 11f, Embolden = true };
    private static readonly SKFont _font12 = new() { Size = 12f };
    private static readonly SKFont _font12Bold = new() { Size = 12f, Embolden = true };
    private static readonly SKFont _font13Bold = new() { Size = 13f, Embolden = true };

    // Gecachte String-Caches (vermeidet String-Interpolation pro Karte pro Frame)
    private static readonly Dictionary<int, string> _milestoneLabelCache = new();
    private static readonly Dictionary<int, string> _unlockLevelLabelCache = new();
    private static readonly Dictionary<(int, int), string> _workerCountLabelCache = new();

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

        // Workshop-Farbgradient als Header-Hintergrund (gecacht pro WorkshopType)
        // Shader nur neu erstellen wenn sich die Header-Groesse geaendert hat
        // (alle Karten haben dieselbe Groesse, daher reicht ein globaler Check)
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (_lastHeaderW != headerBounds.Width || _lastHeaderH != headerBounds.Height)
        {
            // Bounds geaendert → Cache invalidieren
            foreach (var s in _headerShaderCache.Values) s.Dispose();
            _headerShaderCache.Clear();
            _lastHeaderW = headerBounds.Width;
            _lastHeaderH = headerBounds.Height;
        }
        // ReSharper restore CompareOfFloatsByEqualityOperator
        if (!_headerShaderCache.TryGetValue(data.Type, out var cachedHeaderShader))
        {
            cachedHeaderShader = SKShader.CreateLinearGradient(
                new SKPoint(headerBounds.MidX, headerBounds.Top),
                new SKPoint(headerBounds.MidX, headerBounds.Bottom),
                new[] { wsColor.WithAlpha(180), wsColor.WithAlpha(60) },
                null,
                SKShaderTileMode.Clamp);
            _headerShaderCache[data.Type] = cachedHeaderShader;
        }
        _fillPaint.Shader = cachedHeaderShader;
        canvas.DrawRect(headerBounds, _fillPaint);
        _fillPaint.Shader = null;

        // Workshop-Illustration: AI-Bild
        canvas.Save();
        canvas.ClipRect(headerBounds);
        var aiBg = TryGetWorkshopBitmap(data.Type);
        if (aiBg != null)
            canvas.DrawBitmap(aiBg, headerBounds);
        canvas.Restore();

        // Level-Badge oben-rechts
        GameCardRenderer.DrawLevelBadge(canvas, inner.Right - 16f, inner.Top + 14f, data.Level, 22f);

        // Krone bei Lv.1000+
        if (data.Level >= 1000)
        {
            GameCardRenderer.DrawCrown(canvas, inner.Right - 16f, inner.Top - 2f, 16f, 10f);
        }

        // Rebirth-Sterne oben-links (gold gefuellt fuer aktive, leer fuer fehlende)
        if (data.RebirthStars > 0)
        {
            DrawRebirthStars(canvas, inner.Left + 6f, inner.Top + 6f, data.RebirthStars, 5);
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
        canvas.DrawText(data.Name ?? "", textX, statsTop + lineH * 0.8f, SKTextAlign.Left, _font11Bold, _textPaint);

        // Zeile 2: Worker-Anzeige (Helm-Icons + Zahl)
        float workerY = statsTop + lineH * 1.7f;
        DrawWorkerIcons(canvas, textX, workerY, data.WorkerCount, data.MaxWorkers, wsColor);

        // Zeile 3: Einkommen
        float incomeY = statsTop + lineH * 2.7f;
        GameCardRenderer.DrawCoinIcon(canvas, textX + 5f, incomeY, 5f);
        _textPaint.Color = data.IsNetNegative ? new SKColor(0xEF, 0x44, 0x44) : new SKColor(0x22, 0xC5, 0x5E);
        canvas.DrawText(data.NetIncomeText ?? "", textX + 14f, incomeY + 3.5f, SKTextAlign.Left, _font10, _textPaint);

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
            if (!_milestoneLabelCache.TryGetValue(data.NextMilestone, out var milestoneLabel))
            {
                milestoneLabel = $"\u2192 Lv.{data.NextMilestone}";
                _milestoneLabelCache[data.NextMilestone] = milestoneLabel;
            }
            canvas.DrawText(milestoneLabel, inner.Right - 8f, progressY - 1f, SKTextAlign.Right, _font8, _textPaint);
        }

        // Time-to-Upgrade Anzeige (unter Fortschrittsbalken, vor Button)
        if (!string.IsNullOrEmpty(data.TimeToUpgrade) && !data.CanAffordUpgrade && !data.IsMaxLevel)
        {
            _textPaint.Color = new SKColor(0xFF, 0xA5, 0x00, 180); // Amber dezent
            canvas.DrawText(data.TimeToUpgrade, inner.Right - 8f, progressY + 11f, SKTextAlign.Right, _font8, _textPaint);
        }

        // === Upgrade-Button (25%) ===
        var buttonBounds = GetUpgradeButtonBounds(bounds);

        if (data.IsMaxLevel)
        {
            // Max-Level: Goldener Badge statt Button
            _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 40);
            canvas.DrawRoundRect(buttonBounds, 8f, 8f, _fillPaint);
            _textPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
            canvas.DrawText("MAX", buttonBounds.MidX, buttonBounds.MidY + 4f, SKTextAlign.Center, _font11Bold, _textPaint);
        }
        else
        {
            // Upgrade-Button
            string upgradeText = data.UpgradeCostText ?? "";
            bool enabled = data.CanAffordUpgrade;
            var btnColor = enabled ? wsColor : new SKColor(0x50, 0x50, 0x58);
            GameCardRenderer.Draw3DButton(canvas, buttonBounds, btnColor, upgradeText, 10f, enabled, 6f);

            // Workshop-Icon links im Button (statt Pfeil)
            if (_workshopIcons.TryGetValue(data.Type, out var iconKind))
            {
                _iconPaint.Color = enabled ? SKColors.White : new SKColor(0x80, 0x80, 0x88);
                float iconSize = buttonBounds.Height * 0.5f;
                GameIconRenderer.DrawAt(canvas, iconKind,
                    buttonBounds.Left + iconSize + 4f, buttonBounds.MidY, iconSize, _iconPaint);
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

        // Workshop-Farbglow von oben (gecacht pro WorkshopType)
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (_lastGlowInnerH != inner.Height)
        {
            foreach (var s in _glowShaderCache.Values) s.Dispose();
            _glowShaderCache.Clear();
            _lastGlowInnerH = inner.Height;
        }
        // ReSharper restore CompareOfFloatsByEqualityOperator
        if (!_glowShaderCache.TryGetValue(data.Type, out var cachedGlowShader))
        {
            cachedGlowShader = SKShader.CreateLinearGradient(
                new SKPoint(inner.MidX, inner.Top),
                new SKPoint(inner.MidX, inner.Top + inner.Height * 0.4f),
                new[] { wsColor.WithAlpha(60), SKColors.Transparent },
                null,
                SKShaderTileMode.Clamp);
            _glowShaderCache[data.Type] = cachedGlowShader;
        }
        _fillPaint.Shader = cachedGlowShader;
        canvas.DrawRect(inner, _fillPaint);
        _fillPaint.Shader = null;

        // Pulsierender Glow-Rahmen (zeigt an: freischaltbar)
        byte glowAlpha = (byte)(40 + 25 * MathF.Sin(time * 3f));
        _strokePaint.Color = wsColor.WithAlpha(glowAlpha);
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawRoundRect(bounds.Left + 1f, bounds.Top + 1f, bounds.Width - 2f, bounds.Height - 2f, 10f, 10f, _strokePaint);
        _strokePaint.StrokeWidth = 1f;

        // Workshop-Name
        _textPaint.Color = wsColor;
        canvas.DrawText(data.Name ?? "", inner.MidX, inner.Top + inner.Height * 0.25f, SKTextAlign.Center, _font13Bold, _textPaint);

        // Offenes Schloss
        GameCardRenderer.DrawLockIcon(canvas, inner.MidX, inner.Top + inner.Height * 0.35f, 28f, isOpen: true);

        // "Tippe zum Freischalten"
        _textPaint.Color = new SKColor(0x22, 0xC5, 0x5E, 200);
        canvas.DrawText("Tippe zum Freischalten", inner.MidX, inner.Top + inner.Height * 0.68f, SKTextAlign.Center, _font9, _textPaint);

        // Kosten-Anzeige
        float costY = inner.Top + inner.Height * 0.75f;
        GameCardRenderer.DrawCoinIcon(canvas, inner.MidX - 20f, costY, 6f);
        _textPaint.Color = data.CanAffordUnlock ? new SKColor(0x22, 0xC5, 0x5E) : new SKColor(0xEF, 0x44, 0x44);
        canvas.DrawText(data.UpgradeCostText ?? "", inner.MidX, costY + 4f, SKTextAlign.Center, _font11, _textPaint);

        // Rabatt-Hinweis: "-30% mit Video" (nur wenn ShowAds aktiv → immer anzeigen, Monetarisierungs-Anreiz)
        float discountY = inner.Top + inner.Height * 0.88f;
        _textPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 180); // Gold
        canvas.DrawText("\u25B6 -30%", inner.MidX, discountY + 3f, SKTextAlign.Center, _font8, _textPaint);
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

        // Workshop-Icon (gedimmt, zeigt was freigeschaltet wird)
        var wsColor = WorkshopCardRenderer.GetWorkshopColor(data.Type);
        if (_workshopIcons.TryGetValue(data.Type, out var lockedIcon))
        {
            _iconPaint.Color = wsColor.WithAlpha(60);
            GameIconRenderer.DrawAt(canvas, lockedIcon, inner.MidX, inner.Top + inner.Height * 0.18f, 24f, _iconPaint);
        }

        // Workshop-Name (gedimmt)
        _textPaint.Color = wsColor.WithAlpha(100);
        canvas.DrawText(data.Name ?? "", inner.MidX, inner.Top + inner.Height * 0.35f, SKTextAlign.Center, _font12Bold, _textPaint);

        // Grosses geschlossenes Schloss
        GameCardRenderer.DrawLockIcon(canvas, inner.MidX, inner.Top + inner.Height * 0.48f, 32f, isOpen: false);

        // "Ab Level X"
        _textPaint.Color = new SKColor(0x94, 0xA3, 0xB8, 160);
        if (!_unlockLevelLabelCache.TryGetValue(data.UnlockLevel, out var unlockLabel))
        {
            unlockLabel = $"Ab Level {data.UnlockLevel}";
            _unlockLevelLabelCache[data.UnlockLevel] = unlockLabel;
        }
        canvas.DrawText(unlockLabel, inner.MidX, inner.Top + inner.Height * 0.75f, SKTextAlign.Center, _font10, _textPaint);
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
        var wcKey = (workerCount, maxWorkers);
        if (!_workerCountLabelCache.TryGetValue(wcKey, out var wcLabel))
        {
            wcLabel = $"{workerCount}/{maxWorkers}";
            _workerCountLabelCache[wcKey] = wcLabel;
        }
        canvas.DrawText(wcLabel, textX, y + 2f, SKTextAlign.Left, _font9, _textPaint);
    }

    /// <summary>
    /// Lädt das AI-Workshop-Bild aus dem Asset-Service. Triggert async Laden beim ersten Aufruf.
    /// </summary>
    private static SkiaSharp.SKBitmap? TryGetWorkshopBitmap(WorkshopType type)
    {
        if (_assetService == null) return null;
        var name = _assetNames.GetValueOrDefault(type, "carpenter");
        var path = $"workshops/{name}.webp";
        var bmp = _assetService.GetBitmap(path);
        if (bmp == null)
            _ = _assetService.LoadBitmapAsync(path);
        return bmp;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Rebirth-Sterne
    // ═══════════════════════════════════════════════════════════════════════

    // Gecachter Stern-Pfad (5-zackig)
    private static SKPath? _starPath;

    /// <summary>
    /// Zeichnet Rebirth-Sterne: gold gefuellt fuer aktive, halbtransparent fuer fehlende.
    /// </summary>
    private static void DrawRebirthStars(SKCanvas canvas, float x, float y, int activeStars, int maxStars)
    {
        float starSize = 7f;
        float spacing = 11f;

        for (int i = 0; i < maxStars; i++)
        {
            float cx = x + i * spacing + starSize;
            float cy = y + starSize;
            bool isActive = i < activeStars;

            _fillPaint.Color = isActive
                ? new SKColor(0xFF, 0xD7, 0x00, 230)   // Gold gefuellt
                : new SKColor(0xFF, 0xD7, 0x00, 50);   // Halbtransparent

            DrawStar(canvas, cx, cy, starSize, _fillPaint);

            // Goldener Rand fuer aktive Sterne
            if (isActive)
            {
                _strokePaint.Color = new SKColor(0xD9, 0x77, 0x06, 180);
                _strokePaint.StrokeWidth = 0.8f;
                DrawStar(canvas, cx, cy, starSize, _strokePaint);
                _strokePaint.StrokeWidth = 1f;
            }
        }
    }

    /// <summary>
    /// Zeichnet einen 5-zackigen Stern mit gecachtem Pfad.
    /// </summary>
    private static void DrawStar(SKCanvas canvas, float cx, float cy, float radius, SKPaint paint)
    {
        _starPath ??= CreateStarPath();

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.Scale(radius / 10f);
        canvas.DrawPath(_starPath, paint);
        canvas.Restore();
    }

    /// <summary>
    /// Erstellt einen 5-zackigen Stern-Pfad (zentriert bei 0,0, Radius 10).
    /// </summary>
    private static SKPath CreateStarPath()
    {
        var path = new SKPath();
        const int points = 5;
        const float outerR = 10f;
        const float innerR = 4f;
        const float startAngle = -MathF.PI / 2; // Spitze nach oben

        for (int i = 0; i < points * 2; i++)
        {
            float r = (i % 2 == 0) ? outerR : innerR;
            float angle = startAngle + i * MathF.PI / points;
            float px = r * MathF.Cos(angle);
            float py = r * MathF.Sin(angle);

            if (i == 0)
                path.MoveTo(px, py);
            else
                path.LineTo(px, py);
        }
        path.Close();
        return path;
    }
}
