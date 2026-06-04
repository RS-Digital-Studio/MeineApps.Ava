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
using SmartMeasure.Shared;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Javax.Microedition.Khronos.Opengles;
using EGLConfig = Javax.Microedition.Khronos.Egl.EGLConfig;
// Camera: ARCore-Typ (nicht Android.Graphics.Camera). ArCaptureActivity nutzt ausschliesslich ARCore.
using Camera = Google.AR.Core.Camera;

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
public partial class ArCaptureActivity : AndroidX.AppCompat.App.AppCompatActivity, GLSurfaceView.IRenderer
{
    public const int REQUEST_CODE = 9011;

    // Statisches Ergebnis-Feld (Intent-Extras haben Groessenlimit).
    // Lock schützt gegen Race wenn der Konsument das Result liest waehrend die
    // naechste Session bereits einen neuen Wert schreibt.
    private static readonly object _lastResultLock = new();
    private static ArCaptureResult? _lastResult;
    public static ArCaptureResult? ConsumeLastResult()
    {
        lock (_lastResultLock)
        {
            var result = _lastResult;
            _lastResult = null;
            return result;
        }
    }

    /// <summary>Doppel-Submit-Guard: <see cref="FinishCapture"/> kann durch Back-Button und
    /// Toolbar-Fertig gleichzeitig getriggert werden. Auch <see cref="OnPause"/>/<see cref="OnDestroy"/>
    /// dürfen das Result nicht noch einmal anfassen.</summary>
    private int _finished; // 0 = aktiv, 1 = abgeschlossen — via Interlocked.Exchange

    // ARCore
    private Session? _arSession;
    private GLSurfaceView? _glSurfaceView;
    private bool _installRequested;

    // OpenGL + Kamera-Rendering
    private int _cameraTextureId;
    private readonly float[] _projectionMatrix = new float[16];
    private readonly float[] _viewMatrix = new float[16];
    private ArBackgroundRenderer? _backgroundRenderer;
    // volatile: in OnSurfaceChanged (GL-Thread) gesetzt, u.a. in PlaceTotalStationPoint
    // (UI-Thread) gelesen — Sichtbarkeit über die Thread-Grenze garantieren.
    private volatile int _viewportWidth;
    private volatile int _viewportHeight;

    // Overlay
    private ArPointOverlayView? _overlayView;

    // Erfasste Daten
    private readonly List<ArPoint> _points = [];
    private readonly List<ArContour> _contours = [];
    private ArContour? _activeContour;
    private DateTime _sessionStart;
    // volatile: vom UI-Thread (SetMode) geschrieben, vom GL-Thread (BuildOverlayState) jeden
    // Frame gelesen. Ohne Barrier könnte der GL-Thread auf ARM einen veralteten Modus cachen →
    // falscher Modus-Branch (z.B. Tape-Punkte im falschen Buffer). Alle anderen primitiven
    // Cross-Thread-Flags der Klasse sind ebenfalls volatile.
    private volatile CaptureMode _captureMode = CaptureMode.Point;

    /// <summary>Tape-Measure-Buffer (Plan-Kap. 5.3). Punkte hier landen NICHT im
    /// ArCaptureResult — Ad-hoc-Messung, Buffer wird beim Mode-Wechsel oder Long-Click
    /// auf den Tape-Button geleert.</summary>
    private readonly List<ArPoint> _tapeMeasurePoints = [];

    /// <summary>Bereits gesetzte Ecken der laufenden Rechteck-Erfassung (max 2 = Basiskante).
    /// Der dritte Tipp schliesst das Rechteck via <see cref="ArRectangleBuilder"/> zu einer
    /// geschlossenen <see cref="ArContour"/> ab. Geschuetzt durch <see cref="_dataLock"/>.</summary>
    private readonly List<ArPoint> _rectangleCorners = [];

    /// <summary>Quadrat-Snap fuer den Rechteck-Modus: zieht die Tiefe auf die Basislaenge,
    /// wenn sie nahe genug liegt (Robert-Wunsch: gefuehrtes Quadrat ohne Millimeter-Pingelei).</summary>
    private const bool RectangleSquareSnapEnabled = true;

    /// <summary>Plan-Kap. 5.9: Statische Bruecke fuer Stakeout-Targets. Der Caller
    /// (MainViewModel, StakeoutViewModel) setzt die Liste vor dem Activity-Start;
    /// ArCaptureActivity liest sie in OnCreate und reset im OnDestroy.</summary>
    private static readonly object _stakeoutTargetsLock = new();
    private static IReadOnlyList<StakeoutTarget>? _pendingStakeoutTargets;

    /// <summary>Wird vom Caller vor <c>StartActivityForResult</c> aufgerufen, damit der
    /// Stakeout-Modus echte Ziele bekommt.</summary>
    public static void SetStakeoutTargets(IReadOnlyList<StakeoutTarget>? targets)
    {
        lock (_stakeoutTargetsLock) _pendingStakeoutTargets = targets;
    }

    /// <summary>Plan-Kap. 5.2: Statische Bruecke fuer Site-Points. Bestehende
    /// Projekt-Punkte werden als Earth-Anchors visualisiert, damit der User neue Punkte
    /// im selben Koordinatensystem erfasst.</summary>
    private static readonly object _sitePointsLock = new();
    private static IReadOnlyList<SurveyPoint>? _pendingSitePoints;
    public static void SetSitePoints(IReadOnlyList<SurveyPoint>? points)
    {
        lock (_sitePointsLock) _pendingSitePoints = points;
    }

    /// <summary>Plan-Kap. 5.7: Statische Bruecke fuer Referenz-Marker. Augmented-Images-
    /// Datenbank wird beim Activity-Start aus dieser Liste aufgebaut.</summary>
    private static readonly object _markerLock = new();
    private static IReadOnlyList<ArReferenceMarker>? _pendingMarkers;
    public static void SetReferenceMarkers(IReadOnlyList<ArReferenceMarker>? markers)
    {
        lock (_markerLock) _pendingMarkers = markers;
    }

    private IReadOnlyList<ArReferenceMarker>? _referenceMarkers;
    private readonly Dictionary<string, ArReferenceMarker> _markersByImageName = [];

    /// <summary>Site-Points dieser Session (Snapshot zu OnCreate). Werden NICHT ins
    /// ArCaptureResult zurueckgegeben — reine Visualisierungs-Layer.</summary>
    private IReadOnlyList<SurveyPoint>? _sitePoints;

    /// <summary>Bereits via Earth-Anchor instanzierte Site-Punkte als ArPoint-Marker.
    /// Werden im OnDrawFrame iterativ angelegt (max 2 pro Frame) sobald
    /// <see cref="_geospatialActive"/> ist.</summary>
    private readonly List<ArPoint> _sitePointAnchors = [];
    private int _siteAnchorsCreated;

    /// <summary>Plan-Kap. 5.8: Aktueller RTK-Stab-Anchor (immer max einer). Wird einmal
    /// pro Sekunde an die aktuelle BLE-Stab-Position aktualisiert — alte Anchors werden
    /// detacht. null wenn kein RTK-Fix oder noch nicht erzeugt.</summary>
    private ArPoint? _rtkStabAnchor;

    /// <summary>Frame-Zaehler fuer das 1Hz-Refresh des Stab-Anchors (RTK-Stab bewegt sich
    /// gelegentlich, aber 30fps-Refresh wuerde Anchor-Hard-Limit erschoepfen).</summary>
    private int _rtkStabRefreshFrameCounter;

    /// <summary>Letzte gemeldete Fix-Quality des Stabs — bestimmt die Marker-Farbe
    /// (RTK-Fix=Gruen, Float=Gelb, sonst Rot/Aus).</summary>
    private volatile int _rtkStabLastFixQuality;

    /// <summary>Stakeout-Ziele dieser Session (Snapshot zu OnCreate). Veraenderungen an
    /// <see cref="StakeoutTarget.IsReached"/> sind sichtbar fuer den UI-Layer weil
    /// StakeoutTarget ein <c>ObservableObject</c> ist.</summary>
    private IReadOnlyList<StakeoutTarget>? _stakeoutTargets;

    /// <summary>Schwellwert fuer Target-erreicht in Metern (Plan-Kap. 5.9: 10 cm).</summary>
    private const double StakeoutReachedThresholdMeters = 0.10;

    /// <summary>Letzte Distanz zum aktiven Target — verhindert Spam-Haptic wenn der User
    /// gerade ueber das Target hinausschwankt. Erst nach Verlassen erneut feuern.</summary>
    private double _stakeoutLastDistance = double.PositiveInfinity;

    /// <summary>Letzte berechnete Distanz/Bearing/Label des aktiven Targets. GL-Thread
    /// schreibt unter _stakeoutSnapshotLock, BuildOverlayState liest.</summary>
    private readonly object _stakeoutSnapshotLock = new();
    private double? _stakeoutCurrentDistance;
    private double? _stakeoutCurrentRelativeBearingDeg;
    private string? _stakeoutCurrentTargetLabel;

    // Sensordaten zum Session-Start. _magneticHeading wird Cross-Thread geschrieben
    // (Sensor-Thread) und gelesen (GL-/UI-Thread) — Nullable<float> ist ein 8-Byte-Struct
    // und damit NICHT atomic. Lösung: int-Bits eines float, NaN = "kein Wert", via
    // Volatile.Read/Write.
    // _gps*-Felder werden von mehreren Threads geschrieben (BLE-PositionUpdated, GPS-Location-
    // Listener, GPS-Averaging) und in FinishCapture gelesen. Nullable<double>/<float> sind nicht
    // atomar → Torn-Reads (Lat von Fix A, Lon von Fix B). _gpsLock klammert jeden Schreib-
    // Cluster UND die Lesung, sodass der GPS-Anker immer aus EINEM konsistenten Fix stammt.
    private readonly object _gpsLock = new();
    private double? _gpsLatitude;
    private double? _gpsLongitude;
    private double? _gpsAltitude;
    private float? _gpsAccuracy;

    /// <summary>Aktuelle Quelle der GPS-Referenz. Wird in das ArCaptureResult propagiert
    /// und bestimmt in ArTransferService ob die Accuracy auf RTK-Niveau (±2 cm) oder
    /// Android-Location (±5 m) angesetzt wird.</summary>
    private ArGpsSource _gpsSource = ArGpsSource.None;

    /// <summary>BLE-Stab als RTK-Quelle (Plan 3.1 RTK-AR-Fusion). Null wenn kein Rover
    /// connected ist oder die App im Mock-Mode läuft. Wird im OnCreate aus App.Services
    /// aufgelöst — ArCaptureActivity ist eine separate Activity ohne eigenen DI-Container.</summary>
    private IBleService? _bleService;

    /// <summary>Letzter RTK Fix-Quality (0=NoFix, 4=RTK-Fix, 5=Float) zur Session-Zeit.</summary>
    private int _rtkFixQuality;

    /// <summary>Listener-Handle für PositionUpdated — beim OnDestroy abmelden.</summary>
    private Action<double, double, double>? _rtkPositionHandler;
    private float? _barometricAltitude;

    // Volatile-Bits eines float — sentinel NaN = "kein Wert".
    private int _magneticHeadingBits = BitConverter.SingleToInt32Bits(float.NaN);
    private float? _magneticHeading
    {
        get
        {
            var f = BitConverter.Int32BitsToSingle(System.Threading.Volatile.Read(ref _magneticHeadingBits));
            return float.IsNaN(f) ? null : f;
        }
        set => System.Threading.Volatile.Write(ref _magneticHeadingBits,
            BitConverter.SingleToInt32Bits(value ?? float.NaN));
    }

    // Sensor-Manager fuer Heading/Barometer
    private global::Android.Hardware.SensorManager? _sensorManager;
    private readonly float[] _rotationMatrix = new float[9];
    private readonly float[] _orientationAngles = new float[3];

    /// <summary>Aktive Sensor-Listener — werden in OnDestroy garantiert unregistered,
    /// auch wenn die PostDelayed-Cleanups noch nicht abgelaufen sind. Sonst Sensor-Leak
    /// + Battery-Drain wenn Activity vor 5s-Timeout beendet wird.</summary>
    private readonly List<global::Android.Hardware.ISensorEventListener> _activeSensorListeners = [];

    /// <summary>Aktive LocationManager-Listener für gleiches Cleanup wie Sensoren.</summary>
    private readonly List<(global::Android.Locations.LocationManager mgr,
        global::Android.Locations.ILocationListener listener)> _activeLocationListeners = [];

    // Drag-Startposition fuer Undo
    private (float x, float y, float z)? _dragStartPos;
    private string? _dragStartAnchorId;

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

    // Undo/Redo. Undo-Stack mit Bounded-FIFO-Limit (Plan Kap. 4.6) — bei 30-min-Sessions
    // sammeln sich sonst hunderte Aktionen, jede mit ArPoint/Lock-Referenz. 200 ist mehr als
    // ein realistischer User pro Session macht und hält die RAM-Belegung deckelig.
    private const int MaxUndoStackSize = 200;
    private readonly BoundedStack<IArAction> _undoStack = new(MaxUndoStackSize);
    // Redo ebenfalls gedeckelt (war unbeschränkt) — sonst hält wiederholtes Undo/Redo-Togglen
    // hunderte Actions samt ArPoint-Graph im Speicher, entgegen der MaxUndoStackSize-Absicht.
    private readonly BoundedStack<IArAction> _redoStack = new(MaxUndoStackSize);

    // Screen-Positionen der projizierten Punkte (wird pro Frame aktualisiert)
    private readonly List<(float screenX, float screenY, int pointIndex)> _projectedPoints = [];
    private readonly List<(float screenX, float screenY, int contourIdx, int pointIdx)> _projectedContourPoints = [];

    // UI-Referenzen
    private TextView? _modeText;
    private TextView? _counterText;

    // Letzter Frame fuer Hit-Testing.
    //
    // ARCore-Frames sind nicht offiziell cross-thread-safe — die JNI-Referenz bleibt
    // zwar live, ARCore koennte aber waehrend Session.Update() interne Buffer recyceln.
    // Pragmatische Loesung: HitTest-Requests vom UI-Thread werden via _pendingFrameOps
    // an den GL-Thread weitergereicht und am Anfang von OnDrawFrame abgearbeitet, BEVOR
    // ein neues Update() laeuft. Damit garantiert: Frame.HitTest laeuft nur auf dem
    // GL-Thread und ist mit dem Frame-Lifecycle synchron.
    //
    // Drag-Operationen werden dadurch um 1 Frame (~16ms) verzoegert — kaum merklich.
    private Frame? _lastFrame;
    private readonly object _frameLock = new();
    private readonly object _dataLock = new(); // Schuetzt _points, _contours, _activeContour

    /// <summary>Queue von Lambdas die am Anfang von OnDrawFrame im GL-Thread abgearbeitet
    /// werden (typisch HitTest-Requests vom UI-Thread). Lock = die Liste selbst.</summary>
    private readonly List<Action<Frame>> _pendingFrameOps = [];

    // Reticle/Live-HitTest (aktualisiert pro Frame vom GL-Thread)
    private float _reticleHitDistance;
    private float? _reticleHeightDelta;
    private ArHitQuality _reticleHitQuality;
    // Welt-Position des aktuellen Reticle-Ziels (lokale ARCore-Meter). Wird in
    // UpdateReticleState gesetzt (GL-Thread) und in BuildOverlayState (ebenfalls GL-Thread)
    // gelesen, um die Live-Segment-Werte (Distanz/Hoehe/Steigung) vom letzten gesetzten
    // Punkt zum Crosshair-Ziel zu berechnen. Beide laufen sequenziell im OnDrawFrame →
    // kein Lock noetig. null wenn kein gueltiger Hit.
    private (float x, float y, float z)? _reticleWorld;

    // Session-Recovery: temporärer Save nach jedem Punkt-Set
    private const string RecoveryKeyPoints = "ar.recovery.points";
    private const string RecoveryKeyContours = "ar.recovery.contours";
    private const string RecoveryKeyTimestamp = "ar.recovery.timestamp";

    // Haptic Feedback
    private global::Android.OS.Vibrator? _vibrator;

    // Akustisches Feedback beim Punkt-Setzen — Camera-Shutter-Click.
    // Liegt VOR OnCreate-Init, weil PlaceNewPoint via GL-Thread reinkommen kann
    // bevor der UI-Thread fertig ist. Lazy-Init beim ersten Play.
    private global::Android.Media.MediaActionSound? _shutterSound;
    private bool _soundEnabled = true; // Default an; via SharedPreferences "ar.sound.enabled"

    // Beep für Bestätigungs-Aktionen (Fertig / Kontur geschlossen).
    private bool _shutterLoaded;

    // Coach-Marks-Dialog beim ersten AR-Start.
    // SharedPreferences-Key "ar.coachmarks.shown".
    private bool _coachMarksShown;

    // Auto-Close Detection (für Kontur)
    private const float AutoCloseDistanceMeters = 0.3f; // 30cm — User kann Loop schließen

    // Präzisions-Manager: Anchors + Multi-Frame-Averaging + Stabilität
    private readonly ArAnchorManager _anchorManager = new();
    private ArStabilityMonitor? _stabilityMonitor;

    // Multi-Frame-Sampling-State — geteilt zwischen UI-Thread (Start) und GL-Thread (Update/Finalize).
    // _samplerLock schützt die Gruppe; _activeSampler-Referenz-Swap ist zusätzlich volatile-like
    // durch den Lock.
    private readonly object _samplerLock = new();
    private ArPoseSampler? _activeSampler;
    private float _sampleTargetX, _sampleTargetY;
    private int _samplesCollected;
    private long _sampleStartMs;
    private const int MultiFrameSampleTargetCount = 10;
    private const int MultiFrameSampleTimeoutMs = 800;

    /// <summary>Minimum-Sample-Count: weniger gilt als unsichere Messung, der Punkt wird verworfen.
    /// Bei 800ms Timeout und 30fps wären 6 Samples mindestens — auf 5 Samples vor 3.2-Fix konnte
    /// ein Tracking-Aussetzer mid-sampling einen unsicheren Punkt mit hoher Confidence liefern.</summary>
    private const int MinValidSampleCount = 6;

    /// <summary>
    /// Maximaler Pausen-Anteil am Sampling-Fenster bevor das Sampling als unsicher gilt.
    /// 0.5 = wenn die Hälfte der Frames kein Tracking hatte → Punkt verwerfen + retry-Hint.
    /// </summary>
    private const float MaxPauseRatio = 0.5f;

    /// <summary>Frames im aktiven Sampling-Fenster mit Tracking=on. _samplerLock.</summary>
    private int _samplerActiveFrames;

    /// <summary>Frames im aktiven Sampling-Fenster ohne Tracking (Kamera abgedeckt etc.). _samplerLock.</summary>
    private int _samplerPauseFrames;

    // GPS/Heading Averaging (Multi-Sample beim Session-Start)
    private readonly List<(double lat, double lon, double? alt, float? acc)> _gpsSamples = [];
    private readonly List<float> _headingSamples = [];
    private const int GpsSampleTargetCount = 10;
    private const int HeadingSampleTargetCount = 20;

    // Ground-Plane als absolute Höhen-Referenz (Y-Wert der größten horizontalen Plane).
    // Float? ist nicht atomic → wir nutzen stattdessen float + separates volatile-Flag.
    private volatile bool _groundPlaneYSet;
    private float _groundPlaneYValue;

    // ARCore-basiertes Heading (Sensor-Fusion aus Kamera-Pose)
    private readonly List<float> _arCoreHeadingSamples = [];

    // Magnetometer-Accuracy (0=keine bis 3=hoch) — vom Sensor-Thread geschrieben, GL-Thread gelesen
    private volatile int _magneticAccuracy = 3;
    // int-Flag + Interlocked statt volatile bool: das check-then-set unten muss atomar sein,
    // sonst können zwei schnell aufeinanderfolgende Accuracy-Events beide das Gate passieren
    // und zwei Kalibrierungs-Dialoge öffnen.
    private int _lowMagAccuracyWarned;

    // Tracking-Quality-History — vom GL-Thread inkrementiert, von UI-Thread in FinishCapture gelesen
    private volatile int _frameCountTracking;
    private volatile int _frameCountTotal;

    // Display-Insets für Punch-Hole (S25 Ultra hat zentrale Kamera oben!) + Nav-Bar.
    // Wird beim ersten Window-Attach ermittelt.
    private float _topInsetPx;
    private float _bottomInsetPx;

    // Multi-Sample-Count dynamisch: Highend-Geräte (Elite-Chip) können schneller samplen
    private int _effectiveMultiFrameSampleTargetCount = 10;

    // Geospatial-API (ARCore VPS): Nach Aktivierung liefert earth.GetCameraGeospatialPose()
    // globale Lat/Lon/Alt/Heading mit Accuracy. Viel präziser als Magnetometer+GPS.
    private volatile bool _geospatialActive;
    private volatile bool _geospatialEnabled; // Config war erfolgreich gesetzt
    private readonly List<float> _geospatialHorizontalAccSamples = [];
    private readonly List<float> _geospatialHeadingAccSamples = [];

    /// <summary>Immutable Snapshot der Geospatial-Pose. Cross-Thread-sicher via
    /// Volatile-Reference-Swap — ein Reader sieht entweder den alten oder den neuen
    /// Snapshot, niemals einen halben Zustand. Null = noch nichts empfangen.</summary>
    /// <param name="Altitude">VPS-Ellipsoid-Höhe der KAMERA zum Snapshot-Zeitpunkt.</param>
    /// <param name="CameraLocalY">ARCore-Welt-Y der Kamera zum SELBEN Zeitpunkt. Aus der Differenz
    /// (Altitude − CameraLocalY) ergibt sich der konstante Offset ARCore-Y → VPS-Ellipsoidhöhe,
    /// mit dem die Höhe eines beliebigen Hit-Punkts berechnet wird: <c>Altitude + (hitY − CameraLocalY)</c>.
    /// Ohne das bekäme jeder Punkt die Kamera-/Augenhöhe statt seiner Gelände-Höhe.</param>
    private sealed record GeospatialSnapshot(
        double Latitude, double Longitude, double Altitude,
        float HorizontalAccuracy, float Heading, float HeadingAccuracy,
        float CameraLocalY);

    private GeospatialSnapshot? _lastGeoSnapshot; // gelesen via Volatile.Read

    /// <summary>
    /// Punkte aus Recovery-Restore, die einen Earth-Anchor brauchen sobald Geospatial-Tracking
    /// aktiv ist (Plan Kap. 3.3). ARCore-Anchors überleben den Prozesstod nicht — der alte
    /// AnchorId ist beim Restart tot. Wir nutzen die persistierten Geo-Koordinaten um sie neu
    /// zu erzeugen. Wird in TryRestoreRecoveryState befüllt, in ReattachPendingEarthAnchors
    /// im OnDrawFrame abgearbeitet.
    /// </summary>
    private readonly List<ArPoint> _pendingEarthAnchorRestore = [];
    private readonly object _pendingRestoreLock = new();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Statisches _lastResult kann von einer vorherigen Session stammen
        // (z.B. Process-Kill ohne ConsumeLastResult-Aufruf). Defensiv null-en.
        lock (_lastResultLock) _lastResult = null;
        _finished = 0;

        _sessionStart = DateTime.UtcNow;

        // Vibrator für Haptic-Feedback (Samsung-optimierte Predefined-Effects auf API 29+).
        // Ab API 31 ist Context.VibratorService deprecated → über VibratorManager beziehen.
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var vibratorManager = GetSystemService(VibratorManagerService)
                as global::Android.OS.VibratorManager;
            _vibrator = vibratorManager?.DefaultVibrator;
        }
        else
        {
            _vibrator = GetSystemService(VibratorService) as global::Android.OS.Vibrator;
        }

        // MediaActionSound + Settings-Flag laden. Sound wird beim Punkt-Setzen + Kontur-Schließen
        // gespielt, lässt sich via SharedPreferences "ar.sound.enabled" abschalten.
        var soundPrefs = GetSharedPreferences("smartmeasure_ar", FileCreationMode.Private);
        _soundEnabled = soundPrefs?.GetBoolean("ar.sound.enabled", true) ?? true;
        _coachMarksShown = soundPrefs?.GetBoolean("ar.coachmarks.shown", false) ?? false;
        // Plan-Kap. 5.15: Quality-Heatmap-Toggle (Default: aus, weil visuell aufdringlich)
        _heatmapEnabled = soundPrefs?.GetBoolean("ar.heatmap.enabled", false) ?? false;

        // Stabilitäts-Monitor für präzise Punkt-Erfassung
        _stabilityMonitor = new ArStabilityMonitor(this);

        // Lokalisierte Overlay-Labels einmalig laden (Plan-Kap. 4.11). Sprachwechsel
        // mid-session passieren nicht — die AR-Activity laeuft als Modal-Fullscreen.
        _overlayLabels = LoadLocalizedLabels();

        // Stakeout-Targets aus statischer Bruecke uebernehmen (Plan-Kap. 5.9).
        lock (_stakeoutTargetsLock) _stakeoutTargets = _pendingStakeoutTargets;
        // Site-Points aus statischer Bruecke uebernehmen (Plan-Kap. 5.2).
        lock (_sitePointsLock) _sitePoints = _pendingSitePoints;
        // Referenz-Marker aus statischer Bruecke uebernehmen (Plan-Kap. 5.7).
        lock (_markerLock) _referenceMarkers = _pendingMarkers;
        if (_referenceMarkers != null)
        {
            foreach (var m in _referenceMarkers)
                _markersByImageName[m.ImageAssetName] = m;
        }

        // Sample-Count an Gerät anpassen — auf leistungsstarken Chips mehr Samples
        // für höhere Präzision innerhalb der 800ms-Timeout
        _effectiveMultiFrameSampleTargetCount = IsHighEndDevice() ? 15 : 10;

        // Plan 3.1 RTK-AR-Fusion: BleService aus App.Services holen. Wenn verfügbar UND
        // ein Stab mit RTK-Fix verbunden ist, nutzen wir die RTK-Position als GPS-Anker
        // (±2 cm statt ±5 m vom Handy-GPS) und überspringen den Android-LocationManager-Pfad.
        try { _bleService = App.Services?.GetService<IBleService>(); }
        catch { /* Mock-Pfad ohne DI: harmlos */ }

        var rtkUsed = TryUseRtkAsGpsAnchor();
        if (!rtkUsed)
        {
            // Sensordaten beim Start erfassen (Multi-Sample-Averaging läuft parallel über 2-5s)
            CaptureGpsPosition();
        }
        CaptureSensorData();

        // Session-Recovery: Falls vorhandene temp-Daten aus abgestürzter Session → wiederherstellen
        TryRestoreRecoveryState();

        // Root-Layout: FrameLayout (Schichten uebereinander)
        var rootLayout = new FrameLayout(this)
        {
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };

        // Schicht 1: GLSurfaceView (ARCore Kamera-Preview + OpenGL).
        // OpenGL ES 3.0 statt 2.0 — deutlich bessere Performance auf Snapdragon 8 Elite
        // (NPU + GPU-Shader-Compilation-Caching). Wenn 3.0 nicht verfügbar fällt Android
        // automatisch auf 2.0 zurück.
        _glSurfaceView = new GLSurfaceView(this);
        _glSurfaceView.PreserveEGLContextOnPause = true;
        _glSurfaceView.SetEGLContextClientVersion(3);
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

    // Toolbar-Buttons mit Mode-Highlight. Maßband/Abstecken/Tachymeter sitzen im "Mehr"-Menue.
    private Button? _btnPoint;
    private Button? _btnContour;
    private Button? _btnMore;

    // Toolbar-Referenz für Nav-Bar-Safe-Area-Update bei OnApplyWindowInsets
    private HorizontalScrollView? _toolbarScrollView;

    // Aktueller Kontur-Typ für Garten-Elemente (wird beim "Neue Linie" gewählt)
    private global::SmartMeasure.Shared.Models.ArContourType _currentContourType
        = global::SmartMeasure.Shared.Models.ArContourType.Grenze;

    private void CreateToolbar(FrameLayout root)
    {
        var density = Resources!.DisplayMetrics!.Density;

        // Toolbar: HorizontalScrollView damit alle Buttons auf schmalen Bildschirmen erreichbar sind
        var scrollView = new HorizontalScrollView(this)
        {
            HorizontalScrollBarEnabled = false,
            FillViewport = true
        };
        // Höhe 80dp (war 64dp) wegen größeren Buttons + Padding. Bottom-Margin wird
        // später in OnApplyWindowInsets auf _bottomInsetPx gesetzt — sonst überlappt
        // die System-Navigation-Bar des S25 Ultra die Toolbar.
        var scrollParams = new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            (int)(80 * density))
        {
            Gravity = GravityFlags.Bottom
        };
        scrollView.LayoutParameters = scrollParams;
        // Toolbar-BG satter (war ARGB 200, 0, 0, 0 — bei sonnigem Garten kaum lesbar).
        // ARGB 235, 18, 18, 28 = dichtes Dunkelblau-Schwarz mit minimaler Transparenz.
        scrollView.SetBackgroundColor(Color.Argb(235, 18, 18, 28));
        _toolbarScrollView = scrollView;

        // Subtile Trennlinie oben am Toolbar — markiert die Grenze zur Kamera-Sicht.
        var topBorder = new View(this);
        topBorder.SetBackgroundColor(Color.Argb(80, 255, 255, 255));
        var borderParams = new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            (int)(1f * density))
        {
            Gravity = GravityFlags.Bottom,
            BottomMargin = (int)(80 * density),
        };
        topBorder.LayoutParameters = borderParams;
        root.AddView(topBorder);

        var toolbar = new LinearLayout(this)
        {
            Orientation = global::Android.Widget.Orientation.Horizontal,
        };
        toolbar.SetGravity(GravityFlags.CenterVertical);
        toolbar.SetPadding((int)(4 * density), (int)(6 * density), (int)(4 * density), (int)(6 * density));

        // Toolbar-Buttons — Haptic + Gartenplanungs-Workflow:
        // Linie-Button öffnet Typ-Auswahl-Dialog (Weg/Beet/Mauer/Zaun/Rasen/Terrasse/Grenze)
        // Bei Typ-Auswahl wird aktive Kontur automatisch abgeschlossen + neue vom Typ gestartet.
        _btnPoint = AddToolbarButton(toolbar, Resource.Drawable.ic_ar_point, "Punkt", density,
            () => { VibrateLight(); SetMode(CaptureMode.Point); },
            tooltip: "Einzelne Messpunkte setzen", isActive: true);
        _btnContour = AddToolbarButton(toolbar, Resource.Drawable.ic_ar_area, "Fläche", density,
            () => { VibrateLight(); ShowContourTypeDialog(); },
            tooltip: "Neue Kontur (Weg/Beet/Mauer/...) beginnen");
        AddToolbarButton(toolbar, Resource.Drawable.ic_ar_close, "Schließen", density,
            () => { VibrateMedium(); CloseActiveContour(); },
            tooltip: "Aktive Kontur schließen (mind. 3 Punkte)");
        AddToolbarButton(toolbar, Resource.Drawable.ic_ar_undo, "Zurück", density,
            () => { VibrateLight(); Undo(); },
            tooltip: "Letzte Aktion rückgängig");
        AddToolbarButton(toolbar, Resource.Drawable.ic_ar_redo, "Vor", density,
            () => { VibrateLight(); Redo(); },
            tooltip: "Aktion wiederholen");
        _btnMore = AddToolbarButton(toolbar, Resource.Drawable.ic_ar_more, "Mehr", density,
            () => { VibrateLight(); ShowMoreMenu(_btnMore!); },
            tooltip: "Weitere Werkzeuge (Maßband, Tachymeter, Aufnahme, Hilfe)");
        AddToolbarButton(toolbar, Resource.Drawable.ic_ar_done, "Fertig", density,
            ConfirmFinishCapture,
            tooltip: "Aufnahme beenden und Punkte übertragen");

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
        backButton.Click += (_, _) => ConfirmDiscardAndExit();
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

    /// <summary>Toolbar-Button mit Vektor-Icon (oben) + kurzem Label (darunter). Ersetzt die
    /// fruehere Emoji-/Unicode-Beschriftung durch saubere VectorDrawables (Material-Icons).</summary>
    private Button AddToolbarButton(LinearLayout toolbar, int iconResId, string label, float density,
        Action onClick, string? tooltip = null, bool isActive = false)
    {
        var button = new Button(this)
        {
            Text = label,
            TextSize = 11, // kompaktes Label unter dem Icon
        };
        button.SetAllCaps(false);
        button.SetTextColor(Color.White);
        // Icon ueber dem Label (CompoundDrawable top), durchgaengig in Weiss getoent.
        button.SetCompoundDrawablesWithIntrinsicBounds(0, iconResId, 0, 0);
        button.CompoundDrawableTintList = global::Android.Content.Res.ColorStateList.ValueOf(Color.White);
        button.CompoundDrawablePadding = (int)(2 * density);
        button.SetBackgroundColor(isActive
            ? Color.Argb(220, 255, 107, 0)
            : Color.Argb(80, 255, 255, 255));
        button.SetPadding((int)(12 * density), (int)(4 * density), (int)(12 * density), (int)(4 * density));
        // Touch-Target-Mindestbreite 48dp (Accessibility) — kurze Labels wie "Vor"/"Mehr"
        // ergäben sonst < 48dp breite Tap-Ziele.
        button.SetMinimumWidth((int)(48 * density));
        button.SetMinWidth((int)(48 * density));
        // Höhe 64dp — Platz fuer Icon + Label, gut treffbar auf dem 6.9"-Display
        var lp = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent, (int)(64 * density))
        {
            LeftMargin = (int)(4 * density),
            RightMargin = (int)(4 * density)
        };
        button.LayoutParameters = lp;
        button.Click += (_, _) => onClick();

        // Long-Press-Tooltip ab API 26 (= unser min-SDK). Hilft Erst-Nutzern die Icon-Buttons
        // zu verstehen ohne dass sie den Help-Dialog öffnen müssen.
        if (!string.IsNullOrEmpty(tooltip) && OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            button.TooltipText = tooltip;
        }

        toolbar.AddView(button);
        return button;
    }

    /// <summary>Overflow-Menue mit Sekundaer-/Profi-Werkzeugen — haelt die Haupt-Toolbar schlank.
    /// Maßband-Reset erscheint nur im aktiven Maßband-Modus.</summary>
    private void ShowMoreMenu(View anchor)
    {
        var menu = new global::Android.Widget.PopupMenu(this, anchor);
        const int idTape = 1, idTapeReset = 2, idStakeout = 3, idTachy = 4,
                  idDelete = 5, idScreenshot = 6, idRecord = 7, idHelp = 8;

        var popupMenu = menu.Menu!;
        popupMenu.Add(0, idTape, 0, "Maßband (Ad-hoc-Distanz)");
        if (_captureMode == CaptureMode.TapeMeasure)
            popupMenu.Add(0, idTapeReset, 0, "Maßband zurücksetzen");
        popupMenu.Add(0, idStakeout, 0, "Abstecken (Ziele finden)");
        popupMenu.Add(0, idTachy, 0, "Tachymeter (Stativ-Modus)");
        // Löschen nur aktivieren, wenn ein Punkt ausgewählt ist — sonst ist der Eintrag
        // ausgegraut statt einen toten Klick mit leiser "Kein Punkt"-Meldung zu erzeugen.
        var hasSelection = _selectedPointIndex >= 0 || _isContourPointSelected;
        popupMenu.Add(0, idDelete, 0, "Ausgewählten Punkt löschen")!.SetEnabled(hasSelection);
        popupMenu.Add(0, idScreenshot, 0, "Screenshot speichern");
        popupMenu.Add(0, idRecord, 0, _isRecording ? "Aufnahme stoppen" : "Aufnahme starten");
        popupMenu.Add(0, idHelp, 0, "Hilfe");

        menu.MenuItemClick += (_, e) =>
        {
            switch (e.Item!.ItemId)
            {
                case idTape: VibrateLight(); SetMode(CaptureMode.TapeMeasure); break;
                case idTapeReset: VibrateMedium(); ResetTapeMeasure(); break;
                case idStakeout: VibrateLight(); SetMode(CaptureMode.Stakeout); break;
                case idTachy: VibrateLight(); ToggleTotalStationMode(); break;
                case idDelete: ConfirmDeleteSelectedPoint(); break;
                case idScreenshot: TakeScreenshot(); break;
                case idRecord: ToggleRecording(); break;
                case idHelp: ShowHelpDialog(); break;
            }
        };
        menu.Show();
    }

    private void SetMode(CaptureMode mode)
    {
        // Aktive Kontur abschliessen wenn Modus wechselt — KOMPLETT unter Lock
        // damit Render-Thread nicht zwischen Add+Nullify eine inkonsistente
        // Zwischenansicht sieht.
        lock (_dataLock)
        {
            // Aktive Kontur einheitlich abschliessen: ab 3 Punkten committen (mit Bowditch +
            // Anchor-Detach + Undo-Eintrag), unter 3 Punkten verwerfen. Vorher landete jede
            // 1-2-Punkt-Kontur ungeschlossen + ohne Anchor-Detach dauerhaft in _contours.
            FinalizeOrDiscardActiveContour();

            // Transiente Rechteck-Ecken beim Modus-Wechsel verwerfen — sonst koennte eine
            // halb gesetzte Basiskante in einem anderen Modus weiterleben.
            _rectangleCorners.Clear();
        }

        _captureMode = mode;

        // Modus-Text + Button-Highlighting aktualisieren
        if (_modeText != null)
            _modeText.Text = mode switch
            {
                CaptureMode.Contour => "Modus: Linie",
                CaptureMode.Rectangle => "Modus: Rechteck",
                CaptureMode.TapeMeasure => "Modus: Maßband",
                CaptureMode.Stakeout => "Modus: Absteck",
                CaptureMode.TotalStation => "Modus: Total-Station",
                _ => "Modus: Punkt",
            };

        // Stakeout: Hint wenn keine Targets bereitstehen (Plan-Kap. 5.9)
        if (mode == CaptureMode.Stakeout)
        {
            var hasTargets = _stakeoutTargets != null && _stakeoutTargets.Count > 0;
            if (!hasTargets)
                ShowTransientHint("Keine Stakeout-Ziele — aus Stakeout-Tab oeffnen");
            // Reset Cooldown-Distanz bei Mode-Aktivierung
            _stakeoutLastDistance = double.PositiveInfinity;
        }

        // Nur die beiden Mode-Buttons der Haupt-Toolbar werden hervorgehoben. Maßband/
        // Abstecken/Tachymeter sitzen im "Mehr"-Menue — der aktive Modus steht im _modeText.
        _btnPoint?.SetBackgroundColor(mode == CaptureMode.Point
            ? Color.Argb(220, 255, 107, 0)    // Aktiv: kräftiges Orange
            : Color.Argb(80, 255, 255, 255));  // Inaktiv: dezent
        // Der "Fläche"-Button deckt sowohl Freihand-Kontur als auch Rechteck ab.
        _btnContour?.SetBackgroundColor(mode is CaptureMode.Contour or CaptureMode.Rectangle
            ? Color.Argb(220, 255, 107, 0)
            : Color.Argb(80, 255, 255, 255));
        // "Mehr"-Button hervorheben, solange ein Spezial-Modus aus dem Overflow-Menü aktiv ist
        // (Maßband/Abstecken/Tachymeter) — sonst leuchtet KEIN Button und der Nutzer merkt nicht,
        // dass er den Modus gewechselt hat (z.B. Tape-Punkte landen dann nicht im Projekt).
        _btnMore?.SetBackgroundColor(mode is CaptureMode.TapeMeasure or CaptureMode.Stakeout or CaptureMode.TotalStation
            ? Color.Argb(220, 255, 107, 0)
            : Color.Argb(80, 255, 255, 255));
    }

    /// <summary>
    /// Aktive Kontur schließen + Bowditch-Correction anwenden.
    /// Der Rundungsfehler zwischen letztem und erstem Punkt wird proportional zur
    /// zurückgelegten Distanz auf alle Zwischenpunkte verteilt (klassische Vermessungs-Technik).
    ///
    /// WICHTIG: Nach Bowditch werden die Anchors der korrigierten Punkte detacht,
    /// damit RefreshAllAnchors die Korrektur im nächsten Frame NICHT überschreibt.
    /// Die Punkte werden dadurch static (nicht mehr drift-korrigiert), aber das ist OK —
    /// eine geschlossene Kontur ist per Definition eine fertige Messung.
    /// </summary>
    /// <summary>
    /// Gemeinsame Abschluss-Logik einer aktiven Kontur (Voraussetzung: &gt;= 3 Punkte, wird vom
    /// Aufrufer geprüft). Detacht die Anchors (sonst überschreibt RefreshAllAnchors die
    /// Bowditch-Korrektur), wendet Bowditch an, verschiebt die Kontur nach <see cref="_contours"/>
    /// und pusht GENAU EINE undobare <see cref="AddContourAction"/>. Die Einzel-Punkt-Actions der
    /// Kontur werden durch den <c>IsClosed</c>-Schutz in <see cref="AddContourPointAction"/> /
    /// <see cref="DeleteContourPointAction"/> neutralisiert — so kann ein Undo nach dem Schließen
    /// die fertige, Bowditch-korrigierte Fläche nicht mehr korrumpieren.
    /// MUSS unter <see cref="_dataLock"/> laufen. Liefert das Bowditch-Ergebnis (für eine
    /// Schlussfehler-Warnung) und die committete Kontur via <paramref name="committed"/>.
    /// </summary>
    private ArMathHelpers.BowditchResult CommitActiveContour(out ArContour committed)
    {
        var contour = _activeContour!;
        contour.IsClosed = true;
        foreach (var p in contour.Points)
        {
            if (!string.IsNullOrEmpty(p.AnchorId))
            {
                _anchorManager.Detach(p.AnchorId);
                p.AnchorId = null;
            }
        }
        var bowditch = ArPrecisionHelpers.ApplyBowditchCorrection(contour);
        _contours.Add(contour);
        _activeContour = null;
        _undoStack.Push(new AddContourAction(_dataLock, _contours, contour));
        _redoStack.Clear();
        committed = contour;
        return bowditch;
    }

    /// <summary>
    /// Schließt die aktive Kontur beim Verlassen des Kontur-Modus einheitlich ab: ab 3 Punkten
    /// via <see cref="CommitActiveContour"/> (mit Anchor-Detach, Bowditch, Undo-Eintrag), eine
    /// unfertige Linie (&lt; 3 Punkte) wird verworfen — Anchors freigegeben, mit Hinweis, wenn
    /// schon Punkte gesetzt waren (vorher stiller Datenverlust bzw. ungeschlossener Müll-Eintrag).
    /// MUSS unter <see cref="_dataLock"/> laufen.
    /// </summary>
    private void FinalizeOrDiscardActiveContour()
    {
        if (_activeContour == null) return;

        if (_activeContour.Points.Count >= 3)
        {
            var bowditch = CommitActiveContour(out _);
            if (bowditch == ArMathHelpers.BowditchResult.TooLarge)
                ShowTransientHint("Großer Schlussfehler — Kontur prüfen (Tracking-Drift?)");
            return;
        }

        var discarded = _activeContour.Points.Count;
        foreach (var p in _activeContour.Points)
        {
            if (!string.IsNullOrEmpty(p.AnchorId))
            {
                _anchorManager.Detach(p.AnchorId);
                p.AnchorId = null;
            }
        }
        _activeContour = null;
        if (discarded > 0)
            ShowTransientHint($"Unfertige Linie ({discarded} {(discarded == 1 ? "Punkt" : "Punkte")}) verworfen");
    }

    private void CloseActiveContour()
    {
        ArContour? closedContour = null;
        var bowditch = ArMathHelpers.BowditchResult.Degenerate;
        var tooFew = false;
        lock (_dataLock)
        {
            if (_activeContour == null || _activeContour.Points.Count < 3)
            {
                // Bewusst NICHT verwerfen — der Schließen-Button bei zu wenigen Punkten soll
                // den User nur hinweisen, damit er weiterzeichnen kann.
                tooFew = true;
            }
            else
            {
                bowditch = CommitActiveContour(out closedContour);
            }
        }

        if (tooFew)
        {
            ShowTransientHint("Mindestens 3 Punkte zum Schließen nötig");
            return;
        }

        if (closedContour != null)
        {
            var pts = closedContour.Points.Count;
            var area = closedContour.CalculateArea();
            var typeLabel = ContourTypeOptions.FirstOrDefault(o => o.Type == closedContour.ContourType).Label
                ?? closedContour.ContourType.ToString();
            ShowTransientHint(bowditch == ArMathHelpers.BowditchResult.TooLarge
                ? $"{typeLabel}: {pts} Punkte — großer Schlussfehler, bitte prüfen!"
                : $"{typeLabel}: {pts} Punkte, {area:F1} m²");
        }

        UpdateCounter();
        _overlayView?.Invalidate();

        if (_modeText != null)
            _modeText.Text = "Kontur geschlossen";
    }

    /// <summary>
    /// Verarbeitet einen gesetzten Eckpunkt im Rechteck-Modus. MUSS unter <see cref="_dataLock"/>
    /// aufgerufen werden (aus dem FinalizeSampling-UI-Thread-Block).
    ///
    /// Erste zwei Tipps sammeln die Basiskante in <see cref="_rectangleCorners"/>; der dritte
    /// legt die Tiefe fest und schliesst via <see cref="ArRectangleBuilder"/> ein exakt
    /// rechtwinkliges Rechteck (bzw. Quadrat per Snap) als geschlossene <see cref="ArContour"/> ab.
    /// </summary>
    private void HandleRectangleCornerPlaced(ArPoint corner)
    {
        // Phase 1: Basiskante sammeln (Ecke 1 + 2)
        if (_rectangleCorners.Count < 2)
        {
            _rectangleCorners.Add(corner);
            CapturePhotoForPoint(corner);
            ShowTransientHint(_rectangleCorners.Count == 1
                ? "Ecke 1/2 gesetzt — zweite Ecke der Basiskante anvisieren"
                : "Basiskante steht — jetzt die Tiefe anvisieren und tippen");
            return;
        }

        // Phase 2: dritter Tipp = Tiefe → rechtwinkliges Rechteck konstruieren
        var result = ArRectangleBuilder.Compute(
            _rectangleCorners[0], _rectangleCorners[1], corner, RectangleSquareSnapEnabled);
        if (result == null)
        {
            ShowTransientHint("Rechteck zu flach — Tiefe deutlicher anvisieren");
            VibrateWarning();
            return;
        }

        // Original-Messecken (mit Anchor) wiederverwenden; Anchors detachen, damit
        // RefreshAllAnchors die konstruierte, starre Rechteck-Form nicht verzieht
        // (analog CloseActiveContour — eine fertige Flaeche ist eine fertige Messung).
        var c0 = _rectangleCorners[0];
        var c1 = _rectangleCorners[1];
        foreach (var p in new[] { c0, c1 })
        {
            if (!string.IsNullOrEmpty(p.AnchorId))
            {
                _anchorManager.Detach(p.AnchorId);
                p.AnchorId = null;
            }
        }

        // Position der Messecken exakt auf die Ergebnis-Ebene setzen (no-op bei flachem
        // Boden, korrigiert minimale Hoehen-Inkonsistenz bei Neigung).
        c0.X = result.Corners[0].X; c0.Y = result.Corners[0].Y; c0.Z = result.Corners[0].Z;
        c1.X = result.Corners[1].X; c1.Y = result.Corners[1].Y; c1.Z = result.Corners[1].Z;

        // Konstruierte Gegenecken: Confidence/HitQuality konservativ aus den Messpunkten ableiten.
        var avgConfidence = (c0.Confidence + c1.Confidence + corner.Confidence) / 3f;
        var minHitQuality = Math.Min(c0.HitQuality, Math.Min(c1.HitQuality, corner.HitQuality));
        ArPoint MakeCorner(ArRectangleBuilder.Corner src) => new()
        {
            X = src.X,
            Y = src.Y,
            Z = src.Z,
            Confidence = avgConfidence,
            HitQuality = minHitQuality,
            Timestamp = DateTime.UtcNow,
            MagAccuracyAtCapture = corner.MagAccuracyAtCapture,
        };
        var c2 = MakeCorner(result.Corners[2]);
        var c3 = MakeCorner(result.Corners[3]);

        var rectangle = new ArContour
        {
            ContourType = _currentContourType,
            IsClosed = true,
            Points = [c0, c1, c2, c3],
        };
        _contours.Add(rectangle);
        _undoStack.Push(new AddContourAction(_dataLock, _contours, rectangle));
        _redoStack.Clear();
        _rectangleCorners.Clear();

        var typeLabel = ContourTypeOptions.FirstOrDefault(o => o.Type == _currentContourType).Label
            ?? _currentContourType.ToString();
        var shape = result.IsSquare ? "Quadrat" : "Rechteck";
        ShowTransientHint(
            $"{shape} {typeLabel}: {result.LengthMeters:F2} × {result.DepthMeters:F2} m, {result.AreaMeters:F1} m²");
        VibrateMedium();
    }

    private void FinishCapture()
    {
        // Doppel-Submit-Guard: Back-Button + Toolbar-Fertig duerfen nicht doppelt feuern.
        if (System.Threading.Interlocked.Exchange(ref _finished, 1) != 0) return;

        // Aktive Kontur abschliessen UND Snapshot — alles unter EINEM Lock,
        // damit Render-Thread keinen halben Zustand sieht.
        List<ArPoint> pointsCopy;
        List<ArContour> contoursCopy;
        lock (_dataLock)
        {
            // Aktive Kontur einheitlich abschliessen: ab 3 Punkten committen, sonst verwerfen.
            // Vorher landete eine angefangene 1-2-Punkt-Linie ungefiltert im Ergebnis.
            FinalizeOrDiscardActiveContour();

            // Eine halb gesetzte Rechteck-Basiskante ist unfertig → verwerfen (analog SetMode).
            _rectangleCorners.Clear();

            pointsCopy = new List<ArPoint>(_points);
            contoursCopy = new List<ArContour>(_contours);
        }

        var trackingRatio = _frameCountTotal > 0
            ? (float)_frameCountTracking / _frameCountTotal
            : 1f;
        var finalQualityScore = ArPrecisionHelpers.ComputeTrackingQualityScore(
            isTracking: true,
            planeCount: 5, // Näherung
            stabilityScore: _stabilityMonitor?.StabilityScore ?? 1f,
            magAccuracy: _magneticAccuracy,
            anchorCount: _anchorManager.CountTracking(),
            avgPositionStdDev: GetAverageStdDev());

        var (geoHAcc, geoHeadingAcc) = GetGeospatialMedianAccuracy();

        // Bevorzuge Geospatial-Werte (VPS) über rohe GPS/Magnetometer wenn verfügbar.
        // Snapshot atomar lesen — Cross-Thread-Sicherheit gegen GL-Thread-Update.
        var geoSnap = System.Threading.Volatile.Read(ref _lastGeoSnapshot);
        // GPS-Anker atomar als EINEN konsistenten Fix lesen (mehrere BLE-/Location-Writer).
        double? gpsLat, gpsLon, gpsAlt;
        float? gpsAcc;
        int rtkFix;
        ArGpsSource gpsSource;
        lock (_gpsLock)
        {
            gpsLat = _gpsLatitude;
            gpsLon = _gpsLongitude;
            gpsAlt = _gpsAltitude;
            gpsAcc = _gpsAccuracy;
            rtkFix = _rtkFixQuality;
            gpsSource = _gpsSource;
        }
        var finalLat = geoSnap?.Latitude ?? gpsLat;
        var finalLon = geoSnap?.Longitude ?? gpsLon;
        var finalAlt = geoSnap?.Altitude ?? gpsAlt;
        var finalGpsAcc = _geospatialActive && geoHAcc.HasValue
            ? geoHAcc.Value
            : gpsAcc;
        var finalHeading = _geospatialActive && geoSnap != null
            ? geoSnap.Heading
            : _magneticHeading;

        var captureResult = new ArCaptureResult
        {
            Points = pointsCopy,
            Contours = contoursCopy,
            GpsLatitude = finalLat,
            GpsLongitude = finalLon,
            GpsAltitude = finalAlt,
            GpsAccuracy = finalGpsAcc,
            MagneticHeading = finalHeading,
            BarometricAltitude = _barometricAltitude,
            StartedAt = _sessionStart,
            SessionDuration = DateTime.UtcNow - _sessionStart,
            GroundPlaneY = GetGroundPlaneY(),
            TrackingQualityScore = finalQualityScore,
            TrackingContinuityRatio = trackingRatio,
            GeospatialActive = _geospatialActive,
            GeospatialHorizontalAccuracy = geoHAcc,
            GeospatialHeadingAccuracy = geoHeadingAcc,
            // Plan 3.1: GPS-Source + RTK-FixQuality für Accuracy-Berechnung in ArTransferService
            GpsSource = gpsSource == ArGpsSource.None && finalLat.HasValue
                ? ArGpsSource.AndroidLocation
                : gpsSource,
            RtkFixQuality = rtkFix,
        };
        lock (_lastResultLock) _lastResult = captureResult;

        // Session war erfolgreich abgeschlossen — Recovery-State aufräumen
        ClearRecoveryState();

        SetResult(Result.Ok, new Intent());
        Finish();
    }

    /// <summary>
    /// Plan Kap. 4.4: Bei Display-Rotation mid-sampling den aktiven Sampler abbrechen.
    /// Die _sampleTargetX/Y-Koordinaten zeigen sonst nach dem Rotate auf eine ganz andere
    /// Stelle im Bild → die nachfolgenden Samples landen woanders als der erste.
    /// </summary>
    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);

        bool wasSampling;
        lock (_samplerLock)
        {
            wasSampling = _activeSampler != null;
            _activeSampler = null;
            _samplesCollected = 0;
            _samplerActiveFrames = 0;
            _samplerPauseFrames = 0;
        }
        if (wasSampling)
        {
            RunOnUiThread(() =>
            {
                ShowTransientHint("Rotation erkannt — bitte Punkt erneut anvisieren");
                VibrateWarning();
            });
        }
    }

    /// <summary>Back-Button → Cancel (kein Result). Konsument bekommt
    /// <see cref="Result.Canceled"/> und kein altes <see cref="_lastResult"/>.</summary>
    // OnBackPressed ist ab API 33 deprecated (OnBackInvokedCallback) — die AR-Activity ist
    // eine native AppCompatActivity, ein Umstieg auf OnBackInvokedDispatcher waere ein eigenes Feature.
#pragma warning disable CA1422
    public override void OnBackPressed()
    {
        // Statt sofort abzubrechen: bei erfassten Daten erst nachfragen (verhindert Datenverlust
        // durch versehentliche Back-Geste). Ohne Daten schließt ConfirmDiscardAndExit direkt.
        // KEIN base.OnBackPressed() — sonst würde die Activity trotz Dialog sofort schließen.
        ConfirmDiscardAndExit();
    }
#pragma warning restore CA1422

    private void UpdateCounter()
    {
        // _contours.Sum iteriert die Liste — ohne Lock kann ein parallel laufender
        // Add InvalidOperationException werfen.
        int total;
        lock (_dataLock)
        {
            total = _points.Count + _contours.Sum(c => c.Points.Count)
                + (_activeContour?.Points.Count ?? 0);
        }
        if (_counterText != null)
            _counterText.Text = $"Punkte: {total}";
    }

    #region Touch → Auswahl / Drag / Hit-Test

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e == null) return base.OnTouchEvent(e);

        var density = Resources!.DisplayMetrics!.Density;

        // Toolbar-Bereich ignorieren (View-Höhe statt DisplayMetrics, korrekt bei Fullscreen).
        // Die Toolbar belegt 80dp + Navigations-Inset (nicht nur 64dp) — sonst setzt ein Tap auf
        // den oberen Button-Rand gleichzeitig einen Geister-Messpunkt im Hintergrund.
        var toolbarZone = 80 * density + _bottomInsetPx;
        var viewHeight = _glSurfaceView?.Height ?? Resources.DisplayMetrics!.HeightPixels;
        if (e.GetY() > viewHeight - toolbarZone)
            return base.OnTouchEvent(e);

        // Top-Safe-Zone: der obere Streifen enthält nur UI-Chrome (Zurück-Button, Zähler,
        // Modus-Text, Nordpfeil) — ein Tap dort darf keinen Messpunkt setzen. Badge + Nordpfeil
        // werden zusätzlich gezielt in HandleTouchDown abgefangen.
        if (e.GetY() < _topInsetPx + 56 * density)
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
        _dragStartAnchorId = null;

        // Readiness-Badge tap → Detail-Panel öffnen. VOR der normalen Punkt-Selection,
        // damit ein versehentlicher Punkt-Set im Badge-Bereich nicht passiert.
        if (_overlayView != null
            && _overlayView.ReadinessBadgeBounds.Contains(_touchDownX, _touchDownY))
        {
            ShowReadinessDetailDialog();
            return true;
        }

        // Plan Kap. 4.13: Nordpfeil-Tap → Kompass-Kalibrierungs-Hint. Gleiche
        // Pre-Empt-Position wie Readiness-Badge — verhindert Punkt-Setzen oberhalb des
        // Nordpfeils. ShowCompassCalibrationHint liegt in ArCaptureActivity.Dialogs.cs.
        if (_overlayView != null
            && _overlayView.NorthArrowBounds.Contains(_touchDownX, _touchDownY))
        {
            ShowCompassCalibrationHint();
            return true;
        }

        // Pruefen ob ein existierender Punkt (Einzel oder Kontur) getroffen wurde
        var selectRadius = SELECT_RADIUS_DP * density;
        FindNearestProjectedPoint(_touchDownX, _touchDownY, selectRadius);

        // Startposition + Anchor merken fuer Undo (Anchor wird beim Verschieben überschrieben).
        var selectedPoint = GetSelectedArPoint();
        if (selectedPoint != null)
        {
            _dragStartPos = (selectedPoint.X, selectedPoint.Y, selectedPoint.Z);
            _dragStartAnchorId = selectedPoint.AnchorId;
        }

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
                // Neuen Punkt am CROSSHAIR (Bildmitte) setzen, NICHT an der Tap-Position — so wird
                // exakt der Punkt gemessen, dessen Live-Distanz/Tiefe das Reticle anzeigt. Der Tap
                // ist nur der Auslöser; anvisiert wird mit dem Crosshair (Standard in AR-Mess-Apps).
                PlaceNewPoint(_viewportWidth / 2f, _viewportHeight / 2f);
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
                _undoStack.Push(new MovePointAction(p, oldX, oldY, oldZ, p.X, p.Y, p.Z,
                    _dragStartAnchorId, p.AnchorId));
                _redoStack.Clear();
            }
        }

        _isDragging = false;
        _dragStartPos = null;
        _dragStartAnchorId = null;
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

    /// <summary>
    /// Placiert einen Punkt mit Multi-Frame-Averaging, Stability-Check und Anchor-Erstellung.
    ///
    /// Ablauf:
    /// 1. Stability-Check — bei starker Bewegung Warnung und Abbruch
    /// 2. Start eines Sampling-Fensters (500-800ms, bis zu 10 Samples)
    /// 3. Pro Frame im Sampling-Fenster: HitTest am Target-Pixel, Sample speichern
    /// 4. Nach Abschluss: robuster Median, Outlier-Filter, Anchor erzeugen
    /// 5. ArPoint mit echter Confidence (HitQuality + Sample-Variance + Stability + Anchor)
    /// </summary>
    private void PlaceNewPoint(float screenX, float screenY)
    {
        // Plan-Kap. 5.17: Total-Station-Modus umgeht den Sampling-Pfad — Distanz
        // kommt direkt aus der Depth-API + Stativ-Origin, kein Multi-Frame-Averaging.
        if (_captureMode == CaptureMode.TotalStation)
        {
            var tsPoint = PlaceTotalStationPoint(screenX, screenY);
            if (tsPoint == null) return;
            RunOnUiThread(() =>
            {
                lock (_dataLock)
                {
                    // Undo-Eintrag wie im Point-Modus — sonst nimmt "Zurück" den falschen
                    // Punkt zurück und der Tachymeter-Punkt bleibt unentfernbar.
                    _undoStack.Push(new AddPointAction(_dataLock, _points, tsPoint));
                    _redoStack.Clear();
                    _points.Add(tsPoint);
                }
                ShowTransientHint($"Punkt {_points.Count} stationiert " +
                    $"({tsPoint.GeoLatitude:F6}, {tsPoint.GeoLongitude:F6})");
                VibrateLight();
                _overlayView?.Invalidate();
                SaveRecoveryState();
            });
            return;
        }

        // Bereits ein Sample-Fenster aktiv? Ignorieren.
        lock (_samplerLock)
        {
            if (_activeSampler != null)
            {
                // Reject-Haptik: der Hint kann bei stockendem Frame-Push untergehen, die
                // Vibration signalisiert dem zügig tippenden Vermesser "noch nicht".
                ShowTransientHint("Sample läuft, bitte kurz warten...");
                HapticReject();
                return;
            }
        }

        // Pre-Mess-Validation: alle Präzisions-Voraussetzungen prüfen
        var (ready, checkList) = ValidatePreMeasureConditions();
        if (!ready)
        {
            RunOnUiThread(() =>
            {
                Toast.MakeText(this, $"Messung noch nicht bereit: {checkList}",
                    ToastLength.Long)?.Show();
                ShowTransientHint($"{checkList}");
            });
            VibrateWarning();
            return;
        }

        // Quick-Test: Gibt es überhaupt einen Hit am Target-Pixel?
        // UI-Thread → Frame-Op auf GL-Thread queuen.
        var probe = HitTestAtFromUi(screenX, screenY);
        if (probe == null)
        {
            RunOnUiThread(() =>
            {
                Toast.MakeText(this, "Keine Fläche erkannt — Kamera langsam bewegen",
                    ToastLength.Short)?.Show();
                ShowTransientHint("Keine Fläche");
            });
            return;
        }

        // Plan-Kap. 3.5 / 5.10: Sky-Pixel-Filter. Wenn Scene-Semantics aktiv ist und
        // der Hit auf einen Sky-Pixel faellt, lehnen wir die Messung ab — Instant-
        // Placement liefert sonst eine 1.5m-Schaetzung obwohl der echte Hit Kilometer
        // entfernt waere. Plane- und Point-Hits werden trotzdem akzeptiert (an reflektiven
        // Glasfassaden klassifiziert ARCore manchmal das Spiegelbild als Sky).
        if (probe.SemanticLabel == ArSemanticLabel.Sky && probe.HitQuality <= 1)
        {
            RunOnUiThread(() =>
            {
                ShowTransientHint("Himmel-Bereich — bitte einen festen Punkt anvisieren");
            });
            VibrateWarning();
            return;
        }

        // Sample-Fenster starten — atomar unter Lock
        lock (_samplerLock)
        {
            _activeSampler = new ArPoseSampler();
            _sampleTargetX = screenX;
            _sampleTargetY = screenY;
            _samplesCollected = 0;
            _sampleStartMs = Java.Lang.JavaSystem.CurrentTimeMillis();
            // Frame-Klassifikations-Counter zurücksetzen (Plan 3.2)
            _samplerActiveFrames = 0;
            _samplerPauseFrames = 0;
            AddSampleFromHit(probe); // erster Sample
        }

        ShowTransientHint("Messung läuft...");
        VibrateLight();
    }

    /// <summary>Fügt einen Sample in den aktiven Sampler ein. Muss unter _samplerLock aufgerufen werden.</summary>
    private void AddSampleFromHit(ArPoint sample)
    {
        var quality = sample.HitQuality;
        if (quality == 0)
            quality = sample.Confidence >= 0.9f ? 3 : sample.Confidence >= 0.7f ? 2 : 1;
        _activeSampler?.Add(sample.X, sample.Y, sample.Z, quality);
        _samplesCollected++;
    }

    /// <summary>
    /// Wird pro Frame aufgerufen — auch bei Tracking-Verlust! Sonst bliebe der Sampler
    /// stuck nach Tracking-Abbruch.
    /// Sammelt bis zu MultiFrameSampleTargetCount Samples, dann abschließen.
    /// Hartes Timeout nach MultiFrameSampleTimeoutMs garantiert Cleanup in jedem Fall.
    ///
    /// <paramref name="isTracking"/>: Bei false werden Pause-Frames gezählt aber kein HitTest
    /// ausgeführt. FinalizeSampling kann das Ergebnis später wegen zu vielen Aussetzern
    /// verwerfen (Plan Kap. 3.2 — "Tracking-Verlust mitten in Multi-Frame-Sampling").
    /// </summary>
    private void UpdateSamplingIfActive(bool isTracking)
    {
        float targetX, targetY;
        bool shouldFinish;
        bool samplerActive;

        lock (_samplerLock)
        {
            samplerActive = _activeSampler != null;
            if (!samplerActive) return;

            // Frame-Klassifikation: tracking vs paused
            if (isTracking) _samplerActiveFrames++;
            else _samplerPauseFrames++;

            var elapsedMs = Java.Lang.JavaSystem.CurrentTimeMillis() - _sampleStartMs;
            shouldFinish = _samplesCollected >= _effectiveMultiFrameSampleTargetCount
                          || elapsedMs >= MultiFrameSampleTimeoutMs;
            targetX = _sampleTargetX;
            targetY = _sampleTargetY;
        }

        if (!shouldFinish)
        {
            // Ohne Tracking nicht hit-testen — Frame liefert eh keinen brauchbaren Hit.
            // So vermeiden wir auch das Wegwerfen von CPU-Zyklen, die ARCore gerade für
            // Re-Localization braucht.
            if (!isTracking) return;

            // Neuen HitTest am Target-Pixel machen — kann außerhalb Lock sein,
            // HitTestAt arbeitet nur mit _lastFrame (eigener Lock)
            var sample = HitTestAt(targetX, targetY);
            if (sample != null)
            {
                lock (_samplerLock)
                {
                    if (_activeSampler != null)
                        AddSampleFromHit(sample);
                }
            }
            return;
        }

        FinalizeSampling();
    }

    /// <summary>
    /// Schließt das Sampling-Fenster ab: Median berechnen, Outlier filtern, Anchor erstellen,
    /// Punkt in Liste einfügen. Wird ausschließlich vom GL-Thread (UpdateSamplingIfActive in
    /// OnDrawFrame) aufgerufen — die Reticle-/Frame-Felder werden GL-Thread-intern gelesen.
    /// </summary>
    private void FinalizeSampling()
    {
        ArPoseSampler? sampler;
        int activeFrames, pauseFrames;
        lock (_samplerLock)
        {
            sampler = _activeSampler;
            _activeSampler = null;
            _samplesCollected = 0;
            activeFrames = _samplerActiveFrames;
            pauseFrames = _samplerPauseFrames;
            _samplerActiveFrames = 0;
            _samplerPauseFrames = 0;
        }

        // Tracking-Continuity pro Punkt: was war der Anteil an Tracking-Frames während Sampling?
        // 1.0 = perfekt durchgängig, &lt;0.5 = Sampler war mehr als die Hälfte ohne Tracking.
        var totalFrames = activeFrames + pauseFrames;
        var continuity = totalFrames > 0 ? (float)activeFrames / totalFrames : 1f;

        // Plan 3.2: Bei zu vielen Pause-Frames Punkt verwerfen — sonst kämen 2-3 Samples
        // einer früheren Tracking-Phase als "guter Punkt" durch (Confidence wäre hoch
        // wegen Anchor-Bonus + Hit-Quality, obwohl die Position auf Rest-Samples basiert).
        if (totalFrames >= 5 && continuity < 1f - MaxPauseRatio)
        {
            RunOnUiThread(() =>
            {
                ShowTransientHint("Tracking verloren — Punkt erneut anvisieren");
                VibrateWarning();
            });
            return;
        }

        // Mindest-Sample-Count: weniger Samples bedeutet zu wenig Information für robuste
        // Outlier-Detection (Plan Kap. 3.2 — min 6 statt vorher 3).
        if (sampler == null || sampler.Count < MinValidSampleCount)
        {
            RunOnUiThread(() => ShowTransientHint($"Zu wenige Samples ({sampler?.Count ?? 0}/{MinValidSampleCount}) — erneut versuchen"));
            return;
        }

        var result = sampler.ComputeRobustMedian();
        if (result == null)
        {
            RunOnUiThread(() => ShowTransientHint("Zu viel Streuung — erneut versuchen"));
            return;
        }

        var (x, y, z, stdDev, validCount, maxQuality) = result.Value;

        // Plan Kap. 3.7 Snap-Engine: Vertex- und Right-Angle-Snap auf den geschätzten Punkt.
        // Snap nur anwenden wenn nicht durch Long-Press deaktiviert (Feature für später —
        // hier immer aktiv, mit Anzeige-Feedback im Toast).
        var (snappedX, snappedY, snappedZ, snapType) = ApplySnapToHit(x, y, z);
        if (snapType != SmartMeasure.Shared.Services.ArSnapEngine.SnapType.None)
        {
            x = snappedX;
            y = snappedY;
            z = snappedZ;
        }

        // Pose an exakter Session-Position erstellen (für Anchor)
        Pose? anchorPose = null;
        try
        {
            anchorPose = Pose.MakeTranslation(x, y, z);
        }
        catch { /* Session evtl. geschlossen */ }

        var stability = _stabilityMonitor?.StabilityScore ?? 0.5f;

        // Depth-Sanity-Check: Depth-Map am Tap-Pixel gegen die Distanz Kamera → gemessener
        // Punkt vergleichen. WICHTIG: die echte Distanz zum (gesampelten) Tap-Punkt nehmen,
        // NICHT _reticleHitDistance — die gilt nur für die Bildmitte, der Tap ist aber frei
        // platzierbar. Sonst wird Depth von Pixel A gegen die Distanz von Pixel B geprüft.
        var depthMultiplier = 1.0f;
        try
        {
            Frame? frame;
            lock (_frameLock) frame = _lastFrame;
            if (frame != null && _viewportWidth > 0 && _viewportHeight > 0)
            {
                var hitDistance = _reticleHitDistance;
                var camPose = frame.Camera?.Pose;
                if (camPose != null)
                {
                    var cdx = x - camPose.Tx();
                    var cdy = y - camPose.Ty();
                    var cdz = z - camPose.Tz();
                    hitDistance = MathF.Sqrt(cdx * cdx + cdy * cdy + cdz * cdz);
                }
                if (hitDistance > 0)
                    depthMultiplier = ArPrecisionHelpers.DepthSanityMultiplier(
                        frame, _sampleTargetX, _sampleTargetY,
                        hitDistance, _viewportWidth, _viewportHeight);
            }
        }
        catch { /* Depth optional */ }

        // Confidence aus mehreren Faktoren (0-1):
        // - Hit-Quality (0.3 für Plane=3, 0.2 für Point=2, 0.1 für Instant=1)
        // - Niedrige Position-StdDev = hohe Confidence (0.3)
        // - Stability beim Capture (0.2)
        // - Depth-Sanity-Multiplikator (0.8-1.2 auf StdDev+Hit)
        // - Anchor erstellt (+0.2 am Ende)
        var hitComponent = maxQuality == 3 ? 0.3f : maxQuality == 2 ? 0.2f : 0.1f;
        var stdDevComponent = MathF.Max(0f, 0.3f - stdDev / 0.05f * 0.3f);
        var stabilityComponent = stability * 0.2f;
        var confidence = (hitComponent + stdDevComponent) * depthMultiplier + stabilityComponent;

        // Geospatial-Snapshot atomar lesen (Volatile-Reference) und einmal für ArPoint
        // UND Anchor verwenden — sonst koennte der GL-Thread mitten in einer halben
        // Pose-Aktualisierung den Snapshot wechseln.
        var geoSnapshot = System.Threading.Volatile.Read(ref _lastGeoSnapshot);

        // Geo-Position des HIT-PUNKTS (Lat/Lon/Ellipsoid-Alt) — exakt via ARCore-Earth, sonst
        // Kamera-Snapshot + ARCore-Höhenkorrektur. Vorher bekam jeder Punkt die KAMERA-Position
        // (Lat/Lon + Augenhöhe) statt seiner echten Gelände-Position → Geländemodell flach + Lage versetzt.
        var (hitGeoLat, hitGeoLon, hitGeoAlt, hitGeoHAcc) = ResolveHitGeoPose(anchorPose, y, geoSnapshot);

        // Camera-Pitch zum Capture-Zeitpunkt (Plan Kap. 4.2). Steiler Pitch ⇒ ungenauere Tiefe;
        // wird in den SurveyPoint übernommen und kann später in Berichten/Quality-Score genutzt werden.
        float capturePitchDeg = 0f;
        try
        {
            Frame? pitchFrame;
            lock (_frameLock) pitchFrame = _lastFrame;
            var cameraPose = pitchFrame?.Camera?.Pose;
            if (cameraPose != null)
                capturePitchDeg = ArPrecisionHelpers.ExtractPitchFromCameraPose(cameraPose);
        }
        catch { /* Pitch optional */ }

        // Magnetometer-Accuracy beim Capture festhalten — wandert in SurveyPoint.MagAccuracy
        // und entscheidet später ob horizontale Tilt-Korrektur sicher angewendet werden darf.
        var capturedMagAccuracy = _magneticAccuracy;

        var arPoint = new ArPoint
        {
            X = x,
            Y = y,
            Z = z,
            HitQuality = maxQuality,
            PositionStdDev = stdDev,
            SampleCount = validCount,
            Timestamp = DateTime.UtcNow,
            Confidence = Math.Clamp(confidence, 0f, 1f),

            // Geospatial-Geo-Position des HIT-PUNKTS (±1-3m via VPS), aufgelöst oben.
            GeoLatitude = hitGeoLat,
            GeoLongitude = hitGeoLon,
            GeoAltitude = hitGeoAlt,
            GeoHorizontalAccuracy = hitGeoHAcc,

            // Capture-Zeitpunkt-Metadaten (Plan Kap. 4.2)
            CameraPitchDeg = capturePitchDeg,
            MagAccuracyAtCapture = capturedMagAccuracy,

            // Pro-Punkt-Tracking-Qualität (Plan Kap. 3.2)
            SampleTrackingContinuity = continuity,
        };

        // Anchor erstellen — Earth-Anchor bevorzugen (drift-frei + persistent)
        // wenn Geospatial aktiv, sonst lokaler Session-Anchor.
        var hasAnchor = false;
        if (_geospatialActive && geoSnapshot != null && _arSession?.Earth != null)
        {
            hasAnchor = _anchorManager.TryCreateEarthAnchor(
                _arSession.Earth,
                geoSnapshot.Latitude,
                geoSnapshot.Longitude,
                geoSnapshot.Altitude,
                arPoint);
        }

        // Fallback auf lokalen Anchor
        if (!hasAnchor)
            hasAnchor = _anchorManager.TryCreateAnchor(_arSession, anchorPose, arPoint);

        if (hasAnchor)
            arPoint.Confidence = MathF.Min(1f, arPoint.Confidence + 0.2f);

        VibrateLight();
        PlayShutterSound();
        RunOnUiThread(() =>
        {
            lock (_dataLock)
            {
                if (_captureMode == CaptureMode.TapeMeasure)
                {
                    // Plan-Kap. 5.3: Tape-Punkte gehen in eigenen Buffer (kein Projekt-Save,
                    // kein Undo-Stack — Reset per Long-Press auf den Mass-Button).
                    _tapeMeasurePoints.Add(arPoint);
                    var total = ComputeTapeMeasureTotalMeters();
                    ShowTransientHint($"Punkt {_tapeMeasurePoints.Count}  ges. {total:F2} m");
                }
                else if (_captureMode == CaptureMode.Point)
                {
                    _undoStack.Push(new AddPointAction(_dataLock, _points, arPoint));
                    _redoStack.Clear();
                    _points.Add(arPoint);
                    ShowTransientHint($"Punkt {_points.Count}  Streuung {stdDev * 100:F1}cm  ({validCount} Samples)");
                    // Plan-Kap. 5.6: Foto-Annotation pro Punkt (nicht im Tape-Modus —
                    // Ad-hoc-Messung braucht kein Foto, das wuerde nur Storage fressen).
                    CapturePhotoForPoint(arPoint);
                }
                else if (_captureMode == CaptureMode.Rectangle)
                {
                    // Gefuehrtes 3-Punkt-Rechteck: erste zwei Tipps = Basiskante, dritter
                    // schliesst die rechtwinklige Flaeche ab. Laeuft unter _dataLock.
                    HandleRectangleCornerPlaced(arPoint);
                }
                else
                {
                    // Aktive Kontur mit aktuell gewähltem Typ (aus ShowContourTypeDialog)
                    _activeContour ??= new ArContour { ContourType = _currentContourType };
                    _undoStack.Push(new AddContourPointAction(_dataLock, _activeContour, arPoint));
                    _redoStack.Clear();
                    _activeContour.Points.Add(arPoint);
                    var typeLabel = ContourTypeOptions.FirstOrDefault(o => o.Type == _activeContour.ContourType).Label
                        ?? _activeContour.ContourType.ToString();
                    ShowTransientHint($"{typeLabel}: {_activeContour.Points.Count} Punkte");
                    // Foto auch fuer Kontur-Punkte (z.B. "Ecke Mauer Nord" mit Sichtbeleg).
                    CapturePhotoForPoint(arPoint);
                }
            }
            UpdateCounter();
            _overlayView?.Invalidate();

            SaveRecoveryState();
        });
    }

    private void MoveSelectedPoint(float screenX, float screenY)
    {
        var point = GetSelectedArPoint();
        if (point == null) return;

        // UI-Thread → Frame-Op auf GL-Thread queuen (Drag-Hot-Path).
        var newPos = HitTestAtFromUi(screenX, screenY);
        if (newPos == null) return;

        RunOnUiThread(() =>
        {
            // Mutation unter _dataLock: GL-Thread iteriert in ProjectPointsToScreen
            // parallel über die Listen — torn-reads (NaN, Visual-Flicker) sonst möglich.
            lock (_dataLock)
            {
                point.X = newPos.X;
                point.Y = newPos.Y;
                point.Z = newPos.Z;
                point.AnchorId = newPos.AnchorId;
            }
            _overlayView?.Invalidate();
        });
    }

    /// <summary>UI-Thread-sicherer HitTest: schedult eine Frame-Operation auf den GL-Thread
    /// und wartet mit Timeout auf das Ergebnis. ARCore-Frame.HitTest laeuft so garantiert
    /// auf dem korrekten Thread und kollidiert nicht mit Session.Update().</summary>
    /// <param name="timeoutMs">Maximal warten — Default 60ms (~3 Frames @60fps).</param>
    private ArPoint? HitTestAtFromUi(float screenX, float screenY, int timeoutMs = 60)
    {
        // Schon auf dem GL-Thread? Direkt aufrufen.
        if (_glSurfaceView?.Holder?.Surface != null
            && System.Threading.Thread.CurrentThread.ManagedThreadId == _glThreadId)
        {
            return HitTestAt(screenX, screenY);
        }

        ArPoint? result = null;
        using var done = new System.Threading.ManualResetEventSlim(false);
        Action<Frame> op = _ =>
        {
            try { result = HitTestAt(screenX, screenY); }
            // Der Caller kann nach Timeout das Event bereits disposed haben (using) — dann
            // ignorieren statt eine ObjectDisposedException im GL-Thread zu schlucken.
            finally { try { done.Set(); } catch (ObjectDisposedException) { /* Caller gab auf */ } }
        };
        lock (_pendingFrameOps) _pendingFrameOps.Add(op);

        // GL-Thread macht continuous rendering — Op wird im nächsten Frame ausgeführt.
        if (!done.Wait(timeoutMs))
        {
            // Timeout (GL-Thread-Stall oder OnPause leerte die Queue): Op zurückziehen, damit
            // ein späterer Drain nicht auf das gleich disposed Event Set() ruft.
            lock (_pendingFrameOps) _pendingFrameOps.Remove(op);
        }
        return result;
    }

    /// <summary>Ruft alle queueten Frame-Operationen auf — wird vom GL-Thread am Anfang
    /// von OnDrawFrame aufgerufen, BEVOR ein neuer Update() ein neues Frame holt.</summary>
    private void DrainPendingFrameOps(Frame frame)
    {
        List<Action<Frame>>? ops = null;
        lock (_pendingFrameOps)
        {
            if (_pendingFrameOps.Count == 0) return;
            ops = new List<Action<Frame>>(_pendingFrameOps);
            _pendingFrameOps.Clear();
        }
        foreach (var op in ops)
        {
            try { op(frame); }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("ArCapture",
                    $"PendingFrameOp fehlgeschlagen: {ex.Message}");
            }
        }
    }

    /// <summary>Thread-ID des GL-Render-Threads (in OnSurfaceCreated gesetzt).
    /// Erlaubt HitTestAtFromUi den Direkt-Aufruf wenn schon auf GL-Thread (Reentrancy-frei).</summary>
    private int _glThreadId;

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
            // 1. Plane- und Point-HitTests bevorzugen (höchste Qualität)
            var hitResults = frame.HitTest(screenX, screenY);

            HitResult? bestHit = null;
            var confidence = 0.9f;

            if (hitResults != null)
            {
                foreach (var hit in hitResults)
                {
                    var trackable = hit.Trackable;
                    if (trackable is Plane plane && plane.IsPoseInPolygon(hit.HitPose))
                    {
                        bestHit = hit;
                        confidence = 0.9f;
                        break;
                    }
                    if (trackable is Google.AR.Core.Point && bestHit == null)
                    {
                        bestHit = hit;
                        confidence = 0.7f;
                    }
                }
            }

            // 2. Instant Placement Fallback wenn kein Plane/Point getroffen.
            // Plan Kap. 3.6: Statt hardcoded 1,5 m versuchen wir zuerst das Depth-Image am
            // Touch-Pixel zu lesen. Auf S25 Ultra ist Raw-Depth verfügbar (Stereo-Sensor)
            // und liefert real-world-Distanzen ±5 % für 0,3–30 m. Bei Sky/Glanz fällt
            // TryGetDepthMeters auf null zurück und wir benutzen die alte 1,5-m-Annahme.
            if (bestHit == null && _viewportWidth > 0 && _viewportHeight > 0)
            {
                var depthMeters = ArPrecisionHelpers.TryGetDepthMeters(
                    frame, screenX, screenY, _viewportWidth, _viewportHeight);
                var instantDistance = depthMeters ?? 1.5f;

                var instantHits = frame.HitTestInstantPlacement(screenX, screenY, instantDistance);
                if (instantHits != null && instantHits.Count > 0)
                {
                    bestHit = instantHits[0];
                    // Wenn Depth-Image die Distanz lieferte, ist die Confidence höher
                    // (real-world-Tiefe statt Schätzung). 0.65 vs 0.5 für reine Annahme.
                    confidence = depthMeters.HasValue ? 0.65f : 0.5f;
                }
            }

            if (bestHit == null) return null;
            var pose = bestHit.HitPose;
            if (pose == null) return null;

            // hitQuality: 3=Plane (conf>=0.9), 2=FeaturePoint (>=0.7), 1=Instant-Placement (<0.7)
            var hitQuality = confidence >= 0.9f ? 3 : confidence >= 0.7f ? 2 : 1;

            // Plan-Kap. 3.5 / 5.10: Semantic-Label am Hit-Pixel auslesen. Sky/Water-Hits
            // werden nicht direkt verworfen (Plane-Detection war bereits erfolgreich, also
            // ist es vermutlich eine reflektive Oberflaeche und kein echter Himmel), aber
            // das Label wird mitgespeichert. PlaceNewPoint pruft den Label und kann
            // ablehnen, wenn nur Instant-Placement + Sky zusammenkommen.
            var semantic = ArSemanticLabel.None;
            if (_viewportWidth > 0 && _viewportHeight > 0)
            {
                semantic = ArPrecisionHelpers.TryGetSemanticLabel(
                    frame, screenX, screenY, _viewportWidth, _viewportHeight);
            }

            var arPoint = new ArPoint
            {
                X = pose.Tx(),
                Y = pose.Ty(),
                Z = pose.Tz(),
                Confidence = confidence,
                HitQuality = hitQuality,
                SemanticLabel = semantic,
                Timestamp = DateTime.UtcNow,
            };

            // Snap-to-Edge nur bei Plane-Hits (Instant Placement hat keine Kanten)
            if (hitQuality == 3)
                SnapToPlaneEdge(arPoint, frame);

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
                if (q == null || q.Length < 4) continue;
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
                _undoStack.Push(new DeletePointAction(_dataLock, _points, _selectedPointIndex, _points[_selectedPointIndex]));
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
                    _undoStack.Push(new DeleteContourPointAction(_dataLock, contour, _selectedContourPointIdx, point));
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

    /// <summary>
    /// Plan 3.1 RTK-AR-Fusion: Wenn ein BLE-Stab mit RTK-Fix verbunden ist, nutze dessen
    /// Position als GPS-Anker für die AR-Session. Liefert true wenn RTK aktiv übernommen
    /// wurde — der Caller überspringt dann den Android-LocationManager-Pfad. Bei false
    /// bleibt der bisherige Pfad (LocationManager) als Fallback aktiv.
    ///
    /// FixQuality: 4=RTK-Fix (cm), 5=RTK-Float (dm). Akzeptiert beides — alles besser
    /// als ±5 m Handy-GPS. DGPS/Standard-GPS (1–2) verwerfen wir, da der Android-Pfad
    /// gleichwertig ist und ohne BLE-Strom-Overhead läuft.
    ///
    /// PositionUpdated-Event wird abonniert, damit die Position über die gesamte Session
    /// aktuell bleibt (User kann den Stab bewegen während er mit dem Phone misst).
    /// </summary>
    private bool TryUseRtkAsGpsAnchor()
    {
        if (_bleService == null) return false;
        if (!_bleService.IsConnected) return false;

        var state = _bleService.GetStateSnapshot();
        if (state.FixQuality < 4) return false;
        if (!state.Latitude.HasValue || !state.Longitude.HasValue) return false;

        var lat = state.Latitude.Value;
        var lon = state.Longitude.Value;

        // StickState hat cm — wir konvertieren auf m und limitieren auf 2cm minimum.
        // RTK-Float (FixQuality=5) kann 5-30cm Accuracy haben, RTK-Fix (4) typisch 1-3cm.
        var rtkAcc = MathF.Max(state.HorizontalAccuracy / 100f, 0.02f);
        lock (_gpsLock)
        {
            _gpsLatitude = lat;
            _gpsLongitude = lon;
            _gpsAltitude = state.Altitude;
            _gpsAccuracy = rtkAcc;
            _gpsSource = ArGpsSource.RtkRover;
            _rtkFixQuality = state.FixQuality;
        }

        // GPS-Sample-Liste mit dem RTK-Wert seeden, damit FinalizeGpsAveraging nicht
        // ein leeres Sample-Set nutzt.
        lock (_gpsSamples)
        {
            _gpsSamples.Add((lat, lon, state.Altitude, rtkAcc));
        }

        // Live-Updates abonnieren (2 Hz vom Rover). User kann den Stab während der
        // Session umstecken; jede neue Position aktualisiert den Anker.
        _rtkPositionHandler = (newLat, newLon, newAlt) =>
        {
            // Läuft auf dem BLE-Thread und kann nach OnDestroy noch in-flight sein.
            if (IsDestroyed || IsFinishing) return;
            var s = _bleService.GetStateSnapshot();
            if (s.FixQuality < 4) return; // Fix verloren → keinen schlechteren Wert übernehmen

            lock (_gpsLock)
            {
                _gpsLatitude = newLat;
                _gpsLongitude = newLon;
                _gpsAltitude = newAlt;
                _gpsAccuracy = MathF.Max(s.HorizontalAccuracy / 100f, 0.02f);
                _rtkFixQuality = s.FixQuality;
            }
        };
        _bleService.PositionUpdated += _rtkPositionHandler;

        global::Android.Util.Log.Info("ArCapture",
            $"RTK-AR-Fusion aktiv: FixQuality={state.FixQuality}, ±{state.HorizontalAccuracy:F1}cm horizontal");
        return true;
    }

    /// <summary>
    /// GPS-Erfassung mit Multi-Sample-Averaging über 5 Sekunden. Vorher: einmaliger
    /// LastKnownLocation-Snapshot (kann mehrere Minuten alt sein und ±10m abweichen).
    /// Jetzt: aktive Request + Samples-Accumulation, gewichtetes Mittel nach Accuracy.
    ///
    /// Permissions: Wir akzeptieren auch Coarse-Location (Android 12+: User kann Fine
    /// verweigern aber Coarse erlauben). Bei reinem Coarse nutzen wir NetworkProvider
    /// statt GpsProvider — RequestLocationUpdates(Gps...) wirft sonst SecurityException.
    /// </summary>
    private void CaptureGpsPosition()
    {
        try
        {
            var hasFine = AndroidX.Core.Content.ContextCompat.CheckSelfPermission(this,
                    global::Android.Manifest.Permission.AccessFineLocation)
                == global::Android.Content.PM.Permission.Granted;
            var hasCoarse = AndroidX.Core.Content.ContextCompat.CheckSelfPermission(this,
                    global::Android.Manifest.Permission.AccessCoarseLocation)
                == global::Android.Content.PM.Permission.Granted;
            if (!hasFine && !hasCoarse)
            {
                global::Android.Util.Log.Warn("ArCapture", "GPS-Permission fehlt (weder Fine noch Coarse)");
                return;
            }

            var locationManager = GetSystemService(LocationService) as global::Android.Locations.LocationManager;
            if (locationManager == null) return;

            // Initialer Snapshot (Fallback falls aktive Samples nicht ankommen).
            // Coarse-only → NetworkProvider; Fine → GPS bevorzugt, Network als Fallback.
            global::Android.Locations.Location? location = null;
            if (hasFine)
            {
                location = locationManager.GetLastKnownLocation(
                    global::Android.Locations.LocationManager.GpsProvider);
            }
            location ??= locationManager.GetLastKnownLocation(
                global::Android.Locations.LocationManager.NetworkProvider);

            if (location != null)
            {
                lock (_gpsLock)
                {
                    _gpsLatitude = location.Latitude;
                    _gpsLongitude = location.Longitude;
                    _gpsAltitude = location.HasAltitude ? location.Altitude : null;
                    _gpsAccuracy = location.HasAccuracy ? location.Accuracy : null;
                }

                lock (_gpsSamples)
                {
                    _gpsSamples.Add((location.Latitude, location.Longitude,
                        location.HasAltitude ? location.Altitude : null,
                        location.HasAccuracy ? location.Accuracy : null));
                }
            }

            // Aktive Updates über 5 Sekunden sammeln. FusedLocationProvider bevorzugt
            // (Hardware-Fusion + Dual-Frequency-GNSS L1+L5 → ~±1-3m statt ±3-8m), LocationManager
            // als Fallback, falls Google Play Services fehlt.
            var usedFused = TryStartFusedLocationUpdates(hasFine);
            GpsSampleListener? legacyListener = null;
            if (!usedFused)
            {
                var provider = hasFine
                    ? global::Android.Locations.LocationManager.GpsProvider
                    : global::Android.Locations.LocationManager.NetworkProvider;
                legacyListener = new GpsSampleListener(this);
                try
                {
                    locationManager.RequestLocationUpdates(provider, 500L, 0f, legacyListener);
                    _activeLocationListeners.Add((locationManager, legacyListener));
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Warn("ArCapture",
                        $"RequestLocationUpdates({provider}) failed: {ex.Message}");
                }
            }

            // Nach 5s abmelden und Median bilden
            Window?.DecorView?.PostDelayed(() =>
            {
                if (IsFinishing || IsDestroyed) return;
                StopFusedLocationUpdates();
                if (legacyListener != null)
                {
                    try { locationManager.RemoveUpdates(legacyListener); } catch { /* OK */ }
                    _activeLocationListeners.Remove((locationManager, legacyListener));
                }
                FinalizeGpsAveraging();
            }, 5000);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"GPS nicht verfuegbar: {ex.Message}");
        }
    }

    private global::Android.Gms.Location.IFusedLocationProviderClient? _fusedLocationClient;
    private FusedGpsCallback? _fusedGpsCallback;

    /// <summary>Startet hochpräzise Standort-Updates über den FusedLocationProvider (Google Play
    /// Services). Nutzt auf dem S25 Ultra die Dual-Frequency-GNSS (L1+L5) + Sensor-Fusion.
    /// Liefert false, wenn Play Services fehlt → der Aufrufer nutzt dann den LocationManager.</summary>
    private bool TryStartFusedLocationUpdates(bool hasFine)
    {
        try
        {
            _fusedLocationClient ??= global::Android.Gms.Location.LocationServices
                .GetFusedLocationProviderClient(this);
            var priority = hasFine
                ? global::Android.Gms.Location.Priority.PriorityHighAccuracy
                : global::Android.Gms.Location.Priority.PriorityBalancedPowerAccuracy;
            var request = new global::Android.Gms.Location.LocationRequest.Builder(priority, 1000L)
                .SetMinUpdateIntervalMillis(500L)
                .Build();
            _fusedGpsCallback = new FusedGpsCallback(this);
            _fusedLocationClient.RequestLocationUpdates(request, _fusedGpsCallback,
                global::Android.OS.Looper.MainLooper);
            return true;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture",
                $"FusedLocationProvider nicht verfügbar, Fallback auf LocationManager: {ex.Message}");
            return false;
        }
    }

    /// <summary>Meldet die FusedLocationProvider-Updates ab (idempotent).</summary>
    private void StopFusedLocationUpdates()
    {
        try
        {
            if (_fusedLocationClient != null && _fusedGpsCallback != null)
                _fusedLocationClient.RemoveLocationUpdates(_fusedGpsCallback);
        }
        catch { /* OK — Client evtl. schon weg */ }
        _fusedGpsCallback = null;
    }

    /// <summary>Füttert die FusedLocationProvider-Fixes in denselben GPS-Sample-Puffer wie der
    /// LocationManager-Pfad — FinalizeGpsAveraging bildet daraus das gewichtete Mittel.</summary>
    private sealed class FusedGpsCallback(ArCaptureActivity activity)
        : global::Android.Gms.Location.LocationCallback
    {
        public override void OnLocationResult(global::Android.Gms.Location.LocationResult result)
        {
            foreach (var loc in result.Locations)
            {
                if (loc == null) continue;
                lock (activity._gpsSamples)
                {
                    activity._gpsSamples.Add((loc.Latitude, loc.Longitude,
                        loc.HasAltitude ? loc.Altitude : (double?)null,
                        loc.HasAccuracy ? loc.Accuracy : (float?)null));
                }
            }
        }
    }

    /// <summary>Magnetometer-Heading und barometrische Hoehe erfassen</summary>
    private void CaptureSensorData()
    {
        try
        {
            _sensorManager = GetSystemService(SensorService) as global::Android.Hardware.SensorManager;
            if (_sensorManager == null) return;

            // Rotation Vector (Fusion aus Gyro+Accel+Mag) fuer Heading.
            // SensorDelay.Game = ~50Hz für dichte Multi-Sample-Erfassung.
            // 5s aktiv = 250 Samples — mehr als HeadingSampleTargetCount (20), damit
            // FinalizeHeadingAveraging sicher genug Daten hat.
            var rotationSensor = _sensorManager.GetDefaultSensor(global::Android.Hardware.SensorType.RotationVector);
            if (rotationSensor != null)
            {
                var listener = new HeadingSensorListener(this);
                _sensorManager.RegisterListener(listener, rotationSensor,
                    global::Android.Hardware.SensorDelay.Game);
                _activeSensorListeners.Add(listener);

                Window?.DecorView?.PostDelayed(() =>
                {
                    if (IsFinishing || IsDestroyed) return;
                    try { _sensorManager?.UnregisterListener(listener); } catch { /* OK */ }
                    _activeSensorListeners.Remove(listener);
                }, 5000);
            }

            // Barometer fuer relative Hoehe
            var pressureSensor = _sensorManager.GetDefaultSensor(global::Android.Hardware.SensorType.Pressure);
            if (pressureSensor != null)
            {
                var listener = new PressureSensorListener(this);
                _sensorManager.RegisterListener(listener, pressureSensor,
                    global::Android.Hardware.SensorDelay.Normal);
                _activeSensorListeners.Add(listener);

                Window?.DecorView?.PostDelayed(() =>
                {
                    if (IsFinishing || IsDestroyed) return;
                    try { _sensorManager?.UnregisterListener(listener); } catch { /* OK */ }
                    _activeSensorListeners.Remove(listener);
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

            // Multi-Sample: sammeln statt direkt überschreiben
            lock (activity._headingSamples)
            {
                if (activity._headingSamples.Count < HeadingSampleTargetCount)
                    activity._headingSamples.Add(azimuthDeg);
            }

            // Direkter Set für Live-Kompass (wird später durch Median überschrieben)
            activity._magneticHeading = azimuthDeg;
        }

        public void OnAccuracyChanged(global::Android.Hardware.Sensor? sensor,
            global::Android.Hardware.SensorStatus accuracy)
        {
            // Magnetometer-Accuracy erfassen für Kompass-Kalibrierungs-Dialog + Quality-Score
            var acc = accuracy switch
            {
                global::Android.Hardware.SensorStatus.AccuracyHigh => 3,
                global::Android.Hardware.SensorStatus.AccuracyMedium => 2,
                global::Android.Hardware.SensorStatus.AccuracyLow => 1,
                _ => 0,
            };
            activity._magneticAccuracy = acc;

            // Atomar: nur der erste Übergang auf "gewarnt" zeigt den Dialog.
            if (acc < 2 && System.Threading.Interlocked.CompareExchange(
                    ref activity._lowMagAccuracyWarned, 1, 0) == 0)
            {
                activity.RunOnUiThread(() => activity.ShowCompassCalibrationHint());
            }
        }
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

                // Session erstellen — mit allen sinnvollen Features
                _arSession = new Session(this);
                var config = new Google.AR.Core.Config(_arSession);
                config.SetUpdateMode(Google.AR.Core.Config.UpdateMode.LatestCameraImage);
                config.SetPlaneFindingMode(Google.AR.Core.Config.PlaneFindingMode.HorizontalAndVertical);

                // Depth API — für präzisere Hit-Tests auf kompatiblen Geräten (Samsung S25 Ultra ja)
                if (_arSession.IsDepthModeSupported(Google.AR.Core.Config.DepthMode.Automatic))
                    config.SetDepthMode(Google.AR.Core.Config.DepthMode.Automatic);

                // Instant Placement — Hit-Test auch ohne Plane-Detection als Fallback
                config.SetInstantPlacementMode(Google.AR.Core.Config.InstantPlacementMode.LocalYUp);

                // Light Estimation — auf NPU-starken Geräten (Snapdragon 8 Elite, S25 Ultra)
                // EnvironmentalHdr für vollständige Environment-Map. Auf schwächeren Geräten
                // Fallback auf AmbientIntensity (geringere CPU-Last).
                try
                {
                    var preferredLight = IsHighEndDevice()
                        ? Google.AR.Core.Config.LightEstimationMode.EnvironmentalHdr
                        : Google.AR.Core.Config.LightEstimationMode.AmbientIntensity;
                    config.SetLightEstimationMode(preferredLight);
                }
                catch
                {
                    config.SetLightEstimationMode(Google.AR.Core.Config.LightEstimationMode.AmbientIntensity);
                }

                // Focus-Mode auf Auto — Kamera soll sich auf Messziel scharfstellen
                config.SetFocusMode(Google.AR.Core.Config.FocusMode.Auto);

                // Scene Semantic Segmentation: Pro-Pixel-Kategorien (Sky/Terrain/Building/Water).
                // Nutzen wir für Punkt-Validation (kein Messen in Sky-Pixeln).
                try
                {
                    if (_arSession.IsSemanticModeSupported(Google.AR.Core.Config.SemanticMode.Enabled))
                    {
                        config.SetSemanticMode(Google.AR.Core.Config.SemanticMode.Enabled);
                    }
                }
                catch { /* harmlos */ }

                // Plan-Kap. 5.7: Augmented Images / ArUco-Marker als Referenz-Punkte.
                // Wenn der Caller via SetReferenceMarkers Marker uebergeben hat, bauen
                // wir eine AugmentedImageDatabase aus den ImageAssetName-Eintraegen und
                // setzen sie auf die Config. ARCore versucht dann pro Frame die
                // angegebenen Bilder zu erkennen — getrackte Marker liefern eine Pose,
                // die wir mit der eingemessenen Geo-Position abgleichen koennen.
                try
                {
                    if (_referenceMarkers != null && _referenceMarkers.Count > 0)
                    {
                        var db = new global::Google.AR.Core.AugmentedImageDatabase(_arSession);
                        foreach (var m in _referenceMarkers)
                        {
                            try
                            {
                                using var stream = Assets?.Open(m.ImageAssetName);
                                if (stream == null) continue;
                                using var bmp = global::Android.Graphics.BitmapFactory.DecodeStream(stream);
                                if (bmp == null) continue;
                                // AddImage(name, bitmap, widthInMeters) registriert das Bild
                                // mit bekannter physischer Groesse — verbessert die Pose-Genauigkeit
                                // gegenueber dem groessenlosen Default.
                                db.AddImage(m.ImageAssetName, bmp, m.WidthMeters);
                            }
                            catch (Exception ex)
                            {
                                global::Android.Util.Log.Warn("ArCapture",
                                    $"Marker-Bild {m.ImageAssetName} nicht ladbar: {ex.Message}");
                            }
                        }
                        config.SetAugmentedImageDatabase(db);
                    }
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Warn("ArCapture",
                        $"Augmented-Images-Setup fehlgeschlagen: {ex.Message}");
                }

                // Geospatial-API: VPS für globale Positionierung (±1-3m horizontal, ±5° Heading)
                // statt Magnetometer (±15-30° in Metallumgebung). Benötigt Google Cloud API-Key
                // in AndroidManifest + Internet während Session-Start. Bei Fehlschlag fallen wir
                // stumm auf Magnetometer+GPS zurück.
                try
                {
                    if (_arSession.IsGeospatialModeSupported(
                        Google.AR.Core.Config.GeospatialMode.Enabled))
                    {
                        config.SetGeospatialMode(Google.AR.Core.Config.GeospatialMode.Enabled);
                        _geospatialEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Warn("ArCapture",
                        $"Geospatial-Mode nicht aktivierbar (evtl. API-Key fehlt): {ex.Message}");
                }

                // Configure mit progressivem Fallback-Retry: wir isolieren welches Feature
                // genau das Problem ist, statt alle zugleich zu deaktivieren. So sieht der
                // User in der Diagnose-Meldung welches Feature welchen Fehler verursacht.
                try
                {
                    _arSession.Configure(config);
                    global::Android.Util.Log.Info("ArCapture",
                        $"ARCore-Session OK mit allen Features. Geospatial={_geospatialEnabled}, " +
                        $"HighEnd={IsHighEndDevice()}");
                }
                catch (Exception cfgEx)
                {
                    // Original-Exception mit vollem Stack loggen
                    global::Android.Util.Log.Error("ArCapture",
                        $"Configure mit voller Config fehlgeschlagen: " +
                        $"{cfgEx.GetType().FullName}: {cfgEx.Message}\n{cfgEx.StackTrace}");

                    // Konkrete Fehlermeldung für User
                    var userMsg = cfgEx.Message;
                    if (userMsg.Length > 200) userMsg = userMsg.Substring(0, 200);

                    // Schritt 1: Geospatial isoliert testen (wahrscheinlichster Verursacher)
                    try
                    {
                        config.SetGeospatialMode(Google.AR.Core.Config.GeospatialMode.Disabled);
                        _arSession.Configure(config);
                        _geospatialEnabled = false;

                        Toast.MakeText(this,
                            $"Geospatial deaktiviert: {cfgEx.GetType().Name}: {userMsg}",
                            ToastLength.Long)?.Show();
                        global::Android.Util.Log.Warn("ArCapture",
                            $"Konnte nach Geospatial-Off konfigurieren → Geospatial war Ursache");
                    }
                    catch (Exception geoEx)
                    {
                        global::Android.Util.Log.Error("ArCapture",
                            $"Auch ohne Geospatial fehlgeschlagen: {geoEx.Message}");

                        // Schritt 2: Alle optionalen Features deaktivieren
                        try { config.SetSemanticMode(Google.AR.Core.Config.SemanticMode.Disabled); } catch { }
                        try { config.SetLightEstimationMode(Google.AR.Core.Config.LightEstimationMode.Disabled); } catch { }
                        try { config.SetInstantPlacementMode(Google.AR.Core.Config.InstantPlacementMode.Disabled); } catch { }
                        try { config.SetDepthMode(Google.AR.Core.Config.DepthMode.Disabled); } catch { }
                        _geospatialEnabled = false;

                        try
                        {
                            _arSession.Configure(config);
                            Toast.MakeText(this,
                                $"Minimal-Config: {geoEx.GetType().Name}: {geoEx.Message}",
                                ToastLength.Long)?.Show();
                        }
                        catch (Exception finalEx)
                        {
                            global::Android.Util.Log.Error("ArCapture",
                                $"Minimal-Config fehlgeschlagen: {finalEx}");
                            Toast.MakeText(this,
                                $"AR-Start unmöglich: {finalEx.GetType().Name}: {finalEx.Message}",
                                ToastLength.Long)?.Show();
                            SetResult(Result.Canceled);
                            Finish();
                            return;
                        }
                    }
                }
            }
            catch (UnavailableException ex)
            {
                global::Android.Util.Log.Error("ArCapture", $"ARCore nicht verfuegbar: {ex}");
                Toast.MakeText(this,
                    "ARCore nicht verfügbar — Google Play Services for AR im Play Store aktualisieren",
                    ToastLength.Long)?.Show();
                SetResult(Result.Canceled);
                Finish();
                return;
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error("ArCapture", $"ARCore Session-Fehler: {ex}");
                // User-Feedback statt stillem Finish — sonst weiß User nicht was los ist
                Toast.MakeText(this, $"AR-Fehler: {ex.Message}", ToastLength.Long)?.Show();
                SetResult(Result.Canceled);
                Finish();
                return;
            }
        }

        try
        {
            _arSession?.Resume();
            _glSurfaceView?.OnResume();
            _stabilityMonitor?.Start();
        }
        catch (CameraNotAvailableException ex)
        {
            global::Android.Util.Log.Error("ArCapture", $"Camera not available: {ex}");
            Toast.MakeText(this, "Kamera nicht verfügbar — andere App nutzt sie?",
                ToastLength.Long)?.Show();
            _arSession = null;
            SetResult(Result.Canceled);
            Finish();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("ArCapture", $"Resume-Fehler: {ex}");
            Toast.MakeText(this, $"AR-Resume fehlgeschlagen: {ex.Message}",
                ToastLength.Long)?.Show();
            SetResult(Result.Canceled);
            Finish();
        }
    }

    protected override void OnPause()
    {
        base.OnPause();
        // Korrekte Reihenfolge (sonst CameraNotAvailableException im naechsten Update):
        // 1) ARCore-Session pausieren — kein neuer Frame.Update() mehr
        // 2) GL-Surface pausieren — blockiert bis Render-Thread idle ist
        // 3) Frame-Cache invalidieren — ARCore wuerde alte Referenzen recyceln
        // 4) Sensoren stoppen
        _arSession?.Pause();
        _glSurfaceView?.OnPause();
        lock (_frameLock) { _lastFrame = null; }
        _stabilityMonitor?.Stop();

        // SetAutoStopOnPause(true) hat die MP4-Aufnahme mit dem Session-Pause bereits beendet.
        // _isRecording sonst stale → das "Mehr"-Menü zeigt weiter "Aufnahme stoppen" und neue
        // Mess-Aktivität nach Resume wird nicht aufgezeichnet.
        if (_isRecording)
        {
            _isRecording = false;
            _currentRecordingPath = null;
        }

        // FusedLocationProvider-Updates stoppen (Strom; läuft sonst im Hintergrund weiter).
        StopFusedLocationUpdates();

        // Sampler abbrechen: nach OnPause laeuft kein OnDrawFrame mehr, der das 800ms-Fenster
        // finalisieren wuerde. Sonst finalisiert der erste Frame nach OnResume mit Pre-Pause-
        // Samples an inzwischen verschobener Kamera → falscher Punkt. (Analog OnConfigurationChanged.)
        lock (_samplerLock)
        {
            _activeSampler = null;
            _samplesCollected = 0;
            _samplerActiveFrames = 0;
            _samplerPauseFrames = 0;
        }

        // Wartende UI-Thread-HitTest-Ops verwerfen: ohne Render-Thread werden sie nie gedraint;
        // ihre ManualResetEventSlim werden nach Timeout disposed → ein spaeterer Drain (nach
        // OnResume) wuerde sonst auf ein disposed Event treffen. Caller laufen sauber ins Timeout.
        lock (_pendingFrameOps) _pendingFrameOps.Clear();
    }

    #endregion

    #region GLSurfaceView.IRenderer (OpenGL Kamera-Rendering)

    public void OnSurfaceCreated(IGL10? gl, EGLConfig? config)
    {
        // Thread-ID merken: HitTestAtFromUi nutzt das zur Reentrancy-Erkennung
        _glThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

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
                // Dispose wuerde JNI-Referenz freigeben → HitTest crasht.
                _lastFrame = frame;
            }

            // Vom UI-Thread queuete Frame-Operationen (HitTest etc.) JETZT abarbeiten,
            // bevor weitere GL-Aktionen den Frame-State veraendern. So laeuft Frame.HitTest
            // garantiert auf dem GL-Thread mit aktuellem Frame.
            DrainPendingFrameOps(frame);

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

            var isTracking = camera.TrackingState == TrackingState.Tracking;
            CheckTrackingTransition(isTracking);

            _frameCountTotal++;

            // Sampling-Watchdog läuft UNABHÄNGIG vom Tracking-Status — bei Tracking-Verlust
            // während einer 800ms-Sample-Session sonst Sampler stuck + Hint "Messung läuft..."
            // bleibt dauerhaft stehen.
            // Plan 3.2: TrackingState wird in den Sampler weitergereicht, Pause-Frames werden
            // gezählt, FinalizeSampling kann darauf einen unsicheren Punkt verwerfen.
            UpdateSamplingIfActive(isTracking);

            // Thermal + Battery + Anchor-Cleanup alle 60 Frames (~1x/s) — bewusst TRACKING-
            // UNABHÄNGIG: gerade bei Tracking-Verlust arbeitet ARCore am härtesten (Re-Lokalisierung)
            // und das Gerät heizt auf; der Akku-Auto-Finish (Datensicherung vor OS-Kill) muss
            // ebenfalls feuern, und PruneStopped räumt die bei flackerndem Tracking auflaufenden
            // gestoppten Anchors. Vorher lag der Block im if(isTracking) und schwieg genau dann.
            _thermalCheckFrameCounter++;
            if (_thermalCheckFrameCounter >= 60)
            {
                _thermalCheckFrameCounter = 0;
                CheckThermalStatus();
                CheckBatteryStatus();
                _anchorManager.PruneStopped();
            }

            if (isTracking)
            {
                _frameCountTracking++;

                camera.GetProjectionMatrix(_projectionMatrix, 0, 0.1f, 100.0f);
                camera.GetViewMatrix(_viewMatrix, 0);

                // DRIFT-KOMPENSATION: Anchors pro Frame refreshen
                RefreshAllAnchors();

                // Multi-Sample GPS/Heading (Startup-Phase, läuft erste 5s parallel)
                CollectInitialSensorSamples();

                // Ground-Plane-Detection: Y-Wert der größten horizontalen Plane als Referenz
                UpdateGroundPlaneReference();

                // ARCore-Rotation als Heading-Quelle sampeln (Sensor-Fusion, stabiler als Mag)
                SampleArCoreHeading(camera);

                // Geospatial Pose sampeln (VPS via Google Street View Matching)
                UpdateGeospatialPose(camera);

                // Plan-Kap. 3.5: LightEstimate auswerten — bei abruptem Helligkeits-Sprung
                // (Lampe an/aus, Wolke vor Sonne) Sampling unterbrechen, weil ARCore-Feature-
                // Detection in solchen Frames unzuverlaessig ist.
                UpdateLightEstimate(frame);

                // Plan-Kap. 5.7: ArUco-Marker erkennen + Re-Localisation
                if (_referenceMarkers != null && _referenceMarkers.Count > 0)
                    UpdateAugmentedImageRecognition();

                // Plan-Kap. 5.9: Stakeout-Distanz + Pfeil-Richtung pro Frame neu berechnen.
                if (_captureMode == CaptureMode.Stakeout)
                    UpdateStakeout();

                // Plan 3.3: Recovery-Punkte wieder mit Earth-Anchors verknüpfen, sobald
                // Geospatial aktiv ist. Limitiert auf 2 Re-Attaches pro Frame, damit auch
                // ein 100-Punkt-Restore die Render-Loop nicht blockt.
                if (_geospatialActive)
                {
                    ReattachPendingEarthAnchors(maxPerFrame: 2);
                    // Plan-Kap. 5.2: Bestehende Projekt-Punkte als Site-Marker verankern
                    CreatePendingSiteAnchors(maxPerFrame: 2);
                    // Plan-Kap. 5.8: RTK-Stab-Live-Position einmal pro Sekunde refreshen
                    _rtkStabRefreshFrameCounter++;
                    if (_rtkStabRefreshFrameCounter >= 30)
                    {
                        _rtkStabRefreshFrameCounter = 0;
                        UpdateRtkStabAnchor();
                    }
                }

                // Plan-Kap. 5.15: Quality-Heatmap einmal pro Sekunde berechnen (zu teuer
                // pro Frame). Berechnung basiert auf Plane-Coverage + globalem Tracking-
                // Quality-Score — eine echte Per-Patch-FeaturePoint-Analyse erfordert
                // Iteration aller Trackables und ist fuer eine Folge-Iteration vorgesehen.
                if (_heatmapEnabled)
                {
                    _heatmapFrameCounter++;
                    if (_heatmapFrameCounter >= 30)
                    {
                        _heatmapFrameCounter = 0;
                        UpdateQualityHeatmap();
                    }
                }

                // Punkt-Positionen fuer Overlay in Screen-Koordinaten umrechnen
                ProjectPointsToScreen();

                // Live-HitTest am Reticle (Bildmitte) — zeigt Nutzer was bei Tap getroffen wird
                UpdateReticleState(frame, camera);
            }
            else
            {
                _reticleHitQuality = ArHitQuality.None;
                _reticleHitDistance = 0f;
                _reticleHeightDelta = null;
            }

            // Overlay-State pro Frame auf UI-Thread pushen
            var state = BuildOverlayState(camera);
            RunOnUiThread(() => _overlayView?.UpdateState(state));
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
    /// <summary>Reusable MVP-Matrix-Buffer (16 float) — vermeidet float[]-Allocation pro Frame.
    /// GL-Thread-only, kein Lock.</summary>
    private readonly float[] _mvpMatrixScratch = new float[16];

    /// <summary>Reusable Builder fuer Einzelpunkt-Projektionen. Wird unter _dataLock befuellt,
    /// dann atomar in _projectedPoints uebernommen — verhindert Per-Frame
    /// <c>new List&lt;...&gt;()</c>-Allocations (Plan Kap. 4.8). GL-Thread-only.</summary>
    private readonly List<(float screenX, float screenY, int pointIndex)> _projectedPointsBuilder = [];

    /// <summary>Reusable Builder fuer Kontur-Punkt-Projektionen (analog Builder oben).</summary>
    private readonly List<(float screenX, float screenY, int contourIdx, int pointIdx)> _projectedContourPointsBuilder = [];

    /// <summary>Reusable Builder fuer Tape-Measure-Punkte (Plan-Kap. 5.3) — Screen-Koordinaten
    /// in Reihenfolge ihrer Tap-Erfassung.</summary>
    private readonly List<(float screenX, float screenY)> _projectedTapeMeasureBuilder = [];

    /// <summary>Reusable Builder fuer Site-Marker (Plan-Kap. 5.2) — projizierte Earth-
    /// Anchor-Positionen bestehender Projekt-Punkte mit Label.</summary>
    private readonly List<(float screenX, float screenY, string label)> _projectedSiteMarkersBuilder = [];

    /// <summary>Aktuelle Projektion des RTK-Stab-Anchors (Plan-Kap. 5.8). Wird in
    /// <see cref="ProjectPointsToScreen"/> aktualisiert, in <see cref="BuildOverlayState"/>
    /// in den Snapshot uebernommen. null = Anchor nicht sichtbar / kein RTK-Fix.</summary>
    private (float screenX, float screenY)? _projectedRtkStab;

    // Plan-Kap. 5.15: Quality-Heatmap — 12x21-Grid (96 Patches), pro Patch 0..1.
    // Berechnung alle 30 Frames (~1Hz), Snapshot wird in ArOverlayState durchgereicht.
    private const int HeatmapCols = 12;
    private const int HeatmapRows = 21;
    private readonly float[,] _heatmapGrid = new float[HeatmapCols, HeatmapRows];
    private int _heatmapFrameCounter;
    private bool _heatmapEnabled;

    private void ProjectPointsToScreen()
    {
        global::Android.Opengl.Matrix.MultiplyMM(_mvpMatrixScratch, 0, _projectionMatrix, 0, _viewMatrix, 0);

        _projectedPointsBuilder.Clear();
        _projectedContourPointsBuilder.Clear();
        _projectedTapeMeasureBuilder.Clear();

        // Punkte-Snapshot unter Lock erstellen (GL-Thread liest, UI-Thread schreibt)
        lock (_dataLock)
        {
            // Einzelpunkte projizieren
            for (var i = 0; i < _points.Count; i++)
            {
                var screen = WorldToScreen(_points[i], _mvpMatrixScratch);
                if (screen.HasValue)
                    _projectedPointsBuilder.Add((screen.Value.x, screen.Value.y, i));
            }

            // Kontur-Punkte projizieren
            for (var ci = 0; ci < _contours.Count; ci++)
            {
                for (var pi = 0; pi < _contours[ci].Points.Count; pi++)
                {
                    var screen = WorldToScreen(_contours[ci].Points[pi], _mvpMatrixScratch);
                    if (screen.HasValue)
                        _projectedContourPointsBuilder.Add((screen.Value.x, screen.Value.y, ci, pi));
                }
            }

            // Aktive Kontur
            if (_activeContour != null)
            {
                for (var pi = 0; pi < _activeContour.Points.Count; pi++)
                {
                    var screen = WorldToScreen(_activeContour.Points[pi], _mvpMatrixScratch);
                    if (screen.HasValue)
                        _projectedContourPointsBuilder.Add((screen.Value.x, screen.Value.y, -1, pi));
                }
            }

            // Plan-Kap. 5.3: Tape-Measure-Punkte
            for (var i = 0; i < _tapeMeasurePoints.Count; i++)
            {
                var screen = WorldToScreen(_tapeMeasurePoints[i], _mvpMatrixScratch);
                if (screen.HasValue)
                    _projectedTapeMeasureBuilder.Add((screen.Value.x, screen.Value.y));
            }

            // Plan-Kap. 5.2: Site-Marker (Earth-Anchor-Cache, bestehende Projekt-Punkte)
            _projectedSiteMarkersBuilder.Clear();
            foreach (var sm in _sitePointAnchors)
            {
                var screen = WorldToScreen(sm, _mvpMatrixScratch);
                if (screen.HasValue)
                    _projectedSiteMarkersBuilder.Add((screen.Value.x, screen.Value.y, sm.Label ?? ""));
            }

            // Plan-Kap. 5.8: RTK-Stab-Marker (eigener Render-Pfad mit Fix-Farbe)
            _projectedRtkStab = null;
            if (_rtkStabAnchor != null)
            {
                var screen = WorldToScreen(_rtkStabAnchor, _mvpMatrixScratch);
                if (screen.HasValue)
                    _projectedRtkStab = (screen.Value.x, screen.Value.y);
            }
        }

        lock (_projectedPoints)
        {
            _projectedPoints.Clear();
            _projectedPoints.AddRange(_projectedPointsBuilder);
        }

        lock (_projectedContourPoints)
        {
            _projectedContourPoints.Clear();
            _projectedContourPoints.AddRange(_projectedContourPointsBuilder);
        }

        // Erkannte Planes projizieren
        var projectedPlanes = ProjectPlanesToScreen(_mvpMatrixScratch);

        // Overlay auf UI-Thread aktualisieren. Die Builder leben auf dem GL-Thread weiter —
        // wir kopieren NUR einmal in eine Übergabe-Liste statt zweifach (vorher: erst Builder,
        // dann eine zusaetzliche Snapshot-Copy unter dem _projectedPoints-Lock).
        var ptsForUi = new List<(float, float, int)>(_projectedPointsBuilder);
        var cPtsForUi = new List<(float, float, int, int)>(_projectedContourPointsBuilder);

        RunOnUiThread(() =>
        {
            _overlayView?.UpdateProjectedPositions(ptsForUi, cPtsForUi);
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
                if (pq == null || pq.Length < 4) continue;
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
        if (_undoStack.Count == 0)
        {
            ShowTransientHint("Nichts zum Rückgängig machen");
            return;
        }
        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        ClearSelection();
        UpdateCounter();
        _overlayView?.Invalidate();
        ShowTransientHint("Rückgängig");
        SaveRecoveryState();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            ShowTransientHint("Nichts zum Wiederholen");
            return;
        }
        var action = _redoStack.Pop();
        action.Redo();
        _undoStack.Push(action);
        ClearSelection();
        UpdateCounter();
        _overlayView?.Invalidate();
        ShowTransientHint("Wiederholt");
        SaveRecoveryState();
    }

    /// <summary>Setzt die KOMPLETTE Punkt-Selektion zurück (Einzel- UND Kontur-Indizes). Nach
    /// einer Listen-Mutation (Undo/Redo/Insert) verschieben sich Indizes — eine stale Selektion
    /// würde sonst beim nächsten Löschen/Verschieben den falschen Punkt treffen.</summary>
    private void ClearSelection()
    {
        _selectedPointIndex = -1;
        _selectedContourIdx = -1;
        _selectedContourPointIdx = -1;
        _isContourPointSelected = false;
        _overlayView?.SetSelectedIndex(-1);
    }

    #region Haptic Feedback

    /// <summary>
    /// Kurzes Vibrations-Feedback für Punkt-Set. Auf Android 10+ nutzt es die
    /// OEM-getunten Predefined-Effects (Samsung hat besonders feine Haptic-Motoren!),
    /// Fallback auf CreateOneShot.
    /// </summary>
    private void VibrateLight()
    {
        if (_vibrator == null || !_vibrator.HasVibrator) return;
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                var effect = global::Android.OS.VibrationEffect.CreatePredefined(
                    global::Android.OS.VibrationEffect.EffectTick);
                if (effect != null) { _vibrator.Vibrate(effect); return; }
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                _vibrator.Vibrate(global::Android.OS.VibrationEffect.CreateOneShot(30,
                    global::Android.OS.VibrationEffect.DefaultAmplitude));
            else
#pragma warning disable CA1422
                _vibrator.Vibrate(30);
#pragma warning restore CA1422
        }
        catch { /* harmlos */ }
    }

    /// <summary>Mittleres Feedback für Aktionen wie "Kontur schließen".</summary>
    private void VibrateMedium()
    {
        if (_vibrator == null || !_vibrator.HasVibrator) return;
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                var effect = global::Android.OS.VibrationEffect.CreatePredefined(
                    global::Android.OS.VibrationEffect.EffectClick);
                if (effect != null) { _vibrator.Vibrate(effect); return; }
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                _vibrator.Vibrate(global::Android.OS.VibrationEffect.CreateOneShot(60,
                    global::Android.OS.VibrationEffect.DefaultAmplitude));
            else
#pragma warning disable CA1422
                _vibrator.Vibrate(60);
#pragma warning restore CA1422
        }
        catch { /* harmlos */ }
    }

    /// <summary>Stärkeres Warn-Feedback (Pattern) für Tracking-Verlust.</summary>
    private void VibrateWarning()
    {
        if (_vibrator == null || !_vibrator.HasVibrator) return;
        try
        {
            // Android 10+ (API 29): Predefined-Effects werden vom OEM getunt (Samsung premium)
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                var effect = global::Android.OS.VibrationEffect.CreatePredefined(
                    global::Android.OS.VibrationEffect.EffectDoubleClick);
                if (effect != null) { _vibrator.Vibrate(effect); return; }
            }

            long[] pattern = [0, 80, 40, 80];
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                _vibrator.Vibrate(global::Android.OS.VibrationEffect.CreateWaveform(pattern, -1));
            else
#pragma warning disable CA1422
                _vibrator.Vibrate(pattern, -1);
#pragma warning restore CA1422
        }
        catch { /* harmlos */ }
    }

    #endregion

    #region Sound Feedback

    /// <summary>
    /// Kurzer Shutter-Klick beim Punkt-Setzen. Lazy-Load der MediaActionSound, da sie
    /// nur ~5ms zum Initialisieren braucht und Speicher belegt — wir laden sie erst beim
    /// ersten Aufruf. Wenn _soundEnabled == false, kein Init.
    /// </summary>
    private void PlayShutterSound()
    {
        if (!_soundEnabled) return;
        try
        {
            if (_shutterSound == null)
            {
                _shutterSound = new global::Android.Media.MediaActionSound();
                _shutterSound.Load(global::Android.Media.MediaActionSoundType.ShutterClick);
                _shutterLoaded = true;
            }
            _shutterSound.Play(global::Android.Media.MediaActionSoundType.ShutterClick);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"ShutterSound failed: {ex.Message}");
        }
    }

    /// <summary>Toggle Sound-Output. Persistiert in SharedPreferences.</summary>
    private void SetSoundEnabled(bool enabled)
    {
        _soundEnabled = enabled;
        try
        {
            var prefs = GetSharedPreferences("smartmeasure_ar", FileCreationMode.Private);
            using var editor = prefs?.Edit();
            editor?.PutBoolean("ar.sound.enabled", enabled);
            editor?.Apply();
        }
        catch { /* harmlos */ }
    }

    #endregion

    // Bestätigungs-Dialoge: ArCaptureActivity.Dialogs.cs

    #region Device-Detection + Window-Insets

    /// <summary>
    /// Gerät ist "High-End" (Snapdragon 8 Gen 3+ / Tensor G4 / Apple-Level): mehr RAM,
    /// bessere NPU, schnelle Kamera-Pipeline. Erlaubt aggressiveres Sampling.
    /// Heuristik: ActivityManager meldet RAM > 8GB.
    /// </summary>
    private bool IsHighEndDevice()
    {
        try
        {
            var am = GetSystemService(ActivityService) as global::Android.App.ActivityManager;
            if (am == null) return false;
            var memInfo = new global::Android.App.ActivityManager.MemoryInfo();
            am.GetMemoryInfo(memInfo);
            // TotalMem in Bytes — 8GB+ → High-End (S25 Ultra hat 12GB)
            return memInfo.TotalMem >= 8L * 1024 * 1024 * 1024;
        }
        catch { return false; }
    }

    /// <summary>
    /// Liest Window-Insets (Status-Bar + Punch-Hole oben, Navigation-Bar unten).
    /// Auf S25 Ultra ist die Kamera zentral oben → Top-UI muss unter dem Loch sein.
    /// </summary>
    public override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        try
        {
            var decor = Window?.DecorView;
            if (decor == null) return;

            // OnApplyWindowInsets wird beim ersten Layout aufgerufen
            decor.SetOnApplyWindowInsetsListener(new InsetListener(this));
        }
        catch { /* harmlos */ }

        // Coach-Marks beim ersten AR-Start zeigen (vor allem anderen, damit User Kontext hat).
        ShowCoachMarksIfNeeded();
    }

    private sealed class InsetListener(ArCaptureActivity activity)
        : Java.Lang.Object, global::Android.Views.View.IOnApplyWindowInsetsListener
    {
        public global::Android.Views.WindowInsets OnApplyWindowInsets(
            global::Android.Views.View? v, global::Android.Views.WindowInsets? insets)
        {
            if (insets != null)
            {
                try
                {
                    if (OperatingSystem.IsAndroidVersionAtLeast(30))
                    {
                        var systemBars = insets.GetInsets(
                            global::Android.Views.WindowInsets.Type.SystemBars()
                            | global::Android.Views.WindowInsets.Type.DisplayCutout());
                        if (systemBars != null)
                        {
                            activity._topInsetPx = systemBars.Top;
                            activity._bottomInsetPx = systemBars.Bottom;
                        }
                    }
                    else
                    {
#pragma warning disable CA1422
                        activity._topInsetPx = insets.SystemWindowInsetTop;
                        activity._bottomInsetPx = insets.SystemWindowInsetBottom;
#pragma warning restore CA1422
                    }

                    // Toolbar-Bottom-Margin auf Nav-Bar-Höhe setzen — sonst überlappt
                    // die System-Navigation-Bar (Gestensteuerung oder 3-Button-Nav auf S25 Ultra).
                    activity.RunOnUiThread(() =>
                    {
                        var tb = activity._toolbarScrollView;
                        if (tb?.LayoutParameters is FrameLayout.LayoutParams lp)
                        {
                            lp.BottomMargin = (int)activity._bottomInsetPx;
                            tb.LayoutParameters = lp;
                        }
                    });
                }
                catch { /* harmlos */ }
            }
            return insets ?? new global::Android.Views.WindowInsets(insets);
        }
    }

    #endregion

    // Session-Recovery: ArCaptureActivity.Recovery.cs

    #region Screenshot

    /// <summary>Screenshot des aktuellen AR-Frames + Overlay, speichert als PNG.
    ///
    /// Implementierung:
    /// 1. PixelCopy.Request fuer die GL-Surface (Kamera-Feed) — View.Draw allein
    ///    wuerde nur schwarz liefern, weil GL-Frames nicht in die View-Hierarchie
    ///    gerendert werden.
    /// 2. Anschließend zeichnen wir das Overlay (Punkte, Reticle, Stats) auf dasselbe Bitmap.
    /// 3. Speichern als PNG.
    /// </summary>
    private void TakeScreenshot()
    {
        VibrateLight();
        var glView = _glSurfaceView;
        var overlay = _overlayView;
        if (glView == null || overlay == null || glView.Width <= 0 || glView.Height <= 0)
        {
            Toast.MakeText(this, "Screenshot nicht möglich", ToastLength.Short)?.Show();
            return;
        }

        var width = glView.Width;
        var height = glView.Height;
        var bitmap = global::Android.Graphics.Bitmap.CreateBitmap(
            width, height, global::Android.Graphics.Bitmap.Config.Argb8888!);
        if (bitmap == null)
        {
            Toast.MakeText(this, "Screenshot fehlgeschlagen (Bitmap)", ToastLength.Short)?.Show();
            return;
        }

        var handlerThread = new HandlerThread("PixelCopy-ArCapture");
        handlerThread.Start();
        var handler = new Handler(handlerThread.Looper!);
        try
        {
            PixelCopy.Request(glView, bitmap, new PixelCopyListener(result =>
            {
                try
                {
                    if (result != (int)PixelCopyResult.Success)
                    {
                        bitmap.Recycle();
                        bitmap.Dispose();
                        RunOnUiThread(() =>
                        {
                            Toast.MakeText(this, $"Screenshot fehlgeschlagen (Code {result})",
                                ToastLength.Short)?.Show();
                        });
                        return;
                    }

                    // Overlay drüber compositen — auf einem temporären Canvas
                    using var canvas = new Canvas(bitmap);
                    overlay.Draw(canvas);

                    // Auf I/O-Thread speichern, damit UI nicht blockt
                    var pngPath = SaveBitmapAsPng(bitmap);
                    RunOnUiThread(() =>
                    {
                        if (pngPath != null)
                        {
                            Toast.MakeText(this,
                                $"Screenshot: {System.IO.Path.GetFileName(pngPath)}",
                                ToastLength.Short)?.Show();
                            ShowTransientHint("Screenshot gespeichert");
                        }
                        else
                        {
                            Toast.MakeText(this, "Screenshot konnte nicht gespeichert werden",
                                ToastLength.Short)?.Show();
                        }
                    });
                }
                finally
                {
                    bitmap.Recycle();
                    bitmap.Dispose();
                    handlerThread.QuitSafely();
                }
            }), handler);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"PixelCopy.Request failed: {ex.Message}");
            bitmap.Recycle();
            bitmap.Dispose();
            handlerThread.QuitSafely();
            Toast.MakeText(this, "Screenshot fehlgeschlagen", ToastLength.Short)?.Show();
        }
    }

    private string? SaveBitmapAsPng(global::Android.Graphics.Bitmap bitmap)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dir = GetExternalFilesDir("Screenshots")?.AbsolutePath
                ?? global::Android.OS.Environment.DirectoryPictures
                ?? FilesDir?.AbsolutePath;
            if (string.IsNullOrEmpty(dir)) return null;
            Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"SmartMeasure_{timestamp}.png");
            using var stream = File.OpenWrite(path);
            bitmap.Compress(global::Android.Graphics.Bitmap.CompressFormat.Png!, 100, stream);
            return path;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"SaveBitmapAsPng failed: {ex.Message}");
            return null;
        }
    }

    private sealed class PixelCopyListener(Action<int> onFinish)
        : Java.Lang.Object, PixelCopy.IOnPixelCopyFinishedListener
    {
        public void OnPixelCopyFinished(int copyResult) => onFinish(copyResult);
    }

    #endregion

    #region GPS Multi-Sample-Averaging

    /// <summary>Empfängt GPS-Updates während der 5s-Sampling-Phase.</summary>
    private sealed class GpsSampleListener(ArCaptureActivity activity)
        : Java.Lang.Object, global::Android.Locations.ILocationListener
    {
        public void OnLocationChanged(global::Android.Locations.Location location)
        {
            if (location == null) return;
            lock (activity._gpsSamples)
            {
                if (activity._gpsSamples.Count >= GpsSampleTargetCount) return;
                activity._gpsSamples.Add((location.Latitude, location.Longitude,
                    location.HasAltitude ? location.Altitude : null,
                    location.HasAccuracy ? location.Accuracy : null));
            }
        }

        public void OnProviderDisabled(string provider) { }
        public void OnProviderEnabled(string provider) { }
        public void OnStatusChanged(string? provider, global::Android.Locations.Availability status,
            global::Android.OS.Bundle? extras) { }
    }

    /// <summary>
    /// Bildet gewichteten Median aller GPS-Samples und schreibt ihn in die Session-Referenz.
    /// Gewichtung: inverse Accuracy (präzisere Samples zählen stärker).
    /// </summary>
    private void FinalizeGpsAveraging()
    {
        List<(double lat, double lon, double? alt, float? acc)> samples;
        lock (_gpsSamples) samples = new List<(double, double, double?, float?)>(_gpsSamples);
        if (samples.Count == 0) return;

        // Weights aus Accuracy: 1 / max(accuracy, 0.5m) damit genauere Samples mehr Gewicht haben
        var weights = samples.Select(s => 1.0 / Math.Max(s.acc ?? 10f, 0.5f)).ToArray();
        var totalWeight = weights.Sum();
        if (totalWeight <= 0) return;

        var avgLat = 0.0;
        var avgLon = 0.0;
        var avgAlt = 0.0;
        var altCount = 0;
        var minAcc = float.MaxValue;

        for (var i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            var w = weights[i];
            avgLat += s.lat * w;
            avgLon += s.lon * w;
            if (s.alt.HasValue) { avgAlt += s.alt.Value * w; altCount++; }
            if (s.acc.HasValue && s.acc.Value < minAcc) minAcc = s.acc.Value;
        }

        lock (_gpsLock)
        {
            _gpsLatitude = avgLat / totalWeight;
            _gpsLongitude = avgLon / totalWeight;
            if (altCount > 0) _gpsAltitude = avgAlt / totalWeight;
            _gpsAccuracy = minAcc < float.MaxValue ? minAcc : _gpsAccuracy;
        }

        // Plan Kap. 4.5: Sample-Liste freigeben — nach Averaging brauchen wir die
        // einzelnen Werte nicht mehr und der Cap (200) würde bei Recovery + lange laufender
        // Session RAM halten.
        lock (_gpsSamples) _gpsSamples.Clear();

        global::Android.Util.Log.Info("ArCapture",
            $"GPS-Averaging: {samples.Count} samples, best acc={minAcc:F1}m");
    }

    #endregion

    #region Heading-Averaging

    /// <summary>
    /// Finalisiert Heading-Averaging aus den gesammelten Samples (Median statt Mean,
    /// weil Magnetometer-Ausreisser sonst alles kaputtmachen).
    /// </summary>
    private void FinalizeHeadingAveraging()
    {
        float[] samples;
        lock (_headingSamples) samples = _headingSamples.ToArray();
        if (samples.Length == 0) return;

        // Circular median: Winkel können um 360° springen
        var sinSum = samples.Sum(h => MathF.Sin(h * MathF.PI / 180f));
        var cosSum = samples.Sum(h => MathF.Cos(h * MathF.PI / 180f));
        var medianRad = MathF.Atan2(sinSum / samples.Length, cosSum / samples.Length);
        var medianDeg = medianRad * 180f / MathF.PI;
        if (medianDeg < 0) medianDeg += 360f;

        _magneticHeading = medianDeg;

        // Plan Kap. 4.5: nach Averaging Sample-Listen freigeben.
        lock (_headingSamples) _headingSamples.Clear();
        lock (_arCoreHeadingSamples) _arCoreHeadingSamples.Clear();

        global::Android.Util.Log.Info("ArCapture",
            $"Heading-Averaging: {samples.Length} samples → {medianDeg:F1}°");
    }

    #endregion

    #region Per-Frame Präzisions-Helpers

    /// <summary>
    /// Plan Kap. 3.7: Wendet die Snap-Engine auf einen Hit-Punkt an. Schickt alle aktuell
    /// gesetzten Punkte (Einzel + alle Konturen) als Vertex-Snap-Kandidaten, die aktive
    /// Kontur als Right-Angle-Referenz und die kompletten Konturkanten als Parallel- /
    /// Extension-Quelle. Snap-Hint wird per Hint+Haptic gemeldet.
    /// </summary>
    private (float x, float y, float z, SmartMeasure.Shared.Services.ArSnapEngine.SnapType type) ApplySnapToHit(float x, float y, float z)
    {
        List<ArPoint> existing;
        List<ArPoint>? activeContourPoints = null;
        List<SmartMeasure.Shared.Services.ArSnapEngine.Edge>? edges = null;
        lock (_dataLock)
        {
            // Snapshots — Snap-Engine darf nicht auf Listen iterieren die parallel mutiert werden.
            existing = new List<ArPoint>(_points);
            foreach (var c in _contours) existing.AddRange(c.Points);
            if (_activeContour != null)
            {
                existing.AddRange(_activeContour.Points);
                activeContourPoints = new List<ArPoint>(_activeContour.Points);
            }

            // Edge-Liste fuer Parallel- + Extension-Snap. Pro abgeschlossener Kontur alle
            // Edges (inkl. Schluss-Edge wenn geschlossen). Aktive Kontur: Edges zwischen
            // bisherigen Punkten — die nicht-fertige "letzter Punkt → Hit"-Strecke
            // gehoert NICHT in die Edge-Liste (sonst snappt der Hit auf seine eigene
            // Richtung).
            edges = [];
            foreach (var c in _contours)
            {
                for (var i = 0; i < c.Points.Count - 1; i++)
                    edges.Add(new SmartMeasure.Shared.Services.ArSnapEngine.Edge(c.Points[i], c.Points[i + 1]));
                if (c.IsClosed && c.Points.Count >= 3)
                    edges.Add(new SmartMeasure.Shared.Services.ArSnapEngine.Edge(c.Points[^1], c.Points[0]));
            }
            if (_activeContour != null)
            {
                for (var i = 0; i < _activeContour.Points.Count - 1; i++)
                    edges.Add(new SmartMeasure.Shared.Services.ArSnapEngine.Edge(_activeContour.Points[i], _activeContour.Points[i + 1]));
            }
        }

        var snapped = SmartMeasure.Shared.Services.ArSnapEngine.Apply(x, y, z, existing, activeContourPoints, edges);
        if (snapped.type != SmartMeasure.Shared.Services.ArSnapEngine.SnapType.None)
        {
            var hint = snapped.type switch
            {
                SmartMeasure.Shared.Services.ArSnapEngine.SnapType.Vertex => "Vertex-Snap",
                SmartMeasure.Shared.Services.ArSnapEngine.SnapType.RightAngle => "90°-Snap",
                SmartMeasure.Shared.Services.ArSnapEngine.SnapType.Parallel => "Parallel-Snap",
                SmartMeasure.Shared.Services.ArSnapEngine.SnapType.Extension => "Verlängerung",
                _ => null,
            };
            if (hint != null)
                RunOnUiThread(() => { ShowTransientHint(hint); VibrateLight(); });
        }
        return snapped;
    }

    /// <summary>Aktualisiert alle ArPoint-Positionen aus ihren Anchors (Drift-Kompensation).</summary>
    private void RefreshAllAnchors()
    {
        List<ArPoint> allPoints;
        lock (_dataLock)
        {
            allPoints = new List<ArPoint>(_points);
            foreach (var c in _contours) allPoints.AddRange(c.Points);
            if (_activeContour != null) allPoints.AddRange(_activeContour.Points);
            // Plan-Kap. 5.2: Site-Marker auch refreshen, sonst driften die alten Punkte
            allPoints.AddRange(_sitePointAnchors);
            // Plan-Kap. 5.8: RTK-Stab-Live-Anchor mit refreshen
            if (_rtkStabAnchor != null) allPoints.Add(_rtkStabAnchor);
        }
        _anchorManager.RefreshAnchors(allPoints);
    }

    /// <summary>
    /// Plan 3.3: Verbindet wiederhergestellte Recovery-Punkte erneut mit Earth-Anchors.
    /// Wird pro Frame aufgerufen wenn <see cref="_geospatialActive"/> true ist. Pro Frame
    /// werden höchstens <paramref name="maxPerFrame"/> Punkte verarbeitet, damit auch
    /// große Recovery-Sets (50–100 Punkte) die Render-Loop nicht stallen.
    /// </summary>
    private void ReattachPendingEarthAnchors(int maxPerFrame)
    {
        var earth = _arSession?.Earth;
        if (earth == null) return;
        if (earth.TrackingState != TrackingState.Tracking) return;

        List<ArPoint> batch;
        lock (_pendingRestoreLock)
        {
            if (_pendingEarthAnchorRestore.Count == 0) return;
            var take = Math.Min(maxPerFrame, _pendingEarthAnchorRestore.Count);
            batch = _pendingEarthAnchorRestore.GetRange(0, take);
            _pendingEarthAnchorRestore.RemoveRange(0, take);
        }

        foreach (var p in batch)
        {
            if (!p.GeoLatitude.HasValue || !p.GeoLongitude.HasValue) continue;
            var alt = p.GeoAltitude ?? 0.0;
            _anchorManager.TryCreateEarthAnchor(earth, p.GeoLatitude.Value,
                p.GeoLongitude.Value, alt, p);
        }
    }

    // GL-Thread liest + setzt — volatile gegen JIT-Caching.
    private volatile bool _sensorAveragingFinalized;
    private int _groundUpdateFrameCounter;

    /// <summary>Schließt GPS/Heading-Averaging nach 5s ab (einmal pro Session).</summary>
    private void CollectInitialSensorSamples()
    {
        if (_sensorAveragingFinalized) return;

        var elapsedMs = (DateTime.UtcNow - _sessionStart).TotalMilliseconds;
        if (elapsedMs < 5000) return;

        // Sensor-Fusion: bevorzugt ARCore-Heading (stabiler), Magnetometer als Fallback
        var arHeadingFinal = FinalizeArCoreHeading();
        if (arHeadingFinal.HasValue)
        {
            _magneticHeading = arHeadingFinal.Value;
            global::Android.Util.Log.Info("ArCapture",
                $"Heading-Final: ARCore-Fusion → {arHeadingFinal.Value:F1}°");
        }
        else
        {
            FinalizeHeadingAveraging();
            global::Android.Util.Log.Info("ArCapture",
                $"Heading-Final: Magnetometer-Fallback → {_magneticHeading:F1}°");
        }

        _sensorAveragingFinalized = true;
    }

    /// <summary>
    /// Aktualisiert die Ground-Plane-Y-Referenz alle 30 Frames (~1x/sek).
    /// Die größte horizontale Plane = wahrscheinlichster Boden.
    /// </summary>
    private void UpdateGroundPlaneReference()
    {
        _groundUpdateFrameCounter++;
        if (_groundUpdateFrameCounter < 30) return;
        _groundUpdateFrameCounter = 0;

        if (_arSession == null) return;
        var y = ArPrecisionHelpers.FindGroundPlaneY(_arSession);
        if (y.HasValue)
        {
            // Den TIEFSTEN (niedrigsten Y) erkannten Boden über die Session behalten — das ist
            // typisch der echte Boden (Rasen), nicht eine erhöhte Plane (Terrasse/Tisch). Vorher
            // wurde der Wert alle 30 Frames mit der gerade GRÖSSTEN Plane überschrieben → die
            // Höhenreferenz sprang, sobald eine andere Plane die größte wurde.
            if (!_groundPlaneYSet || y.Value < _groundPlaneYValue)
            {
                _groundPlaneYValue = y.Value;
                _groundPlaneYSet = true;
            }
        }
    }

    /// <summary>Thread-safe getter für Ground-Plane-Y (null wenn noch nicht erkannt).</summary>
    private float? GetGroundPlaneY() => _groundPlaneYSet ? _groundPlaneYValue : null;

    #region Recording API (MP4-Session-Archiv)

    private bool _isRecording;
    private string? _currentRecordingPath;

    /// <summary>
    /// Startet/stoppt Session-Recording. ARCore speichert Camera-Feed + Sensor-Metadata
    /// als MP4. Kann später in Playback-Mode abgespielt werden für Nachbetrachtung.
    /// </summary>
    private void ToggleRecording()
    {
        if (_arSession == null) return;

        if (_isRecording)
        {
            try
            {
                _arSession.StopRecording();
                _isRecording = false;
                ShowTransientHint($"Aufnahme gespeichert: {System.IO.Path.GetFileName(_currentRecordingPath)}");
                VibrateMedium();
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("ArCapture", $"StopRecording failed: {ex.Message}");
            }
        }
        else
        {
            try
            {
                var dir = GetExternalFilesDir("Recordings")?.AbsolutePath
                    ?? global::Android.OS.Environment.DirectoryMovies
                    ?? FilesDir?.AbsolutePath;
                if (string.IsNullOrEmpty(dir))
                {
                    ShowTransientHint("Aufnahme nicht möglich: kein Speicherpfad");
                    return;
                }
                Directory.CreateDirectory(dir);
                _currentRecordingPath = System.IO.Path.Combine(dir,
                    $"SmartMeasure_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

                var recordingConfig = new global::Google.AR.Core.RecordingConfig(_arSession);
                recordingConfig.SetMp4DatasetFilePath(_currentRecordingPath);
                recordingConfig.SetAutoStopOnPause(true);

                _arSession.StartRecording(recordingConfig);
                _isRecording = true;
                ShowTransientHint("Aufnahme läuft");
                VibrateMedium();
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("ArCapture", $"StartRecording failed: {ex.Message}");
                Toast.MakeText(this, $"Recording-Fehler: {ex.Message}", ToastLength.Short)?.Show();
            }
        }
    }

    #endregion

    #region Thermal + Battery Management

    /// <summary>
    /// Überwacht Thermal-Status des Geräts. Bei kritischer Hitze werden teure
    /// Features degradiert (z.B. EnvironmentalHdr → AmbientIntensity, Sample-Count reduziert).
    /// S25 Ultra mit Snapdragon 8 Elite bleibt normalerweise cool, aber bei langen Sessions
    /// oder direkter Sonne kann es kritisch werden.
    /// </summary>
    private void CheckThermalStatus()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29)) return;

        try
        {
            var pm = GetSystemService(PowerService) as global::Android.OS.PowerManager;
            if (pm == null) return;

            // PowerManager.ThermalStatus ist int (1=Light, 2=Moderate, 3=Severe, 4=Critical, 5=Emergency)
            var status = (int)pm.CurrentThermalStatus;
            string? warning;
            // WICHTIG: Das Sample-Ziel darf NIE unter MinValidSampleCount fallen — sonst
            // verwirft FinalizeSampling JEDEN Punkt ("Zu wenige Samples") und es lässt sich
            // bei Hitze gar nichts mehr messen, genau im Outdoor-Hauptszenario.
            if (status >= 4) // Critical+
            {
                _effectiveMultiFrameSampleTargetCount = MinValidSampleCount;
                warning = "Gerät kritisch heiß — Pause empfohlen";
            }
            else if (status >= 3) // Severe
            {
                _effectiveMultiFrameSampleTargetCount = MinValidSampleCount + 1;
                warning = "Gerät heiß — Präzision reduziert";
            }
            else if (status >= 2) // Moderate
            {
                _effectiveMultiFrameSampleTargetCount = 10;
                warning = null;
            }
            else
            {
                _effectiveMultiFrameSampleTargetCount = IsHighEndDevice() ? 15 : 10;
                warning = null;
            }

            // Persistenter Banner statt einmaligem Transient-Hint —
            // bleibt sichtbar solange das Gerät throttled.
            _thermalWarningText = warning;
        }
        catch { /* harmlos */ }
    }

    /// <summary>Persistenter Banner-Text für Thermal-Throttling. Null = ok.
    /// Wird in BuildOverlayState an <see cref="ArOverlayState.ThermalWarning"/> übergeben.</summary>
    private volatile string? _thermalWarningText;

    /// <summary>Warnt User bei niedrigem Akku via persistentem Banner.
    /// Niveau-Updates: kein Spam, der Banner aktualisiert sich nur stillschweigend.
    /// Bei kritisch niedrigem Akku (&lt;5%) wird die Session einmalig automatisch
    /// abgeschlossen — alle bisher erfassten Punkte werden ins Result gepackt, bevor
    /// das System die App eventuell kalt killt. Plan Kap. 4.7.</summary>
    private void CheckBatteryStatus()
    {
        try
        {
            var bm = GetSystemService(BatteryService) as global::Android.OS.BatteryManager;
            if (bm == null) return;

            var level = bm.GetIntProperty((int)global::Android.OS.BatteryProperty.Capacity);
            _batteryWarningText = level switch
            {
                > 0 and < 5  => $"Akku {level}% — Session wird beendet",
                > 0 and < 15 => $"Akku {level}% — Session bald beenden",
                _            => null,
            };

            // Auto-Finish: nur 1x feuern, nur wenn Session noch laeuft und tatsaechlich
            // Daten erfasst wurden (leere Session zu speichern ist sinnlos).
            if (level > 0 && level < 5
                && System.Threading.Interlocked.CompareExchange(ref _batteryAutoFinishFired, 1, 0) == 0
                && _finished == 0)
            {
                var hasData = false;
                lock (_dataLock)
                    hasData = _points.Count > 0
                        || _contours.Count > 0
                        || (_activeContour?.Points.Count ?? 0) > 0;

                if (hasData)
                {
                    RunOnUiThread(() =>
                    {
                        ShowTransientHint($"Akku {level}% — Session wird beendet, alle Punkte werden gesichert");
                        VibrateWarning();
                        // 1.2s warten damit der User den Hint sieht, dann FinishCapture.
                        _glSurfaceView?.PostDelayed(() =>
                        {
                            try { FinishCapture(); } catch { /* harmlos */ }
                        }, 1200);
                    });
                }
            }
        }
        catch { /* harmlos */ }
    }

    private volatile string? _batteryWarningText;
    private int _thermalCheckFrameCounter;

    /// <summary>0=noch nicht ausgeloest, 1=Auto-Finish wurde getriggert. Verhindert
    /// Spam-Trigger wenn CheckBatteryStatus weiter zyklisch laeuft (alle 60 Frames).</summary>
    private int _batteryAutoFinishFired;

    #endregion

    #region Android 15 HapticFeedbackConstants (moderner Weg)

    /// <summary>
    /// View.PerformHapticFeedback mit Android-14+ Constants (GestureStart, GestureEnd,
    /// Confirm, Reject, ToggleOn). Auf S25 Ultra nutzt das Samsung's Premium-Haptics-Engine.
    /// Fallback auf alte Vibrator-API für Kompatibilität.
    /// </summary>
    // FeedbackConstants.Confirm/Reject existieren erst ab API 30 — der Enum-Zugriff
    // selbst wird daher in TryPerformHaptic hinter den Versions-Guard gezogen. Auf
    // älteren Geräten greift direkt der VibrateLight-Fallback.
    private void HapticConfirm() => TryPerformHaptic(HapticKind.Confirm);

    private void HapticReject() => TryPerformHaptic(HapticKind.Reject);

    private enum HapticKind { Confirm, Reject }

    private void TryPerformHaptic(HapticKind kind)
    {
        try
        {
            var root = _glSurfaceView?.RootView ?? Window?.DecorView;
            if (root != null && OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var constant = kind == HapticKind.Confirm
                    ? global::Android.Views.FeedbackConstants.Confirm
                    : global::Android.Views.FeedbackConstants.Reject;
                root.PerformHapticFeedback(constant);
                return;
            }
        }
        catch { /* harmlos */ }

        // Fallback auf unsere bestehende VibrateLight
        VibrateLight();
    }

    #endregion

    /// <summary>Letzte gemessene Pixel-Intensitaet aus LightEstimate (0.0..1.0). NaN bis
    /// der erste gueltige Wert anliegt. Volatile-Bits-Pattern wie bei _magneticHeading —
    /// GL-Thread schreibt, Sampler-Pruefung kann von anderem Thread lesen.</summary>
    private int _lastPixelIntensityBits = BitConverter.SingleToInt32Bits(float.NaN);

    /// <summary>Anzahl Frames seit dem letzten Reset des Light-Jump-Triggers. Verhindert
    /// dass eine einzige Helligkeits-Schwankung das Sampling 60x pro Sekunde aborted.</summary>
    private int _lightJumpCooldownFrames;

    /// <summary>Plan-Kap. 3.5: Light-Estimation auslesen und auf abrupte Helligkeits-
    /// Sprunge reagieren. Wenn die PixelIntensity um &gt;40% sprint und gerade ein
    /// Multi-Frame-Sampling laeuft, wird das Sampling abgebrochen — die Feature-Detection
    /// liefert in solchen Frames keine stabilen Hits.</summary>
    private void UpdateLightEstimate(Frame frame)
    {
        try
        {
            var le = frame.LightEstimate;
            if (le == null) return;

            // State auf "Valid" pruefen — ARCore meldet "NotValid" wenn Light-Estimation
            // mid-frame disabled wurde (z.B. Thermal-Recovery).
            if (le.GetState() != LightEstimate.State.Valid) return;

            var intensity = le.PixelIntensity;
            if (float.IsNaN(intensity) || intensity <= 0f) return;

            var previousBits = System.Threading.Volatile.Read(ref _lastPixelIntensityBits);
            var previous = BitConverter.Int32BitsToSingle(previousBits);
            System.Threading.Volatile.Write(ref _lastPixelIntensityBits,
                BitConverter.SingleToInt32Bits(intensity));

            // Erstmessung oder noch im Cooldown → kein Jump-Trigger.
            if (float.IsNaN(previous) || previous <= 0f) return;
            if (_lightJumpCooldownFrames > 0)
            {
                _lightJumpCooldownFrames--;
                return;
            }

            // Relative Aenderung. 0.4 = 40% Sprung in beide Richtungen.
            var delta = MathF.Abs(intensity - previous) / previous;
            if (delta < 0.4f) return;

            // Cooldown so dass die naechsten ~2s keine weitere Detektion ausgeloest wird
            // — Helligkeit pendelt sich oft erst nach mehreren Frames ein.
            _lightJumpCooldownFrames = 60;

            // Aktives Sampling abbrechen
            bool wasSampling;
            lock (_samplerLock)
            {
                wasSampling = _activeSampler != null;
                if (wasSampling)
                {
                    _activeSampler = null;
                    _samplesCollected = 0;
                    _samplerActiveFrames = 0;
                    _samplerPauseFrames = 0;
                }
            }

            if (wasSampling)
            {
                RunOnUiThread(() =>
                {
                    ShowTransientHint("Helligkeit gewechselt — bitte erneut anvisieren");
                    VibrateWarning();
                });
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"UpdateLightEstimate failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Liest die Geospatial-Kamera-Pose (VPS) wenn Earth-Tracking aktiv ist.
    /// Aktualisiert _lastGeoLatitude/Longitude/etc. die beim Punkt-Set in ArPoint geschrieben werden.
    /// Sampelt Accuracy-Werte für Final-Report.
    /// </summary>
    private void UpdateGeospatialPose(Camera? camera)
    {
        if (!_geospatialEnabled || _arSession == null) return;

        try
        {
            var earth = _arSession.Earth;
            if (earth == null) return;

            // TrackingState ist ausreichend — Earth trackt nur wenn es funktioniert.
            // EarthState-Property hat Namens-Konflikt mit Inner-Enum-Typ im Xamarin-Binding.
            if (earth.TrackingState != TrackingState.Tracking)
            {
                _geospatialActive = false;
                return;
            }

            _geospatialActive = true;

            var pose = earth.CameraGeospatialPose;
            if (pose == null) return;

            // ARCore-Welt-Y der Kamera zum selben Zeitpunkt wie die VPS-Höhe festhalten —
            // erlaubt später, die echte Gelände-Höhe jedes Hit-Punkts statt der Kamerahöhe abzuleiten.
            var camLocalY = camera?.Pose?.Ty() ?? 0f;

            var snapshot = new GeospatialSnapshot(
                Latitude: pose.Latitude,
                Longitude: pose.Longitude,
                Altitude: pose.Altitude,
                HorizontalAccuracy: (float)pose.HorizontalAccuracy,
                Heading: (float)pose.Heading,
                HeadingAccuracy: (float)pose.HeadingAccuracy,
                CameraLocalY: camLocalY);
            System.Threading.Volatile.Write(ref _lastGeoSnapshot, snapshot);

            // Samples für Final-Report (Median)
            lock (_geospatialHorizontalAccSamples)
            {
                if (_geospatialHorizontalAccSamples.Count < 200)
                    _geospatialHorizontalAccSamples.Add(snapshot.HorizontalAccuracy);
            }
            lock (_geospatialHeadingAccSamples)
            {
                if (_geospatialHeadingAccSamples.Count < 200)
                    _geospatialHeadingAccSamples.Add(snapshot.HeadingAccuracy);
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"Geospatial-Update fehlgeschlagen: {ex.Message}");
            _geospatialActive = false;
        }
    }

    /// <summary>
    /// Liefert die Geo-Position (Lat/Lon/WGS84-Ellipsoid-Alt/H-Acc) eines HIT-PUNKTS, nicht der
    /// Kamera. Primär via <c>earth.GetGeospatialPose(hitPose)</c> (ARCore rechnet die exakte
    /// Geo-Position der lokalen Pose). Fallback: Kamera-Snapshot, dessen Höhe per ARCore-Y-Differenz
    /// (hitY − Kamera-Y) auf die Hit-Höhe korrigiert wird (Lat/Lon bleibt dann kamera-nah).
    /// Liefert (null,…) wenn Geospatial inaktiv.
    /// </summary>
    private (double? lat, double? lon, double? alt, float? hAcc) ResolveHitGeoPose(
        Pose? hitPose, float hitY, GeospatialSnapshot? snapshot)
    {
        if (!_geospatialActive) return (null, null, null, null);

        // Primär: exakte Geo-Pose des Hit-Punkts direkt von ARCore-Earth.
        if (hitPose != null)
        {
            try
            {
                var earth = _arSession?.Earth;
                if (earth != null && earth.TrackingState == TrackingState.Tracking)
                {
                    var gp = earth.GetGeospatialPose(hitPose);
                    if (gp != null)
                        return (gp.Latitude, gp.Longitude, gp.Altitude, (float)gp.HorizontalAccuracy);
                }
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("ArCapture", $"GetGeospatialPose(hit) fehlgeschlagen: {ex.Message}");
            }
        }

        // Fallback: Kamera-Snapshot, Höhe per ARCore-Y-Differenz auf den Hit-Punkt korrigiert.
        if (snapshot != null)
            return (snapshot.Latitude, snapshot.Longitude,
                    snapshot.Altitude + (hitY - snapshot.CameraLocalY), snapshot.HorizontalAccuracy);

        return (null, null, null, null);
    }

    /// <summary>Median-Accuracy aus Geospatial-Samples berechnen (für Report).</summary>
    private (float? horizontal, float? heading) GetGeospatialMedianAccuracy()
    {
        float[] hSamples, dSamples;
        lock (_geospatialHorizontalAccSamples) hSamples = _geospatialHorizontalAccSamples.ToArray();
        lock (_geospatialHeadingAccSamples) dSamples = _geospatialHeadingAccSamples.ToArray();

        float? MedianOf(float[] arr)
        {
            if (arr.Length == 0) return null;
            Array.Sort(arr);
            return arr.Length % 2 == 0
                ? (arr[arr.Length / 2 - 1] + arr[arr.Length / 2]) / 2f
                : arr[arr.Length / 2];
        }

        return (MedianOf(hSamples), MedianOf(dSamples));
    }

    /// <summary>
    /// Sammelt ARCore-Heading aus der Kamera-Pose (Sensor-fusioniert).
    /// Stabiler als rohes Magnetometer, vor allem in Metallumgebung.
    /// </summary>
    private void SampleArCoreHeading(Google.AR.Core.Camera camera)
    {
        try
        {
            var pose = camera.Pose;
            if (pose == null) return;

            var heading = ArPrecisionHelpers.ExtractHeadingFromCameraPose(pose);
            if (!heading.HasValue) return;

            lock (_arCoreHeadingSamples)
            {
                if (_arCoreHeadingSamples.Count < HeadingSampleTargetCount)
                    _arCoreHeadingSamples.Add(heading.Value);
            }
        }
        catch { /* harmlos */ }
    }

    /// <summary>
    /// Zirkulärer MITTELWERT der ARCore-Heading-Samples (atan2 der sin-/cos-Summen) — robust
    /// gegen den 360°-/0°-Sprung. Liefert null wenn zu wenige Samples.
    /// </summary>
    private float? FinalizeArCoreHeading()
    {
        float[] samples;
        lock (_arCoreHeadingSamples) samples = _arCoreHeadingSamples.ToArray();
        if (samples.Length < 5) return null;

        var sinSum = samples.Sum(h => MathF.Sin(h * MathF.PI / 180f));
        var cosSum = samples.Sum(h => MathF.Cos(h * MathF.PI / 180f));
        var meanRad = MathF.Atan2(sinSum / samples.Length, cosSum / samples.Length);
        var meanDeg = meanRad * 180f / MathF.PI;
        if (meanDeg < 0) meanDeg += 360f;
        return meanDeg;
    }

    /// <summary>
    /// Prüft ob die aktuelle Session-Qualität gut genug für Punkt-Set ist.
    /// Liefert (Ready, Checklist) wobei Checklist Probleme aufzählt.
    /// </summary>
    private (bool ready, string checkList) ValidatePreMeasureConditions()
    {
        var failed = new List<string>();

        if (_arSession == null) failed.Add("Session nicht aktiv");

        var stability = _stabilityMonitor?.StabilityScore ?? 0f;
        if (stability < 0.6f) failed.Add("Kamera wackelt");

        if (_magneticAccuracy < 2) failed.Add("Kompass unkalibriert");

        // Plane-Count
        var planeCount = 0;
        try
        {
            var trackables = _arSession?.GetAllTrackables(Java.Lang.Class.FromType(typeof(Plane)));
            if (trackables != null)
                foreach (var t in trackables)
                    if (t is Plane p && p.TrackingState == TrackingState.Tracking && p.SubsumedBy == null)
                        planeCount++;
        }
        catch { /* harmlos */ }

        if (planeCount == 0) failed.Add("Keine Fläche erkannt");

        return (failed.Count == 0, string.Join(" · ", failed));
    }

    /// <summary>Durchschnittliche Messgenauigkeit aller bisherigen Punkte (für Quality-Score).</summary>
    private float GetAverageStdDev()
    {
        lock (_dataLock)
        {
            var all = new List<ArPoint>(_points);
            foreach (var c in _contours) all.AddRange(c.Points);
            if (all.Count == 0) return 0f;
            return all.Where(p => p.PositionStdDev > 0).DefaultIfEmpty().Average(p => p?.PositionStdDev ?? 0f);
        }
    }

    #endregion

    // Kontur-Typ + Kompass: ArCaptureActivity.Dialogs.cs

    // Readiness-Detail-Dialog: ArCaptureActivity.Dialogs.cs

    // Help-Dialog + Coach-Marks: ArCaptureActivity.Dialogs.cs

    #region Transient-Hint

    // Vom UI-Thread (Touch/Dialoge) gesetzt, vom GL-Thread (BuildOverlayState) konsumiert.
    // Volatile.Write sichert die Sichtbarkeit des Schreibens, Interlocked.Exchange holt+löscht
    // atomar — verhindert verlorene Hints über die Thread-Grenze.
    private string? _currentTransientHint;

    private void ShowTransientHint(string text)
    {
        // Wird im nächsten Frame-Update-Zyklus von ConsumeTransientHint aufgegriffen.
        System.Threading.Volatile.Write(ref _currentTransientHint, text);
    }

    private string? ConsumeTransientHint()
        => System.Threading.Interlocked.Exchange(ref _currentTransientHint, null);

    #endregion

    #region Live-Stats + Reticle

    /// <summary>
    /// Live-HitTest in der Bildschirmmitte (Reticle). Nutzt Plane-First, dann Instant Placement.
    /// Liefert (Quality, DistanceMeters, HeightDelta) für Overlay-Anzeige.
    /// </summary>
    private void UpdateReticleState(Frame frame, Google.AR.Core.Camera camera)
    {
        var cx = _viewportWidth / 2f;
        var cy = _viewportHeight / 2f;

        var (quality, distance, heightDelta, world) = PerformReticleHitTest(frame, camera, cx, cy);

        _reticleHitQuality = quality;
        _reticleHitDistance = distance;
        _reticleHeightDelta = heightDelta;
        _reticleWorld = world;
    }

    private (ArHitQuality quality, float distance, float? height, (float x, float y, float z)? world) PerformReticleHitTest(
        Frame frame, Google.AR.Core.Camera camera, float screenX, float screenY)
    {
        try
        {
            // 1. Plane-HitTest bevorzugen
            var hits = frame.HitTest(screenX, screenY);
            if (hits != null)
            {
                foreach (var hit in hits)
                {
                    if (hit.Trackable is Plane plane && plane.IsPoseInPolygon(hit.HitPose))
                        return BuildHitInfo(hit, ArHitQuality.Plane, camera);

                    if (hit.Trackable is Google.AR.Core.Point)
                        return BuildHitInfo(hit, ArHitQuality.Point, camera);
                }
            }

            // 2. Instant Placement als Fallback (geschätzte Tiefe 1.5m)
            var instantHits = frame.HitTestInstantPlacement(screenX, screenY, 1.5f);
            if (instantHits != null && instantHits.Count > 0)
                return BuildHitInfo(instantHits[0], ArHitQuality.InstantPlacement, camera);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"Reticle hit-test failed: {ex.Message}");
        }

        return (ArHitQuality.None, 0f, null, null);
    }

    private (ArHitQuality, float, float?, (float x, float y, float z)?) BuildHitInfo(HitResult hit, ArHitQuality quality, Google.AR.Core.Camera camera)
    {
        var pose = hit.HitPose;
        var cameraPose = camera.Pose;
        if (pose == null || cameraPose == null) return (quality, 0f, null, null);

        var dx = pose.Tx() - cameraPose.Tx();
        var dy = pose.Ty() - cameraPose.Ty();
        var dz = pose.Tz() - cameraPose.Tz();
        var distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        // Welt-Position des Ziels (lokale ARCore-Meter) — fuer Live-Segment-Werte.
        var world = (pose.Tx(), pose.Ty(), pose.Tz());

        // Höhe relativ zum ersten Punkt (Session-Referenz; das Live-Segment-ΔH zum LETZTEN
        // Punkt wird separat in BuildOverlayState berechnet).
        float? heightDelta = null;
        lock (_dataLock)
        {
            if (_points.Count > 0)
                heightDelta = pose.Ty() - _points[0].Y;
            else if (_contours.Count > 0 && _contours[0].Points.Count > 0)
                heightDelta = pose.Ty() - _contours[0].Points[0].Y;
        }

        return (quality, distance, heightDelta, world);
    }

    /// <summary>
    /// Baut den aktuellen Overlay-State aus den Live-Daten zusammen.
    /// Wird pro Frame aufgerufen, Ergebnis an _overlayView.UpdateState.
    /// </summary>
    private ArOverlayState BuildOverlayState(Google.AR.Core.Camera camera)
    {
        var isTracking = camera.TrackingState == TrackingState.Tracking;
        string? failReason = null;

        if (!isTracking)
        {
            // TrackingFailureReason ist ein Java-Enum-Object, kein C#-Enum — deshalb
            // Equals() statt switch-Pattern. String-Vergleich ist zu fragil bei Proguard.
            // Plan-Kap. 4.11: Lokalisierte Texte aus dem Session-Labels-Snapshot.
            var reason = camera.TrackingFailureReason;
            if (reason != null)
            {
                if (reason.Equals(TrackingFailureReason.InsufficientLight))
                    failReason = _overlayLabels.TrackingInsufficientLight;
                else if (reason.Equals(TrackingFailureReason.InsufficientFeatures))
                    failReason = _overlayLabels.TrackingInsufficientFeatures;
                else if (reason.Equals(TrackingFailureReason.ExcessiveMotion))
                    failReason = _overlayLabels.TrackingExcessiveMotion;
                else if (reason.Equals(TrackingFailureReason.CameraUnavailable))
                    failReason = _overlayLabels.TrackingCameraUnavailable;
                else if (reason.Equals(TrackingFailureReason.BadState))
                    failReason = _overlayLabels.TrackingBadState;
                else
                    failReason = _overlayLabels.TrackingLost;
            }
            else
            {
                failReason = _overlayLabels.TrackingLost;
            }
        }

        // Live-Stats berechnen
        float liveArea = 0f, liveLength = 0f, heightRange = 0f;
        (float x, float y)? autoClose = null;

        // Live-Segment ("Gummiband") vom letzten gesetzten Punkt zum Reticle-Ziel
        bool showLiveSegment = false;
        (float x, float y)? liveSegFromScreen = null;
        float? liveSegHorizontal = null, liveSegSlope = null, liveSegHeight = null;
        var liveSegActive = false;
        float? liveSegOffScreenDir = null;
        List<(float horizontal, float heightDelta)>? activeContourSegMeters = null;

        // Rechteck-Vorschau (gefuehrte 3-Punkt-Methode)
        var isRectMode = _captureMode == CaptureMode.Rectangle;
        var rectCornerCount = 0;
        List<(float, float)>? rectCornerScreen = null;
        List<(float, float)>? rectPreviewScreen = null;
        var rectIsSquare = false;
        float rectLen = 0f, rectDepth = 0f, rectArea = 0f;

        lock (_dataLock)
        {
            // Alle Konturen
            foreach (var c in _contours)
            {
                liveLength += c.CalculateLength();
                if (c.IsClosed) liveArea += c.CalculateArea();
            }

            // Aktive Kontur
            if (_activeContour != null)
            {
                liveLength += _activeContour.CalculateLength();
                if (_activeContour.Points.Count >= 3)
                {
                    // Provisorische Fläche (als wäre sie geschlossen). Defensive Kopie der
                    // Punkt-Liste — CalculateArea ist read-only, aber so teilt der Temp-Contour
                    // nicht die Live-Referenz.
                    var tempClosed = new ArContour
                    {
                        Points = new List<ArPoint>(_activeContour.Points),
                        IsClosed = true,
                    };
                    liveArea += tempClosed.CalculateArea();
                }

                // Segment-Werte (horizontale Distanz + Hoehenunterschied) zwischen den gesetzten
                // Kontur-Punkten — fuer die Inter-Punkt-Pillen im Overlay. So bleiben Distanz UND
                // ΔH stehen, nachdem der Punkt gesetzt wurde (frueher leerer Stub).
                if (_activeContour.Points.Count >= 2)
                {
                    activeContourSegMeters = new List<(float, float)>(_activeContour.Points.Count - 1);
                    for (var i = 0; i < _activeContour.Points.Count - 1; i++)
                    {
                        var a = _activeContour.Points[i];
                        var b = _activeContour.Points[i + 1];
                        activeContourSegMeters.Add((a.Distance2DTo(b), b.Y - a.Y));
                    }
                }

                // Auto-Close-Detection: echter Welt-Abstand Reticle-Ziel → erster Kontur-Punkt
                // (15 cm Grundriss-Radius, analog ArSnapEngine-VertexSnap). Robuster als die
                // fruehere 80px-Screen-Heuristik (kein Zoom-/Distanz-Artefakt).
                if (_captureMode == CaptureMode.Contour && _activeContour.Points.Count >= 3
                    && _reticleWorld is { } rwClose)
                {
                    var first = _activeContour.Points[0];
                    var cdx = rwClose.x - first.X;
                    var cdz = rwClose.z - first.Z;
                    if (MathF.Sqrt(cdx * cdx + cdz * cdz) < 0.15f)
                    {
                        var firstScreen = _projectedContourPoints.FirstOrDefault(p => p.contourIdx == -1 && p.pointIdx == 0);
                        if (firstScreen != default)
                            autoClose = (firstScreen.screenX, firstScreen.screenY);
                    }
                }
            }

            // Live-Segment ("Gummiband"): vom zuletzt gesetzten Punkt zum aktuellen Reticle-Ziel.
            // Gilt im Contour- (aktive Kontur) und Point-Modus (letzter Einzelpunkt).
            ArPoint? lastPlaced = null;
            if (_captureMode == CaptureMode.Contour && _activeContour is { Points.Count: > 0 })
                lastPlaced = _activeContour.Points[^1];
            else if (_captureMode == CaptureMode.Point && _points.Count > 0)
                lastPlaced = _points[^1];

            if (lastPlaced != null && _reticleWorld is { } rw && _reticleHitQuality != ArHitQuality.None)
            {
                var sdx = rw.x - lastPlaced.X;
                var sdy = rw.y - lastPlaced.Y;
                var sdz = rw.z - lastPlaced.Z;
                liveSegHorizontal = MathF.Sqrt(sdx * sdx + sdz * sdz);          // Grundriss
                liveSegSlope = MathF.Sqrt(sdx * sdx + sdy * sdy + sdz * sdz);   // schraeg (3D)
                liveSegHeight = sdy;                                            // ΔH (signiert)
                liveSegActive = true; // Distanz/ΔH gültig — UNABHÄNGIG von der Sichtbarkeit des Vorpunkts

                // Startpunkt-Screen-Position. Liegt der letzte Punkt im Bild → volles Gummiband.
                // Liegt er ausserhalb (oder hinter der Kamera) → KEIN Gummiband, aber Distanz/ΔH
                // werden trotzdem am Reticle gezeigt (liveSegActive) + Rand-Pfeil zur Richtung.
                var ls = WorldToScreen(lastPlaced, _mvpMatrixScratch);
                if (ls is { } lsv && lsv.x >= 0 && lsv.x <= _viewportWidth
                    && lsv.y >= 0 && lsv.y <= _viewportHeight)
                {
                    liveSegFromScreen = lsv;
                    showLiveSegment = true;
                }
                else if (ls is { } off)
                {
                    // Vor der Kamera, aber ausserhalb des Bildes → Richtung Bildmitte → Punkt.
                    liveSegOffScreenDir = MathF.Atan2(off.y - _viewportHeight / 2f,
                        off.x - _viewportWidth / 2f) * 180f / MathF.PI;
                }
            }

            // Rechteck-Vorschau: gesetzte Ecken + (ab 2 Ecken) das live aufgespannte Rechteck
            // aus der Reticle-Tiefe projizieren. WorldToScreen nutzt die in ProjectPointsToScreen
            // gefuellte MVP-Matrix (gleicher Frame).
            if (isRectMode)
            {
                rectCornerCount = _rectangleCorners.Count;
                if (rectCornerCount > 0)
                {
                    rectCornerScreen = new List<(float, float)>(rectCornerCount);
                    foreach (var rc in _rectangleCorners)
                    {
                        var s = WorldToScreen(rc, _mvpMatrixScratch);
                        if (s.HasValue) rectCornerScreen.Add(s.Value);
                    }
                }

                if (rectCornerCount == 2 && _reticleWorld is { } rwRect
                    && _reticleHitQuality != ArHitQuality.None)
                {
                    var probe = new ArPoint { X = rwRect.x, Y = rwRect.y, Z = rwRect.z };
                    var rectResult = ArRectangleBuilder.Compute(
                        _rectangleCorners[0], _rectangleCorners[1], probe, RectangleSquareSnapEnabled);
                    if (rectResult != null)
                    {
                        var pts = new List<(float, float)>(4);
                        foreach (var corner in rectResult.Corners)
                        {
                            var s = WorldToScreen(
                                new ArPoint { X = corner.X, Y = corner.Y, Z = corner.Z },
                                _mvpMatrixScratch);
                            if (s.HasValue) pts.Add(s.Value);
                        }
                        // Nur als gueltige Vorschau werten, wenn alle vier Ecken im Bild liegen.
                        if (pts.Count == 4) rectPreviewScreen = pts;
                        rectIsSquare = rectResult.IsSquare;
                        rectLen = rectResult.LengthMeters;
                        rectDepth = rectResult.DepthMeters;
                        rectArea = rectResult.AreaMeters;
                    }
                }
                else if (rectCornerCount == 1 && _reticleWorld is { } rwBase
                    && _reticleHitQuality != ArHitQuality.None)
                {
                    // Live-Länge der entstehenden Basiskante: Horizontaldistanz (Grundriss X/Z)
                    // von der ersten Ecke zum Reticle-Ziel — wird als Pille am Gummiband gezeigt,
                    // damit der Nutzer beim Aufspannen der Basiskante die Strecke sieht.
                    var dx = rwBase.x - _rectangleCorners[0].X;
                    var dz = rwBase.z - _rectangleCorners[0].Z;
                    rectLen = MathF.Sqrt(dx * dx + dz * dz);
                }
            }

            // Höhen-Range
            if (_points.Count > 0 || _contours.Count > 0)
            {
                var allY = new List<float>();
                foreach (var p in _points) allY.Add(p.Y);
                foreach (var c in _contours) foreach (var p in c.Points) allY.Add(p.Y);
                if (_activeContour != null) foreach (var p in _activeContour.Points) allY.Add(p.Y);

                if (allY.Count > 1)
                    heightRange = allY.Max() - allY.Min();
            }
        }

        var planeCount = 0;
        try
        {
            var trackables = _arSession?.GetAllTrackables(Java.Lang.Class.FromType(typeof(Plane)));
            if (trackables != null)
            {
                foreach (var t in trackables)
                    if (t is Plane p && p.TrackingState == TrackingState.Tracking && p.SubsumedBy == null)
                        planeCount++;
            }
        }
        catch { /* harmlos */ }

        // Sampler-State unter Lock lesen (K3-Fix)
        bool isSampling;
        float samplingProgress;
        lock (_samplerLock)
        {
            isSampling = _activeSampler != null;
            samplingProgress = isSampling
                ? (float)_samplesCollected / _effectiveMultiFrameSampleTargetCount
                : 0f;
        }

        // Validation nur EINMAL aufrufen — war vorher 2× pro Frame (teuer wegen Plane-Iteration)
        var (ready, checkList) = ValidatePreMeasureConditions();
        var stability = _stabilityMonitor?.StabilityScore ?? 1f;
        var anchorCount = _anchorManager.CountTracking();

        return new ArOverlayState
        {
            IsTracking = isTracking,
            TrackingFailureReason = failReason,
            ReticleX = _viewportWidth / 2f,
            ReticleY = _viewportHeight / 2f,
            HitQuality = _reticleHitQuality,
            HitDistanceMeters = _reticleHitDistance > 0 ? _reticleHitDistance : null,
            HitHeightDelta = _reticleHeightDelta,
            DetectedPlaneCount = planeCount,
            SessionSeconds = (long)(DateTime.UtcNow - _sessionStart).TotalSeconds,
            CompassHeading = _magneticHeading ?? 0f,
            LiveAreaSquareMeters = liveArea,
            LiveLengthMeters = liveLength,
            HeightRangeMeters = heightRange,
            AutoCloseTarget = autoClose,
            TransientHint = ConsumeTransientHint(),
            AnchorCount = anchorCount,
            StabilityScore = stability,
            IsSampling = isSampling,
            SamplingProgress = samplingProgress,
            IsReadyToMeasure = ready,
            ReadinessIssues = checkList,
            TrackingQualityScore = ArPrecisionHelpers.ComputeTrackingQualityScore(
                isTracking, planeCount, stability, _magneticAccuracy, anchorCount, GetAverageStdDev()),
            GroundPlaneY = GetGroundPlaneY(),
            MagneticAccuracy = _magneticAccuracy,
            TopInsetPixels = _topInsetPx,
            BottomInsetPixels = _bottomInsetPx,
            ThermalWarning = _thermalWarningText,
            BatteryWarning = _batteryWarningText,
            Labels = _overlayLabels,
            // Plan-Kap. 5.3: Tape-Measure-Daten fuer das Overlay
            TapeMeasureScreenPoints = BuildTapeMeasureSnapshot(),
            TapeMeasureSegmentMeters = BuildTapeMeasureSegmentMeters(),
            TapeMeasureTotalMeters = ComputeTapeMeasureTotalMetersLocked(),
            IsTapeMeasureMode = _captureMode == CaptureMode.TapeMeasure,
            // Live-Segment ("Gummiband") + Kontur-Segment-Distanzen
            ShowLiveSegment = showLiveSegment,
            LiveSegmentActive = liveSegActive,
            LiveSegmentOffScreenDirectionDeg = liveSegOffScreenDir,
            LiveSegmentFromScreen = liveSegFromScreen,
            LiveSegmentHorizontalMeters = liveSegHorizontal,
            LiveSegmentSlopeMeters = liveSegSlope,
            LiveSegmentHeightDelta = liveSegHeight,
            ActiveContourSegments = activeContourSegMeters,
            // Rechteck-/Quadrat-Vorschau (gefuehrte 3-Punkt-Methode)
            IsRectangleMode = isRectMode,
            RectangleCornerCount = rectCornerCount,
            RectangleCornerScreenPoints = rectCornerScreen,
            RectanglePreviewScreenPoints = rectPreviewScreen,
            RectangleIsSquare = rectIsSquare,
            RectangleLengthMeters = rectLen,
            RectangleDepthMeters = rectDepth,
            RectangleAreaMeters = rectArea,
            // Plan-Kap. 5.9: Stakeout-Daten fuer das Overlay
            IsStakeoutMode = _captureMode == CaptureMode.Stakeout,
            StakeoutDistanceMeters = ReadStakeoutDistance(),
            StakeoutRelativeBearingDeg = ReadStakeoutBearing(),
            StakeoutTargetLabel = ReadStakeoutLabel(),
            StakeoutReachedCount = _stakeoutTargets?.Count(t => t.IsReached) ?? 0,
            StakeoutTotalCount = _stakeoutTargets?.Count ?? 0,
            // Plan-Kap. 5.2: Site-Marker-Snapshot
            SiteMarkerScreenPoints = BuildSiteMarkerSnapshot(),
            // Plan-Kap. 5.8: RTK-Stab-Live-Position
            RtkStabScreenPos = _projectedRtkStab,
            RtkStabFixQuality = _rtkStabLastFixQuality,
            // Plan-Kap. 5.15: Quality-Heatmap (Snapshot der letzten Berechnung)
            QualityHeatmapGrid = _heatmapEnabled ? CloneHeatmapGrid() : null,
            QualityHeatmapCols = HeatmapCols,
            QualityHeatmapRows = HeatmapRows,
        };
    }

    /// <summary>Plan-Kap. 5.15: Defensive Kopie damit Overlay-View nicht parallel mit
    /// dem GL-Thread auf dem Live-Grid schreibt.</summary>
    private float[,] CloneHeatmapGrid()
    {
        var copy = new float[HeatmapCols, HeatmapRows];
        Array.Copy(_heatmapGrid, copy, _heatmapGrid.Length);
        return copy;
    }

    private IReadOnlyList<(float screenX, float screenY, string label)>? BuildSiteMarkerSnapshot()
    {
        if (_projectedSiteMarkersBuilder.Count == 0) return null;
        return new List<(float, float, string)>(_projectedSiteMarkersBuilder);
    }

    private double? ReadStakeoutDistance()
    {
        lock (_stakeoutSnapshotLock) return _stakeoutCurrentDistance;
    }
    private double? ReadStakeoutBearing()
    {
        lock (_stakeoutSnapshotLock) return _stakeoutCurrentRelativeBearingDeg;
    }
    private string? ReadStakeoutLabel()
    {
        lock (_stakeoutSnapshotLock) return _stakeoutCurrentTargetLabel;
    }

    /// <summary>Snapshot der projizierten Tape-Measure-Punkte (Plan-Kap. 5.3) — sicheres
    /// Cross-Thread-Read: BuildOverlayState laeuft auf GL-Thread, Builder wird in
    /// ProjectPointsToScreen direkt davor unter _dataLock befuellt.</summary>
    private IReadOnlyList<(float screenX, float screenY)>? BuildTapeMeasureSnapshot()
    {
        if (_projectedTapeMeasureBuilder.Count == 0) return null;
        return new List<(float, float)>(_projectedTapeMeasureBuilder);
    }

    private IReadOnlyList<float>? BuildTapeMeasureSegmentMeters()
    {
        lock (_dataLock)
        {
            if (_tapeMeasurePoints.Count < 2) return null;
            var segments = new List<float>(_tapeMeasurePoints.Count - 1);
            for (var i = 1; i < _tapeMeasurePoints.Count; i++)
                segments.Add(_tapeMeasurePoints[i].DistanceTo(_tapeMeasurePoints[i - 1]));
            return segments;
        }
    }

    private float ComputeTapeMeasureTotalMetersLocked()
    {
        lock (_dataLock) return ComputeTapeMeasureTotalMeters();
    }

    /// <summary>Aktuelle lokalisierte Overlay-Labels. Wird in OnCreate aus AppStrings
    /// gefuellt und bleibt fuer die Session konstant (Sprachwechsel mid-session passieren
    /// nicht — die Activity laeuft als Modal-Fullscreen). Plan-Kap. 4.11.</summary>
    private ArOverlayLabels _overlayLabels = ArOverlayLabels.GermanDefaults;

    /// <summary>Summe aller aufeinanderfolgenden Tape-Measure-Distanzen in Metern.
    /// Caller muss bereits unter <see cref="_dataLock"/> stehen.</summary>
    private float ComputeTapeMeasureTotalMeters()
    {
        if (_tapeMeasurePoints.Count < 2) return 0f;
        var total = 0f;
        for (var i = 1; i < _tapeMeasurePoints.Count; i++)
            total += _tapeMeasurePoints[i].DistanceTo(_tapeMeasurePoints[i - 1]);
        return total;
    }

    /// <summary>Plan-Kap. 5.9: Pro Frame Distanz + Bearing zum naechsten Stakeout-Target
    /// neu berechnen. Aktuelle Position aus Geospatial-Pose (bevorzugt) oder RTK-Snapshot;
    /// bei &lt;10cm wird Target.IsReached gesetzt und das naechste angesteuert.</summary>
    private void UpdateStakeout()
    {
        if (_stakeoutTargets == null || _stakeoutTargets.Count == 0) return;

        // Aktuelle Position: Geospatial-Pose zuerst (VPS), sonst RTK-Snapshot, sonst
        // die initialen GPS-Werte (Activity-Start) — die letzteren bringen aber wenig,
        // weil sie sich nicht aendern. Realistisch braucht Stakeout entweder Geospatial-
        // Tracking oder einen aktiven RTK-Rover.
        double? curLat = null, curLon = null;
        double? curHeadingDeg = null;

        var geoSnap = System.Threading.Volatile.Read(ref _lastGeoSnapshot);
        if (geoSnap != null)
        {
            curLat = geoSnap.Latitude;
            curLon = geoSnap.Longitude;
            curHeadingDeg = geoSnap.Heading;
        }
        else if (_bleService is { IsConnected: true } && _bleService.CurrentState.FixQuality >= 4)
        {
            var s = _bleService.GetStateSnapshot();
            curLat = s.Latitude;
            curLon = s.Longitude;
            curHeadingDeg = _magneticHeading;
        }

        if (!curLat.HasValue || !curLon.HasValue)
        {
            lock (_stakeoutSnapshotLock)
            {
                _stakeoutCurrentDistance = null;
                _stakeoutCurrentRelativeBearingDeg = null;
                _stakeoutCurrentTargetLabel = null;
            }
            return;
        }

        // Naechstes unerreichtes Target
        StakeoutTarget? target = null;
        foreach (var t in _stakeoutTargets)
        {
            if (!t.IsReached) { target = t; break; }
        }

        if (target == null)
        {
            lock (_stakeoutSnapshotLock)
            {
                _stakeoutCurrentDistance = null;
                _stakeoutCurrentRelativeBearingDeg = null;
                _stakeoutCurrentTargetLabel = null;
            }
            return;
        }

        var coords = App.Services?.GetService<ICoordinateService>();
        if (coords == null) return;

        var distanceM = coords.HaversineDistance(curLat.Value, curLon.Value, target.Latitude, target.Longitude);
        var bearingTrue = coords.GetBearing(curLat.Value, curLon.Value, target.Latitude, target.Longitude);

        // Relative Pfeil-Richtung = Bearing zum Target minus aktuelles Heading (Camera-Forward = "vorne")
        double? relBearing = null;
        if (curHeadingDeg.HasValue)
        {
            relBearing = bearingTrue - curHeadingDeg.Value;
            // Normalisieren auf -180..180
            while (relBearing > 180) relBearing -= 360;
            while (relBearing < -180) relBearing += 360;
        }

        // Best-Distance fortschreiben
        if (distanceM < target.BestDistance) target.BestDistance = distanceM;

        // Reached-Detection mit Hysterese: erst feuern wenn der User von "weit weg"
        // (>= 30cm) auf "<=10cm" geht — verhindert Wackler an der Schwelle.
        if (_stakeoutLastDistance > 0.30 && distanceM <= StakeoutReachedThresholdMeters)
        {
            target.IsReached = true;
            // Hysterese für das NÄCHSTE Target zurücksetzen: ein direkt benachbartes Ziel
            // (bereits < 30 cm — im Vermessungs-Kontext der Normalfall) würde sonst klemmen,
            // weil _stakeoutLastDistance auf ~10 cm steht und die ">30 cm → <10 cm"-Bedingung
            // erst nach Weglaufen + Zurück greift.
            _stakeoutLastDistance = double.PositiveInfinity;
            RunOnUiThread(() =>
            {
                VibrateMedium();
                ShowTransientHint($"{target.Label} erreicht! (Distanz {distanceM * 100:F1} cm)");
            });
        }
        else
        {
            _stakeoutLastDistance = distanceM;
        }

        lock (_stakeoutSnapshotLock)
        {
            _stakeoutCurrentDistance = distanceM;
            _stakeoutCurrentRelativeBearingDeg = relBearing;
            _stakeoutCurrentTargetLabel = target.Label;
        }
    }

    /// <summary>Plan-Kap. 5.15: Echte Per-Patch-FeaturePoint-Density. Iteriert getrackte
    /// Point-Trackables, projiziert sie in Screen-Koordinaten und akkumuliert pro Patch.
    /// Kombiniert mit Plane-Coverage (geringerer Gewicht — Planes sind sekundaer) und
    /// globalem Quality-Score als Daempfung.</summary>
    private void UpdateQualityHeatmap()
    {
        if (_viewportWidth <= 0 || _viewportHeight <= 0) return;
        if (_arSession == null) return;

        // Patches initial mit 0 (= Rot/schlecht) belegen
        for (var c = 0; c < HeatmapCols; c++)
            for (var r = 0; r < HeatmapRows; r++)
                _heatmapGrid[c, r] = 0f;

        var patchW = _viewportWidth / (float)HeatmapCols;
        var patchH = _viewportHeight / (float)HeatmapRows;

        // Feature-Points iterieren — primaere Datenquelle laut Plan
        try
        {
            var pointTrackables = _arSession.GetAllTrackables(
                Java.Lang.Class.FromType(typeof(global::Google.AR.Core.Point)));
            if (pointTrackables != null)
            {
                foreach (var t in pointTrackables)
                {
                    if (t is not global::Google.AR.Core.Point pt) continue;
                    if (pt.TrackingState != TrackingState.Tracking) continue;
                    var pose = pt.Pose;
                    if (pose == null) continue;

                    var arPoint = new ArPoint { X = pose.Tx(), Y = pose.Ty(), Z = pose.Tz() };
                    var screen = WorldToScreen(arPoint, _mvpMatrixScratch);
                    if (!screen.HasValue) continue;

                    var col = (int)(screen.Value.x / patchW);
                    var row = (int)(screen.Value.y / patchH);
                    if (col < 0 || col >= HeatmapCols || row < 0 || row >= HeatmapRows) continue;
                    // 0.1 pro Feature-Point — ein guter Patch mit ~10 Features kommt auf 1.0
                    _heatmapGrid[col, row] = MathF.Min(1f, _heatmapGrid[col, row] + 0.10f);
                }
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"FeaturePoint-Heatmap failed: {ex.Message}");
        }

        // Plane-Coverage als zusaetzliches Boost (Planes deuten auf flaches, mess-freundliches
        // Terrain hin — geringerer Gewicht weil Feature-Points die primaere Qualitaets-Quelle sind)
        var planes = _overlayView?.GetProjectedPlanesSnapshot();
        if (planes != null)
        {
            foreach (var poly in planes)
            {
                foreach (var (px, py) in poly)
                {
                    var col = (int)(px / patchW);
                    var row = (int)(py / patchH);
                    if (col < 0 || col >= HeatmapCols || row < 0 || row >= HeatmapRows) continue;
                    _heatmapGrid[col, row] = MathF.Min(1f, _heatmapGrid[col, row] + 0.15f);
                }
            }
        }

        // Globaler Tracking-Quality-Score als Daempfung (0..100 → 0..1)
        var stability = _stabilityMonitor?.StabilityScore ?? 1f;
        var anchorCount = _anchorManager.CountTracking();
        var qualityScore = ArPrecisionHelpers.ComputeTrackingQualityScore(
            isTracking: true, planeCount: 5, stabilityScore: stability,
            magAccuracy: _magneticAccuracy, anchorCount: anchorCount,
            avgPositionStdDev: GetAverageStdDev()) / 100f;

        for (var c = 0; c < HeatmapCols; c++)
            for (var r = 0; r < HeatmapRows; r++)
                _heatmapGrid[c, r] *= qualityScore;
    }

    /// <summary>Plan-Kap. 5.8: Refresht den Earth-Anchor des RTK-Stabs an seiner aktuellen
    /// BLE-Position. Loescht den alten Anchor (Anchor-Hard-Limit) und erzeugt einen neuen.
    /// Wird einmal pro Sekunde gerufen (30 Frames). _rtkStabAnchor.GeoLat/Lon/Alt bleiben
    /// als Tooltip-Quelle erhalten. Wenn kein RTK-Fix → Anchor wird entfernt.</summary>
    private void UpdateRtkStabAnchor()
    {
        if (_bleService is not { IsConnected: true })
        {
            ClearRtkStabAnchor();
            return;
        }
        var state = _bleService.GetStateSnapshot();
        if (state.FixQuality < 1 || !state.Latitude.HasValue || !state.Longitude.HasValue)
        {
            ClearRtkStabAnchor();
            return;
        }
        var stabAltNn = state.Altitude ?? 0.0;

        var earth = _arSession?.Earth;
        if (earth == null || earth.TrackingState != TrackingState.Tracking) return;

        // Alten Anchor freigeben (analog Anchor-Lifecycle in CloseActiveContour)
        if (_rtkStabAnchor != null && !string.IsNullOrEmpty(_rtkStabAnchor.AnchorId))
        {
            _anchorManager.Detach(_rtkStabAnchor.AnchorId);
            _rtkStabAnchor.AnchorId = null;
        }

        var newMarker = new ArPoint
        {
            Label = $"Stab ({FixQualityLabel(state.FixQuality)})",
            Confidence = state.FixQuality >= 4 ? 0.95f : 0.6f,
            HitQuality = 3,
            GeoLatitude = state.Latitude,
            GeoLongitude = state.Longitude,
            GeoAltitude = state.Altitude,
            Timestamp = DateTime.UtcNow,
        };

        // Stab-Altitude bereits NN-korrigiert von BleService.OnCharacteristicChanged.
        // Earth.CreateAnchor erwartet Ellipsoid → grobe 48m-Naehrung (DE) wieder addieren.
        var ellipsoidAlt = stabAltNn + 48.0;
        if (_anchorManager.TryCreateEarthAnchor(earth, state.Latitude.Value, state.Longitude.Value,
            ellipsoidAlt, newMarker))
        {
            lock (_dataLock) _rtkStabAnchor = newMarker;
            _rtkStabLastFixQuality = state.FixQuality;
        }
    }

    private void ClearRtkStabAnchor()
    {
        if (_rtkStabAnchor == null) return;
        if (!string.IsNullOrEmpty(_rtkStabAnchor.AnchorId))
            _anchorManager.Detach(_rtkStabAnchor.AnchorId);
        lock (_dataLock) _rtkStabAnchor = null;
        _rtkStabLastFixQuality = 0;
    }

    private static string FixQualityLabel(int fix) => fix switch
    {
        4 => "RTK-Fix",
        5 => "RTK-Float",
        2 => "DGPS",
        1 => "GPS",
        _ => "?",
    };

    /// <summary>Plan-Kap. 5.7: Per Frame die getrackten Augmented-Images abfragen. Wenn
    /// ein bekannter Marker (Image-Name in <see cref="_markersByImageName"/>) im Status
    /// Tracking gefunden wird, erzeugen wir EINMAL einen Earth-Anchor an der
    /// vorab eingemessenen RTK-Position. Damit ist das Vermessungs-Koordinatensystem im
    /// AR-Frame physisch verankert — der Marker dient als reale Referenz, das Anchor
    /// haelt die Position drift-frei (via VPS).</summary>
    private readonly HashSet<string> _markersAlreadyLocalized = [];
    private void UpdateAugmentedImageRecognition()
    {
        if (_arSession == null) return;
        var earth = _arSession.Earth;

        try
        {
            var trackables = _arSession.GetAllTrackables(
                Java.Lang.Class.FromType(typeof(global::Google.AR.Core.AugmentedImage)));
            if (trackables == null) return;

            foreach (var t in trackables)
            {
                if (t is not global::Google.AR.Core.AugmentedImage img) continue;
                if (img.TrackingState != TrackingState.Tracking) continue;

                var imageName = img.Name ?? string.Empty;
                if (string.IsNullOrEmpty(imageName)) continue;
                if (!_markersByImageName.TryGetValue(imageName, out var marker)) continue;
                if (_markersAlreadyLocalized.Contains(imageName)) continue;

                // Marker neu lokalisiert — Earth-Anchor an seiner vorab eingemessenen
                // Position erzeugen. Visualisierung erfolgt via Site-Marker-Layer.
                if (earth == null || earth.TrackingState != TrackingState.Tracking) continue;

                var markerPoint = new ArPoint
                {
                    Label = $"Marker: {marker.Name}",
                    Confidence = 0.98f,
                    HitQuality = 3,
                    GeoLatitude = marker.Latitude,
                    GeoLongitude = marker.Longitude,
                    GeoAltitude = marker.Altitude,
                    GeoHorizontalAccuracy = marker.AccuracyCm / 100f,
                    Timestamp = DateTime.UtcNow,
                };
                var ellipsoidAlt = marker.Altitude + 48.0;
                if (_anchorManager.TryCreateEarthAnchor(earth, marker.Latitude, marker.Longitude,
                    ellipsoidAlt, markerPoint))
                {
                    lock (_dataLock) _sitePointAnchors.Add(markerPoint);
                    _markersAlreadyLocalized.Add(imageName);
                    RunOnUiThread(() =>
                    {
                        ShowTransientHint($"Marker erkannt: {marker.Name}");
                        VibrateMedium();
                    });
                }
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture",
                $"AugmentedImage-Recognition fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>Plan-Kap. 5.2: Erzeugt iterativ Earth-Anchors fuer alle bestehenden
    /// SurveyPoints aus dem Site-Points-Snapshot. Wird im OnDrawFrame aufgerufen sobald
    /// Geospatial-Tracking aktiv ist. Max <paramref name="maxPerFrame"/> Anchors pro
    /// Frame, damit auch 100+ Punkte den Render-Loop nicht blocken. Die erzeugten
    /// ArPoint-Marker landen in <see cref="_sitePointAnchors"/>, RefreshAllAnchors haelt
    /// ihre Position drift-kompensiert.</summary>
    private void CreatePendingSiteAnchors(int maxPerFrame)
    {
        var siteSnap = _sitePoints;
        if (siteSnap == null || _siteAnchorsCreated >= siteSnap.Count) return;
        if (_arSession == null) return;
        var earth = _arSession.Earth;
        if (earth == null || earth.TrackingState != TrackingState.Tracking) return;

        var created = 0;
        while (_siteAnchorsCreated < siteSnap.Count && created < maxPerFrame)
        {
            var sp = siteSnap[_siteAnchorsCreated];
            // Site-Marker als reduzierter ArPoint — HitQuality=3 (Plane-aequivalent, weil
            // RTK-Source), Confidence vom GPS abhaengig. Label "Site: <Original-Label>".
            var marker = new ArPoint
            {
                Label = string.IsNullOrEmpty(sp.Label) ? $"Site #{sp.Id}" : $"Site: {sp.Label}",
                Confidence = sp.HorizontalAccuracy < 5 ? 0.95f : 0.7f,
                HitQuality = 3,
                GeoLatitude = sp.Latitude,
                GeoLongitude = sp.Longitude,
                GeoAltitude = sp.Altitude,
                GeoHorizontalAccuracy = sp.HorizontalAccuracy / 100f,
                Timestamp = sp.Timestamp,
            };

            // Geoid-Korrektur: SurveyPoint.Altitude ist NN, Earth.CreateAnchor erwartet
            // WGS84-Ellipsoid. Wir invertieren die ArTransferService-Korrektur grob —
            // pragmatisch ohne IGeoidService-Lookup (Konstante 48m fuer DE als Naehrung,
            // siehe IGeoidService Pauschal-Fallback).
            var ellipsoidAlt = sp.Altitude + 48.0;

            if (_anchorManager.TryCreateEarthAnchor(earth, sp.Latitude, sp.Longitude,
                ellipsoidAlt, marker))
            {
                lock (_dataLock) _sitePointAnchors.Add(marker);
                created++;
                _siteAnchorsCreated++;
            }
            else
            {
                // Anchor-Erstellung transient fehlgeschlagen (Earth kurz instabil oder Hard-Limit
                // momentan erreicht) — diesen Site-Punkt NICHT überspringen, sondern im nächsten
                // Frame erneut versuchen. Vorher sprang der Zähler trotzdem weiter → Marker dauerhaft weg.
                break;
            }
        }
    }

    /// <summary>Plan-Kap. 5.6: Foto vom aktuellen Kamera-Frame asynchron erstellen und
    /// in <c>IAppPaths.PhotosFolder</c> als JPEG ablegen. Setzt sofort <see cref="ArPoint.PhotoPath"/>
    /// auf den geplanten Dateinamen — auch wenn der eigentliche Disk-Write asynchron
    /// laeuft. Der PDF-Bericht prueft beim Render File.Exists.</summary>
    private void CapturePhotoForPoint(ArPoint point)
    {
        var glView = _glSurfaceView;
        if (glView == null || glView.Width <= 0 || glView.Height <= 0) return;

        IAppPaths? paths = null;
        try { paths = App.AppPathsFactory?.Invoke(); }
        catch { return; }
        if (paths == null) return;

        var fileName = $"pt_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.jpg";
        var fullPath = System.IO.Path.Combine(paths.PhotosFolder, fileName);

        // ArPoint sofort markieren — Async-Write soll Modell nicht blocken
        point.PhotoPath = fileName;

        global::Android.Graphics.Bitmap? bitmap;
        try
        {
            bitmap = global::Android.Graphics.Bitmap.CreateBitmap(
                glView.Width, glView.Height, global::Android.Graphics.Bitmap.Config.Argb8888!);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"PhotoCapture: Bitmap-Alloc failed: {ex.Message}");
            return;
        }
        if (bitmap == null) return;

        var handlerThread = new HandlerThread("PhotoCapture-ArPoint");
        handlerThread.Start();
        var handler = new Handler(handlerThread.Looper!);

        try
        {
            PixelCopy.Request(glView, bitmap, new PixelCopyListener(result =>
            {
                try
                {
                    if (result != (int)PixelCopyResult.Success)
                    {
                        global::Android.Util.Log.Warn("ArCapture",
                            $"PhotoCapture: PixelCopy result {result}");
                        return;
                    }

                    // JPEG-Quality 80 ist ein guter Kompromiss zwischen Groesse (~200 KB
                    // bei 1080p) und Detail. Plan-Kap. 5.6 nennt 1MB pro Punkt als Budget.
                    using var fs = System.IO.File.OpenWrite(fullPath);
                    bitmap.Compress(global::Android.Graphics.Bitmap.CompressFormat.Jpeg!, 80, fs);
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Warn("ArCapture",
                        $"PhotoCapture: JPEG-Save failed: {ex.Message}");
                }
                finally
                {
                    try { bitmap.Recycle(); } catch { }
                    try { bitmap.Dispose(); } catch { }
                    handlerThread.QuitSafely();
                }
            }), handler);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture",
                $"PhotoCapture: PixelCopy-Request failed: {ex.Message}");
            try { bitmap.Recycle(); bitmap.Dispose(); } catch { }
            handlerThread.QuitSafely();
        }
    }

    /// <summary>Plan-Kap. 5.17: Total-Station-Modus aktivieren. Stationiert das Phone an
    /// der aktuellen RTK-Stab-Position (wenn vorhanden) oder zeigt einen Hint dass kein
    /// Stab connected ist.</summary>
    private void ToggleTotalStationMode()
    {
        var ts = App.Services?.GetService<ITotalStationService>();
        if (ts == null)
        {
            ShowTransientHint("Total-Station-Service nicht verfuegbar");
            return;
        }

        // Origin = aktuelle RTK-Stab-Position (gestaubt durch BLE)
        if (_bleService is not { IsConnected: true })
        {
            ShowTransientHint("Stab nicht verbunden — Total-Station braucht RTK-Anker");
            return;
        }
        var s = _bleService.GetStateSnapshot();
        if (s.FixQuality < 4 || !s.Latitude.HasValue || !s.Longitude.HasValue)
        {
            ShowTransientHint("Kein RTK-Fix am Stab — bitte warten oder NTRIP pruefen");
            return;
        }

        // Heading aus Geospatial-Pose oder Magnetometer
        var geoSnap = System.Threading.Volatile.Read(ref _lastGeoSnapshot);
        var heading = geoSnap?.Heading ?? _magneticHeading ?? 0f;

        ts.SetStationOrigin(s.Latitude.Value, s.Longitude.Value, s.Altitude ?? 0.0, heading);
        SetMode(CaptureMode.TotalStation);
        ShowTransientHint($"Stationiert ({s.Latitude.Value:F6}, {s.Longitude.Value:F6})");
        VibrateMedium();
    }

    /// <summary>Plan-Kap. 5.17: Im Total-Station-Modus berechnet PlaceNewPoint den
    /// Ziel-Punkt nicht via ARCore-HitTest, sondern radial vom Stativ-Origin aus —
    /// Distanz aus Depth-API, Bearing aus ARCore-Camera-Heading, Pitch aus ARCore-
    /// Camera-Pitch. Liefert direkt einen ArPoint mit Geo-Position.</summary>
    private ArPoint? PlaceTotalStationPoint(float screenX, float screenY)
    {
        var ts = App.Services?.GetService<ITotalStationService>();
        if (ts?.Station == null)
        {
            ShowTransientHint("Bitte zuerst stationieren (Tachy aktivieren)");
            return null;
        }

        Frame? frame;
        lock (_frameLock) frame = _lastFrame;
        if (frame == null) return null;

        var depth = ArPrecisionHelpers.TryGetDepthMeters(
            frame, screenX, screenY, _viewportWidth, _viewportHeight);
        if (!depth.HasValue)
        {
            ShowTransientHint("Keine Depth-Daten am Reticle — naeher heran/anders zielen");
            return null;
        }

        var camPose = frame.Camera?.Pose;
        if (camPose == null) return null;
        var q = camPose.GetRotationQuaternion();
        if (q == null || q.Length < 4) return null;

        var relHeading = ArMathHelpers.ExtractHeadingFromQuaternion(q[0], q[1], q[2], q[3]) ?? 0f;
        var pitch = ArMathHelpers.ExtractPitchFromQuaternion(q[0], q[1], q[2], q[3]);

        // Bearing relativ zur Stativ-Heading: ARCore-Heading absolut → relative Differenz
        var (lat, lon, alt) = ts.ProjectTarget(depth.Value, relHeading - (float)ts.Station.Value.headingDeg, pitch);

        return new ArPoint
        {
            // X/Y/Z bleiben 0 — fuer Total-Station-Punkte nicht relevant (kein Anchor noetig)
            GeoLatitude = lat,
            GeoLongitude = lon,
            GeoAltitude = alt,
            GeoHorizontalAccuracy = (float)(depth.Value * 0.02), // ~2% von Distanz als Schaetzung
            Confidence = 0.85f,
            HitQuality = 2,
            CameraPitchDeg = pitch,
            MagAccuracyAtCapture = _magneticAccuracy,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>Plan-Kap. 5.3: Tape-Buffer leeren — Long-Click auf den Mass-Button.</summary>
    private void ResetTapeMeasure()
    {
        lock (_dataLock) _tapeMeasurePoints.Clear();
        ShowTransientHint("Maßband zurückgesetzt");
        _overlayView?.Invalidate();
    }

    /// <summary>Liest die AR-spezifischen Strings aus AppStrings und baut den
    /// Snapshot fuer ArOverlayState.Labels.</summary>
    private static ArOverlayLabels LoadLocalizedLabels()
    {
        // AppStrings ist statisch + bedient sich an Thread.CurrentUICulture, die zu Activity-
        // Start bereits auf die User-Sprache gesetzt ist (MainActivity → LocalizationService).
        // ResourceManager liefert deutsche Defaults wenn ein Key in einer Sprache fehlt.
        var a = SmartMeasure.Shared.Resources.Strings.AppStrings.ArStatPoints;
        if (string.IsNullOrEmpty(a)) return ArOverlayLabels.GermanDefaults;

        return new ArOverlayLabels(
            Points: SmartMeasure.Shared.Resources.Strings.AppStrings.ArStatPoints,
            Area: SmartMeasure.Shared.Resources.Strings.AppStrings.ArStatArea,
            Length: SmartMeasure.Shared.Resources.Strings.AppStrings.ArStatLength,
            HeightDelta: SmartMeasure.Shared.Resources.Strings.AppStrings.ArStatHeightDelta,
            Anchors: SmartMeasure.Shared.Resources.Strings.AppStrings.ArStatAnchors,
            Time: SmartMeasure.Shared.Resources.Strings.AppStrings.ArStatTime,
            HoldStill: SmartMeasure.Shared.Resources.Strings.AppStrings.ArReticleHoldStill,
            Ready: SmartMeasure.Shared.Resources.Strings.AppStrings.ArBadgeReady,
            TrackingLost: SmartMeasure.Shared.Resources.Strings.AppStrings.ArTrackingLost,
            TrackingInsufficientLight: SmartMeasure.Shared.Resources.Strings.AppStrings.ArTrackingInsufficientLight,
            TrackingInsufficientFeatures: SmartMeasure.Shared.Resources.Strings.AppStrings.ArTrackingInsufficientFeatures,
            TrackingExcessiveMotion: SmartMeasure.Shared.Resources.Strings.AppStrings.ArTrackingExcessiveMotion,
            TrackingCameraUnavailable: SmartMeasure.Shared.Resources.Strings.AppStrings.ArTrackingCameraUnavailable,
            TrackingBadState: SmartMeasure.Shared.Resources.Strings.AppStrings.ArTrackingBadState);
    }

    private bool _wasTrackingLastFrame = true;

    /// <summary>Gibt Vibrations-Feedback wenn Tracking verloren geht (nur bei Übergang).</summary>
    private void CheckTrackingTransition(bool isTracking)
    {
        if (_wasTrackingLastFrame && !isTracking)
            VibrateWarning();
        _wasTrackingLastFrame = isTracking;
    }

    #endregion

    protected override void OnDestroy()
    {
        // Aktive Sensor-Listener garantiert abmelden, auch wenn PostDelayed-Cleanups
        // noch nicht abgelaufen sind (sonst Sensor-Leak + Battery-Drain).
        if (_sensorManager != null)
        {
            foreach (var listener in _activeSensorListeners.ToArray())
            {
                try { _sensorManager.UnregisterListener(listener); } catch { /* OK */ }
            }
            _activeSensorListeners.Clear();
        }
        foreach (var (mgr, listener) in _activeLocationListeners.ToArray())
        {
            try { mgr.RemoveUpdates(listener); } catch { /* OK */ }
        }
        _activeLocationListeners.Clear();
        StopFusedLocationUpdates();

        // RTK-PositionUpdated-Handler abmelden (Plan 3.1) — sonst hält der BleService
        // die Lambda-Referenz auf diese Activity und verhindert GC.
        if (_bleService != null && _rtkPositionHandler != null)
        {
            try { _bleService.PositionUpdated -= _rtkPositionHandler; } catch { /* OK */ }
            _rtkPositionHandler = null;
        }

        // Präzisions-Manager freigeben BEVOR Session geschlossen wird
        // (Anchors halten Session-Referenz)
        _stabilityMonitor?.Dispose();
        _stabilityMonitor = null;
        _anchorManager.Dispose();

        // Pending Frame-Ops verwerfen — Caller-Threads warten mit Timeout, kein Hang.
        lock (_pendingFrameOps) _pendingFrameOps.Clear();

        // OpenGL-Ressourcen aufraeumen. NICHT via QueueEvent — nach OnPause (läuft immer vor
        // OnDestroy) ist der GL-Thread pausiert und arbeitet seine Queue erst beim nächsten
        // OnResume ab, das nie kommt → der Java-NIO-Direct-Buffer im Renderer würde leaken.
        // Daher synchron; die GL-Delete-Calls sind gegen den fehlenden EGL-Kontext abgesichert.
        if (_cameraTextureId != 0)
        {
            try { GLES20.GlDeleteTextures(1, new[] { _cameraTextureId }, 0); }
            catch { /* kein aktiver GL-Kontext mehr */ }
            _cameraTextureId = 0;
        }
        _backgroundRenderer?.Dispose();
        _backgroundRenderer = null;

        _arSession?.Close();
        _arSession = null;

        // MediaActionSound freigeben (sonst Audio-Resource-Leak)
        if (_shutterLoaded)
        {
            try { _shutterSound?.Release(); } catch { /* OK */ }
            _shutterSound = null;
            _shutterLoaded = false;
        }

        base.OnDestroy();
    }

    private enum CaptureMode
    {
        Point,
        Contour,
        /// <summary>Ad-hoc-Messmodus (Apple-Measure-Klon, Plan-Kap. 5.3): Punkte werden
        /// in einem separaten Buffer gehalten, NICHT ins Projekt uebertragen. Polylinie
        /// + Distanz-Labels zwischen Punkten + Gesamtsumme im Footer.</summary>
        TapeMeasure,
        /// <summary>Plan-Kap. 5.9: Absteck-Modus. Zeigt Pfeil + Distanz zum naechsten
        /// unerreichten Stakeout-Target. Bei &lt;10cm wird das Target als erreicht markiert.</summary>
        Stakeout,
        /// <summary>Plan-Kap. 5.17: Total-Station-Modus. Phone auf Stativ ueber RTK-Stab.
        /// Reticle-Hit liefert via Depth-API + ARCore-Heading die Ziel-Lat/Lon ueber
        /// <see cref="ITotalStationService"/>.</summary>
        TotalStation,
        /// <summary>Gefuehrte 3-Punkt-Rechteck-/Quadrat-Erfassung: zwei Tipps spannen die
        /// Basiskante auf, der dritte legt die Tiefe fest. <see cref="ArRectangleBuilder"/>
        /// erzwingt rechte Winkel im Grundriss und snappt bei Bedarf auf ein Quadrat.</summary>
        Rectangle
    }

    #region Undo/Redo Actions

    private interface IArAction
    {
        void Undo();
        void Redo();
    }

    // Undo/Redo-Actions sperren über _dataLock damit Render-Thread nicht während
    // List-Mutation iteriert. Vorher: Race-Crash mit IndexOutOfRangeException möglich.
    private sealed class AddPointAction(object lockObj, List<ArPoint> list, ArPoint point) : IArAction
    {
        public void Undo() { lock (lockObj) list.Remove(point); }
        public void Redo() { lock (lockObj) list.Add(point); }
    }

    private sealed class DeletePointAction(object lockObj, List<ArPoint> list, int index, ArPoint point) : IArAction
    {
        public void Undo() { lock (lockObj) list.Insert(Math.Min(index, list.Count), point); }
        public void Redo() { lock (lockObj) list.Remove(point); }
    }

    private sealed class AddContourPointAction(object lockObj, ArContour contour, ArPoint point) : IArAction
    {
        // IsClosed-Schutz: Nach Kontur-Schluss (Bowditch-korrigiert) darf ein Undo keinen Punkt
        // mehr aus der fertigen Fläche entfernen/einfügen — das würde sie korrumpieren. Die ganze
        // geschlossene Kontur wird stattdessen über die AddContourAction zurückgenommen.
        public void Undo() { lock (lockObj) { if (!contour.IsClosed) contour.Points.Remove(point); } }
        public void Redo() { lock (lockObj) { if (!contour.IsClosed) contour.Points.Add(point); } }
    }

    // Eine komplett gesetzte Kontur (z.B. ein abgeschlossenes Rechteck) als Ganzes
    // zuruecknehmen/wiederherstellen. Redo fuegt nur ein, wenn nicht schon vorhanden.
    private sealed class AddContourAction(object lockObj, List<ArContour> list, ArContour contour) : IArAction
    {
        public void Undo() { lock (lockObj) list.Remove(contour); }
        public void Redo() { lock (lockObj) { if (!list.Contains(contour)) list.Add(contour); } }
    }

    private sealed class DeleteContourPointAction(object lockObj, ArContour contour, int index, ArPoint point) : IArAction
    {
        // IsClosed-Schutz analog AddContourPointAction — keine Punkt-Mutation an einer fertigen Kontur.
        public void Undo() { lock (lockObj) { if (!contour.IsClosed) contour.Points.Insert(Math.Min(index, contour.Points.Count), point); } }
        public void Redo() { lock (lockObj) { if (!contour.IsClosed) contour.Points.Remove(point); } }
    }

    private sealed class MovePointAction(ArPoint point,
        float oldX, float oldY, float oldZ,
        float newX, float newY, float newZ,
        string? oldAnchorId, string? newAnchorId) : IArAction
    {
        // AnchorId mitrestaurieren — sonst zieht RefreshAllAnchors den Punkt im nächsten Frame
        // wieder auf die Move-Position (Undo wirkte ohne das nur einen Frame lang).
        public void Undo() { point.X = oldX; point.Y = oldY; point.Z = oldZ; point.AnchorId = oldAnchorId; }
        public void Redo() { point.X = newX; point.Y = newY; point.Z = newZ; point.AnchorId = newAnchorId; }
    }

    /// <summary>
    /// Stack mit FIFO-Cap: bei Überschreitung verfallen die ältesten Einträge stillschweigend.
    /// Verwenden wir für den Undo-Stack, damit lange Sessions nicht hunderte ArPoint-Referenzen
    /// + Lock-Objekte halten. <see cref="Push"/>/<see cref="Pop"/>/<see cref="Count"/>/<see cref="Clear"/>
    /// sind API-kompatibel zu <see cref="Stack{T}"/>.
    /// </summary>
    private sealed class BoundedStack<T>(int maxSize)
    {
        private readonly LinkedList<T> _list = new();
        public int Count => _list.Count;

        public void Push(T value)
        {
            _list.AddFirst(value);
            // O(1) Drop des ältesten Eintrags wenn Limit überschritten
            while (_list.Count > maxSize && _list.Last != null)
                _list.RemoveLast();
        }

        public T Pop()
        {
            if (_list.First == null)
                throw new InvalidOperationException("BoundedStack ist leer");
            var v = _list.First.Value;
            _list.RemoveFirst();
            return v;
        }

        public void Clear() => _list.Clear();
    }

    #endregion
}
