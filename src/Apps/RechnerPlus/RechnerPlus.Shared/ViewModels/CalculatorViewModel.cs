using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.CalcLib;
using MeineApps.Core.Ava.Localization;

namespace RechnerPlus.ViewModels;

public partial class CalculatorViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly CalculatorEngine _engine;
    private readonly ExpressionParser _parser;
    private readonly ILocalizationService _localization;
    private readonly IHistoryService _historyService;

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

    public CalculatorViewModel(CalculatorEngine engine, ExpressionParser parser,
                                ILocalizationService localization, IHistoryService historyService)
    {
        _engine = engine;
        _parser = parser;
        _localization = localization;
        _historyService = historyService;
        _localization.LanguageChanged += OnLanguageChanged;
        _historyService.HistoryChanged += OnHistoryChanged;
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
    }

    [RelayCommand]
    private void ShowHistory() => IsHistoryVisible = true;

    [RelayCommand]
    private void HideHistory() => IsHistoryVisible = false;

    [RelayCommand]
    private void ClearHistory()
    {
        _historyService.Clear();
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

    [RelayCommand]
    private void InputDigit(string digit)
    {
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

    [RelayCommand]
    private void Calculate()
    {
        if (HasError) return;

        try
        {
            var fullExpression = Expression + Display;
            var result = _parser.Evaluate(fullExpression);

            if (!result.IsError)
            {
                var formattedResult = FormatResult(result.Value);
                _historyService.AddEntry(fullExpression, formattedResult, result.Value);
                Display = formattedResult;
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

    // Scientific functions
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

    // Memory functions
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

    [RelayCommand]
    private void ToggleAngleMode() => IsRadians = !IsRadians;

    [RelayCommand]
    private void SetMode(CalculatorMode mode) => CurrentMode = mode;

    private void ShowError(string message) { HasError = true; ErrorMessage = message; }
    private void ClearError() { HasError = false; ErrorMessage = ""; }

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
