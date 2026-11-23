using ONA.Insights.Application.Features.Recommendations.Commands;
using ONA.Insights.Application.Features.Recommendations.Queries;
using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.Exceptions;

namespace ONA.Insights.Application.Tests;

public sealed class RecommendationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryRecommendationRepository _repo = new();
    private readonly FakeInsightGenerator _generator = new();

    private GenerateRecommendationsCommandHandler Generator(AnalysisRunResult? run) =>
        new(_repo, new FakeAnalysisResultsPort(run), _generator, new FixedClock(Now));

    private RespondToRecommendationCommandHandler Responder() => new(_repo, new FixedClock(Now));

    [Fact]
    public async Task Generate_CreatesOneRecommendationPerBottleneckWithProvenance()
    {
        var handler = Generator(Runs.Completed("run1", "n2", "n3"));

        var result = await handler.HandleAsync(new GenerateRecommendationsCommand("run1"));

        Assert.Equal(2, result.Count);
        Assert.Equal(2, _repo.Items.Count);
        Assert.All(result, r =>
        {
            Assert.Equal("run1", r.AnalysisRunId);
            Assert.Equal("v1", r.GraphVersionId);
            Assert.Equal("b1", r.BoardId);
            Assert.Equal("Delivered", r.Status);
        });
        Assert.Equal(["n2", "n3"], result.Select(r => r.TargetNodeId));
        Assert.Equal("Advice for Person n2", result[0].Content);
    }

    [Fact]
    public async Task Generate_Twice_ReturnsExistingWithoutDuplicates()
    {
        var handler = Generator(Runs.Completed("run1", "n2", "n3"));
        var first = await handler.HandleAsync(new GenerateRecommendationsCommand("run1"));

        var second = await handler.HandleAsync(new GenerateRecommendationsCommand("run1"));

        Assert.Equal(first.Select(r => r.Id), second.Select(r => r.Id));
        Assert.Equal(2, _repo.Items.Count);
        Assert.Equal(2, _generator.Calls);
    }

    [Fact]
    public async Task Generate_RunNotCompleted_Throws()
    {
        var running = Runs.Completed("run1", "n1") with { Status = AnalysisRunStatus.Running };

        var ex = await Assert.ThrowsAsync<AnalysisRunNotCompletedException>(() =>
            Generator(running).HandleAsync(new GenerateRecommendationsCommand("run1")));

        Assert.Equal("recommendation.analysis_run_not_completed", ex.Code);
        Assert.Empty(_repo.Items);
    }

    [Fact]
    public async Task Generate_NoBottlenecks_Throws()
    {
        var ex = await Assert.ThrowsAsync<NoBottlenecksException>(() =>
            Generator(Runs.Completed("run1")).HandleAsync(new GenerateRecommendationsCommand("run1")));

        Assert.Equal("recommendation.no_bottlenecks", ex.Code);
    }

    [Fact]
    public async Task Generate_RunNotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<AnalysisRunNotFoundException>(() =>
            Generator(null).HandleAsync(new GenerateRecommendationsCommand("missing")));

        Assert.Equal("recommendation.analysis_run_not_found", ex.Code);
    }

    [Fact]
    public async Task Respond_Approve_UpdatesStatusAndSaves()
    {
        var created = await Generator(Runs.Completed("run1", "n2")).HandleAsync(new GenerateRecommendationsCommand("run1"));

        var dto = await Responder().HandleAsync(
            new RespondToRecommendationCommand(created[0].Id, "Approve", "go", "user-1"));

        Assert.Equal("Approved", dto.Status);
        Assert.Equal("go", dto.UserComment);
        Assert.Equal("user-1", dto.RespondedByUserId);
        Assert.Equal(Now.UtcDateTime, dto.RespondedAt);
        Assert.Equal(1, _repo.SaveCount);
    }

    [Fact]
    public async Task Respond_Reject_UpdatesStatus()
    {
        var created = await Generator(Runs.Completed("run1", "n2")).HandleAsync(new GenerateRecommendationsCommand("run1"));

        var dto = await Responder().HandleAsync(new RespondToRecommendationCommand(created[0].Id, "reject", null, null));

        Assert.Equal("Rejected", dto.Status);
    }

    [Fact]
    public async Task Respond_Twice_ThrowsInvalidStateTransition()
    {
        var created = await Generator(Runs.Completed("run1", "n2")).HandleAsync(new GenerateRecommendationsCommand("run1"));
        await Responder().HandleAsync(new RespondToRecommendationCommand(created[0].Id, "approve", null, null));

        var ex = await Assert.ThrowsAsync<InvalidRecommendationStateTransitionException>(() =>
            Responder().HandleAsync(new RespondToRecommendationCommand(created[0].Id, "reject", null, null)));

        Assert.Equal("recommendation.invalid_state_transition", ex.Code);
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Respond_InvalidResponse_Throws(string? response)
    {
        var created = await Generator(Runs.Completed("run1", "n2")).HandleAsync(new GenerateRecommendationsCommand("run1"));

        var ex = await Assert.ThrowsAsync<InvalidRecommendationResponseException>(() =>
            Responder().HandleAsync(new RespondToRecommendationCommand(created[0].Id, response, null, null)));

        Assert.Equal("recommendation.invalid_response", ex.Code);
        Assert.Equal(RecommendationStatus.Delivered, _repo.Items[0].Status);
    }

    [Fact]
    public async Task Respond_UnknownRecommendation_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<RecommendationNotFoundException>(() =>
            Responder().HandleAsync(new RespondToRecommendationCommand("nope", "approve", null, null)));

        Assert.Equal("recommendation.not_found", ex.Code);
    }

    [Fact]
    public async Task GetById_ReturnsDtoOrNull()
    {
        var created = await Generator(Runs.Completed("run1", "n2")).HandleAsync(new GenerateRecommendationsCommand("run1"));
        var handler = new GetRecommendationByIdQueryHandler(_repo);

        var found = await handler.HandleAsync(new GetRecommendationByIdQuery(created[0].Id));
        var missing = await handler.HandleAsync(new GetRecommendationByIdQuery("nope"));

        Assert.Equal(created[0].Id, found?.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task List_FiltersByRunAndStatus()
    {
        var a = await Generator(Runs.Completed("runA", "n1", "n2")).HandleAsync(new GenerateRecommendationsCommand("runA"));
        await Generator(Runs.Completed("runB", "n3")).HandleAsync(new GenerateRecommendationsCommand("runB"));
        await Responder().HandleAsync(new RespondToRecommendationCommand(a[0].Id, "approve", null, null));
        var handler = new GetRecommendationsQueryHandler(_repo);

        var all = await handler.HandleAsync(new GetRecommendationsQuery(null, null));
        var runA = await handler.HandleAsync(new GetRecommendationsQuery("runA", null));
        var approved = await handler.HandleAsync(new GetRecommendationsQuery(null, "approved"));
        var delivered = await handler.HandleAsync(new GetRecommendationsQuery("runA", "Delivered"));

        Assert.Equal(3, all.Count);
        Assert.Equal(2, runA.Count);
        Assert.Single(approved);
        Assert.Single(delivered);
    }

    [Fact]
    public async Task List_InvalidStatus_Throws()
    {
        await Assert.ThrowsAsync<InvalidRecommendationResponseException>(() =>
            new GetRecommendationsQueryHandler(_repo).HandleAsync(new GetRecommendationsQuery(null, "bogus")));
    }
}
