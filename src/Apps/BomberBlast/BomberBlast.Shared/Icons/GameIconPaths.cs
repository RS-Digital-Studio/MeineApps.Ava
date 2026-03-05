using Avalonia.Media;

namespace BomberBlast.Icons;

/// <summary>
/// Alle Icon-Pfaddaten fuer BomberBlast im Neon Arcade Stil.
/// Jedes Icon ist in einem 24x24 Koordinatenraum definiert.
/// Stil: Eckig, geometrisch, oktagonal statt rund, fett/bold.
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

    // Oktagon-Basis (Kreis-Ersatz): M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z
    // Innerer Oktagon (Ring):        M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z
    private static readonly Dictionary<GameIconKind, string> Paths = new()
    {
        // =====================================================================
        // NAVIGATION
        // =====================================================================

        [GameIconKind.ArrowLeft] =
            "M16 5L7 12L16 19V16L11 12L16 8Z",

        [GameIconKind.ArrowUpBold] =
            "M12 3L4 14H9V21H15V14H20Z",

        [GameIconKind.ArrowUpBoldCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 7L7 14H10V18H14V14H17Z",

        [GameIconKind.ChevronRight] =
            "M8 5L17 12L8 19V16L13 12L8 8Z",

        [GameIconKind.ChevronDoubleUp] =
            "M12 4L5 11H8L12 7.5L16 11H19Z" +
            " M12 12L5 19H8L12 15.5L16 19H19Z",

        [GameIconKind.Close] =
            "M5 3L3 5L10 12L3 19L5 21L12 14L19 21L21 19L14 12L21 5L19 3L12 10Z",

        // =====================================================================
        // STATUS
        // =====================================================================

        [GameIconKind.Check] =
            "M9 16L5 12L3 14L9 20L21 8L19 6Z",

        [GameIconKind.CheckCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M10 15L7 12L8.5 10.5L10 12.5L15.5 7L17 8.5Z",

        [GameIconKind.Lock] =
            "F0 M8 10V7L10 4H14L16 7V10H18V21H6V10Z" +
            " M11 14V18H13V14L14 13H10Z",

        [GameIconKind.LockOpen] =
            "F0 M6 10H18V21H6Z" +
            " M9 8V5L11 3H14L16 5V8H14V5H11V8Z" +
            " M11 14V18H13V14L14 13H10Z",

        [GameIconKind.LockOutline] =
            "F0 M8 10V7L10 4H14L16 7V10H18V21H6V10Z" +
            " M8 12H16V19H8Z",

        // =====================================================================
        // STARS & REWARDS
        // =====================================================================

        [GameIconKind.Star] =
            "M12 2L14.5 9H22L16.5 14.5L18 22L12 18L6 22L7.5 14.5L2 9H9.5Z",

        [GameIconKind.StarOutline] =
            "F0 M12 2L14.5 9H22L16.5 14.5L18 22L12 18L6 22L7.5 14.5L2 9H9.5Z" +
            " M12 7L10.5 11H7L9.5 14L8.5 18L12 15.5L15.5 18L14.5 14L17 11H13.5Z",

        [GameIconKind.StarCircleOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 4.5L17 7L19 12L17 17L12 19.5L7 17L5 12L7 7Z" +
            " M12 7L13.5 10.5H17L14.5 13L15.5 17L12 15L8.5 17L9.5 13L7 10.5H10.5Z",

        [GameIconKind.Crown] =
            "M2 17V8L6 12L12 3L18 12L22 8V17Z" +
            " M3 18H21V21H3Z",

        [GameIconKind.Trophy] =
            "M7 2H17V5H19V10L17 12H15V16H16V19H8V16H9V12H7L5 10V5H7Z",

        [GameIconKind.TrophyBroken] =
            "M7 2H11L12 8L11 14V16H8V16H9V12H7L5 10V5H7Z" +
            " M13 2H17V5H19V10L17 12H15V16H16V19H14V14L13 8Z",

        [GameIconKind.TrophyOutline] =
            "F0 M7 2H17V5H19V10L17 12H15V16H16V19H8V16H9V12H7L5 10V5H7Z" +
            " M9 4V10H11V14H13V10H15V4Z",

        [GameIconKind.Medal] =
            "M10 2L8 5L10 8L7 10V16L12 21L17 16V10L14 8L16 5L14 2Z",

        // =====================================================================
        // COMBAT & GAME
        // =====================================================================

        [GameIconKind.Sword] =
            "M11 1H13L14 4L13 14L15 16V18H13V21L12 23L11 21V18H9V16L11 14L10 4Z",

        [GameIconKind.Shield] =
            "M12 2L20 6V14L12 22L4 14V6Z",

        [GameIconKind.Skull] =
            "M7 3H17L20 7V13L17 16H16L15 20H13V17H11V20H9L8 16H7L4 13V7Z" +
            " M9 8V12H11V8Z M13 8V12H15V8Z" +
            " M10 14H14L12 16Z",

        [GameIconKind.Fire] =
            "M12 1L15 7L18 6L16 12L19 11L17 16L19 18L15 17L14 21L12 23L10 21L9 17L5 18L7 16L4 11L7 12L5 6L8 7Z",

        [GameIconKind.Bomb] =
            "M15 1H17V3L16 4H14L15 1Z" +
            " M12 5L18 7L21 12V17L18 21L12 23L6 21L3 17V12L6 7Z" +
            " M9 11L10 10L11 11L10 12Z",

        [GameIconKind.Ghost] =
            "M8 3H16L19 6V16L17 20L15 17L13 20L11 17L9 20L7 17L5 20V6Z" +
            " M9 8V11H11V8Z M13 8V11H15V8Z",

        // =====================================================================
        // ECONOMY
        // =====================================================================

        [GameIconKind.CircleMultiple] =
            "M8 2L14 2L18 5L20 9V11L18 13L14 13L16 15V17L14 19L10 20L6 18L4 15V13L6 11L8 11L6 9V7Z" +
            " M8 4L14 4L17 6L18 9L17 11L14 12L8 12L6 11L5 9L6 7Z",

        [GameIconKind.CircleOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z",

        [GameIconKind.CurrencyUsd] =
            "M11 2H13V4H16V8H13V4H11V8H8V4H11Z" +
            " M8 10H16V14H8Z" +
            " M11 16H8V20H11V22H13V20H16V16H13V20H11Z",

        [GameIconKind.Diamond] =
            "M12 2L22 12L12 22L2 12Z",

        [GameIconKind.DiamondStone] =
            "M7 3H17L22 10L12 22L2 10Z" +
            " M7 3L5 10H19L17 3Z",

        [GameIconKind.Gift] =
            "M3 9H21V21H3Z" +
            " M8 4L10 2L12 6L14 2L16 4L14 7H10Z",

        [GameIconKind.TreasureChest] =
            "M3 10H21V21H3Z" +
            " M4 5H20L21 10H3Z" +
            " M10 13H14V17H10Z",

        // =====================================================================
        // CARDS & DUNGEON
        // =====================================================================

        [GameIconKind.CardsPlaying] =
            "M4 4H16V19H4Z" +
            " M8 2H20V17H18V4H8Z",

        [GameIconKind.CardsPlayingOutline] =
            "F0 M4 4H16V19H4Z M6 6H14V17H6Z" +
            " M8 2H20V17H18V4H8Z",

        [GameIconKind.CartPlus] =
            "M2 3H6L8 13H18L21 5H9Z" +
            " M8 17H11V20H8Z M17 17H20V20H17Z" +
            " M12.5 6H13.5V8H15.5V9H13.5V11H12.5V9H10.5V8H12.5Z",

        [GameIconKind.Stairs] =
            "M2 22V18H6V14H10V10H14V6H18V2H22V22Z",

        [GameIconKind.Dice] =
            "F0 M3 3H21V21H3Z" +
            " M8 7H10V9H8Z M11 10.5H13V12.5H11Z M14 14H16V16H14Z",

        [GameIconKind.LinkVariant] =
            "M5 10L7 8H11L13 10V14L11 16H7L5 14Z" +
            " M11 10L13 8H17L19 10V14L17 16H13L11 14Z",

        // =====================================================================
        // TIME
        // =====================================================================

        [GameIconKind.Clock] =
            "M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M11 6H13V12H17V14H11Z",

        [GameIconKind.ClockFast] =
            "M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M11 6H13V12H17V14H11Z" +
            " M20 7H23V8H20Z M21 11H24V12H21Z M20 15H23V16H20Z",

        [GameIconKind.ClockOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z" +
            " M11 7H13V12H16V14H11Z",

        [GameIconKind.Timer] =
            "M10 1H14V3H10Z" +
            " M12 4L18 7L21 12L18 18L12 22L6 18L3 12L6 7Z" +
            " M11 8H13V14H17V16H11Z",

        [GameIconKind.CalendarCheck] =
            "F0 M3 3H21V21H3Z M5 8H19V19H5Z" +
            " M10 16L7 13L8.5 11.5L10 13.5L15.5 9.5L17 11Z",

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
            " M12 8L15 10L16 12L15 14L12 16L9 14L8 12L9 10Z",

        [GameIconKind.CogOutline] =
            "F0 M10 1H14L15 4L17 5L20 3L22 6L20 9L21 12L20 15L22 18L20 21L17 19L15 20L14 23H10L9 20L7 19L4 21L2 18L4 15L3 12L4 9L2 6L4 3L7 5L9 4Z" +
            " M12 6L16 8L18 12L16 16L12 18L8 16L6 12L8 8Z",

        [GameIconKind.Gamepad] =
            "M5 8H19L22 11V16L19 19H15L13 17H11L9 19H5L2 16V11Z" +
            " M7 11V13H9V11Z M15 11H17V13H15Z",

        [GameIconKind.GamepadVariant] =
            "M3 9H21L23 12V17L20 20H16L13 18H11L8 20H4L1 17V12Z" +
            " M7 12V14H9V12Z M15 12H17V14H15Z",

        [GameIconKind.VolumeHigh] =
            "M3 9V15H7L13 20V4L7 9Z" +
            " M15 8L17 10V14L15 16Z" +
            " M17 5L20 8V16L17 19Z",

        [GameIconKind.Translate] =
            "M2 3H12V5H8L6 9L8 13H6L5 11H3L2 13V3Z" +
            " M12 11H22V21H12Z M15 13L17 19L19 13Z",

        [GameIconKind.AdsOff] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z" +
            " M4 3L5 2L21 20L20 21Z",

        // =====================================================================
        // CLOUD
        // =====================================================================

        [GameIconKind.Cloud] =
            "M6 18L4 16V13L6 11L8 8H12L15 6L18 8L20 8L22 10V14L20 16Z",

        [GameIconKind.CloudCheck] =
            "M6 18L4 16V13L6 11L8 8H12L15 6L18 8L20 8L22 10V14L20 16Z" +
            " M9 14L11 16L17 10Z",

        [GameIconKind.CloudDownload] =
            "M6 18L4 16V13L6 11L8 8H12L15 6L18 8L20 8L22 10V14L20 16Z" +
            " M11.5 9H12.5V14H11.5Z M9 13L12 16L15 13L14 12L12.5 14V9H11.5V14L10 12Z",

        [GameIconKind.CloudOffOutline] =
            "M3 4L5 2L22 21L20 23Z" +
            " M8 8L6 11L4 13V16L6 18H17Z" +
            " M15 6L18 8H20L22 10V14L20 16H19Z",

        [GameIconKind.CloudSync] =
            "M6 18L4 16V13L6 11L8 8H12L15 6L18 8L20 8L22 10V14L20 16Z" +
            " M9 13L13 10L13 12H15L15 10L17 13H15L15 14L13 14L13 16L9 13Z",

        [GameIconKind.CloudUpload] =
            "M6 18L4 16V13L6 11L8 8H12L15 6L18 8L20 8L22 10V14L20 16Z" +
            " M11.5 16H12.5V11H11.5Z M9 12L12 9L15 12L14 13L12.5 11V16H11.5V11L10 13Z",

        // =====================================================================
        // CHARTS
        // =====================================================================

        [GameIconKind.ChartLine] =
            "M3 20V4H5V16L9 12L13 15L18 8L21 11V20Z",

        [GameIconKind.ChartDonut] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 7L16 8.5L17.5 12L16 15.5L12 17L8 15.5L6.5 12L8 8.5Z",

        // =====================================================================
        // NATURE & WORLD
        // =====================================================================

        [GameIconKind.PineTree] =
            "M12 2L18 10H15L20 17H4L9 10H6Z" +
            " M11 17H13V22H11Z",

        [GameIconKind.Waves] =
            "M2 8L4 6L7 8L10 6L13 8L16 6L19 8L22 6V9L19 11L16 9L13 11L10 9L7 11L4 9Z" +
            " M2 14L4 12L7 14L10 12L13 14L16 12L19 14L22 12V15L19 17L16 15L13 17L10 15L7 17L4 15Z",

        [GameIconKind.Terrain] =
            "M2 20L8 8L12 14L17 5L22 20Z",

        [GameIconKind.WeatherSunny] =
            "M12 7L15 8L17 11V13L15 16L12 17L9 16L7 13V11L9 8Z" +
            " M11 2H13V5H11Z M11 19H13V22H11Z" +
            " M2 11V13H5V11Z M19 11V13H22V11Z" +
            " M5 4L7 4L8 6L6 7Z M17 4L19 4L18 7L16 6Z" +
            " M5 20L6 17L8 18L7 20Z M18 17L19 20L17 20L16 18Z",

        [GameIconKind.Snowflake] =
            "M11 2H13V22H11Z" +
            " M4 7L6 6L12 12L6 18L4 17L9 12Z" +
            " M20 7L18 6L12 12L18 18L20 17L15 12Z",

        [GameIconKind.Water] =
            "M12 2L18 11L18 16L16 19L12 21L8 19L6 16L6 11Z",

        [GameIconKind.Seed] =
            "M12 3L15 6L16 10L15 14L12 18L9 14L8 10L9 6Z" +
            " M11 8V18H13V8Z",

        [GameIconKind.Clover] =
            "M10 3L12 2L14 3L14 6L12 7L10 6Z" +
            " M18 10L19 12L18 14L15 14L14 12L15 10Z" +
            " M14 18L12 19L10 18L10 15L12 14L14 15Z" +
            " M6 14L5 12L6 10L9 10L10 12L9 14Z" +
            " M11 19V22H13V19Z",

        // =====================================================================
        // BUILDINGS
        // =====================================================================

        [GameIconKind.Factory] =
            "M2 21V13L6 9V13L10 9V13L14 9V4H22V21Z",

        [GameIconKind.Pillar] =
            "M6 3H18V5H16V19H18V21H6V19H8V5H6Z",

        [GameIconKind.Store] =
            "M4 3H20L22 8L20 10L18 8L16 10L14 8L12 10L10 8L8 10L6 8L4 10L2 8Z" +
            " M4 10V21H20V10Z" +
            " M10 14H14V21H10Z",

        [GameIconKind.Home] =
            "M12 3L2 12H5V21H10V15H14V21H19V12H22Z",

        // =====================================================================
        // PEOPLE & PROFILE
        // =====================================================================

        [GameIconKind.AccountCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M10 7H14L15 9V11L13 12H11L9 11V9Z" +
            " M7 17V15L9 13H15L17 15V17Z",

        [GameIconKind.Heart] =
            "M12 21L3 12V8L5 5L8 4L12 7L16 4L19 5L21 8V12Z",

        [GameIconKind.HeartPlus] =
            "F0 M12 21L3 12V8L5 5L8 4L12 7L16 4L19 5L21 8V12Z" +
            " M11 10H13V12H15V14H13V16H11V14H9V12H11Z",

        [GameIconKind.HeartPulse] =
            "M12 21L3 12V8L5 5L8 4L12 7L16 4L19 5L21 8V12Z" +
            " M5 11H8L10 8L12 14L14 10L16 11H19Z",

        [GameIconKind.ContentSave] =
            "F0 M3 3H17L21 7V21H3Z" +
            " M7 3V9H15V3Z M13 4H14V8H13Z" +
            " M7 12H17V19H7Z",

        // =====================================================================
        // INFO & HELP
        // =====================================================================

        [GameIconKind.HelpCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M10 8L11 7H14L15 9L13 11L12 12V14H11V12L13 10L14 9L13 8H11L10 9Z" +
            " M11 16H13V18H11Z",

        [GameIconKind.InformationOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z" +
            " M11 7H13V9H11Z M11 11H13V17H11Z",

        [GameIconKind.AlertCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M11 7H13V14H11Z M11 16H13V18H11Z",

        [GameIconKind.LightbulbOn] =
            "M9 4L12 2L15 4L17 7V12L15 15V18H9V15L7 12V7Z" +
            " M9 19H15V21H9Z" +
            " M10 22H14Z",

        // =====================================================================
        // MEDIA
        // =====================================================================

        [GameIconKind.Video] =
            "M3 6V18H16V14L22 18V6L16 10V6Z",

        [GameIconKind.Play] =
            "M6 3L20 12L6 21Z",

        [GameIconKind.SkipNext] =
            "M4 4L16 12L4 20Z M18 4H21V20H18Z",

        [GameIconKind.Repeat] =
            "M4 7H18L20 9V10H18V9H6L4 11V14L6 16H8V18H6L4 16L2 14V9Z" +
            " M20 17H6L4 15V14H6V15H18L20 13V10L18 8H16V6H18L20 8L22 10V15Z",

        [GameIconKind.Refresh] =
            "M12 4L18 7L20 11H18L17 9L12 6L8 8L6 12L8 16L12 18L16 16L18 13H20L18 17L12 20L6 18L4 14V10L6 7Z",

        // =====================================================================
        // MISC
        // =====================================================================

        [GameIconKind.Shuffle] =
            "M3 7H7L10 12L7 17H3V15H6L9 12L6 9H3Z" +
            " M14 7H21V9H18L15 12L18 15H21V17H14L11 12Z" +
            " M19 5L22 7L19 9Z M19 15L22 17L19 19Z",

        [GameIconKind.Speedometer] =
            "M12 2L20 6L22 14L18 20H6L2 14L4 6Z" +
            " M12 13L15 7Z",

        [GameIconKind.MapMarker] =
            "M12 2L17 5L19 10L12 22L5 10L7 5Z" +
            " M12 7L14 9V11L12 13L10 11V9Z",

        [GameIconKind.Target] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 6L16 8L18 12L16 16L12 18L8 16L6 12L8 8Z" +
            " M12 9L14 10L15 12L14 14L12 15L10 14L9 12L10 10Z",

        [GameIconKind.Palette] =
            "M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M8 8H10V10H8Z M14 6H16V8H14Z" +
            " M16 11H18V13H16Z M7 14H9V16H7Z",

        [GameIconKind.PaletteOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 4.5L17 7L19 12L17 17L12 19.5L7 17L5 12L7 7Z" +
            " M8 8H10V10H8Z M14 6H16V8H14Z" +
            " M16 11H18V13H16Z M7 14H9V16H7Z",

        [GameIconKind.ViewGrid] =
            "M3 3H11V11H3Z M13 3H21V11H13Z" +
            " M3 13H11V21H3Z M13 13H21V21H13Z",

        [GameIconKind.SchoolOutline] =
            "M12 3L22 9L12 15L2 9Z" +
            " M6 11V18L12 21L18 18V11L12 14Z",

        [GameIconKind.BookOpen] =
            "M2 5H11L12 6L13 5H22V19H13L12 20L11 19H2Z" +
            " M11 6H12V19H11Z M12 6H13V19H12Z",

        [GameIconKind.GooglePlay] =
            "M4 2L20 12L4 22Z" +
            " M4 2L14 12L4 22Z",

        [GameIconKind.Flash] =
            "M13 2L7 13H11L7 22L17 11H13Z",

        [GameIconKind.FlashOutline] =
            "F0 M13 2L7 13H11L7 22L17 11H13Z" +
            " M12.5 5L9 12H12L9 18L15 12H12Z",

        // =====================================================================
        // POWERUP & MECHANIC ICONS
        // =====================================================================

        [GameIconKind.Account] =
            "M12 4L15 5L16 8V10L14 12H10L8 10V8L9 5Z" +
            " M5 20V18L7 15H17L19 18V20Z",

        [GameIconKind.FlashAlert] =
            "M13 1L7 11H11L8 18L17 9H13Z" +
            " M11 19H13V21H11Z",

        [GameIconKind.ArrowRightCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M8 11H15L12 8L14 6L19 12L14 18L12 16L15 13H8Z",

        [GameIconKind.ShieldOutline] =
            "F0 M12 2L20 6V14L12 22L4 14V6Z" +
            " M12 5L7 8V13L12 19L17 13V8Z",

        [GameIconKind.HelpCircleOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 4.5L17 7L19 12L17 17L12 19.5L7 17L5 12L7 7Z" +
            " M10 9L12 7H14L15 9L13 11V13H12V11L14 9H12L11 10Z" +
            " M11 16H13V18H11Z",

        [GameIconKind.Shoe] =
            "M4 10L6 6H10L12 8L14 6H16L20 10V14H18L16 16H6L4 14Z",

        [GameIconKind.DotsHorizontal] =
            "M4 10H8V14H4Z M10 10H14V14H10Z M16 10H20V14H16Z",

        [GameIconKind.StarCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 6L13.5 10H17.5L14.5 12.5L15.5 17L12 14.5L8.5 17L9.5 12.5L6.5 10H10.5Z",

        [GameIconKind.SkullOutline] =
            "F0 M7 3H17L20 7V13L17 16H16L15 20H13V17H11V20H9L8 16H7L4 13V7Z" +
            " M9 5H15L17 8V12L15 14H14V18H13V15H11V18H10V14H9L7 12V8Z" +
            " M9 8H11V11H9Z M13 8H15V11H13Z" +
            " M10 13H14L12 15Z",

        [GameIconKind.ArrowRightBold] =
            "M12 3L20 12L12 21V16H4V8H12Z",

        [GameIconKind.SwapHorizontal] =
            "M8 4L2 9L8 14V11H14V7H8Z" +
            " M16 10L22 15L16 20V17H10V13H16Z",

        // =====================================================================
        // SKIN-KATEGORIE ICONS
        // =====================================================================

        [GameIconKind.Trail] =
            "M20 6L18 8L14 8L12 10L10 8L6 8L4 6Z" +
            " M20 12L18 14L14 14L12 16L10 14L6 14L4 12Z" +
            " M16 18L14 20L12 20L10 20L8 18Z",

        [GameIconKind.Celebration] =
            "M12 2L14 8L10 14L8 22L6 22L10 12L8 8Z" +
            " M16 4L18 6L16 10L20 18L18 19L14 10L17 6Z" +
            " M4 6L6 4L7 8Z M18 2L20 3L19 7Z",

        [GameIconKind.CardFrame] =
            "F0 M4 2H20V22H4Z M6 4H18V20H6Z" +
            " M8 8H16V16H8Z",

        // =====================================================================
        // COLLECTION: ENEMY ICONS
        // =====================================================================

        [GameIconKind.EmoticonOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 4.5L17 7L19 12L17 17L12 19.5L7 17L5 12L7 7Z" +
            " M9 9H11V11H9Z M13 9H15V11H13Z M9 14H15L14 16H10Z",

        [GameIconKind.EmoticonAngryOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 4.5L17 7L19 12L17 17L12 19.5L7 17L5 12L7 7Z" +
            " M8 8L11 10V11H9V10Z M16 8L13 10V11H15V10Z M9 15L12 13L15 15Z",

        [GameIconKind.EmoticonCoolOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 4.5L17 7L19 12L17 17L12 19.5L7 17L5 12L7 7Z" +
            " M7 9H11V11H7Z M13 9H17V11H13Z M9 14H15L13 16H11Z",

        [GameIconKind.EmoticonDevilOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 4.5L17 7L19 12L17 17L12 19.5L7 17L5 12L7 7Z" +
            " M9 9H11V11H9Z M13 9H15V11H13Z M9 14L12 16L15 14Z" +
            " M7 3L9 6Z M17 3L15 6Z",

        [GameIconKind.Jellyfish] =
            "M8 4H16L19 8V12L17 14L18 18L16 20L14 17L12 20L10 17L8 20L6 18L7 14L5 12V8Z" +
            " M9 8V10H11V8Z M13 8V10H15V8Z",

        [GameIconKind.Run] =
            "M14 3L16 3L16 5L14 5Z" +
            " M11 6L15 6L17 9L15 9L16 12L13 17L15 21L13 22L10 17L12 13L10 10L7 12L6 10L10 7Z",

        [GameIconKind.GhostOutline] =
            "F0 M8 3H16L19 6V16L17 20L15 17L13 20L11 17L9 20L7 17L5 20V6Z" +
            " M10 5H14L16 7V14L15 16L13 14L11 16L9 14L8 16V7Z" +
            " M9 8V11H11V8Z M13 8V11H15V8Z",

        [GameIconKind.ContentCut] =
            "M12 2L20 12L12 14L4 12Z" +
            " M8 16L10 14L8 20Z M16 16L14 14L16 20Z",

        [GameIconKind.CubeOutline] =
            "F0 M12 2L22 8V16L12 22L2 16V8Z" +
            " M12 5L18 8.5V15L12 18.5L6 15V8.5Z" +
            " M12 12V18.5 M12 12L6 8.5 M12 12L18 8.5",

        // =====================================================================
        // COLLECTION: BOSS ICONS
        // =====================================================================

        [GameIconKind.Mountain] =
            "M2 20L9 6L13 12L17 4L22 20Z",

        [GameIconKind.WeatherNight] =
            "M12 4L14 2L16 4L18 2L18 6L20 6L18 8L20 10L16 10L16 14L14 12L12 14L10 12L8 14L8 10L4 10L6 8L4 6L6 6L6 2L8 4L10 2Z",

        // =====================================================================
        // COLLECTION: POWERUP ICONS
        // =====================================================================

        [GameIconKind.FlashOn] =
            "M13 2L7 13H11L7 22L17 11H13Z",

        [GameIconKind.WallOutline] =
            "F0 M2 4H22V20H2Z M4 6H10V11H4Z M12 6H22V11H12Z" +
            " M2 13H8V18H2Z M10 13H16V18H10Z M18 13H22V18H18Z",

        [GameIconKind.RadioButtonChecked] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z" +
            " M12 8L15 9.5L16.5 12L15 14.5L12 16L9 14.5L7.5 12L9 9.5Z",

        [GameIconKind.ArrowRightBoldCircleOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 4.5L17 7L19 12L17 17L12 19.5L7 17L5 12L7 7Z" +
            " M8 11H15L12 8L14 6L19 12L14 18L12 16L15 13H8Z",

        [GameIconKind.ShieldFireOutline] =
            "F0 M12 2L20 6V14L12 22L4 14V6Z" +
            " M12 5L17 8V13L12 19L7 13V8Z" +
            " M12 8L14 12L12 14L10 12Z",

        [GameIconKind.ShoePrint] =
            "M8 4H14L16 6V10L14 12H10L8 10Z" +
            " M9 14H13L14 16V20L12 22H10L8 20V16Z",

        [GameIconKind.StarFourPoints] =
            "M12 2L14 10L22 12L14 14L12 22L10 14L2 12L10 10Z",

        // =====================================================================
        // COLLECTION: COSMETIC + EFFECT ICONS
        // =====================================================================

        [GameIconKind.Flare] =
            "M12 2V8L14 10H20V14H14L12 16V22H12V16L10 14H4V10H10L12 8V2Z",

        [GameIconKind.Shimmer] =
            "M8 2L9 5L12 6L9 7L8 10L7 7L4 6L7 5Z" +
            " M16 10L17 13L20 14L17 15L16 18L15 15L12 14L15 13Z" +
            " M6 14L7 16L9 17L7 18L6 20L5 18L3 17L5 16Z",

        [GameIconKind.ImageFilterFrames] =
            "F0 M6 4H22V18H6Z M8 6H20V16H8Z" +
            " M2 8H4V22H18V20H4V8Z",

        // =====================================================================
        // ACHIEVEMENT ICONS
        // =====================================================================

        [GameIconKind.StarShooting] =
            "M4 2L6 8L12 6L10 12L16 10L14 16L20 14L22 22L14 18L16 12L10 14L12 8L6 10Z",

        [GameIconKind.SwordCross] =
            "M6 2L8 4L7 12L10 14L12 12L14 14L17 12L16 4L18 2L19 4L18 14L15 16L18 19L16 21L12 17L8 21L6 19L9 16L6 14L5 4Z",

        [GameIconKind.TimerSand] =
            "M6 2H18V6L14 12L18 18V22H6V18L10 12L6 6Z" +
            " M8 4V5L12 10L16 5V4Z M8 20V19L12 14L16 19V20Z",

        [GameIconKind.Flag] =
            "M5 2H7V22H5Z M7 3H19L16 8L19 13H7Z",

        [GameIconKind.CalendarStar] =
            "F0 M3 3H21V21H3Z M5 8H19V19H5Z" +
            " M12 9L13 12H16L13.5 14L14.5 17L12 15L9.5 17L10.5 14L8 12H11Z",

        [GameIconKind.Lightning] =
            "M13 2L7 13H11L7 22L17 11H13Z",

        [GameIconKind.BookOpenVariant] =
            "M2 5H11L12 6L13 5H22V19H13L12 20L11 19H2Z" +
            " M11 6H12V19H11Z M12 6H13V19H12Z",

        [GameIconKind.ShieldStar] =
            "M12 2L20 6V14L12 22L4 14V6Z" +
            " M12 6L13.5 10H17L14 12.5L15 16.5L12 14L9 16.5L10 12.5L7 10H10.5Z",

        [GameIconKind.ShieldCrown] =
            "M12 2L20 6V14L12 22L4 14V6Z" +
            " M7 14V9L9 11L12 7L15 11L17 9V14Z",

        [GameIconKind.Cards] =
            "M4 4H16V19H4Z" +
            " M8 2H20V17H18V4H8Z",

        [GameIconKind.Dice5] =
            "F0 M3 3H21V21H3Z" +
            " M7 7H9V9H7Z M15 7H17V9H15Z M11 11H13V13H11Z M7 15H9V17H7Z M15 15H17V17H15Z",

        // =====================================================================
        // BATTLEPASS / DUNGEON ICONS
        // =====================================================================

        [GameIconKind.Tshirt] =
            "M2 6L6 2H10L12 4L14 2H18L22 6V10L18 12V21H6V12L2 10Z",

        [GameIconKind.Tortoise] =
            "M8 8H16L19 12V16L16 18H8L5 16V12Z" +
            " M6 18L4 20Z M18 18L20 20Z M10 18V21H14V18Z" +
            " M10 10V14H14V10Z",

        [GameIconKind.Magnet] =
            "M6 4H10V8L8 16L10 18H14L16 16V8H20V4H16V8L14 14H10L8 8V4Z",

        [GameIconKind.ShieldFire] =
            "M12 2L20 6V14L12 22L4 14V6Z" +
            " M12 7L14 11L12 14L10 11Z",

        [GameIconKind.Reload] =
            "M12 4L18 7L20 11H18L17 9L12 6L8 8L6 12L8 16L12 18L16 16L18 13H20L18 17L12 20L6 18L4 14V10L6 7Z",

        [GameIconKind.TimerOutline] =
            "F0 M10 1H14V3H10Z" +
            " M12 4L18 7L21 12L18 18L12 22L6 18L3 12L6 7Z" +
            " M12 7L16 9L18 12L16 16L12 18L8 16L6 12L8 9Z" +
            " M11 8H13V13H16V15H11Z",

        // =====================================================================
        // ALIASE (String-Kompatibilitaet mit Services)
        // =====================================================================

        [GameIconKind.PartyPopper] =
            "M12 2L14 8L10 14L8 22L6 22L10 12L8 8Z" +
            " M16 4L18 6L16 10L20 18L18 19L14 10L17 6Z" +
            " M4 6L6 4L7 8Z M18 2L20 3L19 7Z",

        [GameIconKind.ShoeSneaker] =
            "M4 10L6 6H10L12 8L14 6H16L20 10V14H18L16 16H6L4 14Z",
    };
}
