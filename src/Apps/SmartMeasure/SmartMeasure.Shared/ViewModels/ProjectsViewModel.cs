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

    public ProjectsViewModel(IProjectService projectService, IExportService exportService,
        IBlenderExportService blenderExportService, ITerrainService terrainService,
        ICoordinateService coordinateService, IAppPaths appPaths)
    {
        _projectService = projectService;
        _exportService = exportService;
        _blenderExportService = blenderExportService;
        _terrainService = terrainService;
        _coordinateService = coordinateService;
        _appPaths = appPaths;
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
