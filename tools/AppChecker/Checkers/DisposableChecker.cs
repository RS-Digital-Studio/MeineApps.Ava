using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft Disposable-Pattern in Klassen:
/// - Klasse hat IDisposable-Felder aber keine Dispose-Methode â†’ WARN (Memory-Leak)
/// - Klasse hat Dispose-Methode aber ein IDisposable-Feld wird nicht disposed â†’ INFO
///
/// Bekannte IDisposable-Typen werden hardgecoded geprueft (HttpClient, SemaphoreSlim,
/// CancellationTokenSource, Timer, Stream, SKPaint, SKMaskFilter, ...).
/// </summary>
class DisposableChecker : IChecker
{
    public string Category => "Disposable";

    // Typen die IDisposable sind und in der Codebase vorkommen koennen
    static readonly string[] DisposableTypes =
    [
        // DispatcherTimer fehlt bewusst: Avalonias DispatcherTimer ist NICHT IDisposable (Start/Stop-Lifecycle)
        "SemaphoreSlim", "CancellationTokenSource", "Timer",
        "HttpClient", "FileStream", "MemoryStream", "StreamReader", "StreamWriter",
        "SKPaint", "SKMaskFilter", "SKShader", "SKImage", "SKSurface", "SKBitmap",
        "SKColorFilter", "SKPathEffect", "SKRuntimeEffect", "SKTypeface",
        "ManualResetEvent", "ManualResetEventSlim", "AutoResetEvent",
        "Mutex", "Semaphore", "ReaderWriterLockSlim",
        "MediaPlayer", "BluetoothGatt", "ClientWebSocket"
    ];

    static readonly Regex ClassDeclRegex = new(
        @"^\s*(?:public|internal|sealed|partial|abstract|static|\s)+\s*class\s+(\w+)\b([^{]*)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int classesWithFieldsNoDispose = 0;
        int classesWithMissingFieldDispose = 0;

        // Regex fuer Felder mit bekannten Disposable-Typen
        // Beispiel: private SemaphoreSlim _lock = new(1, 1);
        // Beispiel: private readonly CancellationTokenSource _cts = new();
        var disposableFieldPattern = new Regex(
            @"^\s*(?:private|protected|internal|public)\s+(?:readonly\s+|static\s+)*(?<type>" +
            string.Join("|", DisposableTypes) +
            @")(?:<[\w?,\s]+>)?\s*\??\s+(?<name>_?\w+)\s*[=;]",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Wir gruppieren Felder pro Datei UND pro Klasse
        foreach (var file in ctx.SharedCsFiles)
        {
            var content = file.Content;

            // Felder finden
            var fieldMatches = disposableFieldPattern.Matches(content);
            if (fieldMatches.Count == 0) continue;

            // Klassen-Deklarationen in Reihenfolge sammeln (Char-Offset)
            var classMatches = ClassDeclRegex.Matches(content)
                .Cast<Match>()
                .Where(m => !content.Substring(Math.Max(0, m.Index - 30), Math.Min(30, m.Index)).Contains("//"))
                .ToList();

            if (classMatches.Count == 0) continue;

            // Pro Klasse: Felder zuordnen + Dispose-Pruefung
            for (int c = 0; c < classMatches.Count; c++)
            {
                var classMatch = classMatches[c];
                // static classes haben keinen Instanz-Lifetime â†’ kein Instanz-Dispose moeglich/noetig.
                // (z.B. ProjectThumbnailRenderer: static Paint-Cache fuer die Prozess-Lebenszeit.)
                if (Regex.IsMatch(classMatch.Value, @"\bstatic\b")) continue;
                int classStart = FindClassBodyStart(content, classMatch);
                int classEnd = (c + 1 < classMatches.Count) ? classMatches[c + 1].Index : content.Length;
                if (classStart < 0 || classStart >= classEnd) continue;

                int actualClassEnd = FindClassBodyEnd(content, classStart);
                if (actualClassEnd > 0) classEnd = Math.Min(classEnd, actualClassEnd);

                var classBody = content.Substring(classStart, classEnd - classStart);

                // Felder in dieser Klasse
                var fieldsInClass = fieldMatches.Cast<Match>()
                    .Where(m => m.Index >= classStart && m.Index < classEnd)
                    .Select(m => new { Type = m.Groups["type"].Value, Name = m.Groups["name"].Value, LineNum = GetLineNumber(content, m.Index) })
                    .ToList();

                if (fieldsInClass.Count == 0) continue;

                var className = classMatch.Groups[1].Value;
                var classInheritance = classMatch.Groups[2].Value;

                // Suppressed check pro Field
                fieldsInClass = fieldsInClass
                    .Where(f => !FileHelpers.IsSuppressed(file.Lines, f.LineNum - 1))
                    .ToList();

                if (fieldsInClass.Count == 0) continue;

                // Hat die Klasse eine Dispose-Methode?
                bool implementsIDisposable = classInheritance.Contains("IDisposable")
                                           || classInheritance.Contains("IAsyncDisposable");
                bool hasDisposeMethod = Regex.IsMatch(classBody,
                    @"(public|protected)\s+(virtual\s+|override\s+|async\s+)?(void|Task|ValueTask)\s+(Dispose|DisposeAsync)\s*\(");

                if (!hasDisposeMethod)
                {
                    var fieldList = string.Join(", ", fieldsInClass.Take(3).Select(f => $"{f.Type} {f.Name}"));
                    var more = fieldsInClass.Count > 3 ? $" (+{fieldsInClass.Count - 3} mehr)" : "";

                    // Managed Lock-Primitive (SemaphoreSlim/ReaderWriterLockSlim/ManualResetEventSlim) allokieren
                    // kein OS-Handle (solange kein AvailableWaitHandle abgerufen wird) â†’ kein echter Leak, nur INFO.
                    if (fieldsInClass.All(f => IsManagedLockPrimitive(f.Type)))
                    {
                        results.Add(new(Severity.Info, Category,
                            $"{className} hat {fieldsInClass.Count} managed Lock-Primitive(e) ohne Dispose in {file.RelativePath} ({fieldList}{more}) â†’ unkritisch (kein OS-Handle), Dispose optional"));
                        continue;
                    }

                    classesWithFieldsNoDispose++;
                    var disposableHint = implementsIDisposable ? " (Klasse implementiert IDisposable aber keine Dispose-Methode)" : "";
                    results.Add(new(Severity.Warn, Category,
                        $"{className} hat {fieldsInClass.Count} IDisposable-Feld(er) aber keine Dispose-Methode in {file.RelativePath} â†’ Memory-Leak: {fieldList}{more}{disposableHint}"));
                    continue;
                }

                // Dispose existiert â†’ pruefen ob alle Felder darin disposed werden
                foreach (var field in fieldsInClass)
                {
                    // field.Dispose() oder field?.Dispose() im classBody?
                    var fieldDisposePattern = new Regex(
                        $@"\b{Regex.Escape(field.Name)}\s*\??\s*\.\s*(Dispose|DisposeAsync|Cancel)\s*\(",
                        RegexOptions.Compiled);
                    if (!fieldDisposePattern.IsMatch(classBody))
                    {
                        classesWithMissingFieldDispose++;
                        if (classesWithMissingFieldDispose <= 5)
                            results.Add(new(Severity.Info, Category,
                                $"{className}.{field.Name} ({field.Type}) wird in Dispose nicht freigegeben in {file.RelativePath}:{field.LineNum}"));
                    }
                }
            }
        }

        if (classesWithFieldsNoDispose == 0)
            results.Add(new(Severity.Pass, Category, "Alle Klassen mit IDisposable-Feldern haben Dispose-Methode"));
        if (classesWithMissingFieldDispose == 0)
            results.Add(new(Severity.Pass, Category, "Alle IDisposable-Felder werden in Dispose freigegeben"));

        return results;
    }

    /// <summary>Managed Lock-Primitive ohne OS-Handle â€” Dispose ist optional, kein echter Leak.</summary>
    static bool IsManagedLockPrimitive(string type) => type switch
    {
        "SemaphoreSlim" or "ReaderWriterLockSlim" or "ManualResetEventSlim" => true,
        _ => false
    };

    static int FindClassBodyStart(string content, Match classMatch)
    {
        int idx = content.IndexOf('{', classMatch.Index + classMatch.Length);
        return idx < 0 ? -1 : idx + 1;
    }

    static int FindClassBodyEnd(string content, int bodyStart)
    {
        int depth = 1;
        for (int i = bodyStart; i < content.Length; i++)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    static int GetLineNumber(string content, int offset)
    {
        if (offset < 0) offset = 0;
        if (offset > content.Length) offset = content.Length;
        int line = 1;
        for (int i = 0; i < offset; i++)
            if (content[i] == '\n') line++;
        return line;
    }
}
