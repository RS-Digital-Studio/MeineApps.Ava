using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerRechner.ViewModels;

/// <summary>
/// ViewModel for the project management page
/// </summary>
public sealed partial class ProjectsViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly ILocalizationService _localization;
    private readonly IMaterialExportService _exportService;
    private readonly IFileShareService _fileShareService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPurchaseService _purchaseService;
    private readonly IPhotoPickerService _photoPickerService;

    /// <summary>
    /// Raised when the VM wants to navigate to a page.
    /// The string parameter is the route/page name (may include query params).
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event for showing alerts/messages to the user (title, message)
    /// </summary>
    public event Action<string, string>? MessageRequested;

    [ObservableProperty]
    private ObservableCollection<Project> _projects = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasProjects;

    [ObservableProperty]
    private bool _showDeleteConfirmation;

    [ObservableProperty]
    private Project? _projectToDelete;

    /// <summary>
    /// True when not loading and no projects exist
    /// </summary>
    public bool ShowEmptyState => !IsLoading && !HasProjects;

    public ProjectsViewModel(
        IProjectService projectService,
        ILocalizationService localization,
        IMaterialExportService exportService,
        IFileShareService fileShareService,
        IRewardedAdService rewardedAdService,
        IPurchaseService purchaseService,
        IPhotoPickerService photoPickerService)
    {
        _projectService = projectService;
        _localization = localization;
        _exportService = exportService;
        _fileShareService = fileShareService;
        _rewardedAdService = rewardedAdService;
        _purchaseService = purchaseService;
        _photoPickerService = photoPickerService;
    }

    private void NavigateTo(string route) => NavigationRequested?.Invoke(route);

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            var projects = await _projectService.LoadAllProjectsAsync();
            Projects = new ObservableCollection<Project>(projects);
            HasProjects = Projects.Count > 0;
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(_localization.GetString("Error"), _localization.GetString("ProjectLoadFailed"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void LoadProject(Project? project)
    {
        if (project == null) return;

        try
        {
            var route = GetRouteForCalculatorType(project.CalculatorType);
            if (!string.IsNullOrEmpty(route))
            {
                NavigateTo($"{route}?projectId={project.Id}");
            }
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(_localization.GetString("Error"), _localization.GetString("ProjectLoadFailed"));
        }
    }

    [RelayCommand]
    private void DeleteProject(Project? project)
    {
        if (project == null) return;
        ProjectToDelete = project;
        ShowDeleteConfirmation = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteProject()
    {
        if (ProjectToDelete == null) return;

        ShowDeleteConfirmation = false;

        try
        {
            await _projectService.DeleteProjectAsync(ProjectToDelete.Id);
            Projects.Remove(ProjectToDelete);
            HasProjects = Projects.Count > 0;
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error"),
                _localization.GetString("ProjectDeleteFailed"));
        }
        finally
        {
            ProjectToDelete = null;
        }
    }

    [RelayCommand]
    private void CancelDeleteProject()
    {
        ShowDeleteConfirmation = false;
        ProjectToDelete = null;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigateTo("..");
    }

    [RelayCommand]
    private async Task ExportProject(Project? project)
    {
        if (project == null) return;

        try
        {
            // Ad-Gate: Premium direkt, Free nach Rewarded Ad
            if (!_purchaseService.IsPremium)
            {
                var adResult = await _rewardedAdService.ShowAdAsync("project_export");
                if (!adResult) return;
            }

            // Projekt-Daten als Inputs/Results aufbereiten
            var data = project.GetData();
            var inputs = new Dictionary<string, string>();
            var results = new Dictionary<string, string>();

            if (data != null)
            {
                foreach (var kvp in data)
                {
                    if (kvp.Key == "Result" && kvp.Value is JsonElement resultElement)
                    {
                        // Result-Dictionary entpacken
                        if (resultElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in resultElement.EnumerateObject())
                            {
                                var label = _localization.GetString(prop.Name) ?? prop.Name;
                                results[label] = prop.Value.ToString();
                            }
                        }
                    }
                    else if (kvp.Key != "SelectedCalculator")
                    {
                        var label = _localization.GetString(kvp.Key) ?? kvp.Key;
                        inputs[label] = kvp.Value?.ToString() ?? "";
                    }
                }
            }

            var calcTypeName = _localization.GetString("CalcType" + project.CalculatorType) ?? project.CalculatorType.ToString();
            var path = await _exportService.ExportProjectToPdfAsync(project.Name, calcTypeName, inputs, results);
            await _fileShareService.ShareFileAsync(path, _localization.GetString("ExportProject") ?? "Share Project", "application/pdf");
            MessageRequested?.Invoke(_localization.GetString("Success") ?? "Success", _localization.GetString("PdfExportSuccess") ?? "PDF exported!");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(_localization.GetString("Error") ?? "Error", _localization.GetString("PdfExportFailed") ?? "Export failed.");
        }
    }

    private static string GetRouteForCalculatorType(CalculatorType calculatorType) => calculatorType switch
    {
        // FREE
        CalculatorType.Tiles => "TileCalculatorPage",
        CalculatorType.Wallpaper => "WallpaperCalculatorPage",
        CalculatorType.Paint => "PaintCalculatorPage",
        CalculatorType.Flooring => "FlooringCalculatorPage",

        // PREMIUM
        CalculatorType.DrywallFraming => "DrywallPage",
        CalculatorType.Baseboard => "DrywallPage",
        CalculatorType.VoltageDrop => "ElectricalPage",
        CalculatorType.PowerCost => "ElectricalPage",
        CalculatorType.OhmsLaw => "ElectricalPage",
        CalculatorType.MetalWeight => "MetalPage",
        CalculatorType.ThreadDrill => "MetalPage",
        CalculatorType.Paving => "GardenPage",
        CalculatorType.Soil => "GardenPage",
        CalculatorType.PondLiner => "GardenPage",
        CalculatorType.RoofPitch => "RoofSolarPage",
        CalculatorType.RoofTiles => "RoofSolarPage",
        CalculatorType.SolarYield => "RoofSolarPage",
        CalculatorType.ConcreteSlab => "ConcretePage",
        CalculatorType.ConcreteStrip => "ConcretePage",
        CalculatorType.ConcreteColumn => "ConcretePage",
        CalculatorType.Stairs => "StairsPage",
        CalculatorType.Plaster => "PlasterPage",
        CalculatorType.Screed => "ScreedPage",
        CalculatorType.Insulation => "InsulationPage",
        CalculatorType.CableSizing => "CableSizingPage",
        CalculatorType.Grout => "GroutPage",

        // Profi-Werkzeuge
        CalculatorType.HourlyRate => "HourlyRatePage",
        CalculatorType.MaterialCompare => "MaterialComparePage",
        CalculatorType.AreaMeasure => "AreaMeasurePage",

        _ => string.Empty
    };

    #region Notizen

    [ObservableProperty]
    private bool _showNotesEditor;

    [ObservableProperty]
    private string _editingNotes = "";

    [ObservableProperty]
    private Project? _notesProject;

    /// <summary>Fotos des aktuellen Projekts im Notizen-Editor</summary>
    public ObservableCollection<string> CurrentProjectPhotos { get; } = [];

    /// <summary>Öffnet den Notizen-Editor für ein Projekt</summary>
    [RelayCommand]
    private void EditNotes(Project? project)
    {
        if (project == null) return;
        NotesProject = project;
        EditingNotes = project.Notes;

        // Fotos laden
        CurrentProjectPhotos.Clear();
        foreach (var photo in project.PhotoPaths)
            CurrentProjectPhotos.Add(photo);

        ShowNotesEditor = true;
    }

    /// <summary>Speichert die Notizen</summary>
    [RelayCommand]
    private async Task SaveNotesAsync()
    {
        if (NotesProject == null) return;

        NotesProject.Notes = EditingNotes.Trim();
        await _projectService.SaveProjectAsync(NotesProject);
        ShowNotesEditor = false;
        CurrentProjectPhotos.Clear();
        NotesProject = null;

        // Projektliste aktualisieren damit Notiz-Vorschau sichtbar wird
        await LoadProjectsAsync();
    }

    /// <summary>Bricht den Notizen-Editor ab</summary>
    [RelayCommand]
    private void CancelNotesEdit()
    {
        ShowNotesEditor = false;
        NotesProject = null;
        CurrentProjectPhotos.Clear();
    }

    #endregion

    #region Foto-Dokumentation

    /// <summary>Öffnet den Photo-Picker und fügt ein Foto zum Projekt hinzu (max. 5)</summary>
    [RelayCommand]
    private async Task AddPhoto(Project? project)
    {
        if (project == null) return;

        if (project.PhotoPaths.Count >= 5)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Fehler",
                _localization.GetString("MaxPhotosReached") ?? "Maximal 5 Fotos pro Projekt");
            return;
        }

        var photoPath = await _photoPickerService.PickPhotoAsync();
        if (string.IsNullOrEmpty(photoPath)) return;

        project.PhotoPaths.Add(photoPath);
        CurrentProjectPhotos.Add(photoPath);
        await _projectService.SaveProjectAsync(project);
    }

    /// <summary>Löscht ein Foto aus dem Projekt und vom Dateisystem</summary>
    [RelayCommand]
    private async Task DeletePhoto(string? photoPath)
    {
        if (string.IsNullOrEmpty(photoPath) || NotesProject == null) return;

        NotesProject.PhotoPaths.Remove(photoPath);
        CurrentProjectPhotos.Remove(photoPath);
        await _photoPickerService.DeletePhotoAsync(photoPath);
        await _projectService.SaveProjectAsync(NotesProject);
    }

    #endregion
}
