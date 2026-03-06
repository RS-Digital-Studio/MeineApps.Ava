namespace BomberBlast.Icons;

/// <summary>
/// Alle verfuegbaren Game-Icons fuer BomberBlast (Neon Arcade Stil).
/// Ersetzt Material.Icons.MaterialIconKind mit eigenen geometrischen Designs.
/// </summary>
public enum GameIconKind
{
    None,

    // Navigation
    ArrowLeft,
    ArrowUpBold,
    ArrowUpBoldCircle,
    ChevronRight,
    ChevronDoubleUp,
    Close,

    // Status
    Check,
    CheckCircle,
    Lock,
    LockOpen,
    LockOutline,

    // Stars & Rewards
    Star,
    StarOutline,
    StarCircleOutline,
    Crown,
    Trophy,
    TrophyBroken,
    TrophyOutline,
    Medal,

    // Combat & Game
    Sword,
    Shield,
    Skull,
    Fire,
    Bomb,
    Ghost,

    // Economy
    CircleMultiple,
    CircleOutline,
    CurrencyUsd,
    Diamond,
    DiamondStone,
    Gift,
    TreasureChest,

    // Cards & Dungeon
    CardsPlaying,
    CardsPlayingOutline,
    CartPlus,
    Stairs,
    Dice,
    LinkVariant,

    // Time
    Clock,
    ClockFast,
    ClockOutline,
    Timer,
    CalendarCheck,
    CalendarToday,
    CalendarWeek,

    // Settings
    Cog,
    CogOutline,
    Gamepad,
    GamepadVariant,
    VolumeHigh,
    Translate,
    AdsOff,

    // Cloud
    Cloud,
    CloudCheck,
    CloudDownload,
    CloudOffOutline,
    CloudSync,
    CloudUpload,

    // Charts
    ChartLine,
    ChartDonut,

    // Nature & World
    PineTree,
    Waves,
    Terrain,
    WeatherSunny,
    Snowflake,
    Water,
    Seed,
    Clover,

    // Buildings
    Factory,
    Pillar,
    Store,
    Home,

    // People & Profile
    AccountCircle,
    Heart,
    HeartPlus,
    HeartPulse,
    ContentSave,

    // Info & Help
    HelpCircle,
    InformationOutline,
    AlertCircle,
    LightbulbOn,

    // Media
    Video,
    Play,
    SkipNext,
    Repeat,
    Refresh,

    // Misc
    Shuffle,
    Speedometer,
    MapMarker,
    Target,
    Palette,
    PaletteOutline,
    ViewGrid,
    SchoolOutline,
    BookOpen,
    GooglePlay,
    Flash,
    FlashOutline,

    // PowerUp & Mechanic Icons (ShopViewModel)
    Account,
    FlashAlert,
    ArrowRightCircle,
    ShieldOutline,
    HelpCircleOutline,
    Shoe,
    DotsHorizontal,
    StarCircle,
    SkullOutline,
    ArrowRightBold,
    SwapHorizontal,

    // Skin-Kategorie Icons
    Trail,
    Celebration,
    CardFrame,

    // Collection/Enemy Icons (String-basiert via Converter)
    EmoticonOutline,
    EmoticonAngryOutline,
    EmoticonCoolOutline,
    EmoticonDevilOutline,
    Jellyfish,
    Run,
    GhostOutline,
    ContentCut,
    CubeOutline,

    // Collection/Boss Icons
    Mountain,
    WeatherNight,

    // Collection/PowerUp Icons
    FlashOn,
    WallOutline,
    RadioButtonChecked,
    ArrowRightBoldCircleOutline,
    ShieldFireOutline,
    ShoePrint,
    StarFourPoints,

    // Collection/Cosmetic Icons
    Flare,
    Shimmer,
    ImageFilterFrames,

    // Achievement Icons
    StarShooting,
    SwordCross,
    TimerSand,
    Flag,
    CalendarStar,
    Lightning,
    BookOpenVariant,
    ShieldStar,
    ShieldCrown,
    Cards,
    Dice5,

    // BattlePass/Dungeon Icons
    Tshirt,
    Tortoise,
    Magnet,
    ShieldFire,
    Reload,
    TimerOutline,

    // Alias (PartyPopper → String-Kompatibilitaet mit Services)
    PartyPopper,
    ShoeSneaker,

    // Bomben-Karten Icons (DeckViewModel)
    WeatherFog,
    FlipHorizontal,
    Tornado,
    CircleSlice8,
}
