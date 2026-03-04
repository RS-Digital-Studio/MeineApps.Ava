namespace BomberBlast.Core;

/// <summary>
/// Manages the level countdown timer (200 seconds per level)
/// </summary>
public sealed class GameTimer
{
    /// <summary>Default time limit per level in seconds (original NES)</summary>
    public const int DEFAULT_TIME_LIMIT = 200;

    /// <summary>Warning threshold in seconds (plays warning sound)</summary>
    public const int WARNING_THRESHOLD = 30;

    private float _remainingTime;
    private bool _isRunning;
    private bool _warningTriggered;

    /// <summary>Remaining time in seconds</summary>
    public float RemainingTime => _remainingTime;

    /// <summary>Remaining time as integer for display</summary>
    public int RemainingSeconds => (int)Math.Ceiling(_remainingTime);

    /// <summary>Whether the timer is currently running</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Whether time has run out</summary>
    public bool IsExpired => _remainingTime <= 0;

    /// <summary>Whether we're in warning zone (under 30 seconds)</summary>
    public bool IsWarning => _remainingTime <= WARNING_THRESHOLD && _remainingTime > 0;

    /// <summary>Fires when timer enters warning zone</summary>
    public event Action? OnWarning;

    /// <summary>Fires when timer expires</summary>
    public event Action? OnExpired;

    /// <summary>
    /// Initialize timer with default time limit
    /// </summary>
    public void Reset()
    {
        Reset(DEFAULT_TIME_LIMIT);
    }

    /// <summary>
    /// Initialize timer with custom time limit
    /// </summary>
    public void Reset(int seconds)
    {
        _remainingTime = seconds;
        _isRunning = false;
        _warningTriggered = false;
    }

    /// <summary>
    /// Start the countdown
    /// </summary>
    public void Start()
    {
        _isRunning = true;
    }

    /// <summary>
    /// Pause the countdown
    /// </summary>
    public void Pause()
    {
        _isRunning = false;
    }

    /// <summary>
    /// Resume the countdown
    /// </summary>
    public void Resume()
    {
        _isRunning = true;
    }

    /// <summary>
    /// Update timer (call every frame)
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last frame in seconds</param>
    public void Update(float deltaTime)
    {
        if (!_isRunning || _remainingTime <= 0)
            return;

        _remainingTime -= deltaTime;

        // Check for warning threshold
        if (!_warningTriggered && _remainingTime <= WARNING_THRESHOLD)
        {
            _warningTriggered = true;
            OnWarning?.Invoke();
        }

        // Check for expiration
        if (_remainingTime <= 0)
        {
            _remainingTime = 0;
            _isRunning = false;
            OnExpired?.Invoke();
        }
    }

    /// <summary>
    /// Add bonus time (for certain power-ups or events)
    /// </summary>
    public void AddTime(int seconds)
    {
        _remainingTime += seconds;
    }
}
