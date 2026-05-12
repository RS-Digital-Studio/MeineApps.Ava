using System;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentraler Frame-Clock für visuell-orientierte Renderer (AAA-Audit P1).
/// Ein einzelner Dispatcher-Timer treibt alle Subscriber, statt dass jeder Renderer
/// einen eigenen Timer betreibt. Reduziert Timer-Overhead und ermöglicht einheitliche
/// Drosselung (Scroll-Pause, Pause-State, FpsProfile-Wechsel).
///
/// Pattern:
/// - 30 Hz Standard-Tick (DashboardActive-FpsProfile).
/// - Subscriber registrieren sich via <see cref="Subscribe"/> und bekommen <see cref="FrameTickEventArgs"/>
///   mit Delta-Zeit für Animation. Idempotent: doppeltes Subscribe ist Noop.
/// - Bei 0 Subscribern stoppt der Timer automatisch (Battery-Save).
///
/// Migration: Bestehende Renderer-Timer können schrittweise auf <see cref="IFrameClock"/>
/// umgestellt werden. Renderer mit eigenständigen Frequenzen (z.B. 1 Hz Logic-Ticks,
/// Hold-Timer 120 ms) behalten ihren eigenen Timer — IFrameClock ist nur fuer visuelle
/// Render-Loops.
/// </summary>
public interface IFrameClock
{
    /// <summary>Anzahl aktuell registrierter Subscriber.</summary>
    int SubscriberCount { get; }

    /// <summary>Ob der Clock gerade tickt (mindestens 1 Subscriber + nicht pausiert).</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Subscriber an den Clock anhaengen. Idempotent — doppeltes Subscribe wird ignoriert.
    /// Startet den Timer wenn dies der erste Subscriber ist.
    /// </summary>
    void Subscribe(EventHandler<FrameTickEventArgs> handler);

    /// <summary>
    /// Subscriber abmelden. Stoppt den Timer wenn dies der letzte Subscriber war.
    /// </summary>
    void Unsubscribe(EventHandler<FrameTickEventArgs> handler);

    /// <summary>
    /// Pausiert den Clock global (z.B. wenn App in den Hintergrund geht).
    /// Subscriber bleiben registriert, aber bekommen keine Ticks bis <see cref="Resume"/>.
    /// </summary>
    void Pause();

    /// <summary>Pause aufheben.</summary>
    void Resume();
}

/// <summary>
/// Frame-Tick-Argument mit Delta-Zeit und kumulierter Render-Zeit fuer Shader-Animationen.
/// </summary>
public sealed class FrameTickEventArgs : EventArgs
{
    /// <summary>Sekunden seit dem letzten Tick (Stopwatch-basiert, nicht Timer-Interval).</summary>
    public float DeltaSeconds { get; }

    /// <summary>Kumulierte Sekunden seit Clock-Start (fuer Shader-Time-Uniform).</summary>
    public float ElapsedSeconds { get; }

    public FrameTickEventArgs(float deltaSeconds, float elapsedSeconds)
    {
        DeltaSeconds = deltaSeconds;
        ElapsedSeconds = elapsedSeconds;
    }
}
