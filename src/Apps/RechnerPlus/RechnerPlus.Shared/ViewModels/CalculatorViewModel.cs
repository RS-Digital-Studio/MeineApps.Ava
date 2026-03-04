using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.CalcLib;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace RechnerPlus.ViewModels;

/// <summary>
/// Taschenrechner-ViewModel: Felder, Constructor, Properties, Events, Lifecycle.
/// Partial-Dateien: Calculations, Display, History.
/// </summary>
public sealed partial class CalculatorViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private bool _isLoading;
    private readonly CalculatorEngine _engine;
    private readonly ExpressionParser _parser;
    private readonly ILocalizationService _localization;
    private readonly IHistoryService _historyService;
    private readonly IPreferencesService _preferences;
    private readonly IHapticService _haptic;

    private const string HistoryKey = "calculator_history";
    private const string MemoryKey = "calculator_memory";
    private const string MemoryHasKey = "calculator_has_memory";
    private const string ModeKey = "calculator_mode";
    private const string NumberFormatKey = "calculator_number_format";
    private const int MaxExpressionLength = 200;

    // Zahlenformat: 0 = US (1,234.56), 1 = EU (1.234,56)
    private int _numberFormat;
    private char _decimalSep = '.';
    private char _thousandSep = ',';

    // Gecachte Dezimalstellen-Einstellung (-1 = Auto, 0-10 = fest)
    private int _cachedDecimalPlaces;

    // Für wiederholtes "=" (letzte Operation wiederholen, wie Windows-Rechner)
    private string? _lastOperator;
    private string? _lastOperand;

    // Letztes Ergebnis für ANS-Taste
    private double _lastResult;

    // Undo/Redo: LinkedList statt Stack (O(1) RemoveFirst beim Overflow, kein Array-Umkopieren)
    private readonly LinkedList<CalculatorState> _undoList = new();
    private readonly Stack<CalculatorState> _redoStack = new();
    private const int MaxUndoStates = 50;

    [ObservableProperty]
    private string _display = "0";

    [ObservableProperty]
    private string _expression = "";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBasicMode))]
    [NotifyPropertyChangedFor(nameof(IsScientificMode))]
    private CalculatorMode _currentMode = CalculatorMode.Basic;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AngleModeText))]
    private bool _isRadians = true;

    [ObservableProperty]
    private bool _isHistoryVisible;

    /// <summary>INV-Modus: sin→asin, cos→acos, tan→atan, log→10^x, ln→e^x</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinButtonText))]
    [NotifyPropertyChangedFor(nameof(CosButtonText))]
    [NotifyPropertyChangedFor(nameof(TanButtonText))]
    [NotifyPropertyChangedFor(nameof(LogButtonText))]
    [NotifyPropertyChangedFor(nameof(LnButtonText))]
    private bool _isInverseMode;

    /// <summary>Aktuell aktiver Operator für Highlight (÷, ×, −, +, ^).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDivideActive))]
    [NotifyPropertyChangedFor(nameof(IsMultiplyActive))]
    [NotifyPropertyChangedFor(nameof(IsSubtractActive))]
    [NotifyPropertyChangedFor(nameof(IsAddActive))]
    private string? _activeOperator;

    /// <summary>Live-Preview-Ergebnis (grau unter dem Display).</summary>
    [ObservableProperty]
    private string _previewResult = "";

    /// <summary>Responsive Schriftgröße für lange Zahlen im Display.</summary>
    [ObservableProperty]
    private double _displayFontSize = 52;

    [ObservableProperty]
    private double _memory;

    [ObservableProperty]
    private bool _hasMemory;

    [ObservableProperty]
    private bool _showClearHistoryConfirm;

    private bool _isNewCalculation = true;

    // Funktionsgraph-Daten
    private string? _activeFunctionName;
    private Func<float, float>? _activeFunction;
    private float? _functionGraphCurrentX;

    #region Computed Properties

    public string AngleModeText => IsRadians ? "RAD" : "DEG";
    public bool IsBasicMode => CurrentMode == CalculatorMode.Basic;
    public bool IsScientificMode => CurrentMode == CalculatorMode.Scientific;

    // INV-abhängige Button-Texte
    public string SinButtonText => IsInverseMode ? "sin\u207B\u00B9" : "sin";
    public string CosButtonText => IsInverseMode ? "cos\u207B\u00B9" : "cos";
    public string TanButtonText => IsInverseMode ? "tan\u207B\u00B9" : "tan";
    public string LogButtonText => IsInverseMode ? "10\u02E3" : "log";
    public string LnButtonText => IsInverseMode ? "e\u02E3" : "ln";

    // Operator-Highlight Properties
    public bool IsDivideActive => ActiveOperator == "\u00F7";
    public bool IsMultiplyActive => ActiveOperator == "\u00D7";
    public bool IsSubtractActive => ActiveOperator == "\u2212";
    public bool IsAddActive => ActiveOperator == "+";

    // Lokalisierte Strings für View-Bindings
    public string ModeBasicText => _localization.GetString("ModeBasic");
    public string ModeScientificText => _localization.GetString("ModeScientific");

    // History lokalisierte Strings
    public string HistoryTitleText => _localization.GetString("HistoryTitle");
    public string ClearHistoryText => _localization.GetString("ClearHistory");
    public string NoCalculationsYetText => _localization.GetString("NoCalculationsYet");

    public bool HasHistory => _historyService.History.Count > 0;
    public IReadOnlyList<CalculationHistoryEntry> HistoryEntries => _historyService.History;

    /// <summary>Formatierter Memory-Wert für ToolTip-Anzeige.</summary>
    public string MemoryDisplay => HasMemory ? FormatResult(Memory) : "";

    /// <summary>Text für den Dezimal-Button (abhängig vom Zahlenformat).</summary>
    public string DecimalButtonText => _decimalSep.ToString();

    // Lokalisierte Gruppen-Header
    public string HistoryTodayText => _localization.GetString("HistoryToday") ?? "Heute";
    public string HistoryYesterdayText => _localization.GetString("HistoryYesterday") ?? "Gestern";
    public string HistoryOlderText => _localization.GetString("HistoryOlder") ?? "Älter";

    #endregion

    #region History-Caches

    // Gecachte History-Listen (werden bei OnHistoryChanged / OnLanguageChanged neu berechnet)
    private IReadOnlyList<CalculationHistoryEntry> _cachedRecentHistory = [];
    private List<HistoryGroup> _cachedGroupedHistory = [];

    /// <summary>Die letzten 2 Berechnungen für Mini-History unter dem Display.</summary>
    public IReadOnlyList<CalculationHistoryEntry> RecentHistory => _cachedRecentHistory;

    /// <summary>Gruppierte History nach Datum (Heute/Gestern/Älter).</summary>
    public List<HistoryGroup> GroupedHistory => _cachedGroupedHistory;

    private List<HistoryGroup> BuildGroupedHistory()
    {
        var groups = new List<HistoryGroup>();
        if (!HasHistory) return groups;

        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);

        // Timestamp ist UTC → für Tages-Vergleich in Lokalzeit konvertieren
        var todayEntries = _historyService.History.Where(e => e.Timestamp.ToLocalTime().Date == today).ToList();
        var yesterdayEntries = _historyService.History.Where(e => e.Timestamp.ToLocalTime().Date == yesterday).ToList();
        var olderEntries = _historyService.History.Where(e => e.Timestamp.ToLocalTime().Date < yesterday).ToList();

        if (todayEntries.Count > 0)
            groups.Add(new HistoryGroup(HistoryTodayText, todayEntries));
        if (yesterdayEntries.Count > 0)
            groups.Add(new HistoryGroup(HistoryYesterdayText, yesterdayEntries));
        if (olderEntries.Count > 0)
            groups.Add(new HistoryGroup(HistoryOlderText, olderEntries));

        return groups;
    }

    /// <summary>Baut die gecachten History-Listen neu auf.</summary>
    private void RebuildHistoryCaches()
    {
        _cachedRecentHistory = _historyService.History.Take(2).ToList();
        _cachedGroupedHistory = BuildGroupedHistory();
    }

    #endregion

    #region Events

    /// <summary>Event für Floating-Text-Anzeige (Text, Kategorie).</summary>
    public event Action<string, string>? FloatingTextRequested;

    /// <summary>Event zum Kopieren in die Zwischenablage (Text). View handhabt die Clipboard-API.</summary>
    public event Func<string, Task>? ClipboardCopyRequested;

    /// <summary>Event zum Lesen der Zwischenablage. View handhabt die Clipboard-API und ruft PasteValue() auf.</summary>
    public event Func<Task>? ClipboardPasteRequested;

    /// <summary>Event zum Teilen eines Textes (Share Intent auf Android, Clipboard auf Desktop).</summary>
    public event Func<string, Task>? ShareRequested;

    /// <summary>Event nach erfolgreicher Berechnung (für Ergebnis-Animation im View).</summary>
    public event EventHandler? CalculationCompleted;

    /// <summary>Event bei Fehler (für Shake-Animation im View).</summary>
    public event EventHandler? ErrorShakeRequested;

    /// <summary>Event nach erfolgreichem Copy (für Copy-Feedback-Animation im View).</summary>
    public event EventHandler? CopyFeedbackRequested;

    /// <summary>Event wenn sich die aktive Funktion ändert (für Funktionsgraph im View).</summary>
    public event EventHandler? FunctionGraphChanged;

    #endregion

    #region Funktionsgraph-Properties

    /// <summary>Name der aktiven wissenschaftlichen Funktion (null = kein Graph).</summary>
    public string? ActiveFunctionName
    {
        get => _activeFunctionName;
        private set
        {
            if (_activeFunctionName != value)
            {
                _activeFunctionName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowFunctionGraph));
            }
        }
    }

    /// <summary>Die aktive Funktion als Delegate für den Graph-Renderer.</summary>
    public Func<float, float>? ActiveFunction
    {
        get => _activeFunction;
        private set { _activeFunction = value; OnPropertyChanged(); }
    }

    /// <summary>Aktueller X-Wert für den leuchtenden Punkt auf dem Funktionsgraph.</summary>
    public float? FunctionGraphCurrentX
    {
        get => _functionGraphCurrentX;
        private set { _functionGraphCurrentX = value; OnPropertyChanged(); }
    }

    /// <summary>Gibt an ob der Funktionsgraph sichtbar sein soll.</summary>
    public bool ShowFunctionGraph => ActiveFunctionName != null;

    #endregion

    #region Constructor & Lifecycle

    public CalculatorViewModel(CalculatorEngine engine, ExpressionParser parser,
                                ILocalizationService localization, IHistoryService historyService,
                                IPreferencesService preferences, IHapticService haptic)
    {
        _engine = engine;
        _parser = parser;
        _localization = localization;
        _historyService = historyService;
        _preferences = preferences;
        _haptic = haptic;
        _localization.LanguageChanged += OnLanguageChanged;
        _historyService.HistoryChanged += OnHistoryChanged;

        // Gespeicherten Modus laden
        _currentMode = (CalculatorMode)_preferences.Get(ModeKey, 0);

        // Zahlenformat und Dezimalstellen initialisieren
        _numberFormat = _preferences.Get(NumberFormatKey, 0);
        _decimalSep = _numberFormat == 1 ? ',' : '.';
        _thousandSep = _numberFormat == 1 ? '.' : ',';
        _cachedDecimalPlaces = _preferences.Get("calculator_decimal_places", -1);

        LoadHistory();
        LoadMemory();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ModeBasicText));
        OnPropertyChanged(nameof(ModeScientificText));
        OnPropertyChanged(nameof(HistoryTitleText));
        OnPropertyChanged(nameof(ClearHistoryText));
        OnPropertyChanged(nameof(NoCalculationsYetText));
        OnPropertyChanged(nameof(HistoryTodayText));
        OnPropertyChanged(nameof(HistoryYesterdayText));
        OnPropertyChanged(nameof(HistoryOlderText));
        // Gruppierte History neu bauen (Header-Texte haben sich geändert)
        _cachedGroupedHistory = BuildGroupedHistory();
        OnPropertyChanged(nameof(GroupedHistory));
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        // Caches aktualisieren bevor PropertyChanged gefeuert wird
        RebuildHistoryCaches();
        OnPropertyChanged(nameof(HistoryEntries));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(RecentHistory));
        OnPropertyChanged(nameof(GroupedHistory));
        if (!_isLoading)
            SaveHistory();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _localization.LanguageChanged -= OnLanguageChanged;
        _historyService.HistoryChanged -= OnHistoryChanged;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>Snapshot des Rechner-Zustands für Undo/Redo.</summary>
public record CalculatorState(
    string Display,
    string Expression,
    bool IsNewCalculation,
    string? ActiveOperator,
    string? LastOperator,
    string? LastOperand,
    bool HasError,
    string ErrorMessage,
    string PreviewResult,
    double LastResult);

public enum CalculatorMode
{
    Basic,
    Scientific
}

/// <summary>Gruppierter Verlaufs-Abschnitt (Heute/Gestern/Älter).</summary>
public record HistoryGroup(string Header, IReadOnlyList<CalculationHistoryEntry> Entries);
