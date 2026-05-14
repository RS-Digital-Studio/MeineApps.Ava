using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Cinematic-Director : Zeitgesteuerter Effekt-Sequenzer für AAA-Boss-Reveals
/// und Big-Win-Sequenzen. Wird in GameEngine.Update mit deltaTime tick'ed,
/// erlaubt ein-Frame-Effekte (Particle-Burst), Continuous-Effekte (Camera-Zoom)
/// und Audio-Cues über Action-Callbacks.
///
/// Design: Lightweight-Sequencer mit ordered Event-List statt Full-Timeline-Tools.
/// Effekt-Owner (GameEngine) registriert Callbacks, Sequencer ruft sie at-time auf.
/// </summary>
public sealed class CinematicSequencer
{
    /// <summary>Ein zeitgesteuerter Event in einer Sequence.</summary>
    public readonly struct TimedEvent
    {
        /// <summary>Sekunde im Sequence-Zeitraum (0 = Start).</summary>
        public readonly float TriggerSeconds;

        /// <summary>Beliebige Aktion. Wird einmalig bei TriggerSeconds aufgerufen.</summary>
        public readonly Action Action;

        public TimedEvent(float triggerSeconds, Action action)
        {
            TriggerSeconds = triggerSeconds;
            Action = action;
        }
    }

    private readonly List<TimedEvent> _events = new();
    private float _elapsed;
    private float _duration;
    private int _nextEventIndex;
    private bool _isPlaying;

    /// <summary>
    /// v2.0.47 — Optional Camera-Zoom-Effekt während der Sequence.
    /// Wenn > 0 wird vom GameRenderer ein Canvas-Scale um Pivot angewendet.
    /// Animations-Pattern: 0 → MaxZoom → 0 (Triangle-Wave über Progress 0-1).
    /// </summary>
    public float MaxCameraZoom { get; set; }

    /// <summary>Pivot-Position in Welt-Koordinaten für Camera-Zoom.</summary>
    public float ZoomPivotX { get; set; }
    public float ZoomPivotY { get; set; }

    /// <summary>
    /// Aktueller Zoom-Faktor basierend auf Progress.
    /// 0 = kein Zoom, sonst MaxCameraZoom × Triangle-Wave(progress).
    /// Triangle-Wave: peak bei progress=0.5, zurück auf 0 bei progress=1.
    /// </summary>
    public float CurrentZoomFactor
    {
        get
        {
            if (!_isPlaying || MaxCameraZoom <= 0) return 0f;
            // Triangle-Wave 0→1→0 mit Ease-In-Out
            float p = Math.Clamp(_elapsed / _duration, 0f, 1f);
            float t = p < 0.5f ? p * 2f : (1f - p) * 2f;
            // Ease-In-Out (smoothstep)
            t = t * t * (3f - 2f * t);
            return MaxCameraZoom * t;
        }
    }

    /// <summary>Ob aktuell eine Sequence läuft.</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>Aktueller Fortschritt 0-1 oder 0 wenn nicht aktiv.</summary>
    public float Progress => _duration > 0 && _isPlaying ? Math.Clamp(_elapsed / _duration, 0f, 1f) : 0f;

    /// <summary>Verbleibende Sekunden oder 0 wenn nicht aktiv.</summary>
    public float TimeRemaining => _isPlaying ? Math.Max(0f, _duration - _elapsed) : 0f;

    /// <summary>
    /// Startet eine neue Sequence. Existierende Sequence wird abgebrochen.
    /// Events müssen nach TriggerSeconds aufsteigend sortiert sein.
    /// </summary>
    public void Play(float durationSeconds, IReadOnlyList<TimedEvent> events)
    {
        _events.Clear();
        _events.AddRange(events);
        // Robustheit: Sortieren falls Caller nicht sortiert
        _events.Sort((a, b) => a.TriggerSeconds.CompareTo(b.TriggerSeconds));
        _duration = durationSeconds;
        _elapsed = 0;
        _nextEventIndex = 0;
        _isPlaying = true;
    }

    /// <summary>Bricht die aktuelle Sequence sofort ab.</summary>
    public void Stop()
    {
        _isPlaying = false;
        _events.Clear();
        _nextEventIndex = 0;
        _elapsed = 0;
        _duration = 0;
        MaxCameraZoom = 0;
        ZoomPivotX = 0;
        ZoomPivotY = 0;
    }

    /// <summary>Pro Frame aufrufen — feuert fällige Events + beendet Sequence am Ende.</summary>
    public void Update(float deltaTime)
    {
        if (!_isPlaying) return;

        _elapsed += deltaTime;

        // Alle fälligen Events feuern (auch mehrere im selben Frame möglich)
        while (_nextEventIndex < _events.Count && _events[_nextEventIndex].TriggerSeconds <= _elapsed)
        {
            try { _events[_nextEventIndex].Action(); }
            catch { /* Best-effort: ein einzelner Effekt-Fehler darf die Sequence nicht abbrechen */ }
            _nextEventIndex++;
        }

        if (_elapsed >= _duration)
        {
            Stop();
        }
    }
}
