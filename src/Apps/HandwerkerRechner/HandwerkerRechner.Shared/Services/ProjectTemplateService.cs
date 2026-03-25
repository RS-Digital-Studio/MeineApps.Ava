using System.Text.Json;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.Services;

/// <summary>
/// Verwaltet eingebaute und benutzerdefinierte Projekt-Vorlagen.
/// Eigene Vorlagen werden als JSON im AppData-Verzeichnis gespeichert.
/// </summary>
public sealed class ProjectTemplateService : IProjectTemplateService
{
    private readonly string _templatesFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<ProjectTemplate>? _customTemplates;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ProjectTemplateService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MeineApps", "HandwerkerRechner");
        Directory.CreateDirectory(appDataPath);
        _templatesFilePath = Path.Combine(appDataPath, "templates.json");
    }

    public async Task<List<ProjectTemplate>> GetAllTemplatesAsync()
    {
        var all = new List<ProjectTemplate>(GetBuiltinTemplates());
        var custom = await LoadCustomTemplatesAsync();
        all.AddRange(custom);
        return all;
    }

    public List<ProjectTemplate> GetBuiltinTemplates() =>
    [
        // Badezimmer fliesen
        new ProjectTemplate
        {
            Id = "builtin_bathroom",
            NameKey = "TemplateBathroom",
            Category = "bathroom",
            DescriptionKey = "TemplateBathroomDesc",
            IconKind = "ShowerHead",
            IsCustom = false,
            Calculators =
            [
                new TemplateCalculatorEntry
                {
                    Route = "TileCalculatorPage",
                    CalculatorType = CalculatorType.Tiles,
                    DefaultValues = new()
                    {
                        ["RoomLength"] = "2.5",
                        ["RoomWidth"] = "3.2",
                        ["TileLength"] = "20",
                        ["TileWidth"] = "20",
                        ["WastePercent"] = "10"
                    }
                },
                new TemplateCalculatorEntry
                {
                    Route = "GroutPage",
                    CalculatorType = CalculatorType.Grout,
                    DefaultValues = new()
                    {
                        ["TileLengthCm"] = "20",
                        ["TileWidthCm"] = "20",
                        ["GroutWidthMm"] = "3",
                        ["GroutDepthMm"] = "5"
                    }
                }
            ]
        },

        // Wohnzimmer streichen
        new ProjectTemplate
        {
            Id = "builtin_living_room",
            NameKey = "TemplateLivingRoom",
            Category = "living_room",
            DescriptionKey = "TemplateLivingRoomDesc",
            IconKind = "Sofa",
            IsCustom = false,
            Calculators =
            [
                new TemplateCalculatorEntry
                {
                    Route = "PaintCalculatorPage",
                    CalculatorType = CalculatorType.Paint,
                    DefaultValues = new()
                    {
                        ["Area"] = "52",
                        ["CoveragePerLiter"] = "10",
                        ["Coats"] = "2"
                    }
                },
                new TemplateCalculatorEntry
                {
                    Route = "WallpaperCalculatorPage",
                    CalculatorType = CalculatorType.Wallpaper,
                    DefaultValues = new()
                    {
                        ["RoomLength"] = "5",
                        ["RoomWidth"] = "4",
                        ["RoomHeight"] = "2.5"
                    }
                }
            ]
        },

        // Gartenpflasterung
        new ProjectTemplate
        {
            Id = "builtin_garden",
            NameKey = "TemplateGarden",
            Category = "garden",
            DescriptionKey = "TemplateGardenDesc",
            IconKind = "Flower",
            IsCustom = false,
            Calculators =
            [
                new TemplateCalculatorEntry
                {
                    Route = "GardenPage",
                    CalculatorType = CalculatorType.Paving,
                    DefaultValues = new()
                    {
                        ["Area"] = "40",
                        ["StoneLength"] = "20",
                        ["StoneWidth"] = "10"
                    }
                }
            ]
        },

        // Trockenbau Wand
        new ProjectTemplate
        {
            Id = "builtin_drywall",
            NameKey = "TemplateDrywall",
            Category = "construction",
            DescriptionKey = "TemplateDrywallDesc",
            IconKind = "Wall",
            IsCustom = false,
            Calculators =
            [
                new TemplateCalculatorEntry
                {
                    Route = "DrywallPage",
                    CalculatorType = CalculatorType.DrywallFraming,
                    DefaultValues = new()
                    {
                        ["WallLength"] = "4",
                        ["WallHeight"] = "2.5",
                        ["IsDoublePlated"] = "true"
                    }
                }
            ]
        },

        // Bodenrenovierung
        new ProjectTemplate
        {
            Id = "builtin_flooring",
            NameKey = "TemplateFlooring",
            Category = "flooring",
            DescriptionKey = "TemplateFlooringDesc",
            IconKind = "Layers",
            IsCustom = false,
            Calculators =
            [
                new TemplateCalculatorEntry
                {
                    Route = "FlooringCalculatorPage",
                    CalculatorType = CalculatorType.Flooring,
                    DefaultValues = new()
                    {
                        ["RoomLength"] = "5",
                        ["RoomWidth"] = "4",
                        ["BoardLength"] = "1.2",
                        ["BoardWidth"] = "19",
                        ["WastePercent"] = "10"
                    }
                },
                new TemplateCalculatorEntry
                {
                    Route = "ScreedPage",
                    CalculatorType = CalculatorType.Screed,
                    DefaultValues = new()
                    {
                        ["Area"] = "20",
                        ["ThicknessCm"] = "5"
                    }
                }
            ]
        }
    ];

    public async Task SaveCustomTemplateAsync(ProjectTemplate template)
    {
        template.IsCustom = true;
        var templates = await LoadCustomTemplatesAsync();

        var existing = templates.FindIndex(t => t.Id == template.Id);
        if (existing >= 0)
            templates[existing] = template;
        else
            templates.Add(template);

        await SaveCustomTemplatesAsync(templates);
    }

    public async Task DeleteCustomTemplateAsync(string templateId)
    {
        var templates = await LoadCustomTemplatesAsync();
        templates.RemoveAll(t => t.Id == templateId);
        await SaveCustomTemplatesAsync(templates);
    }

    private async Task<List<ProjectTemplate>> LoadCustomTemplatesAsync()
    {
        if (_customTemplates != null) return _customTemplates;

        await _semaphore.WaitAsync();
        try
        {
            if (_customTemplates != null) return _customTemplates;

            if (File.Exists(_templatesFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_templatesFilePath);
                    _customTemplates = JsonSerializer.Deserialize<List<ProjectTemplate>>(json) ?? [];
                }
                catch
                {
                    _customTemplates = [];
                }
            }
            else
            {
                _customTemplates = [];
            }

            return _customTemplates;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SaveCustomTemplatesAsync(List<ProjectTemplate> templates)
    {
        await _semaphore.WaitAsync();
        try
        {
            _customTemplates = templates;
            var json = JsonSerializer.Serialize(templates, JsonOptions);
            await File.WriteAllTextAsync(_templatesFilePath, json);
        }
        catch
        {
            // Fehler beim Speichern - Daten bleiben im Cache
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
