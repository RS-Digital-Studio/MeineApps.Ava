namespace BomberBlast.Services;

/// <summary>
/// Strukturiertes Logging-Interface (ersetzt Debug.WriteLine)
/// </summary>
public interface IAppLogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(string message, Exception ex);
}
