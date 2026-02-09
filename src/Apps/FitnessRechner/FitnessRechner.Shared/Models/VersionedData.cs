namespace FitnessRechner.Models;

/// <summary>
/// Wrapper for versioned JSON data storage.
/// Enables future migrations when data structure changes.
/// </summary>
/// <typeparam name="T">The type of data being stored</typeparam>
public class VersionedData<T> where T : class, new()
{
    /// <summary>
    /// Current schema version. Increment when breaking changes occur.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// When this data was last modified
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The actual data being stored
    /// </summary>
    public T Data { get; set; } = new();

    /// <summary>
    /// Creates a new versioned data wrapper
    /// </summary>
    public VersionedData() { }

    /// <summary>
    /// Creates a versioned data wrapper with existing data
    /// </summary>
    public VersionedData(T data, int version = 1)
    {
        Data = data;
        Version = version;
        LastModified = DateTime.UtcNow;
    }
}

/// <summary>
/// Schema version constants for each data type.
/// Update these when making breaking changes to data structures.
/// </summary>
public static class DataSchemaVersions
{
    /// <summary>
    /// FoodLogEntry[] schema version
    /// v1: Initial version with Id, FoodName, Date, Meal, Grams, Calories, Protein, Carbs, Fat
    /// </summary>
    public const int FoodLog = 1;

    /// <summary>
    /// FavoriteFoodEntry[] schema version
    /// v1: Initial version with Id, Food, AddedAt, TimesUsed
    /// </summary>
    public const int Favorites = 1;

    /// <summary>
    /// TrackingEntry[] schema version
    /// v1: Initial version with all tracking types (Weight, Water, BMI, BodyFat)
    /// </summary>
    public const int Tracking = 1;

    /// <summary>
    /// CachedBarcodeEntry[] schema version
    /// v1: Initial version with Barcode, Food, CachedAt, ScannedCount, LastScannedAt
    /// </summary>
    public const int BarcodeCache = 1;
}

/// <summary>
/// Migration interface for data schema upgrades
/// </summary>
public interface IDataMigration<T> where T : class
{
    /// <summary>
    /// Source version this migration upgrades from
    /// </summary>
    int FromVersion { get; }

    /// <summary>
    /// Target version this migration upgrades to
    /// </summary>
    int ToVersion { get; }

    /// <summary>
    /// Performs the migration
    /// </summary>
    T Migrate(T data);
}
