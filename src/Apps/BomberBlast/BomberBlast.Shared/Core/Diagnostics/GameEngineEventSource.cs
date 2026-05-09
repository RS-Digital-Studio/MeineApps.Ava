using System.Diagnostics.Tracing;

namespace BomberBlast.Core.Diagnostics;

/// <summary>
/// EventSource-Tracing-Provider (Phase 27b — AAA-Audit P1).
///
/// <para>Liefert Event-Markers pro Engine-Subsystem für <c>dotnet-trace</c> und Visual-Studio-Diagnostic-Tools.
/// Ermöglicht Frame-Budget-Profiling: <c>dotnet-trace collect --providers BomberBlast-Engine</c>
/// liefert eine Timeline mit Sub-Frame-Granularität.</para>
///
/// <para>Brawl-Stars-Standard: 16ms Frame-Budget pro Subsystem aufgeschlüsselt
/// (4ms Sim + 8ms Render + 4ms Slack). Ohne Markers ist das Profiling auf Mobile praktisch unmöglich.</para>
///
/// <para>Performance: EventSource ist quasi gratis wenn niemand subscribed (Lambda-only-Aufruf-Cost).
/// Bei aktivem Subscriber: ~50ns pro Event auf x64.</para>
/// </summary>
[EventSource(Name = "BomberBlast-Engine")]
public sealed class GameEngineEventSource : EventSource
{
    /// <summary>Singleton-Instanz (Standard-EventSource-Pattern).</summary>
    public static readonly GameEngineEventSource Log = new();

    private GameEngineEventSource() { }

    /// <summary>Ein Frame beginnt. <c>frameNumber</c> = Engine-internal Counter.</summary>
    [Event(1, Level = EventLevel.Verbose, Keywords = Keywords.Frame)]
    public void FrameStart(int frameNumber)
    {
        if (IsEnabled())
            WriteEvent(1, frameNumber);
    }

    /// <summary>Frame fertig. <c>elapsedMs</c> = Wall-Clock seit FrameStart.</summary>
    [Event(2, Level = EventLevel.Verbose, Keywords = Keywords.Frame)]
    public void FrameEnd(int frameNumber, double elapsedMs)
    {
        if (IsEnabled())
            WriteEvent(2, frameNumber, elapsedMs);
    }

    /// <summary>Sim-Tick beginnt (Update-Phase).</summary>
    [Event(3, Level = EventLevel.Verbose, Keywords = Keywords.Sim)]
    public void SimTickStart() { if (IsEnabled()) WriteEvent(3); }

    [Event(4, Level = EventLevel.Verbose, Keywords = Keywords.Sim)]
    public void SimTickEnd(double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(4, elapsedMs);
    }

    /// <summary>Render-Phase beginnt (PaintSurface).</summary>
    [Event(5, Level = EventLevel.Verbose, Keywords = Keywords.Render)]
    public void RenderStart() { if (IsEnabled()) WriteEvent(5); }

    [Event(6, Level = EventLevel.Verbose, Keywords = Keywords.Render)]
    public void RenderEnd(double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(6, elapsedMs);
    }

    /// <summary>AI-Pathfinding-Operation (A*-Suche pro Gegner).</summary>
    [Event(7, Level = EventLevel.Verbose, Keywords = Keywords.AI)]
    public void AStarSearch(int enemyId, int pathLength, double elapsedMs)
    {
        if (IsEnabled()) WriteEvent(7, enemyId, pathLength, elapsedMs);
    }

    /// <summary>Explosion ausgelöst (Bomb-Detonation).</summary>
    [Event(8, Level = EventLevel.Informational, Keywords = Keywords.Gameplay)]
    public void ExplosionTriggered(int gridX, int gridY, int range)
    {
        if (IsEnabled()) WriteEvent(8, gridX, gridY, range);
    }

    /// <summary>Memory-Trim-Event vom OS (Android onTrimMemory).</summary>
    [Event(9, Level = EventLevel.Warning, Keywords = Keywords.Memory)]
    public void MemoryTrimRequested(int trimLevel, long heapBytes)
    {
        if (IsEnabled()) WriteEvent(9, trimLevel, heapBytes);
    }

    /// <summary>Hardware-Tier-Wechsel (Battery/Thermal-Override).</summary>
    [Event(10, Level = EventLevel.Informational, Keywords = Keywords.Memory)]
    public void HardwareTierChanged(int tier, bool batterySave, bool thermalThrottle)
    {
        if (IsEnabled()) WriteEvent(10, tier, batterySave ? 1 : 0, thermalThrottle ? 1 : 0);
    }

    /// <summary>Network-State-Wechsel (Online/Offline für Cloud-Save-Defer).</summary>
    [Event(11, Level = EventLevel.Informational, Keywords = Keywords.Network)]
    public void NetworkStateChanged(bool online)
    {
        if (IsEnabled()) WriteEvent(11, online ? 1 : 0);
    }

    /// <summary>Keywords für Filter-Granularität in dotnet-trace.</summary>
    public static class Keywords
    {
        public const EventKeywords Frame = (EventKeywords)0x0001;
        public const EventKeywords Sim = (EventKeywords)0x0002;
        public const EventKeywords Render = (EventKeywords)0x0004;
        public const EventKeywords AI = (EventKeywords)0x0008;
        public const EventKeywords Gameplay = (EventKeywords)0x0010;
        public const EventKeywords Memory = (EventKeywords)0x0020;
        public const EventKeywords Network = (EventKeywords)0x0040;
    }
}
