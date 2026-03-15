using SQLite;

namespace BingXBot.Core.Data;

[Table("EquitySnapshots")]
public class EquityEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime Time { get; set; }
    public decimal Equity { get; set; }
}
