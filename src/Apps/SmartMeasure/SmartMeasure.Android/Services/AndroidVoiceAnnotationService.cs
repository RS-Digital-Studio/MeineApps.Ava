using Android.Content;
using Android.OS;
using Android.Speech;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Android.Services;

/// <summary>Plan-Kap. 5.12: Android-SpeechRecognizer-basierte Sprach-Annotation.
/// Nimmt 5 Sekunden Mikrofon-Input auf und liefert das Transkript. Audio-File wird
/// nicht zusaetzlich gespeichert — SpeechRecognizer + MediaRecorder gleichzeitig
/// konkurrieren um den Mikrofon-Kanal. Wer Audio braucht, kann den AudioPath leer
/// lassen und das Transkript als Text-Annotation behandeln.</summary>
public sealed class AndroidVoiceAnnotationService : IVoiceAnnotationService
{
    private readonly Context _context;

    public AndroidVoiceAnnotationService(Context context)
    {
        _context = context;
    }

    public Task<VoiceAnnotation?> RecordAsync(int maxSeconds = 5, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<VoiceAnnotation?>();
        var startTime = DateTime.UtcNow;

        if (!SpeechRecognizer.IsRecognitionAvailable(_context))
        {
            tcs.TrySetResult(null);
            return tcs.Task;
        }

        var recognizer = SpeechRecognizer.CreateSpeechRecognizer(_context);
        if (recognizer == null)
        {
            tcs.TrySetResult(null);
            return tcs.Task;
        }

        var listener = new SimpleRecognitionListener(result =>
        {
            try { recognizer.Destroy(); } catch { }
            if (result == null) { tcs.TrySetResult(null); return; }
            var duration = DateTime.UtcNow - startTime;
            tcs.TrySetResult(new VoiceAnnotation(result, string.Empty, duration));
        });
        recognizer.SetRecognitionListener(listener);

        var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraLanguage, _context.Resources?.Configuration?.Locale?.ToString() ?? "de-DE");
        intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
        intent.PutExtra(RecognizerIntent.ExtraSpeechInputCompleteSilenceLengthMillis, maxSeconds * 1000);
        intent.PutExtra(RecognizerIntent.ExtraSpeechInputMinimumLengthMillis, 1000);

        try
        {
            recognizer.StartListening(intent);
        }
        catch (Exception)
        {
            try { recognizer.Destroy(); } catch { }
            tcs.TrySetResult(null);
        }

        // Cancellation honor
        ct.Register(() =>
        {
            try { recognizer.Cancel(); recognizer.Destroy(); } catch { }
            tcs.TrySetResult(null);
        });

        return tcs.Task;
    }

    /// <summary>Java-Listener-Wrapper. <c>IRecognitionListener</c> ist ein Java-Interface,
    /// das ueber Java.Lang.Object erbt — sonst gibt es keinen JNI-Connector.</summary>
    private sealed class SimpleRecognitionListener(Action<string?> onResult)
        : Java.Lang.Object, IRecognitionListener
    {
        public void OnBeginningOfSpeech() { }
        public void OnBufferReceived(byte[]? buffer) { }
        public void OnEndOfSpeech() { }
        public void OnError(SpeechRecognizerError error) => onResult(null);
        public void OnEvent(int eventType, Bundle? bundle) { }
        public void OnPartialResults(Bundle? partialResults) { }
        public void OnReadyForSpeech(Bundle? @params) { }
        public void OnRmsChanged(float rmsdB) { }
        public void OnResults(Bundle? results)
        {
            try
            {
                var matches = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
                onResult(matches?.Count > 0 ? matches[0] : null);
            }
            catch { onResult(null); }
        }
    }
}
