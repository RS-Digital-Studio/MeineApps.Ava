using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft SQLite-Patterns: InsertAsync ID-Bug, SemaphoreSlim</summary>
class SqliteChecker : IChecker
{
    public string Category => "SQLite";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        int insertIdBugCount = 0;
        bool hasSqliteUsage = false;
        bool hasSemaphore = false;

        foreach (var file in ctx.SharedCsFiles)
        {
            if (file.Content.Contains("InsertAsync") || file.Content.Contains("SQLiteAsyncConnection"))
                hasSqliteUsage = true;

            if (file.Content.Contains("SemaphoreSlim"))
                hasSemaphore = true;

            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // entity.Id = await ...InsertAsync(entity) - GIBT ROWCOUNT ZURUECK, NICHT ID!
                if (Regex.IsMatch(trimmed, @"\.\w+\s*=\s*await\s+\w+\.InsertAsync\s*\("))
                {
                    insertIdBugCount++;
                    results.Add(new(Severity.Fail, Category, $"InsertAsync Rueckgabewert als ID verwendet in {file.RelativePath}:{i + 1} → gibt RowCount (1) zurueck, nicht die ID!"));
                }

                // var id = await ...InsertAsync(entity) - auch verdaechtig
                if (Regex.IsMatch(trimmed, @"var\s+\w*[Ii]d\s*=\s*await\s+\w+\.InsertAsync\s*\("))
                {
                    insertIdBugCount++;
                    results.Add(new(Severity.Fail, Category, $"InsertAsync Rueckgabewert in ID-Variable gespeichert in {file.RelativePath}:{i + 1} → gibt RowCount zurueck!"));
                }
            }
        }

        if (!hasSqliteUsage)
        {
            results.Add(new(Severity.Info, Category, "Keine SQLite-Nutzung in dieser App"));
            return results;
        }

        if (insertIdBugCount == 0)
            results.Add(new(Severity.Pass, Category, "Kein InsertAsync ID-Bug gefunden"));

        if (hasSemaphore)
            results.Add(new(Severity.Pass, Category, "SemaphoreSlim fuer Thread-Safety vorhanden"));
        else
            results.Add(new(Severity.Info, Category, "Kein SemaphoreSlim fuer DB-Operationen (evtl. nicht noetig)"));

        return results;
    }
}
