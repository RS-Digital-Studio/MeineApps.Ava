using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Globale Accessibility-Settings: Colorblind-Modi, High-Contrast, UI-Skalierung, Subtitles.
/// Wird einmal beim App-Start geladen, persistiert via IPreferencesService.
/// Alle Properties feuern PropertyChanged-Events damit Renderer / UI live aktualisieren.
/// </summary>
public interface IAccessibilityService
{
    /// <summary>"Off", "Deuteranopia", "Protanopia", "Tritanopia"</summary>
    string ColorblindMode { get; set; }

    /// <summary>Hochkontrast-Modus für UI</summary>
    bool HighContrast { get; set; }

    /// <summary>UI-Skalierung 0.75 bis 1.5</summary>
    double UiScale { get; set; }

    /// <summary>Subtitles für Audio-Cues</summary>
    bool SubtitlesEnabled { get; set; }

    /// <summary>
    /// v2.0.60 (B-C10 / WCAG 2.1): Photosensitivity-Schutz. Wenn true, werden
    /// hochfrequente Pulse-/Blitz-Effekte (Combo-Pulse 12 Hz, Damage-Flash, UltraComboFlash)
    /// gedrosselt oder deaktiviert. Empfohlen für Spieler mit Epilepsie-Risiko.
    /// </summary>
    bool ReducedFlashing { get; set; }

    /// <summary>
    /// Liefert einen SkiaSharp-ColorMatrix passend zum Colorblind-Modus.
    /// Wird im GameRenderer als Post-Processing-Filter verwendet.
    /// Returnt null wenn ColorblindMode == "Off".
    /// </summary>
    float[]? GetColorblindMatrix();

    event EventHandler? AccessibilityChanged;
}

/// <summary>
/// Standard-Implementierung mit Preferences-Persistenz.
/// </summary>
public sealed class AccessibilityService : IAccessibilityService
{
    private const string ColorblindKey = "Accessibility_ColorblindMode";
    private const string HighContrastKey = "Accessibility_HighContrast";
    private const string UiScaleKey = "Accessibility_UiScale";
    private const string SubtitlesKey = "Accessibility_Subtitles";
    private const string ReducedFlashingKey = "Accessibility_ReducedFlashing";

    private readonly IPreferencesService _preferences;
    private string _colorblindMode;
    private bool _highContrast;
    private double _uiScale;
    private bool _subtitlesEnabled;
    private bool _reducedFlashing;

    public event EventHandler? AccessibilityChanged;

    public AccessibilityService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _colorblindMode = _preferences.Get(ColorblindKey, "Off") ?? "Off";
        _highContrast = _preferences.Get(HighContrastKey, false);
        _uiScale = _preferences.Get(UiScaleKey, 1.0);
        _subtitlesEnabled = _preferences.Get(SubtitlesKey, false);
        _reducedFlashing = _preferences.Get(ReducedFlashingKey, false);
    }

    public string ColorblindMode
    {
        get => _colorblindMode;
        set
        {
            if (_colorblindMode == value) return;
            _colorblindMode = value;
            _preferences.Set(ColorblindKey, value);
            AccessibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool HighContrast
    {
        get => _highContrast;
        set
        {
            if (_highContrast == value) return;
            _highContrast = value;
            _preferences.Set(HighContrastKey, value);
            AccessibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public double UiScale
    {
        get => _uiScale;
        set
        {
            if (Math.Abs(_uiScale - value) < 0.001) return;
            _uiScale = Math.Clamp(value, 0.75, 1.5);
            _preferences.Set(UiScaleKey, _uiScale);
            AccessibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool SubtitlesEnabled
    {
        get => _subtitlesEnabled;
        set
        {
            if (_subtitlesEnabled == value) return;
            _subtitlesEnabled = value;
            _preferences.Set(SubtitlesKey, value);
            AccessibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool ReducedFlashing
    {
        get => _reducedFlashing;
        set
        {
            if (_reducedFlashing == value) return;
            _reducedFlashing = value;
            _preferences.Set(ReducedFlashingKey, value);
            AccessibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Standard-Daltonisierungs-Matrizen aus der Brettenmacher/Vienot-Forschung.
    /// 4x5 Color-Matrix (R,G,B,A,Bias) für SKColorFilter.CreateColorMatrix.
    /// Quellen: Daltonize.org, Color-Blindness-Research-Veröffentlichungen.
    /// </summary>
    public float[]? GetColorblindMatrix() => _colorblindMode switch
    {
        "Deuteranopia" => new float[]
        {
            0.625f, 0.375f, 0.0f,   0f, 0f,
            0.7f,   0.3f,   0.0f,   0f, 0f,
            0.0f,   0.3f,   0.7f,   0f, 0f,
            0f,     0f,     0f,     1f, 0f
        },
        "Protanopia" => new float[]
        {
            0.567f, 0.433f, 0.0f,   0f, 0f,
            0.558f, 0.442f, 0.0f,   0f, 0f,
            0.0f,   0.242f, 0.758f, 0f, 0f,
            0f,     0f,     0f,     1f, 0f
        },
        "Tritanopia" => new float[]
        {
            0.95f,  0.05f,  0.0f,   0f, 0f,
            0.0f,   0.433f, 0.567f, 0f, 0f,
            0.0f,   0.475f, 0.525f, 0f, 0f,
            0f,     0f,     0f,     1f, 0f
        },
        _ => null
    };
}
