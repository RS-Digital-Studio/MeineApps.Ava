using BomberBlast.Core;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für GameLoopSettings (v2.0.44 — ).
/// Validiert TickIntervalMs-Berechnung, SetTargetFps-Persistenz, TargetFpsChanged-Event-Firing.
///
/// HINWEIS: GameLoopSettings ist eine static-Klasse — Tests müssen vorsichtig sein und
/// am Ende den Default-Zustand wiederherstellen, damit andere Tests nicht beeinflusst werden.
/// </summary>
[Collection("GameLoopSettingsSequential")] // verhindert parallele Ausführung
public class GameLoopSettingsTests : IDisposable
{
    private readonly int _initialFps;

    public GameLoopSettingsTests()
    {
        _initialFps = GameLoopSettings.TargetFps;
    }

    public void Dispose()
    {
        // Reset auf 30 FPS Default für andere Tests
        GameLoopSettings.SetTargetFps(GameLoopSettings.FrameRate30);
    }

    [Fact]
    public void TickIntervalMs_30Fps_LiefertCirca33ms()
    {
        GameLoopSettings.SetTargetFps(GameLoopSettings.FrameRate30);
        GameLoopSettings.TickIntervalMs.Should().Be(33, "1000ms / 30fps = 33.33ms → int 33");
    }

    [Fact]
    public void TickIntervalMs_60Fps_LiefertCirca16ms()
    {
        GameLoopSettings.SetTargetFps(GameLoopSettings.FrameRate60);
        GameLoopSettings.TickIntervalMs.Should().Be(16, "1000ms / 60fps = 16.66ms → int 16");
    }

    [Fact]
    public void SetTargetFps_UngueltigerWert_FaelltAuf30Zurueck()
    {
        GameLoopSettings.SetTargetFps(45);
        GameLoopSettings.TargetFps.Should().Be(GameLoopSettings.FrameRate30, "nur 30 oder 60 sind zulässig");
    }

    [Fact]
    public void SetTargetFps_FeuertEvent()
    {
        int eventFps = 0;
        EventHandler<int> handler = (_, fps) => eventFps = fps;
        GameLoopSettings.TargetFpsChanged += handler;
        try
        {
            GameLoopSettings.SetTargetFps(GameLoopSettings.FrameRate60);
            eventFps.Should().Be(GameLoopSettings.FrameRate60);
        }
        finally
        {
            GameLoopSettings.TargetFpsChanged -= handler;
        }
    }

    [Fact]
    public void TickInterval_AlsTimeSpan_KorrektUmgewandelt()
    {
        GameLoopSettings.SetTargetFps(GameLoopSettings.FrameRate30);
        GameLoopSettings.TickInterval.TotalMilliseconds.Should().BeApproximately(33, 0.5);
    }

    [Fact]
    public void Initialize_LiestPersistierteWerteAusPreferences()
    {
        var prefs = new InMemoryPreferences();
        prefs.Set(GameLoopSettings.PrefKey, GameLoopSettings.FrameRate60);

        GameLoopSettings.Initialize(prefs);

        GameLoopSettings.TargetFps.Should().Be(GameLoopSettings.FrameRate60);
    }
}
