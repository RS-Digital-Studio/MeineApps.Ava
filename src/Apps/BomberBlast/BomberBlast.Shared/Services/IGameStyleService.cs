namespace BomberBlast.Services;

/// <summary>
/// Visual style for the game renderer
/// </summary>
public enum GameVisualStyle
{
    Classic,
    Neon,
    Retro
}

/// <summary>
/// Service to manage the visual rendering style (Classic HD vs Neon/Cyberpunk)
/// </summary>
public interface IGameStyleService
{
    /// <summary>Current visual style</summary>
    GameVisualStyle CurrentStyle { get; }

    /// <summary>Fired when style changes</summary>
    event Action<GameVisualStyle>? StyleChanged;

    /// <summary>Set and persist the visual style</summary>
    void SetStyle(GameVisualStyle style);
}
