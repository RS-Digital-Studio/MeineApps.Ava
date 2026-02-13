using HandwerkerRechner.Models;
using System.Text.Json;

namespace HandwerkerRechner.Services;

/// <summary>
/// JSON-file-based project storage (thread-safe)
/// </summary>
public class ProjectService : IProjectService
{
    private readonly string _projectsFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<Project> _cachedProjects = [];
    private bool _isLoaded;

    public ProjectService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MeineApps", "HandwerkerRechner");
        Directory.CreateDirectory(appDataPath);
        _projectsFilePath = Path.Combine(appDataPath, "projects.json");
    }

    public async Task SaveProjectAsync(Project project)
    {
        await EnsureLoadedAsync();

        await _semaphore.WaitAsync();
        try
        {
            var existingProject = _cachedProjects.FirstOrDefault(p => p.Id == project.Id);
            if (existingProject != null)
            {
                existingProject.Name = project.Name;
                existingProject.Description = project.Description;
                existingProject.CalculatorType = project.CalculatorType;
                existingProject.DataJson = project.DataJson;
                existingProject.LastModified = DateTime.UtcNow;
            }
            else
            {
                project.Id = Guid.NewGuid().ToString();
                project.CreatedDate = DateTime.UtcNow;
                project.LastModified = DateTime.UtcNow;
                _cachedProjects.Add(project);
            }

            await SaveToFileInternalAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Project>> LoadAllProjectsAsync()
    {
        await EnsureLoadedAsync();

        await _semaphore.WaitAsync();
        try
        {
            return _cachedProjects.OrderByDescending(p => p.LastModified).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Project?> LoadProjectAsync(string projectId)
    {
        await EnsureLoadedAsync();

        await _semaphore.WaitAsync();
        try
        {
            return _cachedProjects.FirstOrDefault(p => p.Id == projectId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteProjectAsync(string projectId)
    {
        await EnsureLoadedAsync();

        await _semaphore.WaitAsync();
        try
        {
            var project = _cachedProjects.FirstOrDefault(p => p.Id == projectId);
            if (project != null)
            {
                _cachedProjects.Remove(project);
                await SaveToFileInternalAsync();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Project>> LoadProjectsByTypeAsync(CalculatorType calculatorType)
    {
        await EnsureLoadedAsync();

        await _semaphore.WaitAsync();
        try
        {
            return _cachedProjects
                .Where(p => p.CalculatorType == calculatorType)
                .OrderByDescending(p => p.LastModified)
                .ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;

        await _semaphore.WaitAsync();
        try
        {
            if (_isLoaded) return;

            if (File.Exists(_projectsFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_projectsFilePath);
                    _cachedProjects = JsonSerializer.Deserialize<List<Project>>(json) ?? [];
                }
                catch (Exception)
                {
                    _cachedProjects = [];
                }
            }

            _isLoaded = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Interner File-Write - MUSS innerhalb eines Semaphore-Locks aufgerufen werden
    /// </summary>
    private async Task SaveToFileInternalAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_cachedProjects, options);
            await File.WriteAllTextAsync(_projectsFilePath, json);
        }
        catch (Exception)
        {
            // Save failed silently - data remains in cache
        }
    }
}
