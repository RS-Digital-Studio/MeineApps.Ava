namespace BomberBlast.ViewModels;

/// <summary>
/// Interface für ViewModels die FloatingText emittieren.
/// </summary>
public interface IFloatingTextEmitter
{
    event Action<string, string>? FloatingTextRequested;
}

/// <summary>
/// Interface für ViewModels die Confetti/Celebration-Effekte ausloesen.
/// Audit L23: Getrennt von IFloatingTextEmitter, damit z.B. GameOverViewModel
/// (Tod-Screen, keine Celebration) den CelebrationRequested-Pflicht-Implementierung
/// und CS0067-Warnung loswird.
/// </summary>
public interface ICelebrationEmitter
{
    event Action? CelebrationRequested;
}

/// <summary>
/// Interface für ViewModels die FloatingText und Celebration-Events emittieren.
/// Bleibt als Convenience-Combo erhalten (Backward-compat).
/// Einheitliche Signatur: Action statt EventHandler (konsistent mit Projekt-Conventions).
/// </summary>
public interface IGameJuiceEmitter : IFloatingTextEmitter, ICelebrationEmitter
{
}
