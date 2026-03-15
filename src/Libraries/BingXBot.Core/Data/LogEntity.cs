using SQLite;

namespace BingXBot.Core.Data;

[Table("LogEntries")]
public class LogEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime Timestamp { get; set; }
    public int Level { get; set; }
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Symbol { get; set; }
}
