using ONA.Graph.Application.Features.Boards.Commands;
using ONA.Graph.Application.Features.Boards.Queries;
using ONA.Graph.Application.Ports;
using ONA.Graph.Domain.Aggregates;
using ONA.Graph.Domain.Exceptions;
using ONA.Graph.Domain.ValueObjects;

namespace ONA.Graph.Application.Tests.Features;

public sealed class BoardCommandHandlerTests
{
    private readonly InMemoryBoardRepository _repository = new();

    [Fact]
    public async Task CreateBoard_WithValidName_ShouldPersistDraftBoard()
    {
        var handler = new CreateBoardCommandHandler(_repository);

        var result = await handler.HandleAsync(new CreateBoardCommand("Engineering", "ONA"));

        var stored = Assert.Single(_repository.Boards);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal("Engineering", stored.Name);
        Assert.Equal(nameof(BoardLifecycle.Draft), result.Lifecycle);
    }

    [Fact]
    public async Task CreateBoard_WithBlankName_ShouldThrowAndPersistNothing()
    {
        var handler = new CreateBoardCommandHandler(_repository);

        var exception = await Assert.ThrowsAsync<InvalidBoardException>(
            () => handler.HandleAsync(new CreateBoardCommand(" ", null)));

        Assert.Equal("board.invalid_name", exception.Code);
        Assert.Empty(_repository.Boards);
    }

    [Fact]
    public async Task RenameBoard_WithoutDescription_ShouldKeepCurrentDescription()
    {
        var board = _repository.Seed(new Board("b1", "Engineering", "ONA"));
        var handler = new RenameBoardCommandHandler(_repository);

        var result = await handler.HandleAsync(new RenameBoardCommand("b1", "People", null, false));

        Assert.Equal("People", board.Name);
        Assert.Equal("ONA", result.Description);
    }

    [Fact]
    public async Task BuildAndPublish_ShouldMoveBoardToPublishedAndSave()
    {
        var board = _repository.Seed(new Board("b1", "Engineering", null));
        board.AddNode("Alex", "Director", "Engineering");

        await new BuildBoardGraphCommandHandler(_repository).HandleAsync(new BuildBoardGraphCommand("b1"));
        var result = await new PublishBoardCommandHandler(_repository, new RecordingEventPublisher()).HandleAsync(new PublishBoardCommand("b1"));

        Assert.Equal(nameof(BoardLifecycle.Published), result.Lifecycle);
        Assert.NotNull(result.CurrentGraphVersionId);
        Assert.Equal(2, _repository.SaveCount);
    }

    [Fact]
    public async Task PublishBoard_WhenDraft_ShouldThrowAndNotSave()
    {
        _repository.Seed(new Board("b1", "Engineering", null));

        await Assert.ThrowsAsync<InvalidGraphStateTransitionException>(
            () => new PublishBoardCommandHandler(_repository, new RecordingEventPublisher()).HandleAsync(new PublishBoardCommand("b1")));

        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task ArchiveBoard_WhenBoardMissing_ShouldThrowBoardNotFound()
    {
        var exception = await Assert.ThrowsAsync<BoardNotFoundException>(
            () => new ArchiveBoardCommandHandler(_repository).HandleAsync(new ArchiveBoardCommand("missing")));

        Assert.Equal("missing", exception.BoardId);
    }

    [Fact]
    public async Task DeleteBoard_WhenPublished_ShouldThrowAndKeepBoard()
    {
        var board = _repository.Seed(new Board("b1", "Engineering", null));
        board.AddNode("Alex", "Director", "Engineering");
        board.BuildNetworkGraph();
        board.PublishGraphVersion();

        var exception = await Assert.ThrowsAsync<BoardDeletionNotAllowedException>(
            () => new DeleteBoardCommandHandler(_repository).HandleAsync(new DeleteBoardCommand("b1")));

        Assert.Equal("board.deletion_not_allowed", exception.Code);
        Assert.Single(_repository.Boards);
    }

    [Fact]
    public async Task DeleteBoard_WhenDraft_ShouldRemoveBoard()
    {
        _repository.Seed(new Board("b1", "Engineering", null));

        var deleted = await new DeleteBoardCommandHandler(_repository).HandleAsync(new DeleteBoardCommand("b1"));

        Assert.True(deleted);
        Assert.Empty(_repository.Boards);
    }

    [Fact]
    public async Task DeleteBoard_WhenMissing_ShouldReturnFalse()
    {
        var deleted = await new DeleteBoardCommandHandler(_repository).HandleAsync(new DeleteBoardCommand("missing"));

        Assert.False(deleted);
    }

    [Fact]
    public async Task Queries_ShouldProjectBoardsToDtos()
    {
        var board = _repository.Seed(new Board("b1", "Engineering", null));
        board.AddNode("Alex", "Director", "Engineering");

        var single = await new GetBoardByIdQueryHandler(_repository).HandleAsync(new GetBoardByIdQuery("b1"));
        var all = await new GetAllBoardsQueryHandler(_repository).HandleAsync(new GetAllBoardsQuery());
        var missing = await new GetBoardByIdQueryHandler(_repository).HandleAsync(new GetBoardByIdQuery("x"));

        Assert.NotNull(single);
        Assert.Equal(1, single.NodeCount);
        Assert.Single(all);
        Assert.Null(missing);
    }

    [Fact]
    public async Task ImportBoardFromHris_ShouldCreateNodesAndSupervisorConnections()
    {
        var port = new StubEmployeeDirectory(new OrganizationSnapshot(
        [
            new OrganizationMember("100", new NodeName("Alex One"), "Director", new DepartmentName("Management"), null),
            new OrganizationMember("101", new NodeName("Sam Smith"), "Manager", new DepartmentName("Engineering"), "100")
        ]));
        var handler = new ImportBoardFromHrisCommandHandler(port, _repository);

        var result = await handler.HandleAsync(new ImportBoardFromHrisCommand("Engineering", Stream.Null));

        var board = Assert.Single(_repository.Boards);
        Assert.Equal(2, result.NodesCreated);
        Assert.Equal(1, result.ConnectionsCreated);
        var connection = Assert.Single(board.Connections);
        Assert.Equal(board.Nodes.Single(n => n.Name == "Alex One").Id, connection.SourceNodeId);
        Assert.Equal(board.Nodes.Single(n => n.Name == "Sam Smith").Id, connection.TargetNodeId);
        Assert.Equal("Management", board.Nodes.Single(n => n.Name == "Alex One").Department);
    }

    [Fact]
    public async Task ImportBoardFromHris_ShouldPassTheColumnMappingToThePort()
    {
        var port = new StubEmployeeDirectory(new OrganizationSnapshot(
        [
            new OrganizationMember("row-2", new NodeName("Alex One"), "Employee", new DepartmentName("General"), null)
        ]));
        var mapping = new ColumnMapping(FullName: "Name", Supervisor: "Boss");

        await new ImportBoardFromHrisCommandHandler(port, _repository)
            .HandleAsync(new ImportBoardFromHrisCommand("Any CSV", Stream.Null, mapping));

        Assert.Equal(mapping, port.ReceivedMapping);
    }

    [Fact]
    public async Task ImportBoardFromHris_WithoutMapping_UsesTheHrisDefault()
    {
        var port = new StubEmployeeDirectory(new OrganizationSnapshot(
        [
            new OrganizationMember("1", new NodeName("Alex One"), "Director", new DepartmentName("Management"), null)
        ]));

        await new ImportBoardFromHrisCommandHandler(port, _repository)
            .HandleAsync(new ImportBoardFromHrisCommand("HRIS", Stream.Null));

        Assert.Null(port.ReceivedMapping);   // null = the port applies ColumnMapping.Hris
    }

    [Fact]
    public async Task PreviewCsv_ReturnsWhatThePortSuggestsAndSavesNothing()
    {
        var preview = new CsvPreview(["Name", "Boss"], [["Alex", ""]], 1, new ColumnMapping(FullName: "Name", Supervisor: "Boss"));

        var result = await new PreviewCsvQueryHandler(new StubEmployeeDirectory(new OrganizationSnapshot([]), preview))
            .HandleAsync(new PreviewCsvQuery(Stream.Null));

        Assert.Equal(preview, result);
        Assert.Empty(_repository.Boards);
    }

    /// <summary>Stub of the outbound port: returns already-translated data (the ACL is tested separately).</summary>
    private sealed class StubEmployeeDirectory(OrganizationSnapshot snapshot, CsvPreview? preview = null) : IEmployeeDirectoryPort
    {
        public ColumnMapping? ReceivedMapping { get; private set; }

        public Task<OrganizationSnapshot> ReadOrganizationAsync(
            Stream employeeExport,
            ColumnMapping? mapping = null,
            CancellationToken cancellationToken = default)
        {
            ReceivedMapping = mapping;
            return Task.FromResult(snapshot);
        }

        public Task<CsvPreview> PreviewAsync(Stream employeeExport, CancellationToken cancellationToken = default) =>
            Task.FromResult(preview ?? new CsvPreview([], [], 0, new ColumnMapping()));
    }
}
