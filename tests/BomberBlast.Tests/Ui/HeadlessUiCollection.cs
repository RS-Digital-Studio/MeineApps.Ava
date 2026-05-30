using System.Threading;
using Avalonia.Headless;
using Xunit;

namespace BomberBlast.Tests.Ui;

/// <summary>
/// Prozessweite Avalonia-Headless-Session. Avalonia darf pro Prozess nur einmal initialisiert
/// werden — daher teilen sich alle UI-Tests EINE Session via xunit-Collection-Fixture.
/// </summary>
public sealed class HeadlessUiSession : IDisposable
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));

    /// <summary>Fuehrt <paramref name="action"/> synchron auf dem Avalonia-UI-Thread aus.</summary>
    public void OnUiThread(Action action) =>
        _session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();

    public void Dispose() => _session.Dispose();
}

[CollectionDefinition(Name)]
public sealed class HeadlessUiCollection : ICollectionFixture<HeadlessUiSession>
{
    public const string Name = "HeadlessUi";
}
