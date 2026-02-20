namespace AppChecker;

/// <summary>Schweregrad eines Check-Ergebnisses</summary>
enum Severity { Pass, Info, Warn, Fail }

/// <summary>Einzelnes Check-Ergebnis</summary>
record CheckResult(Severity Severity, string Category, string Message);

/// <summary>App-Definition mit Package-ID und Feature-Flags</summary>
record AppDef(string Name, string ExpectedAppId, bool IsAdSupported);

/// <summary>Gecachte C#-Datei mit Zeilen und Gesamtinhalt</summary>
record CsFile(string FullPath, string RelativePath, string[] Lines, string Content);

/// <summary>Gecachte AXAML-Datei mit Gesamtinhalt</summary>
record AxamlFile(string FullPath, string RelativePath, string Content);

/// <summary>
/// Kontext fuer alle Checker - enthält gecachte Dateien und Pfade.
/// Dateien werden einmal pro App geladen und an alle Checker durchgereicht.
/// </summary>
class CheckContext
{
    public required AppDef App { get; init; }
    public required string SharedDir { get; init; }
    public required string AndroidDir { get; init; }
    public required string DesktopDir { get; init; }
    public required string SolutionRoot { get; init; }

    /// <summary>Alle .cs Dateien (Shared + Android + Desktop, ohne obj/bin)</summary>
    public required List<CsFile> CsFiles { get; init; }

    /// <summary>Nur Shared .cs Dateien</summary>
    public required List<CsFile> SharedCsFiles { get; init; }

    /// <summary>Nur Android .cs Dateien</summary>
    public required List<CsFile> AndroidCsFiles { get; init; }

    /// <summary>Alle .axaml Dateien (Shared, ohne obj/bin)</summary>
    public required List<AxamlFile> AxamlFiles { get; init; }

    /// <summary>App-Basisverzeichnis (Parent von Shared/Android/Desktop)</summary>
    public string AppBaseDir => Path.GetDirectoryName(SharedDir)!;
}
