using System;
using SkiaSharp;

namespace FitnessRechner.Graphics;

// =====================================================================
// Structs fuer den Vital Signs Monitor
// =====================================================================

/// <summary>
/// Zustand aller Vital-Werte fuer den Hero-Monitor.
/// </summary>
public struct VitalSignsState
{
    public float Weight;        // kg (z.B. 78.5)
    public float Bmi;           // BMI-Wert (z.B. 24.1)
    public float WaterMl;       // ml heute (z.B. 1500)
    public float WaterGoalMl;   // ml Ziel (z.B. 2500)
    public float Calories;      // kcal heute (z.B. 1200)
    public float CalorieGoal;   // kcal Ziel (z.B. 2000)
    public int DailyScore;      // 0-100
    public int WeightTrend;     // -1=runter, 0=gleich, +1=hoch
    public string BmiCategory;  // "Normal", "Uebergewicht" etc.
    public float Time;
    public bool HasData;
}

/// <summary>
/// Quadranten des Vital Signs Monitors fuer Touch-HitTest.
/// </summary>
public enum VitalQuadrant { None, Weight, Bmi, Water, Calories, Center }

/// <summary>
/// Kreisfoermiger Vital Signs Monitor (280x280dp) mit 4 Quadranten,
/// EKG-Ring, Center-Score, Data-Stream Partikel und Touch-HitTest.
/// Instance-basiert mit GC-freiem Render-Loop (Struct-Pool, gecachte Paints, Path.Reset).
/// </summary>
public sealed class VitalSignsHeroRenderer : IDisposable
{
    private bool _disposed;

    // =====================================================================
    // Beat-State
    // =====================================================================

    private float _beatTimer;
    private float _beatGlow;
    private float _sweepAngle; // Aktuelle Sweep-Position in Radiant (0 bis 2*PI)

    // =====================================================================
    // Center-Pulse
    // =====================================================================

    private float _centerPulseScale = 1f;
    private byte _centerGlowAlpha = 60;

    // =====================================================================
    // Data-Stream Partikel (8 max, Struct-Pool)
    // =====================================================================

    private struct DataParticle
    {
        public float X, Y, TargetX, TargetY;
        public float Alpha, Life, MaxLife;
        public bool Active;
        public byte ColorIndex; // 0-3 fuer Feature-Farbe
    }

    private readonly DataParticle[] _dataParticles = new DataParticle[8];

    // =====================================================================
    // Gecachte Paints (0 GC im Render-Loop)
    // =====================================================================

    private readonly SKPaint _ekgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private readonly SKPaint _ekgGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _segmentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private readonly SKPaint _textPaint = new() { IsAntialias = true };
    private readonly SKPaint _centerPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _centerRingPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };

    // =====================================================================
    // Gecachte Ressourcen
    // =====================================================================

    private readonly SKMaskFilter _glowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
    private readonly SKMaskFilter _centerGlowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);
    private readonly SKPath _ekgPath = new();
    private readonly SKPath _segmentPath = new();
    private readonly SKPath _iconPath = new();

    // =====================================================================
    // Feature-Farben als Array fuer Index-Zugriff
    // =====================================================================

    private static readonly SKColor[] FeatureColors =
    {
        MedicalColors.WeightPurple,  // 0 = NW
        MedicalColors.BmiBlue,       // 1 = NE
        MedicalColors.WaterGreen,    // 2 = SW
        MedicalColors.CalorieAmber   // 3 = SE
    };

    // =====================================================================
    // Update (vom Timer aufgerufen)
    // =====================================================================

    /// <summary>
    /// Aktualisiert Beat-Timer, Sweep-Winkel, Center-Pulse und Data-Stream Partikel.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Beat-Timer
        _beatTimer += deltaTime;
        if (_beatTimer >= MedicalColors.BeatPeriod)
        {
            _beatTimer -= MedicalColors.BeatPeriod;
            _beatGlow = 1f;

            // 1-2 Partikel pro Beat spawnen
            EmitDataParticle();
            if (Random.Shared.Next(2) == 0)
                EmitDataParticle();
        }

        // Beat-Glow abklingen
        if (_beatGlow > 0f)
            _beatGlow = MathF.Max(0f, _beatGlow - deltaTime * 4f);

        // Sweep-Winkel: Voller Umlauf pro Beat-Periode (72 BPM, im Uhrzeigersinn)
        _sweepAngle = (_beatTimer / MedicalColors.BeatPeriod) * MathF.Tau;

        // Center-Pulse: Scale pulsiert 1.0 -> 1.03 im Beat-Rhythmus
        float beatNorm = _beatTimer / MedicalColors.BeatPeriod;
        _centerPulseScale = 1f + 0.03f * MathF.Exp(-beatNorm * 6f);
        _centerGlowAlpha = (byte)(60 + 60 * MathF.Exp(-beatNorm * 4f));

        // Data-Stream Partikel aktualisieren
        for (int i = 0; i < _dataParticles.Length; i++)
        {
            if (!_dataParticles[i].Active) continue;
            ref var p = ref _dataParticles[i];
            p.Life += deltaTime;
            if (p.Life >= p.MaxLife)
            {
                p.Active = false;
                continue;
            }

            // Lineare Interpolation von Start zum Center
            float t = p.Life / p.MaxLife;
            p.X = Lerp(p.X, p.TargetX, deltaTime * 2f);
            p.Y = Lerp(p.Y, p.TargetY, deltaTime * 2f);

            // Alpha: Fade-In (0-20%), voll (20-70%), Fade-Out (70-100%)
            if (t < 0.2f)
                p.Alpha = t / 0.2f;
            else if (t < 0.7f)
                p.Alpha = 1f;
            else
                p.Alpha = 1f - (t - 0.7f) / 0.3f;
        }
    }

    // =====================================================================
    // Render (6 Layer)
    // =====================================================================

    /// <summary>
    /// Zeichnet den kompletten Vital Signs Monitor.
    /// bounds sollte quadratisch sein (280x280dp ideal).
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, VitalSignsState state)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float halfSize = MathF.Min(bounds.Width, bounds.Height) / 2f;

        // Partikel-Positionen auf absolute Koordinaten aktualisieren
        UpdateParticlePositions(cx, cy, halfSize);

        // Layer 1: Aeusserer EKG-Ring
        RenderEkgRing(canvas, cx, cy, halfSize);

        // Layer 2: Quadranten-Hintergruende
        RenderQuadrantBackgrounds(canvas, cx, cy, halfSize);

        // Layer 3: Kreuz-Trenner
        RenderCrossDivider(canvas, cx, cy, halfSize);

        // Layer 4: Quadranten-Inhalte
        RenderQuadrantContents(canvas, cx, cy, halfSize, state);

        // Layer 5: Center-Score
        RenderCenterScore(canvas, cx, cy, halfSize, state);

        // Layer 6: Data-Stream Partikel
        RenderDataParticles(canvas, cx, cy);
    }

    // =====================================================================
    // HitTest
    // =====================================================================

    /// <summary>
    /// Bestimmt welcher Quadrant an der Position (x, y) liegt.
    /// </summary>
    public VitalQuadrant HitTest(SKRect bounds, float x, float y)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float dx = x - cx;
        float dy = y - cy;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float outerRadius = MathF.Min(bounds.Width, bounds.Height) / 2f * 0.8f;
        float innerRadius = outerRadius * 0.375f; // ~30% der Haelfte

        if (dist > outerRadius) return VitalQuadrant.None;
        if (dist < innerRadius) return VitalQuadrant.Center;

        // Quadrant per Winkel bestimmen
        float angle = MathF.Atan2(dy, dx); // -PI bis +PI
        // NW: 180-270 Grad -> angle zwischen -PI und -PI/2
        // NE: 270-360 Grad -> angle zwischen -PI/2 und 0
        // SE: 0-90 Grad -> angle zwischen 0 und PI/2
        // SW: 90-180 Grad -> angle zwischen PI/2 und PI

        if (angle >= -MathF.PI && angle < -MathF.PI / 2f) return VitalQuadrant.Weight;     // NW
        if (angle >= -MathF.PI / 2f && angle < 0) return VitalQuadrant.Bmi;                 // NE
        if (angle >= 0 && angle < MathF.PI / 2f) return VitalQuadrant.Calories;              // SE
        return VitalQuadrant.Water;                                                            // SW
    }

    // =====================================================================
    // Layer 1: Aeusserer EKG-Ring
    // =====================================================================

    private void RenderEkgRing(SKCanvas canvas, float cx, float cy, float halfSize)
    {
        float ekgRadius = halfSize * 0.9f;
        float ekgAmplitude = halfSize * 0.06f;

        int waveLen = MedicalColors.EkgWave.Length;
        int cycles = 3;
        int totalPoints = waveLen * cycles;

        _ekgPath.Reset();

        bool firstPoint = true;
        for (int c = 0; c < cycles; c++)
        {
            for (int j = 0; j < waveLen; j++)
            {
                int totalIndex = c * waveLen + j;
                // Winkel fuer diesen Punkt + Sweep-Offset
                float angle = (float)totalIndex / totalPoints * MathF.Tau;
                // Radius = Basisradius + EKG-Amplitude * Welle
                float r = ekgRadius + MedicalColors.EkgWave[j] * ekgAmplitude;
                float px = cx + r * MathF.Cos(angle);
                float py = cy + r * MathF.Sin(angle);

                if (firstPoint)
                {
                    _ekgPath.MoveTo(px, py);
                    firstPoint = false;
                }
                else
                {
                    _ekgPath.LineTo(px, py);
                }
            }
        }
        _ekgPath.Close();

        // Trail-Effekt: Im Uhrzeigersinn vor dem Sweep verblasst der Trace
        // Sweep-Punkt bewegt sich mit _sweepAngle
        // Wir zeichnen den Ring mit einem konischen Shader fuer den Trail

        // Sweep-Punkt Position berechnen
        int sweepWaveIndex = (int)(_sweepAngle / MathF.Tau * waveLen) % waveLen;
        float sweepR = ekgRadius + MedicalColors.EkgWave[sweepWaveIndex] * ekgAmplitude;
        float sweepX = cx + sweepR * MathF.Cos(_sweepAngle);
        float sweepY = cy + sweepR * MathF.Sin(_sweepAngle);

        // EKG-Ring zeichnen mit Sweep-basierten Farben
        // Nutze SweepGradient fuer den Trail-Effekt
        float sweepDeg = _sweepAngle * 180f / MathF.PI;
        // Gradient: Am Sweep-Punkt voll sichtbar, dahinter (im Uhrzeigersinn) verblasst
        // CreateSweepGradient(center, colors, positions, tileMode, startAngle, endAngle)
        using var trailShader = SKShader.CreateSweepGradient(
            new SKPoint(cx, cy),
            new SKColor[]
            {
                MedicalColors.Cyan.WithAlpha(0),     // Startpunkt (vor dem Sweep)
                MedicalColors.Cyan.WithAlpha(20),     // 25% herum
                MedicalColors.Cyan.WithAlpha(80),     // 50% herum
                MedicalColors.Cyan.WithAlpha(200),    // 75% herum - fast beim Sweep
                MedicalColors.Cyan,                    // Am Sweep-Punkt
                MedicalColors.Cyan.WithAlpha(0),      // Direkt nach dem Sweep
            },
            new float[] { 0f, 0.25f, 0.5f, 0.85f, 0.95f, 1f },
            SKShaderTileMode.Clamp,
            // Rotation: Der Sweep-Punkt ist bei sweepDeg, also rotieren wir so
            // dass der Gradient-Start direkt nach dem Sweep liegt
            sweepDeg, sweepDeg + 360f);

        _ekgPaint.Shader = trailShader;
        _ekgPaint.Color = MedicalColors.Cyan;
        canvas.DrawPath(_ekgPath, _ekgPaint);
        _ekgPaint.Shader = null;

        // Glow am Sweep-Punkt (verstaerkt bei QRS-Spike)
        float glowSize = 4f + _beatGlow * 10f;
        byte glowAlpha = (byte)(80 + _beatGlow * 175);

        _ekgGlowPaint.Color = MedicalColors.Cyan.WithAlpha(glowAlpha);
        _ekgGlowPaint.MaskFilter = _glowMask;
        canvas.DrawCircle(sweepX, sweepY, glowSize, _ekgGlowPaint);
        _ekgGlowPaint.MaskFilter = null;

        // Fester Kern-Punkt
        _ekgGlowPaint.Color = MedicalColors.Cyan;
        canvas.DrawCircle(sweepX, sweepY, 2.5f, _ekgGlowPaint);
    }

    // =====================================================================
    // Layer 2: Quadranten-Hintergruende (4 Segmente)
    // =====================================================================

    private void RenderQuadrantBackgrounds(SKCanvas canvas, float cx, float cy, float halfSize)
    {
        float outerR = halfSize * 0.8f;
        float innerR = outerR * 0.375f;

        // Segmentwinkel: NW=180-270, NE=270-360, SW=90-180, SE=0-90
        // In SKPath ArcTo werden Winkel in Grad angegeben (0=rechts, im Uhrzeigersinn)
        DrawQuadrantSegment(canvas, cx, cy, outerR, innerR, 180f, 90f, MedicalColors.WeightPurple);  // NW
        DrawQuadrantSegment(canvas, cx, cy, outerR, innerR, 270f, 90f, MedicalColors.BmiBlue);       // NE
        DrawQuadrantSegment(canvas, cx, cy, outerR, innerR, 90f, 90f, MedicalColors.WaterGreen);     // SW
        DrawQuadrantSegment(canvas, cx, cy, outerR, innerR, 0f, 90f, MedicalColors.CalorieAmber);    // SE
    }

    private void DrawQuadrantSegment(SKCanvas canvas, float cx, float cy,
        float outerR, float innerR, float startAngle, float sweepAngle, SKColor color)
    {
        _segmentPath.Reset();

        var outerRect = new SKRect(cx - outerR, cy - outerR, cx + outerR, cy + outerR);
        var innerRect = new SKRect(cx - innerR, cy - innerR, cx + innerR, cy + innerR);

        // Aeusserer Bogen (im Uhrzeigersinn)
        _segmentPath.ArcTo(outerRect, startAngle, sweepAngle, true);
        // Linie zum inneren Bogen-Ende
        float endAngleRad = (startAngle + sweepAngle) * MathF.PI / 180f;
        float innerEndX = cx + innerR * MathF.Cos(endAngleRad);
        float innerEndY = cy + innerR * MathF.Sin(endAngleRad);
        _segmentPath.LineTo(innerEndX, innerEndY);
        // Innerer Bogen (gegen den Uhrzeigersinn = negativ)
        _segmentPath.ArcTo(innerRect, startAngle + sweepAngle, -sweepAngle, false);
        _segmentPath.Close();

        _segmentPaint.Color = color.WithAlpha(25);
        canvas.DrawPath(_segmentPath, _segmentPaint);
    }

    // =====================================================================
    // Layer 3: Kreuz-Trenner
    // =====================================================================

    private void RenderCrossDivider(SKCanvas canvas, float cx, float cy, float halfSize)
    {
        float outerR = halfSize * 0.8f;
        float innerR = outerR * 0.375f;

        _linePaint.Color = MedicalColors.Cyan.WithAlpha(25);

        // Horizontale Linie
        canvas.DrawLine(cx - outerR, cy, cx - innerR, cy, _linePaint);
        canvas.DrawLine(cx + innerR, cy, cx + outerR, cy, _linePaint);

        // Vertikale Linie
        canvas.DrawLine(cx, cy - outerR, cx, cy - innerR, _linePaint);
        canvas.DrawLine(cx, cy + innerR, cx, cy + outerR, _linePaint);
    }

    // =====================================================================
    // Layer 4: Quadranten-Inhalte
    // =====================================================================

    private void RenderQuadrantContents(SKCanvas canvas, float cx, float cy, float halfSize, VitalSignsState state)
    {
        float outerR = halfSize * 0.8f;
        float innerR = outerR * 0.375f;
        // Zentrum jedes Quadranten: Mitte zwischen innerem und aeusserem Radius, bei 45 Grad-Winkel
        float midR = (outerR + innerR) / 2f;

        // NW - Gewicht (225 Grad = -135 Grad)
        float nwAngle = 225f * MathF.PI / 180f;
        float nwX = cx + midR * MathF.Cos(nwAngle);
        float nwY = cy + midR * MathF.Sin(nwAngle);
        RenderWeightQuadrant(canvas, nwX, nwY, halfSize, state);

        // NE - BMI (315 Grad = -45 Grad)
        float neAngle = 315f * MathF.PI / 180f;
        float neX = cx + midR * MathF.Cos(neAngle);
        float neY = cy + midR * MathF.Sin(neAngle);
        RenderBmiQuadrant(canvas, neX, neY, halfSize, state);

        // SW - Wasser (135 Grad)
        float swAngle = 135f * MathF.PI / 180f;
        float swX = cx + midR * MathF.Cos(swAngle);
        float swY = cy + midR * MathF.Sin(swAngle);
        RenderWaterQuadrant(canvas, swX, swY, halfSize, state);

        // SE - Kalorien (45 Grad)
        float seAngle = 45f * MathF.PI / 180f;
        float seX = cx + midR * MathF.Cos(seAngle);
        float seY = cy + midR * MathF.Sin(seAngle);
        RenderCalorieQuadrant(canvas, seX, seY, halfSize, state);
    }

    /// <summary>
    /// NW-Quadrant: Gewicht mit Waagen-Icon und Trend-Pfeil.
    /// </summary>
    private void RenderWeightQuadrant(SKCanvas canvas, float qx, float qy, float halfSize, VitalSignsState state)
    {
        float scale = halfSize / 140f; // Skalierung relativ zur idealen Haelfte (280/2)

        // Icon (kleine Waage)
        DrawScaleIcon(canvas, qx, qy - 14f * scale, 10f * scale, MedicalColors.WeightPurple);

        // Gewichtswert
        _textPaint.Color = MedicalColors.TextPrimary;
        _textPaint.TextSize = 18f * scale;
        _textPaint.FakeBoldText = true;
        _textPaint.TextAlign = SKTextAlign.Center;
        canvas.DrawText(state.Weight.ToString("F1"), qx, qy + 4f * scale, _textPaint);

        // "kg" Label
        _textPaint.Color = MedicalColors.TextMuted;
        _textPaint.TextSize = 10f * scale;
        _textPaint.FakeBoldText = false;
        float kgY = qy + 16f * scale;

        // Trend-Pfeil neben "kg"
        string trendArrow;
        SKColor trendColor;
        if (state.WeightTrend < 0)
        {
            trendArrow = "kg \u2193"; // Pfeil runter
            trendColor = MedicalColors.WaterGreen;
        }
        else if (state.WeightTrend > 0)
        {
            trendArrow = "kg \u2191"; // Pfeil hoch
            trendColor = MedicalColors.CriticalRed;
        }
        else
        {
            trendArrow = "kg \u2192"; // Pfeil rechts
            trendColor = MedicalColors.TextMuted;
        }

        // "kg" zeichnen
        canvas.DrawText("kg", qx - 6f * scale, kgY, _textPaint);

        // Trend-Pfeil
        _textPaint.Color = trendColor;
        _textPaint.TextSize = 12f * scale;
        string arrow = state.WeightTrend < 0 ? "\u2193" : state.WeightTrend > 0 ? "\u2191" : "\u2192";
        canvas.DrawText(arrow, qx + 10f * scale, kgY, _textPaint);
    }

    /// <summary>
    /// NE-Quadrant: BMI mit Gauge-Icon und farbcodierter Kategorie.
    /// </summary>
    private void RenderBmiQuadrant(SKCanvas canvas, float qx, float qy, float halfSize, VitalSignsState state)
    {
        float scale = halfSize / 140f;

        // Icon (kleines Gauge)
        DrawGaugeIcon(canvas, qx, qy - 14f * scale, 10f * scale, MedicalColors.BmiBlue);

        // BMI-Wert
        _textPaint.Color = MedicalColors.TextPrimary;
        _textPaint.TextSize = 18f * scale;
        _textPaint.FakeBoldText = true;
        _textPaint.TextAlign = SKTextAlign.Center;
        canvas.DrawText(state.Bmi.ToString("F1"), qx, qy + 4f * scale, _textPaint);

        // Kategorie-Text (farbcodiert)
        SKColor categoryColor = GetBmiCategoryColor(state.BmiCategory);
        _textPaint.Color = categoryColor;
        _textPaint.TextSize = 10f * scale;
        _textPaint.FakeBoldText = false;
        string categoryText = state.BmiCategory ?? "";
        // Kuerzen wenn zu lang
        if (categoryText.Length > 12) categoryText = categoryText[..12];
        canvas.DrawText(categoryText, qx, qy + 16f * scale, _textPaint);
    }

    /// <summary>
    /// SW-Quadrant: Wasser mit Tropfen-Icon und Fortschritts-Arc.
    /// </summary>
    private void RenderWaterQuadrant(SKCanvas canvas, float qx, float qy, float halfSize, VitalSignsState state)
    {
        float scale = halfSize / 140f;

        // Icon (kleiner Tropfen)
        DrawDropIcon(canvas, qx, qy - 14f * scale, 10f * scale, MedicalColors.WaterGreen);

        // Wasser-Wert formatieren
        string waterText;
        if (state.WaterMl >= 1000f)
            waterText = (state.WaterMl / 1000f).ToString("F1") + "L";
        else
            waterText = state.WaterMl.ToString("F0") + "ml";

        _textPaint.Color = MedicalColors.TextPrimary;
        _textPaint.TextSize = 18f * scale;
        _textPaint.FakeBoldText = true;
        _textPaint.TextAlign = SKTextAlign.Center;
        canvas.DrawText(waterText, qx, qy + 4f * scale, _textPaint);

        // Mini-Arc fuer Fortschritt
        float progress = state.WaterGoalMl > 0 ? Math.Clamp(state.WaterMl / state.WaterGoalMl, 0f, 1f) : 0f;
        if (progress > 0)
            DrawMiniArc(canvas, qx, qy + 12f * scale, 14f * scale, progress, MedicalColors.WaterGreen);
    }

    /// <summary>
    /// SE-Quadrant: Kalorien mit Flammen-Icon und Fortschritts-Arc.
    /// </summary>
    private void RenderCalorieQuadrant(SKCanvas canvas, float qx, float qy, float halfSize, VitalSignsState state)
    {
        float scale = halfSize / 140f;

        // Icon (kleine Flamme)
        DrawFlameIcon(canvas, qx, qy - 14f * scale, 10f * scale, MedicalColors.CalorieAmber);

        // Kalorien-Wert
        _textPaint.Color = MedicalColors.TextPrimary;
        _textPaint.TextSize = 18f * scale;
        _textPaint.FakeBoldText = true;
        _textPaint.TextAlign = SKTextAlign.Center;
        canvas.DrawText(state.Calories.ToString("F0"), qx, qy + 4f * scale, _textPaint);

        // "kcal" Label
        _textPaint.Color = MedicalColors.TextMuted;
        _textPaint.TextSize = 10f * scale;
        _textPaint.FakeBoldText = false;
        canvas.DrawText("kcal", qx, qy + 16f * scale, _textPaint);

        // Mini-Arc fuer Fortschritt
        float progress = state.CalorieGoal > 0 ? Math.Clamp(state.Calories / state.CalorieGoal, 0f, 1f) : 0f;
        if (progress > 0)
            DrawMiniArc(canvas, qx, qy + 12f * scale, 14f * scale, progress, MedicalColors.CalorieAmber);
    }

    // =====================================================================
    // Layer 5: Center-Score
    // =====================================================================

    private void RenderCenterScore(SKCanvas canvas, float cx, float cy, float halfSize, VitalSignsState state)
    {
        float outerR = halfSize * 0.8f;
        float centerR = outerR * 0.3f;

        // Hintergrund-Kreis
        _centerPaint.Color = MedicalColors.NavyDeep;
        canvas.DrawCircle(cx, cy, centerR, _centerPaint);

        // Pulsierender Cyan-Ring
        float ringR = centerR * _centerPulseScale;
        _centerRingPaint.Color = MedicalColors.Cyan.WithAlpha(_centerGlowAlpha);
        _centerRingPaint.MaskFilter = _centerGlowMask;
        canvas.DrawCircle(cx, cy, ringR, _centerRingPaint);
        _centerRingPaint.MaskFilter = null;

        // Scharfer Ring darueber
        _centerRingPaint.Color = MedicalColors.Cyan.WithAlpha(180);
        canvas.DrawCircle(cx, cy, centerR, _centerRingPaint);

        // Score-Zahl
        float scale = halfSize / 140f;
        _textPaint.Color = MedicalColors.TextPrimary;
        _textPaint.TextSize = 24f * scale;
        _textPaint.FakeBoldText = true;
        _textPaint.TextAlign = SKTextAlign.Center;
        canvas.DrawText(state.DailyScore.ToString(), cx, cy + 3f * scale, _textPaint);

        // "Score" Label darunter
        _textPaint.Color = MedicalColors.TextMuted;
        _textPaint.TextSize = 9f * scale;
        _textPaint.FakeBoldText = false;
        canvas.DrawText("Score", cx, cy + 15f * scale, _textPaint);
    }

    // =====================================================================
    // Layer 6: Data-Stream Partikel
    // =====================================================================

    private void RenderDataParticles(SKCanvas canvas, float cx, float cy)
    {
        for (int i = 0; i < _dataParticles.Length; i++)
        {
            if (!_dataParticles[i].Active) continue;
            ref var p = ref _dataParticles[i];

            if (p.Alpha < 0.01f) continue;

            byte alpha = (byte)(p.Alpha * 255f);
            SKColor color = FeatureColors[p.ColorIndex % FeatureColors.Length];
            _particlePaint.Color = color.WithAlpha(alpha);

            // Leuchtender Punkt (2-3px)
            float radius = 2f + p.Alpha;
            canvas.DrawCircle(p.X, p.Y, radius, _particlePaint);

            // Subtiler Glow
            _particlePaint.MaskFilter = _glowMask;
            _particlePaint.Color = color.WithAlpha((byte)(alpha / 3));
            canvas.DrawCircle(p.X, p.Y, radius * 2f, _particlePaint);
            _particlePaint.MaskFilter = null;
        }
    }

    // =====================================================================
    // Hilfs-Zeichenmethoden
    // =====================================================================

    /// <summary>
    /// Zeichnet einen Mini-Fortschrittsbogen unter einem Quadranten-Wert.
    /// </summary>
    private void DrawMiniArc(SKCanvas canvas, float cx, float cy, float radius, float progress, SKColor color)
    {
        // Track (Hintergrund)
        _arcPaint.Color = color.WithAlpha(30);
        var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
        canvas.DrawArc(rect, 180f, 180f, false, _arcPaint);

        // Fortschritt
        float sweepAngle = 180f * progress;
        _arcPaint.Color = color;
        canvas.DrawArc(rect, 180f, sweepAngle, false, _arcPaint);
    }

    /// <summary>
    /// Zeichnet ein kleines Waagen-Icon.
    /// </summary>
    private void DrawScaleIcon(SKCanvas canvas, float x, float y, float size, SKColor color)
    {
        float s = size * 0.5f;
        _iconPaint.Color = color;

        // Waage: Balken oben + Stuetze + Basis
        canvas.DrawLine(x - s, y - s * 0.3f, x + s, y - s * 0.3f, _iconPaint);
        canvas.DrawLine(x, y - s * 0.3f, x, y + s * 0.4f, _iconPaint);

        _iconPath.Reset();
        _iconPath.MoveTo(x - s * 0.5f, y + s * 0.6f);
        _iconPath.LineTo(x + s * 0.5f, y + s * 0.6f);
        _iconPath.LineTo(x, y + s * 0.3f);
        _iconPath.Close();
        _iconPaint.Style = SKPaintStyle.Fill;
        canvas.DrawPath(_iconPath, _iconPaint);
        _iconPaint.Style = SKPaintStyle.Stroke;
    }

    /// <summary>
    /// Zeichnet ein kleines Gauge/Tacho-Icon.
    /// </summary>
    private void DrawGaugeIcon(SKCanvas canvas, float x, float y, float size, SKColor color)
    {
        float s = size * 0.5f;
        _iconPaint.Color = color;

        // Halbkreis-Bogen
        var rect = new SKRect(x - s, y - s * 0.3f, x + s, y + s * 0.7f);
        canvas.DrawArc(rect, 180f, 180f, false, _iconPaint);

        // Nadel (von Mitte nach oben-rechts)
        float nadelEndX = x + s * 0.4f;
        float nadelEndY = y - s * 0.1f;
        canvas.DrawLine(x, y + s * 0.2f, nadelEndX, nadelEndY, _iconPaint);

        // Kleiner Punkt in der Mitte
        _iconPaint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(x, y + s * 0.2f, 1.5f, _iconPaint);
        _iconPaint.Style = SKPaintStyle.Stroke;
    }

    /// <summary>
    /// Zeichnet ein kleines Tropfen-Icon.
    /// </summary>
    private void DrawDropIcon(SKCanvas canvas, float x, float y, float size, SKColor color)
    {
        float s = size * 0.5f;
        _iconPaint.Color = color;
        _iconPaint.Style = SKPaintStyle.Fill;

        _iconPath.Reset();
        _iconPath.MoveTo(x, y - s);  // Spitze oben
        _iconPath.CubicTo(
            x - s * 0.8f, y + s * 0.1f,
            x - s * 0.6f, y + s * 0.8f,
            x, y + s);
        _iconPath.CubicTo(
            x + s * 0.6f, y + s * 0.8f,
            x + s * 0.8f, y + s * 0.1f,
            x, y - s);
        _iconPath.Close();

        canvas.DrawPath(_iconPath, _iconPaint);
        _iconPaint.Style = SKPaintStyle.Stroke;
    }

    /// <summary>
    /// Zeichnet ein kleines Flammen-Icon.
    /// </summary>
    private void DrawFlameIcon(SKCanvas canvas, float x, float y, float size, SKColor color)
    {
        float s = size * 0.5f;
        _iconPaint.Color = color;
        _iconPaint.Style = SKPaintStyle.Fill;

        _iconPath.Reset();
        _iconPath.MoveTo(x, y - s);                               // Flammenspitze oben
        _iconPath.CubicTo(x + s * 0.6f, y - s * 0.2f,            // Rechte Seite nach aussen
                          x + s * 0.7f, y + s * 0.5f,
                          x, y + s);                                // Unten Mitte
        _iconPath.CubicTo(x - s * 0.7f, y + s * 0.5f,            // Linke Seite nach aussen
                          x - s * 0.6f, y - s * 0.2f,
                          x, y - s);                                // Zurueck zur Spitze
        _iconPath.Close();

        canvas.DrawPath(_iconPath, _iconPaint);
        _iconPaint.Style = SKPaintStyle.Stroke;
    }

    // =====================================================================
    // Partikel-Emission
    // =====================================================================

    /// <summary>
    /// Erzeugt einen Data-Stream Partikel aus einem zufaelligen Quadranten zum Center.
    /// </summary>
    private void EmitDataParticle()
    {
        // Freien Slot suchen
        int freeSlot = -1;
        for (int i = 0; i < _dataParticles.Length; i++)
        {
            if (!_dataParticles[i].Active)
            {
                freeSlot = i;
                break;
            }
        }
        if (freeSlot < 0) return; // Pool voll

        ref var p = ref _dataParticles[freeSlot];
        p.Active = true;
        p.Life = 0f;
        p.MaxLife = 0.8f + Random.Shared.NextSingle() * 0.4f; // 0.8-1.2s
        p.Alpha = 0f;
        p.ColorIndex = (byte)Random.Shared.Next(4);

        // Startposition: Quadranten-Zentren werden relativ gesetzt
        // Die tatsaechlichen Positionen werden beim naechsten Render ermittelt
        // Hier setzen wir normalisierte Offsets die im Update per Lerp zum Center wandern
        float angle = p.ColorIndex switch
        {
            0 => 225f, // NW - Gewicht
            1 => 315f, // NE - BMI
            2 => 135f, // SW - Wasser
            _ => 45f   // SE - Kalorien
        };
        float rad = angle * MathF.PI / 180f;
        // Start-Offset: Wird spaeter durch die absolute Position ersetzt
        // Speichere als relative Offsets die bei Render in absolut konvertiert werden
        p.X = MathF.Cos(rad) * 40f; // Wird spaeter + cx
        p.Y = MathF.Sin(rad) * 40f; // Wird spaeter + cy
        p.TargetX = 0f;
        p.TargetY = 0f;
    }

    /// <summary>
    /// Setzt die Partikel-Positionen auf absolute Koordinaten.
    /// Muss vor dem ersten Render aufgerufen werden.
    /// </summary>
    private void UpdateParticlePositions(float cx, float cy, float halfSize)
    {
        float midR = halfSize * 0.5f;
        for (int i = 0; i < _dataParticles.Length; i++)
        {
            if (!_dataParticles[i].Active) continue;
            ref var p = ref _dataParticles[i];

            // Wenn Partikel gerade erst gespawned wurde (Life < deltaTime),
            // Position auf absolute Werte setzen
            if (p.Life < 0.05f && MathF.Abs(p.TargetX) < 1f)
            {
                float angle = p.ColorIndex switch
                {
                    0 => 225f,
                    1 => 315f,
                    2 => 135f,
                    _ => 45f
                };
                float rad = angle * MathF.PI / 180f;
                p.X = cx + midR * MathF.Cos(rad);
                p.Y = cy + midR * MathF.Sin(rad);
                p.TargetX = cx;
                p.TargetY = cy;
            }
        }
    }

    // =====================================================================
    // Hilfsfunktionen
    // =====================================================================

    /// <summary>
    /// Bestimmt die Farbe fuer eine BMI-Kategorie.
    /// </summary>
    private static SKColor GetBmiCategoryColor(string? category)
    {
        if (string.IsNullOrEmpty(category)) return MedicalColors.TextMuted;

        // Einfache Zuordnung ueber Keywords
        string lower = category.ToLowerInvariant();
        if (lower.Contains("unter") || lower.Contains("under"))
            return MedicalColors.BmiBlueLight;
        if (lower.Contains("normal"))
            return MedicalColors.WaterGreen;
        if (lower.Contains("ueber") || lower.Contains("over") || lower.Contains("prä") || lower.Contains("pre"))
            return MedicalColors.CalorieAmber;
        if (lower.Contains("adip") || lower.Contains("obes"))
            return MedicalColors.CriticalRed;

        return MedicalColors.TextMuted;
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * Math.Clamp(t, 0f, 1f);
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _ekgPaint.Dispose();
        _ekgGlowPaint.Dispose();
        _segmentPaint.Dispose();
        _linePaint.Dispose();
        _textPaint.Dispose();
        _centerPaint.Dispose();
        _centerRingPaint.Dispose();
        _arcPaint.Dispose();
        _particlePaint.Dispose();
        _iconPaint.Dispose();

        _glowMask.Dispose();
        _centerGlowMask.Dispose();

        _ekgPath.Dispose();
        _segmentPath.Dispose();
        _iconPath.Dispose();
    }
}
