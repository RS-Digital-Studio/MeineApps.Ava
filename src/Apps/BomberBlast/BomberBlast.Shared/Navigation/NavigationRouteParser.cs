namespace BomberBlast.Navigation;

/// <summary>
/// Reine Parsing-Helfer fuer Routen-Strings — Compound-Route-Aufloesung, BaseRoute/Query-Trennung
/// und CloudSave-Init-Gating. Statisch + zustandslos, damit der <see cref="NavigationCoordinator"/>
/// schlank bleibt und die fehleranfaellige String-Logik isoliert testbar ist.
/// </summary>
public static class NavigationRouteParser
{
    /// <summary>
    /// Loest zusammengesetzte Routen auf und trennt BaseRoute von Query.
    ///
    /// <para>
    /// Beispiele:
    /// </para>
    /// <list type="bullet">
    /// <item><c>"Game?mode=story&amp;level=5"</c> → BaseRoute <c>"Game"</c>, Query <c>"mode=story&amp;level=5"</c></item>
    /// <item><c>"//MainMenu/Game?mode=story"</c> → BaseRoute <c>"Game"</c>, Query <c>"mode=story"</c></item>
    /// <item><c>"//MainMenu"</c> → BaseRoute <c>"MainMenu"</c>, Query <c>null</c></item>
    /// <item><c>"Shop"</c> → BaseRoute <c>"Shop"</c>, Query <c>null</c></item>
    /// </list>
    /// </summary>
    public static (string BaseRoute, string? Query) Parse(string route)
    {
        route ??= string.Empty;

        // Zusammengesetzte Routen behandeln ("//MainMenu/Game?mode=story" → "Game?mode=story").
        if (route.StartsWith("//"))
        {
            var withoutPrefix = route[2..];
            var slashIndex = withoutPrefix.IndexOf('/');
            route = slashIndex >= 0 ? withoutPrefix[(slashIndex + 1)..] : withoutPrefix;
        }

        var questionMark = route.IndexOf('?');
        if (questionMark < 0)
            return (route, null);

        return (route[..questionMark], route[(questionMark + 1)..]);
    }

    /// <summary>
    /// True wenn die Route den lokalen Persistenz-State liest und daher den CloudSave-Init-Task
    /// abgeschlossen haben muss (Race-Schutz: frisches Geraet darf nicht den Leer-State
    /// ueber den Cloud-State schieben).
    /// </summary>
    public static bool RequiresCloudSaveInit(string baseRoute)
        => baseRoute is "Game" or "LevelSelect" or "Dungeon"
            or "DailyChallenge" or "WeeklyChallenge" or "Deck" or "Collection";
}
