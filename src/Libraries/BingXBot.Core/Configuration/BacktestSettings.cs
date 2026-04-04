namespace BingXBot.Core.Configuration;

public class BacktestSettings
{
    public decimal InitialBalance { get; set; } = 1000m;
    public decimal MakerFee { get; set; } = 0.0002m;
    public decimal TakerFee { get; set; } = 0.0005m;
    public decimal SlippagePercent { get; set; } = 0.05m;
    public bool SimulateFundingRate { get; set; } = true;
    /// <summary>Simulierte Funding-Rate pro 8h-Intervall in % (z.B. 0.01 = 0.01%). Nur aktiv wenn SimulateFundingRate=true.</summary>
    public decimal SimulatedFundingRatePercent { get; set; } = 0.01m;
}
