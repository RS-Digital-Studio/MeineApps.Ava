using System.Diagnostics;

namespace MeineApps.UI.Loading;

/// <summary>
/// Basis-Klasse für Lade-Pipelines. Führt Ladeschritte sequentiell aus
/// und meldet gewichteten Fortschritt über ProgressChanged.
/// Jeder Step wird in try/catch gewrappt - Fehler werden geloggt aber
/// die Pipeline läuft weiter (degraded statt crash).
/// </summary>
public abstract class LoadingPipelineBase : ILoadingPipeline
{
    private readonly List<LoadingStep> _steps = new();

    public event Action<float, string>? ProgressChanged;

    /// <summary>
    /// Fügt einen Ladeschritt hinzu. Reihenfolge = Ausführungsreihenfolge.
    /// </summary>
    protected void AddStep(LoadingStep step) => _steps.Add(step);

    /// <summary>
    /// Führt alle registrierten Schritte sequentiell aus.
    /// Meldet nach jedem Schritt den gewichteten Fortschritt.
    /// </summary>
    public async Task ExecuteAsync()
    {
        var totalWeight = _steps.Sum(s => s.Weight);
        if (totalWeight == 0) return;

        var completedWeight = 0;
        var sw = Stopwatch.StartNew();

        foreach (var step in _steps)
        {
            // Fortschritt vor dem Schritt melden (zeigt den aktuellen Status-Text)
            var progress = (float)completedWeight / totalWeight;
            ProgressChanged?.Invoke(progress, step.DisplayName);

            try
            {
                var stepSw = Stopwatch.StartNew();
                await step.ExecuteAsync();
                stepSw.Stop();
                Debug.WriteLine($"[LoadingPipeline] {step.Name}: {stepSw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                // Fehler loggen, aber Pipeline fortsetzen
                Debug.WriteLine($"[LoadingPipeline] FEHLER in {step.Name}: {ex.Message}");
            }

            completedWeight += step.Weight;
        }

        // Abschluss melden
        ProgressChanged?.Invoke(1.0f, "");
        sw.Stop();
        Debug.WriteLine($"[LoadingPipeline] Gesamt: {sw.ElapsedMilliseconds}ms ({_steps.Count} Schritte)");
    }
}
