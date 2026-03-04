using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Persists and manages the visual rendering style
/// </summary>
public sealed class GameStyleService : IGameStyleService
{
    private const string PREF_KEY = "visual_style";
    private readonly IPreferencesService _preferences;

    public GameVisualStyle CurrentStyle { get; private set; }
    public event Action<GameVisualStyle>? StyleChanged;

    public GameStyleService(IPreferencesService preferences)
    {
        _preferences = preferences;

        var saved = _preferences.Get(PREF_KEY, "Classic");
        CurrentStyle = Enum.TryParse<GameVisualStyle>(saved, out var style) ? style : GameVisualStyle.Classic;
    }

    public void SetStyle(GameVisualStyle style)
    {
        if (CurrentStyle == style) return;

        CurrentStyle = style;
        _preferences.Set(PREF_KEY, style.ToString());
        StyleChanged?.Invoke(style);
    }
}
