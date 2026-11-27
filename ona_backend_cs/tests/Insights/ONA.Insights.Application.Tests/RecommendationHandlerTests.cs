using ONA.Insights.Application.Features.Recommendations.Commands;
using ONA.Insights.Application.Features.Recommendations.Queries;
using ONA.Insights.Application.Notifications;
using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.Exceptions;

namespace ONA.Insights.Application.Tests;

public sealed class RecommendationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryRecommendationRepository _repo = new();
    private readonly FakeInsightGenerator _generator = new();

    private readonly RecordingPublisher _notifications = new();

    private GenerateRecommendationsCommandHandler Generator(AnalysisRunResult? run) =>
        new(_repo, new FakeAnalysisResultsPort(run), _generator, new FixedClock(Now), _notifications);

    private RespondToRecommendationCommandHandler Responder() => new(_repo, new FixedClock(Now), _notifications);

    [Fact]
    public async Task Generate_PublishesOneNotificationWithTheNumberOfNewRecommendations()
    {
        var handler = Generator(Runs.Completed("run1", "n2", "n3"));

        await handler.Handle(new GenerateRecommendationsCommand("run1"), default);

        var notification = Assert.IsType<RecommendationsGeneratedNotification>(Assert.Single(_notifications.Published));
        Assert.Equal(("run1", "b1", 2), (notification.AnalysisRunId, notification.BoardId, notification.Count));
    }

    [Fact]
    public async Task Generate_Twice_PublishesNothingTheSecondTime()
    {
        var handler = Generator(Runs.Completed("run1", "n2", "n3"));
        await handler.Handle(new GenerateRecommendationsCommand("run1"), default);

        await handler.Handle(new GenerateRecommendationsCommand("run1"), default);

        Assert.Single(_notifications.Published);
    }

    [Fact]
    public async Task Generate_WhenNotificationFails_StillReturnsSavedRecommendations()
    {
        _notifications.ThrowOnPublish = true;

        var result = await Generator(Runs.Completed("run1", "n2")).Handle(new GenerateRecommendationsCommand("run1"), default);

        Assert.Single(result);
        Assert.Single(_repo.Items);
    }

    [Fact]
    public async Task Generate_CreatesOneRecommendationPerBottleneckWithProvenance()
    {
        var handler = Generator(Runs.Completed("run1", "n2", "n3"));

        var result = await handler.Handle(new GenerateRecommendationsCommand("run1"), default);

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
        var first = await handler.Handle(new GenerateRecommendationsCommand("run1"), default);

        var second = await handler.Handle(new GenerateRecommendationsCommand("run1"), default);

        Assert.Equal(first.Select(r => r.Id), second.Select(r => r.Id));
        Assert.Equal(2, _repo.Items.Count);
        Assert.Equal(2, _generator.Calls);
    }

    [Fact]
    public async Task Generate_RunNotCompleted_Throws()
    {
        var running = Runs.Completed("run1", "n1") with { Status = AnalysisRunStatus.Running };

        var ex = await Assert.ThrowsAsync<AnalysisRunNotCompletedException>(() =>
            Generator(running).Handle(new GenerateRecommendationsCommand("run1"), default));

        Assert.Equal("recommendation.analysis_run_not_completed", ex.Code);
        Assert.Empty(_repo.Items);
    }

    [Fact]
    public async Task Generate_NoBottlenecks_Throws()
    {
        var ex = await Assert.ThrowsAsync<NoBottlenecksException>(() =>
            Generator(Runs.Completed("run1")).Handle(new GenerateRecommendationsCommand("run1"), default));

        Assert.Equal("recommendation.no_bottlenecks", ex.Code);
    }

    [Fact]
    public async Task Generate_RunNotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<AnalysisRunNotFoundException>(() =>
            Generator(null).Handle(new GenerateRecommendationsCommand("missing"), default));

        Assert.Equal("recommendation.analysis_run_not_found", ex.Code);
    }

    [Fact]
    public async Task Respond_Approve_UpdatesStatusAndSaves()
    {
        var created = await Generator(Runs.Completed("run1", "n2")).Handle(new GenerateRecommendationsCommand("run1"), default);

        var dto = await Responder().Handle(
            new RespondToRecommendationCommand(created[0].Id, "Approve", "go", "user-1"));

        Assert.Equal("Approved", dto.Status);
        Assert.Equal("go", dto.UserComment);
        Assert.Equal("user-1", dto.RespondedByUserId);
        Assert.Equal(Now.UtcDateTime, dto.RespondedAt);
        Assert.Equal(1, _repo.SaveCount);
    }

    [Fact]
    public async Task Respond_PublishesNotificationWithTheNewStatus()
    {
        var created = await Generator(Runs.Completed("run1", "n2")).Handle(new GenerateRecommendationsCommand("run1"), default);
        _notifications.Published.Clear();

        await Responder().Handle(new RespondToRecommendationCommand(created[0].Id, "approve", null, "user-1"), default);

        var notification = Assert.IsType<RecommendationRespondedNotification>(Assert.Single(_notifications.Published));
        Assert.Equal((created[0].Id, "run1", "b1", "Approved"),
            (notification.RecommendationId, notification.AnalysisRunId, notification.BoardId, notification.Status));
    }

    [Fact]
    public async Task Respond_WhenNotificationFails_StillSavesTheDecision()
    {
        var created = await Generator(Runs.Completed("run1", "n2")).Handle(new GenerateRecommendationsCommand("run1"), default);
        _notifications.ThrowOnPublish = true;

        var dto = await Responder().Handle(new RespondToRecommendationCommand(created[0].Id, "reject", null, null), default);

        Assert.Equal("Rejected", dto.Status);
        Assert.Equal(1, _repo.SaveCount);
    }

    [Fact]
    public async Task Respond_Reject_UpdatesStatus()
    {
        var created = await Generator(Runs.Completed("run1", "n2")).Handle(new GenerateRecommendationsCommand("run1"), default);

        var dto = await Responder().Handle(new RespondToRecommendationCommand(created[0].Id, "reject", null, null), default);

        Assert.Equal("Rejected", dto.Status);
    }

    [Fact]
    public async Task Respond_Twice_ThrowsInvalidStateTransition()
    {
        var created = await Generator(Runs.Completed("run1", "n2")).Handle(new GenerateRecommendationsCommand("run1"), default);
        await Responder().Handle(new RespondToRecommendationCommand(created[0].Id, "approve", null, null), default);

        var ex = await Assert.ThrowsAsync<InvalidRecommendationStateTransitionException>(() =>
            Responder().Handle(new RespondToRecommendationCommand(created[0].Id, "reject", null, null), default));

        Assert.Equal("recommendation.invalid_state_transition", ex.Code);
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Respond_InvalidResponse_Throws(string? response)
    {
        var created = await Generator(Runs.Completed("run1", "n2")).Handle(new GenerateRecommendationsCommand("run1"), default);

        var ex = await Assert.ThrowsAsync<InvalidRecommendationResponseException>(() =>
            Responder().Handle(new RespondToRecommendationCommand(created[0].Id, response, null, null), default));

        Assert.Equal("recommendation.invalid_response", ex.Code);
        Assert.Equal(RecommendationStatus.Delivered, _repo.Items[0].Status);
    }

    [Fact]
    public async Task Respond_UnknownRecommendation_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<RecommendationNotFoundException>(() =>
            Responder().Handle(new RespondToRecommendationCommand("nope", "approve", null, null), default));

        Assert.Equal("recommendation.not_found", ex.Code);
    }

    [Fact]
    public async Task GetById_ReturnsDtoOrNull()
    {
        var created = await Generator(Runs.Completed("run1", "n2")).Handle(new GenerateRecommendationsCommand("run1"), default);
        var handler = new GetRecommendationByIdQueryHandler(_repo);

        var found = await handler.Handle(new GetRecommendationByIdQuery(created[0].Id), default);
        var missing = await handler.Handle(new GetRecommendationByIdQuery("nope"), default);

        Assert.Equal(created[0].Id, found?.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task List_FiltersByRunAndStatus()
    {
        var a = await Generator(Runs.Completed("runA", "n1", "n2")).Handle(new GenerateRecommendationsCommand("runA"), default);
        await Generator(Runs.Completed("runB", "n3")).Handle(new GenerateRecommendationsCommand("runB"), default);
        await Responder().Handle(new RespondToRecommendationCommand(a[0].Id, "approve", null, null), default);
        var handler = new GetRecommendationsQueryHandler(_repo);

        var all = await handler.Handle(new GetRecommendationsQuery(null, null), default);
        var runA = await handler.Handle(new GetRecommendationsQuery("runA", null), default);
        var approved = await handler.Handle(new GetRecommendationsQuery(null, "approved"), default);
        var delivered = await handler.Handle(new GetRecommendationsQuery("runA", "Delivered"), default);

        Assert.Equal(3, all.Count);
        Assert.Equal(2, runA.Count);
        Assert.Single(approved);
        Assert.Single(delivered);
    }

    [Fact]
    public async Task List_InvalidStatus_Throws()
    {
        await Assert.ThrowsAsync<InvalidRecommendationResponseException>(() =>
            new GetRecommendationsQueryHandler(_repo).Handle(new GetRecommendationsQuery(null, "bogus"), default));
    }
}
