namespace BomberBlast.ViewModels;

/// <summary>
/// Interface für ViewModels die FloatingText und Celebration-Events emittieren.
/// Einheitliche Signatur: Action statt EventHandler (konsistent mit Projekt-Conventions).
/// </summary>
public interface IGameJuiceEmitter
{
    event Action<string, string>? FloatingTextRequested;
    event Action? CelebrationRequested;
}
