using Avalonia.Media;

namespace BomberBlast.Icons;

/// <summary>
/// Alle Icon-Pfaddaten fuer BomberBlast im Neon Arcade Stil.
/// Jedes Icon ist in einem 24x24 Koordinatenraum definiert.
/// Eigene geometrische Designs - Oktagone statt Kreise, scharfe Kanten, Arcade-Aesthetik.
/// F0 = EvenOdd Fuellregel (fuer Icons mit Aussparungen).
/// </summary>
public static class GameIconPaths
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<GameIconKind, Geometry?> _geometryCache = new();

    public static Geometry? GetGeometry(GameIconKind kind)
    {
        return _geometryCache.GetOrAdd(kind, static k =>
        {
            if (Paths.TryGetValue(k, out var pathData))
                return StreamGeometry.Parse(pathData);
            return null;
        });
    }

    public static string? GetPathData(GameIconKind kind)
    {
        return Paths.GetValueOrDefault(kind);
    }

    private static readonly Dictionary<GameIconKind, string> Paths = new()
    {
        // =====================================================================
        // NAVIGATION
        // =====================================================================
        [GameIconKind.ArrowLeft] = "M17 4L6 12L17 20V16L10 12L17 8Z",
        [GameIconKind.ArrowUpBold] = "M12 2L3 14H8V22H16V14H21Z",
        [GameIconKind.ArrowUpBoldCircle] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M12 7L7 14H10V18H14V14H17Z",
        [GameIconKind.ChevronRight] = "M7 4L18 12L7 20V16L14 12L7 8Z",
        [GameIconKind.ChevronDoubleUp] =
            "M12 3L5 10L8 10L12 6L16 10L19 10Z" +
            " M12 11L5 18L8 18L12 14L16 18L19 18Z",
        [GameIconKind.Close] = "M6 2L2 6L9 12L2 18L6 22L12 15L18 22L22 18L15 12L22 6L18 2L12 9Z",

        // =====================================================================
        // STATUS
        // =====================================================================
        [GameIconKind.Check] = "M9 17L4 12L2 14L9 21L22 8L20 6Z",
        [GameIconKind.CheckCircle] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M10 15L7 12L9 10L10 12L15 7L17 9Z",
        [GameIconKind.Lock] =
            "F0 M8 10V7L10 4H14L16 7V10H18V21H6V10Z" +
            " M11 14V18H13V14L14 13H10Z",
        [GameIconKind.LockOpen] =
            "F0 M8 10V7L10 4H14L16 7V8H14V6L13 5H11L10 6V10H18V21H6V10Z" +
            " M11 14V18H13V14L14 13H10Z",
        [GameIconKind.LockOutline] =
            "F0 M8 10V7L10 4H14L16 7V10H18V21H6V10Z" +
            " M8 12H16V19H8Z",

        // =====================================================================
        // STARS & REWARDS
        // =====================================================================
        [GameIconKind.Star] = "M12 1L15 8H22L17 13L19 22L12 17L5 22L7 13L2 8H9Z",
        [GameIconKind.StarOutline] =
            "F0 M12 1L15 8H22L17 13L19 22L12 17L5 22L7 13L2 8H9Z" +
            " M12 6L10 10H6.5L9 13L8 17L12 14.5L16 17L15 13L17.5 10H14Z",
        [GameIconKind.StarCircleOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M12 6L13.5 10H17L14 12.5L15 16.5L12 14L9 16.5L10 12.5L7 10H10.5Z",
        [GameIconKind.Crown] = "M2 18V8L7 13L12 2L17 13L22 8V18Z M3 19H21V22H3Z",
        [GameIconKind.Trophy] =
            "M7 2H17V5H20V10L17 13H15V17H17V20H7V17H9V13H7L4 10V5H7Z",
        [GameIconKind.TrophyBroken] =
            "M7 2H11L12 8L11 14V17H7V17H9V13H7L4 10V5H7Z" +
            " M13 2H17V5H20V10L17 13H15V17H17V20H14V14L13 8Z",
        [GameIconKind.TrophyOutline] =
            "F0 M7 2H17V5H20V10L17 13H15V17H17V20H7V17H9V13H7L4 10V5H7Z" +
            " M9 4V11H11V15H13V11H15V4Z",
        [GameIconKind.Medal] =
            "F0 M9 2L7 5L9 8L6 10V17L12 22L18 17V10L15 8L17 5L15 2Z" +
            " M10 11V15L12 18L14 15V11Z",

        // =====================================================================
        // COMBAT & GAME
        // =====================================================================
        [GameIconKind.Sword] =
            "M10 1H14L15.5 5L14 15L17 18V20H13V23H11V20H7V18L10 15L8.5 5Z",
        [GameIconKind.Shield] = "M12 2L20 6V16L16 22H8L4 16V6Z",
        [GameIconKind.Skull] =
            "M6 3H18L21 6V13L18 17H16L15 21H13V18H11V21H9L8 17H6L3 13V6Z" +
            " M8 8H11V11H8Z M13 8H16V11H13Z M10 13H14L12 16Z",
        [GameIconKind.Fire] =
            "M12 1L16 7L19 5L17 12L21 10L18 17L14 15L13 22L12 23L11 22L10 15L6 17L3 10L7 12L5 5L8 7Z",
        [GameIconKind.Bomb] =
            "M7 8H15L19 12V18L15 22H7L3 18V12Z" +
            " M15 8L17 6L18 4H20V2H18L16 4L15 6Z" +
            " M9 11V14H11V11Z",
        [GameIconKind.Ghost] =
            "M8 3H16L20 7V17L18 19L16 17L14 19L12 17L10 19L8 17L6 19L4 17V7Z" +
            " M9 9H11V11H9Z M13 9H15V11H13Z",

        // =====================================================================
        // ECONOMY
        // =====================================================================
        [GameIconKind.CircleMultiple] =
            "M5 7H11L15 10V14L11 17H5L2 14V10Z" +
            " M13 5H19L22 8V12L19 15H16V14L18 12V9L16 7H13Z",
        [GameIconKind.CircleOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z",
        [GameIconKind.CurrencyUsd] =
            "M16 6H10L7 9V11L10 13H14L17 15V18L14 20H8V18H13L15 17V15L13 13H9L7 11V8L9 6H16Z" +
            " M11 2H13V22H11Z",
        [GameIconKind.Diamond] = "M12 2L22 12L12 22L2 12Z",
        [GameIconKind.DiamondStone] = "M7 3H17L22 10L12 22L2 10Z M7 3L5 10H19L17 3Z M12 4L9 10H15Z",
        [GameIconKind.Gift] =
            "M3 10H21V22H3Z M7 4L10 2L12 6L14 2L17 4L14 8H10Z" +
            " M11 10H13V22H11Z",
        [GameIconKind.TreasureChest] =
            "M2 10H22V22H2Z M3 4H21L22 10H2Z" +
            " M10 13H14V18H10Z",

        // =====================================================================
        // CARDS & DUNGEON
        // =====================================================================
        [GameIconKind.CardsPlaying] =
            "M4 4H16V19H4Z M8 2H20V17H18V4H8Z",
        [GameIconKind.CardsPlayingOutline] =
            "F0 M4 4H16V19H4Z M6 6H14V17H6Z" +
            " M8 2H20V17H18V4H8Z",
        [GameIconKind.CartPlus] =
            "M2 3H5L8 14H19L21 5H8Z M9 17H11V19H9Z M17 17H19V19H17Z" +
            " M13 6V9H16V11H13V14H11V11H8V9H11V6Z",
        [GameIconKind.Stairs] = "M2 22V18H6V14H10V10H14V6H18V2H22V22Z",
        [GameIconKind.Dice] =
            "F0 M5 3H19L21 5V19L19 21H5L3 19V5Z" +
            " M8 7H10V9H8Z M14 7H16V9H14Z M11 11H13V13H11Z" +
            " M8 15H10V17H8Z M14 15H16V17H14Z",
        [GameIconKind.LinkVariant] =
            "M8 12L6 14V18L8 20H12L14 18V16H12L10 18L8 18V14L10 12Z" +
            " M16 12L18 10V6L16 4H12L10 6V8H12L14 6L16 6V10L14 12Z",

        // =====================================================================
        // TIME
        // =====================================================================
        [GameIconKind.Clock] =
            "M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M11 6H13V12H17V14H11Z",
        [GameIconKind.ClockFast] =
            "M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M11 6H13V12H17V14H11Z" +
            " M20 4H23V5H20Z M21 8H23V9H21Z M20 16H23V17H20Z",
        [GameIconKind.ClockOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M11 7H13V12H16V14H11Z",
        [GameIconKind.Timer] =
            "M10 1H14V3H10Z" +
            " M8 5H16L21 9V17L16 22H8L3 17V9Z" +
            " M11 8H13V14H17V16H11Z",
        [GameIconKind.CalendarCheck] =
            "F0 M3 3H21V21H3Z M5 8H19V19H5Z" +
            " M10 16L7 13L9 11L10 13L16 8L18 10Z",
        [GameIconKind.CalendarToday] =
            "F0 M3 3H21V21H3Z M5 8H19V19H5Z" +
            " M7 10H11V14H7Z",
        [GameIconKind.CalendarWeek] =
            "F0 M3 3H21V21H3Z M5 8H19V19H5Z" +
            " M7 10H9V12H7Z M11 10H13V12H11Z M15 10H17V12H15Z",

        // =====================================================================
        // SETTINGS
        // =====================================================================
        [GameIconKind.Cog] =
            "F0 M10 1H14L15 4L17 5L20 3L22 6L20 9L21 12L20 15L22 18L20 21L17 19L15 20L14 23H10L9 20L7 19L4 21L2 18L4 15L3 12L4 9L2 6L4 3L7 5L9 4Z" +
            " M10 8H14L17 11V13L14 16H10L7 13V11Z",
        [GameIconKind.CogOutline] =
            "F0 M10 1H14L15 4L17 5L20 3L22 6L20 9L21 12L20 15L22 18L20 21L17 19L15 20L14 23H10L9 20L7 19L4 21L2 18L4 15L3 12L4 9L2 6L4 3L7 5L9 4Z" +
            " M10 8H14L17 11V13L14 16H10L7 13V11Z" +
            " M11 10H13L14 11V13L13 14H11L10 13V11Z",
        [GameIconKind.Gamepad] =
            "M3 8H21L22 11V17L20 19H16L14 17H10L8 19H4L2 17V11Z" +
            " M7 11V13H9V11Z M15 11H17V13H15Z",
        [GameIconKind.GamepadVariant] =
            "M6 5H18L20 7V17L18 19H6L4 17V7Z" +
            " M7 10V12H9V10Z M8 12V14H10V12Z" +
            " M15 10H17V12H15Z M15 14H17V16H15Z",
        [GameIconKind.VolumeHigh] =
            "M3 9V15H7L13 20V4L7 9Z" +
            " M15 8L17 10V14L15 16Z M17 5L20 8V16L17 19Z",
        [GameIconKind.Translate] =
            "M2 3H13V5H7L5 9L7 13H5L4 11H3V3Z" +
            " M11 11H22V21H11Z M14 13L17 19L20 13Z",
        [GameIconKind.AdsOff] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M5 5L19 19L17 21L3 7Z",

        // =====================================================================
        // CLOUD
        // =====================================================================
        [GameIconKind.Cloud] =
            "M4 17L3 15V12L5 10L8 8H10L12 6H15L17 8H19L21 10V15L20 17Z",
        [GameIconKind.CloudCheck] =
            "M4 17L3 15V12L5 10L8 8H10L12 6H15L17 8H19L21 10V15L20 17Z" +
            " M10 15L8 13L9.5 11.5L10 13L14 9L15.5 10.5Z",
        [GameIconKind.CloudDownload] =
            "M4 17L3 15V12L5 10L8 8H10L12 6H15L17 8H19L21 10V15L20 17Z" +
            " M11 9H13V13H15L12 16L9 13H11Z",
        [GameIconKind.CloudOffOutline] =
            "F0 M4 17L3 15V12L5 10L8 8H10L12 6H15L17 8H19L21 10V15L20 17Z" +
            " M5 16L4 14V13L6 11L8 10H11L13 8H14L16 9H18L19 11V14L18 16Z" +
            " M3 5L5 3L21 19L19 21Z",
        [GameIconKind.CloudSync] =
            "M4 17L3 15V12L5 10L8 8H10L12 6H15L17 8H19L21 10V15L20 17Z" +
            " M9 13L13 10V12H15V10L17 13H15V14H13V16L9 13Z",
        [GameIconKind.CloudUpload] =
            "M4 17L3 15V12L5 10L8 8H10L12 6H15L17 8H19L21 10V15L20 17Z" +
            " M11 16H13V12H15L12 9L9 12H11Z",

        // =====================================================================
        // CHARTS
        // =====================================================================
        [GameIconKind.ChartLine] = "M3 18L8 11L12 14L20 4L22 6L12 18L8 15L5 19Z",
        [GameIconKind.ChartDonut] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M10 7H14L17 10V14L14 17H10L7 14V10Z",

        // =====================================================================
        // NATURE & WORLD
        // =====================================================================
        [GameIconKind.PineTree] =
            "M12 2L5 12H8L4 18H20L16 12H19Z" +
            " M11 18H13V22H11Z",
        [GameIconKind.Waves] =
            "M2 6L6 10L10 6L14 10L18 6L22 10V12L18 8L14 12L10 8L6 12L2 8Z" +
            " M2 14L6 18L10 14L14 18L18 14L22 18V20L18 16L14 20L10 16L6 20L2 16Z",
        [GameIconKind.Terrain] = "M14 6L10 12L13 16L11 17L7 10L1 18H23Z",
        [GameIconKind.WeatherSunny] =
            "M10 8H14L16 10V14L14 16H10L8 14V10Z" +
            " M11 2H13V5H11Z M11 19H13V22H11Z" +
            " M2 11V13H5V11Z M19 11V13H22V11Z" +
            " M5 5L7 5L8 7L6 8Z M17 5L19 5L18 8L16 7Z" +
            " M5 19L6 16L8 17L7 19Z M18 16L19 19L17 19L16 17Z",
        [GameIconKind.Snowflake] =
            "M11 2H13V22H11Z" +
            " M3 7L5 5L12 12L5 19L3 17L8 12Z" +
            " M21 7L19 5L12 12L19 19L21 17L16 12Z" +
            " M5 11H8V13H5Z M16 11H19V13H16Z",
        [GameIconKind.Water] = "M12 2L19 12V17L16 20L12 22L8 20L5 17V12Z",
        [GameIconKind.Seed] =
            "M12 3L17 8V14L15 18L12 20L9 18L7 14V8Z" +
            " M11 8V18H13V8Z M9 12L12 15L15 12Z",
        [GameIconKind.Clover] =
            "M12 3L15 5V8H18L20 11L18 14H15V17L12 19L9 17V14H6L4 11L6 8H9V5Z" +
            " M11 19V22H13V19Z",

        // =====================================================================
        // BUILDINGS
        // =====================================================================
        [GameIconKind.Factory] = "M2 22V14L7 10V14L12 10V14L17 10V4H22V22Z",
        [GameIconKind.Pillar] =
            "M6 3H18V5L16 5V19L18 19V21H6V19L8 19V5L6 5Z" +
            " M10 5H11V19H10Z M13 5H14V19H13Z",
        [GameIconKind.Store] =
            "M3 3H21L23 8L21 10L19 8L17 10L15 8L13 10L11 8L9 10L7 8L5 10L3 8L1 8Z" +
            " M3 10V21H21V10Z M10 14H14V21H10Z",
        [GameIconKind.Home] =
            "M12 3L2 12H5V21H19V12H22Z" +
            " M10 15H14V21H10Z",

        // =====================================================================
        // PEOPLE & PROFILE
        // =====================================================================
        [GameIconKind.AccountCircle] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M12 5L15 6V9L13 11H11L9 9V6Z" +
            " M6 18V16L8 14H16L18 16V18Z",
        [GameIconKind.Heart] = "M12 22L3 12V7L6 3L9 3L12 7L15 3L18 3L21 7V12Z",
        [GameIconKind.HeartPlus] =
            "F0 M12 22L3 12V7L6 3L9 3L12 7L15 3L18 3L21 7V12Z" +
            " M11 9H13V11H15V13H13V15H11V13H9V11H11Z",
        [GameIconKind.HeartPulse] =
            "M12 22L3 12V7L6 3L9 3L12 7L15 3L18 3L21 7V12Z" +
            " M4 12H8L10 9L12 15L14 11L16 12H20V13H4V12Z",
        [GameIconKind.ContentSave] =
            "F0 M4 2H18L21 6V21H3V2Z" +
            " M14 2V8H8V2Z M8 12H16V19H8Z",

        // =====================================================================
        // INFO & HELP
        // =====================================================================
        [GameIconKind.HelpCircle] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M10 8L11 7H14L15 9L13 11V13H11V11L13 10L14 9L13 8H11L10 9Z" +
            " M11 16H13V18H11Z",
        [GameIconKind.InformationOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M11 7H13V9H11Z M11 11H13V17H11Z",
        [GameIconKind.AlertCircle] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M11 6H13V14H11Z M11 16H13V18H11Z",
        [GameIconKind.LightbulbOn] =
            "M9 3L12 1L15 3L17 7V12L15 15V18H9V15L7 12V7Z" +
            " M9 19H15V21H9Z" +
            " M4 2L6 4Z M20 2L18 4Z M1 8H4V10H1Z M20 8H23V10H20Z",

        // =====================================================================
        // MEDIA
        // =====================================================================
        [GameIconKind.Video] = "M2 6H16V10L22 6V18L16 14V18H2Z",
        [GameIconKind.Play] = "M7 4L20 12L7 20Z",
        [GameIconKind.SkipNext] = "M6 18L14 12L6 6V18Z M16 6V18H18V6Z",
        [GameIconKind.Repeat] =
            "M7 7H17V10L21 7L17 4V7H5V13H7Z" +
            " M17 17H7V14L3 17L7 20V17H19V11H17Z",
        [GameIconKind.Refresh] =
            "M12 4L6 7L4 11H6L7 9L12 6L16 8L18 12L16 16L12 18L8 16L6 13H4L6 17L12 20L18 17L20 14V10L18 7Z" +
            " M20 4L18 6L20 8Z",

        // =====================================================================
        // MISC
        // =====================================================================
        [GameIconKind.Shuffle] =
            "M2 7H8L16 17H20V20L23 17L20 14V17H18L10 7H2Z" +
            " M14 7H20V4L23 7Z M2 17H8L10 15L8 13Z",
        [GameIconKind.Speedometer] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V13H5V9Z M13 10L16 6L17 7L14 11Z",
        [GameIconKind.MapMarker] =
            "M12 2L18 6V12L12 22L6 12V6Z" +
            " M10 8H14V12H10Z",
        [GameIconKind.Target] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M10 6H14L18 10V14L14 18H10L6 14V10Z" +
            " M11 9H13L15 11V13L13 15H11L9 13V11Z",
        [GameIconKind.Palette] =
            "M8 2H16L22 8V16L16 22H13V19L14 17H16L19 14V10L16 5H8L5 10V14L8 17H10V19H8L2 16V8Z" +
            " M7 8H9V10H7Z M11 5H13V7H11Z M16 9H18V11H16Z M7 13H9V15H7Z",
        [GameIconKind.PaletteOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M7 9H9V11H7Z M11 5H13V7H11Z M16 9H18V11H16Z M7 13H9V15H7Z",
        [GameIconKind.ViewGrid] = "M3 3H11V11H3Z M13 3H21V11H13Z M3 13H11V21H3Z M13 13H21V21H13Z",
        [GameIconKind.SchoolOutline] =
            "F0 M12 3L22 9L12 15L2 9Z" +
            " M6 11V18L12 21L18 18V11L12 14Z" +
            " M8 12V17L12 19L16 17V12Z",
        [GameIconKind.BookOpen] =
            "M1 5H11L12 6L13 5H23V19H13L12 20L11 19H1Z" +
            " M11.5 6V19H12.5V6Z",
        [GameIconKind.GooglePlay] =
            "M4 2L14 12L4 22Z" +
            " M14 10L20 12L14 14Z",
        [GameIconKind.Flash] = "M13 2L7 13H11L7 22L17 11H13Z",
        [GameIconKind.FlashOutline] =
            "F0 M13 2L7 13H11L7 22L17 11H13Z" +
            " M12.5 5L9 12H12L9 18L15 12H12Z",

        // =====================================================================
        // POWERUP & MECHANIC ICONS
        // =====================================================================
        [GameIconKind.Account] =
            "M12 4L15 6V9L13 11H11L9 9V6Z" +
            " M4 20V18L7 14H17L20 18V20Z",
        [GameIconKind.FlashAlert] =
            "M13 2L7 13H11L7 20L17 11H13Z" +
            " M11 21H13V23H11Z",
        [GameIconKind.ArrowRightCircle] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M8 11H15V8L19 12L15 16V13H8Z",
        [GameIconKind.ShieldOutline] =
            "F0 M12 2L20 6V16L16 22H8L4 16V6Z" +
            " M12 5L18 8V15L15 19H9L6 15V8Z",
        [GameIconKind.HelpCircleOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M10 8L11 7H14L15 9L13 11V13H11V11L13 10L14 9L13 8H11L10 9Z" +
            " M11 16H13V18H11Z",
        [GameIconKind.Shoe] =
            "M4 16V13L6 11H10L13 8H15L16 9H18L20 11V16Z" +
            " M8 7V10L10 11L12 9V7L10 6H8Z",
        [GameIconKind.DotsHorizontal] =
            "M4 10H8V14H4Z M10 10H14V14H10Z M16 10H20V14H16Z",
        [GameIconKind.StarCircle] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M12 6L13.5 10H17.5L14.5 12.5L15.5 17L12 14.5L8.5 17L9.5 12.5L6.5 10H10.5Z",
        [GameIconKind.SkullOutline] =
            "F0 M6 3H18L21 6V13L18 17H16L15 21H13V18H11V21H9L8 17H6L3 13V6Z" +
            " M8 5H16L19 8V12L17 15H15L14 19H13V16H11V19H10L9 15H7L5 12V8Z" +
            " M9 8H11V11H9Z M13 8H15V11H13Z M10 13H14L12 15Z",
        [GameIconKind.ArrowRightBold] = "M4 10V14H14V18L22 12L14 6V10Z",
        [GameIconKind.SwapHorizontal] =
            "M8 4L2 9L8 14V11H14V7H8Z" +
            " M16 10L22 15L16 20V17H10V13H16Z",

        // =====================================================================
        // SKIN-KATEGORIE ICONS
        // =====================================================================
        [GameIconKind.Trail] =
            "M2 5L6 9L10 5L14 9L18 5L22 9V11L18 7L14 11L10 7L6 11L2 7Z" +
            " M2 12L6 16L10 12L14 16L18 12L22 16V18L18 14L14 18L10 14L6 18L2 14Z",
        [GameIconKind.Celebration] =
            "M3 16L5 9L10 14Z M5 21L7 14L12 19Z" +
            " M13 2L11 7L15 11L20 9Z" +
            " M17 3L18 5L20 6L19 8L17 7L16 5Z" +
            " M21 8L22 10L23 10L23 12L21 12L21 10Z" +
            " M6 3L7 5L8 5L8 7L6 7L6 5Z",
        [GameIconKind.CardFrame] =
            "F0 M4 2H20L21 3V21L20 22H4L3 21V3Z" +
            " M5 4H19V20H5Z M7 6H17V18H7Z",

        // =====================================================================
        // COLLECTION: ENEMY ICONS
        // =====================================================================
        [GameIconKind.EmoticonOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M9 9H11V11H9Z M13 9H15V11H13Z" +
            " M8 14L10 16H14L16 14Z",
        [GameIconKind.EmoticonAngryOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M7 8L11 10V11H9V10Z M17 8L13 10V11H15V10Z" +
            " M8 16L10 14H14L16 16Z",
        [GameIconKind.EmoticonCoolOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M6 9H12V12H6Z M12 9H18V12H12Z" +
            " M8 14L10 16H14L16 14Z",
        [GameIconKind.EmoticonDevilOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M9 9H11V11H9Z M13 9H15V11H13Z" +
            " M8 16L10 14H14L16 16Z" +
            " M5 3L7 6Z M19 3L17 6Z",
        [GameIconKind.Jellyfish] =
            "M8 3H16L20 7V13L18 15V16L16 18L18 20L16 22L14 20L12 22L10 20L8 22L6 20L8 18L6 16V15L4 13V7Z" +
            " M10 8H12V10H10Z M14 8H16V10H14Z",
        [GameIconKind.Run] =
            "M14 3H16V5H14Z" +
            " M11 6H15L17 9L15 9L16 12L13 17L15 21H13L10 17L12 13L10 10L7 12L6 10L10 7Z",
        [GameIconKind.GhostOutline] =
            "F0 M8 3H16L20 7V17L18 19L16 17L14 19L12 17L10 19L8 17L6 19L4 17V7Z" +
            " M9 5H15L18 8V15L16 17L14 15L12 17L10 15L8 17L6 15V8Z" +
            " M9 9H11V11H9Z M13 9H15V11H13Z",
        [GameIconKind.ContentCut] =
            "F0 M5 3L7 3L12 10L17 3L19 3L20 5L15 12L20 19L19 21L17 21L12 14L7 21L5 21L4 19L9 12L4 5Z" +
            " M6 5V8H7V5Z M17 5V8H18V5Z",
        [GameIconKind.CubeOutline] =
            "F0 M12 2L21 7V17L12 22L3 17V7Z" +
            " M3 7L12 12L21 7Z M12 12L12 22L3 17V7Z",

        // =====================================================================
        // COLLECTION: BOSS ICONS
        // =====================================================================
        [GameIconKind.Mountain] = "M12 2L6 12L2 12L2 22H22V12L18 12Z",
        [GameIconKind.WeatherNight] =
            "M10 2L7 3L5 6L4 10L5 14L7 17L10 19L14 19L13 17L11 14L10 10L11 6L13 3Z" +
            " M18 2L19 4L21 5L19 6L18 8L17 6L15 5L17 4Z" +
            " M20 8L21 10L22 10L22 12L20 12L20 10Z",

        // =====================================================================
        // COLLECTION: POWERUP ICONS
        // =====================================================================
        [GameIconKind.FlashOn] = "M13 2L7 13H11L7 22L17 11H13Z M19 4L21 5L22 7L21 9L19 10L17 9L16 7L17 5Z",
        [GameIconKind.WallOutline] =
            "F0 M3 4H21V20H3Z" +
            " M5 6H11V11H5Z M13 6H19V11H13Z" +
            " M5 13H11V18H5Z M13 13H19V18H13Z",
        [GameIconKind.RadioButtonChecked] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M10 8H14L16 10V14L14 16H10L8 14V10Z",
        [GameIconKind.ArrowRightBoldCircleOutline] =
            "F0 M8 2H16L22 8V16L16 22H8L2 16V8Z" +
            " M9 5H15L19 9V15L15 19H9L5 15V9Z" +
            " M8 11V13H14V16L18 12L14 8V11Z",
        [GameIconKind.ShieldFireOutline] =
            "F0 M12 2L20 6V16L16 22H8L4 16V6Z" +
            " M12 5L18 8V15L15 19H9L6 15V8Z" +
            " M12 7L10 11V14L11 16L12 17L13 16L14 14V11Z",
        [GameIconKind.ShoePrint] =
            "M10 2H14L16 4V9L14 11H10L8 9V4Z" +
            " M9 13H15L17 15V19L15 21H9L7 19V15Z",
        [GameIconKind.StarFourPoints] = "M12 1L14 10L23 12L14 14L12 23L10 14L1 12L10 10Z",

        // =====================================================================
        // COLLECTION: COSMETIC ICONS
        // =====================================================================
        [GameIconKind.Flare] =
            "M10 8H14L16 10V14L14 16H10L8 14V10Z" +
            " M11 2H13V6H11Z M11 18H13V22H11Z" +
            " M2 11V13H6V11Z M18 11V13H22V11Z" +
            " M5 5L7 5L8 7L6 8Z M17 5L19 5L18 8L16 7Z" +
            " M5 19L6 16L8 17L7 19Z M18 16L19 19L17 19L16 17Z",
        [GameIconKind.Shimmer] =
            "M9 2L11 6L15 8L11 10L9 14L7 10L3 8L7 6Z" +
            " M18 10L19 13L22 14L19 15L18 18L17 15L14 14L17 13Z" +
            " M7 16L8 19L11 20L8 21L7 23L6 21L3 20L6 19Z",
        [GameIconKind.ImageFilterFrames] =
            "F0 M4 2H20L21 3V21L20 22H4L3 21V3Z" +
            " M5 4H19V20H5Z" +
            " M10 9H14L16 11V15L14 17H10L8 15V11Z" +
            " M11 11H13L14 12V14L13 15H11L10 14V12Z",

        // =====================================================================
        // ACHIEVEMENT ICONS
        // =====================================================================
        [GameIconKind.StarShooting] =
            "M12 6L13.5 10H17L14 12.5L15 17L12 14.5L9 17L10 12.5L7 10H10.5Z" +
            " M2 2L5 5Z M3 7L5.5 8Z M7 3L8 5.5Z",
        [GameIconKind.SwordCross] =
            "M6 2L8 4L7 12L10 15L12 13L14 15L17 12L16 4L18 2L19 4L18 14L15 17L18 20L16 22L12 18L8 22L6 20L9 17L6 14L5 4Z" +
            " M19 2L20 3L19 5L18 4Z",
        [GameIconKind.TimerSand] =
            "M6 2H18V7L14 11L12 12L14 13V18L18 22H6V18L10 14L12 12L10 10V7Z" +
            " M8 4H16V6L12 10L8 6Z M8 20H16L12 16Z",
        [GameIconKind.Flag] =
            "M5 2H7V22H5Z M7 3H19L16 8L19 13H7Z",
        [GameIconKind.CalendarStar] =
            "F0 M3 3H21V21H3Z M5 8H19V19H5Z" +
            " M12 9L13 12H16L14 14L15 17L12 15L9 17L10 14L8 12H11Z",
        [GameIconKind.Lightning] = "M14 2L8 10H11L6 16H9L5 22L18 13H14L19 7H15Z",
        [GameIconKind.BookOpenVariant] =
            "M1 5H11L12 6L13 5H23V19H13L12 20L11 19H1Z" +
            " M11.5 6V19H12.5V6Z" +
            " M3 8H10V9H3Z M3 11H10V12H3Z" +
            " M14 8H21V9H14Z M14 11H21V12H14Z",
        [GameIconKind.ShieldStar] =
            "M12 2L20 6V16L16 22H8L4 16V6Z" +
            " M12 7L13.5 11H17L14 13L15 17L12 14.5L9 17L10 13L7 11H10.5Z",
        [GameIconKind.ShieldCrown] =
            "M12 2L20 6V16L16 22H8L4 16V6Z" +
            " M7 15V10L10 12L12 7L14 12L17 10V15Z",
        [GameIconKind.Cards] =
            "M4 4H16V19H4Z M8 2H20V17H18V4H8Z",
        [GameIconKind.Dice5] =
            "F0 M5 3H19L21 5V19L19 21H5L3 19V5Z" +
            " M8 7H10V9H8Z M14 7H16V9H14Z" +
            " M11 11H13V13H11Z" +
            " M8 15H10V17H8Z M14 15H16V17H14Z",

        // =====================================================================
        // BATTLEPASS / DUNGEON ICONS
        // =====================================================================
        [GameIconKind.Tshirt] =
            "M16 2L20 6V10L16 12V22H8V12L4 10V6L8 2H10L11 4H13L14 2Z",
        [GameIconKind.Tortoise] =
            "M7 7H17L20 10V15L17 18H7L4 15V10Z" +
            " M10 9H14L16 11V13L14 15H10L8 13V11Z" +
            " M4 16L3 18L5 20Z M20 16L21 18L19 20Z",
        [GameIconKind.Magnet] =
            "M6 2H10V8L12 10L14 8V2H18V10L16 14L12 17L8 14L6 10Z" +
            " M6 2H10V4H6Z M14 2H18V4H14Z",
        [GameIconKind.ShieldFire] =
            "M12 2L20 6V16L16 22H8L4 16V6Z" +
            " M12 7L10 11V14L11 16L12 17L13 16L14 14V11Z",
        [GameIconKind.Reload] =
            "M12 4L6 7L4 11H6L7 9L12 6L16 8L18 12L16 16L12 18L8 16L6 13H4L6 17L12 20L18 17L20 14V10L18 7Z" +
            " M20 4L18 6L20 8Z",
        [GameIconKind.TimerOutline] =
            "F0 M10 1H14V3H10Z" +
            " M8 5H16L21 9V17L16 22H8L3 17V9Z" +
            " M9 7H15L19 10V16L15 19H9L5 16V10Z" +
            " M11 9H13V14H16V16H11Z",

        // =====================================================================
        // ALIASE (String-Kompatibilitaet mit Services)
        // =====================================================================
        [GameIconKind.PartyPopper] =
            "M3 16L5 9L10 14Z M5 21L7 14L12 19Z" +
            " M13 2L11 7L15 11L20 9Z" +
            " M17 3L18 5L20 6L19 8L17 7L16 5Z" +
            " M21 8L22 10L23 10L23 12L21 12L21 10Z" +
            " M6 3L7 5L8 5L8 7L6 7L6 5Z",
        [GameIconKind.ShoeSneaker] =
            "M4 16V13L6 11H10L13 8H15L16 9H18L20 11V16Z" +
            " M8 7V10L10 11L12 9V7L10 6H8Z",

        // Bomben-Karten Icons
        [GameIconKind.WeatherFog] =
            "M4 8H10L11 7H17L18 8H20V10H18L17 11H11L10 10H4Z" +
            " M6 13H12L13 12H19L20 13H22V15H20L19 16H13L12 15H6Z" +
            " M3 18H9L10 17H16L17 18H21V20H17L16 21H10L9 20H3Z",
        [GameIconKind.FlipHorizontal] =
            "M11 3H13V21H11Z" +
            " M7 7L3 12L7 17V14H9V10H7Z" +
            " M17 7L21 12L17 17V14H15V10H17Z",
        [GameIconKind.Tornado] =
            "M5 4H19L20 5H4Z" +
            " M6 7H18L19 9H5Z" +
            " M8 11H16L17 13H7Z" +
            " M9 15H15L16 17H8Z" +
            " M11 19H14L13 21H10Z",
        [GameIconKind.CircleSlice8] =
            "M12 2L14 10L22 12L14 14L12 22L10 14L2 12L10 10Z" +
            " M12 6L13 10L17 7L14 11L18 12L14 13L17 17L13 14L12 18L11 14L7 17L10 13L6 12L10 11L7 7L11 10Z",
    };
}
