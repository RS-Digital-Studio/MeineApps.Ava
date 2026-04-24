using System.Collections.Concurrent;
using System.Text.Json;
using BingXBot.Backtest;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Data;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBot.Trading.Local;

/// <summary>
/// Server-seitige Backtest-Steuerung: Startet BacktestEngine-Jobs in Background-Tasks.
/// Progress wird per IProgress&lt;int&gt; live in die Jobs geschrieben und via ProgressReceived-Event
/// gepusht (wird vom BotHubEventForwarder an SignalR-Clients weitergereicht).
/// Jobs werden in SQLite persistiert — Clients sehen auch nach Server-Restart den Endzustand.
/// </summary>
public sealed class LocalBacktestService : IBacktestControlService, IDisposable
{
    private readonly IPublicMarketDataClient _publicClient;
    private readonly RiskSettings _defaultRiskSettings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly BotDatabaseService? _db;
    private readonly ConcurrentDictionary<string, BacktestJob> _jobs = new();

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public event Action<BacktestProgressDto>? ProgressReceived;
    public event Action<BacktestResultDto>? Completed;

    public LocalBacktestService(
        IPublicMarketDataClient publicClient,
        RiskSettings defaultRiskSettings,
        BotDatabaseService? db = null,
        ILoggerFactory? loggerFactory = null)
    {
        _publicClient = publicClient;
        _defaultRiskSettings = defaultRiskSettings;
        _db = db;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public async Task<BacktestJobDto> StartAsync(BacktestRequestDto request, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var job = new BacktestJob(jobId, request, BacktestJobState.Queued, DateTime.UtcNow) { Cts = cts };
        _jobs[jobId] = job;

        // Persistieren: Bei Server-Crash kann Client den Job beim naechsten Start als "Failed" abfragen.
        await PersistJobAsync(job).ConfigureAwait(false);

        // Fire-and-forget: Exceptions aus RunJobAsync werden dort intern gecatcht + als Failed persistiert.
        _ = Task.Run(() => RunJobAsync(job), cts.Token);

        return new BacktestJobDto(jobId, BacktestJobState.Queued, job.QueuedAtUtc);
    }

    private async Task RunJobAsync(BacktestJob job)
    {
        job.State = BacktestJobState.Running;
        await PersistJobAsync(job).ConfigureAwait(false);
        try
        {
            var riskSettings = job.Request.RiskOverride ?? CloneRisk(_defaultRiskSettings);
            var backtestSettings = new BacktestSettings
            {
                InitialBalance = job.Request.InitialBalance,
                HtfTimeFrame = null,
                EntryTimeFrame = null
            };

            var strategy = StrategyFactory.Create(job.Request.StrategyName);
            var riskManager = new RiskManager(riskSettings, NullLogger<RiskManager>.Instance);
            var engine = new BacktestEngine(_publicClient, _loggerFactory.CreateLogger<BacktestEngine>());

            // Bar-Progress fuer Remote-UI (BacktestStatusDto.CurrentBar/TotalBars). Ohne diesen
            // Callback waren die Felder immer 0/0 und der Client konnte keinen sinnvollen
            // Fortschrittsbalken anzeigen (nur Prozent, ohne Bar-Anzahl-Kontext).
            engine.SetBarProgress(new Progress<(int Current, int Total)>(bars =>
            {
                job.CurrentBar = bars.Current;
                job.TotalBars = bars.Total;
            }));

            var progress = new Progress<int>(pct =>
            {
                job.Progress = pct / 100f;
                ProgressReceived?.Invoke(new BacktestProgressDto(job.JobId, job.Progress, job.CurrentBar, job.TotalBars));
            });

            var report = await engine.RunAsync(
                strategy: strategy,
                riskManager: riskManager,
                symbol: job.Request.Symbol,
                timeFrame: job.Request.TimeFrame,
                from: job.Request.StartUtc,
                to: job.Request.EndUtc,
                settings: backtestSettings,
                progress: progress,
                ct: job.Cts?.Token ?? CancellationToken.None).ConfigureAwait(false);

            job.Result = MapResult(job, report);
            job.State = BacktestJobState.Completed;
            Completed?.Invoke(job.Result);
        }
        catch (OperationCanceledException)
        {
            job.State = BacktestJobState.Cancelled;
        }
        catch (Exception ex)
        {
            job.Error = ex.Message;
            job.State = BacktestJobState.Failed;
        }
        finally
        {
            // Finaler Persistenz-Stand — damit Client Endzustand sieht auch nach Server-Restart.
            await PersistJobAsync(job).ConfigureAwait(false);
        }
    }

    private async Task PersistJobAsync(BacktestJob job)
    {
        if (_db == null) return;
        try
        {
            await _db.UpsertBacktestJobAsync(new BacktestJobEntity
            {
                JobId = job.JobId,
                State = job.State.ToString(),
                SchemaVersion = BacktestJobEntity.CurrentSchemaVersion,
                RequestJson = JsonSerializer.Serialize(job.Request, JsonOpts),
                ResultJson = job.Result == null ? null : JsonSerializer.Serialize(job.Result, JsonOpts),
                Error = job.Error,
                QueuedAtUtc = job.QueuedAtUtc,
                CompletedAtUtc = job.State is BacktestJobState.Completed or BacktestJobState.Failed or BacktestJobState.Cancelled
                    ? DateTime.UtcNow : null,
                Progress = job.Progress
            }).ConfigureAwait(false);
        }
        catch { /* best-effort */ }
    }

    /// <summary>
    /// Laedt persistierte Jobs beim Server-Start in das In-Memory-Dict. Ruft vorher
    /// `MarkOrphanedBacktestJobsAsFailedAsync` auf, damit Running/Queued-Jobs nach Crash
    /// als Failed angezeigt werden.
    /// </summary>
    public async Task RestoreFromDbAsync()
    {
        if (_db == null) return;
        await _db.MarkOrphanedBacktestJobsAsFailedAsync().ConfigureAwait(false);

        var rows = await _db.GetAllBacktestJobsAsync().ConfigureAwait(false);
        foreach (var row in rows)
        {
            if (!Enum.TryParse<BacktestJobState>(row.State, out var state)) continue;

            // Schema-Version-Check: Nach DTO-Aenderungen koennen alte Payloads inkompatibel sein.
            // Statt silent skip: als Failed markieren, damit der Client den Endzustand sieht.
            if (row.SchemaVersion != BacktestJobEntity.CurrentSchemaVersion)
            {
                await _db.UpsertBacktestJobAsync(new BacktestJobEntity
                {
                    JobId = row.JobId,
                    State = "Failed",
                    SchemaVersion = BacktestJobEntity.CurrentSchemaVersion,
                    RequestJson = row.RequestJson,
                    ResultJson = null,
                    Error = $"Job inkompatibel (Schema v{row.SchemaVersion}, erwartet v{BacktestJobEntity.CurrentSchemaVersion}).",
                    QueuedAtUtc = row.QueuedAtUtc,
                    CompletedAtUtc = DateTime.UtcNow,
                    Progress = 0
                }).ConfigureAwait(false);
                continue;
            }

            try
            {
                var req = JsonSerializer.Deserialize<BacktestRequestDto>(row.RequestJson, JsonOpts);
                var res = string.IsNullOrEmpty(row.ResultJson)
                    ? null
                    : JsonSerializer.Deserialize<BacktestResultDto>(row.ResultJson, JsonOpts);
                if (req == null) continue;
                _jobs[row.JobId] = new BacktestJob(row.JobId, req, state, row.QueuedAtUtc)
                {
                    Progress = row.Progress,
                    Error = row.Error,
                    Result = res
                };
            }
            catch { /* korrupt, skip */ }
        }
    }

    public Task<BacktestStatusDto> GetStatusAsync(string jobId, CancellationToken ct = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            throw new KeyNotFoundException($"Backtest-Job {jobId} nicht gefunden");

        return Task.FromResult(new BacktestStatusDto(
            JobId: job.JobId,
            State: job.State,
            Progress: job.Progress,
            CurrentBar: job.CurrentBar,
            TotalBars: job.TotalBars,
            EstimatedSecondsRemaining: job.EstimatedSecondsRemaining,
            Error: job.Error));
    }

    public Task<BacktestResultDto?> GetResultAsync(string jobId, CancellationToken ct = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job)) return Task.FromResult<BacktestResultDto?>(null);
        return Task.FromResult(job.Result);
    }

    public Task CancelAsync(string jobId, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.State = BacktestJobState.Cancelled;
            // ObjectDisposedException-Schutz: paralleles Dispose() oder mehrfaches Cancel
            // raced sonst mit dem CancellationTokenSource-Lifecycle.
            try { job.Cts?.Cancel(); }
            catch (ObjectDisposedException) { /* bereits disposed — Cancel ist idempotent */ }
        }
        return Task.CompletedTask;
    }

    private BacktestResultDto MapResult(BacktestJob job, BingXBot.Backtest.Reports.PerformanceReport report)
    {
        var trades = report.Trades.Select(t => new BacktestTradeDto(
            Symbol: t.Symbol,
            Side: t.Side,
            EntryPrice: t.EntryPrice,
            ExitPrice: t.ExitPrice,
            Quantity: t.Quantity,
            Pnl: t.Pnl,
            PnlPercent: t.EntryPrice > 0 ? (t.ExitPrice - t.EntryPrice) / t.EntryPrice * 100m * (t.Side == Side.Buy ? 1 : -1) : 0m,
            EntryTimeUtc: t.EntryTime,
            ExitTimeUtc: t.ExitTime,
            Reason: t.Reason)).ToList();

        var equity = report.EquityCurve.Select(p => new EquityPointDto(p.Time, p.Equity)).ToList();
        var finalBalance = equity.Count > 0 ? equity[^1].Equity : job.Request.InitialBalance;

        return new BacktestResultDto(
            JobId: job.JobId,
            Request: job.Request,
            FinalBalance: finalBalance,
            TotalPnl: report.TotalPnl,
            TotalPnlPercent: job.Request.InitialBalance > 0 ? report.TotalPnl / job.Request.InitialBalance * 100m : 0m,
            MaxDrawdown: report.MaxDrawdown,
            MaxDrawdownPercent: report.MaxDrawdownPercent,
            SharpeRatio: report.SharpeRatio,
            SortinoRatio: report.SortinoRatio,
            CalmarRatio: report.CalmarRatio,
            ProfitFactor: report.ProfitFactor,
            WinRate: report.WinRate,
            TotalTrades: report.TotalTrades,
            WinningTrades: report.WinningTrades,
            LosingTrades: report.LosingTrades,
            MaxConsecutiveWins: report.MaxConsecutiveWins,
            MaxConsecutiveLosses: report.MaxConsecutiveLosses,
            Trades: trades,
            EquityCurve: equity,
            CompletedUtc: DateTime.UtcNow);
    }

    private static RiskSettings CloneRisk(RiskSettings src)
    {
        // JSON-Roundtrip-Clone: kopiert ALLE RiskSettings-Felder inklusive der neueren SK-Buch-Felder
        // (MaxRiskPercentPerTrade, BCZoneEntryStrategy, EntryMode, SlBufferPipsByTf, CategorySettings,
        //  RunnerConfig, NewsBlackout, MaxDailyRiskPercent, HighProbabilityPositionMultiplier, ...).
        //
        // Die alte Property-by-Property-Kopie deckte nur 10 von ~30 Feldern ab — Backtest lief dann
        // mit Default-Risiko statt User-Settings, Ergebnisse waren nicht mit Live vergleichbar.
        //
        // Dictionaries (PipScalingByTf, CategorySettings, SlBufferPipsByTf) sind mit System.Text.Json
        // serialisierbar. System.Text.Json ignoriert JsonIgnore-Attribute sauber.
        var json = JsonSerializer.Serialize(src, JsonOpts);
        return JsonSerializer.Deserialize<RiskSettings>(json, JsonOpts) ?? new RiskSettings();
    }

    public void Dispose()
    {
        foreach (var j in _jobs.Values)
        {
            try { j.Cts?.Cancel(); j.Cts?.Dispose(); } catch { }
        }
        _jobs.Clear();
    }

    private sealed record BacktestJob(string JobId, BacktestRequestDto Request, BacktestJobState State, DateTime QueuedAtUtc)
    {
        public BacktestJobState State { get; set; } = State;
        public float Progress { get; set; }
        public int CurrentBar { get; set; }
        public int TotalBars { get; set; }
        public int EstimatedSecondsRemaining { get; set; }
        public string? Error { get; set; }
        public BacktestResultDto? Result { get; set; }
        public CancellationTokenSource? Cts { get; set; }
    }
}
