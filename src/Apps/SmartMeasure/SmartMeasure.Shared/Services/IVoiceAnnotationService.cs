namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.12: Sprach-Annotation pro Punkt. Beim Punkt-Set wird ein
/// Mikrofon aktiviert, User spricht 5 Sek. ein Label. Audio + Transkript landen
/// neben dem Punkt (Hands-free Workflow: Phone in einer Hand, RTK-Stab in der
/// anderen). Android nutzt <c>SpeechRecognizer</c>, Desktop bleibt no-op (kein
/// Mikrofon-Kontext im Vermessungs-UX).</summary>
public interface IVoiceAnnotationService
{
    /// <summary>Audio-Aufnahme + Erkennung asynchron starten. Liefert das Transkript
    /// (lokalisiert) plus den absoluten Pfad zur gespeicherten WAV/MP3 (in
    /// <see cref="IAppPaths.PhotosFolder"/> oder analog). null wenn Mikrofon nicht
    /// verfuegbar, User abgebrochen hat oder kein Sprach-Input erkannt wurde.</summary>
    Task<VoiceAnnotation?> RecordAsync(int maxSeconds = 5, CancellationToken ct = default);
}

/// <summary>Ergebnis einer Voice-Annotation.</summary>
public sealed record VoiceAnnotation(string Transcript, string AudioPath, TimeSpan Duration);
