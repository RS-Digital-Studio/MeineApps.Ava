using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;

namespace HandwerkerRechner.ViewModels;

/// <summary>
/// ViewModel für Projekt-Vorlagen (eingebaute + benutzerdefinierte).
/// Zeigt Vorlagen gruppiert nach Kategorie an und erlaubt das Anwenden.
/// </summary>
public sealed partial class ProjectTemplatesViewModel : ViewModelBase
{
    private readonly IProjectTemplateService _templateService;
    private readonly IProjectService _projectService;
    private readonly ILocalizationService _localization;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;

    public ObservableCollection<ProjectTemplate> BuiltinTemplates { get; } = [];
    public ObservableCollection<ProjectTemplate> CustomTemplates { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasCustomTemplates;

    // Dialog zum Anwenden einer Vorlage
    [ObservableProperty] private bool _showApplyDialog;
    [ObservableProperty] private string _newProjectName = "";
    [ObservableProperty] private ProjectTemplate? _selectedTemplate;

    #region Lokalisierte Texte

    public string PageTitle => _localization.GetString("ProjectTemplates") ?? "Vorlagen";
    public string BuiltinLabel => _localization.GetString("BuiltinTemplates") ?? "Eingebaute Vorlagen";
    public string CustomLabel => _localization.GetString("CustomTemplates") ?? "Eigene Vorlagen";
    public string ApplyLabel => _localization.GetString("ApplyTemplate") ?? "Vorlage anwenden";
    public string NoCustomLabel => _localization.GetString("NoCustomTemplates") ?? "Noch keine eigenen Vorlagen";
    public string ProjectNameLabel => _localization.GetString("ProjectName") ?? "Projektname";

    #endregion

    public ProjectTemplatesViewModel(
        IProjectTemplateService templateService,
        IProjectService projectService,
        ILocalizationService localization)
    {
        _templateService = templateService;
        _projectService = projectService;
        _localization = localization;
    }

    [RelayCommand]
    public async Task LoadTemplatesAsync()
    {
        IsLoading = true;
        try
        {
            BuiltinTemplates.Clear();
            CustomTemplates.Clear();

            var all = await _templateService.GetAllTemplatesAsync();
            foreach (var t in all)
            {
                if (t.IsCustom)
                    CustomTemplates.Add(t);
                else
                    BuiltinTemplates.Add(t);
            }
            HasCustomTemplates = CustomTemplates.Count > 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Zeigt den Dialog zum Benennen und Anwenden einer Vorlage</summary>
    [RelayCommand]
    private void SelectTemplate(ProjectTemplate? template)
    {
        if (template == null) return;
        SelectedTemplate = template;
        NewProjectName = GetLocalizedTemplateName(template);
        ShowApplyDialog = true;
    }

    /// <summary>Wendet die gewählte Vorlage an: Erstellt ein Projekt und navigiert zum ersten Rechner</summary>
    [RelayCommand]
    private async Task ConfirmApplyTemplateAsync()
    {
        if (SelectedTemplate == null || string.IsNullOrWhiteSpace(NewProjectName)) return;

        var firstCalc = SelectedTemplate.Calculators.FirstOrDefault();
        if (firstCalc == null)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Fehler",
                _localization.GetString("TemplateEmpty") ?? "Vorlage enthält keine Rechner");
            return;
        }

        // Projekt erstellen mit den Standardwerten des ersten Rechners
        var project = new Project
        {
            Name = NewProjectName.Trim(),
            CalculatorType = firstCalc.CalculatorType
        };

        var data = new Dictionary<string, object>();
        foreach (var (key, value) in firstCalc.DefaultValues)
            data[key] = value;

        project.SetData(data);
        await _projectService.SaveProjectAsync(project);

        ShowApplyDialog = false;

        FloatingTextRequested?.Invoke(
            _localization.GetString("TemplateApplied") ?? "Vorlage angewendet!", "success");

        // Zum ersten Rechner navigieren mit der Projekt-ID
        NavigationRequested?.Invoke($"{firstCalc.Route}?projectId={project.Id}");
    }

    [RelayCommand]
    private void CancelApplyDialog()
    {
        ShowApplyDialog = false;
        SelectedTemplate = null;
    }

    [RelayCommand]
    private async Task DeleteCustomTemplateAsync(ProjectTemplate? template)
    {
        if (template == null || !template.IsCustom) return;
        await _templateService.DeleteCustomTemplateAsync(template.Id);
        await LoadTemplatesAsync();
        FloatingTextRequested?.Invoke(
            _localization.GetString("TemplateDeleted") ?? "Vorlage gelöscht", "info");
    }

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke("..");

    /// <summary>Gibt den lokalisierten Namen einer Vorlage zurück</summary>
    public string GetLocalizedTemplateName(ProjectTemplate template)
    {
        if (!string.IsNullOrEmpty(template.NameKey))
        {
            var localized = _localization.GetString(template.NameKey);
            if (!string.IsNullOrEmpty(localized)) return localized;
        }
        return template.Name;
    }

    /// <summary>Gibt die lokalisierte Beschreibung einer Vorlage zurück</summary>
    public string GetLocalizedTemplateDescription(ProjectTemplate template)
    {
        if (!string.IsNullOrEmpty(template.DescriptionKey))
        {
            var localized = _localization.GetString(template.DescriptionKey);
            if (!string.IsNullOrEmpty(localized)) return localized;
        }
        return "";
    }
}
