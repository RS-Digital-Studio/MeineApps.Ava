namespace HandwerkerImperium.Services;

using HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Einfacher Logging-Service. Schreibt auf Debug-Output.
/// Kann später um Firebase Crashlytics oder File-Logging erweitert werden.
/// </summary>
public sealed class LogService : ILogService
{
    public void Info(string message) => System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
    public void Warn(string message) => System.Diagnostics.Debug.WriteLine($"[WARN] {message}");

    public void Error(string message, Exception? ex = null) =>
        System.Diagnostics.Debug.WriteLine(ex != null
            ? $"[ERROR] {message}: {ex.Message}"
            : $"[ERROR] {message}");
}
