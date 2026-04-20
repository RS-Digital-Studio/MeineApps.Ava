using SmartMeasure.Shared.Models;
using SQLite;

namespace SmartMeasure.Shared.Services;

/// <summary>SQLite-basierte Projekt-Persistenz</summary>
public class ProjectService : IProjectService
{
    private readonly SQLiteAsyncConnection _db;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Task _initTask;

    public ProjectService(IAppPaths paths)
    {
        // IAppPaths liefert sandbox-sicheren Pfad auf Android (Context.FilesDir)
        // und ApplicationData auf Desktop (Windows %APPDATA% / Linux ~/.config)
        _db = new SQLiteAsyncConnection(paths.DatabasePath);
        _initTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _db.CreateTableAsync<SurveyProject>();
        await _db.CreateTableAsync<SurveyPoint>();
        await _db.CreateTableAsync<GardenElement>();
    }

    private async Task EnsureInitializedAsync()
    {
        await _initTask;
    }

    public async Task<List<SurveyProject>> GetAllProjectsAsync()
    {
        await EnsureInitializedAsync();
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
        await EnsureInitializedAsync();
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
        await EnsureInitializedAsync();
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
        await EnsureInitializedAsync();
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
        await EnsureInitializedAsync();
        await _semaphore.WaitAsync();
        try
        {
            // Atomar: Punkte, Elemente und Projekt werden gemeinsam gelöscht oder gar nicht.
            // App-Crash zwischen den Löschungen würde sonst verwaiste Zeilen hinterlassen.
            await _db.RunInTransactionAsync(db =>
            {
                db.Execute("DELETE FROM SurveyPoint WHERE ProjectId = ?", id);
                db.Execute("DELETE FROM GardenElement WHERE ProjectId = ?", id);
                db.Delete<SurveyProject>(id);
            });
        }
        finally { _semaphore.Release(); }
    }

    public async Task<SurveyProject> DuplicateProjectAsync(int id, string newName)
    {
        await EnsureInitializedAsync();

        // Alles atomar in einer Transaktion + einem Semaphore-Lock
        await _semaphore.WaitAsync();
        try
        {
            // Original laden (ohne Semaphore, da wir ihn bereits halten)
            var original = await _db.FindAsync<SurveyProject>(id);
            if (original == null)
                throw new InvalidOperationException($"Projekt {id} nicht gefunden");

            original.Points = await _db.Table<SurveyPoint>()
                .Where(p => p.ProjectId == id)
                .OrderBy(p => p.Timestamp)
                .ToListAsync();

            original.GardenElements = await _db.Table<GardenElement>()
                .Where(e => e.ProjectId == id)
                .OrderBy(e => e.SortOrder)
                .ToListAsync();

            // Duplikat erstellen
            var duplicate = new SurveyProject
            {
                Name = newName,
                ProjectType = original.ProjectType,
                Notes = original.Notes,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };

            await _db.RunInTransactionAsync(db =>
            {
                db.Insert(duplicate);
                // sqlite-net setzt ID direkt auf dem Objekt

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
                    db.Insert(copy);
                }

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
                    db.Insert(copy);
                }
            });

            return duplicate;
        }
        finally { _semaphore.Release(); }
    }

    public async Task AddPointAsync(int projectId, SurveyPoint point)
    {
        await EnsureInitializedAsync();
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
        await EnsureInitializedAsync();
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
        await EnsureInitializedAsync();
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
        await EnsureInitializedAsync();
        await _semaphore.WaitAsync();
        try
        {
            await _db.UpdateAsync(element);
        }
        finally { _semaphore.Release(); }
    }

    public async Task DeleteGardenElementAsync(int id)
    {
        await EnsureInitializedAsync();
        await _semaphore.WaitAsync();
        try
        {
            await _db.DeleteAsync<GardenElement>(id);
        }
        finally { _semaphore.Release(); }
    }

    public async Task<List<GardenElement>> GetGardenElementsAsync(int projectId)
    {
        await EnsureInitializedAsync();
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
