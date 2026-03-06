using Avalonia.Media;

namespace HandwerkerImperium.Icons;

/// <summary>
/// Alle Icon-Pfaddaten fuer HandwerkerImperium im Warme Werkstatt Stil.
/// 24x24 Koordinatenraum. Geometrisch, handwerklich, warm.
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
        [GameIconKind.ArrowDown] = "M12 21L20 12L16 12L16 3L8 3L8 12L4 12Z",
        [GameIconKind.ArrowDownBold] = "M12 21L4 10H9V3H15V10H20Z",
        [GameIconKind.ArrowLeft] = "M16 5L7 12L16 19V16L11 12L16 8Z",
        [GameIconKind.ArrowRight] = "M8 5L17 12L8 19V16L13 12L8 8Z",
        [GameIconKind.ArrowUpBold] = "M12 3L4 14H9V21H15V14H20Z",
        [GameIconKind.ChevronRight] = "M10 3L19 12L10 21L8 19L15 12L8 5Z",
        // Pfeil nach unten (Gegenstueck zu ChevronUp)
        [GameIconKind.ChevronDown] = "M12 16L4 7H7L12 11.5L17 7H20Z",
        [GameIconKind.ChevronUp] = "M12 8L4 17H7L12 12.5L17 17H20Z",
        [GameIconKind.Close] = "M5 3L3 5L10 12L3 19L5 21L12 14L19 21L21 19L14 12L21 5L19 3L12 10Z",
        [GameIconKind.Plus] = "M10 3H14V10H21V14H14V21H10V14H3V10H10Z",

        // =====================================================================
        // STATUS
        // =====================================================================
        [GameIconKind.CheckCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M10 15L7 12L8.5 10.5L10 12.5L15.5 7L17 8.5Z",
        [GameIconKind.CheckDecagram] =
            "F0 M12 1L14.5 4L18 3L19 6.5L22 8.5L21 12L22 15.5L19 17.5L18 21L14.5 20L12 23L9.5 20L6 21L5 17.5L2 15.5L3 12L2 8.5L5 6.5L6 3L9.5 4Z" +
            " M10 15L7 12L8.5 10.5L10 12.5L15.5 7L17 8.5Z",
        [GameIconKind.Lock] =
            "F0 M8 10V7L10 4H14L16 7V10H18V21H6V10Z" +
            " M11 14V18H13V14L14 13H10Z",
        // Offenes Schloss (Buegel oben offen)
        [GameIconKind.LockOpenVariant] =
            "F0 M8 10V7L10 4H14L16 7V5H14L12 6L10 5V7L8 10Z M6 10H18V21H6Z" +
            " M11 14V18H13V14L14 13H10Z",
        [GameIconKind.LockOutline] =
            "F0 M8 10V7L10 4H14L16 7V10H18V21H6V10Z M8 12H16V19H8Z",
        [GameIconKind.Loading] =
            "M12 2L16 4L14 6L12 5L8 7L6 12L8 17L12 19L16 17L18 12L16 7L14 6L16 4L19 6L21 10V14L19 18L12 22L5 18L3 14V10L5 6Z",
        [GameIconKind.Refresh] =
            "M12 4L18 7L20 11H18L17 9L12 6L8 8L6 12L8 16L12 18L16 16L18 13H20L18 17L12 20L6 18L4 14V10L6 7Z",
        [GameIconKind.Restore] =
            "M12 4L6 7L4 11H6L7 9L12 6L16 8L18 12L16 16L12 18L8 16L6 13H4L6 17L12 20L18 17L20 14V10L18 7Z",
        [GameIconKind.Stop] = "F0 M8 3H16L21 8V16L16 21H8L3 16V8Z M10 7H14L17 10V14L14 17H10L7 14V10Z",

        // =====================================================================
        // STARS & REWARDS
        // =====================================================================
        [GameIconKind.Star] = "M12 2L14.5 9H22L16.5 14.5L18 22L12 18L6 22L7.5 14.5L2 9H9.5Z",
        [GameIconKind.StarCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 6L13.5 10H17.5L14.5 12.5L15.5 17L12 14.5L8.5 17L9.5 12.5L6.5 10H10.5Z",
        [GameIconKind.StarFourPoints] = "M12 2L14 10L22 12L14 14L12 22L10 14L2 12L10 10Z",
        [GameIconKind.StarOutline] =
            "F0 M12 2L14.5 9H22L16.5 14.5L18 22L12 18L6 22L7.5 14.5L2 9H9.5Z" +
            " M12 7L10.5 11H7L9.5 14L8.5 18L12 15.5L15.5 18L14.5 14L17 11H13.5Z",
        [GameIconKind.StarShooting] =
            "M4 2L6 8L12 6L10 12L16 10L14 16L20 14L22 22L14 18L16 12L10 14L12 8L6 10Z",
        [GameIconKind.Crown] = "M2 17V8L6 12L12 3L18 12L22 8V17Z M3 18H21V21H3Z",
        [GameIconKind.Trophy] = "M7 2H17V5H19V10L17 12H15V16H16V19H8V16H9V12H7L5 10V5H7Z",
        [GameIconKind.TrophyAward] =
            "M7 2H17V5H19V10L17 12H15V15L17 16V18L12 21L7 18V16L9 15V12H7L5 10V5H7Z",
        [GameIconKind.TrophyBroken] =
            "M7 2H11L12 8L11 14V16H8V16H9V12H7L5 10V5H7Z" +
            " M13 2H17V5H19V10L17 12H15V16H16V19H14V14L13 8Z",
        [GameIconKind.TrophyOutline] =
            "F0 M7 2H17V5H19V10L17 12H15V16H16V19H8V16H9V12H7L5 10V5H7Z" +
            " M9 4V10H11V14H13V10H15V4Z",
        [GameIconKind.TrophyVariant] =
            "M7 2H17V5H19V10L17 12H15V16H16V19H8V16H9V12H7L5 10V5H7Z" +
            " M12 7L13 9.5H15L13.5 11L14 13L12 11.5L10 13L10.5 11L9 9.5H11Z",
        // Gefuellte Medaille (fuer Silver-Prestige-Tier)
        [GameIconKind.Medal] =
            "M10 2L8 5L10 8L7 10V16L12 21L17 16V10L14 8L16 5L14 2Z" +
            " M12 11L13 13H15L13.5 14.5L14 16.5L12 15L10 16.5L10.5 14.5L9 13H11Z",
        [GameIconKind.MedalOutline] =
            "F0 M10 2L8 5L10 8L7 10V16L12 21L17 16V10L14 8L16 5L14 2Z" +
            " M10 10L9 11V15L12 18L15 15V11L14 10Z",

        // =====================================================================
        // COMBAT & GUILD
        // =====================================================================
        [GameIconKind.Sword] = "M11 1H13L14 4L13 14L15 16V18H13V21L12 23L11 21V18H9V16L11 14L10 4Z",
        [GameIconKind.SwordCross] =
            "M6 2L8 4L7 12L10 14L12 12L14 14L17 12L16 4L18 2L19 4L18 14L15 16L18 19L16 21L12 17L8 21L6 19L9 16L6 14L5 4Z",
        [GameIconKind.Shield] = "M12 2L20 6V14L12 22L4 14V6Z",
        [GameIconKind.ShieldAccount] =
            "M12 2L20 6V14L12 22L4 14V6Z M10 9H14L15 11V12L13 13H11L9 12V11Z M8 17V15L10 14H14L16 15V17Z",
        [GameIconKind.ShieldAlert] =
            "F0 M12 2L20 6V14L12 22L4 14V6Z M11 7H13V14H11Z M11 16H13V18H11Z",
        [GameIconKind.ShieldCheck] =
            "M12 2L20 6V14L12 22L4 14V6Z M10 14L7 11L8.5 9.5L10 11.5L15.5 6.5L17 8Z",
        [GameIconKind.ShieldCrown] =
            "M12 2L20 6V14L12 22L4 14V6Z M7 14V9L9 11L12 7L15 11L17 9V14Z",
        [GameIconKind.ShieldHalfFull] =
            "M12 2L20 6V14L12 22L4 14V6Z M12 5V19L7 15V8Z",
        [GameIconKind.ShieldHome] =
            "M12 2L20 6V14L12 22L4 14V6Z M12 7L7 11V15H10V13H14V15H17V11Z",
        [GameIconKind.ShieldLock] =
            "F0 M12 2L20 6V14L12 22L4 14V6Z" +
            " M10 12V10L11 9H13L14 10V12H15V17H9V12Z M11 14V16H13V14L13.5 13.5H10.5Z",
        [GameIconKind.ShieldOff] =
            "M2 4L4 2L22 20L20 22Z M5 7V14L12 22L20 14V6L12 2L7 4Z",
        [GameIconKind.ShieldPlus] =
            "M12 2L20 6V14L12 22L4 14V6Z M11 8H13V11H16V13H13V16H11V13H8V11H11Z",
        [GameIconKind.ShieldRemove] =
            "M12 2L20 6V14L12 22L4 14V6Z M8 10L10 8L12 10L14 8L16 10L14 12L16 14L14 16L12 14L10 16L8 14L10 12Z",
        [GameIconKind.ShieldStar] =
            "M12 2L20 6V14L12 22L4 14V6Z M12 6L13.5 10H17L14 12.5L15 16.5L12 14L9 16.5L10 12.5L7 10H10.5Z",
        [GameIconKind.ShieldSword] =
            "M12 2L20 6V14L12 22L4 14V6Z M11 7H13V15L14 16V17H10V16L11 15Z",
        [GameIconKind.Skull] =
            "F0 M7 3H17L20 7V13L17 16H16L15 20H13V17H11V20H9L8 16H7L4 13V7Z" +
            " M9 8V12H11V8Z M13 8V12H15V8Z M10 14H14L12 16Z",
        [GameIconKind.Fire] = "M12 1L15 7L18 6L16 12L19 11L17 16L19 18L15 17L14 21L12 23L10 21L9 17L5 18L7 16L4 11L7 12L5 6L8 7Z",
        [GameIconKind.FireFlag] =
            "M5 2H7V22H5Z M7 3H16L14 7L16 11H7Z M12 4L13 6L12 8L14 10H8V4Z",
        [GameIconKind.FlagCheckered] =
            "M5 2H7V22H5Z M7 3H19L16 8L19 13H7Z M9 5V7H11V5Z M13 5V7H15V5Z M9 9V11H11V9Z M13 9V11H15V9Z",

        // =====================================================================
        // ECONOMY
        // =====================================================================
        [GameIconKind.CashMultiple] =
            "M3 6H17V16H3Z M5 8V14H15V8Z M10 10L11 9L12 10L11 11Z" +
            " M7 4H21V14H19V6H7Z",
        [GameIconKind.Cash] =
            "F0 M3 6H21V18H3Z M5 8H19V16H5Z M10 10H14L15 12L14 14H10L9 12Z",
        [GameIconKind.CurrencyEur] =
            "M16 5L14 4H11L8 6L7 9L6 11H3V13H6L7 15L8 18L11 20H14L16 19L17 17H14L12 18L10 17L9 15L8.5 13H13V11H8.5L9 9L10 7L12 6L14 6L17 7Z",
        [GameIconKind.ScrewFlatTop] =
            "M11 2H13L14 4L13 6H14L15 8V18L14 20L12 22L10 20L9 18V8L10 6H11L10 4Z",
        [GameIconKind.DiamondOutline] =
            "F0 M12 2L22 12L12 22L2 12Z M12 6L7 12L12 18L17 12Z",
        [GameIconKind.DiamondStone] = "M7 3H17L22 10L12 22L2 10Z M7 3L5 10H19L17 3Z",
        [GameIconKind.Gift] = "M3 9H21V21H3Z M8 4L10 2L12 6L14 2L16 4L14 7H10Z",
        [GameIconKind.GiftOpen] =
            "M3 11H21V21H3Z M5 8L10 4L12 7L14 4L19 8Z M11 11H13V21H11Z",
        [GameIconKind.GiftOutline] =
            "F0 M3 9H21V21H3Z M5 11H19V19H5Z" +
            " M8 4L10 2L12 6L14 2L16 4L14 7H10Z M11.5 9H12.5V19H11.5Z",
        [GameIconKind.TreasureChest] = "M3 10H21V21H3Z M4 5H20L21 10H3Z M10 13H14V17H10Z",
        [GameIconKind.Bank] = "M12 2L22 8V10H2V8Z M4 11H6V18H4Z M11 11H13V18H11Z M18 11H20V18H18Z M2 19H22V21H2Z",
        [GameIconKind.Finance] = "M4 20V18L8 12L12 15L16 8L20 11V20Z M4 20H20V21H4Z",

        // =====================================================================
        // WORKERS & PEOPLE
        // =====================================================================
        [GameIconKind.Account] =
            "M12 4L15 5L16 8V10L14 12H10L8 10V8L9 5Z M5 20V18L7 15H17L19 18V20Z",
        [GameIconKind.AccountEdit] =
            "M12 4L14 5L15 8V10L13 12H10L8 10V8L9 5Z M5 20V18L7 15H13L11 17V20Z M15 14L20 19L18 21L13 16Z",
        [GameIconKind.AccountGroup] =
            "M8 6L10 7V9L8 10L6 9V7Z M3 16V14L5 12H11L13 14V16Z" +
            " M16 5L18 6V8L16 9L14 8V6Z M12 15V13L14 11H18L20 13V15Z",
        [GameIconKind.AccountGroupOutline] =
            "F0 M8 6L10 7V9L8 10L6 9V7Z M3 16V14L5 12H11L13 14V16Z M5 14V15H11V14L10 13H6Z" +
            " M16 5L18 6V8L16 9L14 8V6Z M12 15V13L14 11H18L20 13V15Z M14 13V14H18V13L17 12H15Z",
        [GameIconKind.AccountHardHat] =
            "M8 8H16L17 6H7Z M12 4L15 5L16 8V10L14 12H10L8 10V8L9 5Z M5 20V18L7 15H17L19 18V20Z",
        [GameIconKind.AccountMultiplePlus] =
            "M8 6L10 7V9L8 10L6 9V7Z M3 16V14L5 12H11L13 14V16Z" +
            " M18 8V11H21V13H18V16H16V13H13V11H16V8Z",
        [GameIconKind.AccountOff] =
            "M2 4L4 2L22 20L20 22Z M12 4L15 5L16 8V10L14 12H10L8 10V8L9 5Z M5 20V18L7 15H17L19 18V20Z",
        [GameIconKind.AccountPlus] =
            "M12 4L15 5L16 8V10L14 12H10L8 10V8L9 5Z M5 20V18L7 15H14V17H16V15H18V17H20V20Z" +
            " M17 8V11H20V13H17V16H15V13H12V11H15V8Z",
        [GameIconKind.AccountRemove] =
            "M12 4L15 5L16 8V10L14 12H10L8 10V8L9 5Z M5 20V18L7 15H14Z" +
            " M15 10L17 8L19 10L21 8L22 9L20 11L22 13L21 14L19 12L17 14L15 12L17 11Z",
        [GameIconKind.AccountSearch] =
            "M12 4L15 5L16 8V10L14 12H10L8 10V8L9 5Z M5 20V18L7 15H13Z" +
            " M17 11L19 11L20 12V14L19 15L17 15L16 14V12Z M19.5 15L22 17.5L21 18.5L18.5 16Z",
        [GameIconKind.AccountStar] =
            "M12 4L15 5L16 8V10L14 12H10L8 10V8L9 5Z M5 20V18L7 15H17L19 18V20Z" +
            " M12 14L13 16H15L13.5 17.5L14 19.5L12 18L10 19.5L10.5 17.5L9 16H11Z",
        [GameIconKind.AccountSwitch] =
            "M8 6L10 7V9L8 10L6 9V7Z M3 16V14L5 12H11L13 14V16Z" +
            " M16 6L18 7V9L16 10L14 9V7Z M11 16V14L13 12H19L21 14V16Z" +
            " M10 18L12 20L10 22Z M14 18L12 20L14 22Z",
        [GameIconKind.AccountTie] =
            "M12 4L15 5L16 8V10L14 12H10L8 10V8L9 5Z M5 20V18L7 15H17L19 18V20Z" +
            " M11 13L12 16L13 13L14 15L12 20L10 15Z",
        [GameIconKind.HumanMaleBoard] =
            "M12 2L14 3V5L12 6L10 5V3Z M5 20V18L8 14H16L19 18V20Z" +
            " M3 7H21V13H3Z M5 9H19V11H5Z",
        [GameIconKind.Ninja] =
            "M7 4H17L19 7V10H5V7Z M5 10H19L18 12H6Z M9 12V14L12 16L15 14V12Z" +
            " M8 8H10V10H8Z M14 8H16V10H14Z",
        [GameIconKind.Run] =
            "M14 3H16V5H14Z" +
            " M11 6L15 6L17 9L15 9L16 12L13 17L15 21L13 22L10 17L12 13L10 10L7 12L6 10L10 7Z",

        // =====================================================================
        // WORKSHOP & TOOLS
        // =====================================================================
        [GameIconKind.Hammer] = "M10 2L14 2L15 4L20 9L18 11L13 6L12 7L18 13L16 15L10 9L4 15L2 13L8 7L9 6Z",
        [GameIconKind.HammerWrench] =
            "M6 2L10 6L8 8L2 4Z M14 2L18 2L20 4L22 8L18 12L16 10L18 8L17 6L14 9L12 7Z" +
            " M8 10L14 16L12 18L4 22L2 20L10 12Z",
        [GameIconKind.Wrench] = "M16 3L18 2L22 6L21 8L17 6L12 11L15 14L11 18L8 21L6 22L2 18L3 16L6 13L9 16L14 11L10 7L12 5Z",
        [GameIconKind.Anvil] = "M4 14L6 10H10L12 8H16L18 10H20L22 14V16H2V14Z M8 16V20H16V16Z",
        [GameIconKind.Screwdriver] = "M11 2H13L14 4L13 12L15 14V16L13 18V22H11V18L9 16V14L11 12L10 4Z",
        [GameIconKind.HandSaw] = "M2 4H5L20 18L22 18L22 20L20 20L5 6H2Z M7 2L9 4L7 6L5 4Z",
        [GameIconKind.Saw] = "M4 8L6 6L8 8L10 6L12 8L14 6L16 8L18 6L20 8V12L4 12Z M4 12V18H20V12Z",
        [GameIconKind.Toolbox] = "M3 10H21V20H3Z M7 8V6H10V4H14V6H17V8Z M11 12H13V16H11Z",
        [GameIconKind.Draw] = "M3 21L5 14L16 3L21 8L10 19Z M5 14L10 19Z",
        // Stift/Bleistift (kantig, hexagonaler Schaft)
        [GameIconKind.Pencil] = "M4 20L6 14L15 5L19 9L10 18Z M15 5L17 3L21 7L19 9Z M6 14L10 18Z",
        [GameIconKind.FormatPaint] = "M4 2H18V4L20 4V10L18 10V8H10V12H8V16H6V12L4 10Z",
        [GameIconKind.SprayBottle] = "M9 2H13V4H14V8H8V4H9Z M8 8L6 10V20L8 22H14L16 20V10L14 8Z M18 10H20V12H18Z M18 14H21V16H18Z M18 18H20V20H18Z",
        [GameIconKind.MagnifyPlus] =
            "M10 2L15 4L17 8V12L15 16L10 18L5 16L3 12V8L5 4Z M9 8H11V10H13V12H11V14H9V12H7V10H9Z" +
            " M15 16L21 22L19 22L15 18Z",

        // =====================================================================
        // CONSTRUCTION & BUILDINGS
        // =====================================================================
        [GameIconKind.Factory] = "M2 21V13L6 9V13L10 9V13L14 9V4H22V21Z",
        [GameIconKind.FloorPlan] =
            "F0 M3 3H21V21H3Z M5 5H19V19H5Z M11.5 5H12.5V19H11.5Z M5 11.5V12.5H19V11.5Z",
        [GameIconKind.Wall] =
            "M2 4H22V20H2Z" +
            " M2 7.5H22V8.5H2Z M2 11.5H22V12.5H2Z M2 15.5H22V16.5H2Z" +
            " M5.5 4V8H6.5V4Z M11.5 4V8H12.5V4Z M17.5 4V8H18.5V4Z" +
            " M3.5 8.5V12H4.5V8.5Z M9.5 8.5V12H10.5V8.5Z M15.5 8.5V12H16.5V8.5Z" +
            " M7.5 12.5V16H8.5V12.5Z M13.5 12.5V16H14.5V12.5Z M19.5 12.5V16H20.5V12.5Z" +
            " M5.5 16.5V20H6.5V16.5Z M11.5 16.5V20H12.5V16.5Z M17.5 16.5V20H18.5V16.5Z",
        [GameIconKind.Pipe] = "M4 8H10V4H14V8H20V12H14V16H10V12H4Z",
        [GameIconKind.CableData] = "M5 4H7V8L10 12H14L17 8V4H19V8L15 13V20H9V13L5 8Z",
        // Einfaches Haus (fuer Roofer-Workshop-Icon)
        [GameIconKind.Home] = "M12 3L2 12H5V21H19V12H22Z M10 14H14V21H10Z",
        [GameIconKind.HomeAutomation] = "M12 3L2 12H5V21H19V12H22Z M10 14H14V21H10Z M11 9H13V11H11Z",
        [GameIconKind.HomeCity] = "M12 3L2 12H5V21H10V17H14V21H19V12H22Z M15 7H21V14H19V9H17V14Z",
        [GameIconKind.HomeEdit] = "M12 3L2 12H5V21H10V15H12L18 9L20 11L14 17V21H19V12H22Z",
        [GameIconKind.HomeGroup] = "M7 5L2 9V15H12V9Z M17 5L12 9V15H22V9Z M12 15V21H2V17H22V21H12Z",
        [GameIconKind.HomeRoof] = "M12 3L2 12H22Z M5 12V21H19V12Z",
        [GameIconKind.OfficeBuilding] =
            "M4 3H20V21H4Z M7 5H9V7H7Z M11 5H13V7H11Z M15 5H17V7H15Z" +
            " M7 9H9V11H7Z M11 9H13V11H11Z M15 9H17V11H15Z" +
            " M7 13H9V15H7Z M11 13H13V15H11Z M15 13H17V15H15Z" +
            " M10 17H14V21H10Z",
        [GameIconKind.OfficeBuildingCog] =
            "M4 3H16V12L14 14V21H4Z M7 5H9V7H7Z M11 5H13V7H11Z M7 9H9V11H7Z M11 9H13V11H11Z" +
            " M18 12L19 14L21 14.5V17.5L19 18L18 20L16 18L15 18L14 17.5V14.5L15 14Z M17 15.5L18 15L19 15.5L19 16.5L18 17L17 16.5Z",
        [GameIconKind.OfficeBuildingOutline] =
            "F0 M4 3H20V21H4Z M6 5H18V19H6Z M8 7H10V9H8Z M12 7H14V9H12Z M16 7H18V9H16Z" +
            " M8 11H10V13H8Z M12 11H14V13H12Z M16 11H18V13H16Z M10 15H14V19H10Z",
        [GameIconKind.DomainPlus] =
            "M4 3H14V10H20V21H4Z M7 5H9V7H7Z M11 5H13V7H11Z M7 9H9V11H7Z M11 9H13V11H11Z" +
            " M16 3V7H20V5L18 3Z M10 15H14V19H10Z",
        [GameIconKind.Garage] = "M3 10L12 4L21 10V20H3Z M7 13H17V20H7Z M9 15H15V20H9Z",
        [GameIconKind.DoorOpen] =
            "M4 3H18V21H4Z M6 5H16V19H6Z M8 5L14 7V17L8 19Z M11.5 11H12.5V13H11.5Z",
        [GameIconKind.Stairs] = "M2 22V18H6V14H10V10H14V6H18V2H22V22Z",
        [GameIconKind.TowerFire] =
            "M10 2H14V6L16 8V14L14 16V22H10V16L8 14V8L10 6Z M11 9L12 7L13 9L12 11Z",
        [GameIconKind.TransmissionTower] =
            "M12 2L14 6H16L18 2L20 4L17 8L19 14L21 22H17L15 14H13L11 14H9L7 22H3L5 14L7 8L4 4L6 2L8 6H10Z" +
            " M11 8H13V14H11Z",
        // Baukran (fuer Contractor-Workshop-Icon)
        [GameIconKind.Crane] =
            "M10 2H12V8H20V10H12V20H10V10H8V8H10Z M6 8L10 4Z M12 8H18L20 10Z M11 12V14L14 14L14 20H12V16L11 16Z",
        [GameIconKind.City] =
            "M2 21V14H6V10H10V6H14V10H18V14H22V21Z M4 16V19H8V16Z M10 12V19H14V12Z M16 16V19H20V16Z",
        // Lagerhaus: Breites Gebaeude mit Rolltor und Kisten
        [GameIconKind.Warehouse] =
            "M12 3L2 9V21H22V9Z M6 12H10V16H6Z M14 12H18V16H14Z M9 17H15V21H9Z",

        // =====================================================================
        // FURNITURE & INTERIOR
        // =====================================================================
        [GameIconKind.Sofa] = "M4 10V8H20V10L22 10V16H20V18H4V16H2V10Z M6 10V14H18V10Z",
        [GameIconKind.TableFurniture] = "M3 10H21V13H19V20H17V13H7V20H5V13H3Z",
        [GameIconKind.Bed] = "M3 10V8H5V6H12V8H21V10L22 12V16H2V12Z M5 10H12V12H5Z",
        [GameIconKind.BedOutline] =
            "F0 M3 10V8H5V6H12V8H21V10L22 12V16H2V12Z M5 8H10V10H5Z" +
            " M4 12H20V14H4Z",
        [GameIconKind.SeatOutline] =
            "F0 M6 4H18V14H6Z M8 6H16V12H8Z M4 14H20V18L18 20H6L4 18Z",
        [GameIconKind.Lamp] = "M9 4L12 2L15 4L17 8V12L15 14H13V18H15V20H9V18H11V14H9L7 12V8Z",
        [GameIconKind.Television] = "M4 4H20V16H4Z M6 6H18V14H6Z M8 18H16V20H8Z",
        [GameIconKind.WashingMachine] =
            "F0 M4 2H20V22H4Z M6 4H18V6H6Z M12 9L16 11V15L12 17L8 15V11Z M12 11L14 12.5V13.5L12 15L10 13.5V12.5Z",
        [GameIconKind.Stove] = "M4 2H20V22H4Z M6 4H18V10H6Z M8 12H10V14H8Z M14 12H16V14H14Z M8 16H10V18H8Z M14 16H16V18H14Z",
        [GameIconKind.ShowerHead] =
            "M14 2L16 4V8H18V10H10V8L12 6V4Z" +
            " M8 11.5H18V12.5H8Z" +
            " M9.5 14V16.5H10.5V14Z M13.5 14V16.5H14.5V14Z" +
            " M7.5 18V20.5H8.5V18Z M11.5 18V20.5H12.5V18Z M15.5 18V20.5H16.5V18Z",
        [GameIconKind.WaterPump] = "M12 4L16 4L18 6V10H14V8H12L10 10V14L12 16H18V18H10L6 14V8L8 5Z",
        [GameIconKind.SilverwareForkKnife] =
            "M7 2V10L9 12V22H7V12L5 10V2Z M6 2V6H8V2Z" +
            " M15 2L17 2L19 6L17 10V22H15V10L13 6Z",

        // =====================================================================
        // MATERIALS & CRAFTING
        // =====================================================================
        [GameIconKind.FlaskOutline] =
            "F0 M9 2H15V8L19 16V20H5V16L9 8Z M11 4H13V9L16 15V18H8V15L11 9Z",
        [GameIconKind.Creation] =
            "M12 2L14 8L20 8L15 12L17 18L12 14L7 18L9 12L4 8L10 8Z",
        [GameIconKind.Palette] =
            "M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M8 8H10V10H8Z M14 6H16V8H14Z M16 11H18V13H16Z M7 14H9V16H7Z",
        [GameIconKind.Water] = "M12 2L18 11V16L16 19L12 21L8 19L6 16V11Z",
        [GameIconKind.Snowflake] =
            "M11 2H13V22H11Z M4 7L6 6L12 12L6 18L4 17L9 12Z M20 7L18 6L12 12L18 18L20 17L15 12Z",
        [GameIconKind.Flower] =
            "M12 6L14 4L16 6L14 8Z M12 6L10 4L8 6L10 8Z" +
            " M16 10L18 8L20 10L18 12Z M8 10L6 8L4 10L6 12Z" +
            " M12 14L14 16L12 18L10 16Z M12 9L14 10.5V13.5L12 15L10 13.5V10.5Z" +
            " M11 18V22H13V18Z",
        [GameIconKind.Forest] = "M12 2L17 9H14L18 14H6L10 9H7Z M11 14H13V22H11Z",
        [GameIconKind.LightningBolt] = "M13 2L7 13H11L7 22L17 11H13Z",
        [GameIconKind.LightningBoltCircle] =
            "M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z M13 5L8 13H11L8 19L16 11H13Z",
        [GameIconKind.PackageDown] =
            "M3 7H21V20H3Z M3 7L12 3L21 7Z M11 10H13V14H15L12 17L9 14H11Z",
        [GameIconKind.PackageVariant] =
            "M3 7H21V20H3Z M3 7L12 3L21 7Z M11.5 7V20H12.5V7Z M11.5 3V7H12.5V3Z",
        [GameIconKind.Chip] =
            "F0 M6 6H18V18H6Z M8 8H16V16H8Z" +
            " M9.5 4H10.5V6H9.5Z M13.5 4H14.5V6H13.5Z M9.5 18H10.5V20H9.5Z M13.5 18H14.5V20H13.5Z" +
            " M4 9.5V10.5H6V9.5Z M4 13.5V14.5H6V13.5Z M18 9.5V10.5H20V9.5Z M18 13.5V14.5H20V13.5Z",
        [GameIconKind.Compass] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z" +
            " M12 8L16 12L12 16L8 12Z M11 11H13V13H11Z",

        // =====================================================================
        // CLIPBOARD & DOCUMENTS
        // =====================================================================
        [GameIconKind.ClipboardCheck] =
            "F0 M6 2H18V21H4V4H6Z M18 4H20V21H4V4Z M8 3H16V5H8Z" +
            " M10 15L7 12L8.5 10.5L10 12.5L15.5 8L17 9.5Z",
        [GameIconKind.ClipboardList] =
            "M6 2H18V21H4V4H6Z M18 4H20V21H4V4Z M8 3H16V5H8Z" +
            " M8 8H10V10H8Z M12 8H18V10H12Z M8 12H10V14H8Z M12 12H18V14H12Z M8 16H10V18H8Z M12 16H18V18H12Z",
        [GameIconKind.ClipboardTextMultiple] =
            "M4 4H16V19H2V6H4Z M16 6H18V21H6V19H16Z M6 8H14V10H6Z M6 12H14V14H6Z M6 16H10V18H6Z",
        [GameIconKind.ClipboardTextOutline] =
            "F0 M6 2H18V21H4V4H6Z M18 4H20V21H4V4Z M8 3H16V5H8Z" +
            " M6 6H18V19H6Z M8 8H16V10H8Z M8 12H16V14H8Z",
        [GameIconKind.FileDocumentCheck] =
            "M4 2H14L20 8V21H4Z M14 2V8H20Z M8 12L10 14L15 9L16.5 10.5L10 17L6.5 13.5Z",
        [GameIconKind.BookOpen] =
            "M2 5H11L12 6L13 5H22V19H13L12 20L11 19H2Z M11 6H12V19H11Z M12 6H13V19H12Z",
        [GameIconKind.BookOpenVariant] =
            "M2 5H11L12 6L13 5H22V19H13L12 20L11 19H2Z M11 6H12V19H11Z M12 6H13V19H12Z" +
            " M4 7.5H10V8.5H4Z M4 10.5H10V11.5H4Z M14 7.5H20V8.5H14Z M14 10.5H20V11.5H14Z",
        [GameIconKind.FormatListNumbered] =
            "M4 4H6V6L5 7H4V6Z M8 5H20V7H8Z M4 10H6V11L5 12H4V11Z M8 10H20V12H8Z M4 16H6V17L5 18H4V17Z M8 16H20V18H8Z",

        // =====================================================================
        // TIME & CALENDAR
        // =====================================================================
        [GameIconKind.Clock] = "M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z M11 6H13V12H17V14H11Z",
        [GameIconKind.ClockOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z M11 7H13V12H16V14H11Z",
        [GameIconKind.ClockPlus] =
            "M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z M11 6H13V12H17V14H11Z" +
            " M18 15V18H21V20H18V23H16V20H13V18H16V15Z",
        [GameIconKind.ProgressClock] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z" +
            " M11 7H13V11H8V13H11V7Z",
        [GameIconKind.TimerOutline] =
            "F0 M10 1H14V3H10Z M12 4L18 7L21 12L18 18L12 22L6 18L3 12L6 7Z" +
            " M12 7L16 9L18 12L16 16L12 18L8 16L6 12L8 9Z M11 8H13V13H16V15H11Z",
        [GameIconKind.TimerRefresh] =
            "M10 1H14V3H10Z M12 4L18 7L21 12L18 18L12 22L6 18L3 12L6 7Z" +
            " M12 8L16 10L14 12H16L14 15L10 13L12 11H10Z",
        [GameIconKind.CalendarCheck] =
            "F0 M3 3H21V21H3Z M5 8H19V19H5Z" +
            " M10 16L7 13L8.5 11.5L10 13.5L15.5 9.5L17 11Z",
        [GameIconKind.CalendarStar] =
            "F0 M3 3H21V21H3Z M5 8H19V19H5Z" +
            " M12 9L13 12H16L13.5 14L14.5 17L12 15L9.5 17L10.5 14L8 12H11Z",
        [GameIconKind.History] =
            "M12 4L18 7L20 11H18L17 9L12 6L8 8L6 12L8 16L12 18L16 16L18 13H20L18 17L12 20L6 18L4 14V10L6 7Z" +
            " M11 7H13V12H16V14H11Z M4 8L2 10L4 12Z",

        // =====================================================================
        // SETTINGS
        // =====================================================================
        [GameIconKind.Cog] =
            "F0 M10 1H14L15 4L17 5L20 3L22 6L20 9L21 12L20 15L22 18L20 21L17 19L15 20L14 23H10L9 20L7 19L4 21L2 18L4 15L3 12L4 9L2 6L4 3L7 5L9 4Z" +
            " M12 8L15 10L16 12L15 14L12 16L9 14L8 12L9 10Z",
        [GameIconKind.CogOutline] =
            "F0 M10 1H14L15 4L17 5L20 3L22 6L20 9L21 12L20 15L22 18L20 21L17 19L15 20L14 23H10L9 20L7 19L4 21L2 18L4 15L3 12L4 9L2 6L4 3L7 5L9 4Z" +
            " M12 6L16 8L18 12L16 16L12 18L8 16L6 12L8 8Z",
        [GameIconKind.CogSync] =
            "F0 M10 1H14L15 4L17 5L20 3L22 6L20 9L21 12L20 15L22 18L20 21L17 19L15 20L14 23H10L9 20L7 19L4 21L2 18L4 15L3 12L4 9L2 6L4 3L7 5L9 4Z" +
            " M12 8L15 10L14 12H16L14 15L10 13L12 11H10L12 8Z",
        [GameIconKind.VolumeHigh] = "M3 9V15H7L13 20V4L7 9Z M15 8L17 10V14L15 16Z M17 5L20 8V16L17 19Z",
        [GameIconKind.Vibrate] = "M8 4H16V20H8Z M5 7V17H7V7Z M17 7V17H19V7Z M2 9V15H4V9Z M20 9V15H22V9Z",
        [GameIconKind.Bell] = "M12 2L14 4L16 8V14L18 16V18H6V16L8 14V8L10 4Z M10 19H14L13 21H11Z",
        [GameIconKind.Translate] = "M2 3H12V5H8L6 9L8 13H6L5 11H3L2 13V3Z M12 11H22V21H12Z M15 13L17 19L19 13Z",
        [GameIconKind.GamepadVariant] = "M3 9H21L23 12V17L20 20H16L13 18H11L8 20H4L1 17V12Z M7 12V14H9V12Z M15 12H17V14H15Z",
        [GameIconKind.ControllerClassic] = "M6 8H18L20 12V16L18 18H6L4 16V12Z M8 10V12H10V10Z M14 11H16V13H14Z M11 14H13V16H11Z",

        // =====================================================================
        // CLOUD & SYNC
        // =====================================================================
        [GameIconKind.CloudDownload] =
            "M6 18L4 16V13L6 11L8 8H12L15 6L18 8L20 8L22 10V14L20 16Z" +
            " M11.5 9H12.5V14H11.5Z M9 13L12 16L15 13L14 12L12.5 14V9H11.5V14L10 12Z",
        [GameIconKind.CloudSync] =
            "M6 18L4 16V13L6 11L8 8H12L15 6L18 8L20 8L22 10V14L20 16Z" +
            " M9 13L13 10V12H15V10L17 13H15V14H13V16L9 13Z",
        [GameIconKind.CloudUpload] =
            "M6 18L4 16V13L6 11L8 8H12L15 6L18 8L20 8L22 10V14L20 16Z" +
            " M11.5 16H12.5V11H11.5Z M9 12L12 9L15 12L14 13L12.5 11V16H11.5V11L10 13Z",
        [GameIconKind.WifiOff] =
            "M2 4L4 2L22 20L20 22Z M12 18L14 16L12 14L10 16Z" +
            " M4 10L6 8L8 10Z M16 8L18 10L20 8Z M8 14L10 12L12 14Z",

        // =====================================================================
        // COMMUNICATION
        // =====================================================================
        [GameIconKind.EmailPlus] = "M3 5H21V19H3Z M3 5L12 12L21 5Z M16 12V15H19V17H16V20H14V17H11V15H14V12Z",
        [GameIconKind.EmailPlusOutline] =
            "F0 M3 5H21V19H3Z M5 7L12 12L19 7V17H5Z M16 12V15H19V17H16V20H14V17H11V15H14V12Z",
        [GameIconKind.ShareVariant] =
            "M18 4H20V8H18Z M18 16H20V20H18Z M4 10H6V14H4Z" +
            " M18 5.5L6 11.5V12.5L18 6.5Z M6 12.5L18 17.5V18.5L6 13.5Z",
        [GameIconKind.KeyVariant] =
            "M7 10L5 12V16L7 18L11 18L13 16L15 16L15 14L17 14V12H13L11 10Z" +
            " M8 13V15H10V13Z",
        [GameIconKind.KeyboardReturn] = "M18 4V14H8L11 11L9 9L4 14L9 19L11 17L8 14Z",

        // =====================================================================
        // INFO & HELP
        // =====================================================================
        [GameIconKind.HelpCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M10 8L11 7H14L15 9L13 11L12 12V14H11V12L13 10L14 9L13 8H11L10 9Z M11 16H13V18H11Z",
        [GameIconKind.InformationOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z" +
            " M11 7H13V9H11Z M11 11H13V17H11Z",
        [GameIconKind.AlertCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M11 7H13V14H11Z M11 16H13V18H11Z",
        [GameIconKind.AlertCircleOutline] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 5L17 7L19 12L17 17L12 19L7 17L5 12L7 7Z" +
            " M11 7H13V13H11Z M11 15H13V17H11Z",
        [GameIconKind.LightbulbOn] = "M9 4L12 2L15 4L17 7V12L15 15V18H9V15L7 12V7Z M9 19H15V21H9Z",
        [GameIconKind.LightbulbOnOutline] =
            "F0 M9 4L12 2L15 4L17 7V12L15 15V18H9V15L7 12V7Z M11 5L13 4L15 6V11L13 14V16H11V14L9 11V6Z" +
            " M9 19H15V21H9Z",

        // =====================================================================
        // CHARTS & DATA
        // =====================================================================
        [GameIconKind.TrendingUp] = "M4 16L10 10L14 14L20 6L20 10L22 8L22 4H18L20 6L14 12L10 8L2 18L4 18Z",
        [GameIconKind.TrendingDown] = "M4 8L10 14L14 10L20 18L20 14L22 16L22 20H18L20 18L14 12L10 16L2 6L4 6Z",
        [GameIconKind.ViewGrid] = "M3 3H11V11H3Z M13 3H21V11H13Z M3 13H11V21H3Z M13 13H21V21H13Z",
        [GameIconKind.ChartDonut] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M12 7L16 8.5L17.5 12L16 15.5L12 17L8 15.5L6.5 12L8 8.5Z",
        [GameIconKind.ChartBar] =
            "M4 20V10H8V20H4Z M10 20V4H14V20H10Z M16 20V12H20V20H16Z",
        [GameIconKind.Check] =
            "M9 16L5 12L3 14L9 20L21 8L19 6Z",

        // =====================================================================
        // EMOTIONS & STATUS
        // =====================================================================
        [GameIconKind.EmoticonHappy] =
            "M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z" +
            " M9 9H11V11H9Z M13 9H15V11H13Z M8 14L10 16L12 16L14 16L16 14Z",
        [GameIconKind.Heart] = "M12 21L3 12V8L5 5L8 4L12 7L16 4L19 5L21 8V12Z",
        [GameIconKind.Sleep] = "M14 4H20L14 10H20Z M10 10H16L10 16H16Z M6 16H12L6 22H12Z",
        [GameIconKind.Dumbbell] = "M4 9H6V8H8V16H6V15H4V9Z M20 9H18V8H16V16H18V15H20V9Z M8 11H16V13H8Z",
        [GameIconKind.ShoeFormal] = "M4 14L6 10H10L14 6L18 6L20 8V12L22 14V16H4Z",
        [GameIconKind.BagPersonal] = "M8 4H16L18 8V20H6V8Z M10 2H14V4H10Z M10 10H14V14H10Z",

        // =====================================================================
        // ACTIONS
        // =====================================================================
        [GameIconKind.HandCoin] =
            "M14 2L17 5V9L14 11H10V13L12 15H18L20 17V20L18 22H8L4 18V12L6 10H10L14 7V5L12 4Z" +
            " M15 5L16 4L18 5L17 6Z",
        [GameIconKind.HandWave] =
            "M10 4L12 2L14 4V10L16 8L18 9V14L16 18L12 21L8 21L5 18V12L7 10V6L9 5Z",
        [GameIconKind.Handshake] =
            "M2 8H6L10 12L14 8H18L22 12V16L18 18H14L12 16L10 18H6L2 14Z",
        [GameIconKind.CartArrowDown] =
            "M2 3H6L8 13H18L21 5H9Z M8 17H11V20H8Z M17 17H20V20H17Z" +
            " M14 6V10L12 8.5L14 6L16 8.5L14 10Z",
        // LKW: Fuehrerkabine rechts, Ladeflaeche links, Raeder
        [GameIconKind.Truck] =
            "M2 7H15V17H2Z M15 10H19L21 13V17H15Z M5 16L7 16L7 19L5 19Z M17 16L19 16L19 19L17 19Z M3 9H13V12H3Z",
        [GameIconKind.TruckDelivery] =
            "M2 6H16V16H2Z M16 10H20L22 14V16H16Z M6 16L8 16L8 18L6 18Z M18 16L20 16L20 18L18 18Z",
        [GameIconKind.ExitToApp] = "M16 8L20 12L16 16V13H10V11H16Z M4 4H14V6H6V18H14V20H4Z",
        [GameIconKind.PlayCircle] =
            "F0 M12 2L19 5L22 12L19 19L12 22L5 19L2 12L5 5Z M10 7L18 12L10 17Z",
        [GameIconKind.Video] = "M3 6V18H16V14L22 18V6L16 10V6Z",
        [GameIconKind.Flash] = "M14 2L8 11H12L5 22L19 13H15L18 4Z",
        [GameIconKind.FlashOutline] =
            "F0 M14 2L8 11H12L5 22L19 13H15L18 4Z M13.5 5L10 11H13L8 18L17 13H14L16 6Z",
        [GameIconKind.RocketLaunch] =
            "M12 2L16 6L18 12L16 16L12 18L8 16L6 12L8 6Z" +
            " M12 6L14 8V12L12 14L10 12V8Z" +
            " M6 16L4 20L8 18Z M18 16L20 20L16 18Z",
        [GameIconKind.SwapHorizontal] =
            "M8 4L2 9L8 14V11H14V7H8Z M16 10L22 15L16 20V17H10V13H16Z",

        // =====================================================================
        // GEBAEUDE-TYPEN (Prestige/Ascension)
        // =====================================================================
        [GameIconKind.SchoolOutline] =
            "F0 M12 3L22 9L12 15L2 9Z M6 11V18L12 21L18 18V11L12 14Z" +
            " M8 12V17L12 19L16 17V12Z",
        [GameIconKind.School] = "M12 3L22 9L12 15L2 9Z M6 11V18L12 21L18 18V11L12 14Z",
        // Absolventenhut: Flaches Brett oben, Quaste rechts
        [GameIconKind.GraduationCap] =
            "M12 4L2 9L12 14L22 9Z M6 11V17L12 20L18 17V11Z M20 9V16H21V9Z",
        [GameIconKind.HardHat] = "M4 14V12L6 8L10 6H14L18 8L20 12V14Z M6 15H18V18H6Z",
        [GameIconKind.StorefrontOutline] =
            "F0 M4 3H20L22 8L20 10L18 8L16 10L14 8L12 10L10 8L8 10L6 8L4 10L2 8Z" +
            " M4 10V21H20V10Z M6 12H18V19H6Z M10 14H14V19H10Z",
        [GameIconKind.WeatherSunset] =
            "M12 4L14 6L12 8L10 6Z M4 11.5H8V12.5H4Z M16 11.5H20V12.5H16Z M5.5 6.5L8 9L7 9.5L5 7Z M18.5 6.5L16 9L17 9.5L19 7Z" +
            " M6 16H18L16 14L12 12L8 14Z M4 18H20V20H4Z",
        [GameIconKind.WhiteBalanceSunny] =
            "M12 7L15 8L17 11V13L15 16L12 17L9 16L7 13V11L9 8Z" +
            " M11 2H13V5H11Z M11 19H13V22H11Z M2 11V13H5V11Z M19 11V13H22V11Z" +
            " M5 4L7 4L8 6L6 7Z M17 4L19 4L18 7L16 6Z M5 20L6 17L8 18L7 20Z M18 17L19 20L17 20L16 18Z",
        [GameIconKind.RobotHappy] =
            "M11 2H13V4H17V6H7V4H11Z M5 6H19V16H5Z M7 8H17V14H7Z" +
            " M9 9H11V11H9Z M13 9H15V11H13Z M9 12H15L14 13H10Z" +
            " M7 16L5 20H9L7 16Z M17 16L19 20H15L17 16Z",
        [GameIconKind.RobotOutline] =
            "F0 M11 2H13V4H17V6H7V4H11Z M5 6H19V16H5Z M7 8H17V14H7Z" +
            " M9 9H11V11H9Z M13 9H15V11H13Z" +
            " M7 16L5 20H9L7 16Z M17 16L19 20H15L17 16Z",
        [GameIconKind.Laptop] = "M4 5H20V16H4Z M2 17H22V19H2Z M6 7H18V14H6Z",

        // =====================================================================
        // GUILD BOSS ICONS
        // =====================================================================
        // Felsiger Golem: Stumpfe Augen + zerklüfteter Rissmund
        [GameIconKind.RockGolem] =
            "M8 4H16L19 8V14L16 18H8L5 14V8Z M9 9H11V11H9Z M13 9H15V11H13Z M9 14L10 15.5L11 14L12 16L13 14L14 15.5L15 14Z",
        // Eisdrache: Diamant-Kristallaugen + Eiszapfen-Reißzähne
        [GameIconKind.IceDragon] =
            "M12 2L16 6L20 8V14L16 18L12 22L8 18L4 14V8L8 6Z" +
            " M9 9L10 11L9 13L8 11Z M15 9L16 11L15 13L14 11Z M10 15L11 17L12 15L13 17L14 15Z" +
            " M4 6L2 4Z M20 6L22 4Z",
        // Feuerdämon: Schräge Flammenaugen + breites dämonisches Grinsen
        [GameIconKind.FireDemon] =
            "M12 2L16 6L18 10V16L14 20L12 22L10 20L6 16V10L8 6Z" +
            " M9 11L11 9V12L9 14Z M15 9L13 11V14L15 12Z M9 15L11 14L12 16L13 14L15 15L12 17Z" +
            " M6 4L8 6Z M18 4L16 6Z",
        // Schattenherr: Schmale bedrohliche Schlitzaugen + Schattenranken-Mund
        [GameIconKind.ShadowLord] =
            "M7 3H17L20 7V15L17 19H14L12 22L10 19H7L4 15V7Z" +
            " M9 9H11V10.5H9Z M13 9H15V10.5H13Z M8 13L10 14L12 13L14 14L16 13L14 16L12 17L10 16Z",
        // Mechanischer Titan: Quadratische Industrieaugen + Doppel-Kieferleisten
        [GameIconKind.MechanicalTitan] =
            "M6 4H18L20 6V18L18 20H6L4 18V6Z" +
            " M8 7H10V10H8Z M14 7H16V10H14Z" +
            " M8 11.5H16V12.5H8Z M10 13.5H14V14.5H10Z" +
            " M8 15.5V18H10V15.5Z M14 15.5V18H16V15.5Z",
        // Alter Vorarbeiter: Weise zusammengekniffene Augen + langer Bart
        [GameIconKind.AncientForeman] =
            "M8 2H16L18 4V6H6V4Z M7 6H17L19 10V16L17 20H7L5 16V10Z" +
            " M9 11H11V12.5H9Z M13 11H15V12.5H13Z M10 14L11 15.5L12 14L13 15.5L14 14L12 18Z",
    };
}
