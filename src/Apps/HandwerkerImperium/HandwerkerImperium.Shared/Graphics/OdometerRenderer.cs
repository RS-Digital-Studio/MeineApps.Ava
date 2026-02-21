using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Animierte Geld-Anzeige mit rollenden Ziffern (Odometer-Effekt).
/// Pro Ziffer ein vertikaler Strip mit 0-9, kaskadierendes Rollen.
/// </summary>
public class OdometerRenderer
{
    private const int MaxDigits = 12;
    private const float DigitRollDuration = 0.4f;  // Sekunden pro Ziffer
    private const float CascadeDelay = 0.05f;       // Verzögerung zwischen Ziffern
    private const float SuffixTransitionDuration = 0.3f;

    // Aktueller und Ziel-Wert
    private decimal _currentValue;
    private decimal _targetValue;
    private bool _isAnimating;

    // Pro-Ziffer Animation
    private readonly float[] _digitPositions = new float[MaxDigits]; // Aktuelle Y-Position (0-9)
    private readonly float[] _digitTargets = new float[MaxDigits];   // Ziel Y-Position
    private readonly float[] _digitTimers = new float[MaxDigits];    // Animation-Timer
    private int _activeDigitCount;

    // Suffix-Animation (K, M, B, T)
    private string _currentSuffix = "";
    private string _targetSuffix = "";
    private float _suffixTransition; // 0=alt, 1=neu
    private float _suffixTimer;

    // Gold-Flash bei großen Beträgen
    private float _flashIntensity;
    private const float FlashDecay = 3f;

    // Gecachte Paints
    private readonly SKPaint _digitPaint = new()
    {
        IsAntialias = true,
        Color = SKColors.White
    };
    private readonly SKPaint _shadowPaint = new()
    {
        IsAntialias = true,
        Color = new SKColor(0, 0, 0, 100)
    };
    private readonly SKPaint _suffixPaint = new()
    {
        IsAntialias = true,
        Color = new SKColor(0xFF, 0xD7, 0x00) // Gold für Suffix
    };
    private readonly SKPaint _flashPaint = new()
    {
        IsAntialias = true
    };
    private readonly SKPaint _bgPaint = new()
    {
        IsAntialias = true,
        Color = new SKColor(0, 0, 0, 60)
    };

    /// <summary>
    /// Neuen Zielwert setzen und Animation starten.
    /// </summary>
    public void SetTarget(decimal newValue)
    {
        if (newValue == _targetValue) return;

        decimal oldValue = _targetValue;
        _targetValue = newValue;

        // Flash bei großem Sprung (>10x)
        if (oldValue > 0 && newValue > oldValue * 10)
            _flashIntensity = 1f;

        // Ziffern berechnen und Animation starten
        StartDigitAnimation(newValue);
        _isAnimating = true;
    }

    /// <summary>
    /// Wert direkt setzen (ohne Animation, z.B. beim Laden).
    /// </summary>
    public void SetImmediate(decimal value)
    {
        _currentValue = value;
        _targetValue = value;
        _isAnimating = false;

        var (digits, suffix) = FormatValue(value);
        _activeDigitCount = digits.Length;
        _currentSuffix = suffix;
        _targetSuffix = suffix;
        _suffixTransition = 1f;

        for (int i = 0; i < MaxDigits; i++)
        {
            if (i < digits.Length)
            {
                _digitPositions[i] = digits[i] - '0';
                _digitTargets[i] = digits[i] - '0';
            }
            else
            {
                _digitPositions[i] = 0;
                _digitTargets[i] = 0;
            }
            _digitTimers[i] = 0;
        }
    }

    /// <summary>
    /// Animation updaten. Jeden Frame aufrufen.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!_isAnimating && _flashIntensity <= 0) return;

        bool anyActive = false;

        for (int i = 0; i < _activeDigitCount; i++)
        {
            if (_digitTimers[i] > 0)
            {
                _digitTimers[i] -= deltaTime;
                float progress = 1f - Math.Max(0, _digitTimers[i]) / DigitRollDuration;
                float eased = EasingFunctions.EaseOutCubic(progress);
                _digitPositions[i] = EasingFunctions.Lerp(_digitPositions[i], _digitTargets[i], eased);

                if (_digitTimers[i] > 0) anyActive = true;
                else _digitPositions[i] = _digitTargets[i]; // Exakt setzen
            }
        }

        // Suffix-Transition
        if (_suffixTimer > 0)
        {
            _suffixTimer -= deltaTime;
            _suffixTransition = 1f - Math.Max(0, _suffixTimer) / SuffixTransitionDuration;
            if (_suffixTimer <= 0)
            {
                _currentSuffix = _targetSuffix;
                _suffixTransition = 1f;
            }
            anyActive = true;
        }

        // Flash abklingen
        if (_flashIntensity > 0)
        {
            _flashIntensity -= deltaTime * FlashDecay;
            if (_flashIntensity < 0) _flashIntensity = 0;
        }

        if (!anyActive)
        {
            _isAnimating = false;
            _currentValue = _targetValue;
        }
    }

    /// <summary>
    /// Odometer auf das Canvas zeichnen.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas.</param>
    /// <param name="bounds">Zeichenbereich (links-oben + Breite/Höhe).</param>
    /// <param name="alignment">0=links, 0.5=zentriert, 1=rechts.</param>
    public void Render(SKCanvas canvas, SKRect bounds, float alignment = 1f)
    {
        if (_activeDigitCount == 0) return;

        float digitHeight = Math.Min(bounds.Height * 0.85f, 28f);
        float digitWidth = digitHeight * 0.6f;
        float gap = digitWidth * 0.08f;
        float suffixWidth = _currentSuffix.Length > 0 ? digitWidth * 0.7f : 0;
        float euroWidth = digitWidth * 0.8f;

        // Tausender-Punkte berechnen
        int dotCount = (_activeDigitCount - 1) / 3;
        float dotWidth = digitWidth * 0.25f;

        float totalWidth = _activeDigitCount * (digitWidth + gap) + dotCount * (dotWidth + gap) + suffixWidth + euroWidth;
        float startX = bounds.Left + (bounds.Width - totalWidth) * alignment;
        float centerY = bounds.MidY;

        using var digitFont = new SKFont(SKTypeface.Default, digitHeight);
        digitFont.Embolden = true;
        using var suffixFont = new SKFont(SKTypeface.Default, digitHeight * 0.7f);
        suffixFont.Embolden = true;
        using var euroFont = new SKFont(SKTypeface.Default, digitHeight * 0.65f);

        // Hintergrund (abgerundetes Rechteck)
        float bgPadH = 6f, bgPadV = 4f;
        var bgRect = new SKRect(
            startX - bgPadH, centerY - digitHeight / 2 - bgPadV,
            startX + totalWidth + bgPadH, centerY + digitHeight / 2 + bgPadV);
        canvas.DrawRoundRect(bgRect, 8, 8, _bgPaint);

        float x = startX;

        // Ziffern von links nach rechts rendern
        canvas.Save();
        canvas.ClipRect(bgRect);

        for (int i = _activeDigitCount - 1; i >= 0; i--)
        {
            float digitPos = _digitPositions[i];
            int currentDigit = (int)digitPos;
            float fraction = digitPos - currentDigit;

            // Aktuelle Ziffer
            byte alpha = (byte)(255 * (1f - fraction * 0.5f));
            float offsetY = -fraction * digitHeight * 0.8f;

            _digitPaint.Color = SKColors.White.WithAlpha(alpha);
            string digitChar = (currentDigit % 10).ToString();
            canvas.DrawText(digitChar, x, centerY + digitHeight * 0.35f + offsetY, SKTextAlign.Left, digitFont, _digitPaint);

            // Nächste Ziffer (teilweise sichtbar bei Animation)
            if (fraction > 0.01f)
            {
                byte nextAlpha = (byte)(255 * fraction * 0.8f);
                float nextOffsetY = (1f - fraction) * digitHeight * 0.8f;
                _digitPaint.Color = SKColors.White.WithAlpha(nextAlpha);
                string nextChar = ((currentDigit + 1) % 10).ToString();
                canvas.DrawText(nextChar, x, centerY + digitHeight * 0.35f + nextOffsetY, SKTextAlign.Left, digitFont, _digitPaint);
            }

            x += digitWidth + gap;

            // Tausender-Punkt einfügen
            int posFromRight = _activeDigitCount - 1 - i;
            if (posFromRight > 0 && posFromRight % 3 == 0 && i > 0)
            {
                _digitPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 150);
                canvas.DrawText(".", x, centerY + digitHeight * 0.15f, SKTextAlign.Left, digitFont, _digitPaint);
                x += dotWidth + gap;
            }
        }

        canvas.Restore();

        // Suffix (K, M, B, T) mit Transition
        if (_targetSuffix.Length > 0 || _currentSuffix.Length > 0)
        {
            if (_suffixTransition < 1f && _currentSuffix != _targetSuffix)
            {
                // Altes Suffix ausblenden
                byte oldAlpha = (byte)((1f - _suffixTransition) * 255);
                _suffixPaint.Color = new SKColor(0xFF, 0xD7, 0x00, oldAlpha);
                canvas.DrawText(_currentSuffix, x, centerY + digitHeight * 0.25f, SKTextAlign.Left, suffixFont, _suffixPaint);

                // Neues Suffix einblenden
                byte newAlpha = (byte)(_suffixTransition * 255);
                _suffixPaint.Color = new SKColor(0xFF, 0xD7, 0x00, newAlpha);
                canvas.DrawText(_targetSuffix, x, centerY + digitHeight * 0.25f, SKTextAlign.Left, suffixFont, _suffixPaint);
            }
            else
            {
                string suffix = _suffixTransition >= 1f ? _targetSuffix : _currentSuffix;
                _suffixPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
                canvas.DrawText(suffix, x, centerY + digitHeight * 0.25f, SKTextAlign.Left, suffixFont, _suffixPaint);
            }
            x += suffixWidth;
        }

        // Euro-Symbol
        _digitPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 180);
        canvas.DrawText(" E", x, centerY + digitHeight * 0.25f, SKTextAlign.Left, euroFont, _digitPaint);

        // Gold-Flash-Overlay
        if (_flashIntensity > 0.01f)
        {
            byte flashAlpha = (byte)(_flashIntensity * 100);
            _flashPaint.Color = new SKColor(0xFF, 0xD7, 0x00, flashAlpha);
            canvas.DrawRoundRect(bgRect, 8, 8, _flashPaint);
        }
    }

    // --- Private Hilfsmethoden ---

    private void StartDigitAnimation(decimal value)
    {
        var (digits, suffix) = FormatValue(value);
        int newCount = digits.Length;

        // Suffix-Wechsel?
        if (suffix != _targetSuffix)
        {
            _currentSuffix = _targetSuffix;
            _targetSuffix = suffix;
            _suffixTimer = SuffixTransitionDuration;
            _suffixTransition = 0f;
        }

        _activeDigitCount = newCount;

        // Ziffern-Animation starten (rechts zuerst, kaskadierend nach links)
        for (int i = 0; i < MaxDigits; i++)
        {
            if (i < newCount)
            {
                int newDigit = digits[newCount - 1 - i] - '0'; // Umgekehrte Reihenfolge (rechts=0)
                _digitTargets[i] = newDigit;
                _digitTimers[i] = DigitRollDuration + i * CascadeDelay; // Kaskade
            }
            else
            {
                _digitTargets[i] = 0;
                _digitTimers[i] = 0;
            }
        }
    }

    /// <summary>
    /// Formatiert einen Wert in Ziffern + Suffix.
    /// Beispiele: 1234 → ("1234", ""), 1234567 → ("1234", "K"), 1234567890 → ("1234", "M")
    /// </summary>
    private static (string digits, string suffix) FormatValue(decimal value)
    {
        if (value < 0) value = 0;

        if (value >= 1_000_000_000_000m)
            return (FormatDigits(value / 1_000_000_000_000m), "T");
        if (value >= 1_000_000_000m)
            return (FormatDigits(value / 1_000_000_000m), "B");
        if (value >= 1_000_000m)
            return (FormatDigits(value / 1_000_000m), "M");
        if (value >= 100_000m)
            return (FormatDigits(value / 1_000m), "K");

        return (FormatDigits(value), "");
    }

    private static string FormatDigits(decimal value)
    {
        // Max 6 Ziffern anzeigen, abgerundet
        long intValue = (long)Math.Floor(value);
        string str = intValue.ToString();
        if (str.Length > 6) str = str[..6];
        return str.Length == 0 ? "0" : str;
    }
}
