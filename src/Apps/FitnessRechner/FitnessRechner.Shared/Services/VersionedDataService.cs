using System.Text.Json;
using FitnessRechner.Models;

namespace FitnessRechner.Services;

/// <summary>
/// Service for reading and writing versioned JSON data.
/// Handles backward compatibility with unversioned data files.
/// </summary>
public static class VersionedDataService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Loads versioned data from a JSON file.
    /// Handles both versioned and unversioned (legacy) formats.
    /// </summary>
    /// <typeparam name="T">Type of data to load</typeparam>
    /// <param name="filePath">Path to the JSON file</param>
    /// <param name="currentVersion">Current schema version for this data type</param>
    /// <returns>The loaded data, or empty list if file doesn't exist</returns>
    public static async Task<VersionedData<List<T>>> LoadAsync<T>(string filePath, int currentVersion) where T : class
    {
        if (!File.Exists(filePath))
        {
            return new VersionedData<List<T>>([], currentVersion);
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);

            // Try to deserialize as versioned data first
            try
            {
                var versionedData = JsonSerializer.Deserialize<VersionedData<List<T>>>(json, _jsonOptions);
                if (versionedData != null && versionedData.Data != null)
                {
                    // TODO: Run migrations if versionedData.Version < currentVersion
                    return versionedData;
                }
            }
            catch (JsonException)
            {
                // Not in versioned format, try legacy format
            }

            // Try to deserialize as raw array (legacy format)
            var legacyData = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions);
            if (legacyData != null)
            {
                return new VersionedData<List<T>>(legacyData, currentVersion);
            }

            return new VersionedData<List<T>>([], currentVersion);
        }
        catch (Exception)
        {
            return new VersionedData<List<T>>([], currentVersion);
        }
    }

    /// <summary>
    /// Saves versioned data to a JSON file with atomic write operations.
    /// </summary>
    /// <typeparam name="T">Type of data to save</typeparam>
    /// <param name="filePath">Path to the JSON file</param>
    /// <param name="data">Data to save</param>
    /// <param name="currentVersion">Current schema version</param>
    public static async Task SaveAsync<T>(string filePath, List<T> data, int currentVersion) where T : class
    {
        var versionedData = new VersionedData<List<T>>(data, currentVersion);
        var tempFilePath = filePath + ".tmp";

        try
        {
            var json = JsonSerializer.Serialize(versionedData, _jsonOptions);
            await File.WriteAllTextAsync(tempFilePath, json);

            // Create backup if original exists
            if (File.Exists(filePath))
            {
                var backupPath = filePath + ".backup";
                File.Copy(filePath, backupPath, overwrite: true);
            }

            // Atomic move
            File.Move(tempFilePath, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
            throw;
        }
    }

    /// <summary>
    /// Gets the current version of a data file without loading all data.
    /// </summary>
    /// <param name="filePath">Path to the JSON file</param>
    /// <returns>Version number, or 0 if unversioned/not found</returns>
    public static async Task<int> GetFileVersionAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return 0;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);

            // Quick check for versioned format
            if (json.TrimStart().StartsWith("{") && json.Contains("\"Version\""))
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Version", out var versionProp))
                {
                    return versionProp.GetInt32();
                }
            }

            // Unversioned (legacy) format
            return 0;
        }
        catch
        {
            return 0;
        }
    }
}
