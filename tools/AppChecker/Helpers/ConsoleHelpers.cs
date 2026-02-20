namespace AppChecker.Helpers;

static class ConsoleHelpers
{
    /// <summary>Farbige Konsolenausgabe (mit optionalem Newline)</summary>
    public static void WriteColor(string text, ConsoleColor color, bool newLine = false)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        if (newLine)
            Console.WriteLine(text);
        else
            Console.Write(text);
        Console.ForegroundColor = prev;
    }

    /// <summary>Gibt ein CheckResult formatiert aus</summary>
    public static void PrintResult(CheckResult r)
    {
        var (color, label) = r.Severity switch
        {
            Severity.Pass => (ConsoleColor.Green, "PASS"),
            Severity.Info => (ConsoleColor.Cyan, "INFO"),
            Severity.Warn => (ConsoleColor.Yellow, "WARN"),
            Severity.Fail => (ConsoleColor.Red, "FAIL"),
            _ => (ConsoleColor.White, "????")
        };
        Console.Write("    [");
        WriteColor(label, color);
        Console.WriteLine($"] {r.Message}");
    }

    /// <summary>Gibt Kategorie-Header und alle Ergebnisse aus</summary>
    public static void PrintCategory(string category, IEnumerable<CheckResult> results)
    {
        WriteColor($"  [{category}]", ConsoleColor.Gray, newLine: true);
        foreach (var r in results)
            PrintResult(r);
    }
}
