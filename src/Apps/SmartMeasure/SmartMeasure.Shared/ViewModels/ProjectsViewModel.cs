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

    public ObservableCollection<SurveyProject> Projects { get; } = [];

    [ObservableProperty] private string _newProjectName = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private SurveyProject? _selectedProject;

    /// <summary>Navigation zum Projekt angefordert</summary>
    public event Action<SurveyProject>? ProjectSelected;

    /// <summary>Export-Daten bereit (Format, Inhalt)</summary>
    public event Action<string, string>? ExportReady;

    /// <summary>Export-Datei erstellt (Dateipfad)</summary>
    public event Action<string>? FileExportReady;

    private bool _isInitialized;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);

    public ProjectsViewModel(IProjectService projectService, IExportService exportService,
        IBlenderExportService blenderExportService, ITerrainService terrainService,
        ICoordinateService coordinateService)
    {
        _projectService = projectService;
        _exportService = exportService;
        _blenderExportService = blenderExportService;
        _terrainService = terrainService;
        _coordinateService = coordinateService;
    }

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

    /// <summary>Projekt als CSV exportieren</summary>
    [RelayCommand]
    private async Task ExportCsvAsync(SurveyProject? project)
    {
        if (project == null) return;

        var full = await _projectService.GetProjectAsync(project.Id);
        if (full == null) return;

        var csv = _exportService.ExportToCsv(full);
        ExportReady?.Invoke("csv", csv);
    }

    /// <summary>Projekt als GeoJSON exportieren</summary>
    [RelayCommand]
    private async Task ExportGeoJsonAsync(SurveyProject? project)
    {
        if (project == null) return;

        var full = await _projectService.GetProjectAsync(project.Id);
        if (full == null) return;

        var geoJson = _exportService.ExportToGeoJson(full);
        ExportReady?.Invoke("geojson", geoJson);
    }

    /// <summary>Projekt als OBJ+MTL fuer Blender exportieren (Terrain + Gartenelemente)</summary>
    [RelayCommand]
    private async Task ExportBlenderAsync(SurveyProject? project)
    {
        if (project == null) return;

        var full = await _projectService.GetProjectAsync(project.Id);
        if (full == null || full.Points.Count < 3) return;

        // Terrain-Mesh berechnen
        var lats = full.Points.Select(p => p.Latitude).ToArray();
        var lons = full.Points.Select(p => p.Longitude).ToArray();
        var alts = full.Points.Select(p => p.Altitude).ToArray();
        var (x, y, z) = _coordinateService.ToLocalMetric(lats, lons, alts);
        var mesh = _terrainService.CreateMesh(x, y, z);

        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartMeasure", "Exports");
        var sanitizedName = string.Join("_", project.Name.Split(Path.GetInvalidFileNameChars()));

        var objPath = await _blenderExportService.ExportObjAsync(
            mesh, full.GardenElements, outputDir, sanitizedName);

        FileExportReady?.Invoke(objPath);
    }

    /// <summary>Projekt als PDF-Vermessungsbericht exportieren</summary>
    [RelayCommand]
    private async Task ExportPdfAsync(SurveyProject? project)
    {
        if (project == null) return;

        var full = await _projectService.GetProjectAsync(project.Id);
        if (full == null) return;

        // Terrain-Mesh fuer Hoehendaten (optional)
        TerrainMesh? mesh = null;
        if (full.Points.Count >= 3)
        {
            var lats = full.Points.Select(p => p.Latitude).ToArray();
            var lons = full.Points.Select(p => p.Longitude).ToArray();
            var alts = full.Points.Select(p => p.Altitude).ToArray();
            var (x, y, z) = _coordinateService.ToLocalMetric(lats, lons, alts);
            mesh = _terrainService.CreateMesh(x, y, z);
        }

        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartMeasure", "Exports");

        var pdfPath = await _exportService.ExportPdfAsync(
            full, full.Points, full.GardenElements, mesh, outputDir);

        FileExportReady?.Invoke(pdfPath);
    }

    /// <summary>Projekt oeffnen</summary>
    [RelayCommand]
    private void OpenProject(SurveyProject? project)
    {
        if (project != null)
            ProjectSelected?.Invoke(project);
    }
}
