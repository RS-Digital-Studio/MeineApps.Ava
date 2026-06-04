using System.Diagnostics;
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace BomberBlast.Core.Diagnostics;

/// <summary>
/// TEMP-DIAGNOSE: Misst die Dauer einzelner Render-Phasen und loggt 1× pro Sekunde die jeweils
/// größte Frame-Zeit je Phase (Zeile mit Präfix <c>BBPHASE</c>, über <see cref="FrameProfiler.Sink"/>
/// → Logcat-Tag BBPERF). Zeigt, WELCHER Render-Block den periodischen ~1,3 s-Stutter verursacht.
///
/// <para>Nutzung: <c>using (RenderProbe.Measure("post")) { ... }</c> um einen Block, und einmal
/// <see cref="EndFrame"/> am Ende von GameRenderer.Render. Allokationsfrei im Hot-Path (Scope ist
/// readonly struct, StringBuilder gecacht, Phasennamen sind String-Literale).</para>
/// </summary>
public static class RenderProbe
{
    // Default AUS (Diagnose-Werkzeug). Zum Aktivieren auf true setzen.
    public static bool Enabled { get; set; }

    /// <summary>
    /// Wenn gesetzt, flusht jeder Scope am Block-Ende diesen Canvas — so wird die (sonst GPU-deferred)
    /// Ausführungszeit der jeweiligen Phase zugeordnet statt am Frame-Ende zu akkumulieren. Am Anfang
    /// von GameRenderer.Render auf den aktuellen Canvas setzen. TEMP-DIAGNOSE.
    /// </summary>
    public static SKCanvas? FlushTarget { get; set; }

    private const int MaxPhases = 24;
    private static readonly string[] _names = new string[MaxPhases];
    private static readonly double[] _maxMs = new double[MaxPhases];
    private static int _count;
    private static long _windowStart;
    private static bool _started;
    private static readonly double TickToMs = 1000.0 / Stopwatch.Frequency;
    private static readonly StringBuilder _sb = new(320);

    /// <summary>Misst die Dauer des umschlossenen Blocks unter dem Phasennamen (String-Literal!).</summary>
    public static Scope Measure(string name) => Enabled ? new Scope(Slot(name)) : new Scope(-1);

    private static int Slot(string name)
    {
        for (int i = 0; i < _count; i++)
            if (_names[i] == name) return i;
        if (_count < MaxPhases) { _names[_count] = name; return _count++; }
        return MaxPhases - 1;
    }

    /// <summary>Am Ende jedes Render-Frames aufrufen. Loggt 1× pro Sekunde die Phasen-Max-Zeiten.</summary>
    public static void EndFrame()
    {
        if (!Enabled) return;
        long now = Stopwatch.GetTimestamp();
        if (!_started) { _windowStart = now; _started = true; return; }
        if ((now - _windowStart) * TickToMs < 1000.0) return;

        _sb.Clear();
        _sb.Append("BBPHASE");
        for (int i = 0; i < _count; i++)
        {
            _sb.Append(' ').Append(_names[i]).Append('=')
               .Append(_maxMs[i].ToString("F0", CultureInfo.InvariantCulture));
            _maxMs[i] = 0;
        }
        FrameProfiler.Sink(_sb.ToString());
        _windowStart = now;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly int _slot;
        private readonly long _t0;
        internal Scope(int slot)
        {
            _slot = slot;
            _t0 = slot >= 0 ? Stopwatch.GetTimestamp() : 0;
        }
        public void Dispose()
        {
            if (_slot < 0) return;
            // Flush erzwingt die Ausführung der in diesem Block enqueuten GPU-Befehle, damit ihre
            // Zeit DIESER Phase zugeordnet wird (statt am Frame-Ende zu akkumulieren).
            try { FlushTarget?.Flush(); } catch { /* Diagnose, Best-Effort */ }
            double ms = (Stopwatch.GetTimestamp() - _t0) * TickToMs;
            if (ms > _maxMs[_slot]) _maxMs[_slot] = ms;
        }
    }
}
