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

    /// <summary>Wird ausgelöst, wenn das Speichern fehlschlägt (z.B. Speicher voll/Schreibschutz).</summary>
    public event Action? SaveFailed;

    public async Task<List<ProjectTemplate>> GetAllTemplatesAsync()
    {
        await EnsureLoadedAsync();

        await _semaphore.WaitAsync();
        try
        {
            // Kopie herausgeben — Aufrufer dürfen nie die gecachte Liste enumerieren
            // (parallele Mutation in SaveCustomTemplateAsync/DeleteCustomTemplateAsync).
            return [.. GetBuiltinTemplates(), .. _customTemplates!];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // WICHTIG: Die Default-Value-Keys MÜSSEN exakt zu den Property-Namen passen, die
    // die jeweilige Calculator-VM in LoadProjectAsync via project.GetValue<T>("Key") erwartet.
    // Sonst greifen die Vorlagen ins Leere und der User sieht Defaults statt Template-Werte.
    public List<ProjectTemplate> GetBuiltinTemplates() =>
    [
        // Badezimmer fliesen
        // Tile: RoomLength, RoomWidth, TileLength (cm), TileWidth (cm), WastePercentage, GroutWidthMm
        // Grout: AreaSqm, TileLengthCm, TileWidthCm, GroutWidthMm, GroutDepthMm
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
                        ["WastePercentage"] = "10",
                        ["GroutWidthMm"] = "3"
                    }
                },
                new TemplateCalculatorEntry
                {
                    Route = "GroutPage",
                    CalculatorType = CalculatorType.Grout,
                    DefaultValues = new()
                    {
                        ["AreaSqm"] = "8",
                        ["TileLengthCm"] = "20",
                        ["TileWidthCm"] = "20",
                        ["GroutWidthMm"] = "3",
                        ["GroutDepthMm"] = "5"
                    }
                }
            ]
        },

        // Wohnzimmer streichen
        // Paint: Area, CoveragePerLiter, NumberOfCoats (int), PricePerLiter
        // Wallpaper: WallLength (Wandumfang m), RoomHeight, RollLength, RollWidth, PatternRepeat
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
                        ["NumberOfCoats"] = "2"
                    }
                },
                new TemplateCalculatorEntry
                {
                    Route = "WallpaperCalculatorPage",
                    CalculatorType = CalculatorType.Wallpaper,
                    // 5x4m Wohnzimmer => Umfang 18m
                    DefaultValues = new()
                    {
                        ["WallLength"] = "18",
                        ["RoomHeight"] = "2.5",
                        ["RollLength"] = "10.05",
                        ["RollWidth"] = "53"
                    }
                }
            ]
        },

        // Gartenpflasterung (GardenViewModel.Paving-Sub-Calculator)
        // Garden: SelectedCalculator (0=Paving), PavingArea, StoneLength, StoneWidth, JointWidth
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
                        ["SelectedCalculator"] = "0",
                        ["PavingArea"] = "40",
                        ["StoneLength"] = "20",
                        ["StoneWidth"] = "10",
                        ["JointWidth"] = "3"
                    }
                }
            ]
        },

        // Trockenbau Wand
        // Drywall: WallLength, WallHeight, DoublePlated (bool)
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
                        ["DoublePlated"] = "true"
                    }
                }
            ]
        },

        // Bodenrenovierung
        // Flooring: RoomLength, RoomWidth, BoardLength, BoardWidth, WastePercentage
        // Screed: FloorArea, ThicknessCm, SelectedScreedType (int)
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
                        ["WastePercentage"] = "10"
                    }
                },
                new TemplateCalculatorEntry
                {
                    Route = "ScreedPage",
                    CalculatorType = CalculatorType.Screed,
                    DefaultValues = new()
                    {
                        ["FloorArea"] = "20",
                        ["ThicknessCm"] = "5",
                        ["SelectedScreedType"] = "0"
                    }
                }
            ]
        }
    ];

    public async Task SaveCustomTemplateAsync(ProjectTemplate template)
    {
        template.IsCustom = true;
        await EnsureLoadedAsync();

        await _semaphore.WaitAsync();
        try
        {
            var existing = _customTemplates!.FindIndex(t => t.Id == template.Id);
            if (existing >= 0)
                _customTemplates[existing] = template;
            else
                _customTemplates.Add(template);

            await SaveToFileInternalAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteCustomTemplateAsync(string templateId)
    {
        await EnsureLoadedAsync();

        await _semaphore.WaitAsync();
        try
        {
            _customTemplates!.RemoveAll(t => t.Id == templateId);
            await SaveToFileInternalAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_customTemplates != null) return;

        await _semaphore.WaitAsync();
        try
        {
            if (_customTemplates != null) return;

            if (File.Exists(_templatesFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_templatesFilePath);
                    _customTemplates = JsonSerializer.Deserialize<List<ProjectTemplate>>(json) ?? [];
                }
                catch
                {
                    // Korrupte Datei sichern statt beim nächsten Save endgültig zu überschreiben
                    TryBackupCorruptFile(_templatesFilePath);
                    _customTemplates = [];
                }
            }
            else
            {
                _customTemplates = [];
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Interner File-Write — MUSS innerhalb des Semaphore-Locks aufgerufen werden.</summary>
    private async Task SaveToFileInternalAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_customTemplates, JsonOptions);
            await File.WriteAllTextAsync(_templatesFilePath, json);
        }
        catch
        {
            // Speichern fehlgeschlagen — Daten bleiben im Cache; UI benachrichtigen statt stillem Verlust
            SaveFailed?.Invoke();
        }
    }

    /// <summary>Best-Effort-Backup einer korrupten JSON-Datei nach "&lt;name&gt;.bak".</summary>
    private static void TryBackupCorruptFile(string filePath)
    {
        try
        {
            File.Copy(filePath, filePath + ".bak", overwrite: true);
        }
        catch
        {
            // Best Effort — Backup darf das Laden nie blockieren
        }
    }
}
