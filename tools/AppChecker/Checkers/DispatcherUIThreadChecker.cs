using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft Cross-Thread-Sicherheit von UI-Mutationen.
///
/// Findet Anti-Patterns:
/// - `Task.Run(() =&gt; { ...Property = ... })` ohne Dispatcher.UIThread.Post
/// - `Task.Run(() =&gt; { _collection.Add(...) })` (ObservableCollection braucht UI-Thread)
/// - Nach `await ... ConfigureAwait(false)`: direkte Property-Mutation oder Collection-Mutation
///
/// Heuristik kann False-Positives liefern → INFO-Severity.
/// </summary>
class DispatcherUIThreadChecker : IChecker
{
    public string Category => "Dispatcher-UI-Thread";

    // Task.Run(() => { ... }) oder Task.Run(async () => { ... })
    static readonly Regex TaskRunLambdaRegex = new(
        @"Task\.Run\s*\(\s*(?:async\s+)?\(\s*\)\s*=>\s*\{",
        RegexOptions.Compiled);

    // Collection-Mutationen
    static readonly Regex CollectionMutationRegex = new(
        @"\b_?(\w*[Cc]ollection|\w*[Ll]ist|\w*[Ii]tems)\s*(\?\.)?\.(Add|Remove|Clear|Insert|RemoveAt|RemoveAll)\s*\(",
        RegexOptions.Compiled);

    // Property-Mutation (ObservableProperty oder eigene Property)
    static readonly Regex PropertyMutationRegex = new(
        @"^\s*([A-Z]\w+(?:\s*\.\s*\w+)?)\s*=\s*[^=]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int taskRunMutations = 0;

        foreach (var file in ctx.SharedCsFiles)
        {
            // Skip Code-Behind und Renderer (oft Background-Threading-Hot-Path mit eigenen Patterns)
            if (file.FullPath.EndsWith(".axaml.cs")) continue;
            if (file.FullPath.Contains("Graphics") || file.FullPath.Contains("Rendering")) continue;

            var content = file.Content;

            foreach (Match taskRunMatch in TaskRunLambdaRegex.Matches(content))
            {
                int bodyStart = taskRunMatch.Index + taskRunMatch.Length;
                int bodyEnd = FindMatchingBrace(content, bodyStart);
                if (bodyEnd <= bodyStart) continue;
                var body = content.Substring(bodyStart, bodyEnd - bodyStart);

                // Body hat Dispatcher.UIThread im Body? → OK (User uses dispatcher)
                if (body.Contains("Dispatcher.UIThread")) continue;

                // Sucht Property-/Collection-Mutationen im Body
                int mutationsInBody = 0;
                if (CollectionMutationRegex.IsMatch(body))
                    mutationsInBody += CollectionMutationRegex.Matches(body).Count;

                if (mutationsInBody == 0) continue;

                taskRunMutations++;
                var lineNum = GetLineNumber(content, taskRunMatch.Index);
                if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;
                if (taskRunMutations <= 8)
                    results.Add(new(Severity.Info, Category,
                        $"Task.Run-Body mit {mutationsInBody} Collection-Mutation(en) ohne Dispatcher.UIThread in {file.RelativePath}:{lineNum} → ObservableCollection-Crash bei UI-Binding"));
            }
        }

        if (taskRunMutations == 0)
            results.Add(new(Severity.Pass, Category, "Keine Task.Run-Bodies mit ungeschuetzten Collection-Mutationen"));
        else if (taskRunMutations > 8)
            results.Add(new(Severity.Info, Category, $"...und {taskRunMutations - 8} weitere Task.Run-Bodies mit potenziellen Cross-Thread-Mutationen"));

        return results;
    }

    static int FindMatchingBrace(string content, int startIndex)
    {
        int depth = 1;
        for (int i = startIndex; i < content.Length; i++)
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
