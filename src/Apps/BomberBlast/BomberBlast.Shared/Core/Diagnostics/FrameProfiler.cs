using System.Diagnostics;
using System.Globalization;

namespace BomberBlast.Core.Diagnostics;

/// <summary>
/// Leichtgewichtiger Frame-Profiler zur LIVE-Diagnose von Gameplay-Stutter.
///
/// <para>Misst pro Frame die echte (ungecappte) Inter-Frame-Zeit sowie Update- und Render-Zeit
/// und aggregiert 1× pro Sekunde zu einer Zeile:</para>
/// <list type="bullet">
/// <item><c>frame_max</c> — größte Inter-Frame-Zeit im Fenster = der sichtbare Hänger.</item>
/// <item><c>work_max</c> / <c>upd_max</c> / <c>rend_max</c> — reine CPU-Arbeit (zeigt ob die Arbeit
/// selbst teuer ist, oder ob der Hänger ZWISCHEN den Frames passiert → externe Pause/GC).</item>
/// <item><c>gc0/1/2</c> — GC-Sammlungen pro Sekunde (Frequenz der GC-Pausen).</item>
/// <item><c>alloc</c> — Allokations-Rate in KB/s = Schlüssel-Indikator für GC-getriebenen Stutter.</item>
/// </list>
///
/// <para>Faustregel zur Deutung: <c>frame_max</c> ≫ <c>work_max</c> bei gleichzeitigem <c>gc0=+N</c>
/// ⇒ der Hänger ist eine GC-Pause; eine hohe <c>alloc</c>-Rate ist die Ursache.</para>
///
/// <para>Output geht über <see cref="Sink"/> — auf Android nach Logcat (Tag <c>BBPERF</c>), live via
/// <c>adb logcat -s BBPERF:I</c> auslesbar. TEMPORÄRE DIAGNOSE-INSTRUMENTIERUNG.</para>
/// </summary>
public sealed class FrameProfiler
{
    /// <summary>Output-Senke. Default = Debug-Output (Desktop). Auf Android in MainActivity auf
    /// <c>Android.Util.Log.Info("BBPERF", x)</c> gesetzt.</summary>
    public static Action<string> Sink { get; set; } = msg => Debug.WriteLine(msg);

    /// <summary>Global an/aus — Profiling komplett deaktivierbar ohne Code-Ausbau. Default AUS
    /// (Diagnose-Werkzeug; zum Aktivieren auf true setzen + in MainActivity den Sink verdrahten).</summary>
    public static bool Enabled { get; set; }

    private static readonly double TickToMs = 1000.0 / Stopwatch.Frequency;

    private long _windowStartTick;
    private bool _started;

    private int _frames;
    private double _frameMsSum, _frameMsMax;
    private double _workMsSum, _workMsMax;
    private double _updMsMax, _rendMsMax;
    private int _gc0Start, _gc1Start, _gc2Start;
    private long _allocStart;

    /// <summary>Einen Frame erfassen. Aggregiert intern und gibt 1× pro Sekunde eine Zeile aus.</summary>
    /// <param name="frameMs">Echte (ungecappte) Inter-Frame-Zeit in ms.</param>
    /// <param name="updateMs">Dauer von GameEngine.Update in ms.</param>
    /// <param name="renderMs">Dauer von GameEngine.Render in ms.</param>
    /// <param name="state">Aktueller GameState (Starting/Playing/...).</param>
    public void Record(double frameMs, double updateMs, double renderMs, GameState state)
    {
        if (!Enabled) return;

        long now = Stopwatch.GetTimestamp();
        if (!_started)
        {
            StartWindow(now);
            _started = true;
        }

        _frames++;
        double workMs = updateMs + renderMs;
        _frameMsSum += frameMs;
        if (frameMs > _frameMsMax) _frameMsMax = frameMs;
        _workMsSum += workMs;
        if (workMs > _workMsMax) _workMsMax = workMs;
        if (updateMs > _updMsMax) _updMsMax = updateMs;
        if (renderMs > _rendMsMax) _rendMsMax = renderMs;

        double elapsedMs = (now - _windowStartTick) * TickToMs;
        if (elapsedMs < 1000.0) return;

        int gc0 = GC.CollectionCount(0) - _gc0Start;
        int gc1 = GC.CollectionCount(1) - _gc1Start;
        int gc2 = GC.CollectionCount(2) - _gc2Start;
        long allocNow = GC.GetTotalAllocatedBytes(precise: false);
        double seconds = elapsedMs / 1000.0;
        double allocKbPerSec = (allocNow - _allocStart) / 1024.0 / seconds;
        double fps = _frames / seconds;

        Sink(string.Format(CultureInfo.InvariantCulture,
            "BBPERF fps={0:F0} frame_avg={1:F1} frame_max={2:F0} work_avg={3:F1} work_max={4:F1} " +
            "upd_max={5:F1} rend_max={6:F1} gc0=+{7} gc1=+{8} gc2=+{9} alloc={10:F0}KB/s state={11}",
            fps, _frameMsSum / _frames, _frameMsMax,
            _workMsSum / _frames, _workMsMax, _updMsMax, _rendMsMax,
            gc0, gc1, gc2, allocKbPerSec, state));

        StartWindow(now);
    }

    private void StartWindow(long now)
    {
        _windowStartTick = now;
        _frames = 0;
        _frameMsSum = _frameMsMax = 0;
        _workMsSum = _workMsMax = 0;
        _updMsMax = _rendMsMax = 0;
        _gc0Start = GC.CollectionCount(0);
        _gc1Start = GC.CollectionCount(1);
        _gc2Start = GC.CollectionCount(2);
        _allocStart = GC.GetTotalAllocatedBytes(precise: false);
    }
}
