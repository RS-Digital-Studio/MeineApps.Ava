using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.CalcLib;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

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

    private const string HistoryKey = "calculator_history";
    private const int MaxExpressionLength = 200;

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

    public string AngleModeText => IsRadians ? "RAD" : "DEG";
    public bool IsBasicMode => CurrentMode == CalculatorMode.Basic;
    public bool IsScientificMode => CurrentMode == CalculatorMode.Scientific;

    // Localized strings for view bindings
    public string ModeBasicText => _localization.GetString("ModeBasic");
    public string ModeScientificText => _localization.GetString("ModeScientific");

    // History localized strings
    public string HistoryTitleText => _localization.GetString("HistoryTitle");
    public string ClearHistoryText => _localization.GetString("ClearHistory");
    public string NoCalculationsYetText => _localization.GetString("NoCalculationsYet");

    public bool HasHistory => _historyService.History.Count > 0;
    public IReadOnlyList<CalculationHistoryEntry> HistoryEntries => _historyService.History;

    [ObservableProperty]
    private double _memory;

    [ObservableProperty]
    private bool _hasMemory;

    private bool _isNewCalculation = true;

    /// <summary>Event fuer Floating-Text-Anzeige (Text, Kategorie).</summary>
    public event Action<string, string>? FloatingTextRequested;

    /// <summary>Event zum Kopieren in die Zwischenablage (Text). View handhabt die Clipboard-API.</summary>
    public event Func<string, Task>? ClipboardCopyRequested;

    /// <summary>Event zum Lesen der Zwischenablage. View handhabt die Clipboard-API und ruft PasteValue() auf.</summary>
    public event Func<Task>? ClipboardPasteRequested;

    [ObservableProperty]
    private bool _showClearHistoryConfirm;

    public CalculatorViewModel(CalculatorEngine engine, ExpressionParser parser,
                                ILocalizationService localization, IHistoryService historyService,
                                IPreferencesService preferences)
    {
        _engine = engine;
        _parser = parser;
        _localization = localization;
        _historyService = historyService;
        _preferences = preferences;
        _localization.LanguageChanged += OnLanguageChanged;
        _historyService.HistoryChanged += OnHistoryChanged;

        LoadHistory();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ModeBasicText));
        OnPropertyChanged(nameof(ModeScientificText));
        OnPropertyChanged(nameof(HistoryTitleText));
        OnPropertyChanged(nameof(ClearHistoryText));
        OnPropertyChanged(nameof(NoCalculationsYetText));
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HistoryEntries));
        OnPropertyChanged(nameof(HasHistory));
        if (!_isLoading)
            SaveHistory();
    }

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
    private async Task CopyDisplay()
    {
        if (HasError) return;
        if (ClipboardCopyRequested != null)
            await ClipboardCopyRequested.Invoke(Display);
        FloatingTextRequested?.Invoke(_localization.GetString("CopySuccess") ?? "Copied!", "info");
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
        if (double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            Display = text;
            _isNewCalculation = false;
            ClearError();
            FloatingTextRequested?.Invoke(_localization.GetString("PasteSuccess") ?? "Pasted", "info");
        }
        else
        {
            FloatingTextRequested?.Invoke(_localization.GetString("PasteInvalidNumber") ?? "Invalid number", "warning");
        }
    }

    [RelayCommand]
    private void SelectHistoryEntry(CalculationHistoryEntry entry)
    {
        Display = entry.Result;
        Expression = "";
        _isNewCalculation = true;
        IsHistoryVisible = false;
        ClearError();
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
            Display = digit;
            _isNewCalculation = false;
        }
        else
        {
            Display += digit;
        }
        ClearError();
    }

    [RelayCommand]
    private void InputOperator(string op)
    {
        if (HasError) return;

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
                    return;
                }

                // Expression endet mit ")" → Operator direkt anfügen ohne "0"
                if (lastChar == ')')
                {
                    Expression = trimmed + " " + op + " ";
                    return;
                }
            }
        }

        Expression += Display + " " + op + " ";
        Display = "0";
        _isNewCalculation = true;
    }

    [RelayCommand]
    private void InputDecimal()
    {
        if (!Display.Contains('.'))
        {
            Display += ".";
            _isNewCalculation = false;
        }
    }

    [RelayCommand]
    private void InputParenthesis(string paren)
    {
        if (paren == "(")
        {
            if (_isNewCalculation || Display == "0")
            {
                Expression += "(";
                Display = "0";
            }
            else
            {
                Expression += Display + " \u00d7 (";
                Display = "0";
            }
        }
        else // ")"
        {
            Expression += Display + ")";
            Display = "0";
        }
        _isNewCalculation = true;
    }

    #endregion

    #region Berechnung

    [RelayCommand]
    private void Calculate()
    {
        if (HasError) return;

        try
        {
            string fullExpression;

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
                    fullExpression = Expression + Display;
                }
            }
            else
            {
                fullExpression = Expression + Display;
            }

            if (string.IsNullOrWhiteSpace(fullExpression))
                return;

            var result = _parser.Evaluate(fullExpression);

            if (!result.IsError)
            {
                var formattedResult = FormatResult(result.Value);
                _historyService.AddEntry(fullExpression, formattedResult, result.Value);
                Display = formattedResult;
                FloatingTextRequested?.Invoke($"= {formattedResult}", "result");
                Expression = "";
                _isNewCalculation = true;
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
        Display = "0";
        Expression = "";
        _isNewCalculation = true;
        ClearError();
    }

    [RelayCommand]
    private void ClearEntry()
    {
        Display = "0";
        _isNewCalculation = true;
        ClearError();
    }

    [RelayCommand]
    private void Backspace()
    {
        if (HasError) return;

        if (Display.Length > 1)
            Display = Display[..^1];
        else
            Display = "0";
    }

    [RelayCommand]
    private void Negate()
    {
        if (Display != "0")
        {
            Display = Display.StartsWith('-') ? Display[1..] : "-" + Display;
        }
    }

    [RelayCommand]
    private void Percent()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            Display = FormatResult(value / 100);
    }

    [RelayCommand]
    private void SquareRoot()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            if (value < 0) { ShowError(_localization.GetString("Error")); return; }
            Display = FormatResult(Math.Sqrt(value));
            _isNewCalculation = true;
        }
    }

    [RelayCommand]
    private void Square()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            Display = FormatResult(value * value);
            _isNewCalculation = true;
        }
    }

    [RelayCommand]
    private void Reciprocal()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            if (value == 0) { ShowError(_localization.GetString("Error")); return; }
            Display = FormatResult(1 / value);
            _isNewCalculation = true;
        }
    }

    [RelayCommand]
    private void Power()
    {
        if (HasError) return;
        Expression += Display + " ^ ";
        Display = "0";
        _isNewCalculation = true;
    }

    [RelayCommand]
    private void Factorial()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            if (value < 0 || value > 170 || value != Math.Floor(value))
            {
                ShowError(_localization.GetString("FactorialRangeError"));
                return;
            }
            double result = 1;
            for (int i = 2; i <= (int)value; i++) result *= i;
            Display = FormatResult(result);
            _isNewCalculation = true;
        }
    }

    #endregion

    #region Wissenschaftliche Funktionen

    [RelayCommand]
    private void Sin()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            var angle = IsRadians ? value : value * Math.PI / 180;
            Display = FormatResult(Math.Sin(angle));
            _isNewCalculation = true;
        }
    }

    [RelayCommand]
    private void Cos()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            var angle = IsRadians ? value : value * Math.PI / 180;
            Display = FormatResult(Math.Cos(angle));
            _isNewCalculation = true;
        }
    }

    [RelayCommand]
    private void Tan()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            var angle = IsRadians ? value : value * Math.PI / 180;
            var result = Math.Tan(angle);
            if (Math.Abs(result) > 1e15)
            {
                ShowError(_localization.GetString("Error"));
                return;
            }
            Display = FormatResult(result);
            _isNewCalculation = true;
        }
    }

    [RelayCommand]
    private void Log()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            if (value <= 0) { ShowError(_localization.GetString("Error")); return; }
            Display = FormatResult(Math.Log10(value));
            _isNewCalculation = true;
        }
    }

    [RelayCommand]
    private void Ln()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            if (value <= 0) { ShowError(_localization.GetString("Error")); return; }
            Display = FormatResult(Math.Log(value));
            _isNewCalculation = true;
        }
    }

    [RelayCommand]
    private void Pi()
    {
        Display = FormatResult(Math.PI);
        _isNewCalculation = true;
    }

    [RelayCommand]
    private void Euler()
    {
        Display = FormatResult(Math.E);
        _isNewCalculation = true;
    }

    [RelayCommand]
    private void Abs()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            Display = FormatResult(Math.Abs(value));
            _isNewCalculation = true;
        }
    }

    #endregion

    #region Memory

    [RelayCommand]
    private void MemoryClear() { Memory = 0; HasMemory = false; }

    [RelayCommand]
    private void MemoryRecall()
    {
        if (HasMemory) { Display = FormatResult(Memory); _isNewCalculation = true; }
    }

    [RelayCommand]
    private void MemoryAdd()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) { Memory += value; HasMemory = true; }
    }

    [RelayCommand]
    private void MemorySubtract()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) { Memory -= value; HasMemory = true; }
    }

    [RelayCommand]
    private void MemoryStore()
    {
        if (double.TryParse(Display, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) { Memory = value; HasMemory = true; }
    }

    #endregion

    [RelayCommand]
    private void ToggleAngleMode() => IsRadians = !IsRadians;

    [RelayCommand]
    private void SetMode(CalculatorMode mode) => CurrentMode = mode;

    private void ShowError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        // Expression leeren damit Altlasten nicht die nächste Berechnung kontaminieren
        Expression = "";
        _isNewCalculation = true;
    }
    private void ClearError() { HasError = false; ErrorMessage = ""; }

    /// <summary>Prüft ob ein Zeichen ein Rechenoperator ist.</summary>
    private static bool IsOperatorChar(char c) =>
        c is '+' or '-' or '\u2212' or '*' or '\u00D7' or '/' or '\u00F7' or '^';

    private string FormatResult(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return _localization.GetString("Error");
        return value.ToString("G15", CultureInfo.InvariantCulture);
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

public enum CalculatorMode
{
    Basic,
    Scientific
}
