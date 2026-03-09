namespace RebornSaga.Overlays;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Models;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;

/// <summary>
/// Kapitel-Freischaltungs-Overlay. Zeigt Gold-Kosten, "Freischalten"-Button
/// und "Gold verdienen: Video ansehen (X/3 verfügbar)"-Option.
/// </summary>
public class ChapterUnlockOverlay : Scene
{
    private readonly ChapterUnlockService _unlockService;
    private readonly GoldService _goldService;
    private readonly ILocalizationService _localization;
    private readonly Player _player;
    private string _chapterId = "";
    private string _chapterName = "";
    private int _cost;
    private float _time;

    // Layout
    private SKRect _panelRect;
    private SKRect _unlockButtonRect;
    private SKRect _videoButtonRect;
    private SKRect _closeButtonRect;

    // Gecachte lokalisierte Strings
    private string _unlockChapterText = "Unlock Chapter";
    private string _costLabel = "Cost:";
    private string _unlockText = "Unlock";
    private string _backText = "Back";
    private string _noVideosText = "No videos available today";
    private string _yourGoldFormat = "Your Gold: {0}";
    private string _watchVideoFormat = "Watch video: +{0} Gold ({1}/3)";

    private string _costText = "";
    private string _goldText = "";
    private string _videoText = "";
    private int _lastGold;
    private int _lastVideoRemaining;

    // Doppelklick-Schutz
    private bool _isBusy;

    // Callback wenn freigeschaltet
    public event Action<string>? Unlocked;

    // Statische Paints
    private static readonly SKPaint _overlayPaint = new() { Color = new SKColor(0, 0, 0, 180) };
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKFont _titleFont = new() { LinearMetrics = true };
    private static readonly SKFont _textFont = new() { LinearMetrics = true };
    private static readonly SKMaskFilter _glowBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    public ChapterUnlockOverlay(
        ChapterUnlockService unlockService,
        GoldService goldService,
        Player player,
        ILocalizationService localization)
    {
        _unlockService = unlockService;
        _goldService = goldService;
        _player = player;
        _localization = localization;
        UpdateLocalizedTexts();
    }

    private void UpdateLocalizedTexts()
    {
        _unlockChapterText = _localization.GetString("UnlockChapter") ?? "Unlock Chapter";
        _costLabel = _localization.GetString("Cost") ?? "Cost:";
        _unlockText = _localization.GetString("Unlock") ?? "Unlock";
        _backText = _localization.GetString("Back") ?? "Back";
        _noVideosText = _localization.GetString("NoVideosToday") ?? "No videos available today";
        _yourGoldFormat = _localization.GetString("YourGold") ?? "Your Gold: {0}";
        _watchVideoFormat = _localization.GetString("WatchVideo") ?? "Watch video: +{0} Gold ({1}/3)";
    }

    /// <summary>
    /// Konfiguriert das Overlay für ein bestimmtes Kapitel.
    /// </summary>
    public void SetChapter(string chapterId, string chapterName)
    {
        _chapterId = chapterId;
        _chapterName = chapterName;
        _cost = ChapterUnlockService.GetCost(chapterId);
        _costText = $"{_cost:N0} Gold";
        _lastGold = -1; // Zwingt Neuberechnung
        _lastVideoRemaining = -1;
    }

    public override void OnEnter()
    {
        _time = 0;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;

        // Gecachte Strings aktualisieren
        if (_player.Gold != _lastGold)
        {
            _lastGold = _player.Gold;
            _goldText = string.Format(_yourGoldFormat, _player.Gold.ToString("N0"));
        }

        if (_goldService.DailyVideoWatchesRemaining != _lastVideoRemaining)
        {
            _lastVideoRemaining = _goldService.DailyVideoWatchesRemaining;
            _videoText = _lastVideoRemaining > 0
                ? string.Format(_watchVideoFormat, GoldService.VideoReward, _lastVideoRemaining)
                : _noVideosText;
        }
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Fade-In
        var alpha = Math.Min(1f, _time / 0.3f);
        var byteAlpha = (byte)(alpha * 255);

        // Overlay-Hintergrund
        _overlayPaint.Color = new SKColor(0, 0, 0, (byte)(180 * alpha));
        canvas.DrawRect(bounds, _overlayPaint);

        // Panel
        var panelW = bounds.Width * 0.85f;
        var panelH = bounds.Height * 0.55f;
        _panelRect = new SKRect(
            bounds.MidX - panelW / 2, bounds.MidY - panelH / 2,
            bounds.MidX + panelW / 2, bounds.MidY + panelH / 2);

        _bgPaint.Color = new SKColor(0x0A, 0x0E, 0x15, (byte)(240 * alpha));
        using var roundRect = new SKRoundRect(_panelRect, 12f);
        canvas.DrawRoundRect(roundRect, _bgPaint);

        // Glow-Rand
        _borderPaint.Color = UIRenderer.Accent.WithAlpha((byte)(byteAlpha * 0.7f));
        _borderPaint.MaskFilter = _glowBlur;
        canvas.DrawRoundRect(roundRect, _borderPaint);
        _borderPaint.MaskFilter = null;
        _borderPaint.Color = UIRenderer.Accent.WithAlpha((byte)(byteAlpha * 0.4f));
        canvas.DrawRoundRect(roundRect, _borderPaint);

        var cx = _panelRect.MidX;
        var y = _panelRect.Top;
        var lineH = panelH * 0.09f;

        // Titel: Kapitel freischalten
        y += lineH * 1.5f;
        _titleFont.Size = panelH * 0.08f;
        _textPaint.Color = UIRenderer.Accent.WithAlpha(byteAlpha);
        canvas.DrawText(_unlockChapterText, cx, y, SKTextAlign.Center, _titleFont, _textPaint);

        // Kapitelname
        y += lineH * 1.2f;
        _titleFont.Size = panelH * 0.07f;
        _textPaint.Color = UIRenderer.PrimaryGlow.WithAlpha(byteAlpha);
        canvas.DrawText(_chapterName, cx, y, SKTextAlign.Center, _titleFont, _textPaint);

        // Kosten
        y += lineH * 1.5f;
        _textFont.Size = panelH * 0.06f;
        _textPaint.Color = UIRenderer.TextPrimary.WithAlpha(byteAlpha);
        canvas.DrawText(_costLabel, cx, y, SKTextAlign.Center, _textFont, _textPaint);

        y += lineH;
        _titleFont.Size = panelH * 0.09f;
        _textPaint.Color = UIRenderer.Accent.WithAlpha(byteAlpha);
        canvas.DrawText(_costText, cx, y, SKTextAlign.Center, _titleFont, _textPaint);

        // Aktuelles Gold
        y += lineH * 1.2f;
        _textFont.Size = panelH * 0.055f;
        var canAfford = _player.Gold >= _cost;
        _textPaint.Color = canAfford ? UIRenderer.Success.WithAlpha(byteAlpha) : UIRenderer.Danger.WithAlpha(byteAlpha);
        canvas.DrawText(_goldText, cx, y, SKTextAlign.Center, _textFont, _textPaint);

        // Freischalten-Button
        y += lineH * 1.3f;
        var btnW = panelW * 0.6f;
        var btnH = panelH * 0.1f;
        _unlockButtonRect = new SKRect(cx - btnW / 2, y, cx + btnW / 2, y + btnH);
        UIRenderer.DrawButton(canvas, _unlockButtonRect, _unlockText,
            color: canAfford ? UIRenderer.Success : UIRenderer.Border);

        // Video-Button
        y += btnH + lineH * 0.5f;
        var hasVideos = _goldService.DailyVideoWatchesRemaining > 0;
        _videoButtonRect = new SKRect(cx - btnW / 2, y, cx + btnW / 2, y + btnH);
        UIRenderer.DrawButton(canvas, _videoButtonRect, _videoText,
            color: hasVideos ? UIRenderer.Accent : UIRenderer.Border);

        // Schließen-Button
        y += btnH + lineH * 0.5f;
        var closeBtnW = panelW * 0.35f;
        _closeButtonRect = new SKRect(cx - closeBtnW / 2, y, cx + closeBtnW / 2, y + btnH * 0.8f);
        UIRenderer.DrawButton(canvas, _closeButtonRect, _backText,
            color: UIRenderer.TextMuted);
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (action == InputAction.Back)
        {
            SceneManager.HideOverlay(this);
            return;
        }

        if (action != InputAction.Tap || _isBusy) return;

        // Schließen
        if (UIRenderer.HitTest(_closeButtonRect, position))
        {
            SceneManager.HideOverlay(this);
            return;
        }

        // Freischalten (_isBusy Guard verhindert doppelte Aufrufe)
        if (UIRenderer.HitTest(_unlockButtonRect, position) && _player.Gold >= _cost)
        {
            // AppChecker:ignore
            _ = UnlockAsync();
            return;
        }

        // Video ansehen (_isBusy Guard verhindert doppelte Aufrufe)
        if (UIRenderer.HitTest(_videoButtonRect, position) && _goldService.CanWatchVideo())
        {
            // AppChecker:ignore
            _ = WatchVideoAsync();
        }
    }

    private async System.Threading.Tasks.Task UnlockAsync()
    {
        _isBusy = true;
        try
        {
            var success = await _unlockService.UnlockWithGoldAsync(_chapterId, _player);
            if (success)
            {
                Unlocked?.Invoke(_chapterId);
                SceneManager.HideOverlay(this);
            }
        }
        catch (Exception)
        {
            // Fehlschlag wird still behandelt (kein UI-Feedback in SkiaSharp-Engine)
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async System.Threading.Tasks.Task WatchVideoAsync()
    {
        _isBusy = true;
        try
        {
            await _goldService.WatchVideoForGoldAsync(_player);
        }
        catch (Exception)
        {
            // Fehlschlag wird still behandelt
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _overlayPaint.Dispose();
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _textPaint.Dispose();
        _titleFont.Dispose();
        _textFont.Dispose();
        // _glowBlur ist static readonly — NICHT disposen
    }
}
