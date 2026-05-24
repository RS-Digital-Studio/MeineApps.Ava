using System.Net.Http;
using System.Text.Json;

namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.16: Holt Live-Daten von NOAA Space Weather + lokaler StickState.
/// Free APIs, kein Schluessel erforderlich:
///   - Kp-Index: services.swpc.noaa.gov/json/planetary_k_index_1m.json (1-min Refresh)
///   - F10.7-Solar-Flux: services.swpc.noaa.gov/json/f107_cm_flux.json (3h Refresh)
/// Beide werden 1h gecached um die NOAA-Server nicht zu spammen.</summary>
public sealed class GnssConditionService : IGnssConditionService, IDisposable
{
    private readonly IBleService _bleService;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private GnssConditions? _cached;
    private DateTime _cachedAtUtc = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private const string KpUrl = "https://services.swpc.noaa.gov/json/planetary_k_index_1m.json";
    private const string F107Url = "https://services.swpc.noaa.gov/json/f107_cm_flux.json";

    public GnssConditionService(IBleService bleService)
    {
        _bleService = bleService;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SmartMeasure/1.0");
    }

    public async Task<GnssConditions> GetCurrentConditionsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cached != null && (DateTime.UtcNow - _cachedAtUtc) < CacheTtl)
                return _cached;

            var stick = _bleService.CurrentState;
            int? sats = stick.SatelliteCount > 0 ? stick.SatelliteCount : null;
            double? pdop = null; // StickState hat aktuell kein PDOP-Feld — kommt mit Firmware-v1.2

            double? kp = await TryFetchKpAsync(ct).ConfigureAwait(false);
            double? f107 = await TryFetchF107Async(ct).ConfigureAwait(false);

            var ionLevel = ClassifyIonosphere(kp);
            var (overall, recommendation) = Recommend(sats, ionLevel);

            _cached = new GnssConditions(sats, pdop, kp, f107, ionLevel, overall, recommendation);
            _cachedAtUtc = DateTime.UtcNow;
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<double?> TryFetchKpAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(KpUrl, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            // Format: [{"time_tag":"...","kp_index":1.67,...}, ...]  — der letzte Eintrag ist der aktuellste.
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var len = doc.RootElement.GetArrayLength();
            if (len == 0) return null;
            var last = doc.RootElement[len - 1];
            if (last.TryGetProperty("kp_index", out var kpProp))
                return kpProp.GetDouble();
            return null;
        }
        catch { return null; }
    }

    private async Task<double?> TryFetchF107Async(CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(F107Url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var len = doc.RootElement.GetArrayLength();
            if (len == 0) return null;
            var last = doc.RootElement[len - 1];
            if (last.TryGetProperty("flux", out var fxProp))
                return fxProp.GetDouble();
            return null;
        }
        catch { return null; }
    }

    /// <summary>Kp-Index-Skala: 0-2=Quiet, 3-4=Unsettled/Active, 5+=Storm.</summary>
    private static GnssQuality ClassifyIonosphere(double? kp) => kp switch
    {
        null => GnssQuality.Unknown,
        < 3 => GnssQuality.Good,
        < 5 => GnssQuality.Fair,
        _ => GnssQuality.Poor,
    };

    private static (GnssQuality overall, string text) Recommend(int? sats, GnssQuality ion)
    {
        // Schwacher Fix dominiert immer (egal wie ruhig die Ionosphaere ist)
        if (sats is < 8) return (GnssQuality.Poor, "Wenige Satelliten — freie Sicht zum Himmel suchen");
        if (ion == GnssQuality.Poor) return (GnssQuality.Poor, "Geomagnetischer Sturm — RTK-Float-Risiko, lieber spaeter messen");
        if (ion == GnssQuality.Fair) return (GnssQuality.Fair, "Erhoehte Ionosphaere — Genauigkeit leicht reduziert");
        if (ion == GnssQuality.Good) return (GnssQuality.Good, "Ionosphaere ruhig — gute Mess-Bedingungen");
        return (GnssQuality.Unknown, "Bedingungen unbekannt — pruefe Internet-Verbindung");
    }

    public void Dispose()
    {
        try { _http.Dispose(); } catch { }
        try { _gate.Dispose(); } catch { }
    }
}
