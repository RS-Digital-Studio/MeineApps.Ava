namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentraler Logging-Service. Ersetzt Debug.WriteLine und bare catches.
/// </summary>
public interface ILogService
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}
