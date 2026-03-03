using SkiaSharp;

namespace MeineApps.UI.SkiaSharp.SplashScreen;

/// <summary>
/// Abstrakte Basis für app-spezifische Splash-Screen-Renderer.
/// Stellt Smooth-Progress-Interpolation, Time-Tracking und Helper-Methoden bereit.
/// Alle konkreten Renderer erben hiervon und implementieren OnUpdate/OnRender.
/// </summary>
public abstract class SplashRendererBase : IDisposable
{
    // --- Öffentliche Steuerung ---
    public float Progress { get; set; }
    public string StatusText { get; set; } = "";
    public string AppName { get; set; } = "App";
    public string AppVersion { get; set; } = "";

    // --- Render-State ---
    protected float RenderedProgress;
    protected float Time;
    protected bool IsInitialized;

    // --- Gecachte Basis-Paints ---
    protected readonly SKPaint StatusPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    protected readonly SKPaint VersionPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    protected readonly SKFont StatusFont = new() { Size = 13f };
    protected readonly SKFont VersionFont = new() { Size = 14f };

    protected readonly Random Rng = new();

    // --- Gecachte Fortschrittsbalken-Ressourcen (vermeidet per-frame Allokation) ---
    private readonly SKPaint _barBgPaint = new() { IsAntialias = true };
    private readonly SKPaint _barFillPaint = new() { IsAntialias = true };
    private SKShader? _barFillShader;
    private float _cachedBarLeft, _cachedBarWidth, _cachedBarY;
    private int _lastPercent = -1;
    private string _percentText = "0%";

    /// <summary>
    /// Aktualisiert Animationen (pro Frame, ~60fps).
    /// </summary>
    public void Update(float deltaTime)
    {
        Time += deltaTime;

        // Smooth-Interpolation zum Zielwert (EaseOut)
        var diff = Progress - RenderedProgress;
        if (Math.Abs(diff) > 0.001f)
            RenderedProgress += diff * 0.12f;
        else
            RenderedProgress = Progress;

        OnUpdate(deltaTime);
    }

    /// <summary>
    /// Rendert den kompletten Splash-Screen.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        OnRender(canvas, bounds);
    }

    /// <summary>App-spezifische Update-Logik (Partikel, Animationen).</summary>
    protected abstract void OnUpdate(float deltaTime);

    /// <summary>App-spezifische Render-Logik (Hintergrund, Szene, Progress).</summary>
    protected abstract void OnRender(SKCanvas canvas, SKRect bounds);

    // --- Helper-Methoden ---

    /// <summary>Zeichnet zentrierten Text.</summary>
    protected static void DrawCenteredText(SKCanvas canvas, string text, float y,
        SKFont font, SKPaint paint, float canvasWidth)
    {
        var textWidth = font.MeasureText(text);
        canvas.DrawText(text, (canvasWidth - textWidth) / 2f, y, font, paint);
    }

    /// <summary>Zeichnet den Status-Text zentriert unter dem Fortschrittsbalken.</summary>
    protected void DrawStatusText(SKCanvas canvas, float w, float y)
    {
        if (string.IsNullOrEmpty(StatusText)) return;
        StatusFont.Size = Math.Min(13f, w * 0.033f);
        StatusPaint.Color = new SKColor(0xBB, 0xBB, 0xBB);
        DrawCenteredText(canvas, StatusText, y, StatusFont, StatusPaint, w);
    }

    /// <summary>Zeichnet die Versionsnummer.</summary>
    protected void DrawVersion(SKCanvas canvas, float w, float y)
    {
        if (string.IsNullOrEmpty(AppVersion)) return;
        VersionFont.Size = Math.Min(14f, w * 0.035f);
        VersionPaint.Color = new SKColor(0x88, 0x88, 0x88);
        DrawCenteredText(canvas, AppVersion, y, VersionFont, VersionPaint, w);
    }

    /// <summary>Zeichnet einen Standard-Fortschrittsbalken mit Gradient und Glow.</summary>
    protected void DrawProgressBar(SKCanvas canvas, float w, float y,
        float barWidth, float barHeight, float barRadius,
        SKColor startColor, SKColor endColor, SKColor bgColor)
    {
        var progress = Math.Clamp(RenderedProgress, 0f, 1f);
        var barLeft = (w - barWidth) / 2f;

        // Hintergrund-Track (gecachter Paint)
        _barBgPaint.Color = bgColor;
        canvas.DrawRoundRect(new SKRect(barLeft, y, barLeft + barWidth, y + barHeight), barRadius, barRadius, _barBgPaint);

        // Fortschritts-Fill
        if (progress > 0.005f)
        {
            var fillWidth = barWidth * progress;

            // Shader nur bei Dimensions-/Farbänderung neu erstellen
            if (_barFillShader == null || barLeft != _cachedBarLeft || barWidth != _cachedBarWidth || y != _cachedBarY)
            {
                _barFillShader?.Dispose();
                _barFillShader = SKShader.CreateLinearGradient(
                    new SKPoint(barLeft, y), new SKPoint(barLeft + barWidth, y),
                    new[] { startColor, endColor }, null, SKShaderTileMode.Clamp);
                _cachedBarLeft = barLeft;
                _cachedBarWidth = barWidth;
                _cachedBarY = y;
            }
            _barFillPaint.Shader = _barFillShader;

            canvas.Save();
            using var clipRRect = new SKRoundRect(new SKRect(barLeft, y, barLeft + barWidth, y + barHeight), barRadius);
            canvas.ClipRoundRect(clipRRect);
            canvas.DrawRoundRect(new SKRect(barLeft, y, barLeft + fillWidth, y + barHeight), barRadius, barRadius, _barFillPaint);
            canvas.Restore();
        }

        // Prozent-Text (nur bei Änderung neu erstellen)
        var percent = (int)(progress * 100);
        if (percent != _lastPercent)
        {
            _lastPercent = percent;
            _percentText = $"{percent}%";
        }
        StatusFont.Size = Math.Min(13f, w * 0.033f);
        StatusPaint.Color = new SKColor(0xAA, 0xAA, 0xAA);
        canvas.DrawText(_percentText, barLeft + barWidth + 10f, y + barHeight / 2f + StatusFont.Size * 0.35f, StatusFont, StatusPaint);
    }

    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        OnDispose();
        _barBgPaint.Dispose();
        _barFillPaint.Dispose();
        _barFillShader?.Dispose();
        StatusPaint.Dispose();
        VersionPaint.Dispose();
        StatusFont.Dispose();
        VersionFont.Dispose();
    }

    /// <summary>App-spezifische Ressourcen freigeben.</summary>
    protected virtual void OnDispose() { }
}
