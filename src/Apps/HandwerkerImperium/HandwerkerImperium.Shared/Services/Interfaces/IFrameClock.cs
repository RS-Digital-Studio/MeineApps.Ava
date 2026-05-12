using System;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentraler Frame-Clock für visuell-orientierte Renderer (AAA-Audit P1).
/// Ein einzelner Dispatcher-Timer treibt alle Subscriber statt 35 einzelner Timer pro
/// Renderer. Pro Subscriber konfigurierbares Intervall — der Clock filtert Ticks
/// transparent nach Subscriber-Frequenz.
///
/// Vorteile:
/// - 1 Timer-Ressource statt 35
/// - Zentrale Pause-Möglichkeit für App-Lifecycle (Battery-Save im Hintergrund)
/// - Auto-Stop bei 0 Subscribern
/// - Stopwatch-basierte DeltaSeconds (genau, nicht Timer-Interval-driven)
///
/// Konventionelle Intervalle (siehe <see cref="HandwerkerImperium.Graphics.FpsProfile"/>):
/// - 30Hz Standard: Mini-Games, Dashboard-Effekte
/// - 15-24Hz: Scroll-Views, Research-Tree, Guild-Research
/// - 5-10Hz: Idle-Anzeigen, Worker-Avatare
///
/// Migration: Bestehende Renderer-DispatcherTimer können schrittweise auf IFrameClock
/// umgestellt werden. Pro Renderer ~5 Zeilen weniger Code (Timer-Allokation +
/// Tick-Verdrahtung entfaellt).
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
    /// <param name="handler">Tick-Callback. Wird nur aufgerufen wenn das Subscriber-eigene
    /// Intervall seit dem letzten Aufruf verstrichen ist.</param>
    /// <param name="interval">Frequenz für diesen Subscriber. Default: 30Hz (~33ms).
    /// Niedrigere Frequenzen (z.B. 10Hz Idle) sparen Render-CPU.</param>
    void Subscribe(EventHandler<FrameTickEventArgs> handler, TimeSpan? interval = null);

    /// <summary>
    /// Subscriber abmelden. Stoppt den Timer wenn dies der letzte Subscriber war.
    /// </summary>
    void Unsubscribe(EventHandler<FrameTickEventArgs> handler);

    /// <summary>
    /// Aendert das Intervall eines bereits registrierten Subscribers (z.B. wenn ein
    /// Renderer von Idle 10Hz auf Effekt-aktiv 30Hz wechselt). Noop wenn handler nicht
    /// registriert ist.
    /// </summary>
    void UpdateInterval(EventHandler<FrameTickEventArgs> handler, TimeSpan interval);

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
    /// <summary>Sekunden seit dem letzten Tick fuer diesen Subscriber (Stopwatch-basiert).</summary>
    public float DeltaSeconds { get; }

    /// <summary>Kumulierte Sekunden seit Clock-Start (fuer Shader-Time-Uniform).</summary>
    public float ElapsedSeconds { get; }

    public FrameTickEventArgs(float deltaSeconds, float elapsedSeconds)
    {
        DeltaSeconds = deltaSeconds;
        ElapsedSeconds = elapsedSeconds;
    }
}
