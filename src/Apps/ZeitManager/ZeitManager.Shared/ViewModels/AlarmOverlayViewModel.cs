using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using ZeitManager.Models;
using ZeitManager.Services;

namespace ZeitManager.ViewModels;

public partial class AlarmOverlayViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly ITimerService _timerService;
    private readonly IAlarmSchedulerService _alarmScheduler;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localization;
    private readonly IShakeDetectionService _shakeDetection;
    private readonly IHapticService _haptic;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("HH:mm");

    [ObservableProperty]
    private bool _canSnooze;

    [ObservableProperty]
    private string _snoozeText = string.Empty;

    [ObservableProperty]
    private bool _isTimerSource;

    // Challenge-Zustand
    [ObservableProperty]
    private bool _hasChallengeActive;

    [ObservableProperty]
    private bool _challengeSolved;

    [ObservableProperty]
    private string _challengeQuestion = string.Empty;

    [ObservableProperty]
    private string _challengeAnswer = string.Empty;

    [ObservableProperty]
    private string _challengeFeedback = string.Empty;

    [ObservableProperty]
    private bool _isMathChallenge;

    [ObservableProperty]
    private bool _isShakeChallenge;

    [ObservableProperty]
    private int _shakeProgress;

    [ObservableProperty]
    private int _shakeTarget;

    public double ShakeProgressFraction => ShakeTarget > 0 ? (double)ShakeProgress / ShakeTarget : 0;

    public bool HasPhysicalSensor => _shakeDetection.HasPhysicalSensor;

    public string ShakeInstructionText => _localization.GetString("ChallengeShakeInstruction");
    public string SimulateShakeText => _localization.GetString("SimulateShake");

    private MathChallenge? _currentChallenge;

    private TimerItem? _sourceTimer;
    private AlarmItem? _sourceAlarm;
    private System.Timers.Timer? _clockTimer;

    // Localized strings
    public string DismissText => _localization.GetString("Dismiss");
    public string SnoozeLabel => _localization.GetString("Snooze");
    public string ChallengeInstructionText => _localization.GetString("ChallengeMathInstruction");
    public string ChallengeAnswerHintText => _localization.GetString("ChallengeAnswerHint");
    public string ChallengeSubmitText => _localization.GetString("ChallengeSubmit");

    public bool CanDismiss => !HasChallengeActive || ChallengeSolved;

    public AlarmOverlayViewModel(
        ITimerService timerService,
        IAlarmSchedulerService alarmScheduler,
        IAudioService audioService,
        ILocalizationService localization,
        IShakeDetectionService shakeDetection,
        IHapticService haptic)
    {
        _timerService = timerService;
        _alarmScheduler = alarmScheduler;
        _audioService = audioService;
        _localization = localization;
        _shakeDetection = shakeDetection;
        _haptic = haptic;

        _shakeDetection.ShakeDetected += OnShakeDetected;
    }

    public void ShowForTimer(TimerItem timer)
    {
        _sourceTimer = timer;
        _sourceAlarm = null;
        IsTimerSource = true;
        Title = timer.Name;
        Subtitle = _localization.GetString("TimerFinishedNotification");
        CanSnooze = true;
        SnoozeText = $"1 {_localization.GetString("MinutesShort")}";
        StartClock();
        _ = _audioService.PlayAsync(timer.AlarmTone, loop: true);
    }

    public void ShowForAlarm(AlarmItem alarm)
    {
        _sourceAlarm = alarm;
        _sourceTimer = null;
        IsTimerSource = false;
        Title = string.IsNullOrEmpty(alarm.Name) ? _localization.GetString("Alarm") : alarm.Name;
        Subtitle = alarm.TimeFormatted;
        CanSnooze = alarm.CurrentSnoozeCount < alarm.MaxSnoozeCount;
        SnoozeText = $"{alarm.SnoozeDurationMinutes} {_localization.GetString("MinutesShort")} ({alarm.MaxSnoozeCount - alarm.CurrentSnoozeCount}x)";

        // Challenge initialisieren
        if (alarm.ChallengeEnabled && alarm.ChallengeType == ChallengeType.Math)
        {
            HasChallengeActive = true;
            ChallengeSolved = false;
            IsMathChallenge = true;
            IsShakeChallenge = false;
            ChallengeAnswer = string.Empty;
            ChallengeFeedback = string.Empty;
            _currentChallenge = MathChallenge.Generate(alarm.ChallengeDifficulty);
            ChallengeQuestion = _currentChallenge.Question;
            OnPropertyChanged(nameof(CanDismiss));
        }
        else if (alarm.ChallengeEnabled && alarm.ChallengeType == ChallengeType.Shake)
        {
            HasChallengeActive = true;
            ChallengeSolved = false;
            IsMathChallenge = false;
            IsShakeChallenge = true;
            ShakeProgress = 0;
            ShakeTarget = alarm.ShakeCount > 0 ? alarm.ShakeCount : 20;
            ChallengeAnswer = string.Empty;
            ChallengeFeedback = string.Empty;
            OnPropertyChanged(nameof(CanDismiss));
            OnPropertyChanged(nameof(ShakeProgressFraction));
            _shakeDetection.StartListening();
        }
        else
        {
            HasChallengeActive = false;
            ChallengeSolved = false;
            IsMathChallenge = false;
            IsShakeChallenge = false;
            OnPropertyChanged(nameof(CanDismiss));
        }

        StartClock();
        _ = _audioService.PlayAsync(alarm.AlarmTone, loop: true);
    }

    [RelayCommand]
    private async Task Dismiss()
    {
        _haptic.HeavyClick();
        _audioService.Stop();
        _shakeDetection.StopListening();
        StopClock();

        if (_sourceTimer != null)
        {
            await _timerService.StopTimerAsync(_sourceTimer);
        }
        else if (_sourceAlarm != null)
        {
            await _alarmScheduler.DismissAlarmAsync(_sourceAlarm);
        }

        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SubmitChallenge()
    {
        if (_currentChallenge == null || _sourceAlarm == null) return;

        if (string.IsNullOrWhiteSpace(ChallengeAnswer))
        {
            ChallengeFeedback = _localization.GetString("ChallengeEmptyAnswer");
            return;
        }

        if (!int.TryParse(ChallengeAnswer.Trim(), out var answer))
        {
            ChallengeFeedback = _localization.GetString("ChallengeInvalidInput");
            return;
        }

        if (answer == _currentChallenge.Answer)
        {
            // Richtig!
            ChallengeSolved = true;
            ChallengeFeedback = _localization.GetString("ChallengeCorrect");
            OnPropertyChanged(nameof(CanDismiss));
        }
        else
        {
            // Falsch → neue Aufgabe generieren
            ChallengeFeedback = string.Format(_localization.GetString("ChallengeWrong"), _currentChallenge.Answer);
            ChallengeAnswer = string.Empty;
            _currentChallenge = MathChallenge.Generate(_sourceAlarm.ChallengeDifficulty);
            ChallengeQuestion = _currentChallenge.Question;
        }
    }

    [RelayCommand]
    private void SimulateShake()
    {
        _shakeDetection.SimulateShake();
    }

    private void OnShakeDetected(object? sender, EventArgs e)
    {
        if (!IsShakeChallenge || ChallengeSolved) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ShakeProgress++;
            OnPropertyChanged(nameof(ShakeProgressFraction));

            if (ShakeProgress >= ShakeTarget)
            {
                ChallengeSolved = true;
                ChallengeFeedback = _localization.GetString("ChallengeCompleted");
                OnPropertyChanged(nameof(CanDismiss));
                _shakeDetection.StopListening();
            }
        });
    }

    [RelayCommand]
    private async Task Snooze()
    {
        _haptic.Click();
        _audioService.Stop();
        _shakeDetection.StopListening();
        StopClock();

        if (_sourceTimer != null)
        {
            await _timerService.SnoozeTimerAsync(_sourceTimer);
        }
        else if (_sourceAlarm != null)
        {
            await _alarmScheduler.SnoozeAlarmAsync(_sourceAlarm);
        }

        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? DismissRequested;

    private void StartClock()
    {
        _clockTimer?.Dispose();
        _clockTimer = new System.Timers.Timer(1000);
        _clockTimer.Elapsed += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                CurrentTime = DateTime.Now.ToString("HH:mm"));
        _clockTimer.Start();
        CurrentTime = DateTime.Now.ToString("HH:mm");
    }

    private void StopClock()
    {
        _clockTimer?.Stop();
        _clockTimer?.Dispose();
        _clockTimer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        StopClock();
        _audioService.Stop();
        _shakeDetection.StopListening();
        _shakeDetection.ShakeDetected -= OnShakeDetected;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
