using ONA.Analysis.Domain.Entities;
using ONA.Analysis.Domain.ValueObjects;

namespace ONA.Analysis.Domain.Services;

/// <summary>
/// Pure, deterministic metrics over a <see cref="NetworkGraph"/>.
/// Betweenness: Brandes' algorithm on the directed graph with unweighted shortest paths,
/// normalized by (n-1)(n-2) when n &gt; 2.
/// Bottleneck policy (default): betweenness &gt; 0 and betweenness &gt;= mean + 1 population standard
/// deviation of all nodes' betweenness. Excluding administrative activity is an undecided policy,
/// so no activity is excluded.
/// </summary>
public sealed class NetworkMetricsCalculator
{
    private const double Tolerance = 1e-12;

    public NetworkAnalysisResult Calculate(NetworkGraph graph)
    {
        var n = graph.Nodes.Count;
        var m = graph.Connections.Count;

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < n; i++)
        {
            index[graph.Nodes[i].Id] = i;
        }

        var adjacency = new List<int>[n];
        for (var i = 0; i < n; i++)
        {
            adjacency[i] = [];
        }

        var inDegree = new int[n];
        var outDegree = new int[n];
        foreach (var connection in graph.Connections)
        {
            var s = index[connection.SourceNodeId];
            var t = index[connection.TargetNodeId];
            adjacency[s].Add(t);
            outDegree[s]++;
            inDegree[t]++;
        }

        var betweenness = Betweenness(adjacency, n);
        var threshold = BottleneckThreshold(betweenness);

        var nodeMetrics = new List<NodeMetrics>(n);
        for (var i = 0; i < n; i++)
        {
            var isBottleneck = betweenness[i] > Tolerance && betweenness[i] >= threshold - Tolerance;
            nodeMetrics.Add(new NodeMetrics(
                graph.Nodes[i].Id, graph.Nodes[i].Name, inDegree[i], outDegree[i], betweenness[i], isBottleneck));
        }

        var density = n > 1 ? (double)m / ((double)n * (n - 1)) : 0d;
        var avgDegree = n > 0 ? (double)m / n : 0d;
        return new NetworkAnalysisResult(new GraphMetrics(n, m, density, avgDegree), nodeMetrics);
    }

    private static double[] Betweenness(List<int>[] adjacency, int n)
    {
        var result = new double[n];
        for (var source = 0; source < n; source++)
        {
            var stack = new Stack<int>();
            var predecessors = new List<int>[n];
            for (var i = 0; i < n; i++)
            {
                predecessors[i] = [];
            }

            var paths = new double[n];
            var distance = new int[n];
            Array.Fill(distance, -1);
            paths[source] = 1;
            distance[source] = 0;

            var queue = new Queue<int>();
            queue.Enqueue(source);
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                stack.Push(v);
                foreach (var w in adjacency[v])
                {
                    if (distance[w] < 0)
                    {
                        distance[w] = distance[v] + 1;
                        queue.Enqueue(w);
                    }

                    if (distance[w] == distance[v] + 1)
                    {
                        paths[w] += paths[v];
                        predecessors[w].Add(v);
                    }
                }
            }

            var dependency = new double[n];
            while (stack.Count > 0)
            {
                var w = stack.Pop();
                foreach (var v in predecessors[w])
                {
                    dependency[v] += paths[v] / paths[w] * (1 + dependency[w]);
                }

                if (w != source)
                {
                    result[w] += dependency[w];
                }
            }
        }

        if (n > 2)
        {
            var norm = (double)(n - 1) * (n - 2);
            for (var i = 0; i < n; i++)
            {
                result[i] /= norm;
            }
        }

        return result;
    }

    private static double BottleneckThreshold(double[] betweenness)
    {
        if (betweenness.Length == 0)
        {
            return 0;
        }

        var mean = betweenness.Average();
        var variance = betweenness.Sum(b => (b - mean) * (b - mean)) / betweenness.Length;
        return mean + Math.Sqrt(variance);
    }
}
