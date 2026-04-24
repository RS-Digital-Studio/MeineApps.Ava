using BingXBot.Core.Models;
using BingXBot.Engine.Strategies.Pipeline;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für <see cref="SkMasterclassPipeline"/> (Task 4.12).</summary>
public class MasterclassPipelineTests
{
    private sealed class PassingStep : IPipelineStep
    {
        public int Order { get; init; }
        public string Name { get; init; } = "";
        public Dictionary<string, object>? OutputData { get; init; }
        public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
            => PipelineStepResult.Ok("ok", OutputData);
    }

    private sealed class FailingStep : IPipelineStep
    {
        public int Order { get; init; }
        public string Name { get; init; } = "";
        public string FailReason { get; init; } = "fail";
        public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
            => PipelineStepResult.Fail(FailReason);
    }

    private static MarketContext CreateContext()
    {
        var ticker = new Ticker("TEST", 100m, 99.5m, 100.5m, 1_000_000m, 0m, DateTime.UtcNow);
        var acc = new AccountInfo(1000m, 1000m, 0m, 0m);
        return new MarketContext("TEST", new List<Candle>(), ticker, new List<Position>(), acc);
    }

    [Fact]
    public void Pipeline_MitAllenSteps_LauftDurch()
    {
        var pipeline = new SkMasterclassPipeline(new[]
        {
            new PassingStep { Order = 1, Name = "Step1" },
            new PassingStep { Order = 2, Name = "Step2" },
            new PassingStep { Order = 3, Name = "Step3" },
        });
        var (success, failed, _, _, _) = pipeline.Run(CreateContext());
        success.Should().BeTrue();
        failed.Should().BeNull();
    }

    [Fact]
    public void Pipeline_FailAtStep2_StoppMitOrder()
    {
        var pipeline = new SkMasterclassPipeline(new IPipelineStep[]
        {
            new PassingStep { Order = 1, Name = "Step1" },
            new FailingStep { Order = 2, Name = "Step2", FailReason = "block" },
            new PassingStep { Order = 3, Name = "Step3" },
        });
        var (success, failed, failedName, reason, _) = pipeline.Run(CreateContext());
        success.Should().BeFalse();
        failed.Should().Be(2);
        failedName.Should().Be("Step2");
        reason.Should().Contain("block");
    }

    [Fact]
    public void Pipeline_LeereSteps_LauftOhneException()
    {
        var pipeline = new SkMasterclassPipeline(Array.Empty<IPipelineStep>());
        var (success, _, _, _, _) = pipeline.Run(CreateContext());
        success.Should().BeTrue();
    }

    [Fact]
    public void Pipeline_Sortiert_NachOrder()
    {
        // Steps in umgekehrter Reihenfolge eingefügt, Pipeline muss sie sortieren
        var executionOrder = new List<int>();
        var steps = new IPipelineStep[]
        {
            new LoggingStep(3, executionOrder),
            new LoggingStep(1, executionOrder),
            new LoggingStep(2, executionOrder),
        };
        var pipeline = new SkMasterclassPipeline(steps);
        pipeline.Run(CreateContext());
        executionOrder.Should().Equal(1, 2, 3);
    }

    private sealed class LoggingStep : IPipelineStep
    {
        private readonly List<int> _log;
        public LoggingStep(int order, List<int> log) { Order = order; _log = log; }
        public int Order { get; }
        public string Name => $"Step{Order}";
        public PipelineStepResult Execute(MarketContext context, Dictionary<string, object> data)
        {
            _log.Add(Order);
            return PipelineStepResult.Ok();
        }
    }

    [Fact]
    public void PipelineStepResult_Ok_EnthaeltPassTrue()
    {
        var result = PipelineStepResult.Ok("done");
        result.Pass.Should().BeTrue();
        result.Reason.Should().Be("done");
    }

    [Fact]
    public void PipelineStepResult_Fail_EnthaeltPassFalse()
    {
        var result = PipelineStepResult.Fail("blocked");
        result.Pass.Should().BeFalse();
        result.Reason.Should().Be("blocked");
    }

    [Fact]
    public void Pipeline_DataWirdZwischenStepsWeitergereicht()
    {
        var pipeline = new SkMasterclassPipeline(new IPipelineStep[]
        {
            new PassingStep { Order = 1, Name = "A", OutputData = new Dictionary<string, object> { ["val"] = 42 } },
            new PassingStep { Order = 2, Name = "B" },
        });
        var (success, _, _, _, data) = pipeline.Run(CreateContext());
        success.Should().BeTrue();
        data.Should().ContainKey("val");
        data["val"].Should().Be(42);
    }

    [Fact]
    public void Pipeline_FailStepNamenUndOrder_ImRueckgabeWert()
    {
        var pipeline = new SkMasterclassPipeline(new IPipelineStep[]
        {
            new FailingStep { Order = 5, Name = "CustomStep", FailReason = "test-reason" },
        });
        var (success, order, name, reason, _) = pipeline.Run(CreateContext());
        success.Should().BeFalse();
        order.Should().Be(5);
        name.Should().Be("CustomStep");
        reason.Should().Contain("test-reason");
    }

    [Fact]
    public void Pipeline_NachFail_NachfolgendeStepsNichtAusgefuehrt()
    {
        var counter = new List<int>();
        var steps = new IPipelineStep[]
        {
            new LoggingStep(1, counter),
            new FailingStep { Order = 2, Name = "Stop" },
            new LoggingStep(3, counter),
        };
        var pipeline = new SkMasterclassPipeline(steps);
        pipeline.Run(CreateContext());
        counter.Should().Equal(1);
    }

    [Fact]
    public void IPipelineStep_InterfaceVerpflichtetOrderUndName()
    {
        IPipelineStep step = new PassingStep { Order = 7, Name = "X" };
        step.Order.Should().Be(7);
        step.Name.Should().Be("X");
    }

    [Fact]
    public void PipelineStepResult_DataKannNullSein()
    {
        var result = PipelineStepResult.Ok();
        result.Data.Should().BeNull();
    }
}
