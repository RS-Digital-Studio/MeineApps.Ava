using BingXBot.Core.Configuration;
using BingXBot.Core.Models;

namespace BingXBot.Core.Interfaces;

public interface IMarketScanner
{
    IAsyncEnumerable<ScanResult> ScanAsync(ScannerSettings settings, CancellationToken ct);
}
