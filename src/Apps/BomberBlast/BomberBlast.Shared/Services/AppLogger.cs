using System.Diagnostics;

namespace BomberBlast.Services;

/// <summary>
/// Einfache Logging-Implementierung mit Level-Prefix
/// </summary>
public class AppLogger : IAppLogger
{
    private const string Tag = "BomberBlast";

    public void LogInfo(string message)
        => Trace.WriteLine($"[{Tag}] INFO: {message}");

    public void LogWarning(string message)
        => Trace.WriteLine($"[{Tag}] WARN: {message}");

    public void LogError(string message)
        => Trace.WriteLine($"[{Tag}] ERROR: {message}");

    public void LogError(string message, Exception ex)
        => Trace.WriteLine($"[{Tag}] ERROR: {message} - {ex.Message}");
}
