using SQLite;

namespace BingXBot.Core.Data;

[Table("Settings")]
public class SettingEntity
{
    [PrimaryKey]
    public string Key { get; set; } = "";

    public string Value { get; set; } = "";
}
