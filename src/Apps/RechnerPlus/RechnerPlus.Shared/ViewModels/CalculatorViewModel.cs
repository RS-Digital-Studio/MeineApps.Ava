using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.CalcLib;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using RechnerPlus.Services;

namespace RechnerPlus.ViewModels;

public partial class CalculatorViewModel : ObservableObject, IDisposable
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

    // Localized strings for view bindings
    public string ModeBasicText => _localization.GetString("ModeBasic");
    public string ModeScientificText => _localization.GetString("ModeScientific");

    // History localized strings
    public string HistoryTitleText => _localization.GetString("HistoryTitle");
    public string ClearHistoryText => _localization.GetString("ClearHistory");
    public string NoCalculationsYetText => _localization.GetString("NoCalculationsYet");

    public bool HasHistory => _historyService.History.Count > 0;
    public IReadOnlyList<CalculationHistoryEntry> HistoryEntries => _historyService.History;

    // Gecachte History-Listen (werden bei OnHistoryChanged / OnLanguageChanged neu berechnet)
    private IReadOnlyList<CalculationHistoryEntry> _cachedRecentHistory = [];
    private List<HistoryGroup> _cachedGroupedHistory = [];

    /// <summary>Die letzten 2 Berechnungen für Mini-History unter dem Display.</summary>
    public IReadOnlyList<CalculationHistoryEntry> RecentHistory => _cachedRecentHistory;

    /// <summary>Gruppierte History nach Datum (Heute/Gestern/Älter).</summary>
    public List<HistoryGroup> GroupedHistory => _cachedGroupedHistory;

    // Lokalisierte Gruppen-Header
    public string HistoryTodayText => _localization.GetString("HistoryToday") ?? "Heute";
    public string HistoryYesterdayText => _localization.GetString("HistoryYesterday") ?? "Gestern";
    public string HistoryOlderText => _localization.GetString("HistoryOlder") ?? "Älter";

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

    [ObservableProperty]
    private double _memory;

    [ObservableProperty]
    private bool _hasMemory;

    /// <summary>Formatierter Memory-Wert für ToolTip-Anzeige.</summary>
    public string MemoryDisplay => HasMemory ? FormatResult(Memory) : "";

    /// <summary>Text für den Dezimal-Button (abhängig vom Zahlenformat).</summary>
    public string DecimalButtonText => _decimalSep.ToString();

    private bool _isNewCalculation = true;

    /// <summary>Display-Wert ohne Tausender-Trennzeichen, normalisiert auf InvariantCulture für Berechnungen.</summary>
    private string RawDisplay
    {
        get
        {
            var raw = Display.Replace(_thousandSep.ToString(), "");
            if (_numberFormat == 1)
                raw = raw.Replace(',', '.'); // EU-Komma → Punkt für Parsing
            return raw;
        }
    }

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

    // Funktionsgraph-Daten
    private string? _activeFunctionName;
    private Func<float, float>? _activeFunction;
    private float? _functionGraphCurrentX;

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

    /// <summary>Setzt die aktive Funktion und feuert das FunctionGraphChanged-Event.</summary>
    private void SetActiveFunction(string name, Func<float, float> func, float currentX)
    {
        ActiveFunctionName = name;
        ActiveFunction = func;
        FunctionGraphCurrentX = currentX;
        FunctionGraphChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Blendet den Funktionsgraph aus.</summary>
    public void ClearFunctionGraph()
    {
        ActiveFunctionName = null;
        ActiveFunction = null;
        FunctionGraphCurrentX = null;
        FunctionGraphChanged?.Invoke(this, EventArgs.Empty);
    }

    [ObservableProperty]
    private bool _showClearHistoryConfirm;

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

    /// <summary>Responsive Schriftgröße bei Display-Änderung aktualisieren.</summary>
    partial void OnDisplayChanged(string value)
    {
        var len = value.Length;
        DisplayFontSize = len switch
        {
            <= 8 => 52,
            <= 12 => 42,
            <= 16 => 34,
            <= 20 => 26,
            _ => 20
        };
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

    /// <summary>Baut die gecachten History-Listen neu auf.</summary>
    private void RebuildHistoryCaches()
    {
        _cachedRecentHistory = _historyService.History.Take(2).ToList();
        _cachedGroupedHistory = BuildGroupedHistory();
    }

    #region Hilfsmethoden

    /// <summary>Zahlenformat und Dezimalstellen aus den Einstellungen neu laden (nach Settings-Änderung).</summary>
    public void RefreshNumberFormat()
    {
        // Dezimalstellen-Cache aktualisieren
        _cachedDecimalPlaces = _preferences.Get("calculator_decimal_places", -1);

        var newFormat = _preferences.Get(NumberFormatKey, 0);
        if (newFormat == _numberFormat)
        {
            // Zahlenformat gleich, aber Dezimalstellen könnten sich geändert haben → Display aktualisieren
            var parseOk = TryParseDisplay(out var val);
            if (parseOk && !HasError && Display != "0")
                Display = FormatResult(val);
            return;
        }

        // Aktuellen Display-Wert vor Format-Wechsel parsen
        var parseSuccess = TryParseDisplay(out var currentValue);
        var hadValidValue = parseSuccess && !HasError && Display != "0";

        _numberFormat = newFormat;
        _decimalSep = newFormat == 1 ? ',' : '.';
        _thousandSep = newFormat == 1 ? '.' : ',';
        OnPropertyChanged(nameof(DecimalButtonText));

        // Display nur aktualisieren wenn der Parse erfolgreich war
        if (hadValidValue)
            Display = FormatResult(currentValue);
    }

    /// <summary>Parst den Display-Wert als double. Tausender-Trennzeichen werden entfernt.</summary>
    private bool TryParseDisplay(out double value) =>
        double.TryParse(RawDisplay, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Zentrale Methode: Setzt Display aus einem Berechnungsergebnis.
    /// Bei NaN/Infinity wird automatisch ShowError() aufgerufen.
    /// </summary>
    private bool SetDisplayFromResult(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            ShowError(_localization.GetString("Error"));
            return false;
        }
        Display = FormatResult(value);
        return true;
    }

    /// <summary>
    /// Setzt Display aus einem CalculationResult (Engine-Methoden mit Error-Handling).
    /// </summary>
    private bool SetDisplayFromResult(CalculationResult result)
    {
        if (result.IsError)
        {
            ShowError(result.ErrorMessage ?? _localization.GetString("Error"));
            return false;
        }
        return SetDisplayFromResult(result.Value);
    }

    /// <summary>Zählt offene Klammern im gegebenen Ausdruck.</summary>
    private static int CountOpenParentheses(string expr)
    {
        int count = 0;
        foreach (char c in expr)
        {
            if (c == '(') count++;
            else if (c == ')') count--;
        }
        return count;
    }

    /// <summary>Zählt offene Klammern in der aktuellen Expression.</summary>
    private int CountOpenParentheses() => CountOpenParentheses(Expression);

    /// <summary>Prüft ob ein Zeichen ein Rechenoperator ist.</summary>
    private static bool IsOperatorChar(char c) =>
        c is '+' or '-' or '\u2212' or '*' or '\u00D7' or '/' or '\u00F7' or '^';

    private string FormatResult(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return _localization.GetString("Error");

        // Floating-Point-Artefakte entfernen (0.1 + 0.2 = 0.3 statt 0.30000000000000004)
        value = Math.Round(value, 10);

        // Dezimalstellen-Einstellung aus Cache (-1 = Auto)
        var decimalPlaces = _cachedDecimalPlaces;
        string raw;
        if (decimalPlaces >= 0)
            raw = value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);
        else
            raw = value.ToString("G15", CultureInfo.InvariantCulture);

        // Tausender-Trennzeichen für den Integer-Teil einfügen (nur Anzeige)
        if (raw.Contains('E') || raw.Contains('e'))
            return raw;

        // raw ist immer InvariantCulture ("." als Dezimal)
        var dotIndex = raw.IndexOf('.');
        string integerPart, decimalPart;
        if (dotIndex >= 0)
        {
            integerPart = raw[..dotIndex];
            decimalPart = _decimalSep + raw[(dotIndex + 1)..]; // Locale-Dezimaltrenner
        }
        else
        {
            integerPart = raw;
            decimalPart = "";
        }

        bool isNegative = integerPart.StartsWith('-');
        var absInt = isNegative ? integerPart[1..] : integerPart;

        if (absInt.Length > 3)
        {
            var sb = new System.Text.StringBuilder();
            int count = 0;
            for (int i = absInt.Length - 1; i >= 0; i--)
            {
                if (count > 0 && count % 3 == 0)
                    sb.Insert(0, _thousandSep); // Locale-Tausendertrenner
                sb.Insert(0, absInt[i]);
                count++;
            }
            absInt = sb.ToString();
        }

        return (isNegative ? "-" : "") + absInt + decimalPart;
    }

    private void ShowError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        Expression = "";
        _isNewCalculation = true;
        _lastOperator = null;
        _lastOperand = null;
        ActiveOperator = null;
        PreviewResult = "";
        _haptic.HeavyClick();
        ErrorShakeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearError() { HasError = false; ErrorMessage = ""; }

    /// <summary>Aktualisiert die Live-Preview bei jeder Eingabe.</summary>
    private void UpdatePreview()
    {
        if (HasError || string.IsNullOrWhiteSpace(Expression))
        {
            PreviewResult = "";
            return;
        }

        try
        {
            var previewExpr = Expression;

            // Aktuellen Display-Wert anhängen wenn nicht gerade ein neues Ergebnis
            if (!_isNewCalculation)
                previewExpr += RawDisplay;
            else
            {
                var trimmed = previewExpr.TrimEnd();
                // Trailing Operator entfernen für Preview
                if (trimmed.Length > 0 && IsOperatorChar(trimmed[^1]))
                    previewExpr = trimmed[..^1].TrimEnd();
            }

            if (string.IsNullOrWhiteSpace(previewExpr))
            {
                PreviewResult = "";
                return;
            }

            // Offene Klammern automatisch schließen für Preview
            var openCount = CountOpenParentheses(previewExpr);
            for (int i = 0; i < openCount; i++)
                previewExpr += ")";

            var result = _parser.Evaluate(previewExpr);
            if (!result.IsError)
            {
                var formatted = FormatResult(result.Value);
                // Nur anzeigen wenn sich der Wert vom Display unterscheidet
                if (formatted != Display)
                    PreviewResult = "= " + formatted;
                else
                    PreviewResult = "";
            }
            else
            {
                PreviewResult = "";
            }
        }
        catch
        {
            PreviewResult = "";
        }
    }

    #endregion

    #region Verlauf-Persistenz

    private void LoadHistory()
    {
        _isLoading = true;
        try
        {
            var json = _preferences.Get<string>(HistoryKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                var entries = JsonSerializer.Deserialize<List<CalculationHistoryEntry>>(json);
                if (entries is { Count: > 0 })
                {
                    _historyService.LoadEntries(entries);
                }
            }
        }
        catch
        {
            // Beschädigten Verlauf ignorieren
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_historyService.History);
            _preferences.Set(HistoryKey, json);
        }
        catch
        {
            // Speicherfehler ignorieren
        }
    }

    #endregion

    #region Memory-Persistenz

    private void LoadMemory()
    {
        var hasMemory = _preferences.Get(MemoryHasKey, false);
        if (hasMemory)
        {
            _memory = _preferences.Get(MemoryKey, 0.0);
            _hasMemory = true;
        }
    }

    private void SaveMemory()
    {
        _preferences.Set(MemoryKey, Memory);
        _preferences.Set(MemoryHasKey, HasMemory);
    }

    #endregion

    #region Undo/Redo

    public bool CanUndo => _undoList.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Erstellt einen Snapshot des aktuellen Rechner-Zustands.</summary>
    private CalculatorState CreateCurrentState() => new(
        Display, Expression, _isNewCalculation,
        ActiveOperator, _lastOperator, _lastOperand,
        HasError, ErrorMessage, PreviewResult, _lastResult);

    /// <summary>Speichert den aktuellen Zustand auf den Undo-Stack (LinkedList, O(1)).</summary>
    private void SaveState()
    {
        // Ältesten Eintrag entfernen wenn Limit erreicht (O(1) statt Array-Umkopieren)
        if (_undoList.Count >= MaxUndoStates)
            _undoList.RemoveFirst();

        _undoList.AddLast(CreateCurrentState());

        _redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    /// <summary>Stellt einen gespeicherten Zustand wieder her.</summary>
    private void RestoreState(CalculatorState state)
    {
        Display = state.Display;
        Expression = state.Expression;
        _isNewCalculation = state.IsNewCalculation;
        ActiveOperator = state.ActiveOperator;
        _lastOperator = state.LastOperator;
        _lastOperand = state.LastOperand;
        HasError = state.HasError;
        ErrorMessage = state.ErrorMessage;
        PreviewResult = state.PreviewResult;
        _lastResult = state.LastResult;
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undoList.Count == 0) return;

        // Aktuellen Zustand auf Redo-Stack sichern
        _redoStack.Push(CreateCurrentState());

        // Letzten Eintrag aus LinkedList holen und entfernen (neuester = letzter Knoten)
        var state = _undoList.Last!.Value;
        _undoList.RemoveLast();
        RestoreState(state);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        _haptic.Tick();
    }

    [RelayCommand]
    private void Redo()
    {
        if (_redoStack.Count == 0) return;

        // Aktuellen Zustand auf Undo-Liste sichern
        if (_undoList.Count >= MaxUndoStates)
            _undoList.RemoveFirst();
        _undoList.AddLast(CreateCurrentState());

        RestoreState(_redoStack.Pop());
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        _haptic.Tick();
    }

    #endregion

    #region History Commands

    [RelayCommand]
    private void ShowHistory() => IsHistoryVisible = true;

    [RelayCommand]
    private void HideHistory() => IsHistoryVisible = false;

    [RelayCommand]
    private void ClearHistory()
    {
        ShowClearHistoryConfirm = true;
    }

    [RelayCommand]
    private void ConfirmClearHistory()
    {
        ShowClearHistoryConfirm = false;
        _historyService.Clear();
    }

    [RelayCommand]
    private void CancelClearHistory()
    {
        ShowClearHistoryConfirm = false;
    }

    [RelayCommand]
    private void DeleteHistoryEntry(CalculationHistoryEntry entry)
    {
        _historyService.DeleteEntry(entry);
    }

    [RelayCommand]
    private async Task CopyDisplay()
    {
        if (HasError) return;
        if (ClipboardCopyRequested != null)
            await ClipboardCopyRequested.Invoke(Display);
        FloatingTextRequested?.Invoke(_localization.GetString("CopySuccess") ?? "Copied!", "info");
        CopyFeedbackRequested?.Invoke(this, EventArgs.Empty);
        _haptic.Tick();
    }

    [RelayCommand]
    private async Task ShareDisplay()
    {
        if (HasError) return;
        string shareText;
        if (string.IsNullOrEmpty(Expression))
        {
            // Kein laufender Ausdruck → nur Display teilen
            shareText = Display;
        }
        else if (_isNewCalculation)
        {
            // Gerade berechnet → Expression ohne Display (Display hat Ergebnis)
            shareText = $"{Expression.TrimEnd()} = {Display}";
        }
        else
        {
            // Mitte der Eingabe → Expression + aktuellen Wert
            shareText = $"{Expression.TrimEnd()} {Display}";
        }
        if (ShareRequested != null)
            await ShareRequested.Invoke(shareText);
        else if (ClipboardCopyRequested != null)
        {
            // Fallback: In Zwischenablage kopieren (Desktop)
            await ClipboardCopyRequested.Invoke(shareText);
            FloatingTextRequested?.Invoke(_localization.GetString("CopySuccess") ?? "Copied!", "info");
        }
        _haptic.Tick();
    }

    [RelayCommand]
    private async Task PasteFromClipboard()
    {
        if (ClipboardPasteRequested != null)
            await ClipboardPasteRequested.Invoke();
    }

    /// <summary>Wird von der View nach Clipboard-Lesen aufgerufen.</summary>
    public void PasteValue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            FloatingTextRequested?.Invoke(_localization.GetString("ClipboardEmpty") ?? "Clipboard empty", "warning");
            return;
        }
        text = text.Trim();
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var pastedValue))
        {
            Display = FormatResult(pastedValue);
            _isNewCalculation = false;
            ClearError();
            UpdatePreview();
            FloatingTextRequested?.Invoke(_localization.GetString("PasteSuccess") ?? "Pasted", "info");
        }
        else
        {
            FloatingTextRequested?.Invoke(_localization.GetString("PasteInvalidNumber") ?? "Invalid number", "warning");
        }
    }

    /// <summary>Tap auf History-Eintrag: Ergebnis ins Display übernehmen.</summary>
    [RelayCommand]
    private void SelectHistoryEntry(CalculationHistoryEntry entry)
    {
        // ResultValue statt Result verwenden → korrekt nach Locale-Wechsel
        Display = FormatResult(entry.ResultValue);
        Expression = "";
        _isNewCalculation = true;
        IsHistoryVisible = false;
        ActiveOperator = null;
        PreviewResult = "";
        ClearError();
    }

    /// <summary>Long-Press auf History-Eintrag: Expression in Zwischenablage kopieren.</summary>
    [RelayCommand]
    private async Task CopyHistoryExpression(CalculationHistoryEntry entry)
    {
        if (ClipboardCopyRequested != null)
            await ClipboardCopyRequested.Invoke($"{entry.Expression} = {entry.Result}");
        FloatingTextRequested?.Invoke(_localization.GetString("CopySuccess") ?? "Copied!", "info");
        _haptic.Tick();
    }

    #endregion

    #region Eingabe

    [RelayCommand]
    private void InputDigit(string digit)
    {
        if (Expression.Length + Display.Length >= MaxExpressionLength)
        {
            ShowError(_localization.GetString("ExpressionTooLong") ?? "Expression too long");
            return;
        }

        if (_isNewCalculation || Display == "0")
        {
            // Implizite Multiplikation nach ")" (z.B. "(5+3)2" → "(5+3) × 2")
            if (Expression.TrimEnd().EndsWith(')'))
            {
                Expression += " \u00D7 ";
            }
            Display = digit;
            _isNewCalculation = false;
            ActiveOperator = null;
        }
        else
        {
            // Tausender-Trennzeichen entfernen vor dem Anhängen (z.B. nach Negate/MR)
            var raw = Display.Replace(_thousandSep.ToString(), "");
            Display = raw + digit;
        }
        ClearError();
        UpdatePreview();
        _haptic.Tick();
    }

    [RelayCommand]
    private void InputOperator(string op)
    {
        if (HasError) return;
        SaveState();

        // Wenn noch keine neue Zahl eingegeben wurde (z.B. direkt nach anderem Operator oder ")")
        if (_isNewCalculation && Expression.Length > 0)
        {
            var trimmed = Expression.TrimEnd();
            if (trimmed.Length > 0)
            {
                var lastChar = trimmed[^1];

                // Expression endet mit Operator → ersetzen (z.B. "5 + " → "5 × ")
                if (IsOperatorChar(lastChar))
                {
                    Expression = trimmed[..^1] + op + " ";
                    ActiveOperator = op;
                    UpdatePreview();
                    _haptic.Click();
                    return;
                }

                // Expression endet mit ")" → Operator direkt anfügen ohne "0"
                if (lastChar == ')')
                {
                    Expression = trimmed + " " + op + " ";
                    ActiveOperator = op;
                    UpdatePreview();
                    _haptic.Click();
                    return;
                }
            }
        }

        Expression += RawDisplay + " " + op + " ";
        Display = "0";
        _isNewCalculation = true;
        ActiveOperator = op;
        UpdatePreview();
        _haptic.Click();
    }

    [RelayCommand]
    private void InputDecimal()
    {
        var decStr = _decimalSep.ToString();

        // Implizite Multiplikation nach ")" (z.B. "(5+3).5" → "(5+3) × 0.5")
        if (_isNewCalculation && Expression.TrimEnd().EndsWith(')'))
        {
            Expression += " \u00D7 ";
            Display = "0" + decStr;
            _isNewCalculation = false;
            _haptic.Tick();
            return;
        }

        if (!Display.Contains(_decimalSep))
        {
            Display += decStr;
            _isNewCalculation = false;
            UpdatePreview();
            _haptic.Tick();
        }
    }

    /// <summary>
    /// Intelligenter Klammer-Button (wie Google Calculator):
    /// Setzt ")" wenn offene Klammern vorhanden UND gerade eine Zahl eingegeben wurde, sonst "(".
    /// </summary>
    [RelayCommand]
    private void InputSmartParenthesis()
    {
        int openCount = CountOpenParentheses();

        // Schließende Klammer wenn: offene Klammern vorhanden UND eine Zahl eingegeben
        if (openCount > 0 && !_isNewCalculation)
            InputParenthesis(")");
        else
            InputParenthesis("(");
    }

    [RelayCommand]
    private void InputParenthesis(string paren)
    {
        if (paren == "(")
        {
            if (_isNewCalculation || Display == "0")
            {
                // Implizite Multiplikation nach ")" (z.B. "(5+3)(2+1)" → "(5+3) × (2+1)")
                if (Expression.TrimEnd().EndsWith(')'))
                {
                    Expression += " \u00D7 (";
                }
                else
                {
                    Expression += "(";
                }
                Display = "0";
            }
            else
            {
                Expression += RawDisplay + " \u00D7 (";
                Display = "0";
            }
            _haptic.Click();
        }
        else // ")"
        {
            // Klammer-Validierung: ")" nur wenn offene Klammern existieren
            if (CountOpenParentheses() <= 0) return;

            // Leere Klammern "()" verhindern
            if (Expression.TrimEnd().EndsWith('(') && _isNewCalculation) return;

            if (_isNewCalculation)
            {
                // Keine Zahl eingegeben → nur ")" anfügen (z.B. nach vorherigem ")")
                Expression += ")";
            }
            else
            {
                Expression += RawDisplay + ")";
            }
            Display = "0";
            _haptic.Click();
        }
        _isNewCalculation = true;
        ActiveOperator = null;
        UpdatePreview();
    }

    #endregion

    #region Berechnung

    /// <summary>
    /// Wiederholtes "=" ohne neue Eingabe: letzte Operation wiederholen (wie Windows-Rechner).
    /// z.B. "5 + 3 = = =" → 8, 11, 14
    /// </summary>
    private bool TryRepeatLastCalculation()
    {
        if (!_isNewCalculation || !string.IsNullOrEmpty(Expression) ||
            _lastOperator == null || _lastOperand == null)
            return false;

        var repeatExpr = $"{RawDisplay} {_lastOperator} {_lastOperand}";
        var repeatResult = _parser.Evaluate(repeatExpr);
        if (!repeatResult.IsError)
        {
            var formatted = FormatResult(repeatResult.Value);
            _historyService.AddEntry(repeatExpr, formatted, repeatResult.Value);
            _lastResult = repeatResult.Value;
            Display = formatted;
            FloatingTextRequested?.Invoke($"= {formatted}", "result");
            CalculationCompleted?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            ShowError(repeatResult.ErrorMessage ?? _localization.GetString("Error"));
        }
        return true;
    }

    [RelayCommand]
    private void Calculate()
    {
        if (HasError) return;
        SaveState();

        // Wiederholtes "=" → letzte Operation wiederholen
        if (TryRepeatLastCalculation()) return;

        try
        {
            string fullExpression;
            string? operatorForRepeat = null;
            string? operandForRepeat = null;

            // Keine neue Eingabe seit letztem Operator/Klammer
            if (_isNewCalculation && Expression.Length > 0)
            {
                var trimmed = Expression.TrimEnd();

                if (trimmed.EndsWith(')'))
                {
                    // "(5+3)" ist bereits vollständig → kein "0" anhängen
                    fullExpression = trimmed;
                }
                else if (trimmed.Length > 0 && IsOperatorChar(trimmed[^1]))
                {
                    // Trailing-Operator entfernen: "5 + " → nur "5" berechnen
                    fullExpression = trimmed[..^1].TrimEnd();
                }
                else
                {
                    fullExpression = Expression + RawDisplay;
                }
            }
            else
            {
                fullExpression = Expression + RawDisplay;

                // Operator und Operand für wiederholtes "=" bestimmen
                var trimmed = Expression.TrimEnd();
                if (trimmed.Length > 0)
                {
                    for (int i = trimmed.Length - 1; i >= 0; i--)
                    {
                        if (IsOperatorChar(trimmed[i]))
                        {
                            operatorForRepeat = trimmed[i].ToString();
                            operandForRepeat = RawDisplay;
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(fullExpression))
                return;

            // Offene Klammern automatisch schließen (wie Windows-Rechner)
            var openCount = CountOpenParentheses(fullExpression);
            for (int i = 0; i < openCount; i++)
                fullExpression += ")";

            var result = _parser.Evaluate(fullExpression);

            if (!result.IsError)
            {
                var formattedResult = FormatResult(result.Value);
                if (double.IsNaN(result.Value) || double.IsInfinity(result.Value))
                {
                    ShowError(_localization.GetString("Error"));
                    return;
                }

                _historyService.AddEntry(fullExpression, formattedResult, result.Value);
                _lastResult = result.Value;
                Display = formattedResult;
                FloatingTextRequested?.Invoke($"= {formattedResult}", "result");
                Expression = "";
                _isNewCalculation = true;
                ActiveOperator = null;
                PreviewResult = "";

                // Für wiederholtes "=" merken
                _lastOperator = operatorForRepeat;
                _lastOperand = operandForRepeat;
                _haptic.HeavyClick();
                CalculationCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ShowError(result.ErrorMessage ?? _localization.GetString("Error"));
            }
        }
        catch (Exception)
        {
            ShowError(_localization.GetString("Error"));
        }
    }

    #endregion

    #region Bearbeitung

    [RelayCommand]
    private void Clear()
    {
        SaveState();
        Display = "0";
        Expression = "";
        _isNewCalculation = true;
        _lastOperator = null;
        _lastOperand = null;
        ActiveOperator = null;
        PreviewResult = "";
        ClearError();
        _haptic.HeavyClick();
    }

    [RelayCommand]
    private void ClearEntry()
    {
        SaveState();
        Display = "0";
        _isNewCalculation = true;
        ClearError();
        UpdatePreview();
        _haptic.Click();
    }

    [RelayCommand]
    private void Backspace()
    {
        if (HasError) return;

        // Tausender-Trennzeichen entfernen beim Bearbeiten eines Ergebnisses
        var raw = RawDisplay;
        if (raw.Length > 1)
        {
            var newRaw = raw[..^1];
            // Ungültige Zwischenzustände bei Scientific-Notation verhindern
            // (z.B. "1E+" oder "1E" sind keine gültigen Zahlen)
            if (newRaw.Contains('E', StringComparison.OrdinalIgnoreCase) &&
                !double.TryParse(newRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                Display = "0";
                _isNewCalculation = true;
            }
            else
            {
                Display = newRaw;
                _isNewCalculation = false;
            }
        }
        else
        {
            Display = "0";
            _isNewCalculation = true;
        }
        UpdatePreview();
        _haptic.Tick();
    }

    [RelayCommand]
    private void Negate()
    {
        SaveState();
        if (RawDisplay != "0")
        {
            if (TryParseDisplay(out var value))
                Display = FormatResult(-value);
            else
            {
                var raw = RawDisplay;
                Display = raw.StartsWith('-') ? raw[1..] : "-" + raw;
            }
            UpdatePreview();
        }
        _haptic.Click();
    }

    [RelayCommand]
    private void Percent()
    {
        SaveState();
        if (!TryParseDisplay(out var value))
            return;

        var trimmedExpr = Expression.TrimEnd();
        if (trimmedExpr.Length > 0)
        {
            // Letzten Operator in der Expression finden
            char lastOp = ' ';
            int lastOpIndex = -1;
            for (int i = trimmedExpr.Length - 1; i >= 0; i--)
            {
                if (IsOperatorChar(trimmedExpr[i]))
                {
                    lastOp = trimmedExpr[i];
                    lastOpIndex = i;
                    break;
                }
            }

            // Bei Addition/Subtraktion: kontextuelles Prozent (wie Windows-Rechner)
            if (lastOpIndex >= 0 && (lastOp == '+' || lastOp == '-' || lastOp == '\u2212'))
            {
                var baseExpr = trimmedExpr[..lastOpIndex].TrimEnd();
                if (!string.IsNullOrEmpty(baseExpr))
                {
                    // Offene Klammern für Auswertung schließen (zentrale Hilfsmethode)
                    var openCount = CountOpenParentheses(baseExpr);
                    for (int j = 0; j < openCount; j++)
                        baseExpr += ")";

                    var baseResult = _parser.Evaluate(baseExpr);
                    if (!baseResult.IsError)
                    {
                        SetDisplayFromResult(baseResult.Value * value / 100);
                        _isNewCalculation = false;
                        _haptic.Click();
                        return;
                    }
                }
            }
        }

        // Standard: einfach durch 100 teilen (bei ×, ÷, oder ohne Expression)
        SetDisplayFromResult(value / 100);
        _isNewCalculation = false;
        UpdatePreview();
        _haptic.Click();
    }

    [RelayCommand]
    private void SquareRoot()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        if (SetDisplayFromResult(_engine.SquareRoot(value)))
        {
            _isNewCalculation = true;
            SetActiveFunction("sqrt", x => x >= 0 ? (float)Math.Sqrt(x) : float.NaN, (float)value);
        }
        _haptic.Click();
    }

    [RelayCommand]
    private void Square()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        if (SetDisplayFromResult(_engine.Square(value)))
        {
            _isNewCalculation = true;
            SetActiveFunction("x\u00B2", x => x * x, (float)value);
        }
        _haptic.Click();
    }

    [RelayCommand]
    private void Reciprocal()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        if (SetDisplayFromResult(_engine.Reciprocal(value)))
        {
            _isNewCalculation = true;
            SetActiveFunction("1/x", x => MathF.Abs(x) < 0.001f ? float.NaN : 1f / x, (float)value);
        }
        _haptic.Click();
    }

    [RelayCommand]
    private void Power()
    {
        // Gleiche Logik wie andere Operatoren (Operator-Ersetzung, ")" Handling)
        InputOperator("^");
    }

    [RelayCommand]
    private void Factorial()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        if (value < 0 || value > 170 || value != Math.Floor(value))
        {
            ShowError(_localization.GetString("FactorialRangeError"));
            return;
        }
        if (SetDisplayFromResult(_engine.Factorial((int)value)))
            _isNewCalculation = true;
        _haptic.Click();
    }

    #endregion

    #region Wissenschaftliche Funktionen

    [RelayCommand]
    private void ToggleInverse()
    {
        IsInverseMode = !IsInverseMode;
        _haptic.Tick();
    }

    /// <summary>Dispatcher: sin oder sin⁻¹ je nach INV-Modus.</summary>
    [RelayCommand]
    private void SinOrInverse()
    {
        if (IsInverseMode)
            Asin();
        else
            Sin();
    }

    /// <summary>Dispatcher: cos oder cos⁻¹ je nach INV-Modus.</summary>
    [RelayCommand]
    private void CosOrInverse()
    {
        if (IsInverseMode)
            Acos();
        else
            Cos();
    }

    /// <summary>Dispatcher: tan oder tan⁻¹ je nach INV-Modus.</summary>
    [RelayCommand]
    private void TanOrInverse()
    {
        if (IsInverseMode)
            Atan();
        else
            Tan();
    }

    /// <summary>Dispatcher: log oder 10^x je nach INV-Modus.</summary>
    [RelayCommand]
    private void LogOrInverse()
    {
        if (IsInverseMode)
            Exp10Function();
        else
            Log();
    }

    /// <summary>Dispatcher: ln oder e^x je nach INV-Modus.</summary>
    [RelayCommand]
    private void LnOrInverse()
    {
        if (IsInverseMode)
            ExpFunction();
        else
            Ln();
    }

    private void Sin()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        var angle = IsRadians ? value : _engine.DegreesToRadians(value);
        if (SetDisplayFromResult(_engine.Sin(angle)))
        {
            _isNewCalculation = true;
            SetActiveFunction("sin", x => (float)Math.Sin(x), (float)angle);
        }
        _haptic.Click();
    }

    private void Cos()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        var angle = IsRadians ? value : _engine.DegreesToRadians(value);
        if (SetDisplayFromResult(_engine.Cos(angle)))
        {
            _isNewCalculation = true;
            SetActiveFunction("cos", x => (float)Math.Cos(x), (float)angle);
        }
        _haptic.Click();
    }

    private void Tan()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        var angle = IsRadians ? value : _engine.DegreesToRadians(value);
        var result = _engine.Tan(angle);
        if (result.IsError) { ShowError(result.ErrorMessage ?? _localization.GetString("Error")); return; }
        // Werte nahe der Polstellen erkennen (z.B. tan(89.9999999°))
        if (Math.Abs(result.Value) > 1e15)
        {
            ShowError(_localization.GetString("TangentUndefined") ?? "Tangent undefined");
            return;
        }
        if (SetDisplayFromResult(result.Value))
        {
            _isNewCalculation = true;
            SetActiveFunction("tan", x =>
            {
                var y = (float)Math.Tan(x);
                return MathF.Abs(y) > 100f ? float.NaN : y;
            }, (float)angle);
        }
        _haptic.Click();
    }

    private void Log()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        if (SetDisplayFromResult(_engine.Log(value)))
        {
            _isNewCalculation = true;
            SetActiveFunction("log", x => x > 0 ? (float)Math.Log10(x) : float.NaN, (float)value);
        }
        _haptic.Click();
    }

    private void Ln()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        if (SetDisplayFromResult(_engine.Ln(value)))
        {
            _isNewCalculation = true;
            SetActiveFunction("ln", x => x > 0 ? (float)Math.Log(x) : float.NaN, (float)value);
        }
        _haptic.Click();
    }

    private void Asin()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        var result = _engine.Asin(value);
        if (result.IsError) { ShowError(result.ErrorMessage ?? _localization.GetString("Error")); return; }
        var output = IsRadians ? result.Value : _engine.RadiansToDegrees(result.Value);
        if (SetDisplayFromResult(output))
            _isNewCalculation = true;
        _haptic.Click();
    }

    private void Acos()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        var result = _engine.Acos(value);
        if (result.IsError) { ShowError(result.ErrorMessage ?? _localization.GetString("Error")); return; }
        var output = IsRadians ? result.Value : _engine.RadiansToDegrees(result.Value);
        if (SetDisplayFromResult(output))
            _isNewCalculation = true;
        _haptic.Click();
    }

    private void Atan()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        var output = IsRadians ? _engine.Atan(value) : _engine.RadiansToDegrees(_engine.Atan(value));
        if (SetDisplayFromResult(output))
            _isNewCalculation = true;
        _haptic.Click();
    }

    private void ExpFunction()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        if (SetDisplayFromResult(_engine.Exp(value)))
            _isNewCalculation = true;
        _haptic.Click();
    }

    private void Exp10Function()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        if (SetDisplayFromResult(_engine.Exp10(value)))
            _isNewCalculation = true;
        _haptic.Click();
    }

    [RelayCommand]
    private void Pi()
    {
        Display = FormatResult(Math.PI);
        _isNewCalculation = true;
        _haptic.Tick();
    }

    [RelayCommand]
    private void Euler()
    {
        Display = FormatResult(Math.E);
        _isNewCalculation = true;
        _haptic.Tick();
    }

    [RelayCommand]
    private void Abs()
    {
        SaveState();
        if (!TryParseDisplay(out var value)) return;
        if (SetDisplayFromResult(_engine.Abs(value)))
            _isNewCalculation = true;
        _haptic.Click();
    }

    /// <summary>ANS-Taste: Letztes Berechnungsergebnis einfügen.</summary>
    [RelayCommand]
    private void Ans()
    {
        // Implizite Multiplikation nach ")" (z.B. "(5+3)Ans")
        if (_isNewCalculation && Expression.TrimEnd().EndsWith(')'))
            Expression += " \u00D7 ";

        Display = FormatResult(_lastResult);
        _isNewCalculation = false;
        ClearError();
        UpdatePreview();
        _haptic.Tick();
    }

    #endregion

    #region Memory

    [RelayCommand]
    private void MemoryClear()
    {
        Memory = 0;
        HasMemory = false;
        OnPropertyChanged(nameof(MemoryDisplay));
        SaveMemory();
        _haptic.Click();
    }

    [RelayCommand]
    private void MemoryRecall()
    {
        if (HasMemory)
        {
            Display = FormatResult(Memory);
            _isNewCalculation = true;
            _haptic.Click();
        }
    }

    [RelayCommand]
    private void MemoryAdd()
    {
        if (TryParseDisplay(out var value))
        {
            Memory += value;
            HasMemory = true;
            OnPropertyChanged(nameof(MemoryDisplay));
            SaveMemory();
            _haptic.Click();
        }
    }

    [RelayCommand]
    private void MemorySubtract()
    {
        if (TryParseDisplay(out var value))
        {
            Memory -= value;
            HasMemory = true;
            OnPropertyChanged(nameof(MemoryDisplay));
            SaveMemory();
            _haptic.Click();
        }
    }

    [RelayCommand]
    private void MemoryStore()
    {
        if (TryParseDisplay(out var value))
        {
            Memory = value;
            HasMemory = true;
            OnPropertyChanged(nameof(MemoryDisplay));
            SaveMemory();
            _haptic.Click();
        }
    }

    #endregion

    [RelayCommand]
    private void ToggleAngleMode()
    {
        IsRadians = !IsRadians;
        _haptic.Tick();
    }

    [RelayCommand]
    private void SetMode(CalculatorMode mode)
    {
        CurrentMode = mode;
        // Nur manuell gewählten Modus speichern (nicht auto-landscape)
        _preferences.Set(ModeKey, (int)mode);
        _haptic.Click();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _localization.LanguageChanged -= OnLanguageChanged;
        _historyService.HistoryChanged -= OnHistoryChanged;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
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
