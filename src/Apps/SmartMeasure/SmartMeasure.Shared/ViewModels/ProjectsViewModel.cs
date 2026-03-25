using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Projektverwaltung: Liste, Erstellen, Duplizieren, Loeschen, Export</summary>
public partial class ProjectsViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly IExportService _exportService;

    public ObservableCollection<SurveyProject> Projects { get; } = [];

    [ObservableProperty] private string _newProjectName = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private SurveyProject? _selectedProject;

    /// <summary>Navigation zum Projekt angefordert</summary>
    public event Action<SurveyProject>? ProjectSelected;

    /// <summary>Export-Daten bereit (Format, Inhalt)</summary>
    public event Action<string, string>? ExportReady;

    public ProjectsViewModel(IProjectService projectService, IExportService exportService)
    {
        _projectService = projectService;
        _exportService = exportService;
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

    /// <summary>Projekt oeffnen</summary>
    [RelayCommand]
    private void OpenProject(SurveyProject? project)
    {
        if (project != null)
            ProjectSelected?.Invoke(project);
    }
}
