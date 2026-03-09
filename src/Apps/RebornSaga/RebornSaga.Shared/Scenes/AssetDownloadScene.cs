namespace RebornSaga.Scenes;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;
using System.Threading;

/// <summary>
/// Download-Screen für AI-generierte Assets beim ersten Start.
/// Zeigt Fortschritt, Partikel-Animation und WLAN-Hinweis.
/// Nach Download automatischer Wechsel zur TitleScene.
/// </summary>
public class AssetDownloadScene : Scene
{
    private readonly IAssetDeliveryService _deliveryService;
    private readonly ILocalizationService _localization;
    private readonly IAudioService? _audioService;

    private float _progress;        // 0.0 - 1.0
    private long _downloadedBytes;
    private long _totalBytes;
    private string _statusText = "Checking assets...";
    private string _currentFileName = "";
    private bool _isDownloading;
    private bool _downloadComplete;
    private bool _downloadFailed;
    private string _errorMessage = "";

    // Gecachte lokalisierte Strings
    private string _checkingAssetsText = "Checking assets...";
    private string _downloadingFilesFormat = "{0} files downloading...";
    private string _downloadingText = "Downloading...";
    private string _downloadCompleteText = "Download complete!";
    private string _downloadFailedText = "Download failed.";
    private string _useWifiText = "Please use a Wi-Fi connection";
    private string _retryDownloadText = "Retry";
    private string _readyText = "Ready!";
    private float _completedDelay;  // Kurze Verzögerung nach Download bevor Szenenwechsel

    // Ambient-Partikel (struct-basiert, kein Heap pro Frame)
    private readonly (float x, float y, float speed, float alpha, float size)[] _particles
        = new (float, float, float, float, float)[40];
    private float _time;

    // Retry-Button Rect (für Hit-Test)
    private SKRect _retryButtonRect;
    private bool _retryHovered;
    private bool _retryPressed;

    // Gepoolte Paints (statisch, keine Allokation pro Frame)
    private static readonly SKPaint _bgPaint = new() { Color = new SKColor(0x0A, 0x16, 0x28) };
    private static readonly SKPaint _barBgPaint = new() { Color = new SKColor(0x1E, 0x29, 0x3B), IsAntialias = true };
    private static readonly SKPaint _barFillPaint = new() { IsAntialias = true };
    private static readonly SKPaint _particlePaint = new() { IsAntialias = true };

    // Gecachter Gradient für Fortschrittsbalken (wird bei Bounds-Änderung neu erstellt)
    private SKShader? _barShader;
    private float _lastBarWidth;

    // Gecachte Fortschritts-Strings (vermeidet String-Allokation pro Frame)
    private string _cachedProgressText = "";
    private float _lastCachedProgress = -1f;

    public AssetDownloadScene(IAssetDeliveryService deliveryService, ILocalizationService localization, IAudioService? audioService = null)
    {
        _deliveryService = deliveryService;
        _localization = localization;
        _audioService = audioService;
        UpdateLocalizedTexts();
        InitializeParticles();
    }

    private void UpdateLocalizedTexts()
    {
        _checkingAssetsText = _localization.GetString("CheckingAssets") ?? "Checking assets...";
        _downloadingFilesFormat = _localization.GetString("DownloadingFiles") ?? "{0} files downloading...";
        _downloadingText = _localization.GetString("Downloading") ?? "Downloading...";
        _downloadCompleteText = _localization.GetString("DownloadComplete") ?? "Download complete!";
        _downloadFailedText = _localization.GetString("DownloadFailed") ?? "Download failed.";
        _useWifiText = _localization.GetString("UseWifi") ?? "Please use a Wi-Fi connection";
        _retryDownloadText = _localization.GetString("RetryDownload") ?? "Retry";
        _readyText = _localization.GetString("Ready") ?? "Ready!";
        _statusText = _checkingAssetsText;
    }

    private void InitializeParticles()
    {
        var rng = new Random(42);
        for (int i = 0; i < _particles.Length; i++)
        {
            _particles[i] = (
                x: rng.NextSingle(),
                y: rng.NextSingle(),
                speed: 0.02f + rng.NextSingle() * 0.05f,
                alpha: 0.2f + rng.NextSingle() * 0.5f,
                size: 2f + rng.NextSingle() * 4f
            );
        }
    }

    public override void OnEnter()
    {
        base.OnEnter();
        StartDownload();
    }

    private async void StartDownload()
    {
        _isDownloading = true;
        _downloadFailed = false;
        _errorMessage = "";
        _statusText = _checkingAssetsText;
        _progress = 0f;

        try
        {
            var check = await _deliveryService.CheckForUpdatesAsync();
            if (!check.UpdateAvailable)
            {
                // Keine Downloads nötig
                _downloadComplete = true;
                _completedDelay = 0f;
                return;
            }

            _totalBytes = check.BytesToDownload;
            _statusText = string.Format(_downloadingFilesFormat, check.FilesToDownload);

            var progress = new Progress<AssetDownloadProgress>(p =>
            {
                _progress = p.TotalBytes > 0 ? (float)p.BytesDownloaded / p.TotalBytes : 0f;
                _downloadedBytes = p.BytesDownloaded;
                _currentFileName = p.CurrentFileName ?? "";
                _statusText = !string.IsNullOrEmpty(p.CurrentFileName)
                    ? p.CurrentFileName
                    : _downloadingText;
            });

            var success = await _deliveryService.DownloadAssetsAsync(progress);
            if (success)
            {
                _downloadComplete = true;
                _completedDelay = 0f;
                _statusText = _downloadCompleteText;
                _progress = 1f;
            }
            else
            {
                _downloadFailed = true;
                _errorMessage = _downloadFailedText;
            }
        }
        catch (Exception ex)
        {
            _downloadFailed = true;
            _errorMessage = ex.Message.Length > 60
                ? ex.Message[..57] + "..."
                : ex.Message;
        }
        finally
        {
            _isDownloading = false;
        }
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;

        // Partikel aufwärts bewegen
        for (int i = 0; i < _particles.Length; i++)
        {
            var p = _particles[i];
            p.y -= p.speed * deltaTime;
            if (p.y < -0.05f) p.y = 1.05f;
            _particles[i] = p;
        }

        // Nach Download kurz warten, dann zur TitleScene
        if (_downloadComplete)
        {
            _completedDelay += deltaTime;
            if (_completedDelay > 0.5f)
            {
                SceneManager?.ChangeScene<TitleScene>();
            }
        }
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        // Dunkler Hintergrund
        canvas.DrawRect(bounds, _bgPaint);

        // Ambient-Partikel (Blau, Violett, Gold)
        RenderParticles(canvas, w, h);

        var centerY = h * 0.45f;

        // "REBORN SAGA" Titel
        UIRenderer.DrawTextWithShadow(canvas, "REBORN SAGA",
            w / 2f, centerY - 60f,
            Math.Min(w * 0.06f, 28f),
            UIRenderer.PrimaryGlow, 2f);

        // Status-Text (aktueller Dateiname oder Meldung)
        UIRenderer.DrawText(canvas, _statusText,
            w / 2f, centerY - 20f,
            Math.Min(w * 0.035f, 16f),
            UIRenderer.TextSecondary, SKTextAlign.Center);

        // Fortschrittsbalken
        RenderProgressBar(canvas, w, centerY);

        // Fortschrittstext (Prozent + MB)
        UpdateProgressText();
        UIRenderer.DrawText(canvas, _cachedProgressText,
            w / 2f, centerY + 36f,
            Math.Min(w * 0.03f, 14f),
            UIRenderer.TextSecondary, SKTextAlign.Center);

        // WLAN-Hinweis während Download
        if (_isDownloading)
        {
            UIRenderer.DrawText(canvas, _useWifiText,
                w / 2f, centerY + 60f,
                Math.Min(w * 0.03f, 13f),
                UIRenderer.TextMuted, SKTextAlign.Center);
        }

        // Fehler-Anzeige + Retry-Button
        if (_downloadFailed)
        {
            UIRenderer.DrawText(canvas, _errorMessage,
                w / 2f, centerY + 100f,
                Math.Min(w * 0.035f, 16f),
                UIRenderer.Danger, SKTextAlign.Center);

            _retryButtonRect = new SKRect(w / 2f - 80f, centerY + 120f, w / 2f + 80f, centerY + 164f);
            UIRenderer.DrawButton(canvas, _retryButtonRect, _retryDownloadText,
                _retryHovered, _retryPressed, UIRenderer.Primary);
        }

        // Download abgeschlossen
        if (_downloadComplete && !_downloadFailed)
        {
            UIRenderer.DrawText(canvas, _readyText,
                w / 2f, centerY + 60f,
                Math.Min(w * 0.04f, 18f),
                UIRenderer.Success, SKTextAlign.Center);
        }
    }

    private void RenderParticles(SKCanvas canvas, float w, float h)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            var p = _particles[i];
            var color = (i % 3) switch
            {
                0 => new SKColor(0x4A, 0x90, 0xD9, (byte)(p.alpha * 255)),  // System-Blau
                1 => new SKColor(0x8B, 0x5C, 0xF6, (byte)(p.alpha * 255)),  // Violett
                _ => new SKColor(0xFF, 0xD7, 0x00, (byte)(p.alpha * 255))   // Gold
            };
            _particlePaint.Color = color;
            canvas.DrawCircle(p.x * w, p.y * h, p.size, _particlePaint);
        }
    }

    private void RenderProgressBar(SKCanvas canvas, float w, float centerY)
    {
        var barX = w * 0.15f;
        var barW = w * 0.7f;
        var barH = 12f;
        var barY = centerY;
        var barRect = new SKRect(barX, barY, barX + barW, barY + barH);
        var barRadius = barH / 2f;

        // Hintergrund
        canvas.DrawRoundRect(barRect, barRadius, barRadius, _barBgPaint);

        // Gefüllter Bereich mit Gradient
        if (_progress > 0.001f)
        {
            var fillRect = new SKRect(barX, barY, barX + barW * _progress, barY + barH);

            // Shader nur neu erstellen wenn sich die Breite signifikant ändert
            if (MathF.Abs(barW - _lastBarWidth) > 1f || _barShader == null)
            {
                _barShader?.Dispose();
                _barShader = SKShader.CreateLinearGradient(
                    new SKPoint(barX, barY), new SKPoint(barX + barW, barY),
                    new[] { new SKColor(0x4A, 0x90, 0xD9), new SKColor(0x8B, 0x5C, 0xF6) },
                    SKShaderTileMode.Clamp);
                _lastBarWidth = barW;
            }

            _barFillPaint.Shader = _barShader;
            canvas.DrawRoundRect(fillRect, barRadius, barRadius, _barFillPaint);
            _barFillPaint.Shader = null;
        }
    }

    private void UpdateProgressText()
    {
        // Nur neu erstellen wenn sich der Fortschritt signifikant geändert hat
        if (MathF.Abs(_progress - _lastCachedProgress) > 0.005f || _lastCachedProgress < 0)
        {
            _lastCachedProgress = _progress;
            if (_totalBytes > 0)
            {
                var mbDown = _downloadedBytes / 1_048_576.0;
                var mbTotal = _totalBytes / 1_048_576.0;
                _cachedProgressText = $"{mbDown:F1} / {mbTotal:F1} MB ({_progress * 100f:F0}%)";
            }
            else
            {
                _cachedProgressText = $"{_progress * 100f:F0}%";
            }
        }
    }

    // --- Input ---

    public override void HandlePointerDown(SKPoint position)
    {
        if (_downloadFailed && UIRenderer.HitTest(_retryButtonRect, position))
            _retryPressed = true;
    }

    public override void HandlePointerMove(SKPoint position)
    {
        _retryHovered = _downloadFailed && UIRenderer.HitTest(_retryButtonRect, position);
    }

    public override void HandlePointerUp(SKPoint position)
    {
        _retryPressed = false;
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (action == InputAction.Tap && _downloadFailed && UIRenderer.HitTest(_retryButtonRect, position))
        {
            // Retry: Download erneut starten
            _downloadFailed = false;
            _errorMessage = "";
            _progress = 0f;
            _lastCachedProgress = -1f;
            StartDownload();
        }
    }

    public override void OnExit()
    {
        _barShader?.Dispose();
        _barShader = null;
    }
}
