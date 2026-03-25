using System.Net.Http.Json;
using GardenControl.Core.DTOs;
using GardenControl.Core.Models;

namespace GardenControl.Shared.Services;

/// <summary>
/// REST-API Client für den GardenControl-Server.
/// Alle Methoden geben bei Fehlern sinnvolle Defaults zurück
/// und melden Fehler über das ErrorOccurred-Event.
/// </summary>
public class ApiService : IApiService
{
    private readonly HttpClient _http;
    private string _baseUrl = "http://192.168.178.56:5000";

    /// <summary>Wird bei jedem API-Fehler gefeuert (für UI-Feedback)</summary>
    public event Action<string>? ErrorOccurred;

    public ApiService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public void SetServerUrl(string url)
    {
        _baseUrl = url.TrimEnd('/');
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _http.GetAsync($"{_baseUrl}/api/status", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            ErrorOccurred?.Invoke("Zeitüberschreitung - Server antwortet nicht");
            return false;
        }
        catch (HttpRequestException ex)
        {
            ErrorOccurred?.Invoke($"Server nicht erreichbar: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Verbindungsfehler: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Zone>> GetZonesAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<Zone>>($"{_baseUrl}/api/zones") ?? [];
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Zonen laden fehlgeschlagen: {ex.Message}");
            return [];
        }
    }

    public async Task<Zone?> UpdateZoneAsync(ZoneConfigDto config)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"{_baseUrl}/api/zones/{config.ZoneId}", config);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<Zone>();

            var error = await response.Content.ReadAsStringAsync();
            ErrorOccurred?.Invoke($"Zone speichern fehlgeschlagen: {error}");
            return null;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Zone speichern fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    public async Task<Zone?> CalibrateAsync(int zoneId, string type)
    {
        try
        {
            var response = await _http.PostAsync($"{_baseUrl}/api/zones/{zoneId}/calibrate/{type}", null);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<Zone>();

            var error = await response.Content.ReadAsStringAsync();
            ErrorOccurred?.Invoke($"Kalibrierung fehlgeschlagen: {error}");
            return null;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Kalibrierung fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    public async Task<List<SensorReading>> GetReadingsAsync(int? zoneId, DateTime from, DateTime to, int maxResults = 1000)
    {
        try
        {
            var query = $"?from={from:O}&to={to:O}&limit={maxResults}";
            if (zoneId.HasValue) query += $"&zoneId={zoneId}";
            return await _http.GetFromJsonAsync<List<SensorReading>>($"{_baseUrl}/api/history/readings{query}") ?? [];
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Verlaufsdaten laden fehlgeschlagen: {ex.Message}");
            return [];
        }
    }

    public async Task<List<IrrigationEvent>> GetEventsAsync(int? zoneId, DateTime from, DateTime to, int maxResults = 100)
    {
        try
        {
            var query = $"?from={from:O}&to={to:O}&limit={maxResults}";
            if (zoneId.HasValue) query += $"&zoneId={zoneId}";
            return await _http.GetFromJsonAsync<List<IrrigationEvent>>($"{_baseUrl}/api/history/events{query}") ?? [];
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Ereignisse laden fehlgeschlagen: {ex.Message}");
            return [];
        }
    }

    public async Task<Dictionary<string, string>> GetConfigAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<Dictionary<string, string>>($"{_baseUrl}/api/config") ?? [];
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Konfiguration laden fehlgeschlagen: {ex.Message}");
            return [];
        }
    }

    public async Task<List<IrrigationSchedule>> GetSchedulesAsync(int? zoneId = null)
    {
        try
        {
            var query = zoneId.HasValue ? $"?zoneId={zoneId}" : "";
            return await _http.GetFromJsonAsync<List<IrrigationSchedule>>($"{_baseUrl}/api/schedules{query}") ?? [];
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Zeitpläne laden fehlgeschlagen: {ex.Message}");
            return [];
        }
    }

    public async Task<IrrigationSchedule?> SaveScheduleAsync(IrrigationSchedule schedule)
    {
        try
        {
            HttpResponseMessage response;
            if (schedule.Id == 0)
                response = await _http.PostAsJsonAsync($"{_baseUrl}/api/schedules", schedule);
            else
                response = await _http.PutAsJsonAsync($"{_baseUrl}/api/schedules/{schedule.Id}", schedule);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<IrrigationSchedule>();

            var error = await response.Content.ReadAsStringAsync();
            ErrorOccurred?.Invoke($"Zeitplan speichern fehlgeschlagen: {error}");
            return null;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Zeitplan speichern fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DeleteScheduleAsync(int id)
    {
        try
        {
            var response = await _http.DeleteAsync($"{_baseUrl}/api/schedules/{id}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ErrorOccurred?.Invoke($"Zeitplan löschen fehlgeschlagen: {error}");
            }
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Zeitplan löschen fehlgeschlagen: {ex.Message}");
            return false;
        }
    }

    public async Task UpdateConfigAsync(SystemConfigDto config)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"{_baseUrl}/api/config", config);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ErrorOccurred?.Invoke($"Konfiguration speichern fehlgeschlagen: {error}");
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Konfiguration speichern fehlgeschlagen: {ex.Message}");
        }
    }
}
