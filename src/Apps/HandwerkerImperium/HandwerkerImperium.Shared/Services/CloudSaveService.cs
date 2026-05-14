using System.Text.Json;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Firebase-basierter Cloud-Save. Schreibt/liest Spielstaende unter
/// <c>cloud_saves/{playerId}</c>.
///
/// Struktur in Firebase:
/// <list type="bullet">
///   <item><c>cloud_saves/{playerId}/metadata</c> — kleine Preview (Level, Money, SavedAt, Version)</item>
///   <item><c>cloud_saves/{playerId}/data</c> — der kompakte State-JSON</item>
/// </list>
///
/// Der Save wird beim Download fuer das aktuelle Geraet neu HMAC-signiert (Integrity-Key
/// ist geraete-gebunden, <see cref="IGameIntegrityService"/>). Das ist bewusst: Cloud-Save
/// schuetzt gegen Geraeteverlust, nicht gegen Save-Editing — der HMAC bleibt lokal wirksam.
/// </summary>
public sealed class CloudSaveService : ICloudSaveService
{
    private const string BasePath = "cloud_saves";

    private readonly IFirebaseService _firebase;
    private readonly IGameIntegrityService _integrity;
    private readonly ILogService _log;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CloudSaveService(IFirebaseService firebase, IGameIntegrityService integrity, ILogService log)
    {
        _firebase = firebase;
        _integrity = integrity;
        _log = log;
    }

    /// <inheritdoc />
    public bool IsAvailable => _firebase.IsOnline && !string.IsNullOrEmpty(_firebase.PlayerId);

    /// <inheritdoc />
    public async Task<CloudSaveMetadata?> GetMetadataAsync()
    {
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return null;

        try
        {
            var path = $"{BasePath}/{_firebase.PlayerId}/metadata";
            return await _firebase.GetAsync<CloudSaveMetadata>(path).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error("Cloud-Save-Metadaten konnten nicht geladen werden.", ex);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<GameState?> DownloadAsync()
    {
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return null;

        try
        {
            var path = $"{BasePath}/{_firebase.PlayerId}/data";
            var raw = await _firebase.QueryAsync(path, "").ConfigureAwait(false);
            if (string.IsNullOrEmpty(raw)) return null;

            // raw ist JSON-umhuellter String (Firebase gibt bei PUT-String die Anfuehrungszeichen mit).
            // Wir parsen entweder den String-Wert oder direkt den JSON-State.
            string stateJson;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                stateJson = doc.RootElement.ValueKind == JsonValueKind.String
                    ? doc.RootElement.GetString() ?? ""
                    : raw;
            }
            catch
            {
                stateJson = raw;
            }

            if (string.IsNullOrEmpty(stateJson)) return null;

            var state = JsonSerializer.Deserialize<GameState>(stateJson, _jsonOptions);
            if (state == null) return null;

            // Cloud-State wurde auf einem anderen Geraet signiert → neu signieren fuer lokales Geraet
            _integrity.ComputeSignature(state);
            return state;
        }
        catch (Exception ex)
        {
            _log.Error("Cloud-Save-Download fehlgeschlagen.", ex);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UploadAsync(GameState state)
    {
        if (!IsAvailable) return false;

        try
        {
            // Vor dem Upload signieren (fuer das uploadende Geraet)
            state.LastSavedAt = DateTime.UtcNow;
            _integrity.ComputeSignature(state);

            var json = JsonSerializer.Serialize(state, _jsonOptions);
            var metadata = BuildMetadata(state);
            return await UploadInternalAsync(json, metadata).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error("Cloud-Save-Upload fehlgeschlagen.", ex);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UploadJsonAsync(string stateJson, CloudSaveMetadata metadata)
    {
        if (!IsAvailable) return false;
        if (string.IsNullOrEmpty(stateJson)) return false;

        try
        {
            // Race-freier Pfad: JSON + Metadata kommen bereits "frozen" vom Aufrufer.
            // Kein State-Zugriff in dieser Methode → kein Collection-modified-Risiko.
            return await UploadInternalAsync(stateJson, metadata).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error("Cloud-Save-Upload (JSON) fehlgeschlagen.", ex);
            return false;
        }
    }

    private static CloudSaveMetadata BuildMetadata(GameState state) => new()
    {
        PlayerLevel = state.PlayerLevel,
        Money = state.Money,
        GoldenScrews = state.GoldenScrews,
        PrestigePoints = state.Prestige.PrestigePoints,
        AscensionLevel = state.Ascension.AscensionLevel,
        SavedAtIso = state.LastSavedAt.ToString("O"),
        StateVersion = state.Version,
        AppVersion = typeof(CloudSaveService).Assembly.GetName().Version?.ToString(3) ?? "unknown"
    };

    private async Task<bool> UploadInternalAsync(string json, CloudSaveMetadata metadata)
    {
        // 1) Metadaten schreiben (klein, fuer Konflikt-Anzeige)
        var metaOk = await _firebase.SetAsync($"{BasePath}/{_firebase.PlayerId}/metadata", metadata)
            .ConfigureAwait(false);

        // 2) Voller State (als JSON-String — Firebase speichert das als String-Wert)
        var dataOk = await _firebase.SetAsync($"{BasePath}/{_firebase.PlayerId}/data", json)
            .ConfigureAwait(false);

        return metaOk && dataOk;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync()
    {
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return false;

        try
        {
            var ok1 = await _firebase.DeleteAsync($"{BasePath}/{_firebase.PlayerId}/data").ConfigureAwait(false);
            var ok2 = await _firebase.DeleteAsync($"{BasePath}/{_firebase.PlayerId}/metadata").ConfigureAwait(false);
            return ok1 && ok2;
        }
        catch (Exception ex)
        {
            _log.Error("Cloud-Save-Loeschen fehlgeschlagen.", ex);
            return false;
        }
    }
}
