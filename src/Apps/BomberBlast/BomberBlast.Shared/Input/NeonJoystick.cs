using BomberBlast.Models.Entities;
using SkiaSharp;

namespace BomberBlast.Input;

/// <summary>
/// Custom Neon-Arcade-Joystick im BomberBlast-Stil.
///
/// Design-Sprache (abgestimmt auf Themes/AppPalette.axaml + GameIcon-System):
/// - Oktagonale Basis statt Kreis (wie die Icons der App)
/// - Primary-Orange #FF6B35 als Haupt-Glow, Cyan #22D3EE als Akzent, Gold #FFDD33 für Trail
/// - 4 Richtungs-Pfeile die bei aktiver Bewegungsrichtung aufleuchten
/// - Trail hinter dem Stick bei Bewegung
/// - Idle-Pulsation im Fixed-Modus, Launch-Flash bei Touch-Start
/// - Bomb-Button mit Rot-Orange-Glow, pulsierender Cyan-Funke auf der Lunte
/// - Detonator-Button mit Cyan-Glow und stilisiertem Blitz
///
/// Mechanik (kompatibel zum vorherigen FloatingJoystick):
/// - 4-Wege-Movement (Grid-basiert wie BomberBlast)
/// - Dominante Achse + Hysterese (1.15x) gegen Richtungsflackern bei ~45°
/// - Deadzone: Fixed 15%, Floating 5%
/// - Following-Base wenn Finger ueber den Radius hinausgeht
/// - Multi-Touch Pointer-ID Tracking (Joystick + Bomb/Detonator gleichzeitig)
/// - Konsumptions-Pattern fuer Bomb/Detonator (1 Press = 1 Event)
///
/// Performance:
/// - Alle SKPaints gepoolt, SKPath via Rewind() wiederverwendet
/// - SKMaskFilter statisch gecacht (Soft/Medium/Hard) - teuer zu erstellen
/// - Trail als Struct-Array (kein GC)
/// </summary>
public class NeonJoystick : IInputHandler, IDisposable
{
    public string Name => "Joystick";

    // ═══════════════════════════════════════════════════════════════════════
    // FARBPALETTE (aus Themes/AppPalette.axaml)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKColor PrimaryOrange = new(0xFF, 0x6B, 0x35);   // #FF6B35
    private static readonly SKColor PrimaryBright = new(0xFF, 0xA8, 0x70);   // Hellere Variante für Active
    private static readonly SKColor AccentCyan    = new(0x22, 0xD3, 0xEE);   // #22D3EE
    private static readonly SKColor CombatRed     = new(0xEF, 0x44, 0x44);   // #EF4444
    private static readonly SKColor SecondaryGold = new(0xFF, 0xDD, 0x33);   // #FFDD33
    private static readonly SKColor SurfacePurple = new(0x1C, 0x1C, 0x35);   // #1C1C35
    private static readonly SKColor DeepDark      = new(0x0A, 0x0A, 0x14);   // Tiefschwarz
    private static readonly SKColor White         = new(0xFF, 0xFF, 0xFF);
    private static readonly SKColor BombBodyIdle  = new(0x44, 0x12, 0x12);
    private static readonly SKColor BombBodyDown  = new(0x7F, 0x1D, 0x1D);
    private static readonly SKColor DetoBodyIdle  = new(0x0A, 0x24, 0x28);
    private static readonly SKColor DetoBodyDown  = new(0x14, 0x4D, 0x58);
    private static readonly SKColor BombHighlight = new(0x55, 0x55, 0x66);

    // Richtungs-Pfeil-Definitionen (ndx, ndy, zugehoerige Direction, Rotation in Grad)
    private static readonly (float ndx, float ndy, Direction dir, float rot)[] ArrowDefs =
    {
        ( 0f, -1f, Direction.Up,    0f),
        ( 1f,  0f, Direction.Right, 90f),
        ( 0f,  1f, Direction.Down,  180f),
        (-1f,  0f, Direction.Left,  270f),
    };

    // ═══════════════════════════════════════════════════════════════════════
    // STATE
    // ═══════════════════════════════════════════════════════════════════════

    // Joystick-Position
    private bool _isPressed;
    private float _baseX, _baseY;
    private float _stickX, _stickY;            // Logische Position (Finger, geclampt) - fuer Richtungs-Logik
    private float _stickDrawX, _stickDrawY;    // Gerenderte Position (smoothed) - vermeidet sichtbare Spruenge zwischen Touch-Events

    // Touch-Events kommen auf Android mit variabler Rate (~30-120Hz), Rendering laeuft bei 30fps.
    // Ohne Smoothing springt der gezeichnete Stick sichtbar zwischen den Sample-Positionen.
    // rate=25 fuehlt sich fluessig an ohne traege zu wirken (bei 30fps erreicht der Draw-Punkt nach
    // ~1 Frame ca. 57% der Distanz, nach 2 Frames ~81%).
    private const float STICK_SMOOTHING_RATE = 25f;

    // Bomb-Button
    private bool _bombPressed;
    private bool _bombConsumed;
    private bool _bombButtonPressed;
    private float _bombButtonX, _bombButtonY;

    // Detonator-Button
    private bool _detonatePressed;
    private bool _detonateConsumed;
    private bool _detonatorButtonPressed;
    private float _detonatorButtonX, _detonatorButtonY;

    // Multi-Touch-Pointer-Tracking (separate IDs fuer Bomb und Detonator,
    // damit gleichzeitiges Halten nicht die Tracking-IDs ueberschreibt)
    private long _joystickPointerId = -1;
    private long _bombButtonPointerId = -1;
    private long _detonatorPointerId = -1;

    // Konfiguration
    private float _joystickRadius = 75f;
    private float _bombButtonRadius = 52f;
    private float _detonatorButtonRadius = 48f;
    private float _opacity = 0.85f;
    private bool _isFixed;

    private const float DEAD_ZONE_FIXED = 0.15f;
    private const float DEAD_ZONE_FLOATING = 0.05f;
    private const float DIRECTION_HYSTERESIS = 1.15f;

    private Direction _currentDirection = Direction.None;

    // Animation
    private float _animTime;           // Läuft kontinuierlich (fuer idle-Puls + Funken-Pulsation)
    private float _activeGlow;         // 0-1, eased zu IsPressed
    private float _bombGlow;           // 0-1, eased zu BombButtonPressed
    private float _detonatorGlow;      // 0-1, eased zu DetonatorButtonPressed
    private float _launchFlash;        // 1 bei Touch-Start, faded auf 0

    // Trail (Ringpuffer, Struct-Pool)
    private struct TrailPoint { public float X; public float Y; public float Age; }
    private readonly TrailPoint[] _trail = CreateInitialTrail();
    private int _trailHead;
    private float _trailTimer;

    // Trail mit Age=999f initialisieren, damit im ersten Frame keine Geister-Dots bei (0,0) sichtbar sind.
    // Audit L10: 12 Punkte statt 8 → flüssigerer visueller Schweif (5-10% mehr Render-Aufwand, vernachlaessigbar).
    private const int TRAIL_POINTS = 12;
    private static TrailPoint[] CreateInitialTrail()
    {
        var t = new TrailPoint[TRAIL_POINTS];
        for (int i = 0; i < t.Length; i++)
            t[i].Age = 999f;
        return t;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GEPOOLTE SKPAINT + SKPATH (alle allokationsfrei pro Frame)
    // ═══════════════════════════════════════════════════════════════════════

    // Joystick-Paints
    private readonly SKPaint _glowPaint         = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _bodyPaint         = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _outlinePaint      = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, IsAntialias = true };
    private readonly SKPaint _innerRingPaint    = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
    private readonly SKPaint _arrowPaint        = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _stickBodyPaint    = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _stickCorePaint    = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _stickOutlinePaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
    private readonly SKPaint _trailPaint        = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };

    // Bomb-Button Paints
    private readonly SKPaint _bombGlowPaint    = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _bombBodyPaint    = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _bombOutlinePaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, IsAntialias = true };
    private readonly SKPaint _bombIconPaint    = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _fusePaint        = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
    private readonly SKPaint _sparkPaint       = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };

    // Detonator-Button Paints
    private readonly SKPaint _detoGlowPaint    = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _detoBodyPaint    = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _detoOutlinePaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, IsAntialias = true };
    private readonly SKPaint _detoIconPaint    = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };

    // Paths (via Rewind() wiederverwendet)
    private readonly SKPath _octagonPath      = new();
    private readonly SKPath _stickOctagonPath = new();
    private readonly SKPath _arrowPath        = new();
    private readonly SKPath _fusePath         = new();
    private readonly SKPath _boltPath         = new();

    // Statisch gecachte MaskFilter (teuer zu erstellen - einmalige Instanzen fuer gesamte App-Lifetime)
    private static readonly SKMaskFilter SoftGlow   = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12f);
    private static readonly SKMaskFilter MediumGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);
    private static readonly SKMaskFilter HardGlow   = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC API (kompatibel zur vorherigen FloatingJoystick-Klasse)
    // ═══════════════════════════════════════════════════════════════════════

    public Direction MovementDirection => _currentDirection;
    public bool BombPressed => _bombPressed && !_bombConsumed;
    public bool DetonatePressed => _detonatePressed && !_detonateConsumed;
    public bool IsActive => _isPressed;

    /// <summary>Event bei Richtungswechsel (für haptisches Feedback)</summary>
    public event Action? DirectionChanged;

    /// <summary>Ob der Detonator-Button angezeigt wird</summary>
    public bool HasDetonator { get; set; }

    /// <summary>Audit L11: Wenn true werden visuelle Pulsations deaktiviert (Accessibility / Mid-Tier-Perf).</summary>
    public bool ReducedEffects { get; set; }

    /// <summary>Fixed-Modus: Joystick immer sichtbar an fester Position unten links</summary>
    public bool IsFixed
    {
        get => _isFixed;
        set => _isFixed = value;
    }

    public float JoystickSize
    {
        get => _joystickRadius * 2;
        set => _joystickRadius = value / 2;
    }

    public float Opacity
    {
        get => _opacity;
        set => _opacity = Math.Clamp(value, 0.1f, 1f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TOUCH-EVENTS (IInputHandler)
    // ═══════════════════════════════════════════════════════════════════════

    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight, long pointerId = 0)
    {
        UpdateBombButtonPosition(screenWidth, screenHeight);

        // Detonator-Button prüfen (ueber dem Bomb-Button)
        if (HasDetonator)
        {
            float ddx = x - _detonatorButtonX;
            float ddy = y - _detonatorButtonY;
            if (ddx * ddx + ddy * ddy <= _detonatorButtonRadius * _detonatorButtonRadius * 1.6f)
            {
                _detonatorButtonPressed = true;
                _detonatePressed = true;
                _detonateConsumed = false;
                _detonatorPointerId = pointerId;
                return;
            }
        }

        // Bomb-Button prüfen (rechte Seite)
        // v2.0.60 (B-E11): Pointer-ID-Guard. Wenn schon ein Pointer auf dem Bomb-Button ist,
        // ignoriere zweite Finger-Hits — verhindert ID-Überschreibung bei eng nebeneinander
        // liegenden gleichzeitigen Touches, die sonst Bomb-Hang verursachen können.
        if (_bombButtonPointerId == -1)
        {
            float dx = x - _bombButtonX;
            float dy = y - _bombButtonY;
            if (dx * dx + dy * dy <= _bombButtonRadius * _bombButtonRadius * 1.6f)
            {
                _bombButtonPressed = true;
                _bombPressed = true;
                _bombConsumed = false;
                _bombButtonPointerId = pointerId;
                return;
            }
        }

        if (_isFixed)
        {
            // Fixed-Modus: Nur auf Joystick-Bereich reagieren (1.8x Radius als Touch-Zone)
            UpdateFixedPosition(screenWidth, screenHeight);
            float jdx = x - _baseX;
            float jdy = y - _baseY;
            float touchZone = _joystickRadius * 1.8f;
            if (jdx * jdx + jdy * jdy <= touchZone * touchZone)
            {
                _isPressed = true;
                _joystickPointerId = pointerId;
                _stickX = x;
                _stickY = y;
                ClampAndFollow();
                SnapStickDraw(); // Anfangs-Position sofort uebernehmen, kein Nachziehen vom letzten Release
                UpdateDirection();
                _launchFlash = 1f;
            }
        }
        else
        {
            // Floating-Modus: Linke 60% - Joystick erscheint wo getippt wird
            if (x < screenWidth * 0.6f)
            {
                _isPressed = true;
                _joystickPointerId = pointerId;
                _baseX = x;
                _baseY = y;
                _stickX = x;
                _stickY = y;
                SnapStickDraw();
                _currentDirection = Direction.None;
                _launchFlash = 1f;
            }
        }
    }

    public void OnTouchMove(float x, float y, long pointerId = 0)
    {
        if (!_isPressed || pointerId != _joystickPointerId)
            return;

        _stickX = x;
        _stickY = y;
        ClampAndFollow();
        UpdateDirection();
        // _stickDrawX/Y werden in Update(deltaTime) per Frame zur Ziel-Position geglaettet
    }

    public void OnTouchEnd(long pointerId = 0)
    {
        // Joystick-Finger losgelassen
        if (pointerId == _joystickPointerId)
        {
            _isPressed = false;
            _stickX = _baseX;
            _stickY = _baseY;
            SnapStickDraw(); // Kein weiches Zurueck-Gleiten beim Loslassen, sonst wirkt der Joystick "schlaff"
            _currentDirection = Direction.None;
            _joystickPointerId = -1;
        }

        // Bomb-Finger losgelassen (separates Tracking!)
        if (pointerId == _bombButtonPointerId)
        {
            _bombButtonPressed = false;
            _bombButtonPointerId = -1;

            // Race-Guard: Tap <16ms (Press+Release zwischen zwei Update-Frames)
            // wuerde _bombPressed haengenlassen. Nach Konsum sofort ausloeschen.
            if (_bombConsumed)
                _bombPressed = false;
        }

        // Detonator-Finger losgelassen (separates Tracking)
        if (pointerId == _detonatorPointerId)
        {
            _detonatorButtonPressed = false;
            _detonatorPointerId = -1;

            if (_detonateConsumed)
                _detonatePressed = false;
        }
    }

    public void Update(float deltaTime)
    {
        _animTime += deltaTime;

        // Stick-Draw-Position zur Ziel-Position smoothen.
        // Exponentielles Ease-Out: alpha = 1 - exp(-rate * dt) -- framerate-unabhaengig, kein Overshoot.
        if (deltaTime > 0f)
        {
            float alpha = 1f - MathF.Exp(-STICK_SMOOTHING_RATE * deltaTime);
            _stickDrawX += (_stickX - _stickDrawX) * alpha;
            _stickDrawY += (_stickY - _stickDrawY) * alpha;
        }

        // Glow-Werte smooth lerpen
        float activeTarget = _isPressed ? 1f : 0f;
        _activeGlow     += (activeTarget - _activeGlow) * Math.Min(1f, deltaTime * 12f);
        _bombGlow       += ((_bombButtonPressed ? 1f : 0f) - _bombGlow) * Math.Min(1f, deltaTime * 14f);
        _detonatorGlow  += ((_detonatorButtonPressed ? 1f : 0f) - _detonatorGlow) * Math.Min(1f, deltaTime * 14f);

        // Launch-Flash schnell ausklingen (in ~0.33s)
        _launchFlash = MathF.Max(0f, _launchFlash - deltaTime * 3f);

        // Trail-Punkte alle 20ms neu setzen (nur bei aktivem Stick)
        if (_isPressed)
        {
            _trailTimer += deltaTime;
            if (_trailTimer >= 0.02f)
            {
                _trailTimer = 0f;
                _trail[_trailHead] = new TrailPoint { X = _stickX, Y = _stickY, Age = 0f };
                _trailHead = (_trailHead + 1) % _trail.Length;
            }
        }

        // Trail-Alter erhöhen (unabhängig vom Timer)
        for (int i = 0; i < _trail.Length; i++)
        {
            if (_trail[i].Age < 999f)
                _trail[i].Age = Math.Min(999f, _trail[i].Age + deltaTime);
        }

        // Konsumptions-Pattern: Bomb/Detonate-Press nach einem Frame ausloeschen
        if (_bombConsumed) _bombPressed = false;
        if (_bombPressed) _bombConsumed = true;

        if (_detonateConsumed) _detonatePressed = false;
        if (_detonatePressed) _detonateConsumed = true;
    }

    public void Reset()
    {
        _isPressed = false;
        _currentDirection = Direction.None;
        _stickX = _baseX;
        _stickY = _baseY;
        SnapStickDraw();
        _bombPressed = false;
        _bombConsumed = false;
        _bombButtonPressed = false;
        _detonatePressed = false;
        _detonateConsumed = false;
        _detonatorButtonPressed = false;
        _joystickPointerId = -1;
        _bombButtonPointerId = -1;
        _detonatorPointerId = -1;
        _activeGlow = 0f;
        _bombGlow = 0f;
        _detonatorGlow = 0f;
        _launchFlash = 0f;
        for (int i = 0; i < _trail.Length; i++)
            _trail[i].Age = 999f;
    }

    /// <summary>
    /// Draw-Position sofort auf Ziel-Position setzen (kein Smoothing).
    /// Wird bei Touch-Start und Touch-End/Reset verwendet, damit der Stick nicht aus der
    /// vorherigen Position heraus nachzieht bzw. nach Release nicht "schlaff" zurueckgleitet.
    /// </summary>
    private void SnapStickDraw()
    {
        _stickDrawX = _stickX;
        _stickDrawY = _stickY;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LAYOUT & BEWEGUNGS-LOGIK
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateFixedPosition(float screenWidth, float screenHeight)
    {
        // Audit H03: Safe-Area-Puffer 32dp gegen Gesture-Bar / Bottom-Cutout (Pixel 4+/S10+).
        // 30dp Left fuer Notch-Buffer (Camera-Cutout im Landscape rechts oder links).
        _baseX = 30 + _joystickRadius;
        _baseY = screenHeight - 32 - _joystickRadius;
        if (!_isPressed)
        {
            _stickX = _baseX;
            _stickY = _baseY;
            SnapStickDraw(); // Layout-Position hat sich evtl. geaendert (Rotation) -> sofort uebernehmen
        }
    }

    private void UpdateBombButtonPosition(float screenWidth, float screenHeight)
    {
        // Audit H03: Bomb-Button 32dp Bottom-Safe-Area + 80dp Right-Cutout-Buffer.
        _bombButtonX = screenWidth - _bombButtonRadius - 80;
        _bombButtonY = screenHeight - _bombButtonRadius - 32;
        _detonatorButtonX = _bombButtonX;
        _detonatorButtonY = _bombButtonY - _bombButtonRadius - _detonatorButtonRadius - 15;
    }

    /// <summary>
    /// Stick auf Radius begrenzen + Following-Base:
    /// Wenn der Finger ueber den Radius hinausgeht, folgt die Basis dem Finger.
    /// </summary>
    private void ClampAndFollow()
    {
        float dx = _stickX - _baseX;
        float dy = _stickY - _baseY;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance > _joystickRadius)
        {
            float excess = distance - _joystickRadius;
            float nx = dx / distance;
            float ny = dy / distance;
            _baseX += nx * excess;
            _baseY += ny * excess;
            _stickX = _baseX + nx * _joystickRadius;
            _stickY = _baseY + ny * _joystickRadius;
        }
    }

    /// <summary>
    /// Richtung per dominanter Achse mit Hysterese bestimmen (4-Wege fuer Grid-Movement).
    /// </summary>
    private void UpdateDirection()
    {
        float dx = _stickX - _baseX;
        float dy = _stickY - _baseY;
        float distSq = dx * dx + dy * dy;
        float deadZonePx = _joystickRadius * (_isFixed ? DEAD_ZONE_FIXED : DEAD_ZONE_FLOATING);

        if (distSq < deadZonePx * deadZonePx)
        {
            if (_currentDirection != Direction.None)
            {
                _currentDirection = Direction.None;
                DirectionChanged?.Invoke();
            }
            return;
        }

        float absDx = MathF.Abs(dx);
        float absDy = MathF.Abs(dy);

        Direction newDir;
        bool isCurrHoriz = _currentDirection is Direction.Left or Direction.Right;
        bool isCurrVert  = _currentDirection is Direction.Up or Direction.Down;

        if (isCurrHoriz)
            newDir = absDy > absDx * DIRECTION_HYSTERESIS
                ? (dy > 0 ? Direction.Down : Direction.Up)
                : (dx > 0 ? Direction.Right : Direction.Left);
        else if (isCurrVert)
            newDir = absDx > absDy * DIRECTION_HYSTERESIS
                ? (dx > 0 ? Direction.Right : Direction.Left)
                : (dy > 0 ? Direction.Down : Direction.Up);
        else
            newDir = absDx > absDy
                ? (dx > 0 ? Direction.Right : Direction.Left)
                : (dy > 0 ? Direction.Down : Direction.Up);

        if (newDir != _currentDirection)
        {
            _currentDirection = newDir;
            DirectionChanged?.Invoke();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDERING (7-Layer-Joystick + Bomb + Detonator)
    // ═══════════════════════════════════════════════════════════════════════

    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        UpdateBombButtonPosition(screenWidth, screenHeight);
        byte alpha = (byte)(_opacity * 255);

        // Joystick rendern (Fixed: immer, Floating: nur bei Input oder Launch-Fade)
        if (_isFixed)
        {
            UpdateFixedPosition(screenWidth, screenHeight);
            RenderJoystick(canvas, alpha, fixedIdlePresence: 0.55f);
        }
        else if (_isPressed || _launchFlash > 0.01f)
        {
            RenderJoystick(canvas, alpha, fixedIdlePresence: 1f);
        }

        // Bomb + Detonator immer sichtbar
        RenderBombButton(canvas, alpha);
        if (HasDetonator)
            RenderDetonatorButton(canvas, alpha);
    }

    private void RenderJoystick(SKCanvas canvas, byte alpha, float fixedIdlePresence)
    {
        // Idle-Pulsation (nur Fixed-Mode visuell relevant).
        // Audit L11: ReducedEffects-Flag deaktiviert Pulse (Accessibility/Performance).
        float idlePulse = ReducedEffects
            ? 1f
            : 0.85f + MathF.Sin(_animTime * 2f) * 0.15f;

        // Presence: 1 im Floating-Mode, zwischen idle und activeGlow im Fixed
        float presence = _isFixed
            ? Math.Max(_activeGlow, fixedIdlePresence)
            : 1f;

        byte baseAlpha = (byte)(alpha * presence);
        byte glowAlpha = (byte)(alpha * (0.35f + _activeGlow * 0.45f) * (presence * presence));
        byte ringAlpha = (byte)(alpha * (0.55f + _activeGlow * 0.45f) * presence);

        // ─── LAYER 0: Launch-Flash (kurzer goldener Expansions-Puls bei Touch-Start) ──
        if (_launchFlash > 0.01f)
        {
            float flashR = _joystickRadius * (1.05f + _launchFlash * 0.35f);
            _glowPaint.MaskFilter = SoftGlow;
            _glowPaint.Color = SecondaryGold.WithAlpha((byte)(alpha * _launchFlash * 0.55f));
            BuildOctagon(_octagonPath, _baseX, _baseY, flashR);
            canvas.DrawPath(_octagonPath, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // ─── LAYER 1: Äusserer Orange-Glow-Halo (weicher Blur) ─────────────────────
        _glowPaint.MaskFilter = SoftGlow;
        _glowPaint.Color = PrimaryOrange.WithAlpha(glowAlpha);
        BuildOctagon(_octagonPath, _baseX, _baseY, _joystickRadius * 1.12f);
        canvas.DrawPath(_octagonPath, _glowPaint);
        _glowPaint.MaskFilter = null;

        // ─── LAYER 2: Basis-Oktagon (dunkles Surface-Purple) ───────────────────────
        BuildOctagon(_octagonPath, _baseX, _baseY, _joystickRadius);
        _bodyPaint.Color = SurfacePurple.WithAlpha((byte)(baseAlpha * 0.85f));
        canvas.DrawPath(_octagonPath, _bodyPaint);

        // ─── LAYER 3: Oktagon-Outline (Orange, pulsiert subtil im Idle) ────────────
        byte outlineA = (byte)(ringAlpha * idlePulse);
        SKColor outlineCol = LerpColor(PrimaryOrange, PrimaryBright, _activeGlow);
        _outlinePaint.Color = outlineCol.WithAlpha(outlineA);
        canvas.DrawPath(_octagonPath, _outlinePaint);

        // ─── LAYER 4: Inneres Dead-Zone-Oktagon (Cyan, subtil) ─────────────────────
        float deadR = Math.Max(
            _joystickRadius * (_isFixed ? DEAD_ZONE_FIXED : DEAD_ZONE_FLOATING) + 6f,
            _joystickRadius * 0.22f);
        BuildOctagon(_octagonPath, _baseX, _baseY, deadR);
        _innerRingPaint.Color = AccentCyan.WithAlpha((byte)(baseAlpha * 0.28f));
        canvas.DrawPath(_octagonPath, _innerRingPaint);

        // ─── LAYER 5: 4 Richtungs-Pfeile (aktive Richtung leuchtet hell) ───────────
        RenderDirectionalArrows(canvas, baseAlpha);

        // ─── LAYER 6: Trail (goldene fading Dots hinter dem Stick) ─────────────────
        RenderTrail(canvas, alpha);

        // ─── LAYER 7: Stick (Oktagon mit Glow-Core) ────────────────────────────────
        RenderStick(canvas, alpha);
    }

    private void RenderDirectionalArrows(SKCanvas canvas, byte baseAlpha)
    {
        float arrowDist = _joystickRadius * 0.78f;
        float arrowSize = _joystickRadius * 0.16f;

        foreach (var (ndx, ndy, dir, rot) in ArrowDefs)
        {
            float px = _baseX + ndx * arrowDist;
            float py = _baseY + ndy * arrowDist;
            bool isActive = _currentDirection == dir;
            byte a = (byte)(baseAlpha * (isActive ? 1f : 0.32f));
            SKColor col = isActive ? PrimaryBright : PrimaryOrange;

            // Path einmal bauen, bei Bedarf zweimal zeichnen (Glow + Fill)
            BuildArrow(_arrowPath, px, py, arrowSize, rot);

            if (isActive)
            {
                _arrowPaint.MaskFilter = MediumGlow;
                _arrowPaint.Color = PrimaryBright.WithAlpha((byte)(baseAlpha * 0.75f));
                canvas.DrawPath(_arrowPath, _arrowPaint);
                _arrowPaint.MaskFilter = null;
            }

            _arrowPaint.Color = col.WithAlpha(a);
            canvas.DrawPath(_arrowPath, _arrowPaint);
        }
    }

    private void RenderTrail(SKCanvas canvas, byte alpha)
    {
        const float LIFETIME = 0.28f;
        for (int i = 0; i < _trail.Length; i++)
        {
            var t = _trail[i];
            if (t.Age >= LIFETIME) continue;
            float k = 1f - (t.Age / LIFETIME);
            byte a = (byte)(alpha * k * 0.55f);
            float size = _joystickRadius * 0.14f * k;
            _trailPaint.Color = SecondaryGold.WithAlpha(a);
            canvas.DrawCircle(t.X, t.Y, size, _trailPaint);
        }
    }

    private void RenderStick(SKCanvas canvas, byte alpha)
    {
        float sr = _joystickRadius * 0.38f;

        // Stick-Farbe: lerpt zwischen PrimaryOrange (idle) und PrimaryBright (active)
        SKColor stickBodyCol = LerpColor(PrimaryOrange, PrimaryBright, _activeGlow);

        // Outer Glow -- nutzt smoothed Draw-Position
        _stickCorePaint.MaskFilter = MediumGlow;
        _stickCorePaint.Color = stickBodyCol.WithAlpha((byte)(alpha * 0.65f));
        BuildOctagon(_stickOctagonPath, _stickDrawX, _stickDrawY, sr * 1.15f);
        canvas.DrawPath(_stickOctagonPath, _stickCorePaint);
        _stickCorePaint.MaskFilter = null;

        // Body-Oktagon
        BuildOctagon(_stickOctagonPath, _stickDrawX, _stickDrawY, sr);
        _stickBodyPaint.Color = stickBodyCol.WithAlpha(alpha);
        canvas.DrawPath(_stickOctagonPath, _stickBodyPaint);

        // Outline (weiss)
        _stickOutlinePaint.Color = White.WithAlpha((byte)(alpha * 0.9f));
        canvas.DrawPath(_stickOctagonPath, _stickOutlinePaint);

        // Zentraler weisser Core-Dot
        _stickCorePaint.Color = White.WithAlpha(alpha);
        canvas.DrawCircle(_stickDrawX, _stickDrawY, sr * 0.32f, _stickCorePaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOMB-BUTTON (Neon-Oktagon, Rot-Orange-Glow, pulsierende Cyan-Funke)
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderBombButton(SKCanvas canvas, byte alpha)
    {
        float r = _bombButtonRadius;
        float cx = _bombButtonX;
        float cy = _bombButtonY;

        // Ruhiger Breath-Effekt: 1.5 Hz (vorher 2.5) und nur ±5% (vorher ±10%).
        // Reduziert sichtbares Pulsieren auf ein dezentes "Atmen".
        float idle = 0.95f + MathF.Sin(_animTime * 1.5f) * 0.05f;
        byte ringA = (byte)(alpha * (0.75f + _bombGlow * 0.25f) * idle);

        // Layer 1: Rot-Glow-Aura — JEDEN Frame mit halbem Alpha zeichnen statt alle
        // 2 Frames An/Aus. Gleiche GPU-Kosten, kein 15-Hz-Flackern mehr.
        // Bei Press volle Intensität für Feedback-Flash.
        byte glowA = (byte)(alpha * (0.22f + _bombGlow * 0.4f) * idle);
        _bombGlowPaint.MaskFilter = SoftGlow;
        _bombGlowPaint.Color = CombatRed.WithAlpha(glowA);
        BuildOctagon(_octagonPath, cx, cy, r * 1.15f);
        canvas.DrawPath(_octagonPath, _bombGlowPaint);
        _bombGlowPaint.MaskFilter = null;

        // Layer 2: Dunkles Body-Oktagon
        BuildOctagon(_octagonPath, cx, cy, r);
        SKColor bodyCol = _bombButtonPressed ? BombBodyDown : BombBodyIdle;
        _bombBodyPaint.Color = bodyCol.WithAlpha(alpha);
        canvas.DrawPath(_octagonPath, _bombBodyPaint);

        // Layer 3: Outline (wechselt zwischen Orange und Bright-Orange bei Press)
        SKColor outlineCol = LerpColor(PrimaryOrange, PrimaryBright, _bombGlow);
        _bombOutlinePaint.Color = outlineCol.WithAlpha(ringA);
        canvas.DrawPath(_octagonPath, _bombOutlinePaint);

        // Layer 4: Bombe (Kugel + Highlight)
        float bs = r * 0.42f;
        float by = cy + bs * 0.1f;

        // Bomben-Glow-Aura (macht die Bombe weicher)
        _bombIconPaint.MaskFilter = HardGlow;
        _bombIconPaint.Color = DeepDark.WithAlpha((byte)(alpha * 0.6f));
        canvas.DrawCircle(cx, by, bs * 1.05f, _bombIconPaint);
        _bombIconPaint.MaskFilter = null;

        // Bombe (fast schwarz)
        _bombIconPaint.Color = DeepDark.WithAlpha(alpha);
        canvas.DrawCircle(cx, by, bs, _bombIconPaint);

        // Highlight oben-links auf der Bombe
        _bombIconPaint.Color = BombHighlight.WithAlpha((byte)(alpha * 0.55f));
        canvas.DrawCircle(cx - bs * 0.3f, by - bs * 0.3f, bs * 0.3f, _bombIconPaint);

        // Lunte (geschwungen)
        _fusePaint.Color = SecondaryGold.WithAlpha(alpha);
        _fusePath.Rewind();
        _fusePath.MoveTo(cx, by - bs);
        _fusePath.QuadTo(cx + bs * 0.3f, by - bs - 10f, cx + bs * 0.55f, by - bs - 4f);
        canvas.DrawPath(_fusePath, _fusePaint);

        // Cyan-Funke am Lunten-Ende (gedämpftes Pulsieren: 6 Hz statt 14 Hz,
        // ±15% statt ±30% — wirkt "glühend" statt "flackernd")
        float sparkX = cx + bs * 0.55f;
        float sparkY = by - bs - 4f;
        float sparkPulse = 0.85f + MathF.Sin(_animTime * 6f) * 0.15f;

        _sparkPaint.MaskFilter = HardGlow;
        _sparkPaint.Color = AccentCyan.WithAlpha((byte)(alpha * 0.85f * sparkPulse));
        canvas.DrawCircle(sparkX, sparkY, 5f * sparkPulse, _sparkPaint);
        _sparkPaint.MaskFilter = null;

        _sparkPaint.Color = White.WithAlpha((byte)(alpha * sparkPulse));
        canvas.DrawCircle(sparkX, sparkY, 2.2f, _sparkPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DETONATOR-BUTTON (Cyan-Oktagon mit Blitz)
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderDetonatorButton(SKCanvas canvas, byte alpha)
    {
        float r = _detonatorButtonRadius;
        float cx = _detonatorButtonX;
        float cy = _detonatorButtonY;

        // Ruhiger Breath-Effekt: 1.5 Hz und ±5% (konsistent mit Bomb-Button).
        // Phasenversatz +1f damit Bomb und Detonator nicht synchron pulsen.
        float idle = 0.95f + MathF.Sin(_animTime * 1.5f + 1f) * 0.05f;
        byte ringA = (byte)(alpha * (0.75f + _detonatorGlow * 0.25f) * idle);

        // Cyan-Glow-Aura — JEDEN Frame mit halbem Alpha statt 15-Hz-Flackern.
        byte glowA = (byte)(alpha * (0.22f + _detonatorGlow * 0.4f) * idle);
        _detoGlowPaint.MaskFilter = SoftGlow;
        _detoGlowPaint.Color = AccentCyan.WithAlpha(glowA);
        BuildOctagon(_octagonPath, cx, cy, r * 1.15f);
        canvas.DrawPath(_octagonPath, _detoGlowPaint);
        _detoGlowPaint.MaskFilter = null;

        // Body
        BuildOctagon(_octagonPath, cx, cy, r);
        SKColor bodyCol = _detonatorButtonPressed ? DetoBodyDown : DetoBodyIdle;
        _detoBodyPaint.Color = bodyCol.WithAlpha(alpha);
        canvas.DrawPath(_octagonPath, _detoBodyPaint);

        // Outline
        _detoOutlinePaint.Color = AccentCyan.WithAlpha(ringA);
        canvas.DrawPath(_octagonPath, _detoOutlinePaint);

        // Stilisierter Blitz
        float s = r * 0.45f;
        _boltPath.Rewind();
        _boltPath.MoveTo(cx + s * 0.15f, cy - s);
        _boltPath.LineTo(cx - s * 0.45f, cy + s * 0.05f);
        _boltPath.LineTo(cx - s * 0.1f,  cy + s * 0.05f);
        _boltPath.LineTo(cx - s * 0.15f, cy + s);
        _boltPath.LineTo(cx + s * 0.45f, cy - s * 0.1f);
        _boltPath.LineTo(cx + s * 0.1f,  cy - s * 0.1f);
        _boltPath.Close();

        // Blitz-Glow
        _detoIconPaint.MaskFilter = HardGlow;
        _detoIconPaint.Color = AccentCyan.WithAlpha((byte)(alpha * 0.7f));
        canvas.DrawPath(_boltPath, _detoIconPaint);
        _detoIconPaint.MaskFilter = null;

        // Blitz (weiss)
        _detoIconPaint.Color = White.WithAlpha(alpha);
        canvas.DrawPath(_boltPath, _detoIconPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PATH-BUILDER (wiederverwendbar via SKPath.Rewind())
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut ein achsen-aligned Oktagon (gerade Seiten oben/unten/links/rechts + Diagonalen),
    /// analog zu den GameIcon-Pfaden der App. Knickpunkt bei 0.414 * r (tan(22.5°)).
    /// </summary>
    private static void BuildOctagon(SKPath path, float cx, float cy, float r)
    {
        float s = r * 0.414f;
        path.Rewind();
        path.MoveTo(cx - s, cy - r);
        path.LineTo(cx + s, cy - r);
        path.LineTo(cx + r, cy - s);
        path.LineTo(cx + r, cy + s);
        path.LineTo(cx + s, cy + r);
        path.LineTo(cx - s, cy + r);
        path.LineTo(cx - r, cy + s);
        path.LineTo(cx - r, cy - s);
        path.Close();
    }

    /// <summary>
    /// Baut einen Chevron-Pfeil (Dreieck mit konkaver Basis) an (cx, cy) in Richtung rotDeg.
    /// rotDeg: 0 = oben, 90 = rechts, 180 = unten, 270 = links.
    /// </summary>
    private static void BuildArrow(SKPath path, float cx, float cy, float size, float rotDeg)
    {
        float rad = rotDeg * MathF.PI / 180f;
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        path.Rewind();
        AddRotatedMove(path, cx, cy, 0f,    -size,       cos, sin);
        AddRotatedLine(path, cx, cy, size,   size * 0.7f, cos, sin);
        AddRotatedLine(path, cx, cy, 0f,     size * 0.2f, cos, sin);
        AddRotatedLine(path, cx, cy, -size,  size * 0.7f, cos, sin);
        path.Close();
    }

    private static void AddRotatedMove(SKPath path, float cx, float cy, float x, float y, float cos, float sin)
        => path.MoveTo(cx + x * cos - y * sin, cy + x * sin + y * cos);

    private static void AddRotatedLine(SKPath path, float cx, float cy, float x, float y, float cos, float sin)
        => path.LineTo(cx + x * cos - y * sin, cy + x * sin + y * cos);

    /// <summary>Linear-Interpolation zwischen zwei SKColor-Werten (inkl. Alpha).</summary>
    private static SKColor LerpColor(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(a.Red   + (b.Red   - a.Red)   * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue  + (b.Blue  - a.Blue)  * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE (gibt alle SKPaint/SKPath frei)
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        _glowPaint.Dispose();
        _bodyPaint.Dispose();
        _outlinePaint.Dispose();
        _innerRingPaint.Dispose();
        _arrowPaint.Dispose();
        _stickBodyPaint.Dispose();
        _stickCorePaint.Dispose();
        _stickOutlinePaint.Dispose();
        _trailPaint.Dispose();

        _bombGlowPaint.Dispose();
        _bombBodyPaint.Dispose();
        _bombOutlinePaint.Dispose();
        _bombIconPaint.Dispose();
        _fusePaint.Dispose();
        _sparkPaint.Dispose();

        _detoGlowPaint.Dispose();
        _detoBodyPaint.Dispose();
        _detoOutlinePaint.Dispose();
        _detoIconPaint.Dispose();

        _octagonPath.Dispose();
        _stickOctagonPath.Dispose();
        _arrowPath.Dispose();
        _fusePath.Dispose();
        _boltPath.Dispose();
    }
}
