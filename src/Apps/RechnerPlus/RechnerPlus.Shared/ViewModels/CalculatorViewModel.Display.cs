using System.Globalization;
using MeineApps.CalcLib;

namespace RechnerPlus.ViewModels;

/// <summary>
/// Display-Formatierung, Live-Preview, Fehlerbehandlung, Clipboard, Funktionsgraph.
/// </summary>
public sealed partial class CalculatorViewModel
{
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

    #region Funktionsgraph

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

    #endregion

    #region Clipboard & Share

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

    #endregion
}
