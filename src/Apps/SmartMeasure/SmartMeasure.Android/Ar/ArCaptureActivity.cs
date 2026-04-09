using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Opengl;
using Android.OS;
using Android.Views;
using Android.Widget;
using Google.AR.Core;
using Google.AR.Core.Exceptions;
using SmartMeasure.Shared.Models;
using Javax.Microedition.Khronos.Opengles;
using EGLConfig = Javax.Microedition.Khronos.Egl.EGLConfig;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Native AR-Capture-Activity mit ARCore.
/// Zeigt Kamera-Preview ueber GLSurfaceView, erlaubt Punkte setzen per Tap.
/// Ergebnis wird ueber statisches Feld zurueckgegeben (zu gross fuer Intent-Extras).
/// </summary>
[Activity(
    Theme = "@style/MyTheme.Fullscreen",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize,
    ScreenOrientation = ScreenOrientation.Portrait)]
public class ArCaptureActivity : AndroidX.AppCompat.App.AppCompatActivity, GLSurfaceView.IRenderer
{
    public const int REQUEST_CODE = 9011;

    // Statisches Ergebnis-Feld (Intent-Extras haben Groessenlimit)
    private static ArCaptureResult? _lastResult;
    public static ArCaptureResult? ConsumeLastResult()
    {
        var result = _lastResult;
        _lastResult = null;
        return result;
    }

    // ARCore
    private Session? _arSession;
    private GLSurfaceView? _glSurfaceView;
    private bool _installRequested;

    // OpenGL + Kamera-Rendering
    private int _cameraTextureId;
    private readonly float[] _projectionMatrix = new float[16];
    private readonly float[] _viewMatrix = new float[16];
    private ArBackgroundRenderer? _backgroundRenderer;
    private int _viewportWidth;
    private int _viewportHeight;

    // Overlay
    private ArPointOverlayView? _overlayView;

    // Erfasste Daten
    private readonly List<ArPoint> _points = [];
    private readonly List<ArContour> _contours = [];
    private ArContour? _activeContour;
    private DateTime _sessionStart;
    private CaptureMode _captureMode = CaptureMode.Point;

    // Sensordaten zum Session-Start
    private double? _gpsLatitude;
    private double? _gpsLongitude;
    private double? _gpsAltitude;
    private float? _gpsAccuracy;
    private float? _magneticHeading;
    private float? _barometricAltitude;

    // Sensor-Manager fuer Heading/Barometer
    private global::Android.Hardware.SensorManager? _sensorManager;
    private readonly float[] _rotationMatrix = new float[9];
    private readonly float[] _orientationAngles = new float[3];

    // Drag-Startposition fuer Undo
    private (float x, float y, float z)? _dragStartPos;

    // Punkt-Editor State (Einzel- oder Kontur-Punkt)
    private int _selectedPointIndex = -1;
    private int _selectedContourIdx = -1;
    private int _selectedContourPointIdx = -1;
    private bool _isContourPointSelected;
    private bool _isDragging;
    private float _touchDownX;
    private float _touchDownY;
    private const float TAP_THRESHOLD_DP = 12f; // Max Bewegung fuer Tap vs. Drag
    private const float SELECT_RADIUS_DP = 30f; // Radius fuer Punkt-Auswahl

    // Undo/Redo
    private readonly Stack<IArAction> _undoStack = new();
    private readonly Stack<IArAction> _redoStack = new();

    // Screen-Positionen der projizierten Punkte (wird pro Frame aktualisiert)
    private readonly List<(float screenX, float screenY, int pointIndex)> _projectedPoints = [];
    private readonly List<(float screenX, float screenY, int contourIdx, int pointIdx)> _projectedContourPoints = [];

    // UI-Referenzen
    private TextView? _modeText;
    private TextView? _counterText;
    private TextView? _distanceText;

    // Letzter Frame fuer Hit-Testing
    private Frame? _lastFrame;
    private readonly object _frameLock = new();
    private readonly object _dataLock = new(); // Schuetzt _points, _contours, _activeContour

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _sessionStart = DateTime.UtcNow;

        // Sensordaten beim Start erfassen
        CaptureGpsPosition();
        CaptureSensorData();

        // Root-Layout: FrameLayout (Schichten uebereinander)
        var rootLayout = new FrameLayout(this)
        {
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };

        // Schicht 1: GLSurfaceView (ARCore Kamera-Preview + OpenGL)
        _glSurfaceView = new GLSurfaceView(this);
        _glSurfaceView.PreserveEGLContextOnPause = true;
        _glSurfaceView.SetEGLContextClientVersion(2);
        _glSurfaceView.SetEGLConfigChooser(8, 8, 8, 8, 16, 0);
        _glSurfaceView.SetRenderer(this);
        _glSurfaceView.RenderMode = Rendermode.Continuously;
        rootLayout.AddView(_glSurfaceView);

        // Schicht 2: Transparentes Overlay (Punkte, Linien, Info)
        _overlayView = new ArPointOverlayView(this, _points, _contours);
        rootLayout.AddView(_overlayView);

        // Schicht 3: Toolbar (native Android Buttons)
        CreateToolbar(rootLayout);

        SetContentView(rootLayout);
    }

    // Toolbar-Buttons fuer aktiven Modus
    private Button? _btnPoint;
    private Button? _btnContour;

    private void CreateToolbar(FrameLayout root)
    {
        var density = Resources!.DisplayMetrics!.Density;

        // Toolbar: HorizontalScrollView damit alle Buttons auf schmalen Bildschirmen erreichbar sind
        var scrollView = new HorizontalScrollView(this)
        {
            HorizontalScrollBarEnabled = false,
            FillViewport = true
        };
        var scrollParams = new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            (int)(64 * density))
        {
            Gravity = GravityFlags.Bottom
        };
        scrollView.LayoutParameters = scrollParams;
        scrollView.SetBackgroundColor(Color.Argb(180, 0, 0, 0));

        var toolbar = new LinearLayout(this)
        {
            Orientation = global::Android.Widget.Orientation.Horizontal,
        };
        toolbar.SetGravity(GravityFlags.CenterVertical);
        toolbar.SetPadding((int)(4 * density), (int)(6 * density), (int)(4 * density), (int)(6 * density));

        // Toolbar-Buttons (kompakter: 56dp statt 80dp, kein Margin-Overhead)
        _btnPoint = AddToolbarButton(toolbar, "\u25CE Punkt", density, () => SetMode(CaptureMode.Point), true);
        _btnContour = AddToolbarButton(toolbar, "\u2500 Linie", density, () => SetMode(CaptureMode.Contour));
        AddToolbarButton(toolbar, "\u25EF Schließen", density, CloseActiveContour);
        AddToolbarButton(toolbar, "\u21B6", density, Undo);
        AddToolbarButton(toolbar, "\u21B7", density, Redo);
        AddToolbarButton(toolbar, "\u2716 Löschen", density, DeleteSelectedPoint);
        AddToolbarButton(toolbar, "\u2714 Fertig", density, FinishCapture);

        scrollView.AddView(toolbar);
        root.AddView(scrollView);

        // Zurueck-Button (oben links)
        var backButton = new ImageButton(this);
        backButton.SetImageResource(global::Android.Resource.Drawable.IcMenuCloseClearCancel);
        backButton.SetBackgroundColor(Color.Transparent);
        backButton.SetColorFilter(Color.White);
        var backParams = new FrameLayout.LayoutParams(
            (int)(48 * density), (int)(48 * density))
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
            TopMargin = (int)(16 * density),
            LeftMargin = (int)(16 * density)
        };
        backButton.LayoutParameters = backParams;
        backButton.Click += (_, _) =>
        {
            SetResult(Result.Canceled);
            Finish();
        };
        root.AddView(backButton);

        // Modus-Anzeige (oben rechts)
        _modeText = new TextView(this)
        {
            Text = "Modus: Punkt",
            TextSize = 14,
        };
        _modeText.SetTextColor(Color.White);
        _modeText.SetShadowLayer(4f, 0f, 0f, Color.Black);
        var modeParams = new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent)
        {
            Gravity = GravityFlags.Top | GravityFlags.End,
            TopMargin = (int)(24 * density),
            RightMargin = (int)(16 * density)
        };
        _modeText.LayoutParameters = modeParams;
        root.AddView(_modeText);

        // Punkt-Zaehler (oben mitte)
        _counterText = new TextView(this)
        {
            Text = "Punkte: 0",
            TextSize = 14,
        };
        _counterText.SetTextColor(Color.White);
        _counterText.SetShadowLayer(4f, 0f, 0f, Color.Black);
        var counterParams = new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent)
        {
            Gravity = GravityFlags.Top | GravityFlags.CenterHorizontal,
            TopMargin = (int)(24 * density)
        };
        _counterText.LayoutParameters = counterParams;
        root.AddView(_counterText);
    }

    private Button AddToolbarButton(LinearLayout toolbar, string text, float density,
        Action onClick, bool isActive = false)
    {
        var button = new Button(this)
        {
            Text = text,
            TextSize = 11
        };
        button.SetAllCaps(false);
        button.SetTextColor(Color.White);
        button.SetBackgroundColor(isActive
            ? Color.Argb(220, 255, 107, 0)   // Aktiv: kräftiges Orange
            : Color.Argb(80, 255, 255, 255)); // Inaktiv: dezentes Weiß
        button.SetPadding((int)(6 * density), 0, (int)(6 * density), 0);
        button.SetMinimumWidth(0);
        button.SetMinWidth(0);
        var lp = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent, (int)(44 * density))
        {
            LeftMargin = (int)(2 * density),
            RightMargin = (int)(2 * density)
        };
        button.LayoutParameters = lp;
        button.Click += (_, _) => onClick();
        toolbar.AddView(button);
        return button;
    }

    private void SetMode(CaptureMode mode)
    {
        // Aktive Kontur abschliessen wenn Modus wechselt
        if (_activeContour != null && _activeContour.Points.Count > 0)
        {
            lock (_dataLock)
                _contours.Add(_activeContour);
            _activeContour = null;
        }

        _captureMode = mode;

        // Modus-Text + Button-Highlighting aktualisieren
        if (_modeText != null)
            _modeText.Text = mode == CaptureMode.Point ? "Modus: Punkt" : "Modus: Linie";

        _btnPoint?.SetBackgroundColor(mode == CaptureMode.Point
            ? Color.Argb(220, 255, 107, 0)    // Aktiv: kräftiges Orange
            : Color.Argb(80, 255, 255, 255));  // Inaktiv: dezent
        _btnContour?.SetBackgroundColor(mode == CaptureMode.Contour
            ? Color.Argb(220, 255, 107, 0)
            : Color.Argb(80, 255, 255, 255));
    }

    /// <summary>Aktive Kontur schliessen (letzten Punkt mit erstem verbinden)</summary>
    private void CloseActiveContour()
    {
        if (_activeContour == null || _activeContour.Points.Count < 3) return;

        _activeContour.IsClosed = true;
        lock (_dataLock)
            _contours.Add(_activeContour);
        _activeContour = null;
        UpdateCounter();
        _overlayView?.Invalidate();

        if (_modeText != null)
            _modeText.Text = "Kontur geschlossen";
    }

    private void FinishCapture()
    {
        // Aktive Kontur abschliessen
        lock (_dataLock)
        {
            if (_activeContour != null && _activeContour.Points.Count > 0)
                _contours.Add(_activeContour);
        }

        // Ergebnis zusammenbauen (Snapshot unter Lock)
        List<ArPoint> pointsCopy;
        List<ArContour> contoursCopy;
        lock (_dataLock)
        {
            pointsCopy = new List<ArPoint>(_points);
            contoursCopy = new List<ArContour>(_contours);
        }

        _lastResult = new ArCaptureResult
        {
            Points = pointsCopy,
            Contours = contoursCopy,
            GpsLatitude = _gpsLatitude,
            GpsLongitude = _gpsLongitude,
            GpsAltitude = _gpsAltitude,
            GpsAccuracy = _gpsAccuracy,
            MagneticHeading = _magneticHeading,
            BarometricAltitude = _barometricAltitude,
            StartedAt = _sessionStart,
            SessionDuration = DateTime.UtcNow - _sessionStart,
        };

        SetResult(Result.Ok, new Intent());
        Finish();
    }

    private void UpdateCounter()
    {
        var total = _points.Count + _contours.Sum(c => c.Points.Count)
            + (_activeContour?.Points.Count ?? 0);
        if (_counterText != null)
            _counterText.Text = $"Punkte: {total}";
    }

    #region Touch → Auswahl / Drag / Hit-Test

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e == null) return base.OnTouchEvent(e);

        var density = Resources!.DisplayMetrics!.Density;
        var toolbarHeight = 64 * density;

        // Toolbar-Bereich ignorieren (View-Höhe statt DisplayMetrics, korrekt bei Fullscreen)
        var viewHeight = _glSurfaceView?.Height ?? Resources.DisplayMetrics!.HeightPixels;
        if (e.GetY() > viewHeight - toolbarHeight)
            return base.OnTouchEvent(e);

        switch (e.Action)
        {
            case MotionEventActions.Down:
                return HandleTouchDown(e, density);

            case MotionEventActions.Move:
                return HandleTouchMove(e, density);

            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
                return HandleTouchUp(e, density);

            default:
                return base.OnTouchEvent(e);
        }
    }

    private bool HandleTouchDown(MotionEvent e, float density)
    {
        _touchDownX = e.GetX();
        _touchDownY = e.GetY();
        _isDragging = false;
        _dragStartPos = null;

        // Pruefen ob ein existierender Punkt (Einzel oder Kontur) getroffen wurde
        var selectRadius = SELECT_RADIUS_DP * density;
        FindNearestProjectedPoint(_touchDownX, _touchDownY, selectRadius);

        // Startposition merken fuer Undo
        var selectedPoint = GetSelectedArPoint();
        if (selectedPoint != null)
            _dragStartPos = (selectedPoint.X, selectedPoint.Y, selectedPoint.Z);

        return true;
    }

    /// <summary>Den aktuell ausgewaehlten ArPoint zurueckgeben (Einzel oder Kontur)</summary>
    private ArPoint? GetSelectedArPoint()
    {
        if (!_isContourPointSelected && _selectedPointIndex >= 0 && _selectedPointIndex < _points.Count)
            return _points[_selectedPointIndex];

        if (_isContourPointSelected && _selectedContourPointIdx >= 0)
        {
            var contour = _selectedContourIdx == -1
                ? _activeContour
                : (_selectedContourIdx < _contours.Count ? _contours[_selectedContourIdx] : null);
            if (contour != null && _selectedContourPointIdx < contour.Points.Count)
                return contour.Points[_selectedContourPointIdx];
        }

        return null;
    }

    private bool HandleTouchMove(MotionEvent e, float density)
    {
        var dx = e.GetX() - _touchDownX;
        var dy = e.GetY() - _touchDownY;
        var tapThreshold = TAP_THRESHOLD_DP * density;

        if (!_isDragging && (dx * dx + dy * dy) > tapThreshold * tapThreshold)
        {
            _isDragging = true;
        }

        if (_isDragging && (_selectedPointIndex >= 0 || _isContourPointSelected))
        {
            // Ausgewaehlten Punkt (Einzel- oder Kontur-Punkt) per Hit-Test verschieben
            MoveSelectedPoint(e.GetX(), e.GetY());
        }

        return true;
    }

    private bool HandleTouchUp(MotionEvent e, float density)
    {
        if (!_isDragging)
        {
            // Tap (kein Drag): Neuen Punkt setzen oder Punkt auswählen
            if (_selectedPointIndex >= 0 || _isContourPointSelected)
            {
                // Punkt war ausgewaehlt → Auswahl behalten (fuer Loeschen etc.)
                RunOnUiThread(() => _overlayView?.SetSelectedIndex(
                    _isContourPointSelected ? -1 : _selectedPointIndex));
            }
            else
            {
                // Kein Punkt getroffen → neuen Punkt per Hit-Test setzen
                PlaceNewPoint(e.GetX(), e.GetY());
            }
        }
        else if (_isDragging && (_selectedPointIndex >= 0 || _isContourPointSelected))
        {
            // Drag beendet → finale Position setzen + Undo-Action erstellen
            MoveSelectedPoint(e.GetX(), e.GetY());

            var p = GetSelectedArPoint();
            if (_dragStartPos.HasValue && p != null)
            {
                var (oldX, oldY, oldZ) = _dragStartPos.Value;
                _undoStack.Push(new MovePointAction(p, oldX, oldY, oldZ, p.X, p.Y, p.Z));
                _redoStack.Clear();
            }
        }

        _isDragging = false;
        _dragStartPos = null;
        return true;
    }

    /// <summary>Naechsten projizierten Punkt finden (Einzel- und Kontur-Punkte)</summary>
    private void FindNearestProjectedPoint(float screenX, float screenY, float maxRadius)
    {
        var bestDist = maxRadius * maxRadius;
        _selectedPointIndex = -1;
        _selectedContourIdx = -1;
        _selectedContourPointIdx = -1;
        _isContourPointSelected = false;

        // Einzelpunkte durchsuchen
        lock (_projectedPoints)
        {
            for (var i = 0; i < _projectedPoints.Count; i++)
            {
                var (px, py, _) = _projectedPoints[i];
                var dx = screenX - px;
                var dy = screenY - py;
                var dist = dx * dx + dy * dy;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    _selectedPointIndex = i;
                    _isContourPointSelected = false;
                }
            }
        }

        // Kontur-Punkte durchsuchen
        lock (_projectedContourPoints)
        {
            for (var i = 0; i < _projectedContourPoints.Count; i++)
            {
                var (px, py, cIdx, pIdx) = _projectedContourPoints[i];
                var dx = screenX - px;
                var dy = screenY - py;
                var dist = dx * dx + dy * dy;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    _selectedPointIndex = -1;
                    _selectedContourIdx = cIdx;
                    _selectedContourPointIdx = pIdx;
                    _isContourPointSelected = true;
                }
            }
        }
    }

    private void PlaceNewPoint(float screenX, float screenY)
    {
        var arPoint = HitTestAt(screenX, screenY);
        if (arPoint == null)
        {
            // Feedback: Kein Plane erkannt → User muss Kamera bewegen
            RunOnUiThread(() =>
                Toast.MakeText(this, "Keine Fläche erkannt - Kamera langsam bewegen", ToastLength.Short)?.Show());
            return;
        }

        RunOnUiThread(() =>
        {
            lock (_dataLock)
            {
                if (_captureMode == CaptureMode.Point)
                {
                    _undoStack.Push(new AddPointAction(_points, arPoint));
                    _redoStack.Clear();
                    _points.Add(arPoint);
                }
                else
                {
                    _activeContour ??= new ArContour { ContourType = ArContourType.Grenze };
                    _undoStack.Push(new AddContourPointAction(_activeContour, arPoint));
                    _redoStack.Clear();
                    _activeContour.Points.Add(arPoint);
                }
            }
            UpdateCounter();
            _overlayView?.Invalidate();
        });
    }

    private void MoveSelectedPoint(float screenX, float screenY)
    {
        var point = GetSelectedArPoint();
        if (point == null) return;

        var newPos = HitTestAt(screenX, screenY);
        if (newPos == null) return;

        RunOnUiThread(() =>
        {
            point.X = newPos.X;
            point.Y = newPos.Y;
            point.Z = newPos.Z;
            point.AnchorId = newPos.AnchorId;
            _overlayView?.Invalidate();
        });
    }

    private ArPoint? HitTestAt(float screenX, float screenY)
    {
        Frame? frame;
        lock (_frameLock)
        {
            frame = _lastFrame;
        }
        if (frame == null) return null;

        try
        {
            var hitResults = frame.HitTest(screenX, screenY);
            if (hitResults == null || hitResults.Count == 0) return null;

            // Besten Hit verwenden (Plane bevorzugt, dann Point)
            HitResult? bestHit = null;
            foreach (var hit in hitResults)
            {
                var trackable = hit.Trackable;
                if (trackable is Plane plane && plane.IsPoseInPolygon(hit.HitPose))
                {
                    bestHit = hit;
                    break;
                }
                bestHit ??= hit;
            }

            if (bestHit == null) return null;
            var pose = bestHit.HitPose;
            if (pose == null) return null;

            var arPoint = new ArPoint
            {
                X = pose.Tx(),
                Y = pose.Ty(),
                Z = pose.Tz(),
                Confidence = 0.9f,
                Timestamp = DateTime.UtcNow,
            };

            // Snap-to-Edge: Pruefen ob eine Plane-Kante in der Naehe ist
            SnapToPlaneEdge(arPoint, frame);

            // Kein Anchor erstellen: Wir nutzen nur die Pose-Koordinaten.
            // CreateAnchor() hat Limit pro Session und die AnchorId wird nie aufgeloest.

            return arPoint;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"Hit-Test fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    /// <summary>Punkt auf die naechste Plane-Kante einrasten (Snap-to-Edge)</summary>
    private const float SNAP_DISTANCE_METERS = 0.15f; // 15cm Snap-Radius

    private void SnapToPlaneEdge(ArPoint point, Frame frame)
    {
        try
        {
            // Alle bekannten Planes verwenden, nicht nur aktualisierte
            var planes = new List<Plane>();
            if (_arSession != null)
            {
                foreach (var trackable in _arSession.GetAllTrackables(Java.Lang.Class.FromType(typeof(Plane)))!)
                {
                    if (trackable is Plane plane && plane.TrackingState == TrackingState.Tracking
                        && plane.SubsumedBy == null)
                        planes.Add(plane);
                }
            }

            var bestDist = SNAP_DISTANCE_METERS;
            float snapX = point.X, snapZ = point.Z;
            var snapped = false;

            foreach (var plane in planes)
            {
                var polygon = plane.Polygon;
                if (polygon == null || polygon.Remaining() < 4) continue;

                var pose = plane.CenterPose;
                if (pose == null) continue;

                // Rotationsmatrix aus Quaternion extrahieren (fuer X/Z-Ebene)
                var q = pose.GetRotationQuaternion();
                float qx = q[0], qy = q[1], qz = q[2], qw = q[3];
                float r00 = 1 - 2 * (qy * qy + qz * qz);
                float r02 = 2 * (qx * qz + qw * qy);
                float r20 = 2 * (qx * qz - qw * qy);
                float r22 = 1 - 2 * (qx * qx + qy * qy);

                // Polygon-Vertices auslesen (2D in Plane-Koordinaten)
                var vertexCount = polygon.Remaining() / 2;
                var vertices = new float[vertexCount * 2];
                polygon.Get(vertices);
                polygon.Rewind();

                // Polygon-Kanten durchgehen, naechste Kante finden
                for (var i = 0; i < vertexCount; i++)
                {
                    var j = (i + 1) % vertexCount;

                    // Plane-lokale (lx, lz) → Welt-Koordinaten via Rotationsmatrix + Translation
                    var lx1 = vertices[i * 2];
                    var lz1 = vertices[i * 2 + 1];
                    var wx1 = pose.Tx() + r00 * lx1 + r02 * lz1;
                    var wz1 = pose.Tz() + r20 * lx1 + r22 * lz1;

                    var lx2 = vertices[j * 2];
                    var lz2 = vertices[j * 2 + 1];
                    var wx2 = pose.Tx() + r00 * lx2 + r02 * lz2;
                    var wz2 = pose.Tz() + r20 * lx2 + r22 * lz2;

                    // Punkt-zu-Linie Abstand (2D, X/Z-Ebene)
                    var dx = wx2 - wx1;
                    var dz = wz2 - wz1;
                    var lenSq = dx * dx + dz * dz;
                    if (lenSq < 0.0001f) continue;

                    var t = Math.Clamp(((point.X - wx1) * dx + (point.Z - wz1) * dz) / lenSq, 0f, 1f);
                    var closestX = wx1 + t * dx;
                    var closestZ = wz1 + t * dz;

                    var distX = point.X - closestX;
                    var distZ = point.Z - closestZ;
                    var dist = MathF.Sqrt(distX * distX + distZ * distZ);

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        snapX = closestX;
                        snapZ = closestZ;
                        snapped = true;
                    }
                }
            }

            if (snapped)
            {
                point.X = snapX;
                point.Z = snapZ;
                if (point.Label == null || !point.Label.Contains("[snap]"))
                    point.Label = (point.Label ?? "") + " [snap]";
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"Snap-to-Edge fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>Ausgewaehlten Punkt loeschen (Einzel- und Kontur-Punkte)</summary>
    private void DeleteSelectedPoint()
    {
        if (!_isContourPointSelected && _selectedPointIndex >= 0 && _selectedPointIndex < _points.Count)
        {
            lock (_dataLock)
            {
                _undoStack.Push(new DeletePointAction(_points, _selectedPointIndex, _points[_selectedPointIndex]));
                _redoStack.Clear();
                _points.RemoveAt(_selectedPointIndex);
            }
            _selectedPointIndex = -1;
        }
        else if (_isContourPointSelected)
        {
            lock (_dataLock)
            {
                var contour = _selectedContourIdx >= 0 && _selectedContourIdx < _contours.Count
                    ? _contours[_selectedContourIdx]
                    : _activeContour;
                if (contour != null && _selectedContourPointIdx >= 0 && _selectedContourPointIdx < contour.Points.Count)
                {
                    var point = contour.Points[_selectedContourPointIdx];
                    _undoStack.Push(new DeleteContourPointAction(contour, _selectedContourPointIdx, point));
                    _redoStack.Clear();
                    contour.Points.RemoveAt(_selectedContourPointIdx);
                }
            }
            _selectedContourIdx = -1;
            _selectedContourPointIdx = -1;
            _isContourPointSelected = false;
        }
        _overlayView?.SetSelectedIndex(-1);
        UpdateCounter();
        _overlayView?.Invalidate();
    }

    #endregion

    #region Sensordaten (GPS + Heading + Barometer)

    private void CaptureGpsPosition()
    {
        try
        {
            // GPS-Permission pruefen (Android 12+ erfordert Runtime-Permission)
            if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(this,
                    global::Android.Manifest.Permission.AccessFineLocation)
                != global::Android.Content.PM.Permission.Granted)
            {
                global::Android.Util.Log.Warn("ArCapture", "GPS-Permission fehlt");
                return;
            }

            var locationManager = GetSystemService(LocationService) as global::Android.Locations.LocationManager;
            if (locationManager == null) return;

            var location = locationManager.GetLastKnownLocation(
                global::Android.Locations.LocationManager.GpsProvider)
                ?? locationManager.GetLastKnownLocation(
                    global::Android.Locations.LocationManager.NetworkProvider);

            if (location != null)
            {
                _gpsLatitude = location.Latitude;
                _gpsLongitude = location.Longitude;
                _gpsAltitude = location.HasAltitude ? location.Altitude : null;
                _gpsAccuracy = location.HasAccuracy ? location.Accuracy : null;
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"GPS nicht verfuegbar: {ex.Message}");
        }
    }

    /// <summary>Magnetometer-Heading und barometrische Hoehe erfassen</summary>
    private void CaptureSensorData()
    {
        try
        {
            _sensorManager = GetSystemService(SensorService) as global::Android.Hardware.SensorManager;
            if (_sensorManager == null) return;

            // Rotation Vector (Fusion aus Gyro+Accel+Mag) fuer Heading
            var rotationSensor = _sensorManager.GetDefaultSensor(global::Android.Hardware.SensorType.RotationVector);
            if (rotationSensor != null)
            {
                var listener = new HeadingSensorListener(this);
                _sensorManager.RegisterListener(listener, rotationSensor,
                    global::Android.Hardware.SensorDelay.Normal);

                // Nach 1 Sekunde abmelden (brauchen nur den initialen Wert)
                Window?.DecorView?.PostDelayed(() =>
                {
                    try { _sensorManager.UnregisterListener(listener); } catch { /* OK */ }
                }, 1000);
            }

            // Barometer fuer relative Hoehe
            var pressureSensor = _sensorManager.GetDefaultSensor(global::Android.Hardware.SensorType.Pressure);
            if (pressureSensor != null)
            {
                var listener = new PressureSensorListener(this);
                _sensorManager.RegisterListener(listener, pressureSensor,
                    global::Android.Hardware.SensorDelay.Normal);

                Window?.DecorView?.PostDelayed(() =>
                {
                    try { _sensorManager.UnregisterListener(listener); } catch { /* OK */ }
                }, 1000);
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"Sensoren nicht verfuegbar: {ex.Message}");
        }
    }

    /// <summary>Sensor-Listener fuer Magnetisches Heading (Azimuth)</summary>
    private sealed class HeadingSensorListener(ArCaptureActivity activity)
        : Java.Lang.Object, global::Android.Hardware.ISensorEventListener
    {
        public void OnSensorChanged(global::Android.Hardware.SensorEvent? e)
        {
            if (e?.Values == null || e.Values.Count < 4) return;
            var values = new float[e.Values.Count];
            for (var i = 0; i < values.Length; i++) values[i] = e.Values[i];

            global::Android.Hardware.SensorManager.GetRotationMatrixFromVector(
                activity._rotationMatrix, values);
            global::Android.Hardware.SensorManager.GetOrientation(
                activity._rotationMatrix, activity._orientationAngles);

            // Azimuth (Rad → Grad, 0-360)
            var azimuthRad = activity._orientationAngles[0];
            var azimuthDeg = (float)(azimuthRad * 180.0 / Math.PI);
            if (azimuthDeg < 0) azimuthDeg += 360f;
            activity._magneticHeading = azimuthDeg;
        }

        public void OnAccuracyChanged(global::Android.Hardware.Sensor? sensor, global::Android.Hardware.SensorStatus accuracy) { }
    }

    /// <summary>Sensor-Listener fuer barometrische Hoehe</summary>
    private sealed class PressureSensorListener(ArCaptureActivity activity)
        : Java.Lang.Object, global::Android.Hardware.ISensorEventListener
    {
        public void OnSensorChanged(global::Android.Hardware.SensorEvent? e)
        {
            if (e?.Values == null || e.Values.Count < 1) return;
            var pressureHPa = e.Values[0];
            // Barometrische Hoehe (Standard-Atmosphaere)
            activity._barometricAltitude = global::Android.Hardware.SensorManager.GetAltitude(
                global::Android.Hardware.SensorManager.PressureStandardAtmosphere, pressureHPa);
        }

        public void OnAccuracyChanged(global::Android.Hardware.Sensor? sensor, global::Android.Hardware.SensorStatus accuracy) { }
    }

    #endregion

    #region ARCore Lifecycle

    protected override void OnResume()
    {
        base.OnResume();

        if (_arSession == null)
        {
            try
            {
                // ARCore-Installation pruefen/anfordern
                var installStatus = ArCoreApk.Instance!.RequestInstall(this, !_installRequested);
                if (installStatus == ArCoreApk.InstallStatus.InstallRequested)
                {
                    _installRequested = true;
                    return;
                }

                // Session erstellen
                _arSession = new Session(this);
                var config = new Google.AR.Core.Config(_arSession);
                config.SetUpdateMode(Google.AR.Core.Config.UpdateMode.LatestCameraImage);
                config.SetPlaneFindingMode(Google.AR.Core.Config.PlaneFindingMode.HorizontalAndVertical);
                config.SetDepthMode(Google.AR.Core.Config.DepthMode.Automatic);
                _arSession.Configure(config);
            }
            catch (UnavailableException ex)
            {
                global::Android.Util.Log.Error("ArCapture", $"ARCore nicht verfuegbar: {ex}");
                Toast.MakeText(this, "ARCore nicht verfuegbar", ToastLength.Long)?.Show();
                SetResult(Result.Canceled);
                Finish();
                return;
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error("ArCapture", $"ARCore Session-Fehler: {ex}");
                SetResult(Result.Canceled);
                Finish();
                return;
            }
        }

        try
        {
            _arSession?.Resume();
            _glSurfaceView?.OnResume();
        }
        catch (CameraNotAvailableException)
        {
            Toast.MakeText(this, "Kamera nicht verfuegbar", ToastLength.Long)?.Show();
            _arSession = null;
            SetResult(Result.Canceled);
            Finish();
        }
    }

    protected override void OnPause()
    {
        base.OnPause();
        _glSurfaceView?.OnPause();
        lock (_frameLock) { _lastFrame = null; }
        _arSession?.Pause();
    }

    #endregion

    #region GLSurfaceView.IRenderer (OpenGL Kamera-Rendering)

    public void OnSurfaceCreated(IGL10? gl, EGLConfig? config)
    {
        GLES20.GlClearColor(0.1f, 0.1f, 0.1f, 1.0f);

        // Kamera-Textur erstellen
        var textures = new int[1];
        GLES20.GlGenTextures(1, textures, 0);
        _cameraTextureId = textures[0];

        // TEXTURE_EXTERNAL_OES fuer Kamera-Frames
        GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, _cameraTextureId);
        GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMinFilter, GLES20.GlLinear);
        GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMagFilter, GLES20.GlLinear);

        _arSession?.SetCameraTextureName(_cameraTextureId);

        // Kamera-Hintergrund-Renderer initialisieren
        _backgroundRenderer = new ArBackgroundRenderer();
        _backgroundRenderer.Initialize();
    }

    public void OnSurfaceChanged(IGL10? gl, int width, int height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        GLES20.GlViewport(0, 0, width, height);
        _arSession?.SetDisplayGeometry((int)SurfaceOrientation.Rotation0, width, height);
    }

    public void OnDrawFrame(IGL10? gl)
    {
        GLES20.GlClear(GLES20.GlColorBufferBit | GLES20.GlDepthBufferBit);

        if (_arSession == null) return;

        try
        {
            var frame = _arSession.Update();
            if (frame == null) return;

            lock (_frameLock)
            {
                // KEIN Dispose: ARCore verwaltet Frame-Lifecycle intern.
                // Dispose wuerde JNI-Referenz freigeben → HitTestAt auf UI-Thread crasht
                _lastFrame = frame;
            }

            // Texturkoordinaten von ARCore holen (Display-Rotation beruecksichtigen)
            var texCoords = new float[8];
            frame.TransformCoordinates2d(
                Coordinates2d.OpenglNormalizedDeviceCoordinates,
                new float[] { -1, -1, 1, -1, -1, 1, 1, 1 },
                Coordinates2d.TextureNormalized,
                texCoords);
            _backgroundRenderer?.UpdateTexCoords(texCoords);

            // Kamera-Hintergrund rendern
            _backgroundRenderer?.Draw(_cameraTextureId);

            var camera = frame.Camera;
            if (camera == null) return;

            if (camera.TrackingState == TrackingState.Tracking)
            {
                camera.GetProjectionMatrix(_projectionMatrix, 0, 0.1f, 100.0f);
                camera.GetViewMatrix(_viewMatrix, 0);

                // Punkt-Positionen fuer Overlay in Screen-Koordinaten umrechnen
                ProjectPointsToScreen();
            }
        }
        catch (CameraNotAvailableException)
        {
            // Kamera verloren
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"Frame-Update fehlgeschlagen: {ex.Message}");
        }
    }

    #endregion

    #region Welt-zu-Screen Projektion

    /// <summary>
    /// Alle 3D-Punkte in Screen-Koordinaten projizieren.
    /// viewMatrix × projectionMatrix × worldPos → NDC → Screen-Pixel.
    /// Wird auf dem GL-Thread aufgerufen (pro Frame).
    /// </summary>
    private void ProjectPointsToScreen()
    {
        var mvpMatrix = new float[16];
        global::Android.Opengl.Matrix.MultiplyMM(mvpMatrix, 0, _projectionMatrix, 0, _viewMatrix, 0);

        var newProjected = new List<(float, float, int)>();
        var newContourProjected = new List<(float, float, int, int)>();

        // Punkte-Snapshot unter Lock erstellen (GL-Thread liest, UI-Thread schreibt)
        lock (_dataLock)
        {
            // Einzelpunkte projizieren
            for (var i = 0; i < _points.Count; i++)
            {
                var screen = WorldToScreen(_points[i], mvpMatrix);
                if (screen.HasValue)
                    newProjected.Add((screen.Value.x, screen.Value.y, i));
            }

            // Kontur-Punkte projizieren
            for (var ci = 0; ci < _contours.Count; ci++)
            {
                for (var pi = 0; pi < _contours[ci].Points.Count; pi++)
                {
                    var screen = WorldToScreen(_contours[ci].Points[pi], mvpMatrix);
                    if (screen.HasValue)
                        newContourProjected.Add((screen.Value.x, screen.Value.y, ci, pi));
                }
            }

            // Aktive Kontur
            if (_activeContour != null)
            {
                for (var pi = 0; pi < _activeContour.Points.Count; pi++)
                {
                    var screen = WorldToScreen(_activeContour.Points[pi], mvpMatrix);
                    if (screen.HasValue)
                        newContourProjected.Add((screen.Value.x, screen.Value.y, -1, pi));
                }
            }
        }

        lock (_projectedPoints)
        {
            _projectedPoints.Clear();
            _projectedPoints.AddRange(newProjected);
        }

        lock (_projectedContourPoints)
        {
            _projectedContourPoints.Clear();
            _projectedContourPoints.AddRange(newContourProjected);
        }

        // Erkannte Planes projizieren
        var projectedPlanes = ProjectPlanesToScreen(mvpMatrix);

        // Overlay auf UI-Thread aktualisieren
        RunOnUiThread(() =>
        {
            List<(float, float, int)> pts;
            List<(float, float, int, int)> cPts;

            lock (_projectedPoints) { pts = new List<(float, float, int)>(_projectedPoints); }
            lock (_projectedContourPoints) { cPts = new List<(float, float, int, int)>(_projectedContourPoints); }

            _overlayView?.UpdateProjectedPositions(pts, cPts);
            _overlayView?.UpdateProjectedPlanes(projectedPlanes);
        });
    }

    /// <summary>
    /// Einzelnen 3D-Punkt in Screen-Koordinaten umrechnen.
    /// Gibt null zurueck wenn der Punkt hinter der Kamera liegt.
    /// </summary>
    private (float x, float y)? WorldToScreen(ArPoint point, float[] mvpMatrix)
    {
        // Homogene Koordinaten
        var clipX = mvpMatrix[0] * point.X + mvpMatrix[4] * point.Y + mvpMatrix[8] * point.Z + mvpMatrix[12];
        var clipY = mvpMatrix[1] * point.X + mvpMatrix[5] * point.Y + mvpMatrix[9] * point.Z + mvpMatrix[13];
        var clipW = mvpMatrix[3] * point.X + mvpMatrix[7] * point.Y + mvpMatrix[11] * point.Z + mvpMatrix[15];

        // Hinter der Kamera?
        if (clipW <= 0.001f) return null;

        // NDC (-1..1)
        var ndcX = clipX / clipW;
        var ndcY = clipY / clipW;

        // Screen-Koordinaten
        var screenX = (ndcX + 1.0f) * 0.5f * _viewportWidth;
        var screenY = (1.0f - ndcY) * 0.5f * _viewportHeight; // Y invertiert

        return (screenX, screenY);
    }

    /// <summary>Erkannte ARCore-Planes in Screen-Koordinaten projizieren</summary>
    private List<List<(float, float)>> ProjectPlanesToScreen(float[] mvpMatrix)
    {
        var result = new List<List<(float, float)>>();

        Frame? frame;
        lock (_frameLock) { frame = _lastFrame; }
        if (frame == null) return result;

        try
        {
            foreach (var trackable in frame.GetUpdatedTrackables(Java.Lang.Class.FromType(typeof(Plane)))!)
            {
                if (trackable is not Plane plane) continue;
                if (plane.TrackingState != TrackingState.Tracking || plane.SubsumedBy != null) continue;

                var polygon = plane.Polygon;
                if (polygon == null || polygon.Remaining() < 6) continue; // Min 3 Punkte

                var pose = plane.CenterPose;
                if (pose == null) continue;

                var vertexCount = polygon.Remaining() / 2;
                var vertices = new float[vertexCount * 2];
                polygon.Get(vertices);
                polygon.Rewind();

                // Rotationsmatrix aus Quaternion (wie in SnapToPlaneEdge)
                var pq = pose.GetRotationQuaternion();
                float pqx = pq[0], pqy = pq[1], pqz = pq[2], pqw = pq[3];
                float pr00 = 1 - 2 * (pqy * pqy + pqz * pqz);
                float pr02 = 2 * (pqx * pqz + pqw * pqy);
                float pr20 = 2 * (pqx * pqz - pqw * pqy);
                float pr22 = 1 - 2 * (pqx * pqx + pqy * pqy);

                var screenPolygon = new List<(float, float)>();
                for (var i = 0; i < vertexCount; i++)
                {
                    // Plane-lokale → Welt-Koordinaten via Rotationsmatrix + Translation
                    var lx = vertices[i * 2];
                    var lz = vertices[i * 2 + 1];
                    var worldX = pose.Tx() + pr00 * lx + pr02 * lz;
                    var worldY = pose.Ty();
                    var worldZ = pose.Tz() + pr20 * lx + pr22 * lz;

                    // Welt → Screen
                    var clipX = mvpMatrix[0] * worldX + mvpMatrix[4] * worldY + mvpMatrix[8] * worldZ + mvpMatrix[12];
                    var clipY = mvpMatrix[1] * worldX + mvpMatrix[5] * worldY + mvpMatrix[9] * worldZ + mvpMatrix[13];
                    var clipW = mvpMatrix[3] * worldX + mvpMatrix[7] * worldY + mvpMatrix[11] * worldZ + mvpMatrix[15];

                    if (clipW <= 0.001f) continue;

                    var sx = (clipX / clipW + 1.0f) * 0.5f * _viewportWidth;
                    var sy = (1.0f - clipY / clipW) * 0.5f * _viewportHeight;
                    screenPolygon.Add((sx, sy));
                }

                if (screenPolygon.Count >= 3)
                    result.Add(screenPolygon);
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"Plane-Projektion fehlgeschlagen: {ex.Message}");
        }

        return result;
    }

    #endregion

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        _selectedPointIndex = -1;
        _overlayView?.SetSelectedIndex(-1);
        UpdateCounter();
        _overlayView?.Invalidate();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        var action = _redoStack.Pop();
        action.Redo();
        _undoStack.Push(action);
        UpdateCounter();
        _overlayView?.Invalidate();
    }

    protected override void OnDestroy()
    {
        // OpenGL-Ressourcen aufraeumen (Textur + Renderer)
        if (_cameraTextureId != 0)
        {
            _glSurfaceView?.QueueEvent(() =>
            {
                GLES20.GlDeleteTextures(1, new[] { _cameraTextureId }, 0);
                _backgroundRenderer?.Dispose();
                _backgroundRenderer = null;
            });
            _cameraTextureId = 0;
        }

        _arSession?.Close();
        _arSession = null;
        base.OnDestroy();
    }

    private enum CaptureMode
    {
        Point,
        Contour
    }

    #region Undo/Redo Actions

    private interface IArAction
    {
        void Undo();
        void Redo();
    }

    private sealed class AddPointAction(List<ArPoint> list, ArPoint point) : IArAction
    {
        public void Undo() => list.Remove(point);
        public void Redo() => list.Add(point);
    }

    private sealed class DeletePointAction(List<ArPoint> list, int index, ArPoint point) : IArAction
    {
        public void Undo() => list.Insert(Math.Min(index, list.Count), point);
        public void Redo() => list.Remove(point);
    }

    private sealed class AddContourPointAction(ArContour contour, ArPoint point) : IArAction
    {
        public void Undo() => contour.Points.Remove(point);
        public void Redo() => contour.Points.Add(point);
    }

    private sealed class DeleteContourPointAction(ArContour contour, int index, ArPoint point) : IArAction
    {
        public void Undo() => contour.Points.Insert(Math.Min(index, contour.Points.Count), point);
        public void Redo() => contour.Points.Remove(point);
    }

    private sealed class MovePointAction(ArPoint point,
        float oldX, float oldY, float oldZ,
        float newX, float newY, float newZ) : IArAction
    {
        public void Undo() { point.X = oldX; point.Y = oldY; point.Z = oldZ; }
        public void Redo() { point.X = newX; point.Y = newY; point.Z = newZ; }
    }

    #endregion
}
