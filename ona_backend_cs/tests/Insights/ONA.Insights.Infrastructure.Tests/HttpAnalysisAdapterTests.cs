using System.Net;
using System.Text;
using ONA.Insights.Application.Ports;
using ONA.Insights.Infrastructure.Acl;

namespace ONA.Insights.Infrastructure.Tests;

public sealed class HttpAnalysisAdapterTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }

    private static HttpAnalysisAdapter Adapter(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://analysis.test/") });

    [Fact]
    public async Task Success_ParsesSnakeCaseJson()
    {
        const string json = """
            {"id":"run1","board_id":"b1","graph_version_id":"v1","status":"Completed",
             "node_metrics":[{"node_id":"n1","node_name":"Alice","in_degree":1,"out_degree":2,"betweenness":0.5,"is_bottleneck":true}],
             "bottlenecks":["n1"]}
            """;
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var dto = await Adapter(handler).ReadRunAsync("run1", CancellationToken.None);

        Assert.Equal("http://analysis.test/api/v1/analysis-runs/run1", handler.LastUri?.ToString());
        Assert.Equal("v1", dto?.GraphVersionId);
        Assert.Equal("Alice", dto?.NodeMetrics?[0].NodeName);
        Assert.True(dto?.NodeMetrics?[0].IsBottleneck);
    }

    [Fact]
    public async Task NotFound_ReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Null(await Adapter(handler).ReadRunAsync("x", CancellationToken.None));
    }

    [Fact]
    public async Task ServerError_IsIntegrationError()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<AnalysisIntegrationException>(() => Adapter(handler).ReadRunAsync("x", CancellationToken.None));
    }

    [Fact]
    public async Task ConnectionFailure_IsIntegrationError()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("refused"));

        await Assert.ThrowsAsync<AnalysisIntegrationException>(() => Adapter(handler).ReadRunAsync("x", CancellationToken.None));
    }

    [Fact]
    public async Task GarbageBody_IsIntegrationError()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json", Encoding.UTF8, "application/json")
        });

        await Assert.ThrowsAsync<AnalysisIntegrationException>(() => Adapter(handler).ReadRunAsync("x", CancellationToken.None));
    }
}
