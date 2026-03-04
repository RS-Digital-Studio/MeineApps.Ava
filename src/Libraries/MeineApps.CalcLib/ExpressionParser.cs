using System.Globalization;

namespace MeineApps.CalcLib;

/// <summary>
/// Parser für mathematische Ausdrücke mit korrekter Operator-Präzedenz.
/// Verwendet Shunting Yard Algorithmus für Infix → Postfix Konvertierung.
/// </summary>
public sealed class ExpressionParser
{
    private readonly CalculatorEngine _engine;

    // Operator-Precedence: höhere Zahl = höhere Priorität
    private static readonly Dictionary<string, int> _precedence = new()
    {
        { "+", 1 },
        { "−", 1 },
        { "-", 1 },  // Minus alternative
        { "×", 2 },
        { "*", 2 },  // Multiply alternative
        { "÷", 2 },
        { "/", 2 },  // Divide alternative
        { "mod", 2 },
        { "^", 3 }   // Potenz (höchste Priorität)
    };

    public ExpressionParser(CalculatorEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Berechnet einen mathematischen Ausdruck mit korrekter Operator-Präzedenz.
    /// Beispiele: "2+3×4" = 14, "(2+3)×4" = 20, "2^3+4" = 12
    /// </summary>
    public CalculationResult Evaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return CalculationResult.Success(0);
        }

        try
        {
            // 1. Tokenize (zerteile in Zahlen, Operatoren, Klammern)
            var tokens = Tokenize(expression);

            // 2. Infix zu Postfix (Shunting Yard)
            var postfix = InfixToPostfix(tokens);

            // 3. Postfix auswerten
            return EvaluatePostfix(postfix);
        }
        catch (Exception ex)
        {
            return CalculationResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// Zerlegt Expression in Tokens: Zahlen, Operatoren, Klammern
    /// "2+3×4" → ["2", "+", "3", "×", "4"]
    /// </summary>
    private List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
        var currentNumber = "";
        var i = 0;

        while (i < expression.Length)
        {
            var c = expression[i];

            // Whitespace ignorieren
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Ziffer oder Dezimalpunkt → Teil der Zahl
            if (char.IsDigit(c) || c == '.' || c == ',')
            {
                currentNumber += c == ',' ? '.' : c;
                i++;
                continue;
            }

            // Scientific notation (E/e) nach Ziffern → Teil der Zahl (z.B. "1E-05", "1.5E+10")
            if ((c == 'E' || c == 'e') && currentNumber.Length > 0)
            {
                currentNumber += c;
                i++;
                if (i < expression.Length && (expression[i] == '+' || expression[i] == '-'))
                {
                    currentNumber += expression[i];
                    i++;
                }
                continue;
            }

            // Wenn Zahl fertig, zu Tokens hinzufügen
            if (currentNumber.Length > 0)
            {
                tokens.Add(currentNumber);
                currentNumber = "";
            }

            // Operator oder Klammer
            if (c is '+' or '−' or '-' or '×' or '*' or '÷' or '/' or '^' or '(' or ')')
            {
                tokens.Add(c.ToString());
                i++;
            }
            // "mod" als 3-Zeichen Operator
            else if (i + 2 < expression.Length && expression.Substring(i, 3).ToLower() == "mod")
            {
                tokens.Add("mod");
                i += 3;
            }
            else
            {
                // Unbekanntes Zeichen → Fehler
                throw new InvalidOperationException($"Invalid character: {c}");
            }
        }

        // Letzte Zahl hinzufügen
        if (currentNumber.Length > 0)
        {
            tokens.Add(currentNumber);
        }

        // Unäre Minus-Zeichen verarbeiten
        // Unäres Minus ist: am Anfang, nach "(", oder nach Operator
        tokens = ProcessUnaryMinus(tokens);

        // Implizite Multiplikation einfügen:
        // ")(", "Zahl(", ")Zahl" → "× " dazwischen
        tokens = InsertImplicitMultiplication(tokens);

        // Validierung: Zwei Operatoren hintereinander nicht erlaubt (außer nach unärem Minus-Handling)
        for (int j = 0; j < tokens.Count - 1; j++)
        {
            if (IsOperator(tokens[j]) && IsOperator(tokens[j + 1]))
            {
                throw new InvalidOperationException($"Invalid expression: consecutive operators '{tokens[j]}' and '{tokens[j + 1]}'");
            }
        }

        // Validierung: Expression darf nicht mit Operator enden
        if (tokens.Count > 0 && IsOperator(tokens[^1]))
        {
            throw new InvalidOperationException("Expression cannot end with an operator");
        }

        // Validierung: Expression darf nicht mit binärem Operator starten
        if (tokens.Count > 0 && IsOperator(tokens[0]))
        {
            throw new InvalidOperationException($"Expression cannot start with operator '{tokens[0]}'");
        }

        return tokens;
    }

    /// <summary>
    /// Verarbeitet unäre Minus-Zeichen.
    /// Konsekutive Minus werden zusammengefasst: --5 → 5, ---5 → -5
    /// Wenn gefolgt von einer Zahl: zusammenfügen ("-" + "5" → "-5")
    /// Sonst: Fallback auf "0 - x" (z.B. vor Klammern)
    /// </summary>
    private List<string> ProcessUnaryMinus(List<string> tokens)
    {
        var result = new List<string>();

        for (int i = 0; i < tokens.Count; i++)
        {
            // Minus am Anfang, nach "(", oder nach Operator → unäres Minus
            if ((tokens[i] == "-" || tokens[i] == "−") &&
                (i == 0 || tokens[i - 1] == "(" || IsOperator(tokens[i - 1])))
            {
                // Konsekutive Minus-Zeichen zusammenfassen (--5 → 5, ---5 → -5)
                int minusCount = 1;
                while (i + 1 < tokens.Count && (tokens[i + 1] == "-" || tokens[i + 1] == "−"))
                {
                    minusCount++;
                    i++;
                }

                bool isNegative = minusCount % 2 == 1;

                if (i + 1 < tokens.Count && IsNumber(tokens[i + 1]))
                {
                    // Unäres Minus + Zahl → negative/positive Zahl
                    result.Add(isNegative ? "-" + tokens[i + 1] : tokens[i + 1]);
                    i++;
                }
                else if (isNegative)
                {
                    // Fallback für Fälle wie -(expr)
                    result.Add("0");
                    result.Add("-");
                }
                // Gerade Anzahl ohne nachfolgende Zahl → hebt sich auf
            }
            else
            {
                result.Add(tokens[i]);
            }
        }

        return result;
    }

    /// <summary>
    /// Fügt implizite Multiplikation ein:
    /// ")(", "Zahl(", ")Zahl" → "×" dazwischen
    /// z.B. (5+3)(2+1) → (5+3)×(2+1), 5(3+2) → 5×(3+2)
    /// </summary>
    private static List<string> InsertImplicitMultiplication(List<string> tokens)
    {
        var result = new List<string>(tokens.Count + 4);

        for (int i = 0; i < tokens.Count; i++)
        {
            result.Add(tokens[i]);

            if (i + 1 >= tokens.Count) continue;

            var current = tokens[i];
            var next = tokens[i + 1];

            // ")" gefolgt von "(" → implizites ×
            // ")" gefolgt von Zahl → implizites ×
            // Zahl gefolgt von "(" → implizites ×
            bool needsMultiply =
                (current == ")" && next == "(") ||
                (current == ")" && IsNumber(next)) ||
                (IsNumber(current) && next == "(");

            if (needsMultiply)
                result.Add("×");
        }

        return result;
    }

    /// <summary>
    /// Shunting Yard Algorithmus: Infix → Postfix Notation
    /// Infix: "2+3×4" → Postfix: "2 3 4 × +"
    /// </summary>
    private List<string> InfixToPostfix(List<string> tokens)
    {
        var output = new List<string>();
        var operatorStack = new Stack<string>();

        foreach (var token in tokens)
        {
            // Zahl → direkt zu Output
            if (IsNumber(token))
            {
                output.Add(token);
            }
            // Operator
            else if (IsOperator(token))
            {
                // Pop Operatoren mit höherer Precedence
                // Potenz (^) ist rechtsassoziativ: nur strikt höhere Precedence poppen
                // Andere Operatoren sind linksassoziativ: höhere oder gleiche Precedence poppen
                while (operatorStack.Count > 0 &&
                       IsOperator(operatorStack.Peek()) &&
                       (GetPrecedence(operatorStack.Peek()) > GetPrecedence(token) ||
                        (GetPrecedence(operatorStack.Peek()) == GetPrecedence(token) && token != "^")))
                {
                    output.Add(operatorStack.Pop());
                }
                operatorStack.Push(token);
            }
            // Öffnende Klammer
            else if (token == "(")
            {
                operatorStack.Push(token);
            }
            // Schließende Klammer
            else if (token == ")")
            {
                // Pop bis öffnende Klammer gefunden
                while (operatorStack.Count > 0 && operatorStack.Peek() != "(")
                {
                    output.Add(operatorStack.Pop());
                }

                if (operatorStack.Count == 0)
                {
                    throw new InvalidOperationException("Mismatched parentheses");
                }

                operatorStack.Pop(); // Öffnende Klammer entfernen
            }
        }

        // Restliche Operatoren zu Output
        while (operatorStack.Count > 0)
        {
            var op = operatorStack.Pop();
            if (op == "(" || op == ")")
            {
                throw new InvalidOperationException("Mismatched parentheses");
            }
            output.Add(op);
        }

        return output;
    }

    /// <summary>
    /// Wertet Postfix-Ausdruck aus
    /// Postfix: "2 3 4 × +" → Ergebnis: 14
    /// </summary>
    private CalculationResult EvaluatePostfix(List<string> postfix)
    {
        var stack = new Stack<double>();

        foreach (var token in postfix)
        {
            if (IsNumber(token))
            {
                // Zahl → auf Stack
                if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
                {
                    // Zu große Zahlen als Fehler behandeln (z.B. 1e309 = Infinity)
                    if (double.IsInfinity(num))
                        return CalculationResult.Error("Number too large");
                    stack.Push(num);
                }
                else
                {
                    return CalculationResult.Error($"Invalid number: {token}");
                }
            }
            else if (IsOperator(token))
            {
                // Operator → 2 Operanden vom Stack holen
                if (stack.Count < 2)
                {
                    return CalculationResult.Error("Invalid expression");
                }

                var b = stack.Pop();
                var a = stack.Pop();

                // Berechnung mit CalculatorEngine
                var result = ApplyOperator(a, b, token);
                if (result.IsError)
                {
                    return result;
                }

                stack.Push(result.Value);
            }
            else
            {
                // Unbekanntes Token → Fehler
                return CalculationResult.Error($"Unknown token: {token}");
            }
        }

        // Ergebnis sollte genau 1 Wert auf dem Stack sein
        if (stack.Count != 1)
        {
            return CalculationResult.Error("Invalid expression");
        }

        return CalculationResult.Success(stack.Pop());
    }

    /// <summary>
    /// Führt Operation mit CalculatorEngine aus
    /// </summary>
    private CalculationResult ApplyOperator(double a, double b, string op)
    {
        return op switch
        {
            "+" => CalculationResult.Success(_engine.Add(a, b)),
            "−" or "-" => CalculationResult.Success(_engine.Subtract(a, b)),
            "×" or "*" => CalculationResult.Success(_engine.Multiply(a, b)),
            "÷" or "/" => _engine.Divide(a, b),
            "^" => _engine.Power(a, b),
            "mod" => _engine.Mod(a, b),
            _ => CalculationResult.Error($"Unknown operator: {op}")
        };
    }

    private static bool IsNumber(string token)
    {
        return double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsOperator(string token)
    {
        return _precedence.ContainsKey(token);
    }

    private static int GetPrecedence(string op)
    {
        return _precedence.TryGetValue(op, out var prec) ? prec : 0;
    }
}
