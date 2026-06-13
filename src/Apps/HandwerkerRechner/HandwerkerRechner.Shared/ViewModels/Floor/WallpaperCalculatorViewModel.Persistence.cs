using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Floor;

public sealed partial class WallpaperCalculatorViewModel
{
    [ObservableProperty]
    private bool _showSaveDialog;

    [ObservableProperty]
    private string _saveProjectName = string.Empty;

    private string DefaultProjectName => _localization.GetString("CalcWallpaper");

    /// <summary>
    /// Load project data from a project ID (replaces IQueryAttributable)
    /// </summary>
    public async Task LoadFromProjectIdAsync(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
            return;

        _currentProjectId = projectId;
        try
        {
            await LoadProjectAsync(projectId);
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] {ex.Message}");
#endif
        }
    }

    private async Task SaveToHistoryAsync()
    {
        try
        {
            var title = string.Format(_localization.GetString("HistoryWallHeight") ?? "{0} m wall, {1} m height", WallLength.ToString("F1"), RoomHeight.ToString("F1"));
            var data = new Dictionary<string, object>
            {
                ["WallLength"] = WallLength,
                ["RoomHeight"] = RoomHeight,
                ["RollLength"] = RollLength,
                ["RollWidth"] = RollWidth,
                ["PatternRepeat"] = PatternRepeat,
                ["PricePerRoll"] = PricePerRoll,
                ["Result"] = Result != null ? new Dictionary<string, object>
                {
                    ["WallArea"] = Result.WallArea,
                    ["StripsNeeded"] = Result.StripsNeeded,
                    ["RollsNeeded"] = Result.RollsNeeded
                } : new Dictionary<string, object>()
            };

            if (ShowDeductions)
            {
                data["DoorCount"] = DoorCount;
                data["DoorWidth"] = DoorWidth;
                data["DoorHeight"] = DoorHeight;
                data["WindowCount"] = WindowCount;
                data["WindowWidth"] = WindowWidth;
                data["WindowHeight"] = WindowHeight;
            }

            _historyService.ScheduleDebouncedSave("WallpaperCalculator", title, data);
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] {ex.Message}");
#endif
        }
    }

    [RelayCommand]
    private void SaveProject()
    {
        if (!HasResult) return;

        SaveProjectName = _currentProjectId != null ? "" : DefaultProjectName;
        ShowSaveDialog = true;
    }

    [RelayCommand]
    private async Task ConfirmSaveProject()
    {
        var name = SaveProjectName;
        if (string.IsNullOrWhiteSpace(name))
            name = DefaultProjectName;

        ShowSaveDialog = false;

        try
        {
            var project = new Project
            {
                Name = name,
                CalculatorType = CalculatorType.Wallpaper
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["WallLength"] = WallLength,
                ["RoomHeight"] = RoomHeight,
                ["RollLength"] = RollLength,
                ["RollWidth"] = RollWidth,
                ["PatternRepeat"] = PatternRepeat,
                ["PricePerRoll"] = PricePerRoll
            };

            if (ShowDeductions)
            {
                data["DoorCount"] = DoorCount;
                data["DoorWidth"] = DoorWidth;
                data["DoorHeight"] = DoorHeight;
                data["WindowCount"] = WindowCount;
                data["WindowWidth"] = WindowWidth;
                data["WindowHeight"] = WindowHeight;
            }

            // Result-Daten mitspeichern für Export
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["WallArea"] = Result.WallArea.ToString("F2"),
                    ["StripsNeeded"] = Result.StripsNeeded,
                    ["RollsNeeded"] = Result.RollsNeeded
                };
                if (ShowCost && PricePerRoll > 0)
                    resultData["TotalCost"] = (Result.RollsNeeded * PricePerRoll).ToString("F2");
                data["Result"] = resultData;
            }

            project.SetData(data);
            await _projectService.SaveProjectAsync(project);
            _currentProjectId = project.Id;

            MessageRequested?.Invoke(
                _localization.GetString("Success"),
                _localization.GetString("ProjectSaved"));
            FloatingTextRequested?.Invoke(_localization.GetString("ProjectSaved"), "success");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error"),
                _localization.GetString("ProjectSaveFailed"));
        }
    }

    [RelayCommand]
    private void CancelSaveProject()
    {
        ShowSaveDialog = false;
        SaveProjectName = string.Empty;
    }

    private async Task LoadProjectAsync(string projectId)
    {
        try
        {
            var project = await _projectService.LoadProjectAsync(projectId);
            if (project == null)
                return;

            WallLength = project.GetValue("WallLength", 14.0);
            RoomHeight = project.GetValue("RoomHeight", 2.5);
            RollLength = project.GetValue("RollLength", 10.05);
            RollWidth = project.GetValue("RollWidth", 53.0);
            PatternRepeat = project.GetValue("PatternRepeat", 0.0);
            PricePerRoll = project.GetValue("PricePerRoll", 0.0);

            DoorCount = project.GetValue("DoorCount", 0);
            DoorWidth = project.GetValue("DoorWidth", 0.8);
            DoorHeight = project.GetValue("DoorHeight", 2.0);
            WindowCount = project.GetValue("WindowCount", 0);
            WindowWidth = project.GetValue("WindowWidth", 1.2);
            WindowHeight = project.GetValue("WindowHeight", 1.0);
            ShowDeductions = DoorCount > 0 || WindowCount > 0;

            await Calculate();
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] {ex.Message}");
#endif
        }
    }
}
