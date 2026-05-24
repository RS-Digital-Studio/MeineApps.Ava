using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Projektverwaltung: Liste, Erstellen, Duplizieren, Loeschen, Export (CSV, GeoJSON, Blender, PDF)</summary>
public partial class ProjectsViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IExportService _exportService;
    private readonly IBlenderExportService _blenderExportService;
    private readonly ITerrainService _terrainService;
    private readonly ICoordinateService _coordinateService;
    private readonly IGardenPlanService _gardenPlanService;
    private readonly IAppPaths _appPaths;

    public ObservableCollection<SurveyProject> Projects { get; } = [];

    [ObservableProperty] private string _newProjectName = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private SurveyProject? _selectedProject;

    /// <summary>Navigation zum Projekt angefordert</summary>
    public event Action<SurveyProject>? ProjectSelected;

    /// <summary>Export-Datei erstellt (Dateipfad)</summary>
    public event Action<string>? FileExportReady;

    /// <summary>Export fehlgeschlagen (Fehlermeldung)</summary>
    public event Action<string>? ExportFailed;

    private bool _isInitialized;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);

    private readonly IDifferentialSnapshotService _differentialService;

    public ProjectsViewModel(IProjectService projectService, IExportService exportService,
        IBlenderExportService blenderExportService, ITerrainService terrainService,
        ICoordinateService coordinateService, IGardenPlanService gardenPlanService,
        IAppPaths appPaths, IDifferentialSnapshotService differentialService)
    {
        _projectService = projectService;
        _exportService = exportService;
        _blenderExportService = blenderExportService;
        _terrainService = terrainService;
        _coordinateService = coordinateService;
        _gardenPlanService = gardenPlanService;
        _appPaths = appPaths;
        _differentialService = differentialService;
    }

    // Plan-Kap. 5.13: Differential-Snapshot zwischen zwei Projekten
    [ObservableProperty] private SurveyProject? _compareProjectA;
    [ObservableProperty] private SurveyProject? _compareProjectB;
    [ObservableProperty] private string _differentialResultText = string.Empty;

    /// <summary>Plan-Kap. 5.13: Compare-Workflow im UI. User waehlt zwei Projekte, klickt
    /// "Vergleichen" → DifferentialSnapshotService.Compare → Text-Zusammenfassung.</summary>
    [RelayCommand]
    private async Task CompareSelectedProjectsAsync()
    {
        if (CompareProjectA == null || CompareProjectB == null)
        {
            DifferentialResultText = "Bitte zwei Projekte zum Vergleich auswaehlen";
            return;
        }

        try
        {
            var a = await _projectService.GetProjectAsync(CompareProjectA.Id);
            var b = await _projectService.GetProjectAsync(CompareProjectB.Id);
            if (a == null || b == null)
            {
                DifferentialResultText = "Projekt nicht ladbar";
                return;
            }
            var result = _differentialService.Compare(a.Points, b.Points);
            var moved = result.Matches.Count(m => m.Change == DifferentialChange.Moved);
            var unchanged = result.Matches.Count - moved;
            DifferentialResultText =
                $"Vergleich: {a.Name} → {b.Name}\n" +
                $"Unveraendert: {unchanged}  ·  Verschoben (>10cm): {moved}\n" +
                $"Neu: {result.Added.Count}  ·  Verschwunden: {result.Removed.Count}";
        }
        catch (Exception ex)
        {
            DifferentialResultText = $"Vergleich fehlgeschlagen: {ex.Message}";
        }
    }

    /// <summary>GardenElements mit LocalPoints befüllen, basierend auf Messpunkt-Schwerpunkt.
    /// Stellt sicher, dass Export-Koordinaten im gleichen System wie das Terrain-Mesh liegen.</summary>
    private void PopulateLocalPoints(List<GardenElement> elements, List<SurveyPoint> points)
    {
        if (elements.Count == 0 || points.Count == 0) return;
        var refLat = points.Average(p => p.Latitude);
        var refLon = points.Average(p => p.Longitude);
        foreach (var el in elements)
            el.LocalPoints = _gardenPlanService.GetLocalPoints(el, refLat, refLon, _coordinateService);
    }

    /// <summary>Sanitized Dateiname aus Projekt-Name.</summary>
    private static string SanitizeFileName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars())).Trim();

    /// <summary>Lazy-Init: Projekte beim ersten Aufruf laden (statt Loaded-Event in View)</summary>
    public async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await LoadProjectsAsync();
    }

    /// <summary>Stellt sicher dass ein Projekt existiert und ausgewaehlt ist.
    /// Erstellt automatisch eins wenn keins vorhanden (z.B. vor AR-Capture).
    /// Thread-safe via SemaphoreSlim.</summary>
    public async Task<SurveyProject?> EnsureProjectExistsAsync()
    {
        if (SelectedProject != null) return SelectedProject;

        await _ensureLock.WaitAsync();
        try
        {
            // Double-Check nach Lock
            if (SelectedProject != null) return SelectedProject;

            await EnsureInitializedAsync();

            // Erstes Projekt waehlen wenn vorhanden
            if (Projects.Count > 0)
            {
                SelectedProject = Projects[0];
                return SelectedProject;
            }

            // Kein Projekt vorhanden → automatisch erstellen
            var name = $"AR-Vermessung {DateTime.UtcNow.ToLocalTime():dd.MM.yyyy HH:mm}";
            var project = await _projectService.CreateProjectAsync(name);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Projects.Insert(0, project);
                SelectedProject = project;
            });
            return project;
        }
        finally { _ensureLock.Release(); }
    }

    /// <summary>Projekte laden</summary>
    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        IsLoading = true;
        try
        {
            var projects = await _projectService.GetAllProjectsAsync();
            Dispatcher.UIThread.Post(() =>
            {
                Projects.Clear();
                foreach (var p in projects)
                    Projects.Add(p);
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Neues Projekt erstellen</summary>
    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName)) return;

        var project = await _projectService.CreateProjectAsync(NewProjectName);
        NewProjectName = string.Empty;
        Dispatcher.UIThread.Post(() => Projects.Insert(0, project));
    }

    /// <summary>Projekt duplizieren (Planungsvariante)</summary>
    [RelayCommand]
    private async Task DuplicateProjectAsync(SurveyProject? project)
    {
        if (project == null) return;

        var duplicate = await _projectService.DuplicateProjectAsync(
            project.Id, $"{project.Name} (Kopie)");
        Dispatcher.UIThread.Post(() => Projects.Insert(0, duplicate));
    }

    /// <summary>Projekt loeschen</summary>
    [RelayCommand]
    private async Task DeleteProjectAsync(SurveyProject? project)
    {
        if (project == null) return;

        await _projectService.DeleteProjectAsync(project.Id);
        Dispatcher.UIThread.Post(() => Projects.Remove(project));
    }

    /// <summary>
    /// Projekt als CSV-Datei exportieren. Schreibt in IAppPaths.ExportFolder und meldet
    /// den resultierenden Pfad via FileExportReady.
    /// </summary>
    [RelayCommand]
    private async Task ExportCsvAsync(SurveyProject? project)
    {
        if (project == null) return;

        try
        {
            var full = await _projectService.GetProjectAsync(project.Id);
            if (full == null)
            {
                ExportFailed?.Invoke("Projekt konnte nicht geladen werden");
                return;
            }

            var csv = _exportService.ExportToCsv(full);
            var path = await WriteExportFileAsync(project.Name, "csv", csv);
            FileExportReady?.Invoke(path);
        }
        catch (Exception ex)
        {
            ExportFailed?.Invoke($"CSV-Export fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>Projekt als GeoJSON-Datei exportieren.</summary>
    [RelayCommand]
    private async Task ExportGeoJsonAsync(SurveyProject? project)
    {
        if (project == null) return;

        try
        {
            var full = await _projectService.GetProjectAsync(project.Id);
            if (full == null)
            {
                ExportFailed?.Invoke("Projekt konnte nicht geladen werden");
                return;
            }

            var geoJson = _exportService.ExportToGeoJson(full);
            var path = await WriteExportFileAsync(project.Name, "geojson", geoJson);
            FileExportReady?.Invoke(path);
        }
        catch (Exception ex)
        {
            ExportFailed?.Invoke($"GeoJSON-Export fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>Projekt als DXF exportieren (AutoCAD/Allplan/Revit-kompatibel).</summary>
    [RelayCommand]
    private async Task ExportDxfAsync(SurveyProject? project)
    {
        if (project == null) return;

        try
        {
            var full = await _projectService.GetProjectAsync(project.Id);
            if (full == null)
            {
                ExportFailed?.Invoke("Projekt konnte nicht geladen werden");
                return;
            }

            var dxf = _exportService.ExportToDxf(full);
            var path = await WriteExportFileAsync(project.Name, "dxf", dxf);
            FileExportReady?.Invoke(path);
        }
        catch (Exception ex)
        {
            ExportFailed?.Invoke($"DXF-Export fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>Projekt als KMZ exportieren (Google Earth/Maps).</summary>
    [RelayCommand]
    private async Task ExportKmzAsync(SurveyProject? project)
    {
        if (project == null) return;

        try
        {
            var full = await _projectService.GetProjectAsync(project.Id);
            if (full == null)
            {
                ExportFailed?.Invoke("Projekt konnte nicht geladen werden");
                return;
            }

            var path = await _exportService.ExportToKmzAsync(full, _appPaths.ExportFolder);
            FileExportReady?.Invoke(path);
        }
        catch (Exception ex)
        {
            ExportFailed?.Invoke($"KMZ-Export fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>Projekt als OBJ+MTL für Blender exportieren (Terrain + Gartenelemente).</summary>
    [RelayCommand]
    private async Task ExportBlenderAsync(SurveyProject? project)
    {
        if (project == null) return;

        try
        {
            var full = await _projectService.GetProjectAsync(project.Id);
            if (full == null || full.Points.Count < 3)
            {
                ExportFailed?.Invoke("Mindestens 3 Punkte für 3D-Export nötig");
                return;
            }

            // Mesh auf Background-Thread berechnen — Delaunay kann bei 200+ Punkten sichtbar
            // lange dauern und würde sonst die UI blockieren.
            var points = full.Points;
            var elements = full.GardenElements;
            var sanitizedName = SanitizeFileName(project.Name);
            var outputDir = _appPaths.ExportFolder;

            // GardenElements in Mesh-Koordinatensystem mappen (gleicher Schwerpunkt wie Mesh)
            PopulateLocalPoints(elements, points);

            var objPath = await Task.Run(async () =>
            {
                var lats = points.Select(p => p.Latitude).ToArray();
                var lons = points.Select(p => p.Longitude).ToArray();
                var alts = points.Select(p => p.Altitude).ToArray();
                var (x, y, z) = _coordinateService.ToLocalMetric(lats, lons, alts);
                var mesh = _terrainService.CreateMesh(x, y, z);
                Directory.CreateDirectory(outputDir);
                return await _blenderExportService.ExportObjAsync(mesh, elements, outputDir, sanitizedName);
            });

            FileExportReady?.Invoke(objPath);
        }
        catch (Exception ex)
        {
            ExportFailed?.Invoke($"Blender-Export fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>Projekt als PDF-Vermessungsbericht exportieren.</summary>
    [RelayCommand]
    private async Task ExportPdfAsync(SurveyProject? project)
    {
        if (project == null) return;

        try
        {
            var full = await _projectService.GetProjectAsync(project.Id);
            if (full == null)
            {
                ExportFailed?.Invoke("Projekt konnte nicht geladen werden");
                return;
            }

            var points = full.Points;
            var elements = full.GardenElements;
            var outputDir = _appPaths.ExportFolder;

            // Mesh + PDF-Render auf Background-Thread (PDF-Serialisierung ist I/O-lastig)
            var pdfPath = await Task.Run(async () =>
            {
                TerrainMesh? mesh = null;
                if (points.Count >= 3)
                {
                    var lats = points.Select(p => p.Latitude).ToArray();
                    var lons = points.Select(p => p.Longitude).ToArray();
                    var alts = points.Select(p => p.Altitude).ToArray();
                    var (x, y, z) = _coordinateService.ToLocalMetric(lats, lons, alts);
                    mesh = _terrainService.CreateMesh(x, y, z);
                }
                Directory.CreateDirectory(outputDir);
                return await _exportService.ExportPdfAsync(full, points, elements, mesh, outputDir);
            });

            FileExportReady?.Invoke(pdfPath);
        }
        catch (Exception ex)
        {
            ExportFailed?.Invoke($"PDF-Export fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Schreibt Export-Content in IAppPaths.ExportFolder. Sanitized Dateiname + Timestamp
    /// verhindert Kollisionen zwischen mehreren Exports desselben Projekts.
    /// </summary>
    private async Task<string> WriteExportFileAsync(string projectName, string extension, string content)
    {
        var sanitized = SanitizeFileName(projectName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{sanitized}_{timestamp}.{extension}";
        var path = Path.Combine(_appPaths.ExportFolder, fileName);

        Directory.CreateDirectory(_appPaths.ExportFolder);
        await File.WriteAllTextAsync(path, content, System.Text.Encoding.UTF8);
        return path;
    }

    /// <summary>Projekt oeffnen</summary>
    [RelayCommand]
    private void OpenProject(SurveyProject? project)
    {
        if (project != null)
            ProjectSelected?.Invoke(project);
    }
}
