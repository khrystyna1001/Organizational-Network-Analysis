using ONA.Insights.Application.Ports;
using ONA.Insights.Infrastructure.Acl;
using ONA.Insights.Infrastructure.Acl.ExternalContracts;

namespace ONA.Insights.Infrastructure.Tests;

/// <summary>Every mapping-table row of the Analysis ACL has a test.</summary>
public sealed class AnalysisRunTranslatorTests
{
    private readonly AnalysisRunTranslator _translator = new();

    private static ANALYSIS_RUN_DTO Dto(string? status = "Completed") => new()
    {
        Id = "run1", BoardId = "b1", GraphVersionId = "v1", Status = status,
        NodeMetrics =
        [
            new() { NodeId = "n1", NodeName = "Alice", InDegree = 1, OutDegree = 2, Betweenness = 0.7, IsBottleneck = true },
            new() { NodeId = "n2", NodeName = "Bob", InDegree = 0, OutDegree = 1, Betweenness = 0.0, IsBottleneck = false }
        ],
        Bottlenecks = ["n1"]
    };

    [Fact]
    public void MapsIdentifiersAndStatus()
    {
        var result = _translator.ToResult(Dto());

        Assert.Equal("run1", result.Id.Value);
        Assert.Equal("b1", result.BoardId.Value);
        Assert.Equal("v1", result.GraphVersionId.Value);
        Assert.Equal(AnalysisRunStatus.Completed, result.Status);
    }

    [Theory]
    [InlineData("Running", AnalysisRunStatus.Running)]
    [InlineData("failed", AnalysisRunStatus.Failed)]
    public void MapsOtherStatuses(string status, AnalysisRunStatus expected) =>
        Assert.Equal(expected, _translator.ToResult(Dto(status)).Status);

    [Fact]
    public void UnknownStatus_IsIntegrationError() =>
        Assert.Throws<AnalysisIntegrationException>(() => _translator.ToResult(Dto("Weird")));

    [Fact]
    public void MapsNodeMetrics()
    {
        var node = _translator.ToResult(Dto()).Nodes[0];

        Assert.Equal("n1", node.NodeId.Value);
        Assert.Equal("Alice", node.NodeName);
        Assert.Equal(1, node.InDegree);
        Assert.Equal(2, node.OutDegree);
        Assert.Equal(0.7, node.Betweenness);
        Assert.True(node.IsBottleneck);
    }

    [Fact]
    public void BottleneckList_FlagsNodeEvenWhenNodeFlagIsFalse()
    {
        var dto = Dto();
        dto.Bottlenecks = ["n2"];

        var nodes = _translator.ToResult(dto).Nodes;

        Assert.True(nodes.Single(n => n.NodeId.Value == "n2").IsBottleneck);
    }

    [Fact]
    public void MissingNodeName_FallsBackToNodeId()
    {
        var dto = Dto();
        dto.NodeMetrics![0].NodeName = null;

        Assert.Equal("n1", _translator.ToResult(dto).Nodes[0].NodeName);
    }

    [Fact]
    public void MissingNodeMetrics_MeansNoNodes()
    {
        var dto = Dto();
        dto.NodeMetrics = null;

        Assert.Empty(_translator.ToResult(dto).Nodes);
    }

    [Fact]
    public void MissingRunId_IsIntegrationError()
    {
        var dto = Dto();
        dto.Id = null;

        Assert.Throws<AnalysisIntegrationException>(() => _translator.ToResult(dto));
    }
}
