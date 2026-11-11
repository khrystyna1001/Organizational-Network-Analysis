using Microsoft.Extensions.DependencyInjection;
using ONA.Graph.Application.DTOs;
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
            var board = await graph.CreateBoardAsync(
                new CreateBoardDto("Engineering", "Graph persistence test"));
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
            var board = await graph.GetBoardAsync(boardId);
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
            var board = await graph.CreateBoardAsync(new CreateBoardDto("Engineering", null));
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

    private ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(fixture.ConnectionString);
        return services.BuildServiceProvider();
    }
}
