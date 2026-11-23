using System.Globalization;
using ONA.Insights.Application.Ports;

namespace ONA.Insights.Infrastructure.Generation;

/// <summary>Deterministic text generator. A future LLM adapter implements the same port.</summary>
internal sealed class RuleBasedInsightGenerator : IInsightGeneratorPort
{
    public Task<string> GenerateAsync(InsightRequest request, CancellationToken cancellationToken = default)
    {
        var ci = CultureInfo.InvariantCulture;
        var ratio = request.AverageBetweenness > 0 ? request.Betweenness / request.AverageBetweenness : 0;
        var share = ratio > 0
            ? $"{ratio.ToString("0.0", ci)}x the average"
            : "far above the average";

        var text =
            $"{request.NodeName} is a likely communication bottleneck: it lies on a disproportionate share of " +
            $"the shortest paths between colleagues (betweenness {FormatBetweenness(request.Betweenness, ci)}, {share}) " +
            $"with {request.InDegree} incoming and {request.OutDegree} outgoing connections " +
            $"(network average {request.AverageDegree.ToString("0.#", ci)} per person, {request.NodeCount} people). " +
            "Consider reviewing which decisions can be delegated and adding direct connections between " +
            "the teams that now depend on this person. " +
            "Administrative load can inflate this score, so a human must verify the finding before acting.";

        return Task.FromResult(text);
    }

    /// <summary>Normalised betweenness shrinks with graph size; keep tiny values readable instead of rounding to 0.</summary>
    private static string FormatBetweenness(double value, CultureInfo ci) =>
        value > 0 && value < 0.001 ? value.ToString("0.0e+0", ci) : value.ToString("0.###", ci);
}
