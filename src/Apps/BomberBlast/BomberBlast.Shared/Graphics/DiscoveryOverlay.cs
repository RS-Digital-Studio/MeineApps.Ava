using MeineApps.Core.Ava.Localization;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// SkiaSharp-Overlay für Erstentdeckungs-Hints.
/// Zeigt eine kompakte Karte mit Titel + Beschreibung beim ersten Kontakt mit
/// einem neuen PowerUp oder einer neuen Welt-Mechanik.
/// </summary>
public class DiscoveryOverlay : IDisposable
{
    private readonly ILocalizationService _localizationService;

    // State
    private bool _isActive;
    private string _titleKey = "";
    private string _descKey = "";
    private float _timer;
    private float _fadeAlpha;
    private const float FADE_IN_DURATION = 0.3f;
    private const float AUTO_DISMISS_TIME = 5f;

    // Gecachte SKPaint/SKFont (einmalig erstellt)
    private readonly SKPaint _dimPaint = new() { Color = new SKColor(0, 0, 0, 140) };
    private readonly SKPaint _cardPaint = new()
    {
        Style = SKPaintStyle.Fill,
        Color = new SKColor(25, 25, 40, 240),
        IsAntialias = true
    };
    private readonly SKPaint _cardBorderPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2.5f,
        Color = new SKColor(255, 215, 0, 220),
        IsAntialias = true
    };
    private readonly SKPaint _titlePaint = new()
    {
        Color = new SKColor(255, 215, 0), // Gold
        IsAntialias = true
    };
    private readonly SKPaint _descPaint = new()
    {
        Color = SKColors.White,
        IsAntialias = true
    };
    private readonly SKPaint _hintPaint = new()
    {
        Color = new SKColor(180, 180, 180),
        IsAntialias = true
    };
    private readonly SKPaint _newBadgePaint = new()
    {
        Style = SKPaintStyle.Fill,
        Color = new SKColor(255, 80, 0),
        IsAntialias = true
    };
    private readonly SKFont _titleFont = new() { Size = 20, Embolden = true };
    private readonly SKFont _descFont = new() { Size = 15 };
    private readonly SKFont _hintFont = new() { Size = 13 };
    private readonly SKFont _badgeFont = new() { Size = 11, Embolden = true };

    public bool IsActive => _isActive;

    public DiscoveryOverlay(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    /// <summary>
    /// Hint anzeigen
    /// </summary>
    public void Show(string titleKey, string descKey)
    {
        _titleKey = titleKey;
        _descKey = descKey;
        _timer = 0;
        _fadeAlpha = 0;
        _isActive = true;
    }

    /// <summary>
    /// Hint ausblenden
    /// </summary>
    public void Dismiss()
    {
        _isActive = false;
    }

    /// <summary>
    /// Timer aktualisieren (Echtzeit-deltaTime)
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!_isActive) return;

        _timer += deltaTime;

        // Fade-In
        if (_timer < FADE_IN_DURATION)
            _fadeAlpha = _timer / FADE_IN_DURATION;
        else
            _fadeAlpha = 1f;

        // Auto-Dismiss
        if (_timer >= AUTO_DISMISS_TIME)
            Dismiss();
    }

    /// <summary>
    /// Overlay rendern
    /// </summary>
    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        if (!_isActive) return;

        byte alpha = (byte)(255 * _fadeAlpha);
        byte dimAlpha = (byte)(140 * _fadeAlpha);

        // Halbtransparenter Hintergrund (gesamter Bildschirm)
        _dimPaint.Color = new SKColor(0, 0, 0, dimAlpha);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _dimPaint);

        // Karten-Dimensionen
        float cardWidth = Math.Min(screenWidth * 0.7f, 380);
        float cardHeight = 140;
        float cardX = (screenWidth - cardWidth) / 2f;
        float cardY = (screenHeight - cardHeight) / 2f;

        // Scale-Bounce beim Erscheinen
        float scale = _timer < FADE_IN_DURATION
            ? 0.8f + 0.2f * (_timer / FADE_IN_DURATION)
            : 1f;

        canvas.Save();
        canvas.Translate(screenWidth / 2f, screenHeight / 2f);
        canvas.Scale(scale);
        canvas.Translate(-screenWidth / 2f, -screenHeight / 2f);

        // Karte zeichnen
        _cardPaint.Color = new SKColor(25, 25, 40, (byte)(240 * _fadeAlpha));
        canvas.DrawRoundRect(cardX, cardY, cardWidth, cardHeight, 16, 16, _cardPaint);

        _cardBorderPaint.Color = new SKColor(255, 215, 0, (byte)(220 * _fadeAlpha));
        canvas.DrawRoundRect(cardX, cardY, cardWidth, cardHeight, 16, 16, _cardBorderPaint);

        // "NEU!" Badge oben links
        float badgeX = cardX + 12;
        float badgeY = cardY + 12;
        float badgeW = 46;
        float badgeH = 20;
        _newBadgePaint.Color = new SKColor(255, 80, 0, alpha);
        canvas.DrawRoundRect(badgeX, badgeY, badgeW, badgeH, 6, 6, _newBadgePaint);

        string newText = _localizationService.GetString("New") ?? "NEW!";
        using var badgePaint = new SKPaint { Color = new SKColor(255, 255, 255, alpha), IsAntialias = true };
        canvas.DrawText(newText, badgeX + badgeW / 2, badgeY + badgeH / 2 + 4,
            SKTextAlign.Center, _badgeFont, badgePaint);

        // Titel
        string title = _localizationService.GetString(_titleKey) ?? _titleKey;
        _titlePaint.Color = new SKColor(255, 215, 0, alpha);
        canvas.DrawText(title, cardX + cardWidth / 2, cardY + 50,
            SKTextAlign.Center, _titleFont, _titlePaint);

        // Beschreibung
        string desc = _localizationService.GetString(_descKey) ?? _descKey;
        _descPaint.Color = new SKColor(255, 255, 255, alpha);
        canvas.DrawText(desc, cardX + cardWidth / 2, cardY + 80,
            SKTextAlign.Center, _descFont, _descPaint);

        // "Tippen zum Schließen" unten
        string tapHint = _localizationService.GetString("TapToDismiss") ?? "Tap to dismiss";
        _hintPaint.Color = new SKColor(180, 180, 180, (byte)(alpha * 0.7f));
        canvas.DrawText(tapHint, cardX + cardWidth / 2, cardY + cardHeight - 12,
            SKTextAlign.Center, _hintFont, _hintPaint);

        canvas.Restore();
    }

    public void Dispose()
    {
        _dimPaint.Dispose();
        _cardPaint.Dispose();
        _cardBorderPaint.Dispose();
        _titlePaint.Dispose();
        _descPaint.Dispose();
        _hintPaint.Dispose();
        _newBadgePaint.Dispose();
        _titleFont.Dispose();
        _descFont.Dispose();
        _hintFont.Dispose();
        _badgeFont.Dispose();
    }
}
