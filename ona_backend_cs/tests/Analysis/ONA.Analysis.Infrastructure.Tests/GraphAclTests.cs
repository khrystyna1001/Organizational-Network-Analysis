using System.Net;
using System.Text;
using ONA.Analysis.Application.Ports;
using ONA.Analysis.Domain.Exceptions;
using ONA.Analysis.Infrastructure.Acl;
using ONA.Analysis.Infrastructure.Acl.ExternalContracts;

namespace ONA.Analysis.Infrastructure.Tests;

public sealed class GraphAclTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (GraphSnapshotAcl Acl, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var adapter = new GraphHttpAdapter(new HttpClient(handler) { BaseAddress = new Uri("http://graph/") });
        return (new GraphSnapshotAcl(adapter, new GraphSnapshotTranslator()), handler);
    }

    private const string Snapshot = """
        {"board_id":"b1","graph_version_id":"v1","lifecycle":"Published",
         "nodes":[{"id":"n1","name":"Ann","title":"CEO","department":"X"},{"id":"n2","name":"Bob","title":"Dev","department":"Y"}],
         "connections":[{"id":"c1","source_node_id":"n1","target_node_id":"n2","weight":2.5}]}
        """;

    [Fact]
    public async Task Success_MapsSnapshotAndCallsSnapshotEndpoint()
    {
        var (acl, handler) = Create(_ => Json(HttpStatusCode.OK, Snapshot));

        var snapshot = await acl.GetSnapshotAsync("b1");

        Assert.Equal("http://graph/api/v1/boards/b1/snapshot", handler.Last!.RequestUri!.ToString());
        Assert.Equal("v1", snapshot.GraphVersionId);
        Assert.Equal(["Ann", "Bob"], snapshot.Nodes.Select(n => n.Name));
        var connection = Assert.Single(snapshot.Connections);
        Assert.Equal(("n1", "n2", 2.5), (connection.SourceNodeId, connection.TargetNodeId, connection.Weight));
    }

    [Fact]
    public async Task NotFoundWithBoardCode_BecomesBoardNotFound()
    {
        var (acl, _) = Create(_ => Json(HttpStatusCode.NotFound, """{"code":"board.not_found"}"""));

        await Assert.ThrowsAsync<BoardNotFoundException>(() => acl.GetSnapshotAsync("b1"));
    }

    [Fact]
    public async Task UnprocessableWithNotPublishedCode_BecomesGraphNotPublished()
    {
        var (acl, _) = Create(_ => Json(HttpStatusCode.UnprocessableEntity, """{"code":"board.graph_not_published"}"""));

        await Assert.ThrowsAsync<GraphNotPublishedException>(() => acl.GetSnapshotAsync("b1"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "not json")]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    public async Task OtherFailures_BecomeGraphIntegrationException(HttpStatusCode status, string body)
    {
        var (acl, _) = Create(_ => Json(status, body));

        await Assert.ThrowsAsync<GraphIntegrationException>(() => acl.GetSnapshotAsync("b1"));
    }

    [Fact]
    public async Task UnreachableGraph_BecomesGraphIntegrationException()
    {
        var (acl, _) = Create(_ => throw new HttpRequestException("down"));

        await Assert.ThrowsAsync<GraphIntegrationException>(() => acl.GetSnapshotAsync("b1"));
    }

    [Fact]
    public void Translator_WithoutGraphVersion_MeansNotPublished()
    {
        var dto = new GRAPH_SNAPSHOT_DTO { BoardId = "b1", Nodes = [], Connections = [] };

        Assert.Throws<GraphNotPublishedException>(() => new GraphSnapshotTranslator().ToSnapshot(dto, "b1"));
    }

    [Fact]
    public void Translator_WithoutNodes_IsIntegrationError()
    {
        var dto = new GRAPH_SNAPSHOT_DTO { BoardId = "b1", GraphVersionId = "v1" };

        Assert.Throws<GraphIntegrationException>(() => new GraphSnapshotTranslator().ToSnapshot(dto, "b1"));
    }
}
