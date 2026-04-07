using BingXBot.Core.Models.ATI;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace BingXBot.Engine.ATI;

/// <summary>
/// ONNX Model Inference: Lädt vortrainierte Modelle (.onnx) und inferiert Predictions.
/// Erlaubt in Python (PyTorch/TensorFlow/scikit-learn) trainierte Modelle in C# zu nutzen.
/// Typischer Workflow: Python trainiert Transformer/LSTM → ONNX Export → C# Inference hier.
/// </summary>
public class OnnxModelInference : IDisposable
{
    private InferenceSession? _session;
    private bool _disposed;

    /// <summary>True wenn ein ONNX-Modell geladen ist.</summary>
    public bool IsModelLoaded => _session != null;

    /// <summary>Pfad zum aktuell geladenen Modell.</summary>
    public string? ModelPath { get; private set; }

    /// <summary>Zeitpunkt des letzten Modell-Ladens.</summary>
    public DateTime? LoadedAt { get; private set; }

    /// <summary>
    /// Lädt ein ONNX-Modell von der Festplatte.
    /// Unterstützt Modelle die einen float[1, FeatureCount] Input und float[1, 2] Output (P(Loss), P(Win)) haben.
    /// </summary>
    public bool LoadModel(string modelPath)
    {
        try
        {
            var options = new SessionOptions
            {
                // CPU-Optimierungen für Desktop
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                InterOpNumThreads = 1,
                IntraOpNumThreads = Environment.ProcessorCount
            };

            _session?.Dispose();
            _session = new InferenceSession(modelPath, options);
            ModelPath = modelPath;
            LoadedAt = DateTime.UtcNow;
            return true;
        }
        catch
        {
            _session = null;
            return false;
        }
    }

    /// <summary>
    /// Inferiert die Gewinn-Wahrscheinlichkeit für einen FeatureSnapshot.
    /// Gibt P(Win) zurück (0.0 - 1.0). Bei Fehler: 0.5 (neutral).
    /// </summary>
    public float Predict(FeatureSnapshot snapshot)
    {
        if (_session == null) return 0.5f;

        try
        {
            var features = snapshot.ToFeatureArray();
            var inputTensor = new DenseTensor<float>(features, new[] { 1, features.Length });

            // Input-Name aus dem Modell lesen (typisch: "input" oder "features")
            var inputName = _session.InputNames[0];
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            using var results = _session.Run(inputs);
            var output = results[0].AsTensor<float>();

            // Output interpretieren: Modell gibt entweder [P(Loss), P(Win)] oder [P(Win)] zurück
            if (output.Length >= 2)
                return output[1]; // [P(Loss), P(Win)] → Index 1 = P(Win)
            if (output.Length == 1)
                return output[0]; // Einzelner Wert = P(Win) direkt

            return 0.5f;
        }
        catch
        {
            return 0.5f;
        }
    }

    /// <summary>
    /// Inferiert Batch-Predictions für mehrere FeatureSnapshots.
    /// Effizienter als einzelne Predict()-Aufrufe für Backtest-Szenarien.
    /// </summary>
    public float[] PredictBatch(IReadOnlyList<FeatureSnapshot> snapshots)
    {
        if (_session == null || snapshots.Count == 0)
            return Array.Empty<float>();

        try
        {
            var featureCount = FeatureSnapshot.FeatureCount;
            var batchSize = snapshots.Count;
            var flatFeatures = new float[batchSize * featureCount];

            for (int i = 0; i < batchSize; i++)
            {
                var features = snapshots[i].ToFeatureArray();
                Array.Copy(features, 0, flatFeatures, i * featureCount, featureCount);
            }

            var inputTensor = new DenseTensor<float>(flatFeatures, new[] { batchSize, featureCount });
            var inputName = _session.InputNames[0];
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            using var results = _session.Run(inputs);
            var output = results[0].AsTensor<float>();

            var predictions = new float[batchSize];
            var outputPerSample = (int)(output.Length / batchSize);

            for (int i = 0; i < batchSize; i++)
            {
                predictions[i] = outputPerSample >= 2
                    ? output[i * outputPerSample + 1] // [P(Loss), P(Win)]
                    : output[i * outputPerSample];     // [P(Win)]
            }

            return predictions;
        }
        catch
        {
            return new float[snapshots.Count]; // Alle 0.0 bei Fehler
        }
    }

    /// <summary>Gibt Modell-Metadaten zurück (Input/Output-Shape, Opset-Version).</summary>
    public string GetModelInfo()
    {
        if (_session == null) return "Kein Modell geladen";

        var inputs = string.Join(", ", _session.InputMetadata.Select(m =>
            $"{m.Key}: {string.Join("x", m.Value.Dimensions)}"));
        var outputs = string.Join(", ", _session.OutputMetadata.Select(m =>
            $"{m.Key}: {string.Join("x", m.Value.Dimensions)}"));

        return $"Inputs: [{inputs}], Outputs: [{outputs}], Pfad: {ModelPath}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session?.Dispose();
        _session = null;
    }
}
