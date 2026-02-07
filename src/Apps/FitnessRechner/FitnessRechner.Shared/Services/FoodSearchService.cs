using System.Text.Json;
using FitnessRechner.Models;

namespace FitnessRechner.Services;

/// <summary>
/// Implementation of intelligent food search with fuzzy matching
/// </summary>
public class FoodSearchService : IFoodSearchService
{
    private const string FOOD_LOG_FILE = "food_log.json";
    private const string FOOD_LOG_ARCHIVE_FILE = "food_log_archive.json";
    private const string FAVORITES_FILE = "food_favorites.json";
    private const string RECIPES_FILE = "recipes.json";
    private const double MIN_SEARCH_SCORE = 0.3;
    private readonly string _filePath;
    private readonly string _archivePath;
    private readonly string _favoritesPath;
    private readonly string _recipesPath;
    private List<FoodLogEntry> _foodLog = [];
    private List<FavoriteFoodEntry> _favorites = [];
    private List<Recipe> _recipes = [];
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _favoritesLock = new(1, 1);
    private readonly SemaphoreSlim _recipesLock = new(1, 1);
    private bool _isLoaded = false;
    private bool _favoritesLoaded = false;
    private bool _recipesLoaded = false;

    public FoodSearchService()
    {
        var dataDir = GetDataDirectory();
        _filePath = Path.Combine(dataDir, FOOD_LOG_FILE);
        _archivePath = Path.Combine(dataDir, FOOD_LOG_ARCHIVE_FILE);
        _favoritesPath = Path.Combine(dataDir, FAVORITES_FILE);
        _recipesPath = Path.Combine(dataDir, RECIPES_FILE);
    }

    #region Search

    public IReadOnlyList<FoodSearchResult> Search(string query, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        query = query.Trim().ToLowerInvariant();
        var results = new List<FoodSearchResult>();

        foreach (var food in FoodDatabase.Foods)
        {
            var bestScore = 0.0;
            var matchedOn = "";

            // Check main name
            var nameScore = CalculateScore(query, food.Name.ToLowerInvariant());
            if (nameScore > bestScore)
            {
                bestScore = nameScore;
                matchedOn = food.Name;
            }

            // Check aliases
            foreach (var alias in food.Aliases)
            {
                var aliasScore = CalculateScore(query, alias.ToLowerInvariant());
                if (aliasScore > bestScore)
                {
                    bestScore = aliasScore;
                    matchedOn = alias;
                }
            }

            // Minimum score for relevance
            if (bestScore >= MIN_SEARCH_SCORE)
            {
                results.Add(new FoodSearchResult
                {
                    Food = food,
                    Score = bestScore,
                    MatchedOn = matchedOn
                });
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Food.Name)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Calculates a relevance score between 0 and 1
    /// </summary>
    private static double CalculateScore(string query, string target)
    {
        // Exact match
        if (target == query)
            return 1.0;

        // Starts with query
        if (target.StartsWith(query))
            return 0.9 + (0.1 * query.Length / target.Length);

        // Contains query
        if (target.Contains(query))
            return 0.7 + (0.2 * query.Length / target.Length);

        // Fuzzy matching with Levenshtein distance
        var distance = LevenshteinDistance(query, target);
        var maxLength = Math.Max(query.Length, target.Length);

        if (maxLength == 0)
            return 0;

        var similarity = 1.0 - ((double)distance / maxLength);

        // Bonus if first characters match
        if (query.Length > 0 && target.Length > 0 && query[0] == target[0])
            similarity += 0.1;

        return Math.Min(1.0, Math.Max(0, similarity));
    }

    /// <summary>
    /// Calculates the Levenshtein distance (edit distance) - space-optimized version
    /// Uses O(min(n,m)) memory instead of O(n*m) by using two rows instead of a matrix
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        var n = s1.Length;
        var m = s2.Length;

        if (n == 0) return m;
        if (m == 0) return n;

        // Early exit: If length difference > 50% of longer string, probably no match
        if (Math.Abs(n - m) > Math.Max(n, m) * 0.5)
            return Math.Max(n, m);

        // Ensure s1 is the shorter string for space optimization
        if (n > m)
        {
            (s1, s2) = (s2, s1);
            (n, m) = (m, n);
        }

        // Space optimization: Only keep two rows instead of full matrix O(min(n,m))
        var previousRow = new int[n + 1];
        var currentRow = new int[n + 1];

        // Initialize first row
        for (var i = 0; i <= n; i++)
            previousRow[i] = i;

        for (var j = 1; j <= m; j++)
        {
            currentRow[0] = j;

            for (var i = 1; i <= n; i++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

                currentRow[i] = Math.Min(
                    Math.Min(currentRow[i - 1] + 1, previousRow[i] + 1),
                    previousRow[i - 1] + cost);
            }

            // Swap rows for next iteration
            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[n];
    }

    #endregion

    #region Categories

    public IReadOnlyList<FoodItem> GetByCategory(FoodCategory category)
    {
        return FoodDatabase.Foods
            .Where(f => f.Category == category)
            .OrderBy(f => f.Name)
            .ToList();
    }

    public IReadOnlyList<FoodCategory> GetCategories()
    {
        return Enum.GetValues<FoodCategory>().ToList();
    }

    #endregion

    #region Food Log

    public async Task SaveFoodLogAsync(FoodLogEntry entry)
    {
        // Transactional save: First save file, then update memory
        var tempLog = new List<FoodLogEntry>(_foodLog) { entry };

        // Create temp file for atomic save
        var tempFilePath = _filePath + ".tmp";
        try
        {
            // Save to temp file
            var json = JsonSerializer.Serialize(tempLog, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(tempFilePath, json);

            // Create backup
            if (File.Exists(_filePath))
            {
                var backupPath = _filePath + ".backup";
                File.Copy(_filePath, backupPath, overwrite: true);
            }

            // Make temp file the final file (atomic operation)
            File.Move(tempFilePath, _filePath, overwrite: true);

            // Only if everything succeeded: memory update
            _foodLog.Add(entry);
        }
        catch
        {
            // Cleanup on error
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
            throw;
        }
    }

    public async Task<IReadOnlyList<FoodLogEntry>> GetFoodLogAsync(DateTime date)
    {
        await EnsureFoodLogLoadedAsync();

        return _foodLog
            .Where(e => e.Date.Date == date.Date)
            .OrderBy(e => e.Meal)
            .ToList();
    }

    public async Task<DailyNutritionSummary> GetDailySummaryAsync(DateTime date)
    {
        var entries = await GetFoodLogAsync(date);

        return new DailyNutritionSummary(
            Date: date,
            TotalCalories: entries.Sum(e => e.Calories),
            TotalProtein: entries.Sum(e => e.Protein),
            TotalCarbs: entries.Sum(e => e.Carbs),
            TotalFat: entries.Sum(e => e.Fat),
            EntryCount: entries.Count
        );
    }

    public async Task DeleteFoodLogAsync(string entryId)
    {
        var entry = _foodLog.FirstOrDefault(e => e.Id == entryId);
        if (entry != null)
        {
            // Transactional deletion: First save file, then update memory
            var tempLog = _foodLog.Where(e => e.Id != entryId).ToList();

            // Create temp file
            var tempFilePath = _filePath + ".tmp";
            try
            {
                var json = JsonSerializer.Serialize(tempLog, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tempFilePath, json);

                // Create backup
                if (File.Exists(_filePath))
                {
                    var backupPath = _filePath + ".backup";
                    File.Copy(_filePath, backupPath, overwrite: true);
                }

                // Atomic operation
                File.Move(tempFilePath, _filePath, overwrite: true);

                // Only if successful: memory update
                _foodLog.Remove(entry);
            }
            catch
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
                throw;
            }
        }
    }

    private async Task LoadFoodLogAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            _foodLog = JsonSerializer.Deserialize<List<FoodLogEntry>>(json) ?? [];
        }
        catch (Exception)
        {
            // Try to restore from backup
            var backupPath = _filePath + ".backup";
            if (File.Exists(backupPath))
            {
                try
                {
                    var backupJson = await File.ReadAllTextAsync(backupPath);
                    _foodLog = JsonSerializer.Deserialize<List<FoodLogEntry>>(backupJson) ?? [];
                }
                catch
                {
                    _foodLog = [];
                }
            }
            else
            {
                _foodLog = [];
            }
        }
    }

    private async Task EnsureFoodLogLoadedAsync()
    {
        if (_isLoaded) return;

        await _loadLock.WaitAsync();
        try
        {
            if (_isLoaded) return;

            if (File.Exists(_filePath))
            {
                await LoadFoodLogAsync();
            }

            _isLoaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task SaveFoodLogToFileAsync()
    {
        try
        {
            // Create backup BEFORE saving (safety against data loss)
            if (File.Exists(_filePath))
            {
                var backupPath = _filePath + ".backup";
                File.Copy(_filePath, backupPath, overwrite: true);
            }

            var json = JsonSerializer.Serialize(_foodLog, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch
        {
            throw;
        }
    }

    #endregion

    #region Favorites

    public async Task SaveFavoriteAsync(FoodItem food)
    {
        await EnsureFavoritesLoadedAsync();

        // Check if already exists
        var existing = _favorites.FirstOrDefault(f => f.Food.Name == food.Name);
        if (existing != null)
        {
            return; // Already a favorite
        }

        var favorite = new FavoriteFoodEntry
        {
            Food = food,
            AddedAt = DateTime.Now,
            TimesUsed = 0
        };

        _favorites.Add(favorite);
        await SaveFavoritesToFileAsync();
    }

    public async Task<IReadOnlyList<FavoriteFoodEntry>> GetFavoritesAsync()
    {
        await EnsureFavoritesLoadedAsync();

        return _favorites
            .OrderByDescending(f => f.TimesUsed)
            .ThenByDescending(f => f.AddedAt)
            .ToList();
    }

    public async Task RemoveFavoriteAsync(string id)
    {
        await EnsureFavoritesLoadedAsync();

        var favorite = _favorites.FirstOrDefault(f => f.Id == id);
        if (favorite != null)
        {
            _favorites.Remove(favorite);
            await SaveFavoritesToFileAsync();
        }
    }

    public async Task<bool> IsFavoriteAsync(string foodName)
    {
        await EnsureFavoritesLoadedAsync();
        return _favorites.Any(f => f.Food.Name == foodName);
    }

    public async Task IncrementFavoriteUsageAsync(string foodName)
    {
        await EnsureFavoritesLoadedAsync();

        var favorite = _favorites.FirstOrDefault(f => f.Food.Name == foodName);
        if (favorite != null)
        {
            favorite.TimesUsed++;
            await SaveFavoritesToFileAsync();
        }
    }

    private async Task LoadFavoritesAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_favoritesPath);
            _favorites = JsonSerializer.Deserialize<List<FavoriteFoodEntry>>(json) ?? [];
        }
        catch (Exception)
        {
            _favorites = [];
        }
    }

    private async Task EnsureFavoritesLoadedAsync()
    {
        if (_favoritesLoaded) return;

        await _favoritesLock.WaitAsync();
        try
        {
            if (_favoritesLoaded) return;

            if (File.Exists(_favoritesPath))
            {
                await LoadFavoritesAsync();
            }

            _favoritesLoaded = true;
        }
        finally
        {
            _favoritesLock.Release();
        }
    }

    private async Task SaveFavoritesToFileAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_favorites, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_favoritesPath, json);
        }
        catch
        {
            throw;
        }
    }

    #endregion

    #region Archive

    public async Task<int> ArchiveOldEntriesAsync(int monthsOld = 6)
    {
        await EnsureFoodLogLoadedAsync();

        var cutoffDate = DateTime.Now.AddMonths(-monthsOld);
        var entriesToArchive = _foodLog.Where(e => e.Date < cutoffDate).ToList();

        if (entriesToArchive.Count == 0)
            return 0;

        // Load existing archive
        var archive = await LoadArchiveAsync();
        archive.AddRange(entriesToArchive);

        // Save archive with atomic operation
        var tempArchivePath = _archivePath + ".tmp";
        try
        {
            var archiveJson = JsonSerializer.Serialize(archive, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(tempArchivePath, archiveJson);

            if (File.Exists(_archivePath))
            {
                var backupPath = _archivePath + ".backup";
                File.Copy(_archivePath, backupPath, overwrite: true);
            }

            File.Move(tempArchivePath, _archivePath, overwrite: true);

            // Remove archived entries from main log
            _foodLog = _foodLog.Where(e => e.Date >= cutoffDate).ToList();
            await SaveFoodLogToFileAsync();

            return entriesToArchive.Count;
        }
        catch
        {
            if (File.Exists(tempArchivePath))
                File.Delete(tempArchivePath);
            throw;
        }
    }

    public async Task<int> GetArchivableEntriesCountAsync(int monthsOld = 6)
    {
        await EnsureFoodLogLoadedAsync();

        var cutoffDate = DateTime.Now.AddMonths(-monthsOld);
        return _foodLog.Count(e => e.Date < cutoffDate);
    }

    public async Task ClearArchiveAsync()
    {
        if (File.Exists(_archivePath))
        {
            await Task.Run(() => File.Delete(_archivePath));
        }
    }

    private async Task<List<FoodLogEntry>> LoadArchiveAsync()
    {
        try
        {
            if (File.Exists(_archivePath))
            {
                var json = await File.ReadAllTextAsync(_archivePath);
                return JsonSerializer.Deserialize<List<FoodLogEntry>>(json) ?? [];
            }
        }
        catch (Exception)
        {
            // Ignore corrupted archive
        }

        return [];
    }

    #endregion

    #region Recipes

    public async Task SaveRecipeAsync(Recipe recipe)
    {
        await EnsureRecipesLoadedAsync();

        // Check if recipe with same name exists
        var existing = _recipes.FirstOrDefault(r => r.Name == recipe.Name);
        if (existing != null)
        {
            // Update existing
            existing.Ingredients = recipe.Ingredients;
            existing.Description = recipe.Description;
            existing.Servings = recipe.Servings;
        }
        else
        {
            _recipes.Add(recipe);
        }

        await SaveRecipesToFileAsync();
    }

    public async Task<IReadOnlyList<Recipe>> GetRecipesAsync()
    {
        await EnsureRecipesLoadedAsync();

        return _recipes
            .OrderByDescending(r => r.TimesUsed)
            .ThenByDescending(r => r.CreatedAt)
            .ToList();
    }

    public async Task DeleteRecipeAsync(string recipeId)
    {
        await EnsureRecipesLoadedAsync();

        var recipe = _recipes.FirstOrDefault(r => r.Id == recipeId);
        if (recipe != null)
        {
            _recipes.Remove(recipe);
            await SaveRecipesToFileAsync();
        }
    }

    public async Task IncrementRecipeUsageAsync(string recipeId)
    {
        await EnsureRecipesLoadedAsync();

        var recipe = _recipes.FirstOrDefault(r => r.Id == recipeId);
        if (recipe != null)
        {
            recipe.TimesUsed++;
            await SaveRecipesToFileAsync();
        }
    }

    public async Task UpdateRecipeAsync(Recipe recipe)
    {
        await EnsureRecipesLoadedAsync();

        var existing = _recipes.FirstOrDefault(r => r.Id == recipe.Id);
        if (existing != null)
        {
            var index = _recipes.IndexOf(existing);
            _recipes[index] = recipe;
            await SaveRecipesToFileAsync();
        }
    }

    private async Task LoadRecipesAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_recipesPath);
            _recipes = JsonSerializer.Deserialize<List<Recipe>>(json) ?? [];
        }
        catch (Exception)
        {
            _recipes = [];
        }
    }

    private async Task EnsureRecipesLoadedAsync()
    {
        if (_recipesLoaded) return;

        await _recipesLock.WaitAsync();
        try
        {
            if (_recipesLoaded) return;

            if (File.Exists(_recipesPath))
            {
                await LoadRecipesAsync();
            }

            _recipesLoaded = true;
        }
        finally
        {
            _recipesLock.Release();
        }
    }

    private async Task SaveRecipesToFileAsync()
    {
        var tempFilePath = _recipesPath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(_recipes, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(tempFilePath, json);

            if (File.Exists(_recipesPath))
            {
                var backupPath = _recipesPath + ".backup";
                File.Copy(_recipesPath, backupPath, overwrite: true);
            }

            File.Move(tempFilePath, _recipesPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
            throw;
        }
    }

    #endregion

    private static string GetDataDirectory()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FitnessRechner");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
