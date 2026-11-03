using EchoAPI.Graph.Application.Abstractions;
using EchoAPI.Graph.Application.DTOs;
using EchoAPI.Graph.Application.Services;
using EchoAPI.Graph.Domain.Aggregates;
using EchoAPI.Graph.Domain.Entities;
using EchoAPI.Graph.Domain.Exceptions;
using Moq;

namespace EchoAPI.Graph.Application.Tests;

public sealed class GraphServiceTests
{
    [Fact]
    public async Task CreateBoardAsyncSavesNewBoard()
    {
        var repository = new Mock<IBoardRepository>();
        var service = new GraphService(repository.Object);

        var result = await service.CreateBoardAsync(
            new CreateBoardDto("Engineering", "ONA"));

        Assert.Equal("Engineering", result.Name);
        repository.Verify(
            item => item.SaveAsync(
                It.Is<Board>(board => board.Name == "Engineering"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddNodeAsyncUpdatesAndSavesExistingBoard()
    {
        var board = new Board("board-id", "Engineering", null);
        var repository = new Mock<IBoardRepository>();
        repository.Setup(item => item.GetByIdAsync(
                board.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(board);
        var service = new GraphService(repository.Object);

        var result = await service.AddNodeAsync(
            board.Id,
            new CreateNodeDto(board.Id, "Alex", "Director", "Engineering"));

        Assert.Equal(board.Id, result.BoardId);
        Assert.Equal("Alex", result.Name);
        repository.Verify(
            item => item.SaveAsync(board, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddNodeAsyncThrowsWhenBoardDoesNotExistAndNeverSaves()
    {
        var repository = new Mock<IBoardRepository>();
        repository.Setup(item => item.GetByIdAsync(
                "missing",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Board?)null);
        var service = new GraphService(repository.Object);

        var exception = await Assert.ThrowsAsync<BoardNotFoundException>(
            () => service.AddNodeAsync(
                "missing",
                new CreateNodeDto("missing", "Alex", "Director", "Engineering")));

        Assert.Equal("missing", exception.BoardId);
        repository.Verify(
            item => item.SaveAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddNodeAsyncPropagatesDomainFailureAndNeverSaves()
    {
        var board = new Board("board-id", "Engineering", null);
        var repository = new Mock<IBoardRepository>();
        repository.Setup(item => item.GetByIdAsync(
                board.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(board);
        var service = new GraphService(repository.Object);

        var exception = await Assert.ThrowsAsync<InvalidNodeException>(
            () => service.AddNodeAsync(
                board.Id,
                new CreateNodeDto(board.Id, "Alex", " ", "Engineering")));

        Assert.Equal("node.invalid_title", exception.Code);
        Assert.Equal("title", exception.FieldName);
        repository.Verify(
            item => item.SaveAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddConnectionAsyncSavesAggregateAfterValidCommand()
    {
        var board = new Board("board-id", "Engineering", null);
        var source = board.AddNode("Alex", "Director", "Engineering");
        var target = board.AddNode("Sam", "Manager", "Engineering");
        var repository = new Mock<IBoardRepository>();
        repository.Setup(item => item.GetByIdAsync(
                board.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(board);
        var service = new GraphService(repository.Object);

        var result = await service.AddConnectionAsync(
            board.Id,
            new CreateConnectionDto(board.Id, source.Id, target.Id, 2.0));

        Assert.Equal(source.Id, result.SourceNodeId);
        Assert.Equal(2.0, result.Weight);
        repository.Verify(
            item => item.SaveAsync(board, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBoardAsyncMapsAggregateToReadModel()
    {
        var board = new Board("board-id", "Engineering", "ONA");
        board.AddNode("Alex", "Director", "Engineering");
        var repository = new Mock<IBoardRepository>();
        repository.Setup(item => item.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { board });
        var service = new GraphService(repository.Object);

        var result = await service.GetNodesAsync(board.Id);

        var node = Assert.Single(result);
        Assert.Equal("Alex", node.Name);
    }
}
