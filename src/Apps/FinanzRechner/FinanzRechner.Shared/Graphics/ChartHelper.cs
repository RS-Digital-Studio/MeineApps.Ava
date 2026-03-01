namespace FinanzRechner.Graphics;

/// <summary>
/// Gemeinsame Hilfsmethoden für Chart-Visualisierungen (Y-Achsen-Skalierung, Label-Formatierung).
/// Vermeidet Code-Duplikation zwischen StackedAreaVisualization und AmortizationBarVisualization.
/// </summary>
public static class ChartHelper
{
    /// <summary>
    /// Berechnet den optimalen Grid-Schritt für die Y-Achse.
    /// </summary>
    public static float CalculateGridStep(float maxVal)
    {
        if (maxVal <= 500) return 100f;
        if (maxVal <= 1000) return 200f;
        if (maxVal <= 2000) return 500f;
        if (maxVal <= 5000) return 1000f;
        if (maxVal <= 10000) return 2000f;
        if (maxVal <= 50000) return 10000f;
        if (maxVal <= 100000) return 20000f;
        if (maxVal <= 500000) return 100000f;
        if (maxVal <= 1000000) return 200000f;
        return maxVal / 5f;
    }

    /// <summary>
    /// Formatiert Y-Achsen-Labels kompakt (1000 -> 1k, 1000000 -> 1M).
    /// </summary>
    public static string FormatYLabel(float value)
    {
        if (value >= 1_000_000) return $"{value / 1_000_000:F1}M";
        if (value >= 1_000) return $"{value / 1_000:F0}k";
        return $"{value:F0}";
    }
}
