namespace HandwerkerImperium.Services;

using HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentraler Logging-Service mit In-Memory-Ringbuffer.
/// Schreibt auf Console (Release-sicher) UND Debug-Output.
/// GetRecentLogs() liefert die letzten 200 Einträge für In-App-Diagnose.
/// </summary>
public sealed class LogService : ILogService
{
    private readonly object _lock = new();
    private readonly string[] _buffer = new string[MaxEntries];
    private int _index;
    private int _count;

    private const int MaxEntries = 200;

    public void Info(string message) => Log("INFO", message);
    public void Warn(string message) => Log("WARN", message);

    public void Error(string message, Exception? ex = null) =>
        Log("ERROR", ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message);

    /// <summary>
    /// Gibt die letzten Log-Einträge zurück (neueste zuerst).
    /// </summary>
    public IReadOnlyList<string> GetRecentLogs()
    {
        lock (_lock)
        {
            var result = new string[_count];
            for (int i = 0; i < _count; i++)
            {
                // Vom neuesten zum ältesten
                int idx = (_index - 1 - i + MaxEntries) % MaxEntries;
                result[i] = _buffer[idx];
            }
            return result;
        }
    }

    private void Log(string level, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
        var entry = $"[{timestamp}] [{level}] {message}";

        // Console.WriteLine funktioniert in Release-Builds (Debug.WriteLine nicht)
        Console.WriteLine(entry);
        System.Diagnostics.Debug.WriteLine(entry);

        lock (_lock)
        {
            _buffer[_index] = entry;
            _index = (_index + 1) % MaxEntries;
            if (_count < MaxEntries) _count++;
        }
    }
}
