namespace WorkTimePro;

/// <summary>
/// Zentrale Farbkonstanten für ViewModels (eliminiert Magic Strings).
/// Für Theme-Awareness wären DynamicResource im XAML nötig, aber VMs haben keinen XAML-Zugriff.
/// </summary>
public static class AppColors
{
    // === Status-Farben ===
    public const string StatusIdle = "#9E9E9E";
    public const string StatusActive = "#4CAF50";
    public const string StatusPaused = "#FF9800";

    // === Balance-Farben ===
    public const string BalancePositive = "#4CAF50";
    public const string BalanceNegative = "#F44336";

    // === Primary ===
    public const string Primary = "#1565C0";
    public const string White = "#FFFFFF";

    // === Kalender-Heatmap (Arbeitszeit-Intensität) ===
    public const string HeatmapLight = "#C8E6C9";      // < 4h
    public const string HeatmapMedium = "#81C784";      // 4-6h
    public const string HeatmapNormal = "#4CAF50";      // 6-8h
    public const string HeatmapHigh = "#388E3C";        // 8-10h
    public const string HeatmapOvertime = "#F44336";    // > 10h Überstunden

    // === Kalender Dark-Theme Hintergrund ===
    public const string CalendarDarkInactive = "#1E1E1E";
    public const string CalendarDarkEmpty = "#2A2A2A";
    public const string CalendarLightInactive = "#F5F5F5";
    public const string CalendarLightEmpty = "#EEEEEE";

    // === Kalender-Text ===
    public const string CalendarTextDarkInactive = "#555555";
    public const string CalendarTextLightInactive = "#BDBDBD";
    public const string CalendarTextDarkWeekend = "#757575";
    public const string CalendarTextLightWeekend = "#9E9E9E";
    public const string CalendarTextDark = "#E0E0E0";
    public const string CalendarTextLight = "#212121";

    // === Premium-Status ===
    public const string PremiumActive = "#4CAF50";
    public const string PremiumTrial = "#FF9800";
    public const string PremiumExpired = "#F44336";
    public const string PremiumFree = "#9E9E9E";

    // === Chart-Farben (Statistik) ===
    public static readonly string[] ChartColors =
    {
        "#1565C0", "#2E7D32", "#F57C00", "#C62828", "#6A1B9A",
        "#00838F", "#4527A0", "#AD1457", "#00695C", "#EF6C00"
    };
}
