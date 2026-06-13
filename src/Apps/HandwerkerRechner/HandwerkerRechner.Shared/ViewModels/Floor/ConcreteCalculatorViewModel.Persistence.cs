using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Floor;

public sealed partial class ConcreteCalculatorViewModel
{
    [ObservableProperty]
    private bool _showSaveDialog;

    [ObservableProperty]
    private string _saveProjectName = string.Empty;

    private string DefaultProjectName => Calculators[SelectedCalculator];

    /// <summary>
    /// Projektdaten per ID laden (ersetzt IQueryAttributable)
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
            string calcType, title;
            Dictionary<string, object> data;

            switch (SelectedCalculator)
            {
                case 0: // Platte
                    calcType = "ConcreteSlabCalculator";
                    title = $"{SlabLength:F1} × {SlabWidth:F1} m, h={SlabHeight} cm → {Result?.VolumeM3:F2} m³";
                    data = new Dictionary<string, object>
                    {
                        ["SlabLength"] = SlabLength,
                        ["SlabWidth"] = SlabWidth,
                        ["SlabHeight"] = SlabHeight,
                        ["BagWeight"] = BagWeight,
                        ["PricePerBag"] = PricePerBag,
                        ["PricePerCubicMeter"] = PricePerCubicMeter,
                        ["Result"] = Result != null ? new Dictionary<string, object>
                        {
                            ["VolumeM3"] = Result.VolumeM3,
                            ["BagsNeeded"] = Result.BagsNeeded
                        } : new Dictionary<string, object>()
                    };
                    break;

                case 1: // Streifenfundament
                    calcType = "StripFoundationCalculator";
                    title = $"{StripLength:F1} m, {StripWidth}×{StripDepth} cm → {Result?.VolumeM3:F2} m³";
                    data = new Dictionary<string, object>
                    {
                        ["StripLength"] = StripLength,
                        ["StripWidth"] = StripWidth,
                        ["StripDepth"] = StripDepth,
                        ["BagWeight"] = BagWeight,
                        ["PricePerBag"] = PricePerBag,
                        ["PricePerCubicMeter"] = PricePerCubicMeter,
                        ["Result"] = Result != null ? new Dictionary<string, object>
                        {
                            ["VolumeM3"] = Result.VolumeM3,
                            ["BagsNeeded"] = Result.BagsNeeded
                        } : new Dictionary<string, object>()
                    };
                    break;

                default: // Säule
                    calcType = "ConcreteColumnCalculator";
                    title = $"Ø{ColumnDiameter} cm, h={ColumnHeight} cm → {Result?.VolumeM3:F2} m³";
                    data = new Dictionary<string, object>
                    {
                        ["ColumnDiameter"] = ColumnDiameter,
                        ["ColumnHeight"] = ColumnHeight,
                        ["BagWeight"] = BagWeight,
                        ["PricePerBag"] = PricePerBag,
                        ["PricePerCubicMeter"] = PricePerCubicMeter,
                        ["Result"] = Result != null ? new Dictionary<string, object>
                        {
                            ["VolumeM3"] = Result.VolumeM3,
                            ["BagsNeeded"] = Result.BagsNeeded
                        } : new Dictionary<string, object>()
                    };
                    break;
            }

            _historyService.ScheduleDebouncedSave(calcType, title, data);
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
            var calcType = SelectedCalculator switch
            {
                0 => CalculatorType.ConcreteSlab,
                1 => CalculatorType.ConcreteStrip,
                2 => CalculatorType.ConcreteColumn,
                _ => CalculatorType.ConcreteSlab
            };

            var project = new Project
            {
                Name = name,
                CalculatorType = calcType
            };

            if (!string.IsNullOrEmpty(_currentProjectId))
            {
                project.Id = _currentProjectId;
            }

            var data = new Dictionary<string, object>
            {
                ["SelectedCalculator"] = SelectedCalculator,
                // Platte
                ["SlabLength"] = SlabLength,
                ["SlabWidth"] = SlabWidth,
                ["SlabHeight"] = SlabHeight,
                // Fundament
                ["StripLength"] = StripLength,
                ["StripWidth"] = StripWidth,
                ["StripDepth"] = StripDepth,
                // Säule
                ["ColumnDiameter"] = ColumnDiameter,
                ["ColumnHeight"] = ColumnHeight,
                // Gemeinsam
                ["BagWeight"] = BagWeight,
                ["PricePerBag"] = PricePerBag,
                ["PricePerCubicMeter"] = PricePerCubicMeter
            };

            // Result-Daten mitspeichern für Export (nur aktiver Sub-Rechner)
            if (HasResult && Result != null)
            {
                var resultData = new Dictionary<string, object>
                {
                    ["ResultVolume"] = Result.VolumeM3.ToString("F2"),
                    ["ResultCite"] = Result.CementKg.ToString("F1"),
                    ["ResultSand"] = Result.SandKg.ToString("F1"),
                    ["ResultGravel"] = Result.GravelKg.ToString("F1"),
                    ["ResultWater"] = Result.WaterLiters.ToString("F1"),
                    ["ResultBags"] = $"{Result.BagsNeeded} × {Result.BagWeight} kg"
                };
                if (ShowBagCost && PricePerBag > 0)
                    resultData["CostBags"] = (Result.BagsNeeded * PricePerBag).ToString("F2");
                if (ShowCubicMeterCost && PricePerCubicMeter > 0)
                    resultData["CostCubicMeter"] = (Result.VolumeM3 * PricePerCubicMeter).ToString("F2");
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

            SelectedCalculator = project.GetValue("SelectedCalculator", 0);

            // Platte
            SlabLength = project.GetValue("SlabLength", 4.0);
            SlabWidth = project.GetValue("SlabWidth", 3.0);
            SlabHeight = project.GetValue("SlabHeight", 15.0);
            // Fundament
            StripLength = project.GetValue("StripLength", 20.0);
            StripWidth = project.GetValue("StripWidth", 30.0);
            StripDepth = project.GetValue("StripDepth", 80.0);
            // Säule
            ColumnDiameter = project.GetValue("ColumnDiameter", 30.0);
            ColumnHeight = project.GetValue("ColumnHeight", 250.0);
            // Gemeinsam
            BagWeight = project.GetValue("BagWeight", 25.0);
            PricePerBag = project.GetValue("PricePerBag", 0.0);
            PricePerCubicMeter = project.GetValue("PricePerCubicMeter", 0.0);

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
