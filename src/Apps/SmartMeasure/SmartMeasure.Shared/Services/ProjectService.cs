using SmartMeasure.Shared.Models;
using SQLite;

namespace SmartMeasure.Shared.Services;

/// <summary>SQLite-basierte Projekt-Persistenz</summary>
public class ProjectService : IProjectService
{
    private readonly SQLiteAsyncConnection _db;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public ProjectService()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "smartmeasure.db");

        _db = new SQLiteAsyncConnection(dbPath);
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _db.CreateTableAsync<SurveyProject>();
        await _db.CreateTableAsync<SurveyPoint>();
        await _db.CreateTableAsync<GardenElement>();
    }

    public async Task<List<SurveyProject>> GetAllProjectsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await _db.Table<SurveyProject>()
                .OrderByDescending(p => p.ModifiedAt)
                .ToListAsync();
        }
        finally { _semaphore.Release(); }
    }

    public async Task<SurveyProject?> GetProjectAsync(int id)
    {
        await _semaphore.WaitAsync();
        try
        {
            var project = await _db.FindAsync<SurveyProject>(id);
            if (project == null) return null;

            project.Points = await _db.Table<SurveyPoint>()
                .Where(p => p.ProjectId == id)
                .OrderBy(p => p.Timestamp)
                .ToListAsync();

            project.GardenElements = await _db.Table<GardenElement>()
                .Where(e => e.ProjectId == id)
                .OrderBy(e => e.SortOrder)
                .ToListAsync();

            return project;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<SurveyProject> CreateProjectAsync(string name, string type = "Grundstueck")
    {
        var project = new SurveyProject
        {
            Name = name,
            ProjectType = type,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        await _semaphore.WaitAsync();
        try
        {
            await _db.InsertAsync(project);
            // sqlite-net setzt die ID direkt auf dem Objekt
            return project;
        }
        finally { _semaphore.Release(); }
    }

    public async Task UpdateProjectAsync(SurveyProject project)
    {
        project.ModifiedAt = DateTime.UtcNow;
        await _semaphore.WaitAsync();
        try
        {
            await _db.UpdateAsync(project);
        }
        finally { _semaphore.Release(); }
    }

    public async Task DeleteProjectAsync(int id)
    {
        await _semaphore.WaitAsync();
        try
        {
            // Punkte + Elemente zuerst loeschen
            await _db.ExecuteAsync("DELETE FROM SurveyPoint WHERE ProjectId = ?", id);
            await _db.ExecuteAsync("DELETE FROM GardenElement WHERE ProjectId = ?", id);
            await _db.DeleteAsync<SurveyProject>(id);
        }
        finally { _semaphore.Release(); }
    }

    public async Task<SurveyProject> DuplicateProjectAsync(int id, string newName)
    {
        var original = await GetProjectAsync(id);
        if (original == null)
            throw new InvalidOperationException($"Projekt {id} nicht gefunden");

        var duplicate = await CreateProjectAsync(newName, original.ProjectType);
        duplicate.Notes = original.Notes;
        await UpdateProjectAsync(duplicate);

        // Punkte kopieren
        foreach (var point in original.Points)
        {
            var copy = new SurveyPoint
            {
                ProjectId = duplicate.Id,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                Altitude = point.Altitude,
                HorizontalAccuracy = point.HorizontalAccuracy,
                VerticalAccuracy = point.VerticalAccuracy,
                TiltAngle = point.TiltAngle,
                TiltAzimuth = point.TiltAzimuth,
                FixQuality = point.FixQuality,
                SatelliteCount = point.SatelliteCount,
                MagAccuracy = point.MagAccuracy,
                Timestamp = point.Timestamp,
                Label = point.Label
            };
            await AddPointAsync(duplicate.Id, copy);
        }

        // Gartenelemente kopieren
        foreach (var element in original.GardenElements)
        {
            var copy = new GardenElement
            {
                ProjectId = duplicate.Id,
                ElementType = element.ElementType,
                PointsJson = element.PointsJson,
                Width = element.Width,
                Height = element.Height,
                TargetAltitude = element.TargetAltitude,
                Material = element.Material,
                SubType = element.SubType,
                LayerThicknessCm = element.LayerThicknessCm,
                AreaSquareMeters = element.AreaSquareMeters,
                LengthMeters = element.LengthMeters,
                VolumeMeters = element.VolumeMeters,
                Notes = element.Notes,
                SortOrder = element.SortOrder
            };
            await AddGardenElementAsync(duplicate.Id, copy);
        }

        return duplicate;
    }

    public async Task AddPointAsync(int projectId, SurveyPoint point)
    {
        point.ProjectId = projectId;
        await _semaphore.WaitAsync();
        try
        {
            await _db.InsertAsync(point);
        }
        finally { _semaphore.Release(); }
    }

    public async Task<List<SurveyPoint>> GetPointsAsync(int projectId)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await _db.Table<SurveyPoint>()
                .Where(p => p.ProjectId == projectId)
                .OrderBy(p => p.Timestamp)
                .ToListAsync();
        }
        finally { _semaphore.Release(); }
    }

    public async Task AddGardenElementAsync(int projectId, GardenElement element)
    {
        element.ProjectId = projectId;
        await _semaphore.WaitAsync();
        try
        {
            await _db.InsertAsync(element);
        }
        finally { _semaphore.Release(); }
    }

    public async Task UpdateGardenElementAsync(GardenElement element)
    {
        await _semaphore.WaitAsync();
        try
        {
            await _db.UpdateAsync(element);
        }
        finally { _semaphore.Release(); }
    }

    public async Task DeleteGardenElementAsync(int id)
    {
        await _semaphore.WaitAsync();
        try
        {
            await _db.DeleteAsync<GardenElement>(id);
        }
        finally { _semaphore.Release(); }
    }

    public async Task<List<GardenElement>> GetGardenElementsAsync(int projectId)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await _db.Table<GardenElement>()
                .Where(e => e.ProjectId == projectId)
                .OrderBy(e => e.SortOrder)
                .ToListAsync();
        }
        finally { _semaphore.Release(); }
    }
}
