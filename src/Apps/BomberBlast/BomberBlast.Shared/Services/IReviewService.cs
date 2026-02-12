namespace BomberBlast.Services;

/// <summary>
/// Service für In-App-Review-Aufforderung.
/// Prüft ob der richtige Zeitpunkt für eine Review-Anfrage ist.
/// </summary>
public interface IReviewService
{
    /// <summary>Prüft ob jetzt eine Review-Anfrage gestellt werden soll</summary>
    bool ShouldPromptReview();

    /// <summary>Markiert dass eine Review-Anfrage gestellt wurde</summary>
    void MarkReviewPrompted();

    /// <summary>Registriert einen Level-Abschluss (für Review-Timing)</summary>
    void OnLevelCompleted(int level);
}
