using ONA.Analysis.Domain.Aggregates;
using ONA.Analysis.Domain.Exceptions;
using ONA.Analysis.Domain.ValueObjects;

namespace ONA.Analysis.Domain.Tests;

public sealed class AnalysisRunTests
{
    private static NetworkAnalysisResult Result() =>
        new(new GraphMetrics(2, 1, 0.5, 0.5),
            [new NodeMetrics("a", "A", 0, 1, 0.9, true), new NodeMetrics("b", "B", 1, 0, 0, false)]);

    [Fact]
    public void NewRun_IsRunningAndBoundToGraphVersion()
    {
        var run = new AnalysisRun("r1", "board-1", "v1");

        Assert.Equal(AnalysisStatus.Running, run.Status);
        Assert.Equal("v1", run.GraphVersionId);
        Assert.Null(run.CompletedAt);
        Assert.Null(run.Metrics);
    }

    [Fact]
    public void Complete_StoresResultAndExposesBottlenecks()
    {
        var run = new AnalysisRun("r1", "board-1", "v1");

        run.Complete(Result());

        Assert.Equal(AnalysisStatus.Completed, run.Status);
        Assert.NotNull(run.CompletedAt);
        Assert.Equal(2, run.NodeMetrics.Count);
        Assert.Equal(["a"], run.Bottlenecks);
    }

    [Fact]
    public void Fail_RecordsError()
    {
        var run = new AnalysisRun("r1", "board-1", "v1");

        run.Fail("boom");

        Assert.Equal(AnalysisStatus.Failed, run.Status);
        Assert.Equal("boom", run.ErrorMessage);
    }

    [Fact]
    public void CompletedRun_CannotBeCompletedOrFailedAgain()
    {
        var run = new AnalysisRun("r1", "board-1", "v1");
        run.Complete(Result());

        var first = Assert.Throws<InvalidAnalysisRunStateTransitionException>(() => run.Complete(Result()));
        var second = Assert.Throws<InvalidAnalysisRunStateTransitionException>(() => run.Fail("x"));

        Assert.Equal("analysis_run.invalid_state_transition", first.Code);
        Assert.Equal("analysis_run.invalid_state_transition", second.Code);
        Assert.Equal(AnalysisStatus.Completed, run.Status);
    }

    [Theory]
    [InlineData(null, "b", "v")]
    [InlineData("r", " ", "v")]
    [InlineData("r", "b", "")]
    public void Run_RejectsEmptyIdentifiers(string? id, string? board, string? version)
    {
        var exception = Assert.Throws<InvalidIdentifierException>(() => new AnalysisRun(id, board, version));

        Assert.Equal("analysis_run.invalid_identifier", exception.Code);
    }

    [Fact]
    public void AnalysisRunId_New_IsUniqueAndEqualByValue()
    {
        var a = AnalysisRunId.New();
        var b = AnalysisRunId.New();

        Assert.NotEqual(a, b);
        Assert.Equal(new BoardId("x"), new BoardId("x"));
        Assert.Equal(new GraphVersionId("v"), new GraphVersionId("v"));
    }
}
