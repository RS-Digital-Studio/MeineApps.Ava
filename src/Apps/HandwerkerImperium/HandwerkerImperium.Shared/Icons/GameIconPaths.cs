using Avalonia.Media;

namespace HandwerkerImperium.Icons;

/// <summary>
/// Ehemals SVG-Pfaddaten fuer alle Icons.
/// Alle Icons sind jetzt AI-generierte WebP-Bitmaps (Assets/visuals/icons/).
/// Stub bleibt fuer Kompatiblitaet mit StringToGameIconKindConverter.
/// </summary>
public static class GameIconPaths
{
    /// <summary>
    /// Gibt null zurueck - alle Icons sind jetzt Bitmaps.
    /// </summary>
    public static Geometry? GetGeometry(GameIconKind kind) => null;

    /// <summary>
    /// Gibt null zurueck - keine SVG-Pfaddaten mehr vorhanden.
    /// </summary>
    public static string? GetPathData(GameIconKind kind) => null;
}
