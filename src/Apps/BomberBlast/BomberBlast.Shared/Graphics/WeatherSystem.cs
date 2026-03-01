using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Wetter-Effekte pro Welt: Partikel die ÜBER dem Grid gerendert werden (unter HUD).
/// Struct-basiert, max 80 Partikel. Leichtgewichtig für 60fps.
/// </summary>
public sealed class WeatherSystem : IDisposable
{
    private struct WeatherParticle
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Life;        // Verbleibende Lebenszeit
        public float MaxLife;
        public float Size;
        public float Rotation;    // Für rotierende Partikel (Blätter)
        public float RotSpeed;
    }

    private const int MAX_PARTICLES = 80;
    private readonly WeatherParticle[] _particles = new WeatherParticle[MAX_PARTICLES];
    private int _activeCount;

    private float _spawnTimer;
    private int _worldIndex;
    private float _screenWidth, _screenHeight;

    // Gepoolte Paint-Objekte
    private readonly SKPaint _fillPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _strokePaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };

    // Gepoolter SKPath für Tropfen-Form (statt pro-Partikel new SKPath())
    private readonly SKPath _tempPath = new();

    /// <summary>
    /// Welt-Index setzen (0-9), bestimmt welcher Wetter-Effekt aktiv ist
    /// </summary>
    public void SetWorld(int worldIndex, float screenWidth, float screenHeight)
    {
        _worldIndex = worldIndex;
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _activeCount = 0;
        _spawnTimer = 0;
    }

    /// <summary>
    /// Wetter-Partikel aktualisieren und neue spawnen
    /// </summary>
    public void Update(float deltaTime, float globalTimer)
    {
        // Spawn-Rate und -Logik pro Welt
        float spawnInterval = GetSpawnInterval();
        if (spawnInterval <= 0) return; // Kein Wetter für diese Welt

        // Bestehende Partikel aktualisieren
        for (int i = _activeCount - 1; i >= 0; i--)
        {
            ref var p = ref _particles[i];
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.Rotation += p.RotSpeed * deltaTime;
            p.Life -= deltaTime;

            // Entfernen wenn tot oder off-screen
            if (p.Life <= 0 || p.Y > _screenHeight + 20 || p.Y < -20 ||
                p.X > _screenWidth + 20 || p.X < -20)
            {
                // Swap mit letztem aktiven Partikel
                _particles[i] = _particles[_activeCount - 1];
                _activeCount--;
            }
        }

        // Neue Partikel spawnen
        _spawnTimer -= deltaTime;
        if (_spawnTimer <= 0 && _activeCount < MAX_PARTICLES)
        {
            SpawnParticle(globalTimer);
            _spawnTimer = spawnInterval;
        }
    }

    private float GetSpawnInterval() => _worldIndex switch
    {
        0 => 0.3f,   // Forest: Blätter
        1 => 0.15f,  // Industrial: Funken
        2 => 0.4f,   // Cavern: Wassertropfen
        3 => 0.8f,   // Sky: Wolkenschatten (wenige, große)
        4 => 0.12f,  // Inferno: Asche
        5 => 0.1f,   // Ruins: Sand
        6 => 0.25f,  // Ocean: Kaustik/Blasen
        7 => 0.08f,  // Volcano: Dichte Asche
        8 => 0.2f,   // SkyFortress: Goldglitzer
        9 => 0.5f,   // ShadowRealm: Dunkle Nebelfetzen
        _ => 0
    };

    private void SpawnParticle(float globalTimer)
    {
        ref var p = ref _particles[_activeCount];

        // Seeded Random basierend auf Timer + Count (deterministische Variation)
        float r1 = ProceduralTextures.CellRandom((int)(globalTimer * 100), _activeCount, _worldIndex);
        float r2 = ProceduralTextures.CellRandom((int)(globalTimer * 100), _activeCount + 100, _worldIndex);

        switch (_worldIndex)
        {
            case 0: // Forest: Fallende Blätter mit Rotation + Wind-Drift
                p.X = r1 * _screenWidth;
                p.Y = -10;
                p.VelocityX = 10f + r2 * 15f; // Wind nach rechts
                p.VelocityY = 20f + r1 * 15f;
                p.Size = 3f + r2 * 2f;
                p.Rotation = r1 * MathF.PI * 2;
                p.RotSpeed = (r2 - 0.5f) * 4f;
                p.Life = p.MaxLife = 5f;
                break;

            case 1: // Industrial: Funken von oben
                p.X = r1 * _screenWidth;
                p.Y = -5;
                p.VelocityX = (r2 - 0.5f) * 20f;
                p.VelocityY = 40f + r1 * 30f;
                p.Size = 1f + r2 * 1.5f;
                p.Rotation = 0;
                p.RotSpeed = 0;
                p.Life = p.MaxLife = 2f + r1;
                break;

            case 2: // Cavern: Wassertropfen
                p.X = r1 * _screenWidth;
                p.Y = -3;
                p.VelocityX = 0;
                p.VelocityY = 80f + r2 * 40f; // Schnell fallend
                p.Size = 1f + r2;
                p.Rotation = 0;
                p.RotSpeed = 0;
                p.Life = p.MaxLife = 3f;
                break;

            case 3: // Sky: Wandernde Wolkenschatten
                p.X = -60;
                p.Y = r1 * _screenHeight;
                p.VelocityX = 8f + r2 * 5f;
                p.VelocityY = 0;
                p.Size = 30f + r1 * 40f;
                p.Rotation = 0;
                p.RotSpeed = 0;
                p.Life = p.MaxLife = 15f;
                break;

            case 4: // Inferno: Asche-Flocken
                p.X = r1 * _screenWidth;
                p.Y = -5;
                p.VelocityX = (r2 - 0.5f) * 8f;
                p.VelocityY = 12f + r1 * 10f;
                p.Size = 1f + r2 * 1.5f;
                p.Rotation = r1 * MathF.PI * 2;
                p.RotSpeed = (r2 - 0.5f) * 2f;
                p.Life = p.MaxLife = 6f;
                break;

            case 5: // Ruins: Horizontale Sand-Partikel
                p.X = -5;
                p.Y = r1 * _screenHeight;
                p.VelocityX = 30f + r2 * 20f;
                p.VelocityY = (r1 - 0.5f) * 5f;
                p.Size = 0.5f + r2;
                p.Rotation = 0;
                p.RotSpeed = 0;
                p.Life = p.MaxLife = 4f;
                break;

            case 6: // Ocean: Kaustik-Blasen
                p.X = r1 * _screenWidth;
                p.Y = _screenHeight + 5;
                p.VelocityX = MathF.Sin(r2 * MathF.PI * 4) * 5f;
                p.VelocityY = -15f - r1 * 10f; // Aufsteigend
                p.Size = 2f + r2 * 3f;
                p.Rotation = 0;
                p.RotSpeed = 0;
                p.Life = p.MaxLife = 5f;
                break;

            case 7: // Volcano: Dichte Asche + gelegentliche Glut
                p.X = r1 * _screenWidth;
                p.Y = -5;
                p.VelocityX = (r2 - 0.5f) * 12f;
                p.VelocityY = 18f + r1 * 15f;
                p.Size = 1.5f + r2 * 2f;
                p.Rotation = r1 * MathF.PI;
                p.RotSpeed = (r2 - 0.5f) * 1.5f;
                p.Life = p.MaxLife = 4f;
                break;

            case 8: // SkyFortress: Goldener Glitzer
                p.X = r1 * _screenWidth;
                p.Y = -5;
                p.VelocityX = (r2 - 0.5f) * 6f;
                p.VelocityY = 8f + r1 * 6f;
                p.Size = 0.8f + r2 * 1.2f;
                p.Rotation = 0;
                p.RotSpeed = 0;
                p.Life = p.MaxLife = 6f;
                break;

            case 9: // ShadowRealm: Dunkle Nebelfetzen
                p.X = r1 * _screenWidth;
                p.Y = r2 * _screenHeight;
                p.VelocityX = 3f + r1 * 5f;
                p.VelocityY = (r2 - 0.5f) * 3f;
                p.Size = 20f + r1 * 30f;
                p.Rotation = 0;
                p.RotSpeed = 0;
                p.Life = p.MaxLife = 8f;
                break;
        }

        _activeCount++;
    }

    /// <summary>
    /// Wetter-Partikel rendern (ÜBER dem Grid, unter HUD)
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        if (_activeCount == 0) return;

        _fillPaint.MaskFilter = null;
        _strokePaint.MaskFilter = null;

        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];
            float lifeRatio = p.Life / p.MaxLife;

            // Fade-In am Anfang, Fade-Out am Ende
            float alpha = lifeRatio < 0.1f ? lifeRatio * 10f :
                          lifeRatio > 0.8f ? (1f - lifeRatio) * 5f : 1f;

            switch (_worldIndex)
            {
                case 0: RenderLeaf(canvas, ref p, alpha); break;
                case 1: RenderSpark(canvas, ref p, alpha); break;
                case 2: RenderWaterDrop(canvas, ref p, alpha); break;
                case 3: RenderCloudShadow(canvas, ref p, alpha); break;
                case 4: RenderAsh(canvas, ref p, alpha); break;
                case 5: RenderSandParticle(canvas, ref p, alpha); break;
                case 6: RenderBubble(canvas, ref p, alpha); break;
                case 7: RenderVolcanicAsh(canvas, ref p, alpha); break;
                case 8: RenderGlitter(canvas, ref p, alpha); break;
                case 9: RenderDarkMist(canvas, ref p, alpha); break;
            }
        }
    }

    private void RenderLeaf(SKCanvas canvas, ref WeatherParticle p, float alpha)
    {
        byte a = (byte)(100 * alpha);
        // Grünes Blatt (Ellipse, rotiert)
        canvas.Save();
        canvas.Translate(p.X, p.Y);
        canvas.RotateRadians(p.Rotation);
        _fillPaint.Color = new SKColor(60, 120, 30, a);
        canvas.DrawOval(0, 0, p.Size, p.Size * 0.5f, _fillPaint);
        canvas.Restore();
    }

    private void RenderSpark(SKCanvas canvas, ref WeatherParticle p, float alpha)
    {
        byte a = (byte)(180 * alpha);
        _fillPaint.Color = new SKColor(255, 160, 30, a);
        canvas.DrawCircle(p.X, p.Y, p.Size * 0.5f, _fillPaint);

        // Leucht-Schweif
        byte glowA = (byte)(60 * alpha);
        _fillPaint.Color = new SKColor(255, 100, 0, glowA);
        canvas.DrawCircle(p.X, p.Y - p.Size, p.Size * 0.3f, _fillPaint);
    }

    private void RenderWaterDrop(SKCanvas canvas, ref WeatherParticle p, float alpha)
    {
        byte a = (byte)(120 * alpha);
        _fillPaint.Color = new SKColor(140, 200, 255, a);

        // Tropfenform (Kreis + kleines Dreieck oben)
        canvas.DrawCircle(p.X, p.Y, p.Size, _fillPaint);
        _tempPath.Rewind();
        _tempPath.MoveTo(p.X - p.Size * 0.5f, p.Y);
        _tempPath.LineTo(p.X, p.Y - p.Size * 2f);
        _tempPath.LineTo(p.X + p.Size * 0.5f, p.Y);
        _tempPath.Close();
        canvas.DrawPath(_tempPath, _fillPaint);
    }

    private void RenderCloudShadow(SKCanvas canvas, ref WeatherParticle p, float alpha)
    {
        byte a = (byte)(20 * alpha);
        _fillPaint.Color = new SKColor(0, 0, 0, a);
        canvas.DrawOval(p.X, p.Y, p.Size, p.Size * 0.4f, _fillPaint);
    }

    private void RenderAsh(SKCanvas canvas, ref WeatherParticle p, float alpha)
    {
        byte a = (byte)(70 * alpha);
        _fillPaint.Color = new SKColor(100, 90, 80, a);
        canvas.Save();
        canvas.Translate(p.X, p.Y);
        canvas.RotateRadians(p.Rotation);
        canvas.DrawRect(-p.Size * 0.5f, -p.Size * 0.3f, p.Size, p.Size * 0.6f, _fillPaint);
        canvas.Restore();
    }

    private void RenderSandParticle(SKCanvas canvas, ref WeatherParticle p, float alpha)
    {
        byte a = (byte)(50 * alpha);
        _fillPaint.Color = new SKColor(200, 180, 140, a);
        canvas.DrawCircle(p.X, p.Y, p.Size, _fillPaint);
    }

    private void RenderBubble(SKCanvas canvas, ref WeatherParticle p, float alpha)
    {
        byte a = (byte)(40 * alpha);
        _strokePaint.Color = new SKColor(150, 220, 255, a);
        _strokePaint.StrokeWidth = 0.8f;
        canvas.DrawCircle(p.X, p.Y, p.Size, _strokePaint);

        // Glanzpunkt
        byte ga = (byte)(60 * alpha);
        _fillPaint.Color = new SKColor(220, 240, 255, ga);
        canvas.DrawCircle(p.X - p.Size * 0.3f, p.Y - p.Size * 0.3f, p.Size * 0.25f, _fillPaint);
    }

    private void RenderVolcanicAsh(SKCanvas canvas, ref WeatherParticle p, float alpha)
    {
        // Mischung aus dunkler Asche und gelegentlicher Glut
        float lifeRatio = p.Life / p.MaxLife;
        bool isEmber = p.Size > 3f; // Größere Partikel = Glut

        if (isEmber)
        {
            byte a = (byte)(120 * alpha);
            _fillPaint.Color = new SKColor(255, 100, 20, a);
            canvas.DrawCircle(p.X, p.Y, p.Size * 0.4f, _fillPaint);
        }
        else
        {
            byte a = (byte)(60 * alpha);
            _fillPaint.Color = new SKColor(60, 50, 40, a);
            canvas.Save();
            canvas.Translate(p.X, p.Y);
            canvas.RotateRadians(p.Rotation);
            canvas.DrawRect(-p.Size * 0.5f, -p.Size * 0.3f, p.Size, p.Size * 0.6f, _fillPaint);
            canvas.Restore();
        }
    }

    private void RenderGlitter(SKCanvas canvas, ref WeatherParticle p, float alpha)
    {
        // Funkelnder Punkt (Intensität variiert mit Life)
        float sparkle = MathF.Sin(p.Life * 15f) * 0.5f + 0.5f;
        byte a = (byte)(150 * alpha * sparkle);
        if (a < 10) return;

        _fillPaint.Color = new SKColor(255, 220, 100, a);
        canvas.DrawCircle(p.X, p.Y, p.Size * sparkle, _fillPaint);
    }

    private void RenderDarkMist(SKCanvas canvas, ref WeatherParticle p, float alpha)
    {
        byte a = (byte)(25 * alpha);
        _fillPaint.Color = new SKColor(20, 10, 35, a);
        canvas.DrawOval(p.X, p.Y, p.Size, p.Size * 0.5f, _fillPaint);
    }

    public void Dispose()
    {
        _tempPath.Dispose();
        _fillPaint.Dispose();
        _strokePaint.Dispose();
    }
}
