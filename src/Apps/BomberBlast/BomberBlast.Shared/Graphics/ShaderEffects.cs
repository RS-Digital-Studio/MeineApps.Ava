using BomberBlast.Services;
using SkiaSharp;
using System;

namespace BomberBlast.Graphics;

/// <summary>
/// GPU-basierte Post-Processing Effekte.
/// Chromatic Aberration, Damage Flash, Water Ripples, Heat Shimmer, Color Grading.
/// SkSL wo verfügbar, CPU-Fallback für ältere Geräte.
/// </summary>
public sealed class ShaderEffects : IDisposable
{
    /// <summary>
    /// Statischer Logger, wird nach DI-Build von App.axaml.cs gesetzt.
    /// </summary>
    public static IAppLogger? Logger { get; set; }

    private bool _disposed;

    // --- Welt-Zustand ---
    private int _currentWorld = -1;
    private bool _isOceanWorld;
    private bool _isHeatWorld; // Inferno (4) oder Volcano (7)

    // --- Color Grading ---
    private SKColor _gradingColor;
    private byte _gradingAlpha;

    // --- Chromatic Aberration ---
    private float _chromaticTimer;
    private const float ChromaticDuration = 0.3f;
    private const float ChromaticMaxOffset = 4f;

    // --- Damage Flash ---
    private float _damageFlashTimer;
    private const float DamageFlashDuration = 0.25f;

    // --- Water Ripples ---
    private SKRuntimeEffect? _waterRippleEffect;
    private bool _gpuRipplesAvailable;
    private float _playerScreenX, _playerScreenY;

    // --- Gepoolte Paints ---
    private readonly SKPaint _overlayPaint = new() { IsAntialias = false };
    private readonly SKPaint _ripplePaint = new() { IsAntialias = true };
    private readonly SKPaint _shimmerPaint = new() { IsAntialias = false };

    // SkSL Wasser-Ripple Shader (SkiaSharp 3.x SkSL-Syntax)
    private const string WaterRippleSkSL = @"
uniform float2 iResolution;
uniform float iTime;
uniform float2 iCenter;

half4 main(float2 coord) {
    float2 uv = coord / iResolution;
    float2 center = iCenter / iResolution;

    float dist = length(uv - center);

    // Konzentrische Wellenringe vom Spieler ausgehend
    float wave = sin(dist * 45.0 - iTime * 4.5) * 0.5 + 0.5;
    wave *= exp(-dist * 5.5);
    wave *= smoothstep(0.0, 0.025, dist);

    // Subtile Kaustik über gesamte Fläche
    float cx = sin(uv.x * 25.0 + iTime * 1.8) * sin(uv.y * 25.0 + iTime * 1.3);
    cx = cx * 0.5 + 0.5;

    float a = wave * 0.13 + cx * 0.035;
    return half4(0.35, 0.6, 0.85, a);
}
";

    // --- Spieler-Treffer-Erkennung ---
    private bool _playerWasInvincible;

    public ShaderEffects()
    {
        TryInitGPUShaders();
    }

    private void TryInitGPUShaders()
    {
        try
        {
            // SkiaSharp 3.x: CreateShader statt Create
            _waterRippleEffect = SKRuntimeEffect.CreateShader(WaterRippleSkSL, out var errors);
            _gpuRipplesAvailable = _waterRippleEffect != null;
            if (!_gpuRipplesAvailable)
                Logger?.LogWarning($"SkSL Kompilierung fehlgeschlagen: {errors}");
        }
        catch (Exception ex)
        {
            _gpuRipplesAvailable = false;
            Logger?.LogWarning($"SkSL nicht verfügbar: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ÖFFENTLICHE API
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Welt-spezifisches Color Grading setzen.
    /// Ergänzt MoodLighting (das Farbtönung macht) um Kontrast/Sättigung.
    /// </summary>
    public void SetWorld(int worldIndex)
    {
        if (worldIndex == _currentWorld) return;
        _currentWorld = worldIndex;
        _isOceanWorld = worldIndex == 6;
        _isHeatWorld = worldIndex == 4 || worldIndex == 7;

        // SoftLight-Overlay-Farbe pro Welt (Kontrast + Farbshift)
        (_gradingColor, _gradingAlpha) = worldIndex switch
        {
            0 => (new SKColor(255, 220, 150), (byte)12),  // Forest: Warmes Gold
            1 => (new SKColor(150, 170, 200), (byte)10),  // Industrial: Kühles Stahlblau
            2 => (new SKColor(100, 130, 200), (byte)14),  // Cavern: Tiefes Blau
            3 => (new SKColor(200, 220, 255), (byte)8),   // Sky: Helles Himmelblau
            4 => (new SKColor(255, 100, 50), (byte)15),   // Inferno: Glühendes Rot-Orange
            5 => (new SKColor(220, 190, 140), (byte)10),  // Ruins: Warmes Sepia
            6 => (new SKColor(80, 150, 220), (byte)14),   // Ocean: Tiefes Ozeanblau
            7 => (new SKColor(255, 120, 60), (byte)13),   // Volcano: Vulkan-Orange
            8 => (new SKColor(255, 230, 150), (byte)10),  // SkyFortress: Goldglanz
            9 => (new SKColor(150, 80, 180), (byte)16),   // ShadowRealm: Düsteres Violett
            _ => (SKColors.White, (byte)0)
        };
    }

    /// <summary>
    /// Damage-Effekte auslösen (Chromatic Aberration + roter Flash).
    /// </summary>
    public void TriggerDamageEffects()
    {
        _chromaticTimer = ChromaticDuration;
        _damageFlashTimer = DamageFlashDuration;
    }

    /// <summary>
    /// Prüft ob der Spieler gerade getroffen wurde (Invincibility-Flanke).
    /// Aufgerufen pro Frame vom Renderer.
    /// </summary>
    public void CheckPlayerHit(bool isInvincible)
    {
        if (isInvincible && !_playerWasInvincible)
            TriggerDamageEffects();
        _playerWasInvincible = isInvincible;
    }

    /// <summary>
    /// Spieler-Bildschirmposition für Water Ripples.
    /// </summary>
    public void UpdatePlayerScreenPos(float sx, float sy)
    {
        _playerScreenX = sx;
        _playerScreenY = sy;
    }

    /// <summary>
    /// Timer aktualisieren (jeder Frame).
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_chromaticTimer > 0) _chromaticTimer = MathF.Max(0, _chromaticTimer - deltaTime);
        if (_damageFlashTimer > 0) _damageFlashTimer = MathF.Max(0, _damageFlashTimer - deltaTime);
    }

    /// <summary>
    /// Alle Post-Processing Effekte rendern.
    /// Aufruf: nach canvas.Restore() (Screen-Space), vor Vignette/HUD.
    /// </summary>
    public void RenderPostProcessing(SKCanvas canvas, float w, float h, float globalTimer)
    {
        // 1. Color Grading (subtiler Kontrast/Farbshift pro Welt)
        if (_gradingAlpha > 0)
            RenderColorGrading(canvas, w, h);

        // 2. Water Ripples (nur Ocean-Welt)
        if (_isOceanWorld)
            RenderWaterRipples(canvas, w, h, globalTimer);

        // 3. Heat Shimmer (Inferno/Volcano - animierte Hitze-Schlieren)
        if (_isHeatWorld)
            RenderHeatShimmer(canvas, w, h, globalTimer);

        // 4. Damage Flash (roter Screen-Flash bei Treffer)
        if (_damageFlashTimer > 0)
            RenderDamageFlash(canvas, w, h);

        // 5. Chromatic Aberration (RGB-Verschiebung bei Treffer)
        if (_chromaticTimer > 0)
            RenderChromaticAberration(canvas, w, h);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COLOR GRADING
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderColorGrading(SKCanvas canvas, float w, float h)
    {
        // SoftLight-Blend: Erhöht Kontrast und verschiebt Farbtöne subtil
        // Ergänzt MoodLighting das mit SrcOver nur tönend wirkt
        _overlayPaint.Color = _gradingColor.WithAlpha(_gradingAlpha);
        _overlayPaint.BlendMode = SKBlendMode.SoftLight;
        canvas.DrawRect(0, 0, w, h, _overlayPaint);
        _overlayPaint.BlendMode = SKBlendMode.SrcOver;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DAMAGE FLASH
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderDamageFlash(SKCanvas canvas, float w, float h)
    {
        float t = _damageFlashTimer / DamageFlashDuration; // 1→0
        // Quadratisch für schnelles Ein-/Ausblenden
        float intensity = t * t;
        byte alpha = (byte)(intensity * 90);

        _overlayPaint.Color = new SKColor(255, 20, 0, alpha);
        _overlayPaint.BlendMode = SKBlendMode.SrcOver;
        canvas.DrawRect(0, 0, w, h, _overlayPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHROMATIC ABERRATION
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderChromaticAberration(SKCanvas canvas, float w, float h)
    {
        float t = _chromaticTimer / ChromaticDuration; // 1→0
        float offset = t * ChromaticMaxOffset;
        byte alpha = (byte)(t * 30);

        // Rot nach links versetzt
        _overlayPaint.Color = new SKColor(255, 0, 0, alpha);
        _overlayPaint.BlendMode = SKBlendMode.Plus;
        canvas.Save();
        canvas.Translate(-offset, offset * 0.3f);
        canvas.DrawRect(0, 0, w, h, _overlayPaint);
        canvas.Restore();

        // Cyan nach rechts versetzt (Komplementär zu Rot)
        _overlayPaint.Color = new SKColor(0, 180, 255, alpha);
        canvas.Save();
        canvas.Translate(offset, -offset * 0.3f);
        canvas.DrawRect(0, 0, w, h, _overlayPaint);
        canvas.Restore();

        _overlayPaint.BlendMode = SKBlendMode.SrcOver;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WATER RIPPLES (Ocean-Welt)
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderWaterRipples(SKCanvas canvas, float w, float h, float timer)
    {
        if (_gpuRipplesAvailable && _waterRippleEffect != null)
            RenderWaterRipplesGPU(canvas, w, h, timer);
        else
            RenderWaterRipplesCPU(canvas, w, h, timer);
    }

    private void RenderWaterRipplesGPU(SKCanvas canvas, float w, float h, float timer)
    {
        try
        {
            var uniforms = new SKRuntimeEffectUniforms(_waterRippleEffect!);
            uniforms["iResolution"] = new[] { w, h };
            uniforms["iTime"] = timer;
            uniforms["iCenter"] = new[] { _playerScreenX, _playerScreenY };

            using var shader = _waterRippleEffect!.ToShader(uniforms);
            _ripplePaint.Shader = shader;
            _ripplePaint.BlendMode = SKBlendMode.SrcOver;
            canvas.DrawRect(0, 0, w, h, _ripplePaint);
            _ripplePaint.Shader = null;
        }
        catch
        {
            // GPU-Shader fehlgeschlagen → ab jetzt CPU-Fallback
            _gpuRipplesAvailable = false;
            RenderWaterRipplesCPU(canvas, w, h, timer);
        }
    }

    private void RenderWaterRipplesCPU(SKCanvas canvas, float w, float h, float timer)
    {
        // CPU-Fallback: Konzentrische Ringe + Kaustik-Linien
        float cx = _playerScreenX, cy = _playerScreenY;

        _ripplePaint.Style = SKPaintStyle.Stroke;
        _ripplePaint.StrokeWidth = 1.5f;
        _ripplePaint.BlendMode = SKBlendMode.SrcOver;

        // 5 konzentrische Wellenringe um den Spieler
        for (int i = 0; i < 5; i++)
        {
            float phase = (timer * 2.5f + i * 1.2f) % 5f;
            float radius = phase / 5f * 180f;
            float alpha = (1f - phase / 5f) * 0.18f;

            if (alpha > 0.01f)
            {
                _ripplePaint.Color = new SKColor(90, 155, 220, (byte)(alpha * 255));
                canvas.DrawCircle(cx, cy, radius, _ripplePaint);
            }
        }

        // Subtile Kaustik-Linien über den gesamten Bildschirm
        _ripplePaint.StrokeWidth = 1f;
        _ripplePaint.Color = new SKColor(120, 180, 240, 12);
        float spacing = 35f;
        for (float y = 0; y < h; y += spacing)
        {
            float offset = MathF.Sin(y * 0.05f + timer * 1.5f) * 15f;
            float endOffset = MathF.Sin((y + spacing) * 0.05f + timer * 1.5f) * 12f;
            canvas.DrawLine(0, y + offset, w, y + endOffset, _ripplePaint);
        }

        _ripplePaint.Style = SKPaintStyle.Fill;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HEAT SHIMMER (Inferno/Volcano)
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderHeatShimmer(SKCanvas canvas, float w, float h, float timer)
    {
        // Animierte Hitze-Schlieren: halbtransparente wellige Bänder
        // Simuliert Hitze-Brechung ohne echte Pixel-Distortion
        int bandCount = 8;
        float bandHeight = h / bandCount;

        for (int i = 0; i < bandCount; i++)
        {
            float baseY = i * bandHeight;
            float waveX = MathF.Sin(timer * 2.5f + i * 0.7f) * 4f;
            float waveY = MathF.Sin(timer * 1.8f + i * 1.1f) * 3f;

            // Subtile Sichtbarkeit, pulsierend
            byte alpha = (byte)(MathF.Abs(MathF.Sin(timer * 3f + i * 0.9f)) * 8);
            if (alpha < 2) continue;

            // Warmes Hitze-Band
            _shimmerPaint.Color = new SKColor(255, 200, 100, alpha);
            _shimmerPaint.BlendMode = SKBlendMode.SrcOver;
            canvas.Save();
            canvas.Translate(waveX, 0);
            canvas.DrawRect(0, baseY + waveY, w, bandHeight * 0.6f, _shimmerPaint);
            canvas.Restore();
        }

        // Zusätzlich: Aufsteigende Hitze-Streifen (vertikale Verwirbelung)
        _shimmerPaint.Color = new SKColor(255, 180, 80, 5);
        for (int i = 0; i < 4; i++)
        {
            float x = w * (0.15f + i * 0.23f) + MathF.Sin(timer * 1.5f + i * 2f) * 20f;
            float stripW = 30f + MathF.Sin(timer * 2f + i) * 10f;
            canvas.DrawRect(x, 0, stripW, h, _shimmerPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _waterRippleEffect?.Dispose();
        _overlayPaint.Dispose();
        _ripplePaint.Dispose();
        _shimmerPaint.Dispose();
    }
}
