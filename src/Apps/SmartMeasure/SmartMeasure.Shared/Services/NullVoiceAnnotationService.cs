namespace SmartMeasure.Shared.Services;

/// <summary>Desktop-/Mock-Default: keine Mikrofon-Aufnahme. Android-Implementation
/// (<c>AndroidVoiceAnnotationService</c>) folgt mit der Verkabelung — Plan-Kap. 5.12
/// fordert <c>android.speech.SpeechRecognizer</c> + <c>MediaRecorder</c>.</summary>
public sealed class NullVoiceAnnotationService : IVoiceAnnotationService
{
    public Task<VoiceAnnotation?> RecordAsync(int maxSeconds = 5, CancellationToken ct = default)
        => Task.FromResult<VoiceAnnotation?>(null);
}
