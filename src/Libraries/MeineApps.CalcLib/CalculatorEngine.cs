namespace MeineApps.CalcLib;

/// <summary>
/// Kern-Berechnungslogik des Taschenrechners.
/// Stateless und vollständig testbar.
/// </summary>
public sealed class CalculatorEngine
{
    private const double EPSILON = 1e-15;

    #region Grundrechenarten (Basic Mode)

    public double Add(double a, double b) => a + b;

    public double Subtract(double a, double b) => a - b;

    public double Multiply(double a, double b) => a * b;

    public CalculationResult Divide(double a, double b)
    {
        if (Math.Abs(b) < EPSILON)
        {
            return CalculationResult.Error("Division by zero");
        }
        return CalculationResult.Success(a / b);
    }

    public double Negate(double value) => -value;

    #endregion

    #region Erweiterte Funktionen (Extended Mode)

    public double Percentage(double value, double percent) => value * percent / 100;

    public CalculationResult SquareRoot(double value)
    {
        if (value < 0)
        {
            return CalculationResult.Error("Square root of negative number");
        }
        return CalculationResult.Success(Math.Sqrt(value));
    }

    public double Square(double value) => value * value;

    public CalculationResult Reciprocal(double value)
    {
        if (Math.Abs(value) < EPSILON)
        {
            return CalculationResult.Error("Division by zero");
        }
        return CalculationResult.Success(1 / value);
    }

    #endregion

    #region Wissenschaftliche Funktionen (Scientific Mode)

    public double Sin(double radians) => Math.Sin(radians);

    public double Cos(double radians) => Math.Cos(radians);

    public CalculationResult Tan(double radians)
    {
        var cos = Math.Cos(radians);
        if (Math.Abs(cos) < EPSILON)
        {
            return CalculationResult.Error("Tangent undefined");
        }
        return CalculationResult.Success(Math.Tan(radians));
    }

    public CalculationResult Log(double value)
    {
        if (value <= 0)
        {
            return CalculationResult.Error("Logarithm only for positive numbers");
        }
        return CalculationResult.Success(Math.Log10(value));
    }

    public CalculationResult Ln(double value)
    {
        if (value <= 0)
        {
            return CalculationResult.Error("Logarithm only for positive numbers");
        }
        return CalculationResult.Success(Math.Log(value));
    }

    public CalculationResult Power(double baseNum, double exponent)
    {
        var result = Math.Pow(baseNum, exponent);
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            return CalculationResult.Error("Invalid result");
        }
        return CalculationResult.Success(result);
    }

    public double Pi => Math.PI;

    public double E => Math.E;

    public CalculationResult Factorial(int n)
    {
        if (n < 0) return CalculationResult.Error("Factorial of negative number");
        if (n <= 1) return CalculationResult.Success(1);

        double result = 1;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
            if (double.IsInfinity(result))
                return CalculationResult.Error("Overflow");
        }
        return CalculationResult.Success(result);
    }

    #endregion

    #region Inverse Trigonometrische Funktionen

    /// <summary>
    /// Arcussinus (sin^-1) - Gibt Radiant zurück
    /// </summary>
    public CalculationResult Asin(double value)
    {
        if (value < -1 || value > 1)
        {
            return CalculationResult.Error("Value must be between -1 and 1");
        }
        return CalculationResult.Success(Math.Asin(value));
    }

    /// <summary>
    /// Arcuscosinus (cos^-1) - Gibt Radiant zurück
    /// </summary>
    public CalculationResult Acos(double value)
    {
        if (value < -1 || value > 1)
        {
            return CalculationResult.Error("Value must be between -1 and 1");
        }
        return CalculationResult.Success(Math.Acos(value));
    }

    /// <summary>
    /// Arcustangens (tan^-1) - Gibt Radiant zurück
    /// </summary>
    public double Atan(double value) => Math.Atan(value);

    #endregion

    #region Hyperbolische Funktionen

    public double Sinh(double value) => Math.Sinh(value);

    public double Cosh(double value) => Math.Cosh(value);

    public CalculationResult Tanh(double value)
    {
        var result = Math.Tanh(value);
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            return CalculationResult.Error("Invalid result");
        }
        return CalculationResult.Success(result);
    }

    #endregion

    #region Exponentialfunktionen

    /// <summary>
    /// e hoch x (e^x)
    /// </summary>
    public CalculationResult Exp(double value)
    {
        var result = Math.Exp(value);
        if (double.IsInfinity(result))
        {
            return CalculationResult.Error("Overflow");
        }
        return CalculationResult.Success(result);
    }

    /// <summary>
    /// 10 hoch x (10^x)
    /// </summary>
    public CalculationResult Exp10(double value)
    {
        var result = Math.Pow(10, value);
        if (double.IsInfinity(result))
        {
            return CalculationResult.Error("Overflow");
        }
        return CalculationResult.Success(result);
    }

    #endregion

    #region Erweiterte Potenzen und Wurzeln

    /// <summary>
    /// x hoch 3 (x^3)
    /// </summary>
    public double Cube(double value) => value * value * value;

    /// <summary>
    /// Kubikwurzel (3rd root of x)
    /// </summary>
    public double CubeRoot(double value) => Math.Cbrt(value);

    /// <summary>
    /// n-te Wurzel (nth root of x)
    /// </summary>
    public CalculationResult NthRoot(double value, double n)
    {
        if (Math.Abs(n) < EPSILON)
        {
            return CalculationResult.Error("Root exponent cannot be zero");
        }

        if (value < 0 && n % 2 == 0)
        {
            return CalculationResult.Error("Even root of negative number");
        }

        var result = value < 0
            ? -Math.Pow(-value, 1 / n)
            : Math.Pow(value, 1 / n);

        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            return CalculationResult.Error("Invalid result");
        }

        return CalculationResult.Success(result);
    }

    #endregion

    #region Weitere Funktionen

    /// <summary>
    /// Absolutwert (|x|)
    /// </summary>
    public double Abs(double value) => Math.Abs(value);

    /// <summary>
    /// Modulo (Rest der Division)
    /// </summary>
    public CalculationResult Mod(double a, double b)
    {
        if (Math.Abs(b) < EPSILON)
        {
            return CalculationResult.Error("Division by zero");
        }
        return CalculationResult.Success(a % b);
    }

    #endregion

    #region Hilfsmethoden

    public double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    public double RadiansToDegrees(double radians) => radians * 180 / Math.PI;

    #endregion
}
