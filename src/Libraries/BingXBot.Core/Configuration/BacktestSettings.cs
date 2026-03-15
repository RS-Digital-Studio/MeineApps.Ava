namespace BingXBot.Core.Configuration;

public class BacktestSettings
{
    public decimal InitialBalance { get; set; } = 1000m;
    public decimal MakerFee { get; set; } = 0.0002m;
    public decimal TakerFee { get; set; } = 0.0005m;
    public decimal SlippagePercent { get; set; } = 0.05m;
    public bool SimulateFundingRate { get; set; } = true;
}
