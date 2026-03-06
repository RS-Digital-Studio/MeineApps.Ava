using CommunityToolkit.Mvvm.Input;
using MeineApps.CalcLib;

namespace RechnerPlus.ViewModels;

/// <summary>
/// Berechnungs-Logik: Eingabe, Operatoren, wissenschaftliche Funktionen, Klammern.
/// </summary>
public sealed partial class CalculatorViewModel
{
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
        if (Expression.Length + Display.Length >= MaxExpressionLength) return;
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
        if (Expression.Length + Display.Length >= MaxExpressionLength) return;
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
        if (Expression.Length + Display.Length >= MaxExpressionLength) return;
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
                !double.TryParse(newRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
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

    #endregion
}
