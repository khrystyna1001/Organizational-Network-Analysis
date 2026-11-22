using Microsoft.Extensions.DependencyInjection;
using ONA.Graph.Application.Abstractions.Messaging;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Features.Boards.Commands;
using ONA.Graph.Application.Features.Boards.Queries;
using ONA.Graph.Application.Services;
using ONA.Graph.Infrastructure;

namespace ONA.Graph.Infrastructure.IntegrationTests;

[Collection(MySqlCollection.Name)]
public sealed class GraphPersistenceTests(MySqlDatabaseFixture fixture)
{
    [Fact]
    public async Task MigrationsCreateSchemaAndGraphDataSurvivesContextRestart()
    {
        await fixture.ResetDatabaseAsync();
        await using var services = CreateServices();
        string boardId;
        string firstNodeId;
        string secondNodeId;

        await using (var scope = services.CreateAsyncScope())
        {
            var graph = scope.ServiceProvider.GetRequiredService<IGraphService>();
            var board = await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateBoardCommand, BoardDto>>()
                .HandleAsync(new CreateBoardCommand("Engineering", "Graph persistence test"));
            boardId = board.Id;

            var first = await graph.AddNodeAsync(
                boardId,
                new CreateNodeDto(boardId, "Alex", "Director", "Engineering"));
            firstNodeId = first.Id;
            var second = await graph.AddNodeAsync(
                boardId,
                new CreateNodeDto(boardId, "Sam", "Manager", "Engineering"));
            secondNodeId = second.Id;
            await graph.AddConnectionAsync(
                boardId,
                new CreateConnectionDto(boardId, firstNodeId, secondNodeId, 1.5));
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var graph = scope.ServiceProvider.GetRequiredService<IGraphService>();
            var board = await scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetBoardByIdQuery, BoardDto?>>()
                .HandleAsync(new GetBoardByIdQuery(boardId));
            var nodes = await graph.GetNodesAsync(boardId);
            var connections = await graph.GetConnectionsAsync(boardId);

            Assert.NotNull(board);
            Assert.Equal("Engineering", board.Name);
            Assert.Equal(2, nodes.Count);
            Assert.Equal(
                new[] { firstNodeId, secondNodeId }.Order(),
                nodes.Select(node => node.Id).Order());
            var connection = Assert.Single(connections);
            Assert.Equal(firstNodeId, connection.SourceNodeId);
            Assert.Equal(secondNodeId, connection.TargetNodeId);
            Assert.Equal(1.5, connection.Weight);
        }
    }

    [Fact]
    public async Task RemovingNodePersistsRemovalOfItsIncidentConnections()
    {
        await fixture.ResetDatabaseAsync();
        await using var services = CreateServices();
        string boardId;
        string firstNodeId;

        await using (var scope = services.CreateAsyncScope())
        {
            var graph = scope.ServiceProvider.GetRequiredService<IGraphService>();
            var board = await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateBoardCommand, BoardDto>>()
                .HandleAsync(new CreateBoardCommand("Engineering", null));
            boardId = board.Id;
            var first = await graph.AddNodeAsync(
                boardId,
                new CreateNodeDto(boardId, "Alex", "Director", "Engineering"));
            firstNodeId = first.Id;
            var second = await graph.AddNodeAsync(
                boardId,
                new CreateNodeDto(boardId, "Sam", "Manager", "Engineering"));
            await graph.AddConnectionAsync(
                boardId,
                new CreateConnectionDto(boardId, first.Id, second.Id, 1.0));
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var graph = scope.ServiceProvider.GetRequiredService<IGraphService>();
            Assert.True(await graph.DeleteNodeAsync(firstNodeId));
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var graph = scope.ServiceProvider.GetRequiredService<IGraphService>();
            Assert.Empty(await graph.GetConnectionsAsync(boardId));
            Assert.Single(await graph.GetNodesAsync(boardId));
        }
    }

    [Fact]
    public async Task LifecycleCommandsPersistStateAndPublishedBoardCannotBeDeleted()
    {
        await fixture.ResetDatabaseAsync();
        await using var services = CreateServices();
        string boardId;

        await using (var scope = services.CreateAsyncScope())
        {
            var board = await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateBoardCommand, BoardDto>>()
                .HandleAsync(new CreateBoardCommand("Lifecycle", null));
            boardId = board.Id;
            await scope.ServiceProvider.GetRequiredService<IGraphService>()
                .AddNodeAsync(boardId, new CreateNodeDto(boardId, "Alex", "Director", "Engineering"));
        }

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<BuildBoardGraphCommand, BoardDto>>()
                .HandleAsync(new BuildBoardGraphCommand(boardId));
        }

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<PublishBoardCommand, BoardDto>>()
                .HandleAsync(new PublishBoardCommand(boardId));
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var reloaded = await scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetBoardByIdQuery, BoardDto?>>()
                .HandleAsync(new GetBoardByIdQuery(boardId));
            Assert.NotNull(reloaded);
            Assert.Equal("Published", reloaded.Lifecycle);
            Assert.NotNull(reloaded.CurrentGraphVersionId);

            await Assert.ThrowsAsync<ONA.Graph.Domain.Exceptions.BoardDeletionNotAllowedException>(
                () => scope.ServiceProvider
                    .GetRequiredService<ICommandHandler<DeleteBoardCommand, bool>>()
                    .HandleAsync(new DeleteBoardCommand(boardId)));
        }

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<ArchiveBoardCommand, BoardDto>>()
                .HandleAsync(new ArchiveBoardCommand(boardId));
            Assert.True(await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<DeleteBoardCommand, bool>>()
                .HandleAsync(new DeleteBoardCommand(boardId)));
        }

        await using (var scope = services.CreateAsyncScope())
        {
            Assert.Null(await scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetBoardByIdQuery, BoardDto?>>()
                .HandleAsync(new GetBoardByIdQuery(boardId)));
        }
    }

    private ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(fixture.ConnectionString);
        return services.BuildServiceProvider();
    }
}
