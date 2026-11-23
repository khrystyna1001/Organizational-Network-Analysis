namespace ONA.Analysis.Domain.ValueObjects;

/// <summary>Whole-graph metrics of one run.</summary>
public sealed class GraphMetrics
{
    public int NodeCount { get; }
    public int ConnectionCount { get; }
    public double Density { get; }
    public double AvgDegree { get; }

    public GraphMetrics(int nodeCount, int connectionCount, double density, double avgDegree)
    {
        NodeCount = nodeCount;
        ConnectionCount = connectionCount;
        Density = density;
        AvgDegree = avgDegree;
    }
}

/// <summary>Per-node metrics of one run.</summary>
public sealed class NodeMetrics
{
    public string NodeId { get; }
    public string NodeName { get; }
    public int InDegree { get; }
    public int OutDegree { get; }
    public double Betweenness { get; }
    public bool IsBottleneck { get; }

    public NodeMetrics(string nodeId, string nodeName, int inDegree, int outDegree, double betweenness, bool isBottleneck)
    {
        NodeId = nodeId;
        NodeName = nodeName;
        InDegree = inDegree;
        OutDegree = outDegree;
        Betweenness = betweenness;
        IsBottleneck = isBottleneck;
    }
}

/// <summary>Output of <c>NetworkMetricsCalculator</c>.</summary>
public sealed class NetworkAnalysisResult
{
    public GraphMetrics Metrics { get; }
    public IReadOnlyList<NodeMetrics> NodeMetrics { get; }

    public NetworkAnalysisResult(GraphMetrics metrics, IReadOnlyList<NodeMetrics> nodeMetrics)
    {
        Metrics = metrics;
        NodeMetrics = nodeMetrics;
    }
}
