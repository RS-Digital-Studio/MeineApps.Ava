namespace WorkTimePro.Models;

/// <summary>
/// Typ eines Zeiteintrags (Check-In oder Check-Out)
/// </summary>
public enum EntryType
{
    CheckIn = 0,
    CheckOut = 1
}

/// <summary>
/// Status eines Arbeitstages
/// </summary>
public enum DayStatus
{
    /// <summary>Normaler Arbeitstag</summary>
    WorkDay = 0,

    /// <summary>Wochenende (kein Arbeitstag)</summary>
    Weekend = 1,

    /// <summary>Urlaub</summary>
    Vacation = 2,

    /// <summary>Feiertag</summary>
    Holiday = 3,

    /// <summary>Krankheit</summary>
    Sick = 4,

    /// <summary>Unbezahlter Urlaub</summary>
    UnpaidLeave = 5,

    /// <summary>Homeoffice</summary>
    HomeOffice = 6,

    /// <summary>Dienstreise</summary>
    BusinessTrip = 7,

    /// <summary>Überstundenabbau</summary>
    OvertimeCompensation = 8,

    /// <summary>Sonderurlaub (Hochzeit, Umzug, etc.)</summary>
    SpecialLeave = 9,

    /// <summary>Schulung / Fortbildung</summary>
    Training = 10,

    /// <summary>Zeitausgleich / Gleittag</summary>
    CompensatoryTime = 11
}

/// <summary>
/// Typ einer Pause
/// </summary>
public enum PauseType
{
    /// <summary>Manuell erfasste Pause</summary>
    Manual = 0,

    /// <summary>Automatisch ergänzte Pause (gesetzlich)</summary>
    Auto = 1
}

/// <summary>
/// Bundesland für Feiertags-Kalender
/// </summary>
public enum GermanState
{
    /// <summary>Baden-Württemberg</summary>
    BW,
    /// <summary>Bayern</summary>
    BY,
    /// <summary>Berlin</summary>
    BE,
    /// <summary>Brandenburg</summary>
    BB,
    /// <summary>Bremen</summary>
    HB,
    /// <summary>Hamburg</summary>
    HH,
    /// <summary>Hessen</summary>
    HE,
    /// <summary>Mecklenburg-Vorpommern</summary>
    MV,
    /// <summary>Niedersachsen</summary>
    NI,
    /// <summary>Nordrhein-Westfalen</summary>
    NW,
    /// <summary>Rheinland-Pfalz</summary>
    RP,
    /// <summary>Saarland</summary>
    SL,
    /// <summary>Sachsen</summary>
    SN,
    /// <summary>Sachsen-Anhalt</summary>
    ST,
    /// <summary>Schleswig-Holstein</summary>
    SH,
    /// <summary>Thüringen</summary>
    TH
}

/// <summary>
/// Schichttyp
/// </summary>
public enum ShiftType
{
    /// <summary>Frühschicht (z.B. 6:00-14:00)</summary>
    Early = 0,

    /// <summary>Spätschicht (z.B. 14:00-22:00)</summary>
    Late = 1,

    /// <summary>Nachtschicht (z.B. 22:00-6:00)</summary>
    Night = 2,

    /// <summary>Normalschicht (z.B. 9:00-17:00)</summary>
    Normal = 3,

    /// <summary>Gleitzeit</summary>
    Flexible = 4,

    /// <summary>Frei</summary>
    Off = 5
}

/// <summary>
/// Export-Format
/// </summary>
public enum ExportFormat
{
    PDF = 0,
    CSV = 1,
    Excel = 2
}

/// <summary>
/// Cloud-Provider für Backup
/// </summary>
public enum CloudProvider
{
    None = 0,
    GoogleDrive = 1,
    OneDrive = 2
}

/// <summary>
/// Zeitraum für Statistiken
/// </summary>
public enum StatisticsPeriod
{
    Week = 0,
    Month = 1,
    Quarter = 2,
    Year = 3,
    Custom = 4
}

/// <summary>
/// Tracking-Status (aktuelle Aktivität)
/// </summary>
public enum TrackingStatus
{
    /// <summary>Nicht gestartet / ausgecheckt</summary>
    Idle = 0,

    /// <summary>Arbeitet (eingecheckt)</summary>
    Working = 1,

    /// <summary>In Pause</summary>
    OnBreak = 2
}
