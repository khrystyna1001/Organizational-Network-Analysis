using ONA.Analysis.Domain.Entities;
using ONA.Analysis.Domain.Exceptions;
using ONA.Analysis.Domain.Services;
using ONA.Analysis.Domain.ValueObjects;

namespace ONA.Analysis.Domain.Tests;

public sealed class NetworkMetricsCalculatorTests
{
    private readonly NetworkMetricsCalculator _calculator = new();

    private static NetworkGraph Graph(string[] nodes, params (string From, string To)[] edges) =>
        new(nodes.Select(n => new NetworkNode(n, n.ToUpperInvariant())),
            edges.Select(e => new NetworkConnection(e.From, e.To, 1.0)));

    private static NodeMetrics Node(NetworkAnalysisResult result, string id) =>
        result.NodeMetrics.Single(n => n.NodeId == id);

    [Fact]
    public void Chain_MiddleNodeHasBetweennessHalfAndIsOnlyBottleneck()
    {
        var graph = Graph(["a", "b", "c"], ("a", "b"), ("b", "c"));

        var result = _calculator.Calculate(graph);

        Assert.Equal(0.5, Node(result, "b").Betweenness, 9);
        Assert.Equal(0, Node(result, "a").Betweenness, 9);
        Assert.Equal(0, Node(result, "c").Betweenness, 9);
        Assert.Equal(["b"], result.NodeMetrics.Where(n => n.IsBottleneck).Select(n => n.NodeId));
    }

    [Fact]
    public void Chain_ComputesDegreesDensityAndAverageDegree()
    {
        var graph = Graph(["a", "b", "c"], ("a", "b"), ("b", "c"));

        var result = _calculator.Calculate(graph);

        Assert.Equal(3, result.Metrics.NodeCount);
        Assert.Equal(2, result.Metrics.ConnectionCount);
        Assert.Equal(2.0 / 6.0, result.Metrics.Density, 9);
        Assert.Equal(2.0 / 3.0, result.Metrics.AvgDegree, 9);
        Assert.Equal((1, 1), (Node(result, "b").InDegree, Node(result, "b").OutDegree));
        Assert.Equal((0, 1), (Node(result, "a").InDegree, Node(result, "a").OutDegree));
        Assert.Equal((1, 0), (Node(result, "c").InDegree, Node(result, "c").OutDegree));
    }

    [Fact]
    public void Star_CenterHasBetweennessOne()
    {
        var graph = Graph(
            ["c", "l1", "l2", "l3"],
            ("c", "l1"), ("l1", "c"), ("c", "l2"), ("l2", "c"), ("c", "l3"), ("l3", "c"));

        var result = _calculator.Calculate(graph);

        Assert.Equal(1.0, Node(result, "c").Betweenness, 9);
        Assert.All(["l1", "l2", "l3"], id => Assert.Equal(0, Node(result, id).Betweenness, 9));
        Assert.Equal(["c"], result.NodeMetrics.Where(n => n.IsBottleneck).Select(n => n.NodeId));
    }

    [Fact]
    public void Cycle_EveryNodeHasBetweennessHalf_AndPolicyFlagsAllEqualNodes()
    {
        var graph = Graph(["a", "b", "c"], ("a", "b"), ("b", "c"), ("c", "a"));

        var result = _calculator.Calculate(graph);

        Assert.All(result.NodeMetrics, n => Assert.Equal(0.5, n.Betweenness, 9));
        // stddev is 0, so betweenness >= mean + 0 holds for every node with betweenness > 0.
        Assert.All(result.NodeMetrics, n => Assert.True(n.IsBottleneck));
    }

    [Fact]
    public void Diamond_SplitsDependencyAcrossEqualShortestPaths()
    {
        var graph = Graph(["a", "b", "c", "d"], ("a", "b"), ("a", "c"), ("b", "d"), ("c", "d"));

        var result = _calculator.Calculate(graph);

        Assert.Equal(0.5 / 6.0, Node(result, "b").Betweenness, 9);
        Assert.Equal(0.5 / 6.0, Node(result, "c").Betweenness, 9);
        Assert.Equal(0, Node(result, "a").Betweenness, 9);
    }

    [Fact]
    public void EmptyGraph_HasZeroMetricsAndNoNodes()
    {
        var result = _calculator.Calculate(Graph([]));

        Assert.Equal(0, result.Metrics.NodeCount);
        Assert.Equal(0, result.Metrics.Density);
        Assert.Equal(0, result.Metrics.AvgDegree);
        Assert.Empty(result.NodeMetrics);
    }

    [Fact]
    public void SingleNode_HasNoBottleneckAndZeroDensity()
    {
        var result = _calculator.Calculate(Graph(["a"]));

        Assert.Equal(0, result.Metrics.Density);
        Assert.False(Node(result, "a").IsBottleneck);
        Assert.Equal(0, Node(result, "a").Betweenness);
    }

    [Fact]
    public void NodesWithoutAnyBetweenness_AreNeverBottlenecks()
    {
        var graph = Graph(["a", "b", "c"], ("a", "b"), ("a", "c"));

        var result = _calculator.Calculate(graph);

        Assert.DoesNotContain(result.NodeMetrics, n => n.IsBottleneck);
    }

    [Fact]
    public void SameGraph_GivesIdenticalResults()
    {
        var graph = Graph(["a", "b", "c", "d"], ("a", "b"), ("b", "c"), ("c", "d"), ("a", "c"));

        var first = _calculator.Calculate(graph);
        var second = _calculator.Calculate(graph);

        Assert.Equal(
            first.NodeMetrics.Select(n => (n.NodeId, n.Betweenness, n.IsBottleneck)),
            second.NodeMetrics.Select(n => (n.NodeId, n.Betweenness, n.IsBottleneck)));
    }

    [Fact]
    public void Graph_RejectsDuplicateNodeIds()
    {
        var exception = Assert.Throws<InvalidNetworkGraphException>(() => Graph(["a", "a"]));

        Assert.Equal("analysis_run.invalid_graph", exception.Code);
    }

    [Theory]
    [InlineData("a", "zzz", 1.0)]
    [InlineData("a", "a", 1.0)]
    [InlineData("a", "b", 0.0)]
    [InlineData("a", "b", double.NaN)]
    public void Graph_RejectsInvalidConnections(string from, string to, double weight)
    {
        var nodes = new[] { new NetworkNode("a", "A"), new NetworkNode("b", "B") };

        var exception = Assert.Throws<InvalidNetworkGraphException>(
            () => new NetworkGraph(nodes, [new NetworkConnection(from, to, weight)]));

        Assert.Equal("analysis_run.invalid_graph", exception.Code);
    }

    [Fact]
    public void Graph_RejectsDuplicateDirectedConnection()
    {
        var nodes = new[] { new NetworkNode("a", "A"), new NetworkNode("b", "B") };

        Assert.Throws<InvalidNetworkGraphException>(() => new NetworkGraph(
            nodes, [new NetworkConnection("a", "b", 1), new NetworkConnection("a", "b", 2)]));
    }
}
