using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Bloom-Pass via <see cref="SKRuntimeEffect"/> (Phase 21b — V3).
///
/// <para>Echter GPU-Bloom statt SKMaskFilter-Software-Blur:</para>
/// <list type="bullet">
///   <item>Threshold-Pass: Pixel über Helligkeits-Schwelle isolieren</item>
///   <item>Box-Blur (Approximation einer Gauß-Faltung) als SkSL-Pixel-Shader</item>
///   <item>Additive Blend zurück auf das Hauptbild</item>
/// </list>
///
/// <para>Tier-Gate: Wird vom <see cref="Services.IHardwareProfileService.ShouldEnableBloom"/>
/// nur für Ultra-Tier aktiviert (deaktiviert bei Battery/Thermal). Mid-Tier bekommt nichts.</para>
///
/// <para>Performance: 2 Render-Pässe (Threshold + Blur). Auf Mid-Tier-Android ~3-5ms zusätzlich.
/// Akzeptabel im Frame-Budget bei Ultra-Geräten (Pixel 7+/Snapdragon 8 Gen 2).</para>
/// </summary>
public sealed class BloomEffect : IDisposable
{
    /// <summary>
    /// Threshold-Shader: Pixel mit Luminanz unter <c>uThreshold</c> werden auf 0 gesetzt.
    /// Output ist nur die "helle" Komponente — Highlights, Funken, Explosionen.
    /// </summary>
    private const string ThresholdSkSL = @"
uniform shader uTexture;
uniform float uThreshold;

half4 main(float2 coord) {
    half4 c = uTexture.eval(coord);
    // Standard-Luminanz-Formel (BT.601 perceptual)
    half lum = dot(c.rgb, half3(0.299, 0.587, 0.114));
    half mask = step(uThreshold, lum);
    return half4(c.rgb * mask, c.a * mask);
}
";

    /// <summary>
    /// Box-Blur-Shader (5x5 Tap, einfacher Approximations-Gauß).
    /// Reicht für Bloom-Halo-Effekt; echter Gauß wäre 9x9 mit gewichteter Faltung.
    /// </summary>
    private const string BlurSkSL = @"
uniform shader uTexture;
uniform float2 uTexelSize;

half4 main(float2 coord) {
    half4 sum = half4(0);
    // 5x5 Box-Blur mit fester Gewichtung 1/25
    for (int y = -2; y <= 2; y++) {
        for (int x = -2; x <= 2; x++) {
            float2 offset = float2(float(x), float(y)) * uTexelSize;
            sum += uTexture.eval(coord + offset);
        }
    }
    return sum * 0.04;
}
";

    private static readonly object _initLock = new();
    private static bool _initTried;
    private static SKRuntimeEffect? _thresholdEffect;
    private static SKRuntimeEffect? _blurEffect;
    private static string? _initErrors;

    private bool _disposed;

    // Audit H11: Cached SKPaint fuer Bloom-Apply (Plus-Blend mit Shader).
    // SKRuntimeEffectUniforms/Children koennen nicht trivial gecacht werden — die API
    // ist auf Per-Frame-Konstruktion ausgelegt. SKPaint laesst sich aber pro Instanz halten.
    private readonly SKPaint _bloomPaint = new() { BlendMode = SKBlendMode.Plus };

    /// <summary>True wenn beide Shader erfolgreich kompiliert wurden.</summary>
    public static bool IsAvailable => _thresholdEffect != null && _blurEffect != null;

    /// <summary>Initialisierungsfehler (für Logging).</summary>
    public static string? InitErrors => _initErrors;

    /// <summary>
    /// Kompiliert die Shader einmalig beim App-Start (analog ShaderEffects.Preload).
    /// </summary>
    public static void Preload()
    {
        if (_initTried) return;
        lock (_initLock)
        {
            if (_initTried) return;
            try
            {
                _thresholdEffect = SKRuntimeEffect.CreateShader(ThresholdSkSL, out var thErr);
                _blurEffect = SKRuntimeEffect.CreateShader(BlurSkSL, out var blErr);
                _initErrors = $"{thErr};{blErr}".Trim(';');
            }
            catch (Exception ex)
            {
                _initErrors = ex.Message;
                _thresholdEffect = null;
                _blurEffect = null;
            }
            finally
            {
                _initTried = true;
            }
        }
    }

    /// <summary>
    /// Wendet den Bloom-Effekt auf ein bereits gerendertes Frame an.
    /// </summary>
    /// <param name="canvas">Ziel-Canvas (das Hauptbild ist bereits gezeichnet).</param>
    /// <param name="sourceImage">Snapshot des Hauptbilds (für Threshold-Sampling).</param>
    /// <param name="bounds">Render-Bounds in Canvas-Koordinaten.</param>
    /// <param name="threshold">Helligkeits-Schwelle [0..1] (Default 0.7 = nur sehr helle Pixel).</param>
    /// <param name="intensity">Additive-Blend-Intensität (0=aus, 1=voller Bloom).</param>
    public void Apply(SKCanvas canvas, SKImage sourceImage, SKRect bounds,
        float threshold = 0.7f, float intensity = 0.5f)
    {
        if (!IsAvailable || sourceImage == null) return;
        if (_disposed) return;

        // Threshold-Pass: nur helle Komponente extrahieren
        using var thresholdUniforms = new SKRuntimeEffectUniforms(_thresholdEffect!)
        {
            { "uThreshold", threshold }
        };
        using var sourceShader = sourceImage.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
        using var thresholdChildren = new SKRuntimeEffectChildren(_thresholdEffect!)
        {
            { "uTexture", sourceShader }
        };
        using var brightOnly = _thresholdEffect!.ToShader(thresholdUniforms, thresholdChildren);

        // Blur-Pass auf den Threshold-Output
        using var blurUniforms = new SKRuntimeEffectUniforms(_blurEffect!)
        {
            { "uTexelSize", new[] { 1f / bounds.Width, 1f / bounds.Height } }
        };
        using var blurChildren = new SKRuntimeEffectChildren(_blurEffect!)
        {
            { "uTexture", brightOnly }
        };
        using var bloomShader = _blurEffect!.ToShader(blurUniforms, blurChildren);

        // Additive Blend: Bloom-Layer wird über das Hauptbild addiert
        // Audit H11: Cached _bloomPaint statt Pro-Frame-Allokation. Shader + ColorF werden pro Frame gesetzt.
        _bloomPaint.Shader = bloomShader;
        _bloomPaint.ColorF = new SKColorF(intensity, intensity, intensity, intensity);
        canvas.DrawRect(bounds, _bloomPaint);
        _bloomPaint.Shader = null; // Shader-Reference loslassen (Shader wird per `using` disposed)
    }

    public void Dispose()
    {
        // Per-Instance disposen wir nichts ausser dem Bloom-Paint — die statischen Effects leben app-weit.
        if (_disposed) return;
        _disposed = true;
        _bloomPaint.Dispose();
    }

    /// <summary>
    /// App-Shutdown-Cleanup. Statische SKRuntimeEffect-Instanzen freigeben.
    /// </summary>
    public static void DisposeSharedResources()
    {
        lock (_initLock)
        {
            _thresholdEffect?.Dispose();
            _blurEffect?.Dispose();
            _thresholdEffect = null;
            _blurEffect = null;
            _initTried = false;
        }
    }
}
