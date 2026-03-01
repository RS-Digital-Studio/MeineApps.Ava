using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Ambient-Partikel pro Welt: Hintergrund-Effekte UNTER dem Grid, ÜBER dem Hintergrund.
/// Struct-basiert, max 60 Partikel. Jede Welt hat eigene atmosphärische Effekte.
/// </summary>
public sealed class AmbientParticleSystem : IDisposable
{
    private struct AmbientParticle
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Phase;      // Individuelle Phase für sin-basierte Effekte
        public byte Type;        // Sub-Typ für Varianz innerhalb einer Welt
    }

    private const int MAX_PARTICLES = 60;
    private readonly AmbientParticle[] _particles = new AmbientParticle[MAX_PARTICLES];
    private int _activeCount;

    private float _spawnTimer;
    private int _worldIndex;
    private float _fieldWidth, _fieldHeight;

    // Gepoolte Paint-Objekte
    private readonly SKPaint _fillPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _strokePaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKMaskFilter _glow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3);

    // Gepoolter SKPath für Seegras-Halme (statt pro-Partikel new SKPath())
    private readonly SKPath _tempPath = new();

    /// <summary>
    /// Welt-Index setzen (0-9) + Spielfeld-Grenzen
    /// </summary>
    public void SetWorld(int worldIndex, float fieldWidth, float fieldHeight)
    {
        _worldIndex = worldIndex;
        _fieldWidth = fieldWidth;
        _fieldHeight = fieldHeight;
        _activeCount = 0;
        _spawnTimer = 0;
    }

    /// <summary>
    /// Ambient-Partikel aktualisieren + spawnen
    /// </summary>
    public void Update(float deltaTime, float globalTimer)
    {
        // Bestehende Partikel updaten
        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];
            p.Life -= deltaTime;

            if (p.Life <= 0)
            {
                // Entfernen: Letzten Partikel an diese Stelle swappen
                _particles[i] = _particles[--_activeCount];
                i--;
                continue;
            }

            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
        }

        // Spawn-Rate je nach Welt
        float spawnInterval = GetSpawnInterval();
        _spawnTimer += deltaTime;
        while (_spawnTimer >= spawnInterval && _activeCount < MAX_PARTICLES)
        {
            _spawnTimer -= spawnInterval;
            SpawnParticle(globalTimer);
        }
    }

    /// <summary>
    /// Ambient-Partikel rendern (innerhalb des Spielfeld-Canvas-Transforms)
    /// </summary>
    public void Render(SKCanvas canvas, float globalTimer)
    {
        if (_activeCount == 0) return;

        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];
            float lifeRatio = p.Life / p.MaxLife;

            switch (_worldIndex)
            {
                case 0: RenderFirefly(canvas, ref p, globalTimer, lifeRatio); break;
                case 1: RenderSteamCloud(canvas, ref p, lifeRatio); break;
                case 2: RenderCrystalShimmer(canvas, ref p, globalTimer, lifeRatio); break;
                case 3: RenderBirdSilhouette(canvas, ref p, globalTimer); break;
                case 4: RenderEmberVein(canvas, ref p, globalTimer, lifeRatio); break;
                case 5: RenderDustWhirl(canvas, ref p, globalTimer, lifeRatio); break;
                case 6: RenderSeaweed(canvas, ref p, globalTimer, lifeRatio); break;
                case 7: RenderMagmaBubble(canvas, ref p, globalTimer, lifeRatio); break;
                case 8: RenderGoldenRay(canvas, ref p, globalTimer, lifeRatio); break;
                case 9: RenderBlinkingEye(canvas, ref p, globalTimer, lifeRatio); break;
            }
        }
    }

    public void Dispose()
    {
        _tempPath.Dispose();
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _glow.Dispose();
    }

    // --- Spawn-Intervall pro Welt ---
    private float GetSpawnInterval() => _worldIndex switch
    {
        0 => 0.4f,   // Forest: Glühwürmchen
        1 => 0.8f,   // Industrial: Dampf
        2 => 0.5f,   // Cavern: Kristall-Schimmer
        3 => 1.5f,   // Sky: Vögel
        4 => 0.6f,   // Inferno: Glut-Adern
        5 => 0.7f,   // Ruins: Staub
        6 => 0.5f,   // Ocean: Seegras
        7 => 0.8f,   // Volcano: Magma-Blasen
        8 => 0.6f,   // SkyFortress: Goldstrahlen
        9 => 1.0f,   // ShadowRealm: Augen
        _ => 1.0f
    };

    // --- Partikel spawnen ---
    private void SpawnParticle(float globalTimer)
    {
        ref var p = ref _particles[_activeCount++];
        p.Phase = globalTimer + _activeCount * 1.37f;
        p.Type = (byte)(_activeCount % 3);

        switch (_worldIndex)
        {
            case 0: // Glühwürmchen: langsames Wandern überall
                p.X = Random.Shared.NextSingle() * _fieldWidth;
                p.Y = Random.Shared.NextSingle() * _fieldHeight;
                p.VelocityX = (Random.Shared.NextSingle() - 0.5f) * 8f;
                p.VelocityY = (Random.Shared.NextSingle() - 0.5f) * 8f;
                p.Size = 2f + Random.Shared.NextSingle() * 1.5f;
                p.MaxLife = p.Life = 4f + Random.Shared.NextSingle() * 3f;
                break;

            case 1: // Dampfwolken: aufsteigend
                p.X = Random.Shared.NextSingle() * _fieldWidth;
                p.Y = _fieldHeight;
                p.VelocityX = (Random.Shared.NextSingle() - 0.5f) * 5f;
                p.VelocityY = -10f - Random.Shared.NextSingle() * 8f;
                p.Size = 8f + Random.Shared.NextSingle() * 6f;
                p.MaxLife = p.Life = 3f + Random.Shared.NextSingle() * 2f;
                break;

            case 2: // Kristall-Schimmer: stationär, blinken
                p.X = Random.Shared.NextSingle() * _fieldWidth;
                p.Y = Random.Shared.NextSingle() * _fieldHeight;
                p.VelocityX = 0;
                p.VelocityY = 0;
                p.Size = 1.5f + Random.Shared.NextSingle();
                p.MaxLife = p.Life = 1.5f + Random.Shared.NextSingle() * 2f;
                break;

            case 3: // Vögel: horizontal fliegend
                p.X = -20f;
                p.Y = Random.Shared.NextSingle() * _fieldHeight * 0.5f;
                p.VelocityX = 25f + Random.Shared.NextSingle() * 15f;
                p.VelocityY = (Random.Shared.NextSingle() - 0.5f) * 3f;
                p.Size = 3f;
                p.MaxLife = p.Life = _fieldWidth / p.VelocityX + 1f;
                break;

            case 4: // Glut-Adern: am Boden, pulsierend
                p.X = Random.Shared.NextSingle() * _fieldWidth;
                p.Y = _fieldHeight * (0.6f + Random.Shared.NextSingle() * 0.4f);
                p.VelocityX = 0;
                p.VelocityY = 0;
                p.Size = 15f + Random.Shared.NextSingle() * 20f;
                p.MaxLife = p.Life = 2f + Random.Shared.NextSingle() * 2f;
                break;

            case 5: // Staub-Wirbel: horizontal driftend
                p.X = Random.Shared.NextSingle() * _fieldWidth;
                p.Y = _fieldHeight * (0.5f + Random.Shared.NextSingle() * 0.5f);
                p.VelocityX = 5f + Random.Shared.NextSingle() * 10f;
                p.VelocityY = (Random.Shared.NextSingle() - 0.5f) * 3f;
                p.Size = 4f + Random.Shared.NextSingle() * 3f;
                p.MaxLife = p.Life = 3f + Random.Shared.NextSingle() * 2f;
                break;

            case 6: // Seegras: am unteren Rand, wogend
                p.X = Random.Shared.NextSingle() * _fieldWidth;
                p.Y = _fieldHeight - 5f;
                p.VelocityX = 0;
                p.VelocityY = 0;
                p.Size = 10f + Random.Shared.NextSingle() * 8f;
                p.MaxLife = p.Life = 5f + Random.Shared.NextSingle() * 3f;
                break;

            case 7: // Magma-Blasen: steigen am Rand auf
                p.X = Random.Shared.NextSingle() * _fieldWidth;
                p.Y = _fieldHeight + 5f;
                p.VelocityX = (Random.Shared.NextSingle() - 0.5f) * 3f;
                p.VelocityY = -8f - Random.Shared.NextSingle() * 6f;
                p.Size = 3f + Random.Shared.NextSingle() * 3f;
                p.MaxLife = p.Life = 2f + Random.Shared.NextSingle() * 2f;
                break;

            case 8: // Goldene Lichtstrahlen: diagonale Streifen
                p.X = Random.Shared.NextSingle() * _fieldWidth;
                p.Y = -10f;
                p.VelocityX = 8f;
                p.VelocityY = 12f;
                p.Size = 2f + Random.Shared.NextSingle() * 2f;
                p.MaxLife = p.Life = 4f + Random.Shared.NextSingle() * 2f;
                break;

            case 9: // Augenpaare: stationär, blinzeln
                p.X = Random.Shared.NextSingle() * _fieldWidth;
                p.Y = Random.Shared.NextSingle() * _fieldHeight;
                p.VelocityX = 0;
                p.VelocityY = 0;
                p.Size = 2.5f;
                p.MaxLife = p.Life = 6f + Random.Shared.NextSingle() * 4f;
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WELT-SPEZIFISCHE RENDER-METHODEN
    // ═══════════════════════════════════════════════════════════════════════

    // Welt 0: Forest - Glühwürmchen (leuchtende wandernde Punkte mit sanftem Glow)
    private void RenderFirefly(SKCanvas canvas, ref AmbientParticle p, float gt, float lifeRatio)
    {
        float brightness = MathF.Sin(gt * 2.5f + p.Phase) * 0.4f + 0.6f;
        byte alpha = (byte)(120 * brightness * MathF.Min(lifeRatio * 3f, 1f));

        // Glow-Halo
        _fillPaint.Color = new SKColor(180, 255, 100, (byte)(alpha * 0.4f));
        _fillPaint.MaskFilter = _glow;
        canvas.DrawCircle(p.X, p.Y, p.Size * 2f, _fillPaint);

        // Heller Kern
        _fillPaint.Color = new SKColor(220, 255, 150, alpha);
        _fillPaint.MaskFilter = null;
        canvas.DrawCircle(p.X, p.Y, p.Size * 0.5f, _fillPaint);
    }

    // Welt 1: Industrial - Dampfwolken (aufsteigende weiße Nebel-Patches)
    private void RenderSteamCloud(SKCanvas canvas, ref AmbientParticle p, float lifeRatio)
    {
        byte alpha = (byte)(30 * MathF.Min(lifeRatio * 2f, 1f) * MathF.Min((1f - lifeRatio) * 3f, 1f));
        _fillPaint.Color = new SKColor(200, 210, 220, alpha);
        _fillPaint.MaskFilter = null;
        float expansion = 1f + (1f - lifeRatio) * 0.5f;
        canvas.DrawOval(p.X, p.Y, p.Size * expansion, p.Size * expansion * 0.6f, _fillPaint);
    }

    // Welt 2: Cavern - Kristall-Schimmer (Lichtpunkte die an Wänden reflektieren)
    private void RenderCrystalShimmer(SKCanvas canvas, ref AmbientParticle p, float gt, float lifeRatio)
    {
        float shimmer = MathF.Max(0, MathF.Sin(gt * 5f + p.Phase) * 2f - 0.8f); // Kurze helle Blitze
        if (shimmer < 0.01f) return;

        byte alpha = (byte)(200 * shimmer * MathF.Min(lifeRatio * 2f, 1f));
        SKColor color = p.Type switch
        {
            0 => new SKColor(100, 200, 255, alpha),  // Blau
            1 => new SKColor(255, 150, 255, alpha),  // Pink
            _ => new SKColor(180, 255, 180, alpha)   // Grün
        };

        _fillPaint.Color = color;
        _fillPaint.MaskFilter = _glow;
        canvas.DrawCircle(p.X, p.Y, p.Size * shimmer, _fillPaint);
        _fillPaint.MaskFilter = null;
    }

    // Welt 3: Sky - Vögel-Silhouetten (V-Formen die langsam fliegen)
    private void RenderBirdSilhouette(SKCanvas canvas, ref AmbientParticle p, float gt)
    {
        float wingFlap = MathF.Sin(gt * 6f + p.Phase) * 3f;
        _strokePaint.Color = new SKColor(40, 40, 60, 50);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.MaskFilter = null;

        // V-Form mit Flügelschlag
        canvas.DrawLine(p.X - p.Size * 2, p.Y + wingFlap, p.X, p.Y, _strokePaint);
        canvas.DrawLine(p.X, p.Y, p.X + p.Size * 2, p.Y + wingFlap, _strokePaint);
    }

    // Welt 4: Inferno - Glut-Adern im Boden (pulsierende orange Linien)
    private void RenderEmberVein(SKCanvas canvas, ref AmbientParticle p, float gt, float lifeRatio)
    {
        float pulse = MathF.Sin(gt * 2f + p.Phase) * 0.4f + 0.6f;
        byte alpha = (byte)(40 * pulse * MathF.Min(lifeRatio * 2f, 1f) * MathF.Min((1f - lifeRatio) * 2f, 1f));

        _strokePaint.Color = new SKColor(255, 120, 30, alpha);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.MaskFilter = _glow;

        // Wellige Linie
        float halfLen = p.Size * 0.5f;
        float wave = MathF.Sin(gt * 1.5f + p.Phase) * 3f;
        canvas.DrawLine(p.X - halfLen, p.Y, p.X, p.Y + wave, _strokePaint);
        canvas.DrawLine(p.X, p.Y + wave, p.X + halfLen, p.Y, _strokePaint);
        _strokePaint.MaskFilter = null;
    }

    // Welt 5: Ruins - Staub-Wirbel (kleine drehende Staubwölkchen)
    private void RenderDustWhirl(SKCanvas canvas, ref AmbientParticle p, float gt, float lifeRatio)
    {
        byte alpha = (byte)(35 * MathF.Min(lifeRatio * 2f, 1f) * MathF.Min((1f - lifeRatio) * 2f, 1f));
        float rotation = gt * 2f + p.Phase;

        // 3 kleine Punkte die sich drehen
        for (int i = 0; i < 3; i++)
        {
            float angle = rotation + i * 2.094f; // 2*PI/3
            float dist = p.Size * 0.4f;
            float dx = MathF.Cos(angle) * dist;
            float dy = MathF.Sin(angle) * dist * 0.6f; // Elliptisch

            _fillPaint.Color = new SKColor(180, 160, 120, alpha);
            _fillPaint.MaskFilter = null;
            canvas.DrawCircle(p.X + dx, p.Y + dy, 1.5f, _fillPaint);
        }
    }

    // Welt 6: Ocean - Seegras (wellenförmige grüne Streifen am unteren Rand)
    private void RenderSeaweed(SKCanvas canvas, ref AmbientParticle p, float gt, float lifeRatio)
    {
        byte alpha = (byte)(50 * MathF.Min(lifeRatio * 3f, 1f) * MathF.Min((1f - lifeRatio) * 3f, 1f));
        float sway = MathF.Sin(gt * 1.2f + p.Phase) * 5f;

        _strokePaint.Color = new SKColor(40, 140, 60, alpha);
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.MaskFilter = null;

        // Geschwungener Halm von unten nach oben
        _tempPath.Rewind();
        _tempPath.MoveTo(p.X, p.Y);
        _tempPath.QuadTo(p.X + sway, p.Y - p.Size * 0.5f, p.X + sway * 0.7f, p.Y - p.Size);
        canvas.DrawPath(_tempPath, _strokePaint);
    }

    // Welt 7: Volcano - Magma-Blasen (aufsteigende Kreise die platzen)
    private void RenderMagmaBubble(SKCanvas canvas, ref AmbientParticle p, float gt, float lifeRatio)
    {
        float pop = lifeRatio < 0.1f ? lifeRatio / 0.1f : 1f; // Platzen am Ende
        byte alpha = (byte)(80 * pop * MathF.Min(lifeRatio * 3f, 1f));

        // Innerer glühender Kern
        _fillPaint.Color = new SKColor(255, 140, 30, (byte)(alpha * 0.5f));
        _fillPaint.MaskFilter = _glow;
        canvas.DrawCircle(p.X, p.Y, p.Size * 1.3f * pop, _fillPaint);

        // Äußere Blase
        _strokePaint.Color = new SKColor(255, 80, 20, alpha);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.MaskFilter = null;
        canvas.DrawCircle(p.X, p.Y, p.Size * pop, _strokePaint);
        _fillPaint.MaskFilter = null;
    }

    // Welt 8: SkyFortress - Goldene Lichtstrahlen (diagonale funkelnde Streifen)
    private void RenderGoldenRay(SKCanvas canvas, ref AmbientParticle p, float gt, float lifeRatio)
    {
        float sparkle = MathF.Sin(gt * 4f + p.Phase) * 0.4f + 0.6f;
        byte alpha = (byte)(25 * sparkle * MathF.Min(lifeRatio * 2f, 1f) * MathF.Min((1f - lifeRatio) * 2f, 1f));

        _fillPaint.Color = new SKColor(255, 220, 100, alpha);
        _fillPaint.MaskFilter = null;
        canvas.DrawCircle(p.X, p.Y, p.Size * sparkle, _fillPaint);
    }

    // Welt 9: ShadowRealm - Blinkende Augenpaare im Hintergrund
    private void RenderBlinkingEye(SKCanvas canvas, ref AmbientParticle p, float gt, float lifeRatio)
    {
        // Blinzeln: geschlossen für kurze Perioden
        float blinkCycle = (gt * 0.4f + p.Phase) % 4f;
        if (blinkCycle > 3.7f) return; // Geschlossen

        float openness = MathF.Min(blinkCycle * 2f, 1f);
        byte alpha = (byte)(40 * openness * MathF.Min(lifeRatio * 3f, 1f) * MathF.Min((1f - lifeRatio) * 3f, 1f));

        _fillPaint.Color = new SKColor(180, 50, 255, alpha);
        _fillPaint.MaskFilter = _glow;

        // Zwei Augen nebeneinander
        float eyeSpacing = p.Size * 2.5f;
        canvas.DrawOval(p.X - eyeSpacing * 0.5f, p.Y, p.Size * 0.8f, p.Size * 0.5f * openness, _fillPaint);
        canvas.DrawOval(p.X + eyeSpacing * 0.5f, p.Y, p.Size * 0.8f, p.Size * 0.5f * openness, _fillPaint);
        _fillPaint.MaskFilter = null;
    }
}
