namespace AppChecker;

/// <summary>
/// Interface fuer alle Checker-Klassen.
/// Check() wird pro App aufgerufen, CheckGlobal() einmal fuer die gesamte Solution.
/// </summary>
interface IChecker
{
    /// <summary>Kategorie-Name fuer die Ausgabe</summary>
    string Category { get; }

    /// <summary>Prueft eine einzelne App</summary>
    List<CheckResult> Check(CheckContext ctx);

    /// <summary>Globale Pruefungen (z.B. Directory.Build.targets). Default: leer.</summary>
    List<CheckResult> CheckGlobal(string solutionRoot) => [];
}
